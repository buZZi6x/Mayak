#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace LighthouseGame
{
    public class KeeperUIValidationProbe : MonoBehaviour
    {
        public bool Finished, Passed;
        public string Result;
        private BlockoutSession _session;
        private BlockoutPlayer _player;
        private BlockoutHUD _hud;
        private Keyboard _keyboard, _originalKeyboard;
        private Mouse _mouse, _originalMouse;
        private readonly List<string> _checks = new List<string>();
        private readonly List<string> _errors = new List<string>();
        private const string Output = "../Artifacts/UnityUI/";

        private void Check(bool valid, string name) => _checks.Add((valid ? "PASS " : "FAIL ") + name);
        private void Log(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _errors.Add(message); }
        private IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null; yield return null;
        }
        private IEnumerator Click()
        {
            InputSystem.QueueStateEvent(_mouse, new MouseState { buttons = 1, position = new Vector2(Screen.width / 2, Screen.height / 2) });
            yield return null; yield return new WaitForEndOfFrame();
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = new Vector2(Screen.width / 2, Screen.height / 2) });
            yield return null; yield return null;
        }
        private IEnumerator Capture(string name)
        {
            _session.MessageUntil = 0; _session.BusyUntil = 0;
            yield return new WaitForSeconds(.3f);
            ScreenCapture.CaptureScreenshot(Output + name + ".png");
            yield return new WaitForEndOfFrame(); yield return null;
        }
        private void Pointer(VisualElement target, EventType type, Vector2 position)
        {
            var source = new Event { type = type, mousePosition = position, button = 0, clickCount = 1 };
            if (type == EventType.MouseDown)
            { using (var e = PointerDownEvent.GetPooled(source)) { e.target = target; target.SendEvent(e); } }
            else if (type == EventType.MouseUp)
            { using (var e = PointerUpEvent.GetPooled(source)) { e.target = target; target.SendEvent(e); } }
            else
            { using (var e = PointerMoveEvent.GetPooled(source)) { e.target = target; target.SendEvent(e); } }
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(Output); Application.logMessageReceived += Log;
            _session = FindAnyObjectByType<BlockoutSession>(); _player = _session.Player; _hud = FindAnyObjectByType<BlockoutHUD>();
            _originalKeyboard = Keyboard.current; _originalMouse = Mouse.current;
            _keyboard = InputSystem.AddDevice<Keyboard>("Keeper UI verification keyboard");
            _mouse = InputSystem.AddDevice<Mouse>("Keeper UI verification mouse");
            _session.Running = false; _session.GameOver = false; _session.BusyUntil = 0;
            CinematicEnvironment.Active.AdvanceTime = false;
            CinematicEnvironment.Active.SetHour(0);
            _session.Director.FreezePreview = true;
            _session.Observations.Automatic = false;
            _player.Teleport(_session.Observation.position, _session.SeaEvent.position + Vector3.up * 6);
            _player.LockCursor(true);
            yield return new WaitForSeconds(1);
            Check(_hud.Root.Q("slot-5") != null && _hud.Root.Q<Label>("fuel").text == "02:00", "Retained HUD binds five slots and actual fuel");
            Check(!_hud.Root.Query<Label>().ToList().Any(x => x.text == "Фотографии" || x.text.Contains("НЕ ДАЙ ПОГАСНУТЬ")), "Removed headings are absent");
            yield return Tap(Key.E);
            Check(!_player.CameraEquipped && !_player.HeldCamera.gameObject.activeSelf, "E holsters camera");
            yield return Click(); Check(_session.Shots.Count == 0, "Holstered camera cannot take a photo");
            yield return Tap(Key.E); yield return Tap(Key.Q);
            Check(_player.CameraEquipped && _player.HeldCamera.gameObject.activeSelf, "E equips actual held camera");
            Check(_player.FlashlightOn && _player.Flashlight.type == LightType.Spot && _player.Flashlight.transform.parent == _player.View.transform && _player.Flashlight.intensity > 0, "Q enables a real view-directed HDRP spotlight");
            yield return Tap(Key.Tab);
            Check(_player.UIHidden && _hud.Root.Q("hud").resolvedStyle.display == DisplayStyle.None && _player.CameraEquipped && _player.HeldCamera.gameObject.activeSelf && _player.FlashlightOn, "Tab hides only UI, preserving camera, flashlight and input");
            bool captureUiHidden = false;
            Action onCapture = () => { if (_session.TakingPhoto) captureUiHidden = _hud.Root.Q("hud").style.display.value == DisplayStyle.None; };
            _session.PhotoVisibilityChanged += onCapture;
            yield return Click();
            Check(_session.Shots.Count == 1 && _session.Shots[0].Image && _player.UIHidden && _player.HeldCamera.gameObject.activeSelf, "Left mouse captures a real photo while Tab UI is hidden");
            Check(captureUiHidden && !_session.TakingPhoto, "Capture excludes UI and completes without changing visibility mode");
            if (_session.Shots.Count > 0)
            {
                var pixels = _session.Shots[0].Image.GetPixels();
                Check(pixels.Max(p => p.grayscale) - pixels.Min(p => p.grayscale) > .08f, "Stored photo contains actual scene detail");
                File.WriteAllBytes(Output + "stored-photo.png", _session.Shots[0].Image.EncodeToPNG());
            }
            _session.PhotoVisibilityChanged -= onCapture;
            yield return Tap(Key.Tab); yield return Tap(Key.Q);
            Check(!_player.UIHidden && !_player.FlashlightOn, "Tab restores HUD and Q switches off light");
            _session.BusyUntil = 0; yield return Tap(Key.Digit1);
            Check(_player.SelectedSlot == 0 && !_player.CameraEquipped, "Digit 1 selects the real first photograph");
            var first = _player.SelectedPhoto;
            _player.SwapPhotos(0, 4);
            Check(_player.SelectedSlot == 4 && _player.PhotoAt(0) == null && _player.PhotoAt(4) == first && _session.Shots.Count == 1, "Moving into empty slot retains photo identity and quota");
            for (int i = 0; i < 4; i++)
            {
                _session.BusyUntil = 0; _session.Capture();
                yield return new WaitForEndOfFrame(); yield return null;
            }
            _session.BusyUntil = 0; _session.Capture();
            Check(_session.Shots.Count == 5 && _player.PhotoAt(4) == first && Enumerable.Range(0, 5).All(i => _player.PhotoAt(i) != null), "Five-frame quota fills remaining slots without overwriting moved photo");
            Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };
            for (int i = 0; i < 5; i++)
            {
                yield return Tap(keys[i]);
                Check(_player.SelectedSlot == i && _hud.Root.Q("slot-" + (i + 1)).ClassListContains("selected"), "Physical key " + (i + 1) + " selects matching slot");
            }
            yield return Tap(Key.Numpad2); Check(_player.SelectedSlot == 1, "NumPad selection works");
            yield return Capture("hud-" + Screen.width + "x" + Screen.height);
            _player.OpenInspection(); yield return null;
            Check(_player.IsInspecting && Cursor.lockState == CursorLockMode.None, "Photo inspection releases mouse");
            int count = _session.Shots.Count; yield return Click();
            Check(_session.Shots.Count == count && _player.IsInspecting, "Inspector click does not leak into shooting");
            yield return Capture("photo-inspector");
            yield return Tap(Key.Tab);
            Check(!_player.IsInspecting && _player.UIHidden && Cursor.lockState == CursorLockMode.Locked, "Tab dismisses modal and resumes control while hiding UI");
            yield return Tap(Key.E); Check(_player.CameraEquipped && _player.UIHidden, "E still equips camera in hidden UI mode");
            yield return Tap(Key.Tab); yield return Tap(Key.E);
            _player.LockCursor(false); yield return null;
            var a = _hud.Root.Q("slot-1"); var b = _hud.Root.Q("slot-3");
            var photoA = _player.PhotoAt(0); var photoB = _player.PhotoAt(2); int price = photoA.Price;
            Pointer(a, EventType.MouseDown, a.worldBound.center);
            Pointer(a, EventType.MouseDrag, b.worldBound.center);
            Pointer(a, EventType.MouseUp, b.worldBound.center);
            yield return null;
            Check(_player.PhotoAt(2) == photoA && _player.PhotoAt(0) == photoB && photoA.Price == price && !_hud.IsDragging, "Pointer drag swaps real photos and releases capture");
            Pointer(a, EventType.MouseDown, a.worldBound.center); Pointer(a, EventType.MouseDrag, b.worldBound.center);
            _player.ToggleUI(); yield return null;
            Check(!_hud.IsDragging && _player.PhotoAt(2) == photoA, "Tab cancels a pending drag without altering photos");
            _player.ToggleUI();
            Pointer(a, EventType.MouseDown, a.worldBound.center); Pointer(a, EventType.MouseDrag, Vector2.zero);
            Pointer(a, EventType.MouseUp, Vector2.zero);
            Check(!_hud.IsDragging && _player.PhotoAt(2) == photoA, "Dropping outside slots cancels safely");
            var door = FindObjectsByType<BlockoutInteractable>().First(s => s.Kind == BlockoutInteractable.Station.Door);
            var collider = door.GetComponentInChildren<Collider>(); var center = collider.bounds.center;
            _player.LockCursor(true);
            foreach (var offset in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
            {
                _player.Teleport(center + offset * 2 - Vector3.up * 1.2f, center);
                yield return null; yield return null;
                if (_player.Focused == door) break;
            }
            Check(_player.Focused == door, "Door is acquired by actual interaction raycast");
            bool open = door.Open;
            yield return Tap(Key.E); Check(door.Open == open, "E does not interact with focused door");
            yield return Tap(Key.F);
            Check(door.Open != open, "F activates focused door");
            yield return Capture("door-prompt");
            Check(_hud.Root.Q("world-card").resolvedStyle.display == DisplayStyle.None, "Door has only its F prompt, no object tooltip");
            _hud.enabled = false; yield return null; _hud.enabled = true; yield return null;
            Check(_hud.Root.Query<VisualElement>("slot-1").ToList().Count == 1, "HUD disable/enable does not duplicate tree or bindings");
            Check(_errors.Count == 0, "No runtime errors during UI verification");
            _player.InputEnabled = false;
            _player.Teleport(_session.Observation.position, _session.SeaEvent.position + Vector3.up * 6);
            if (_player.CameraEquipped) _player.ToggleCamera(); _player.SelectPhoto(0);
            _player.LockCursor(false);
            Passed = _checks.All(c => c.StartsWith("PASS")); Finished = true;
            Result = string.Join("\n", _checks);
            File.WriteAllText(Output + "verification.json", JsonUtility.ToJson(new Evidence { Passed = Passed, Checks = _checks.ToArray(), Errors = _errors.ToArray() }, true));
            Debug.Log("KEEPER_UI_VERIFICATION_COMPLETE " + Passed);
            CleanupInput();
        }

        private void CleanupInput()
        {
            if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
            if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
            _originalKeyboard?.MakeCurrent(); _originalMouse?.MakeCurrent();
            Application.logMessageReceived -= Log;
        }
        private void OnDestroy() { CleanupInput(); if (_player) _player.InputEnabled = true; }
        [Serializable] private class Evidence { public bool Passed; public string[] Checks, Errors; }
    }
}
#endif
