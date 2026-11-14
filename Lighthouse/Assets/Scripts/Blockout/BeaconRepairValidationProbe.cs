#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace LighthouseGame
{
    // Temporary Play Mode probe; never attached to the saved scene or a player build.
    public sealed class BeaconRepairValidationProbe : MonoBehaviour
    {
        [Serializable] private sealed class Report
        {
            public int Passed, Failed;
            public List<string> Checks = new List<string>();
            public List<string> Errors = new List<string>();
            public float UnrepairedWarningSeconds;
            public float WarningGameSeconds, FinalDarkness, FinalLamp, FinalPreparation;
            public bool WarningGameOver, WarningRunning, WarningMenuOpen, WarningSleeping;
        }

        private readonly Report _report = new Report();
        private BlockoutSession _session;
        private BlockoutInteractable _lamp;
        private BlockoutHUD _hud;
        private int _previousBest;
        public bool Finished { get; private set; }
        public bool Passed => Finished && _report.Failed == 0 && _report.Errors.Count == 0;
        public string Stage { get; private set; }

        private void Check(bool value, string label)
        {
            if (value) _report.Passed++; else _report.Failed++;
            _report.Checks.Add((value ? "PASS " : "FAIL ") + label);
        }

        private void Log(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception) _report.Errors.Add(message);
        }

        private void Prepare(float darkness = 0)
        {
            _session.BeginRun(); _session.Preparation = 0; _session.Fuel = 500;
            _session.Lamp = 0; _session.Darkness = darkness;
            _session.Player.InputEnabled = false;
            _session.Player.Teleport(_lamp.transform.position + Vector3.right * 1.3f, _lamp.transform.position + Vector3.up);
            _session.Player.LockCursor(false);
            _session.Observations.Automatic = false;
        }

        private IEnumerator Start()
        {
            Application.logMessageReceived += Log;
            yield return null;
            _session = FindAnyObjectByType<BlockoutSession>();
            _hud = FindAnyObjectByType<BlockoutHUD>();
            _lamp = FindObjectsByType<BlockoutInteractable>().First(x => x.Kind == BlockoutInteractable.Station.Lamp);
            _previousBest = _session.Best;
            var game = FindAnyObjectByType<KeeperGameScene>();
            if (game) game.enabled = false;
            _session.MenuOpen = false;

            Stage = "Healthy beacon and warning lights";
            Prepare(); _session.Lamp = 10;
            _session.Work(_lamp);
            Check(!_session.IsRepairingLamp && !_session.Busy, "A healthy beacon cannot be repaired early");
            _session.Lamp = .1f;
            yield return new WaitForSeconds(.15f);
            Check(_session.LampNeedsRepair && _session.Darkness < .5f, "Repair becomes available at the first warning flashes");
            bool lit = false, dark = false, synchronized = true;
            for (float until = Time.time + 1; Time.time < until;)
            {
                yield return null;
                lit |= _session.Director.LampLight.enabled;
                dark |= !_session.Director.LampLight.enabled;
                synchronized &= _session.Director.BeamLight.enabled == _session.Director.LampLight.enabled;
            }
            Check(lit && dark && synchronized, "Both real beacon lights flash together during the warning");

            Stage = "Late repair, progress, hidden UI and pause";
            Prepare(19.9f);
            float deadline = _session.Darkness, fuel = _session.Fuel;
            _lamp.Use(_session);
            Check(_session.IsRepairingLamp && _session.Busy, "One F interaction starts a six-second repair in the final tenth of a second");
            yield return new WaitForSeconds(.8f);
            float progress = _session.LampRepairProgress;
            Check(!_session.GameOver && _session.Darkness == deadline && progress > .1f && progress < .3f,
                "Late repair freezes the failure deadline while progress advances");
            Check(_session.Fuel < fuel - .7f, "Fuel continues to burn during repair");
            var bar = _hud.Root.Q("lamp-repair-progress");
            var fill = _hud.Root.Q("lamp-repair-fill");
            Check(bar.resolvedStyle.display == DisplayStyle.Flex && fill.resolvedStyle.width > 0 && fill.resolvedStyle.width < bar.resolvedStyle.width,
                "The actual UI Toolkit progress bar displays a partial fill");
            Check(_hud.Root.Q("work-status").resolvedStyle.display == DisplayStyle.None,
                "Repair uses the approved bar without the old generic action timer");
            Check(_hud.Root.Q<Label>("lamp-warning").text.Contains("1 с"), "Warning displays the frozen remaining second");
            _session.Work(_lamp);
            Check(_session.LampRepairProgress == progress, "Repeated F cannot restart an active repair");
            _session.Player.ToggleUI();
            yield return new WaitForSeconds(.3f);
            Check(_session.Player.UIHidden && _session.IsRepairingLamp && _session.LampRepairProgress > progress && _session.Darkness == deadline,
                "Tab hides the interface while repair and the frozen deadline remain correct");
            _session.Player.ToggleUI();
            _session.MenuOpen = true;
            float pausedProgress = _session.LampRepairProgress, pausedFuel = _session.Fuel;
            yield return new WaitForSecondsRealtime(.25f);
            Check(_session.LampRepairProgress == pausedProgress && _session.Fuel == pausedFuel && _session.Darkness == deadline,
                "Pause freezes repair, fuel and warning together");
            _session.MenuOpen = false;
            yield return new WaitForSeconds(.4f);
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Artifacts/BeaconRepair"));
            Directory.CreateDirectory(folder);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "repair-in-progress.png"));
            float timeout = Time.realtimeSinceStartup + 10;
            while (_session.IsRepairingLamp && Time.realtimeSinceStartup < timeout) yield return null;
            yield return null;
            Check(!_session.GameOver && !_session.IsRepairingLamp && !_session.Busy && _session.Lamp > 59 && _session.Darkness == 0,
                "Completed repair restores sixty seconds of healthy operation and clears the deadline");
            Check(_session.Director.LampLight.enabled && _session.Director.BeamLight.enabled,
                "Successful repair stops flickering and restores both lights");
            Check(bar.resolvedStyle.display == DisplayStyle.None, "The progress bar hides on completion");

            Stage = "Interruption and ordinary busy actions";
            Prepare(12); _session.Work(_lamp);
            yield return new WaitForSeconds(.3f);
            _session.Player.Teleport(_session.Observation.position, _session.SeaEvent.position);
            yield return new WaitForSeconds(.3f);
            Check(!_session.IsRepairingLamp && !_session.Busy && _session.LampRepairProgress == 0 && _session.Darkness > 12,
                "Leaving repair range cancels progress and resumes the same remaining deadline");
            float resumed = _session.Darkness;
            _session.BusyUntil = Time.time + 2;
            yield return new WaitForSeconds(.3f);
            Check(_session.Darkness > resumed, "Other busy actions cannot freeze the beacon warning");

            Stage = "Failure at twenty seconds";
            Prepare();
            float started = Time.time;
            timeout = Time.realtimeSinceStartup + 28;
            while (!_session.GameOver && Time.realtimeSinceStartup < timeout) yield return null;
            _report.UnrepairedWarningSeconds = Time.time - started;
            _report.WarningGameSeconds = _session.NightTime;
            _report.FinalDarkness = _session.Darkness; _report.FinalLamp = _session.Lamp;
            _report.FinalPreparation = _session.Preparation; _report.WarningGameOver = _session.GameOver;
            _report.WarningRunning = _session.Running; _report.WarningMenuOpen = _session.MenuOpen; _report.WarningSleeping = _session.Sleeping;
            Check(_session.GameOver && _session.Darkness == 20 && _report.UnrepairedWarningSeconds >= 19.9f && _report.UnrepairedWarningSeconds < 20.5f,
                "An untouched flashing beacon fails after twenty seconds");
            Check(!_session.Director.LampLight.enabled && !_session.Director.BeamLight.enabled,
                "Failure switches off both beacon lights");

            Stage = "Restart, fuel exhaustion and hidden-input repair";
            Prepare(5); _session.Player.ToggleUI(); _session.Work(_lamp);
            Check(_session.Player.UIHidden && _session.IsRepairingLamp, "Repair can start with the UI hidden");
            _session.Player.ToggleUI();
            yield return new WaitForSeconds(.2f);
            _session.BeginRun();
            yield return new WaitForSeconds(.2f);
            Check(!_session.IsRepairingLamp && !_session.Busy && _session.LampRepairProgress == 0 && _session.Darkness == 0 && _session.Lamp == 60,
                "Restart cancels the old repair without leaking progress or a late completion");
            Prepare(5); _session.Fuel = .15f; _session.Work(_lamp);
            yield return new WaitForSeconds(.3f);
            Check(_session.GameOver && !_session.IsRepairingLamp && !_session.Busy,
                "Repair cannot prevent fuel exhaustion and clears on game over");

            _session.BeginRun(); _session.Player.InputEnabled = true; _session.Player.LockCursor(false);
            PlayerPrefs.SetInt("Lighthouse.Blockout.Best", _previousBest); PlayerPrefs.Save(); _session.Best = _previousBest;
            Finished = true; Stage = "Complete";
            Application.logMessageReceived -= Log;
            File.WriteAllText(Path.Combine(folder, "playmode.json"), JsonUtility.ToJson(_report, true));
            Debug.Log("BEACON_REPAIR " + _report.Passed + " passed, " + _report.Failed + " failed, " + _report.Errors.Count + " errors");
        }

        private void OnDestroy() { Application.logMessageReceived -= Log; }
    }
}
#endif
