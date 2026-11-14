using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using LighthouseGame;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Cursor = UnityEngine.Cursor;
using MouseButton = UnityEngine.InputSystem.LowLevel.MouseButton;

// Ephemeral CLI probe: native UI events and Input System gameplay keys, with isolated saves.
public static class CheckMenuRuntime
{
    private static KeeperMenu _menu;
    private static readonly List<string> Checks = new List<string>();
    private static readonly List<string> Errors = new List<string>();
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object Get(string name) => typeof(KeeperMenu).GetField(name, Private).GetValue(_menu);
    private static void Set(string name, object value) => typeof(KeeperMenu).GetField(name, Private).SetValue(_menu, value);
    private static object Call(string name, params object[] args) => typeof(KeeperMenu).GetMethod(name, Private).Invoke(_menu, args);
    private static MenuSettings Draft => (MenuSettings)Get("_draft");
    private static bool Visible(string name) => !_menu.Root.Q(name).ClassListContains("hidden");
    private static void Check(bool success, string label) => Checks.Add((success ? "PASS " : "FAIL ") + label);
    private static void Log(string message, string trace, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Errors.Add(message); }
    private static async Task Key(Key key)
    {
        if (_menu.IsOpen)
        {
            var target = _menu.Root.panel.focusController.focusedElement as VisualElement ?? _menu.Root;
            var code = key == UnityEngine.InputSystem.Key.Enter ? KeyCode.Return : (KeyCode)Enum.Parse(typeof(KeyCode), key.ToString());
            using (var evt = KeyDownEvent.GetPooled(new Event { type=EventType.KeyDown, keyCode=code })) { evt.target=target; target.SendEvent(evt); }
            if (code == KeyCode.Return || code == KeyCode.Space)
            { using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target=target; target.SendEvent(evt); } }
            await Task.Delay(120); return;
        }
        await GameKey(key);
    }
    private static async Task GameKey(Key key)
    {
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key)); await Task.Delay(80);
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()); await Task.Delay(100);
    }
    private static async Task Click(VisualElement element, float fraction = .5f)
    {
        var rect = element.worldBound;
        var position = new Vector2(rect.x + rect.width * fraction, rect.center.y);
        var hit = _menu.Root.panel.Pick(position);
        if (hit == null) throw new InvalidOperationException("No UI hit at " + element.name + " " + position);
        using (var evt = PointerDownEvent.GetPooled(new Event { type=EventType.MouseDown, mousePosition=position, button=0, clickCount=1 })) { evt.target=hit; hit.SendEvent(evt); }
        await Task.Delay(60);
        using (var evt = PointerUpEvent.GetPooled(new Event { type=EventType.MouseUp, mousePosition=position, button=0, clickCount=1 })) { evt.target=hit; hit.SendEvent(evt); }
        await Task.Delay(120);
    }
    private static Task Click(string name) => Click(_menu.Root.Q(name));
    private static async Task Choice(string name, int index)
    { await Click(name); await Click(_menu.Root.Q("choice-popup")[index]); }
    private static async Task Shot(BlockoutSession session, int shipIndex)
    {
        var ship = session.Director.Traffic.Ships[shipIndex];
        session.Player.Teleport(ship.transform.position + Vector3.back * 90 + Vector3.up * 16, ship.transform.position + Vector3.up * 14);
        session.BusyUntil = 0; await Task.Delay(150); session.Capture(); await Task.Delay(500);
    }
    [Serializable] private sealed class Report
    { public bool Passed; public int PassedCount, FailedCount; public string[] Checks, Errors; public string DataDirectory; }

    public static async Task<object> Main()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Start a fresh Play Mode first.");
        _menu = Object.FindAnyObjectByType<KeeperMenu>();
        if (!_menu || !_menu.enabled || _menu.Root == null) throw new InvalidOperationException("Menu must be enabled; do not overlap other probes.");
        var session = Object.FindAnyObjectByType<BlockoutSession>();
        var hud = Object.FindAnyObjectByType<BlockoutHUD>();
        var backdrop = _menu.GetComponent<MenuBackdrop>();
        var ambience = CinematicEnvironment.Active.Ambience;
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Artifacts/UI/MainMenu/Unity"));
        string data = Path.Combine(output, "RuntimeData", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(data);
        string originalPath = (string)Get("_settingsPath");
        var originalSettings = _menu.AppliedSettings;
        var originalArchive = _menu.Archive;
        var background = InputSystem.settings.backgroundBehavior;
        var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        var originalMouse = Mouse.current; var originalKeyboard = Keyboard.current;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var probeMouse = InputSystem.AddDevice<Mouse>("Menu verification mouse");
        var probeKeyboard = InputSystem.AddDevice<Keyboard>("Menu verification keyboard");
        string settingsPath = Path.Combine(data, "settings.json");
        Set("_settingsPath", settingsPath); Set("_archive", new PhotoArchive(Path.Combine(data, "photos"), 0));
        Set("_saved", new MenuSettings()); Set("_draft", new MenuSettings()); Call("ApplyRuntime", _menu.AppliedSettings);
        Application.logMessageReceived += Log;
        try
        {
            _menu.OpenHome(); await Task.Delay(200);
            Check(_menu.IsOpen && Cursor.lockState == CursorLockMode.None && hud.Root.resolvedStyle.display == DisplayStyle.None, "Startup menu owns cursor and hides the gameplay HUD");
            var position = session.Player.transform.position; bool camera = session.Player.CameraEquipped, torch = session.Player.FlashlightOn;
            await GameKey(UnityEngine.InputSystem.Key.W); await GameKey(UnityEngine.InputSystem.Key.E); await GameKey(UnityEngine.InputSystem.Key.Q); await GameKey(UnityEngine.InputSystem.Key.F1);
            Check(Vector3.Distance(position, session.Player.transform.position) < .001f && camera == session.Player.CameraEquipped && torch == session.Player.FlashlightOn && !session.Running, "Menu blocks movement, equipment and the debug restart key");
            await Click("open-settings"); Check(_menu.CurrentPage == "settings" && Visible("panel-audio"), "Mouse opens the audio settings");
            await Click(_menu.Root.Q("master"), .3f);
            Check(Draft.Master >= 25 && Draft.Master <= 35 && Mathf.Abs(AudioListener.volume - Draft.Master / 100f) < .001f, "Segmented scale hit testing updates the real master volume");
            _menu.Root.Q("master").Focus(); await Key(UnityEngine.InputSystem.Key.Home);
            Check(Draft.Master == 0 && AudioListener.volume == 0, "Slider Home mutes all audio");
            await Key(UnityEngine.InputSystem.Key.End); Check(Draft.Master == 100, "Slider End reaches its upper bound");
            await Key(UnityEngine.InputSystem.Key.Escape);
            Check(_menu.CurrentPage == "home" && AudioListener.volume == .8f && !File.Exists(settingsPath), "Back discards the preview without writing settings");
            await Click("open-settings"); _menu.Root.Q("tab-audio").Focus(); await Key(UnityEngine.InputSystem.Key.RightArrow);
            Check(Visible("panel-video") && _menu.Root.Q("tab-video").ClassListContains("active"), "Arrow key changes the settings tab");
            Check(!_menu.Root.Q("fps").enabledInHierarchy, "VSync disables the manual frame limit");
            await Click("vsync"); Check(!Draft.VSync && _menu.Root.Q("fps").enabledInHierarchy, "Custom toggle enables manual frame limiting");
            await Click("fps"); await Key(UnityEngine.InputSystem.Key.DownArrow); await Key(UnityEngine.InputSystem.Key.Enter);
            Check(Draft.FrameLimit == 120 && !Visible("choice-popup"), "Custom selector supports arrows and Enter");
            await Click("resolution"); await Key(UnityEngine.InputSystem.Key.Escape);
            Check(_menu.CurrentPage == "settings" && !Visible("choice-popup"), "Escape closes only the open selector");
            await Click("tab-controls"); _menu.Root.Q("sensitivity").Focus(); await Key(UnityEngine.InputSystem.Key.End); await Click("invertY");
            await Click("apply-settings");
            var loaded = MenuSettings.Load(settingsPath, out string loadError);
            Check(loadError == null && loaded.Sensitivity == 3 && loaded.InvertY && loaded.FrameLimit == 120 && !loaded.VSync && session.Player.LookSensitivity == 3 && session.Player.InvertLookY && QualitySettings.vSyncCount == 0, "Apply persists all tabs and updates real input and rendering settings");
            await Click("reset-settings");
            Check(Draft.Sensitivity == 1 && _menu.AppliedSettings.Sensitivity == 3 && session.Player.LookSensitivity == 3, "Reset changes only the draft until applied");
            await Key(UnityEngine.InputSystem.Key.Escape); await Click("open-settings"); await Click("tab-video");
            await Choice("display", 2); await Click("apply-settings");
            Check(Visible("dialog") && ((Label)_menu.Root.Q("dialog-copy")).text.Contains("с."), "Display change opens the timed confirmation");
            await Key(UnityEngine.InputSystem.Key.Escape);
            Check(!Visible("dialog") && Draft.Display == 0 && _menu.AppliedSettings.Display == 0, "Escape rolls back the pending display mode");
            await Choice("display", 2); await Click("apply-settings"); Set("_displayDeadline", Time.unscaledTime - 1); await Task.Delay(250);
            Check(!Visible("dialog") && Draft.Display == 0 && _menu.AppliedSettings.Display == 0, "Timed-out display change reverts automatically");
            await Choice("display", 2); await Click("apply-settings"); await Click("dialog-confirm");
            Check(_menu.AppliedSettings.Display == 2 && MenuSettings.Load(settingsPath, out _).Display == 2, "Confirmed display preference persists");
            await Click("tab-audio"); _menu.Root.Q<OpticalSlider>("ambience").value = 0; await Task.Delay(150);
            Check(ambience.GetComponentsInChildren<AudioSource>().All(s => s.volume == 0), "Environment mute includes already playing surf and thunder");
            await Click("test-audio");
            Check(_menu.GetComponents<AudioSource>().Any(s => s.clip && s.clip.name == "menu-shutter" && s.volume > 0), "Sound test uses the effects preview channel");
            await Key(UnityEngine.InputSystem.Key.Escape); await Click("open-gallery");
            Check(Visible("empty-archive") && _menu.Root.Q("photo-grid").childCount == 0, "New archive has an honest empty state");
            _menu.ShowPage("home"); await Task.Delay(120); await Click("play");
            Check(!_menu.IsOpen && session.Running && !session.GameOver && session.Shots.Count == 0 && Cursor.lockState == CursorLockMode.Locked && hud.Root.resolvedStyle.display != DisplayStyle.None, "Play starts a clean run, restores HUD and captures the cursor");
            Check(backdrop.Page == -1 && backdrop.CurrentGlitch == 0 && ambience.MenuMultiplier == 1 && ambience.EffectsVolume > 0, "Gameplay restores the audio mix and stops menu interference");
            bool hidden = session.Player.UIHidden, light = session.Player.FlashlightOn;
            await Key(UnityEngine.InputSystem.Key.Tab); await Key(UnityEngine.InputSystem.Key.Q);
            Check(session.Player.UIHidden != hidden && session.Player.FlashlightOn != light, "HUD hide and flashlight still work after leaving the menu");
            await Key(UnityEngine.InputSystem.Key.Tab);
            session.enabled = false; session.Preparation = 0; session.Fuel = session.Lamp = 1000; session.Player.InputEnabled = false;
            for (int i = 0; i < 3; i++) await Shot(session, i);
            Check(session.Shots.Count == 3 && _menu.Archive.Data.Photos.Count == 3 && _menu.Archive.Data.Photos.All(p => p.Price > 0), "Three real game captures reach the permanent archive after their image completes");
            var reopened = new PhotoArchive(Path.Combine(data, "photos"), 0);
            Check(reopened.Data.Photos.Count == 3 && reopened.Data.Best == session.Score && reopened.Data.Photos.Select(p => p.Price).SequenceEqual(reopened.Data.Photos.Select(p => p.Price).OrderByDescending(p => p)), "Reloaded archive keeps real prices, record and descending order");
            var image = reopened.Data.Photos.Count > 0 ? reopened.LoadImage(reopened.Data.Photos[0]) : null;
            Check(image && image.width > 1000 && image.height > 500, "Archived full-frame JPEG decodes independently of live inventory");
            if (image) Object.Destroy(image);
            session.BeginRun(); session.enabled = false; session.Player.InputEnabled = true;
            Check(session.Shots.Count == 0 && _menu.Archive.Data.Photos.Count == 3, "Starting another run does not clear the archive");
            session.GameOver = true; await Key(UnityEngine.InputSystem.Key.Escape);
            Check(_menu.IsOpen && _menu.CurrentPage == "home", "Escape from game over returns to the menu");
            await Click("open-gallery"); await Click("archive-photo-0");
            Check(Visible("photo-viewer") && _menu.Root.Q<Image>("viewer-image").image, "Real archive card opens its full image");
            await Key(UnityEngine.InputSystem.Key.RightArrow);
            Check(_menu.Root.Q<Label>("viewer-position").text.StartsWith("2"), "Photo viewer supports keyboard paging");
            await Key(UnityEngine.InputSystem.Key.Escape);
            Check(!Visible("photo-viewer") && _menu.CurrentPage == "gallery", "Escape closes the photo viewer and stays in the archive");
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "gallery-runtime.png")); await Task.Delay(150);
            _menu.ShowPage("settings"); _menu.SelectTab("video");
            _menu.Root.Q<Toggle>("glitches").value = false; backdrop.PreviewGlitch(); await Task.Delay(200);
            Check(backdrop.CurrentGlitch == 0, "Disabling background faults cancels an active interference preview");
            _menu.Root.Q<Toggle>("glitches").value = true; backdrop.PreviewGlitch(); await Task.Delay(250);
            Check(backdrop.CurrentGlitch > .8f, "Red interference runs on the real menu camera");
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "glitch-runtime.png")); await Task.Delay(150);
        }
        catch (Exception exception) { Errors.Add(exception.ToString()); }
        finally
        {
            Application.logMessageReceived -= Log;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = new Vector2(10, 10) });
            Set("_settingsPath", originalPath); Set("_saved", originalSettings); Set("_draft", originalSettings.Clone());
            Set("_archive", originalArchive); Call("ApplyRuntime", originalSettings);
            session.enabled = true; session.Player.InputEnabled = true;
            _menu.OpenHome(); _menu.ShowPage("settings");
            InputSystem.RemoveDevice(probeMouse); InputSystem.RemoveDevice(probeKeyboard);
            originalMouse?.MakeCurrent(); originalKeyboard?.MakeCurrent();
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
        }
        var report = new Report { PassedCount = Checks.Count(c => c.StartsWith("PASS ")), FailedCount = Checks.Count(c => c.StartsWith("FAIL ")), Checks = Checks.ToArray(), Errors = Errors.ToArray(), DataDirectory = data };
        report.Passed = report.FailedCount == 0 && report.Errors.Length == 0;
        File.WriteAllText(Path.Combine(output, "runtime.json"), JsonUtility.ToJson(report, true));
        return report;
    }
}
