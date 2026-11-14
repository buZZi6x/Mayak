#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace LighthouseGame
{
    // Temporary Play Mode harness. Never attached to the saved scene or a player build.
    public sealed class RadioSleepValidationProbe : MonoBehaviour
    {
        [Serializable] private sealed class Report { public int Passed, Failed; public List<string> Checks = new List<string>(); public List<string> Errors = new List<string>(); }
        private readonly Report _report = new Report();
        private BlockoutSession _session;
        private KeeperRadio _radio;
        private bool _runInBackground;
        public bool Finished { get; private set; }
        public bool Passed => Finished && _report.Failed == 0 && _report.Errors.Count == 0;
        private void Check(bool value, string label) { if (value) _report.Passed++; else _report.Failed++; _report.Checks.Add((value ? "PASS " : "FAIL ") + label); }
        private void Log(string message, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception) _report.Errors.Add(message); }
        private IEnumerator Start()
        {
            Application.logMessageReceived += Log;
            _runInBackground = Application.runInBackground; Application.runInBackground = true;
            yield return null;
            _session = FindAnyObjectByType<BlockoutSession>();
            var game = FindAnyObjectByType<KeeperGameScene>(); if (game) game.enabled = false;
            _session.MenuOpen = false; _session.Player.InputEnabled = false;
            _session.BeginRun(); _session.Preparation = 0; _session.Day = 2;
            if (CinematicEnvironment.Active) CinematicEnvironment.Active.enabled = false;
            _session.Fuel = 500; _session.Lamp = 500;
            _radio = _session.Radio;
            Check(_radio, "Radio binds to the existing station and explicit scene audio references");
            if (!_radio) { Finish(); yield break; }
            _session.Observations.Automatic = false; _session.Observations.Clear();
            var director = _session.Director;
            var encounter = director.Encounter;
            director.FreezePreview = true; _session.Storm = true; director.WillEat = true;
            encounter.ManualTimeline = true; encounter.SelectCreature(true); encounter.Evaluate(38, true); encounter.RenderPose();
            _session.Player.Teleport(_session.Observation.position, director.Boat.position + Vector3.up * 10);
            yield return null;
            _session.Capture(); yield return new WaitForEndOfFrame(); yield return null;
            Check(_session.Shots.Count == 1 && _session.Score > 0 && _session.Shots[0].Sent && _session.Confirmed == _session.Score,
                "A real valued photograph confirms automatically without touching radio");
            if (_session.Shots.Count == 0) { Finish(); yield break; }
            Check(_session.Shots[0].Image && !_session.TakingPhoto, "Photograph completes before a day can clear its texture");
            _session.BusyUntil = 0;
            var bed = FindObjectsByType<BlockoutInteractable>().First(x => x.Kind == BlockoutInteractable.Station.Bed);
            var radioStation = _radio.GetComponent<BlockoutInteractable>();
            int confirmed = _session.Confirmed; int score = _session.Score;
            _session.Work(radioStation); _radio.CloseControls(); _session.Work(radioStation); _radio.CloseControls();
            Check(_session.Confirmed == confirmed && _session.Score == score && !_session.Busy, "Using radio never changes score, sends photos or starts timed work");
            _session.Player.Teleport(_radio.transform.position + Vector3.forward, _radio.transform.position);
            _session.Player.LockCursor(true); _radio.RequestControls(); yield return null;
            var view = _radio.GetComponent<KeeperRadioView>();
            Check(view && view.Root.panel != null && view.Root.Q<Image>("radio-plate").image && _radio.ControlsOpen && Cursor.lockState == CursorLockMode.None,
                "Approved radio image opens in a live UI Toolkit panel and releases the cursor");
            bool cameraEquipped = _session.Player.CameraEquipped, flashlight = _session.Player.Flashlight.enabled;
            _session.Player.ToggleCamera(); _session.Player.ToggleFlashlight(); _session.Capture();
            Check(_session.Player.CameraEquipped == cameraEquipped && _session.Player.Flashlight.enabled == flashlight && _session.Shots.Count == 1,
                "Radio modal blocks equipment and shutter commands");
            view.Root.Q<Slider>("radio-wave").value = 101.6f;
            Check(_radio.Band == RadioBand.Weather && view.Root.Q<Label>("radio-station").text == "МЕТЕОПОСТ", "UI wave slider drives the actual receiver and updates its station");
            Submit(view.Root.Q<Button>("radio-off")); yield return null;
            Check(!_radio.IsPowered, "Off button submits to the receiver");
            Submit(view.Root.Q<Button>("radio-on")); yield return null;
            Check(_radio.IsPowered, "On button submits to the receiver");
            var knob = view.Root.Q("radio-knob"); knob.Focus();
            using (var key = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.End })) { key.target = knob; knob.SendEvent(key); }
            yield return null;
            Check(_radio.Frequency == 108, "Tuning knob accepts keyboard control");
            _radio.Tune(94.2f); view.Root.Q("radio-knob").Focus(); yield return null;
            Check(view.Root.Q("radio-knob").resolvedStyle.borderLeftWidth == 0, "Focused tuning knob has no rectangular selection frame");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Application.dataPath, "../../Artifacts/RadioSleep/receiver-unity-focused.png")));
            yield return new WaitForEndOfFrame();
            Submit(view.Root.Q<Button>("radio-close")); yield return null;
            Check(!_radio.ControlsOpen && Cursor.lockState == CursorLockMode.Locked, "Close button restores the prior locked cursor");
            _session.Player.ToggleUI(); _radio.RequestControls(); yield return null;
            Check(_session.Player.UIHidden && _radio.ControlsOpen, "F interaction can open radio with the HUD hidden");
            _session.Player.ToggleUI();
            Check(_session.Player.UIHidden && !_radio.ControlsOpen && Cursor.lockState == CursorLockMode.Locked, "Tab closes radio and leaves gameplay controls available with HUD hidden");
            _session.Player.ToggleUI();
            _session.Player.LockCursor(false); _radio.RequestControls(); view.enabled = false;
            Check(!_radio.ControlsOpen && Cursor.lockState == CursorLockMode.None, "Disabling the view restores an originally free cursor");
            yield return null; view.enabled = true; _radio.RequestControls(); yield return null;
            Check(_radio.ControlsOpen && view.Root.panel != null, "Re-enabled view binds once and opens again");
            _radio.CloseControls();
            _radio.SetPower(false);
            Check(!_radio.GetComponentsInChildren<AudioSource>().Any(x => x.isPlaying), "Off stops every radio layer");
            _radio.SetPower(true); _radio.Tune(94.2f);
            int initialBroadcasts = _radio.BroadcastCount;
            for (int i = 0; i < 32 && _radio.BroadcastCount == initialBroadcasts; i++) _radio.ConsiderForecast("aurora", 10);
            Check(_radio.CurrentBroadcast == "aurora" && _radio.Transcript.Length > 20, "An eligible future event plays its matching Russian voice and text");
            int count = _radio.BroadcastCount;
            Check(!_radio.ConsiderForecast("aurora", -1) && !_radio.ConsiderForecast("weather-storm", 10) && _radio.BroadcastCount == count,
                "Expired, wrong-wave and overlapping warnings cannot play");
            _radio.Tune(96); yield return null;
            Check(_radio.CurrentBroadcast == "" && _radio.Band == RadioBand.Static, "Retuning out of a station interrupts the voice and clears its text");
            _radio.Tune(94.2f);
            var heard = new HashSet<string>(); string previous = ""; bool noRepeats = true;
            for (int i = 0; i < 12; i++) { _radio.PlayNextTrack(); yield return null; string current = _radio.CurrentMusic; heard.Add(current); noRepeats &= current != previous; previous = current; }
            Check(heard.Count == 3 && !heard.Contains("") && noRepeats, "All three supplied songs occur, with no immediate repeats over four bags");
            float fuel = _session.Fuel; float lamp = _session.Lamp; int day = _session.Day;
            int bonus = _session.Bonus;
            _session.Work(bed);
            Check(_session.Sleeping && _session.Day == day, "One press immediately enters sleep with only one photograph");
            _session.Work(bed); _session.Capture(); _session.Player.ToggleFlashlight();
            float deadline = Time.realtimeSinceStartup + 5; bool opaqueChange = false, fuelFrozen = true, lampFrozen = true;
            while (_session.Sleeping && Time.realtimeSinceStartup < deadline)
            {
                fuelFrozen &= Mathf.Abs(_session.Fuel - (fuel + (_session.Day == day ? 0 : bonus))) < .01f;
                lampFrozen &= Mathf.Abs(_session.Lamp - lamp) < .01f;
                if (_session.Day != day) opaqueChange |= _session.Fade >= .999f;
                yield return null;
            }
            Check(!_session.Sleeping && _session.Day == day + 1 && _session.Shots.Count == 0 && _session.Fade == 0, "Sleep advances once and restores all five frames");
            Check(fuelFrozen && lampFrozen && opaqueChange, "Timers freeze during sleep and the day changes under a fully black frame");
            Check(_session.PaidHundreds == confirmed / 100 && _session.Preparation == 0, "Only newly earned hundreds pay fuel, without another preparation period");
            _session.BusyUntil = 0; day = _session.Day; int paid = _session.PaidHundreds;
            _session.Work(bed);
            yield return new WaitForSecondsRealtime(1.7f);
            Check(_session.Day == day + 1 && _session.PaidHundreds == paid && _session.Bonus == 0, "Sleeping with zero photos is allowed and cannot duplicate fuel rewards");
            _session.Work(bed); yield return new WaitForSecondsRealtime(.2f); _session.BeginRun();
            yield return new WaitForSecondsRealtime(1.7f);
            Check(_session.Day == 1 && !_session.Sleeping && _session.Fade == 0 && _session.Score == 0 && _session.Fuel == _session.StartFuel,
                "Restart during fade cancels the old coroutine and all pending payouts");
            Check(!_session.Player.CameraEquipped || _session.Player.HeldCamera.gameObject.activeSelf, "Restart restores the held camera after sleep");
            _session.Preparation = 0; _session.Work(bed); yield return new WaitForSecondsRealtime(.2f);
            _session.EndRun("Radio sleep validation"); yield return new WaitForSecondsRealtime(1.7f);
            Check(_session.GameOver && !_session.Sleeping && _session.Day == 1 && _session.Fade == 0, "Game over during fade cancels the day transition");
            _session.BeginRun(); _session.Player.InputEnabled = true; _session.Player.LockCursor(false);
            Finish();
        }
        private void Finish()
        {
            Finished = true; Application.logMessageReceived -= Log;
            Application.runInBackground = _runInBackground;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Artifacts/RadioSleep/runtime.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, JsonUtility.ToJson(_report, true));
            Debug.Log("RADIO_SLEEP " + _report.Passed + " passed, " + _report.Failed + " failed, " + _report.Errors.Count + " errors");
        }
        private static void Submit(Button button)
        {
            button.Focus();
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
        }
        private void OnDestroy() { Application.logMessageReceived -= Log; }
    }
}
#endif
