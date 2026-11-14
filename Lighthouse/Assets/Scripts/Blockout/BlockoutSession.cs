using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LighthouseGame
{
    [Serializable] public class BlockoutShot
    {
        public string Label, Key;
        public int Price, Repeat, LightningBonus, LightningStrikeId;
        public bool Sent;
        public List<PhotoSubject> Subjects = new List<PhotoSubject>();
        public Texture2D Image;
    }
    [Serializable] public class PhotoSubject { public string Key, Label; public int Price; }
    public class BlockoutSession : MonoBehaviour
    {
        public event Action RunBegan;
        public event Action DayBegan;
        public event Action StateChanged;
        public event Action PhotosChanged;
        public event Action PhotoVisibilityChanged;
        public event Action EffectsChanged;
        public event Action LampRepairProgressChanged;
        public event Action<BlockoutShot, bool> PhotoCaptured;
        public event Action MenuRequested;
        public bool MenuOpen { get; internal set; }
        public KeeperRadio Radio { get; internal set; }
        public void RequestMenu() => MenuRequested?.Invoke();
        private int _presentationHash;
        private Vector2 _lastEffects;
        private float _lampRepairElapsed, _lastLampRepairProgress;
        private const float LampRepairReach = 4.2f;
        private const int PhotoWidth = 1024, PhotoHeight = 576;
        public BlockoutPlayer Player;
        public BlockoutDirector Director;
        public ObservationDirector Observations;
        [SerializeField] private KeeperSoundscape _soundscape;
        public Transform Observation, SeaEvent;
        public List<BlockoutShot> Shots = new List<BlockoutShot>();
        public bool Running, GameOver, Sleeping, Storm, HelpVisible = true;
        public int Day = 1, Score, Confirmed, PaidHundreds, Best;
        public float Fuel = 120, Lamp = 60, NightTime, Preparation = 15, Darkness, BusyUntil, Fade, Flash;
        public string Message = "Свободный осмотр. F1 — начать вахту, F2 — погода, F3 — время суток.";
        public float MessageUntil;
        public float StartFuel = 120;
        public float LampInterval = 60;
        public string EndReason { get; private set; } = "";
        readonly Dictionary<string, int> repeats = new Dictionary<string, int>();
        System.Random random;
        private CoastalGull[] _gulls;
        private WeepingGirlEncounter _weepingGirl;
        private LeviathanEncounter _leviathan;
        void Awake()
        {
            random = new System.Random(); Best = PlayerPrefs.GetInt("Lighthouse.Blockout.Best", 0);
            _gulls = FindObjectsByType<CoastalGull>(FindObjectsInactive.Include);
            _weepingGirl = FindAnyObjectByType<WeepingGirlEncounter>(FindObjectsInactive.Include);
            _leviathan = FindAnyObjectByType<LeviathanEncounter>(FindObjectsInactive.Include);
        }
        public int DayScore { get { int sum = 0; foreach (var s in Shots) sum += s.Price; return sum; } }
        public const int MaximumActiveEvents = 3;
        public bool IsTutorialDay => Running && Day == 1;
        public bool SpecialEventsUnlocked => !Running || Day >= 2;
        public int ActiveEventCount
        {
            get
            {
                int count = Observations ? Observations.ActiveCount : 0;
                if (Director) count += Director.ActiveEventCount;
                if (_weepingGirl && _weepingGirl.OccupiesEventSlot) count++;
                if (_leviathan && _leviathan.Showing) count++;
                var environment = CinematicEnvironment.Active;
                if (environment && environment.LightningActive) count++;
                return count;
            }
        }
        public bool CanStartEvent => !Running || (SpecialEventsUnlocked && !GameOver && !Sleeping
            && Preparation <= 0 && ActiveEventCount < MaximumActiveEvents);
        public int Bonus => 10 * Mathf.Max(0, Score / 100 - PaidHundreds);
        public bool IsRepairingLamp { get; private set; }
        public BlockoutInteractable RepairStation { get; private set; }
        public float LampRepairProgress => IsRepairingLamp ? Mathf.Clamp01(_lampRepairElapsed / BeaconMaintenance.RepairSeconds) : 0;
        public float LampWarningRemaining => Mathf.Max(0, BeaconMaintenance.WarningSeconds - Darkness);
        public bool LampNeedsRepair => Running && !GameOver && Lamp <= 0;
        public bool Busy => IsRepairingLamp || Time.time < BusyUntil;
        public void Notify(string value) { Message = value; MessageUntil = Time.time + 5; StateChanged?.Invoke(); }
        public void BeginRun()
        {
            StopAllCoroutines(); pendingPhotos=0; ClearShots(); repeats.Clear();
            ClearLampRepair();
            Running = true; GameOver = false; Sleeping = false; Fuel = StartFuel; Lamp = LampInterval;
            Player.RefreshCameraModel(); PhotoVisibilityChanged?.Invoke();
            EndReason = "";
            Darkness = 0; Preparation = 15; Day = 1; Score = Confirmed = PaidHundreds = 0; NightTime = 0; BusyUntil = 0; Fade = 0;
            RunBegan?.Invoke();
            if(CinematicEnvironment.Active&&CinematicEnvironment.Active.Bolt) CinematicEnvironment.Active.Bolt.Clear();
            if(Observations)Observations.Automatic=true;
            Storm = false;
            if (CinematicEnvironment.Active) CinematicEnvironment.Active.BeginDay(Day);
            Director.BeginDay(random.NextDouble() < .65); HelpVisible = false;
            Player.Teleport(Observation.position, SeaEvent.position + Vector3.up * 3); Player.LockCursor(true);
            Message = string.Empty; MessageUntil = 0; StateChanged?.Invoke();
        }
        public void TogglePreviewWeather() { if(CinematicEnvironment.Active){CinematicEnvironment.Active.NextWeather();Director.BeginDay(true);return;} Storm = !Storm; Director.BeginDay(true); Notify(Storm ? "Шторм и кракен." : "Спокойная вода и корабль."); }
        void Update()
        {
            if (MenuOpen) return;
            Flash = Mathf.MoveTowards(Flash, 0, Time.deltaTime * 5);
            if (!Running || GameOver || Sleeping) return;
            if (Preparation > 0) { Preparation = Mathf.Max(0, Preparation - Time.deltaTime); return; }
            NightTime += Time.deltaTime; Fuel = Mathf.Max(0, Fuel - Time.deltaTime);
            if (Fuel <= 0) { EndRun("Топливо кончилось. Маяк погас."); return; }
            bool repairedThisFrame = AdvanceLampRepair(Time.deltaTime);
            BeaconMaintenance.Tick(ref Lamp, ref Darkness, Time.deltaTime, repairedThisFrame);
            if (Darkness >= BeaconMaintenance.WarningSeconds) EndRun("Не успели починить маяк.");
        }
        void LateUpdate()
        {
            // Publish displayed counters only when their visible values change.
            int hash = HashCode.Combine(Mathf.CeilToInt(Fuel), Score, GameOver, Running,
                Mathf.CeilToInt(Preparation), Mathf.CeilToInt(Mathf.Max(0, BusyUntil - Time.time)),
                LampNeedsRepair ? Mathf.CeilToInt(LampWarningRemaining) : 0, Time.time < MessageUntil);
            if (hash != _presentationHash) { _presentationHash = hash; StateChanged?.Invoke(); }
            float repairProgress = LampRepairProgress;
            if (repairProgress != _lastLampRepairProgress)
            { _lastLampRepairProgress = repairProgress; LampRepairProgressChanged?.Invoke(); }
            var effects = new Vector2(Fade, Flash);
            if (effects != _lastEffects) { _lastEffects = effects; EffectsChanged?.Invoke(); }
        }
        public void EndRun(string reason)
        {
            if (!Running || GameOver) return;
            EndReason = reason;
            GameOver = true; StopAllCoroutines(); Sleeping = false; Fade = 0; BusyUntil = 0;
            ClearLampRepair();
            Best = Mathf.Max(Best, Score); PlayerPrefs.SetInt("Lighthouse.Blockout.Best", Best); PlayerPrefs.Save();
            Player.LockCursor(false); Notify(reason + " F1 — новый забег.");
        }
        public int Price(string key, int basePrice) => Mathf.FloorToInt(basePrice * Mathf.Pow(.5f, repeats.TryGetValue(key, out int count) ? count : 0));
        public void Capture()
        {
            if (MenuOpen) return;
            if (GameOver || Busy || Sleeping || Player.EncounterControlLocked || (Radio && Radio.ControlsOpen) || (Running && Preparation > 0)) return;
            if (Shots.Count >= 5) { Notify("Все 5 кадров использованы. Можно идти спать или продолжить наблюдение."); return; }
            Director.PhotoTarget(Player.View, out string key, out string label, out int basic);
            int repeat = repeats.TryGetValue(key, out int n) ? n : 0;
            int price = Price(key, basic);
            var subjects = new List<PhotoSubject>();
            if(basic>0)subjects.Add(new PhotoSubject{Key=key,Label=label,Price=price});
            repeats[key] = repeat + 1;
            if(Observations)
            {
                var visible=new List<ObservationVfx>();Observations.CollectVisible(Player.View,visible);
                foreach(var e in visible)
                {
                    string eventKey=e.Definition.Key;int value=Price(eventKey,e.Definition.Points);
                    repeats[eventKey]=(repeats.TryGetValue(eventKey,out int count)?count:0)+1;
                    subjects.Add(new PhotoSubject{Key=eventKey,Label=e.Definition.Name,Price=value});
                    if(basic==0&&subjects.Count==1){key=eventKey;label=e.Definition.Name;}else label+=" + "+e.Definition.Name;
                    price+=value;e.HoldForPhoto();
                }
            }
            var bolt = CinematicEnvironment.Active ? CinematicEnvironment.Active.Bolt : null;
            var ghost=Observations?Observations.Ghost:null;
            if(ghost&&ghost.VisibleIn(Player.View))
            {
                int value=Price("ghostship",SkyGhostShip.Points);repeats["ghostship"]=(repeats.TryGetValue("ghostship",out int count)?count:0)+1;
                label=subjects.Count>0?label+" + призрачный корабль":"Небесный призрачный корабль";if(subjects.Count==0)key="ghostship";
                subjects.Add(new PhotoSubject{Key="ghostship",Label="Небесный призрачный корабль",Price=value});price+=value;ghost.HoldForPhoto();
            }
            foreach (var gull in _gulls)
            {
                if (!gull || !gull.VisibleIn(Player.View)) continue;
                AddPhotoBonus(subjects, ref key, ref label, ref price, "gulls", "Чайки", CoastalGull.PhotoPoints);
                gull.HoldForPhoto();
                break;
            }
            if (_weepingGirl && _weepingGirl.VisibleIn(Player.View))
                AddPhotoBonus(subjects, ref key, ref label, ref price, "weeping-girl", "Дух плачущей девочки", WeepingGirlEncounter.PhotoPoints);
            if (_leviathan && _leviathan.VisibleIn(Player.View))
            {
                AddPhotoBonus(subjects, ref key, ref label, ref price, LeviathanEncounter.PhotoKey, "Прыжок левиафана", LeviathanEncounter.PhotoPoints);
                _leviathan.HoldForPhoto();
            }
            int lightningBonus = 0, lightningStrikeId = 0;
            if(bolt && bolt.VisibleIn(Player.View))
            {
                lightningBonus=Price("lightning",bolt.PhotoBonus);lightningStrikeId=bolt.StrikeId;
                repeats["lightning"]=(repeats.TryGetValue("lightning",out int lightningRepeat)?lightningRepeat:0)+1;
                bolt.HoldForPhoto();price+=lightningBonus;
                label=subjects.Count>0?label+" + молния":"Молния";
                subjects.Add(new PhotoSubject{Key="lightning",Label="Молния",Price=lightningBonus});
            }
            var shot = new BlockoutShot { Key = key, Label = label, Repeat = repeat, Price = price, Subjects=subjects, LightningBonus=lightningBonus, LightningStrikeId=lightningStrikeId, Image = new Texture2D(PhotoWidth,PhotoHeight,TextureFormat.RGB24,false) };
            Shots.Add(shot); Score += price; Report();
            if (_soundscape) _soundscape.PhotoTaken();
            StartCoroutine(TakeImage(shot)); Player.ShutterKick(); BusyUntil = Time.time + 2;
            Notify(label + ": +" + price + " очк." + (lightningBonus>0?" (молния +"+lightningBonus+")":"") + " Осталось кадров: " + (5 - Shots.Count));
        }
        private void AddPhotoBonus(List<PhotoSubject> subjects, ref string key, ref string label, ref int price,
            string bonusKey, string bonusLabel, int basePrice)
        {
            int value = Price(bonusKey, basePrice);
            repeats[bonusKey] = (repeats.TryGetValue(bonusKey, out int count) ? count : 0) + 1;
            if (subjects.Count == 0) { key = bonusKey; label = bonusLabel; }
            else label += " + " + bonusLabel;
            subjects.Add(new PhotoSubject { Key = bonusKey, Label = bonusLabel, Price = value });
            price += value;
        }
        int pendingPhotos;
        public bool TakingPhoto => pendingPhotos>0;
        IEnumerator TakeImage(BlockoutShot shot)
        {
            bool captured = false, fromRun = Running && !GameOver;
            // Capture the actual post-processed frame. A separate HDRP RenderRequest has
            // independent exposure/cloud history and can disagree with the viewfinder.
            pendingPhotos++;
            Player.RefreshCameraModel(); PhotoVisibilityChanged?.Invoke();
            RenderTexture full=null,small=null;
            RenderTexture oldActive=null;
            try
            {
                yield return new WaitForEndOfFrame();
                if(!shot.Image)yield break;
                oldActive=RenderTexture.active;
                full=RenderTexture.GetTemporary(Screen.width,Screen.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
                small=RenderTexture.GetTemporary(PhotoWidth,PhotoHeight,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
                ScreenCapture.CaptureScreenshotIntoRenderTexture(full);
                if(SystemInfo.graphicsUVStartsAtTop)Graphics.Blit(full,small,new Vector2(1,-1),new Vector2(0,1));
                else Graphics.Blit(full,small);
                RenderTexture.active=small;shot.Image.ReadPixels(new Rect(0,0,PhotoWidth,PhotoHeight),0,0);shot.Image.Apply();
                captured = true;
            }
            finally
            {
                RenderTexture.active=oldActive;if(full)RenderTexture.ReleaseTemporary(full);if(small)RenderTexture.ReleaseTemporary(small);
                pendingPhotos=Mathf.Max(0,pendingPhotos-1);Flash=.7f;
                Player.RefreshCameraModel(); PhotosChanged?.Invoke(); PhotoVisibilityChanged?.Invoke();
                if (captured) PhotoCaptured?.Invoke(shot, fromRun);
            }
        }
        public void Work(BlockoutInteractable station)
        {
            if (MenuOpen) return;
            if (!station || Busy || Sleeping || GameOver || Player.EncounterControlLocked) return;
            if (station.Kind == BlockoutInteractable.Station.Lamp)
            {
                if (!LampNeedsRepair || Preparation > 0 || Darkness >= BeaconMaintenance.WarningSeconds) return;
                if (Vector3.Distance(Player.transform.position, station.transform.position) > LampRepairReach) return;
                RepairStation = station; IsRepairingLamp = true; _lampRepairElapsed = 0;
                BusyUntil = Time.time + BeaconMaintenance.RepairSeconds;
                station.Animate(BeaconMaintenance.RepairSeconds);
                if (_soundscape) _soundscape.WorkStarted(station.Kind, BeaconMaintenance.RepairSeconds);
                Notify("Ремонт маяка — 6 секунд.");
                return;
            }
            if (station.Kind == BlockoutInteractable.Station.Radio)
            {
                Radio?.RequestControls();
                return;
            }
            if (station.Kind == BlockoutInteractable.Station.Bed && !Running) return;
            StartCoroutine(DoWork(station));
        }
        private bool AdvanceLampRepair(float seconds)
        {
            if (!IsRepairingLamp) return false;
            if (!RepairStation || Vector3.Distance(Player.transform.position, RepairStation.transform.position) > LampRepairReach)
            {
                ClearLampRepair();
                Notify("Ремонт прерван: вернитесь к маяку.");
                return false;
            }

            _lampRepairElapsed += seconds;
            BusyUntil = Time.time + Mathf.Max(0, BeaconMaintenance.RepairSeconds - _lampRepairElapsed);
            if (_lampRepairElapsed >= BeaconMaintenance.RepairSeconds)
            {
                Lamp = LampInterval; Darkness = 0;
                ClearLampRepair();
                Notify("Маяк починен. Топливо остаётся прежним.");
            }
            return true;
        }
        private void ClearLampRepair()
        {
            IsRepairingLamp = false; RepairStation = null; _lampRepairElapsed = 0; BusyUntil = 0;
        }
        IEnumerator DoWork(BlockoutInteractable station)
        {
            if (station.Kind == BlockoutInteractable.Station.Bed)
            {
                Sleeping = true;
                Player.CloseInspection();
                Radio?.CloseControls();
                Radio?.Silence();
                station.Animate(1.45f);
                Player.RefreshCameraModel(); PhotoVisibilityChanged?.Invoke();
                // The day changes only behind a fully opaque frame; sleeping suspends all timers.
                for (float elapsed = 0; elapsed < .65f; elapsed += Time.unscaledDeltaTime)
                { Fade = Mathf.SmoothStep(0, 1, elapsed / .65f); yield return null; }
                Fade = 1;
                yield return null;
                Report(); int bonus = 10 * Mathf.Max(0, Confirmed / 100 - PaidHundreds); PaidHundreds = Confirmed / 100;
                Fuel += bonus; ClearShots(); repeats.Clear(); Day++; NightTime = 0; Preparation = 0;
                if (CinematicEnvironment.Active) CinematicEnvironment.Active.BeginDay(Day);
                else Storm = random.NextDouble() < .45;
                Director.BeginDay(random.NextDouble() < .65);
                DayBegan?.Invoke();
                yield return new WaitForSecondsRealtime(.15f);
                for (float elapsed = 0; elapsed < .65f; elapsed += Time.unscaledDeltaTime)
                { Fade = 1 - Mathf.SmoothStep(0, 1, elapsed / .65f); yield return null; }
                Sleeping = false; Fade = 0; BusyUntil = 0;
                Player.RefreshCameraModel(); PhotoVisibilityChanged?.Invoke();
                Notify("День " + Day + ". Топливо: остаток + " + bonus + " секунд.");
            }
        }
        void Report() { foreach (var shot in Shots) if (!shot.Sent) { shot.Sent = true; Confirmed += shot.Price; } PhotosChanged?.Invoke(); }
        void ClearShots() { foreach (var shot in Shots) if (shot.Image) Destroy(shot.Image); Shots.Clear(); PhotosChanged?.Invoke(); }
        void OnDestroy() { ClearShots(); }
    }
}
