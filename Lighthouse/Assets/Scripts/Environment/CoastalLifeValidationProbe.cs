#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace LighthouseGame
{
    // Temporary Play Mode integration probe, following the project's existing QA
    // convention. Never placed in the saved scene or compiled into a player.
    public sealed class CoastalLifeValidationProbe : MonoBehaviour
    {
        public bool Finished, Passed;
        public string Stage, Result;
        private readonly List<string> _checks = new List<string>();
        private readonly List<float> _frameTimes = new List<float>();
        private CoastalGull[] _birds;
        private Vector3[] _previous;
        private float _maxStep, _maxPeak, _maxRms;
        private EnvironmentAudioMeter _meter;
        private int _frames;

        [Serializable] private class Evidence
        {
            public bool Passed;
            public string[] Checks;
            public int Frames, Birds, NaturalLandings, ObstacleAvoidance;
            public float MaxBirdStep, Peak, Rms, MedianMs, P95Ms;
        }

        private void Check(bool condition, string label)
        {
            _checks.Add((condition ? "PASS " : "FAIL ") + label);
            Debug.Log("COASTAL_LIFE_QA " + _checks[_checks.Count - 1]);
        }

        private void Update()
        {
            if (_birds == null || Finished) return;
            _frames++;
            _frameTimes.Add(Time.unscaledDeltaTime * 1000);
            for (int i = 0; i < _birds.Length; i++)
            {
                if (!_birds[i]) continue;
                _maxStep = Mathf.Max(_maxStep, Vector3.Distance(_previous[i], _birds[i].transform.position));
                _previous[i] = _birds[i].transform.position;
            }
            if (_meter)
            {
                _maxPeak = Mathf.Max(_maxPeak, _meter.Peak);
                _maxRms = Mathf.Max(_maxRms, _meter.Rms);
            }
        }

        private IEnumerator Start()
        {
            var env = CinematicEnvironment.Active;
            var session = env.Session;
            var player = session.Player;
            var sounds = FindAnyObjectByType<KeeperSoundscape>();
            player.InputEnabled = false;
            player.enabled = false;
            player.Teleport(new Vector3(0,4.25f,8), new Vector3(-7,4.5f,6));
            session.Running = false;
            env.AdvanceTime = false;
            env.SetHour(14);
            env.SetWeather(CoastalWeather.Clear,true);
            if (!player.UIHidden) player.ToggleUI();
            yield return new WaitForSeconds(.5f);
            _birds = FindObjectsByType<CoastalGull>();
            _previous = _birds.Select(b => b.transform.position).ToArray();
            _meter = player.View.GetComponent<EnvironmentAudioMeter>();
            Check(_birds.Length == 7, "Seven resident birds loaded from the saved scene");
            Check(_birds.All(b => b.enabled && b.Perch && b.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh.blendShapeCount == 3),
                "All birds have a perch and three imported wing poses");
            Check(_birds.All(b => b.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterials.All(m=>m && m.shader.name=="HDRP/Lit")),
                "Imported gull materials use HDRP/Lit");
            Check(_birds.All(b => b.GetComponentsInChildren<Collider>().Length == 0), "Birds do not block player movement or interactions");
            Check(_birds.All(b => Physics.Raycast(b.Perch.position + Vector3.up * .2f, Vector3.down,.4f,~(1<<2),QueryTriggerInteraction.Ignore)),
                "All seven landing points have real collider support");
            var bird = _birds.First(b => b.name == "Seagull 1");
            Vector3 home = bird.Perch.position;
            Stage = "Approach and takeoff";
            Check(bird.CurrentActivity == CoastalGull.Activity.Resting, "Target begins resting on the terrace");
            int startled = bird.StartleCount;
            player.Teleport(home + new Vector3(-2.6f,.08f,.9f), home+Vector3.up*.3f);
            yield return new WaitForSeconds(.65f);
            Check(bird.StartleCount == startled+1 && bird.CurrentActivity == CoastalGull.Activity.TakingOff,
                "Approaching within 4.5 m triggers one takeoff");
            Check(bird.transform.position.y > home.y+.25f, "Takeoff lifts above the terrace");
            var wingSource = bird.GetComponentsInChildren<AudioSource>().First(a=>a.name.StartsWith("Wings"));
            Check(wingSource.isPlaying && wingSource.clip, "Takeoff plays the local wing recording");
            ScreenCapture.CaptureScreenshot("../Artifacts/CoastalLife/takeoff.png");
            yield return new WaitForSeconds(6);
            Check(bird.CurrentActivity == CoastalGull.Activity.Circling && bird.transform.position.y>13,
                "Frightened bird reaches its offshore circuit");
            Stage = "Player guards the perch";
            int landed = bird.LandingCount;
            yield return new WaitForSeconds(32);
            Check(bird.LandingCount==landed, "Bird does not land while the player occupies its return radius");

            Stage = "Natural return and landing";
            player.Teleport(new Vector3(-9,4.25f,-9),home+Vector3.up*.2f);
            float deadline=Time.time+55;
            while(Time.time<deadline && bird.LandingCount==landed) yield return null;
            Check(bird.LandingCount>landed && bird.CurrentActivity==CoastalGull.Activity.Resting,
                "After the player leaves, the bird naturally approaches and lands");
            Check(Vector3.Distance(bird.transform.position,home)<.03f,"Landing finishes on the supported perch");
            Check(bird.GetComponentInChildren<SkinnedMeshRenderer>().GetBlendShapeWeight(2)>80,
                "Landing folds the wings");
            player.Teleport(home+new Vector3(-2.6f,.08f,.9f),home+Vector3.up*.3f);
            yield return new WaitForSeconds(.65f);
            Check(bird.StartleCount==startled+2,"The same bird reacts again after returning");
            player.Teleport(new Vector3(-9,4.25f,-9),home+Vector3.up*.2f);

            Stage = "Shutter and station audio";
            session.BusyUntil=0;
            int photos=session.Shots.Count, clicks=sounds.ShutterCount;
            session.Capture();
            var shutter=player.View.GetComponentsInChildren<AudioSource>(true).First(a=>a.name.StartsWith("Camera ·"));
            Check(session.Shots.Count==photos+1 && sounds.ShutterCount==clicks+1 && shutter.isPlaying,
                "Accepted photograph plays the mechanical shutter exactly once");
            session.Capture();
            Check(sounds.ShutterCount==clicks+1,"Busy capture does not play a false shutter");
            yield return new WaitForSeconds(.25f);
            session.Sleeping=true;session.BusyUntil=0;session.Capture();session.Sleeping=false;
            session.GameOver=true;session.Capture();session.GameOver=false;
            Check(sounds.ShutterCount==clicks+1,"Sleep and game-over reject silent capture requests");
            while(session.Shots.Count<5)
            {
                session.BusyUntil=0;session.Capture();yield return new WaitForSeconds(.3f);
            }
            session.BusyUntil=0;clicks=sounds.ShutterCount;session.Capture();
            Check(session.Shots.Count==5 && sounds.ShutterCount==clicks,"Five-photo limit is preserved without extra shutter sounds");

            var radio=FindObjectsByType<BlockoutInteractable>().First(x=>x.Kind==BlockoutInteractable.Station.Radio);
            player.Teleport(new Vector3(3.4f,9.94f,-2.8f),radio.transform.position+Vector3.up*1.2f);
            session.Work(radio);yield return new WaitForSeconds(.2f);
            Check(session.Radio.ControlsOpen && !session.Busy && session.Shots.All(s=>s.Sent),"Radio opens independently of automatically confirmed photos");
            session.Radio.SetPower(false);
            Check(!radio.GetComponentsInChildren<AudioSource>().Any(a=>a.isPlaying),"Off stops every receiver sound");
            session.Radio.SetPower(true); session.Radio.CloseControls(); yield return null;
            Check(!session.Radio.ControlsOpen && session.Radio.IsPowered,"Closing the panel leaves receiver power unchanged");

            var lamp=FindObjectsByType<BlockoutInteractable>().First(x=>x.Kind==BlockoutInteractable.Station.Lamp);
            var burner=lamp.GetComponentsInChildren<AudioSource>().First(a=>a.name.Contains("burner"));
            session.BusyUntil=0;session.Work(lamp);yield return new WaitForSeconds(.1f);
            Check(lamp.GetComponentsInChildren<AudioSource>().Any(a=>a.name.Contains("lever")&&a.isPlaying),"Lamp servicing plays a local lever sound");
            Check(burner.isPlaying && burner.volume>0,"Working lamp has a quiet burner loop");
            bool directorEnabled=session.Director.enabled;
            session.Director.enabled=false;session.Director.LampLight.enabled=false;
            yield return new WaitForSeconds(1.5f);
            Check(burner.volume<.002f,"Burner fades when the actual lamp light goes out");
            session.Director.LampLight.enabled=true;session.Director.enabled=directorEnabled;
            var door=FindObjectsByType<BlockoutInteractable>().First(x=>x.Kind==BlockoutInteractable.Station.Door);
            door.Use(session);yield return null;
            Check(door.GetComponentInChildren<AudioSource>().isPlaying,"Opening the existing door plays its hinge sound");
            door.Use(session);

            Stage="Shelter, weather and lifecycle";
            player.Teleport(new Vector3(0,9.94f,0),new Vector3(0,11,-20));
            yield return new WaitForSeconds(2);
            Check(env.Ambience.Indoors && _birds.All(b=>b.GetComponentsInChildren<AudioLowPassFilter>().All(f=>f.cutoffFrequency<3500)),
                "Gull sounds are muffled inside the lighthouse");
            player.Teleport(new Vector3(-7,4.25f,6),new Vector3(-12,8,15));
            env.SetWeather(CoastalWeather.Storm,true);yield return new WaitForSeconds(4);
            Check(!env.Ambience.Indoors && _birds.All(b=>b.GetComponentsInChildren<AudioLowPassFilter>().All(f=>f.cutoffFrequency>14000)),
                "Returning outside restores the open-air bird mix");
            Check(_birds.All(b=>b.transform.position.y>3.9f && !float.IsNaN(b.transform.position.x)),"All birds remain above the sea with finite positions");
            Check(FindObjectsByType<CoastalGull>().Length==7,"Long-running colony does not duplicate or despawn birds");
            Check(_maxStep<1.8f,"Flight remains continuous without position jumps (maximum sampled step <1.8 m)");
            Check(_maxPeak>.001f && _maxPeak<.98f && _maxRms>.0001f,"AudioListener receives an audible mix without clipping in this run");
            Check(_birds.Sum(b=>b.LandingCount)>=3,"Several birds complete natural landing cycles");

            _frameTimes.Sort();
            Passed=_checks.All(c=>c.StartsWith("PASS"));Finished=true;Stage="Complete";Result=string.Join("\n",_checks);
            var evidence=new Evidence{Passed=Passed,Checks=_checks.ToArray(),Frames=_frames,Birds=_birds.Length,
                NaturalLandings=_birds.Sum(b=>b.LandingCount),ObstacleAvoidance=_birds.Sum(b=>b.ObstacleAvoidanceCount),
                MaxBirdStep=_maxStep,Peak=_maxPeak,Rms=_maxRms,MedianMs=_frameTimes[_frameTimes.Count/2],P95Ms=_frameTimes[(int)(_frameTimes.Count*.95f)]};
            Directory.CreateDirectory("../Artifacts/CoastalLife");
            File.WriteAllText("../Artifacts/CoastalLife/playmode-verification.json",JsonUtility.ToJson(evidence,true));
            env.SetWeather(CoastalWeather.Clear,true);env.SetHour(17.2f);
            player.enabled=true;player.InputEnabled=true;
            Debug.Log("COASTAL_LIFE_QA_COMPLETE "+Passed);
        }
    }
}
#endif
