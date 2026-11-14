using System.Linq;
using UnityEngine;

namespace LighthouseGame
{
    [DefaultExecutionOrder(70)]
    public class BlockoutDirector : MonoBehaviour
    {
        public BlockoutSession Session;
        public Transform Boat, Kraken, Shoal, SeaAnchor;
        public Light Sun, LampLight, BeamLight;
        public Renderer BeamMesh, FlameMesh;
        public Material Water, Sky;
        public bool WillEat = true;
        public string EventLabel = "Горизонт";
        public bool BoatVisible, KrakenVisible, Feast, ShoalVisible;
        public KrakenEncounter Encounter;
        public MarineTraffic Traffic;
        public int ActiveEventCount => (Encounter ? (Encounter.IsActive ? 1 : 0)
            : (BoatVisible || KrakenVisible ? 1 : 0)) + (ShoalVisible ? 1 : 0);
        bool shoalAdmitted;Vector3 shoalAnchor;
        private int _shoalForecastCycle = -1;
        float previewClock;
        Transform[] tentacles;
        Quaternion[] rest;
        Vector3 outward, across;
        public float EventTime => Session.Running ? Session.NightTime : previewClock;
        void Awake()
        {
            outward = new Vector3(SeaAnchor.position.x, 0, SeaAnchor.position.z).normalized;
            across = Vector3.Cross(Vector3.up, outward);
            tentacles = Kraken.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("ANIM_Tentacle_")).ToArray();
            rest = tentacles.Select(t => t.localRotation).ToArray();
        }
        public bool FreezePreview;
        public void BeginDay(bool eat) { previewClock = 0; _shoalForecastCycle = -1; WillEat = eat; FreezePreview=false; shoalAdmitted=false;ShoalVisible=false;Shoal.gameObject.SetActive(false);if(Encounter)Encounter.BeginNight();if(Session.Observations)Session.Observations.ResetDay();var fleet=Boat.GetComponent<CoastalFleet>();if(fleet)fleet.Select(Random.Range(1,fleet.Vessels.Length)); }
        public void Evaluate(float clock)
        {
            float t = clock % 78f;
            int cycle = Mathf.FloorToInt(clock / 78);
            if (Session.CanStartEvent && !Session.Storm && t >= 25 && t < 36 && cycle != _shoalForecastCycle)
            { _shoalForecastCycle = cycle; Session.Radio?.ConsiderForecast("shoal", 36 - t); }
            bool ready = Session.SpecialEventsUnlocked && (!Session.Running || Session.Preparation <= 0);
            if(Encounter)Encounter.Evaluate(clock,ready);
            else
            {
            bool marineReady = ready && (BoatVisible || KrakenVisible || Session.CanStartEvent);
            BoatVisible = marineReady && t >= 6 && t < (Session.Storm ? 54 : 34);
            KrakenVisible = marineReady && Session.Storm && t >= 17 && t < 56;
            Feast = BoatVisible && KrakenVisible && WillEat && t >= 36 && t < 49;
            Boat.gameObject.SetActive(BoatVisible); Kraken.gameObject.SetActive(KrakenVisible); Shoal.gameObject.SetActive(ShoalVisible);
            var center = outward * 67;
            float travel = Mathf.Lerp(-30, 24, Mathf.Clamp01((t - 6) / 40));
            Vector3 boatPosition = center + across * travel + Vector3.up * (.35f + (CinematicEnvironment.Active ? 0 : Mathf.Sin(clock * 1.4f) * (Session.Storm ? .55f : .15f)));
            if (Feast) boatPosition = Vector3.Lerp(boatPosition, center + across * 3 + Vector3.up * -1.5f, Mathf.Clamp01((t - 36) / 13));
            Boat.position = boatPosition;
            Boat.rotation = Quaternion.LookRotation(-across) * Quaternion.Euler(CinematicEnvironment.Active ? 0 : Mathf.Sin(clock * 1.3f) * 2, 0, Feast ? (t - 36) * 3 : CinematicEnvironment.Active ? 0 : Mathf.Sin(clock) * (Session.Storm ? 9 : 2));
            // A rejected encounter still resolves visibly: the animal dives away.
            float rise = Mathf.Clamp01((t - 17) / 4) * Mathf.Clamp01((56 - t) / (WillEat ? 6 : 18));
            Kraken.position = center + Vector3.up * Mathf.Lerp(-10, -.2f, rise);
            Kraken.rotation = Quaternion.LookRotation(-outward) * Quaternion.Euler(0, Mathf.Sin(clock * .25f) * 9, 0);
            for (int i = 0; i < tentacles.Length; i++)
                tentacles[i].localRotation = rest[i] * Quaternion.Euler(Mathf.Sin(clock * (Feast ? 2.8f : 1.1f) + i) * (Feast ? 24 : 9), Mathf.Sin(clock + i * 2) * 8, Mathf.Cos(clock * .8f + i) * 9);
            }
            if(t<36||t>=57)shoalAdmitted=false;
            if(ready&&Session.CanStartEvent&&!Session.Sleeping&&!Session.Storm&&t>=36&&t<57&&!shoalAdmitted)shoalAdmitted=SeaPlacement.TryFind(Session.Player.View,outward,65,105,new Vector3(42,15,42),4,out shoalAnchor);
            ShoalVisible=ready&&t>=36&&t<57&&shoalAdmitted;Shoal.gameObject.SetActive(ShoalVisible);
            Shoal.position = shoalAnchor + across * Mathf.Sin(clock * .18f) * 10;
            for (int i = 0; i < Shoal.childCount; i++)
            {
                Transform fish = Shoal.GetChild(i);
                fish.localPosition = new Vector3((i - 3) * 1.8f, .15f + Mathf.Max(0, Mathf.Sin(clock * 2 + i)) * 2, Mathf.Sin(i * 2.1f) * 3);
                fish.localRotation = Quaternion.Euler(0, clock * 18 + i * 20, Mathf.Sin(clock * 2 + i) * 35);
            }
            EventLabel = !ready ? "Подготовка" : Feast ? "Кракен атакует корабль" : KrakenVisible ? "Кракен у маяка" : ShoalVisible ? "Светящаяся стая" : BoatVisible ? "Корабль на горизонте" : "Тихий горизонт";
            if(Encounter&&!ShoalVisible)EventLabel=Encounter.Phase;
        }
        void Update()
        {
            if (!Session.Running&&!FreezePreview) previewClock += Time.deltaTime;
            if (!Session.GameOver&&!FreezePreview) Evaluate(EventTime);
            if (!CinematicEnvironment.Active)
            {
            if (Water) Water.SetFloat("_WaveStrength", Session.Storm ? 1.15f : .28f);
            if (Sun)
            {
                Sun.intensity = Mathf.Lerp(Sun.intensity, Session.Running ? (Session.Storm ? .17f : .30f) : (Session.Storm ? .32f : .85f), Time.deltaTime);
                Sun.color = Color.Lerp(Sun.color, Session.Running ? new Color(.56f,.7f,1) : new Color(1,.82f,.63f),Time.deltaTime);
            }
            if (Sky) Sky.SetFloat("_Exposure",Mathf.Lerp(Sky.GetFloat("_Exposure"), Session.Running ? .38f : (Session.Storm ? .65f : 1.1f), Time.deltaTime));
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, Session.Running ? new Color(.055f,.10f,.16f) : (Session.Storm ? new Color(.20f,.27f,.31f) : new Color(.36f,.49f,.55f)),Time.deltaTime);
            RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, Session.Storm ? .005f : .0018f, Time.deltaTime);
            }
            if (LampLight) LampLight.enabled = !Session.Running || (Session.Fuel > 0 && (Session.Lamp > 0 || (int)(Time.time * 6) % 2 == 0) && !Session.GameOver);
            if (BeamLight) BeamLight.enabled = LampLight.enabled;
            if (BeamMesh) BeamMesh.enabled = LampLight.enabled;
            if (FlameMesh) FlameMesh.enabled = LampLight.enabled;
        }
        bool InFrame(Camera camera, Vector3 point)
        {
            Vector3 v = camera.WorldToViewportPoint(point);
            if (v.z <= .1f || v.x < .025f || v.x > .975f || v.y < .025f || v.y > .975f) return false;
            Vector3 delta = point - camera.transform.position;
            return !Physics.Raycast(camera.transform.position, delta.normalized, delta.magnitude - 2, ~(1 << 2), QueryTriggerInteraction.Ignore);
        }
        public void PhotoTarget(Camera camera, out string key, out string label, out int basic)
        {
            bool boat = BoatVisible && (Encounter?Encounter.ShipInFrame(camera):InFrame(camera, Boat.position + Vector3.up * 2));
            bool kraken = KrakenVisible && (Encounter?Encounter.CreatureInFrame(camera):InFrame(camera, Kraken.position + Vector3.up * 2));
            bool octopus=Encounter&&!Encounter.Colossal;
            if (Feast && boat && kraken) { key = octopus?"octopus-feast":"feast"; label = octopus?"Большой осьминог атакует катер":"Кракен разрушает корабль"; basic = octopus?220:300; }
            else if(Encounter&&Encounter.Escaping&&boat&&kraken){key=octopus?"octopus-escape":"escape";label=octopus?"Катер уходит от большого осьминога":"Корабль уходит от кракена";basic=octopus?140:180;}
            else if(Encounter&&Encounter.WreckInFrame(camera)){key="wreck";label="Обломки после нападения";basic=120;}
            else if (kraken) { key = octopus?"octopus":"kraken"; label = octopus?"Большой осьминог":"Кракен"; basic = octopus?100:140; }
            else if (ShoalVisible && InFrame(camera, Shoal.position + Vector3.up)) { key = "shoal"; label = "Светящаяся стая"; basic = 100; }
            else
            {
                var vessel = boat ? Boat.GetComponent<CoastalFleet>() : null;
                if (Traffic && Traffic.TryPhotoTarget(camera, out var passing) && (!vessel || passing.PhotoPoints > vessel.PhotoPoints))
                    vessel = passing;
                if (vessel) { key = vessel.PhotoKey; label = vessel.VesselName; basic = vessel.PhotoPoints; }
                else if (boat) { key = "boat"; label = "Лоцманский катер"; basic = 60; }
                else { key = "empty"; label = "Пустой кадр"; basic = 0; }
            }
        }
    }
}
