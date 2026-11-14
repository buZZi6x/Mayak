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
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Cursor = UnityEngine.Cursor;

public static class CheckMenuScenes
{
    private static readonly List<string> Checks = new List<string>();
    private static readonly List<string> Errors = new List<string>();
    private static readonly List<object> InputTrace = new List<object>();
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool condition, string label) => Checks.Add((condition ? "PASS " : "FAIL ") + label);
    private static void Log(string text, string trace, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Errors.Add(text); }
    private static async Task Wait(Func<bool> condition, string label)
    {
        for (int i = 0; i < 300; i++) { if (condition()) { await Task.Delay(150); return; } await Task.Delay(100); }
        throw new TimeoutException(label);
    }
    private static async Task Scene(string path)
    {
        await Wait(() => SceneManager.GetActiveScene().path == path && (path == KeeperMenu.ScenePath
            ? Object.FindAnyObjectByType<KeeperMenu>()?.Root != null
            : Object.FindAnyObjectByType<BlockoutSession>()?.Running == true), "Scene did not become ready: " + path);
        await Task.Delay(650);
    }
    private static async Task Submit(KeeperMenu menu, string name)
    {
        var button = menu.Root.Q<Button>(name);
        using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
        await Task.Delay(150);
    }
    private static async Task KeyPress(Keyboard keyboard, Key key)
    {
        if (!keyboard.enabled) InputSystem.EnableDevice(keyboard);
        keyboard.MakeCurrent();
        void Trace()
        {
            if (!keyboard[key].wasPressedThisFrame) return;
            var player = Object.FindAnyObjectByType<BlockoutPlayer>();
            InputTrace.Add(new { key = key.ToString(), update = InputState.currentUpdateType.ToString(), current = Keyboard.current == keyboard,
                deviceEnabled = keyboard.enabled, playerEnabled = player && player.enabled, inputEnabled = player && player.InputEnabled,
                menuOpen = player && player.Session.MenuOpen });
        }
        InputSystem.onAfterUpdate += Trace;
        try
        {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); await Task.Delay(100);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Task.Delay(150);
        }
        finally { InputSystem.onAfterUpdate -= Trace; }
    }
    private static int Cameras() => Object.FindObjectsByType<Camera>().Count(c => c.enabled);
    private static int Listeners() => Object.FindObjectsByType<AudioListener>().Count(c => c.enabled);

    public static async Task<object> MainAsync()
    {
        if (!Application.isPlaying || SceneManager.GetActiveScene().path != KeeperMenu.ScenePath)
            throw new InvalidOperationException("Start Play Mode in MainMenu first.");
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Artifacts/UI/MainMenu/SceneSplit"));
        string data = Path.Combine(output, "ProbeData", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(data);
        string settingsPath = Path.Combine(Application.persistentDataPath, "menu-settings.json");
        var backup = new Dictionary<string, byte[]>();
        foreach (var suffix in new[] { "", ".bak", ".tmp" })
        {
            string path = settingsPath + suffix;
            backup[path] = File.Exists(path) ? File.ReadAllBytes(path) : null;
            if (backup[path] != null) File.WriteAllBytes(Path.Combine(data, "settings-before" + suffix + ".bin"), backup[path]);
        }
        var background = InputSystem.settings.backgroundBehavior;
        var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        var originalKeyboard = Keyboard.current;
        var originalMouse = Mouse.current;
        bool keyboardEnabled = originalKeyboard != null && originalKeyboard.enabled;
        bool mouseEnabled = originalMouse != null && originalMouse.enabled;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var keyboard = InputSystem.AddDevice<Keyboard>("Scene transition probe keyboard");
        if (keyboardEnabled) InputSystem.DisableDevice(originalKeyboard);
        if (mouseEnabled) InputSystem.DisableDevice(originalMouse);
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        Application.logMessageReceived += Log;
        try
        {
            var menu = Object.FindAnyObjectByType<KeeperMenu>();
            var music = Object.FindObjectsByType<AudioSource>().Single(a => a.clip && a.clip.name == "DecksideJig");
            Check(music.loop && music.isPlaying && music.clip.channels == 2 && music.clip.loadType == AudioClipLoadType.Streaming, "Provided stereo track plays as streaming menu ambience");
            music.time = music.clip.length - .7f;
            await Task.Delay(2200);
            Check(music.isPlaying && music.time < 5, "Music crosses the end and loops without stopping");
            Check(!Object.FindAnyObjectByType<BlockoutSession>() && !Object.FindAnyObjectByType<BlockoutPlayer>() && !Object.FindAnyObjectByType<BlockoutHUD>(), "Menu scene contains no player, session or gameplay HUD");
            Check(!Object.FindAnyObjectByType<ObservationDirector>() && !Object.FindAnyObjectByType<WeepingGirlEncounter>(), "Menu does not run gameplay events");
            Check(menu.IsOpen && Cameras() == 1 && Listeners() == 1 && Cursor.visible, "Standalone menu owns one camera, listener and free cursor");
            var environment = CinematicEnvironment.Active;
            Check(environment.Ocean && !environment.Session && !environment.AdvanceTime && !environment.AutoWeather && Mathf.Abs(environment.Hour - 17.4f) < .01f, "Menu has its own live ocean and fixed sunset");
            var traffic = Object.FindAnyObjectByType<MarineTraffic>();
            var before = traffic.Ships[0].transform.position; await Task.Delay(350);
            Check(Vector3.Distance(before, traffic.Ships[0].transform.position) > .01f, "Background ships move without a gameplay session");
            await Submit(menu, "open-settings");
            Check(menu.CurrentPage == "settings", "Settings open in standalone menu");
            menu.Root.Q<OpticalSlider>("master").value = 31;
            menu.Root.Q<OpticalSlider>("ambience").value = 0;
            Check(music.volume == 0 && Mathf.Abs(AudioListener.volume - .31f) < .001f, "Master and ambience controls affect menu music");
            menu.Root.Q<OpticalSlider>("ambience").value = 41;
            menu.Root.Q<OpticalSlider>("effects").value = 23;
            await Submit(menu, "tab-controls");
            menu.Root.Q<OpticalSlider>("sensitivity").value = 1.7f;
            menu.Root.Q<Toggle>("invertY").value = true;
            await Submit(menu, "apply-settings");
            Check(MenuSettings.Load(settingsPath, out _).Master == 31 && menu.AppliedSettings.InvertY, "Native Apply persists settings before scene load");
            await Submit(menu, "settings-back");
            Texture2D captured = null;
            for (int cycle = 0; cycle < 2; cycle++)
            {
                var menuId = menu.GetEntityId();
                menu.Play(); menu.Play();
                await Scene(KeeperGameScene.ScenePath);
                var session = Object.FindAnyObjectByType<BlockoutSession>();
                var gameplay = Object.FindAnyObjectByType<KeeperGameScene>();
                Check(!menu && !Object.FindAnyObjectByType<KeeperMenu>() && session.Running && !session.MenuOpen && session.Day == 1 && session.Shots.Count == 0, "Play unloads menu and starts a clean run, cycle " + cycle);
                Check(Cameras() == 1 && Listeners() == 1 && SceneManager.sceneCount == 1, "Repeated Play cannot duplicate scenes, cameras or audio, cycle " + cycle);
                Check(!Object.FindObjectsByType<AudioSource>().Any(a => a.clip && a.clip.name == "DecksideJig"), "Menu music is unloaded during gameplay, cycle " + cycle);
                Check(gameplay.AppliedSettings.Master == 31 && Mathf.Abs(session.Player.LookSensitivity - 1.7f) < .01f && session.Player.InvertLookY, "Gameplay reloads saved audio and mouse settings, cycle " + cycle);
                Check(CinematicEnvironment.Active.Ambience.MenuMultiplier == 1 && Mathf.Abs(CinematicEnvironment.Active.Ambience.EffectsVolume - .23f) < .001f && gameplay.GetComponent<MenuBackdrop>().Page == -1, "Gameplay restores effects and removes menu interference, cycle " + cycle);
                bool light = session.Player.FlashlightOn, hidden = session.Player.UIHidden;
                await KeyPress(keyboard, Key.Q); await KeyPress(keyboard, Key.Tab);
                Check(session.Player.FlashlightOn != light && session.Player.UIHidden != hidden, "Game input works after scene transition, cycle " + cycle);
                await KeyPress(keyboard, Key.Tab);
                // Keep real test photographs outside the user's archive.
                var archive = new PhotoArchive(Path.Combine(data, "photos"), session.Best);
                typeof(KeeperGameScene).GetField("_archive", Private).SetValue(gameplay, archive);
                if (cycle == 0)
                {
                    session.Preparation = 0;
                    var ship = session.Director.Traffic.Ships[2];
                    bool playerEnabled = session.Player.enabled;
                    var position = session.Player.transform.position;
                    var lookAt = session.Player.View.transform.position + session.Player.View.transform.forward * 10;
                    session.Player.enabled = false;
                    try
                    {
                    session.Player.Teleport(ship.transform.position + Vector3.back * 90 + Vector3.up * 16, ship.transform.position + Vector3.up * 14);
                    await Task.Delay(200); session.Capture();
                    await Wait(() => session.Shots.Count > 0 && session.Shots[0].Image, "Photograph did not finish");
                    captured = session.Shots[0].Image;
                    Check(session.Shots[0].Price > 0 && archive.Data.Photos.Count == 1 && File.Exists(Path.Combine(data, "photos", archive.Data.Photos[0].Id + ".jpg")), "Gameplay scene writes a real priced photograph through its archive event");
                    }
                    finally
                    {
                        session.Player.Teleport(position, lookAt);
                        session.Player.enabled = playerEnabled;
                    }
                }
                session.Score = 0;
                session.EndRun("Проверка перехода сцен");
                await KeyPress(keyboard, Key.Escape);
                await Scene(KeeperMenu.ScenePath);
                menu = Object.FindAnyObjectByType<KeeperMenu>();
                Check(!gameplay && !session && menu.GetEntityId() != menuId && menu.IsOpen && !Object.FindAnyObjectByType<BlockoutPlayer>(), "Escape after defeat returns to a fresh menu scene, cycle " + cycle);
                Check(Cameras() == 1 && Listeners() == 1 && Mathf.Abs(CinematicEnvironment.Active.Ambience.MenuMultiplier - .12f) < .001f && Cursor.visible, "Return restores quiet sea, one listener and free cursor, cycle " + cycle);
                Check(menu.AppliedSettings.Master == 31 && menu.AppliedSettings.InvertY, "Settings survive the round trip, cycle " + cycle);
                Check(Object.FindObjectsByType<AudioSource>().Count(a => a.clip && a.clip.name == "DecksideJig" && a.isPlaying && a.loop) == 1, "Returning starts exactly one looping music source, cycle " + cycle);
                Check(!captured, "Unloading gameplay releases captured textures, cycle " + cycle);
            }
            var reloaded = new PhotoArchive(Path.Combine(data, "photos"), 0);
            typeof(KeeperMenu).GetField("_archive", Private).SetValue(menu, reloaded);
            await Submit(menu, "open-gallery");
            Check(reloaded.Data.Photos.Count == 1 && menu.Root.Q("archive-photo-0") != null, "Returned menu reads and displays a persisted photo archive");
            await Submit(menu, "archive-photo-0");
            Check(menu.Root.Q<Image>("viewer-image").image != null, "Archived full photograph opens after both scene unloads");
        }
        catch (Exception exception) { Errors.Add(exception.ToString()); }
        finally
        {
            foreach (var pair in backup)
            {
                if (pair.Value == null) { if (File.Exists(pair.Key)) File.Delete(pair.Key); }
                else File.WriteAllBytes(pair.Key, pair.Value);
            }
            InputSystem.RemoveDevice(keyboard);
            if (keyboardEnabled && originalKeyboard.added) InputSystem.EnableDevice(originalKeyboard);
            if (mouseEnabled && originalMouse.added) InputSystem.EnableDevice(originalMouse);
            if (originalKeyboard != null && originalKeyboard.added) originalKeyboard.MakeCurrent();
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            try { _ = SceneManager.LoadSceneAsync(KeeperMenu.ScenePath); await Scene(KeeperMenu.ScenePath); }
            catch (Exception exception) { Errors.Add(exception.ToString()); }
            Application.logMessageReceived -= Log;
        }
        var report = new { Passed = Errors.Count == 0 && Checks.All(x => x.StartsWith("PASS")), PassedCount = Checks.Count(x => x.StartsWith("PASS")), FailedCount = Checks.Count(x => x.StartsWith("FAIL")), Checks, Errors, InputTrace, DataDirectory = data, SettingsRestored = backup.All(p => p.Value == null ? !File.Exists(p.Key) : File.ReadAllBytes(p.Key).SequenceEqual(p.Value)) };
        File.WriteAllText(Path.Combine(output, "runtime.json"), Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
        return report;
    }
}
