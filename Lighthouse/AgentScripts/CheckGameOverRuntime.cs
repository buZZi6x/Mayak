using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using LighthouseGame;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Cursor = UnityEngine.Cursor;

// Ephemeral CLI verification: no test components or demo values are saved in scenes.
public static class CheckGameOverRuntime
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const string BestKey = "Lighthouse.Blockout.Best";
    private static readonly List<string> Checks = new List<string>();
    private static readonly List<string> Errors = new List<string>();
    private static readonly List<object> Layouts = new List<object>();
    private static BlockoutSession _session;
    private static BlockoutHUD _hud;
    private static Keyboard _keyboard;
    private static Mouse _mouse;
    private static bool _hadBest;
    private static int _savedBest;
    private static string _output;
    private static void Check(bool value, string name) { Checks.Add((value ? "PASS " : "FAIL ") + name); }
    private static void Log(string message, string trace, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert || type == LogType.Warning) { Errors.Add(message); } }
    private static void RestoreBest()
    {
        if (_hadBest) { PlayerPrefs.SetInt(BestKey, _savedBest); }
        else { PlayerPrefs.DeleteKey(BestKey); }
        PlayerPrefs.Save();
    }
    private static void Acquire()
    {
        _session = Object.FindAnyObjectByType<BlockoutSession>();
        _hud = Object.FindAnyObjectByType<BlockoutHUD>();
        var game = Object.FindAnyObjectByType<KeeperGameScene>();
        // EndRun and ReturnToMenu exercise the real archive calls in a project-local sandbox.
        typeof(KeeperGameScene).GetField("_archive", Flags).SetValue(game, new PhotoArchive(Path.Combine(_output, "test-archive"), _savedBest));
        _session.Player.InputEnabled = true;
    }
    private static void Begin()
    {
        _session.BeginRun();
        if (_session.Player.UIHidden) { _session.Player.ToggleUI(); }
        CinematicEnvironment.Active.AdvanceTime = false;
        CinematicEnvironment.Active.SetHour(0);
        _session.Director.FreezePreview = true;
        _session.Observations.Automatic = false;
    }
    private static async Task End(string reason, int score = 480, int best = 1260)
    {
        _session.Score = score; _session.Best = best;
        _session.EndRun(reason); RestoreBest();
        await Task.Delay(250);
    }
    private static async Task GameKey(Key key)
    {
        InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
        await Frames(3);
        InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
        await Frames(3);
    }
    private static async Task Frames(int count)
    {
        int target = Time.frameCount + count;
        for (int i = 0; i < 250 && Time.frameCount < target; i++)
        {
            if (!EditorApplication.isPlaying) { throw new InvalidOperationException("Play Mode stopped during verification."); }
            await Task.Delay(20);
        }
        if (Time.frameCount < target) { throw new TimeoutException("Game frames did not advance."); }
    }
    private static async Task UiKey(KeyCode key, bool shift = false)
    {
        var target = _hud.Root.panel.focusController.focusedElement as VisualElement ?? _hud.Root.Q("game-over");
        using (var evt = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = key, modifiers = shift ? EventModifiers.Shift : EventModifiers.None }))
        { evt.target = target; target.SendEvent(evt); }
        await Task.Delay(100);
    }
    private static async Task Click(string name)
    {
        var element = _hud.Root.Q(name);
        var position = element.worldBound.center;
        var hit = _hud.Root.panel.Pick(position);
        Check(hit == element || element.Contains(hit), "Hit testing resolves " + name);
        var screen = new Vector2(position.x, Screen.height - position.y);
        InputSystem.QueueStateEvent(_mouse, new MouseState { position = screen });
        await Frames(2);
        InputSystem.QueueStateEvent(_mouse, new MouseState { position = screen, buttons = 1 });
        await Frames(2);
        InputSystem.QueueStateEvent(_mouse, new MouseState { position = screen });
        await Frames(3);
    }
    private static bool Shown(string name) { return _hud.Root.Q(name).resolvedStyle.display != DisplayStyle.None; }
    private static string Focused() { return (_hud.Root.panel.focusController.focusedElement as VisualElement)?.name; }
    private static EditorWindow GameView { get { return EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")); } }
    private static void Resize(int width, int height)
    {
        var assembly = typeof(EditorWindow).Assembly;
        var type = assembly.GetType("UnityEditor.GameViewSizes");
        var singleton = typeof(ScriptableSingleton<>).MakeGenericType(type);
        var sizes = singleton.GetProperty("instance", Flags).GetValue(null);
        var group = type.GetProperty("currentGroup", Flags).GetValue(sizes);
        var groupType = group.GetType();
        int count = (int)groupType.GetMethod("GetTotalCount", Flags).Invoke(group, null), index = -1;
        for (int i = 0; i < count; i++)
        {
            var size = groupType.GetMethod("GetGameViewSize", Flags).Invoke(group, new object[] { i });
            if ((int)size.GetType().GetProperty("width", Flags).GetValue(size) == width && (int)size.GetType().GetProperty("height", Flags).GetValue(size) == height) { index = i; break; }
        }
        if (index < 0)
        {
            var size = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"), Flags, null,
                new object[] { Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution"), width, height, "Keeper UI " + width + "x" + height }, null);
            groupType.GetMethod("AddCustomSize", Flags).Invoke(group, new[] { size }); index = count;
        }
        var view = GameView;
        view.GetType().GetProperty("selectedSizeIndex", Flags).SetValue(view, index);
        view.Focus(); view.Repaint();
    }
    private static async Task Capture(int width, int height)
    {
        Resize(width, height); await Task.Delay(350);
        var panel = _hud.Root.Q("result-panel").worldBound;
        string[] names = { "result-title", "result-reason", "result-score", "result-best", "restart", "result-menu" };
        var bounds = names.Select(name => new { name, rect = _hud.Root.Q(name).worldBound }).ToArray();
        bool fits = panel.x >= 0 && panel.y >= 0 && panel.xMax <= Screen.width + 1 && panel.yMax <= Screen.height + 1
            && bounds.All(item => item.rect.xMin >= panel.xMin && item.rect.xMax <= panel.xMax && item.rect.yMin >= panel.yMin && item.rect.yMax <= panel.yMax);
        bool textFits = new[] { "result-score", "result-best" }.All(name =>
        {
            var label = _hud.Root.Q<Label>(name);
            return label.MeasureTextSize(label.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x <= label.contentRect.width + 2;
        });
        Check(fits && textFits, "Result and labels fit " + width + "x" + height);
        Layouts.Add(new { width = Screen.width, height = Screen.height, panel = RectData(panel), bounds = bounds.Select(item => new { item.name, rect = RectData(item.rect) }).ToArray(), fits, textFits });
        ScreenCapture.CaptureScreenshot(Path.Combine(_output, width + "x" + height + ".png"));
        await Task.Delay(250);
    }
    private static async Task WaitForScene(string path)
    {
        for (int i = 0; i < 100 && SceneManager.GetActiveScene().path != path; i++) { await Task.Delay(100); }
        await Task.Delay(300);
        Check(SceneManager.GetActiveScene().path == path, "Loaded " + path);
    }
    private static object RectData(Rect rect) { return new { x = rect.x, y = rect.y, width = rect.width, height = rect.height }; }
    private static async Task LoadGame()
    {
        RestoreBest();
        var operation = SceneManager.LoadSceneAsync(KeeperGameScene.ScenePath);
        while (!operation.isDone) { await Task.Delay(100); }
        await Task.Delay(300); Acquire(); Begin();
    }
    public static async Task<object> Main()
    {
        if (!EditorApplication.isPlaying) { throw new InvalidOperationException("Start LighthouseBlockout in Play Mode first."); }
        var otherProbes = Object.FindObjectsByType<MonoBehaviour>().Where(component => component.enabled && component.GetType().Name.EndsWith("Probe")
            && !Equals(component.GetType().GetField("Done")?.GetValue(component), true)
            && !Equals(component.GetType().GetField("Finished")?.GetValue(component), true)).Select(component => component.GetType().Name).ToArray();
        if (otherProbes.Length > 0) { throw new InvalidOperationException("Another runtime probe is active: " + string.Join(", ", otherProbes)); }
        _output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Artifacts/UI/GameOver/Unity"));
        Directory.CreateDirectory(_output);
        _hadBest = PlayerPrefs.HasKey(BestKey); _savedBest = PlayerPrefs.GetInt(BestKey, 0);
        File.WriteAllText(Path.Combine(_output, "saved-best-before.json"), Newtonsoft.Json.JsonConvert.SerializeObject(new { existed = _hadBest, value = _savedBest }));
        var originalKeyboard = Keyboard.current;
        var originalMouse = Mouse.current;
        var background = InputSystem.settings.backgroundBehavior;
        var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        int originalSize = (int)GameView.GetType().GetProperty("selectedSizeIndex", Flags).GetValue(GameView);
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        _keyboard = InputSystem.AddDevice<Keyboard>("Game over verification keyboard");
        _mouse = InputSystem.AddDevice<Mouse>("Game over verification mouse");
        Application.logMessageReceived += Log;
        try
        {
            Acquire(); Begin(); GameView.Focus(); await Frames(3);
            await GameKey(Key.Tab);
            Check(_session.Player.UIHidden, "Tab hides the HUD before defeat");
            await End("Топливо кончилось. Маяк погас.");
            Check(Shown("game-over") && !Shown("hud"), "Result replaces HUD even when Tab had hidden it");
            Check(_hud.Root.Q<Label>("result-score").text == "480" && _hud.Root.Q<Label>("result-best").text == 1260.ToString("N0", CultureInfo.GetCultureInfo("ru-RU")), "Separate result fields use actual score and best");
            Check(_hud.Root.Q<Label>("result-reason").text == _session.EndReason && Cursor.lockState == CursorLockMode.None, "Actual end reason and free cursor");
            bool hidden = _session.Player.UIHidden, camera = _session.Player.CameraEquipped, torch = _session.Player.FlashlightOn;
            int shots = _session.Shots.Count;
            await GameKey(Key.Tab); await GameKey(Key.Q); await GameKey(Key.E); await GameKey(Key.F);
            _session.Capture();
            Check(Shown("game-over") && _session.Player.UIHidden == hidden && camera == _session.Player.CameraEquipped && torch == _session.Player.FlashlightOn && shots == _session.Shots.Count, "Result blocks HUD toggle, equipment, interaction and capture");
            await UiKey(KeyCode.Tab); Check(Focused() == "restart", "Tab selects restart");
            await UiKey(KeyCode.Tab); Check(Focused() == "result-menu", "Tab selects menu");
            await UiKey(KeyCode.Tab); Check(Focused() == "restart", "Tab wraps within result");
            await UiKey(KeyCode.Tab, true); Check(Focused() == "result-menu", "Shift+Tab wraps backwards");
            await UiKey(KeyCode.LeftArrow); Check(Focused() == "restart", "Arrow switches actions");
            await UiKey(KeyCode.Return);
            Check(!_session.GameOver && _session.Score == 0 && _session.Day == 1 && _session.Fuel > 119 && !Shown("game-over"), "Enter starts a real fresh run");
            Begin(); await End("Не успели починить маяк.");
            Check(_hud.Root.Q<Label>("result-reason").text == "Не успели починить маяк.", "Current lamp failure copy comes from session");
            await GameKey(Key.F1);
            Check(!_session.GameOver && _session.Score == 0 && !Shown("game-over") && Shown("hud"), "F1 restarts and restores normal HUD");
            await End("Она заметила твой взгляд.");
            Check(_hud.Root.Q<Label>("result-reason").text == "Она заметила твой взгляд.", "Encounter failure uses the same result panel");
            _hud.enabled = false; _hud.enabled = true; await Task.Delay(150);
            Check(_hud.Root.Query(name: "game-over").ToList().Count == 1 && Shown("game-over"), "Disable/enable rebuilds one result panel");
            int runs = 0; Action countRun = () => runs++; _session.RunBegan += countRun;
            await Click("restart"); _session.RunBegan -= countRun;
            Check(runs == 1 && !_session.GameOver && Cursor.lockState == CursorLockMode.Locked, "Mouse restart fires once after HUD re-enable");
            Begin(); await End("Топливо кончилось. Маяк погас.");
            await Capture(1920, 1080); await Capture(1280, 720); await Capture(390, 844); await Capture(844, 390);
            _session.Score = _session.Best = int.MaxValue; _session.Notify(string.Empty); await Task.Delay(150);
            Resize(390, 844); await Task.Delay(200);
            Check(_hud.Root.Q<Label>("result-score").text.Replace("\u00a0", "").Replace(" ", "") == int.MaxValue.ToString(), "Large real scores retain every digit");
            Resize(1280, 720); await Task.Delay(200);
            _session.Score = 480; _session.Best = 1260; _session.Notify(string.Empty); RestoreBest(); await Task.Delay(100);
            await Click("result-menu"); await WaitForScene(KeeperMenu.ScenePath);
            Check(Object.FindAnyObjectByType<KeeperMenu>()?.IsOpen == true && !Object.FindAnyObjectByType<BlockoutSession>(), "Menu button loads the separate main-menu scene");
            await LoadGame(); await End("Топливо кончилось. Маяк погас."); RestoreBest();
            await GameKey(Key.Escape); await WaitForScene(KeeperMenu.ScenePath);
            Check(Object.FindAnyObjectByType<KeeperMenu>()?.IsOpen == true, "Escape loads the same main menu");
            await LoadGame();
        }
        catch (Exception exception) { Errors.Add(exception.ToString()); }
        finally
        {
            Application.logMessageReceived -= Log;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            InputSystem.RemoveDevice(_keyboard); originalKeyboard?.MakeCurrent();
            InputSystem.RemoveDevice(_mouse); originalMouse?.MakeCurrent();
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            RestoreBest();
            if (_session) { _session.Best = _savedBest; _session.Player.InputEnabled = true; }
            GameView.GetType().GetProperty("selectedSizeIndex", Flags).SetValue(GameView, originalSize);
        }
        var report = new { passed = Checks.All(check => check.StartsWith("PASS ")) && Errors.Count == 0, passedCount = Checks.Count(check => check.StartsWith("PASS ")), failedCount = Checks.Count(check => check.StartsWith("FAIL ")), checks = Checks, errors = Errors, layouts = Layouts, bestRestored = PlayerPrefs.GetInt(BestKey, 0) == _savedBest && PlayerPrefs.HasKey(BestKey) == _hadBest };
        File.WriteAllText(Path.Combine(_output, "runtime.json"), Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
        return report;
    }
}
