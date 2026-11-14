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
    // Temporary Play Mode probe. Never stored in a scene or included in a player build.
    public sealed class LeviathanValidationProbe : MonoBehaviour
    {
        public bool Done, Passed;
        public string Stage;
        private BlockoutSession _session;
        private LeviathanEncounter _encounter;
        private SkinnedMeshRenderer _body;
        private string _output;
        private readonly List<string> _checks = new List<string>();
        private readonly List<string> _errors = new List<string>();
        private readonly List<float> _frameTimes = new List<float>();
        private readonly List<float> _realFrameTimes = new List<float>();
        private MarineAudioCapture _audioCapture;
        private float _timeScale;
        private int _best;
        private bool _hadBest;

        [Serializable] private sealed class Report
        {
            public bool Done, Passed;
            public string[] Checks, Errors;
            public float[] FrameTimes;
            public float[] RealFrameTimes;
            public float AudioPeak, AudioRms;
        }
        private static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
        private LeviathanAppearance Appearance => (LeviathanAppearance)Get(_encounter, "_appearance");
        private void Check(bool condition, string label) => _checks.Add((condition ? "PASS " : "FAIL ") + label);
        private void Log(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _errors.Add(message);
        }

        private IEnumerator Start()
        {
            _timeScale = Time.timeScale;
            _hadBest = PlayerPrefs.HasKey("Lighthouse.Blockout.Best");
            _best = PlayerPrefs.GetInt("Lighthouse.Blockout.Best", 0);
            _output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Artifacts/Leviathan"));
            Directory.CreateDirectory(_output + "/Frames");
            Application.logMessageReceived += Log;
            var run = Run();
            while (true)
            {
                object current;
                try { if (!run.MoveNext()) break; current = run.Current; }
                catch (Exception exception) { _checks.Add("FAIL " + Stage + ": " + exception); break; }
                yield return current;
            }
            Application.logMessageReceived -= Log;
            Time.timeScale = _timeScale;
            RestoreBest();
            Done = true;
            Passed = _errors.Count == 0 && _checks.All(c => c.StartsWith("PASS "));
            Stage = "Finished";
            File.WriteAllText(_output + "/playmode.json", JsonUtility.ToJson(new Report {
                Done = true, Passed = Passed, Checks = _checks.ToArray(), Errors = _errors.ToArray(), FrameTimes = _frameTimes.ToArray(),
                RealFrameTimes = _realFrameTimes.ToArray(), AudioPeak = _audioCapture ? _audioCapture.Peak : 0,
                AudioRms = _audioCapture ? _audioCapture.Rms : 0 }, true));
        }

        private IEnumerator Run()
        {
            _session = FindAnyObjectByType<BlockoutSession>();
            _encounter = FindAnyObjectByType<LeviathanEncounter>();
            _body = _encounter.GetComponent<SkinnedMeshRenderer>();
            // Detach persistent photo recording before capturing synthetic test scenarios.
            foreach (var component in FindObjectsByType<MonoBehaviour>())
                if (component.GetType().Name == "KeeperGameScene" || component is KeeperMenu) component.enabled = false;
            _session.BeginRun(); _session.enabled = false;
            _session.Player.enabled = false; _session.Player.InputEnabled = false; _session.Player.LockCursor(false);
            var girl = FindAnyObjectByType<WeepingGirlEncounter>(); if (girl) girl.gameObject.SetActive(false);
            foreach (var gull in FindObjectsByType<CoastalGull>()) gull.gameObject.SetActive(false);
            var director = _session.Director;
            director.Encounter.StopPreview(); director.Encounter.ResetEncounter(); director.enabled = false;
            director.Encounter.FrozenPreview = true;
            director.ShoalVisible = false; director.Shoal.gameObject.SetActive(false);
            director.Traffic.Frozen = true;
            foreach (var ship in director.Traffic.Ships) ship.gameObject.SetActive(false);
            var observations = _session.Observations;
            observations.Automatic = false; observations.Clear(); observations.enabled = false;
            var environment = CinematicEnvironment.Active;
            environment.AdvanceTime = false; environment.SetWeather(CoastalWeather.Clear, true); environment.SetHour(15);
            if (environment.Bolt) environment.Bolt.Clear();
            _session.MenuOpen = false;
            Time.timeScale = 1;
            yield return null;

            Stage = "Eligibility and lifecycle";
            for (int day = 1; day <= 5; day++)
            {
                SelectDay(day, 0); _session.Preparation = 0;
                yield return null;
                Check(!_encounter.Showing && !_encounter.Pending, "No appearance on day " + day);
            }
            SelectDay(6, 0); _session.Preparation = 15;
            yield return null;
            Check(!_encounter.Showing && _encounter.Pending, "Selected day waits through preparation");
            _session.Preparation = 0; _session.Sleeping = true;
            yield return null;
            Check(!_encounter.Showing && _encounter.Pending, "Sleep cannot spawn or reroll the selected event");
            _session.Sleeping = false; _session.MenuOpen = true;
            yield return null;
            Check(!_encounter.Showing && _encounter.Pending, "Menu blocks event admission");
            _session.MenuOpen = false;
            director.ShoalVisible = true;
            for (int i = 0; i < 2; i++) { observations.Effects[i].Show(Vector3.right * 2000, Quaternion.identity); observations.Effects[i].Frozen = true; }
            yield return null;
            Check(_session.ActiveEventCount == 3 && !_encounter.Showing && _encounter.Pending, "A full event budget preserves the pending event");
            director.ShoalVisible = false; observations.Clear();
            Set(_encounter, "_wait", -1f); Set(_encounter, "_retry", -1f);
            yield return null;
            Check(_encounter.Showing && _encounter.Consumed && _encounter.SpawnCount == 1, "The scheduler admits one selected breach");
            Check(SeaPlacement.OutsideView(_session.Player.View, _encounter.LastAdmissionBounds), "Admission respects the expanded player view");
            float distance = new Vector2(_encounter.transform.position.x, _encounter.transform.position.z).magnitude;
            Check(distance >= 650 && distance <= 850 && _session.ActiveEventCount == 1, "Distant spawn uses one event slot at 650-850 metres");
            Call(_encounter, "Evaluate", 3f);
            var roar = (AudioSource)Get(_encounter, "_roar");
            yield return null;
            Check(roar.clip && roar.isPlaying && _encounter.RoarCount == 1, "A real recorded roar plays once when the leviathan breaches");
            float age = _encounter.Age; Time.timeScale = 0;
            yield return null; yield return null;
            Check(_encounter.Age == age, "A real pause stops the breach clock");
            Check(!roar.isPlaying, "Pausing the encounter also pauses its voice");
            Time.timeScale = 1;
            yield return null; yield return null;
            Check(roar.isPlaying && _encounter.RoarCount == 1, "Unpausing resumes the same roar without replaying the cue");
            _session.Sleeping = true;
            yield return null;
            Check(!_encounter.Showing && _encounter.Consumed && !_body.enabled, "Sleep clears the active body without granting a second leap");
            _session.Sleeping = false;
            Set(_encounter, "_wait", -1f); Set(_encounter, "_retry", -1f);
            yield return null;
            Check(!_encounter.Showing && _encounter.SpawnCount == 1, "A completed daily event cannot immediately respawn");

            Stage = "Real photographs and occlusion";
            SelectDay(7, 1); // Suppress automatic admission while staging deterministic poses.
            Vector3 outward = _session.SeaEvent.position; outward.y = 0; outward.Normalize();
            var anchor = outward * 730;
            Call(_encounter, "BeginBreach", anchor);
            Time.timeScale = 0;
            Call(_encounter, "Evaluate", 8.1f);
            Aim(_session.Observation.position + Vector3.up * 1.65f, _encounter.Focus);
            _session.HelpVisible = false;
            yield return Settle();
            Check(_encounter.VisibleIn(_session.Player.View), "Actual skinned anatomy is readable from the lighthouse");
            Check(Mathf.Abs(_encounter.transform.lossyScale.x - 3) < .001f && _body.sharedMesh.bounds.size.z * _encounter.transform.lossyScale.z > 570,
                "The actual rendered creature is over 570 metres long");
            var restPosition = _session.Player.View.transform.localPosition;
            var restRotation = _session.Player.View.transform.localRotation;
            Call(_session.Player, "ApplyLeviathanShake");
            Check(Quaternion.Angle(restRotation, _session.Player.View.transform.localRotation) > .01f && _encounter.ShakeStrength > .3f,
                "Continuous shake visibly offsets the camera during the aerial phase");
            Call(_session.Player, "RemoveLeviathanShake");
            for (int i = 0; i < 500; i++) { Call(_session.Player, "ApplyLeviathanShake"); Call(_session.Player, "RemoveLeviathanShake"); }
            Check(Vector3.Distance(restPosition, _session.Player.View.transform.localPosition) < .00001f
                && Quaternion.Angle(restRotation, _session.Player.View.transform.localRotation) < .01f, "500 shake updates do not accumulate camera drift");
            yield return Capture("first-photo");
            Check(_session.Shots.Count == 1 && _session.Shots[0].Price == 1000 && _session.Shots[0].Key == LeviathanEncounter.PhotoKey,
                "A real first photograph awards exactly 1000");
            yield return Capture("repeat-photo");
            Check(_session.Shots[1].Price == 500 && _session.Shots[0].Price == 1000 && _session.Score == 1500,
                "A repeat uses the existing half-price rule without changing the earlier photo");
            var camera = _session.Player.View;
            var previousRotation = camera.transform.rotation;
            camera.transform.rotation = Quaternion.LookRotation(-camera.transform.forward);
            Check(!_encounter.VisibleIn(camera), "A subject behind the camera cannot earn points");
            camera.transform.rotation = previousRotation;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetPositionAndRotation(camera.transform.position + camera.transform.forward * 10, camera.transform.rotation);
            wall.transform.localScale = new Vector3(50, 50, 1); Physics.SyncTransforms();
            Check(!_encounter.VisibleIn(camera), "An opaque foreground wall blocks the leviathan reward");
            yield return Capture("blocked-photo");
            Check(_session.Shots[2].Price == 0, "A fully occluded photograph awards zero");
            wall.SetActive(false); Destroy(wall); Physics.SyncTransforms();
            _body.enabled = false;
            Check(!_encounter.VisibleIn(camera), "A disabled mesh cannot give credit");
            _body.enabled = true;
            int mask = camera.cullingMask; camera.cullingMask &= ~(1 << _body.gameObject.layer);
            Check(!_encounter.VisibleIn(camera), "Camera layer exclusion blocks photo credit");
            camera.cullingMask = mask;
            Call(_encounter, "Evaluate", 20f);
            Check(!_encounter.VisibleIn(camera), "Submerged anatomy and residual foam are not photographic evidence");
            yield return Capture("water-only");
            Check(_session.Shots[3].Price == 0, "Water-only photo awards zero after the dive");
            Call(_encounter, "Evaluate", 8.1f);
            yield return Capture(null);
            _session.BusyUntil = 0; _session.Capture();
            Check(_session.Shots.Count == 5 && _session.Shots[4].Price == 250, "The five-photo quota and independent repeat counter still apply");
            environment.SetHour(21.5f);
            Call(_encounter, "BeginBreach", anchor);
            Time.timeScale = 1;
            while (_encounter.Age < 8.1f) yield return null;
            Time.timeScale = 0;
            yield return Settle();
            yield return Screenshot("night-from-lighthouse.png");
            environment.SetHour(15);

            Stage = "Continuous breach and water cleanup";
            _session.BeginRun(); _session.enabled = false; _session.Player.enabled = false; _session.Player.LockCursor(false);
            _session.Day = 7; _session.Preparation = 0; Appearance.BeginDay(7, 1);
            director.Encounter.FrozenPreview = true; director.enabled = false; observations.Automatic = false; observations.enabled = false;
            Aim(_session.Observation.position + Vector3.up * 1.65f, anchor + Vector3.up * (65 * LeviathanBreach.WorldScale));
            _session.HelpVisible = false;
            environment.SetWeather(CoastalWeather.Clear, true); environment.SetHour(15); environment.AdvanceTime = false;
            Call(_encounter, "BeginBreach", anchor);
            _session.Player.InputEnabled = false; _session.Player.enabled = true;
            _session.MessageUntil = 0;
            _audioCapture = _session.Player.View.gameObject.AddComponent<MarineAudioCapture>();
            _audioCapture.StartCapture();
            float realStart = Time.realtimeSinceStartup;
            Time.timeScale = 1;
            float nextFrame = 0, peak = 0;
            int maxSlots = 0;
            while (_encounter.Showing)
            {
                yield return new WaitForEndOfFrame();
                peak = Mathf.Max(peak, _body.bones.Max(b => b.position.y));
                maxSlots = Mathf.Max(maxSlots, _session.ActiveEventCount);
                if (_encounter.Age >= nextFrame)
                {
                    float stamp = _encounter.Age;
                    var image = ScreenCapture.CaptureScreenshotAsTexture();
                    File.WriteAllBytes(_output + "/Frames/" + _frameTimes.Count.ToString("D4") + ".png", image.EncodeToPNG());
                    Destroy(image); _frameTimes.Add(stamp); _realFrameTimes.Add(Time.realtimeSinceStartup - realStart); nextFrame = stamp + .125f;
                }
            }
            _audioCapture.Finish(_output + "/Leviathan-gameplay-audio.wav");
            Check(peak > 300 && _encounter.Age >= LeviathanBreach.Lifetime, "A complete natural giant breach rises over 300 metres and dives over 23 seconds");
            Check(_encounter.RoarCount == 1 && !roar.isPlaying, "The full natural sequence contains exactly one roar and stops its audio");
            Check(_audioCapture.Peak > .01f && _audioCapture.Peak < .999f && _audioCapture.Rms > .001f, "Recorded game audio is audible without digital clipping");
            Check(_encounter.ShakeStrength == 0 && !(bool)Get(_session.Player, "_hasLeviathanShake"), "Camera shake clears completely after the final dive");
            Check(maxSlots <= 3 && _session.ActiveEventCount == 0, "The event releases its budget slot after diving");
            Check(!_body.enabled && !_encounter.GetComponentsInChildren<UnityEngine.Rendering.HighDefinition.WaterDecal>(true).Any(d=>d.gameObject.activeSelf),
                "Body and both foam decals clean up at the end");
            Check(_encounter.GetComponentInChildren<ParticleSystem>(true).particleCount == 0, "No particles survive cleanup");
            yield return Screenshot("after-dive.png");
            Call(_encounter, "BeginBreach", anchor); _session.BeginRun();
            Check(!_encounter.Showing && _encounter.SpawnCount == 0 && _session.Day == 1 && _encounter.ShakeStrength == 0 && !roar.isPlaying, "F1 BeginRun immediately clears the encounter, sound, shake and daily decision");
            SelectDay(6, 0); _session.Preparation = 0;
            Call(_encounter, "BeginBreach", anchor); _session.EndRun("Leviathan verification");
            yield return null;
            Check(!_encounter.Showing && !_encounter.VisibleIn(camera) && !roar.isPlaying && _encounter.ShakeStrength == 0, "Game Over stops the event, sound, shake and photo eligibility");
        }

        private void SelectDay(int day, float roll)
        {
            Call(_encounter, "Hide");
            _session.Day = day; Appearance.Reset(); Appearance.BeginDay(day, roll);
            Set(_encounter, "_wait", -1f); Set(_encounter, "_retry", -1f);
        }
        private void Aim(Vector3 position, Vector3 target)
        {
            _session.Player.Teleport(position - Vector3.up * 1.65f, target);
            _session.Player.View.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
            Physics.SyncTransforms();
        }
        private IEnumerator Settle() { for (int i = 0; i < 25; i++) yield return null; }
        private IEnumerator Capture(string name)
        {
            _session.BusyUntil = 0; _session.Capture();
            yield return new WaitForEndOfFrame(); yield return null;
            if (name != null) File.WriteAllBytes(_output + "/" + name + ".png", _session.Shots.Last().Image.EncodeToPNG());
        }
        private IEnumerator Screenshot(string name)
        {
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(_output + "/" + name, texture.EncodeToPNG()); Destroy(texture);
        }
        private void RestoreBest()
        {
            if (_hadBest) PlayerPrefs.SetInt("Lighthouse.Blockout.Best", _best); else PlayerPrefs.DeleteKey("Lighthouse.Blockout.Best");
            PlayerPrefs.Save();
        }
        private void OnDestroy() { Time.timeScale = _timeScale; RestoreBest(); }
    }
}
#endif
