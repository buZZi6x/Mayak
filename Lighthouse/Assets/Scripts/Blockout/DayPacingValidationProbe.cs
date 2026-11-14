#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace LighthouseGame
{
    // Attach only in Play Mode. The scene and the player's photo archive are not saved.
    public sealed class DayPacingValidationProbe : MonoBehaviour
    {
        public bool Done, Passed;
        public string Stage;
        private BlockoutSession _session;
        private BlockoutDirector _director;
        private ObservationDirector _observations;
        private CinematicEnvironment _environment;
        private WeepingGirlEncounter _girl;
        private readonly List<string> _checks = new List<string>();
        private readonly List<string> _errors = new List<string>();
        private float _timeScale;
        private int _soakFrames, _maximumEvents, _soakSpawns, _weatherChanges;

        [Serializable] private sealed class Report
        {
            public bool Done, Passed;
            public string[] Checks, Errors;
            public int SoakFrames, MaximumEvents, SoakSpawns, WeatherChanges;
            public float SoakGameSeconds = 300;
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private WeepingGirlAppearance Appearance => (WeepingGirlAppearance)typeof(WeepingGirlEncounter)
            .GetField("_appearance", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_girl);

        private void Check(bool passed, string label)
        {
            _checks.Add((passed ? "PASS " : "FAIL ") + label);
            Debug.Log("DAY_PACING " + _checks[_checks.Count - 1]);
        }

        private void Log(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _errors.Add(message);
        }

        private IEnumerator Start()
        {
            _timeScale = Time.timeScale;
            Application.logMessageReceived += Log;
            var run = Run();
            while (true)
            {
                object current;
                try
                {
                    if (!run.MoveNext()) break;
                    current = run.Current;
                }
                catch (Exception exception)
                {
                    _checks.Add("FAIL " + Stage + ": " + exception);
                    break;
                }
                yield return current;
            }
            Time.timeScale = _timeScale;
            if (_session) _session.enabled = false;
            Application.logMessageReceived -= Log;
            Done = true;
            Passed = _errors.Count == 0 && _checks.All(c => c.StartsWith("PASS "));
            Stage = "Finished";
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Artifacts/DayPacing/playmode.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, JsonUtility.ToJson(new Report
            {
                Done = Done, Passed = Passed, Checks = _checks.ToArray(), Errors = _errors.ToArray(),
                SoakFrames = _soakFrames, MaximumEvents = _maximumEvents,
                SoakSpawns = _soakSpawns, WeatherChanges = _weatherChanges
            }, true));
        }

        private IEnumerator Run()
        {
            _session = FindAnyObjectByType<BlockoutSession>();
            _director = _session.Director;
            _observations = _session.Observations;
            _environment = CinematicEnvironment.Active;
            _girl = FindAnyObjectByType<WeepingGirlEncounter>();
            var menu = FindAnyObjectByType<KeeperMenu>();
            if (menu) menu.enabled = false; // Unsubscribe archive writes before taking test photos.
            _session.Player.enabled = false;
            _session.Player.InputEnabled = false;
            _environment.AdvanceTime = false;
            Time.timeScale = 1;

            Stage = "Restart from a storm into the tutorial";
            _session.Running = false;
            _environment.SetWeather(CoastalWeather.Storm, true);
            _environment.TriggerLightning();
            _observations.Spawn(0, true);
            _environment.AutoWeather = true;
            yield return null;
            _session.BeginRun();
            _session.Player.LockCursor(false);
            Check(_environment.Weather == CoastalWeather.Clear && !_session.Storm && _environment.RainAmount == 0,
                "Starting day 1 immediately replaces the previous storm with clear, dry weather");
            Check(!_environment.LightningActive && _observations.ActiveCount == 0 && !_girl.IsVisible
                && !_director.Encounter.IsActive && !_director.ShoalVisible, "A new run clears all previous significant events");
            _session.enabled = false;
            _session.Preparation = 0;
            int before = _observations.SpawnCount;
            int strikes = _environment.LightningCount;
            bool quiet = true;
            for (int i = 0; i < 96; i++)
            {
                _session.NightTime = i * 5;
                Set(_environment, "nextWeather", -1f);
                Set(_observations, "<NextAt>k__BackingField", -1f);
                _environment.SetWeather((CoastalWeather)(i % 5));
                _observations.Spawn(i % 8, true);
                _observations.SpawnGhost(true);
                _environment.TriggerLightning();
                yield return null;
                quiet &= _environment.Weather == CoastalWeather.Clear && _session.ActiveEventCount == 0;
            }
            Check(quiet && before == _observations.SpawnCount && strikes == _environment.LightningCount,
                "Day 1 stays clear and event-free across 480 seconds of timeline samples, overdue timers and forced requests");
            Check(FindObjectsByType<RainLens>().All(l => l.Wetness == 0) && _environment.RainParticles.particleCount == 0,
                "Restart leaves no rain particles or wet lens in the tutorial");
            Check(_director.Traffic.Ships.All(s => s.gameObject.activeInHierarchy)
                && FindObjectsByType<CoastalGull>().Length > 0 && _session.ActiveEventCount == 0,
                "Ordinary ships and gulls remain available without consuming event slots");
            Check(Enumerable.Range(0, 9).All(i => _observations.Weight(i) == 0), "All rare observation categories are excluded on day 1");

            Stage = "Real tutorial photo, lamp, radio and bed flow";
            _session.NightTime = 0;
            _session.Fuel = _session.Lamp = 1000;
            for (int i = 0; i < 5; i++)
            {
                var ship = _director.Traffic.Ships[i % _director.Traffic.Ships.Length];
                _session.Player.Teleport(ship.transform.position + Vector3.back * 90 + Vector3.up * 16,
                    ship.transform.position + Vector3.up * 14);
                _session.BusyUntil = 0;
                _session.Capture();
                yield return new WaitForEndOfFrame(); yield return null;
            }
            Check(_session.Shots.Count == 5 && _session.Shots.Any(s => s.Price > 0 && s.Image),
                "The tutorial supplies photographable ships and completes the real five-photo quota");
            var stations = FindObjectsByType<BlockoutInteractable>();
            var lamp = stations.First(s => s.Kind == BlockoutInteractable.Station.Lamp);
            _session.Player.Teleport(lamp.transform.position + Vector3.right * 1.5f, lamp.transform.position);
            _session.BusyUntil = 0; _session.Lamp = 0; _session.Darkness = 0;
            _session.Work(lamp);
            yield return new WaitForSeconds(6.2f);
            Check(!_session.IsRepairingLamp && _session.Lamp > _session.LampInterval - 1, "Lamp repair works during warning flashes on day 1");
            _session.Work(stations.First(s => s.Kind == BlockoutInteractable.Station.Radio));
            yield return null;
            Check(_session.Shots.All(s => s.Sent) && _session.Confirmed == _session.Score && !_session.Busy, "Tutorial photos confirm automatically; radio starts no timed report");
            _session.Radio.CloseControls();
            _session.Work(stations.First(s => s.Kind == BlockoutInteractable.Station.Bed));
            yield return new WaitForSecondsRealtime(1.7f);
            Check(_session.Day == 2 && _session.Shots.Count == 0 && !_session.Sleeping,
                "The real bed transition reaches day 2 and resets the photo quota");

            Stage = "Weather and day eligibility";
            _observations.Automatic = false;
            _director.enabled = false;
            _director.BeginDay(true);
            _environment.AutoWeather = false;
            Set(_environment, "rng", new System.Random(1432));
            var weatherKinds = new HashSet<CoastalWeather>();
            for (int i = 0; i < 100; i++) { _environment.BeginDay(2); weatherKinds.Add(_environment.Weather); }
            Check(weatherKinds.Count == 5, "New days can select all five weather profiles");
            _environment.SetHour(22);
            var allowed = new HashSet<int>();
            for (int weather = 0; weather < 5; weather++)
            {
                _environment.SetWeather((CoastalWeather)weather, true);
                for (int kind = 0; kind < 9; kind++) if (_observations.Weight(kind) > 0) allowed.Add(kind);
            }
            Check(allowed.Count == 9, "Day 2 unlocks all nine observation types under their existing weather/time conditions");
            bool changed = true;
            for (int i = 0; i < 8; i++)
            {
                var previous = _environment.Weather;
                Set(_environment, "nextWeather", -1f);
                yield return null;
                changed &= previous != _environment.Weather;
            }
            Check(changed, "During day 2 overdue weather transitions work with preview AutoWeather disabled");
            _environment.SetWeather(CoastalWeather.Clear, true);
            _environment.SetWeather(CoastalWeather.Rain);
            yield return null;
            Check(_environment.RainAmount > 0 && _environment.RainAmount < _environment.Presets[3].Rain,
                "Later weather changes blend precipitation instead of switching it instantly");
            _session.Sleeping = true;
            Set(_environment, "nextWeather", -1f);
            yield return null;
            Check(_environment.Weather == CoastalWeather.Rain && !_session.CanStartEvent, "Sleep blocks weather changes and new events");
            _session.Sleeping = false; _session.GameOver = true;
            yield return null;
            Check(_environment.Weather == CoastalWeather.Rain && !_session.CanStartEvent, "Game over blocks weather changes and new events");
            _session.GameOver = false;
            _environment.BeginDay(2);

            Stage = "Shared admission across marine, sky, shoal, lightning and girl";
            _observations.enabled = false;
            _environment.SetWeather(CoastalWeather.Clear, true);
            _session.Player.Teleport(_session.Observation.position, Vector3.forward * 500);
            _director.Encounter.BeginNight();
            for (int i = 0; i < 20 && !_director.Encounter.IsActive; i++) _director.Encounter.Evaluate(i * .1f, true);
            Check(_director.Encounter.IsActive, "A day-2 marine encounter can enter outside the camera view");
            _observations.Spawn(0, true); _observations.Spawn(1, true);
            Check(_session.ActiveEventCount == 3 && !_session.CanStartEvent, "One marine encounter and two phenomena consume exactly three slots");
            before = _observations.SpawnCount; strikes = _environment.LightningCount;
            _observations.Spawn(2, true); _observations.SpawnGhost(true); _environment.TriggerLightning();
            _director.Evaluate(40);
            Check(_observations.SpawnCount == before && _environment.LightningCount == strikes && !_director.ShoalVisible,
                "A full budget rejects extra phenomena, ghost ship, lightning and luminous shoal");
            _session.Day = 4; _environment.BeginDay(4);
            Appearance.BeginDay(4, 0); _girl.ResetEncounter();
            _session.Player.View.transform.rotation = Quaternion.LookRotation(_session.Player.View.transform.position - _girl.SeatedFocus);
            yield return null; yield return null;
            Check(Appearance.Appears && !_girl.IsVisible && _session.ActiveEventCount == 3,
                "A successful girl roll waits for capacity without rerolling the daily decision");
            _observations.Effects[1].Hide();
            yield return null; yield return null;
            Check(_girl.IsVisible && _session.ActiveEventCount == 3, "The waiting girl takes one released slot");
            _session.Day = 3; _environment.BeginDay(3); _girl.ResetEncounter();
            yield return null;
            Check(!_girl.IsVisible && !Appearance.Appears, "The girl remains unavailable before day 4");
            _director.BeginDay(true);
            _environment.SetWeather(CoastalWeather.Clear, true);
            _observations.Spawn(0, true); _observations.SpawnGhost(true);
            strikes = _environment.LightningCount;
            _environment.TriggerLightning();
            _director.Encounter.Evaluate(0, true); _director.Evaluate(40);
            Check(_environment.LightningCount == strikes + 1 && _session.ActiveEventCount == 3
                && !_director.Encounter.IsActive && !_director.ShoalVisible,
                "Lightning reserves the third slot immediately, before later systems in the same frame");
            yield return new WaitForSeconds(.3f);
            Check(_session.ActiveEventCount == 2 && _session.CanStartEvent, "Lightning expiry releases its slot");
            _observations.Clear();
            Check(_session.ActiveEventCount == 0, "Clearing phenomena and ghost ship releases all their slots");

            Stage = "Weather transitions preserve admitted encounters";
            _environment.SetWeather(CoastalWeather.Storm, true);
            _director.BeginDay(true);
            _session.Player.Teleport(_session.Observation.position, Vector3.forward * 500);
            float clock = 0;
            for (int i = 0; i < 20 && !_director.Encounter.IsActive; i++) _director.Encounter.Evaluate(clock += .5f, true);
            for (int i = 0; i < 90 && _director.Encounter.Clock < 38; i++)
            {
                _session.Player.View.transform.rotation = Quaternion.LookRotation(_session.Player.View.transform.position - _director.Boat.position);
                _director.Encounter.Evaluate(clock += .5f, true);
            }
            bool attackStarted = _director.Feast;
            _environment.SetWeather(CoastalWeather.Clear, true);
            _director.Encounter.Evaluate(clock += .5f, true);
            Check(attackStarted && _director.Feast && _director.KrakenVisible && _director.Encounter.IsActive,
                "An admitted attack survives a storm-to-clear weather transition");
            _director.BeginDay(true);
            _environment.SetWeather(CoastalWeather.Clear, true);
            _director.Evaluate(40);
            bool shoalStarted = _director.ShoalVisible;
            _environment.SetWeather(CoastalWeather.Storm, true);
            _director.Evaluate(41);
            Check(shoalStarted && _director.ShoalVisible, "An admitted luminous shoal finishes after weather changes");

            Stage = "300-second live scheduling soak";
            _session.Day = 4; _environment.BeginDay(4);
            _director.BeginDay(true);
            _session.NightTime = 0; _session.Fuel = _session.Lamp = 5000;
            Appearance.Reset(); Appearance.BeginDay(4, 0); _girl.ResetEncounter();
            _director.enabled = true; _observations.enabled = true; _observations.Automatic = true;
            _session.enabled = true;
            _environment.SetWeather(CoastalWeather.Storm, true);
            _environment.SetHour(22);
            before = _observations.SpawnCount;
            var lastWeather = _environment.Weather;
            Time.timeScale = 10;
            float deadline = Time.realtimeSinceStartup + 80;
            while (_session.NightTime < 300 && Time.realtimeSinceStartup < deadline)
            {
                _session.Player.View.transform.rotation = Quaternion.Euler(0, _session.NightTime * 7, 0);
                yield return null;
                _soakFrames++;
                _maximumEvents = Mathf.Max(_maximumEvents, _session.ActiveEventCount);
                if (_environment.Weather != lastWeather) { _weatherChanges++; lastWeather = _environment.Weather; }
            }
            _soakSpawns = _observations.SpawnCount - before;
            Check(_session.NightTime >= 300 && _soakFrames > 100 && _soakSpawns > 0 && _weatherChanges > 0,
                "Live scheduling completed 300 game seconds with real spawns and dynamic weather");
            Check(_maximumEvents <= 3, "All live schedulers together never exceeded three simultaneous significant events");
            _session.BeginRun(); _session.enabled = false;
            yield return null;
            Check(_session.Day == 1 && _session.ActiveEventCount == 0 && _environment.Weather == CoastalWeather.Clear,
                "Restart after the soak restores the quiet first day");
        }

        private void OnDestroy()
        {
            Time.timeScale = _timeScale;
            Application.logMessageReceived -= Log;
        }
    }
}
#endif
