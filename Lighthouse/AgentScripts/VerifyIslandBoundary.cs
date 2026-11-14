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
using Object = UnityEngine.Object;

// One-off CLI validation, outside Assets: never included in the player build.
public static class VerifyIslandBoundary
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<object> Checks = new List<object>();
    private static readonly List<string> Errors = new List<string>();
    private static readonly List<object> Routes = new List<object>();
    private static BlockoutPlayer _player;
    private static CharacterController _motor;
    private static MethodInfo _move, _contains;
    private static PhysicsScene _physics;
    private static int _failures;
    private static void Check(string name, bool passed, object detail = null)
    {
        Checks.Add(new { name, passed, detail });
        if (!passed) _failures++;
    }
    private static void OnLog(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Errors.Add(message); }
    private static bool Contains(Vector3 position) => (bool)_contains.Invoke(_player, new object[] { position });
    private static void Move(Vector3 displacement) => _move.Invoke(_player, new object[] { displacement });
    private static void Place(Vector3 position, Vector3 direction)
    {
        _player.Teleport(position, position + Vector3.up * 1.65f + direction);
        Physics.SyncTransforms();
    }
    private static bool Supported(Vector3 position)
    {
        // Independent physics evidence: a small part of the actual capsule footprint
        // has support on the real deck. A point ray can fall through a plank seam.
        var hits = new RaycastHit[64];
        int count = _physics.SphereCast(position + Vector3.up * 30, .1f, Vector3.down, hits, 60, ~0, QueryTriggerInteraction.Ignore);
        return hits.Take(count).Any(hit => (hit.collider.name == "COLL_Terrace" || hit.collider.name == "SOLID_Terrace" ||
                hit.collider.name.StartsWith("Pier · weathered board ")) && hit.point.y > 4);
    }
    private static void Settle()
    {
        float speed = 0;
        for (int i = 0; i < 150; i++)
        {
            speed = _motor.isGrounded ? -2 : speed - 18 * .02f;
            Move(Vector3.up * speed * .02f);
        }
    }
    private static async Task NextFrame()
    {
        int frame = Time.frameCount;
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (frame == Time.frameCount)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Play Mode was stopped during the boundary check.");
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The editor stopped advancing game frames.");
            await Task.Delay(5);
        }
    }

    public static async Task<string> Main(bool nativeInput = true)
    {
        if (nativeInput && !Application.isPlaying) throw new InvalidOperationException("Play Mode is required for native input checks.");
        var preview = nativeInput ? default : UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/Scenes/LighthouseBlockout.unity");
        _player = nativeInput ? Object.FindAnyObjectByType<BlockoutPlayer>() : preview.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BlockoutPlayer>()).Single();
        _motor = _player.GetComponent<CharacterController>();
        if (!nativeInput)
        {
            typeof(BlockoutPlayer).GetField("motor", Private).SetValue(_player, _motor);
            typeof(BlockoutPlayer).GetMethod("InitializeIslandBoundary", Private).Invoke(_player, null);
        }
        _physics = _player.gameObject.scene.GetPhysicsScene();
        _move = typeof(BlockoutPlayer).GetMethod("MoveOnIsland", Private);
        _contains = typeof(BlockoutPlayer).GetMethod("CanStandOnIsland", Private);
        var session = _player.Session;
        var originalPosition = _player.transform.position;
        var originalLook = _player.View.transform.position + _player.View.transform.forward * 10;
        bool originalEnabled = _player.enabled, originalInput = _player.InputEnabled;
        bool originalSessionEnabled = session.enabled;
        var originalCursor = Cursor.lockState;
        bool originalVisible = Cursor.visible;
        int originalRespawns = _player.Respawns;
        var originalBackground = InputSystem.settings.backgroundBehavior;
        var originalEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        var originalKeyboard = Keyboard.current;
        var originalMouse = Mouse.current;
        Keyboard keyboard = null;
        Mouse mouse = null;
        BlockoutRouteProbe routeProbe = null;
        Application.logMessageReceived += OnLog;
        try
        {
            _player.enabled = false;
            session.enabled = false;
            var boards = _player.gameObject.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BoxCollider>())
                .Where(c => c.name.StartsWith("Pier · weathered board ")).ToArray();
            var pier = boards[0].transform.parent;
            Check("Boundary initialized from scene", ((Vector2[])typeof(BlockoutPlayer).GetField("_islandOutline", Private).GetValue(_player)).Length == 48 && boards.Length == 36);
            Check("Spawn and observation remain reachable", Contains(_player.Spawn.position) && Contains(session.Observation.position));
            var markers = _player.gameObject.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>()).Where(t => t.name.StartsWith("MARK_Route_")).OrderBy(t => t.name).ToArray();
            Check("All staircase route waypoints inside boundary", markers.Length == 37 && markers.All(t => Contains(t.position)), new { waypoints = markers.Length });
            Check("Open sea rejected at ground and jump heights", Enumerable.Range(0, 72).All(i =>
            {
                float angle = i * Mathf.PI / 36;
                return !Contains(new Vector3(Mathf.Cos(angle) * 35, 4.4f, Mathf.Sin(angle) * 35)) &&
                    !Contains(new Vector3(Mathf.Cos(angle) * 35, 100, Mathf.Sin(angle) * 35));
            }));

            for (int i = 0; i < 72; i++)
            {
                float angle = i * Mathf.PI / 36;
                var direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Place(direction * 9 + Vector3.up * 7, direction);
                // Above all terrace rails: a single large displacement must still stop.
                Move(direction * 30);
                bool supported = Supported(_player.transform.position);
                for (int frame = 0; frame < 40; frame++) Move(direction * .082f);
                Settle();
                var end = _player.transform.position;
                bool passed = supported && Supported(end) && end.y > 4 && Contains(end);
                Check("Airborne shoreline sweep " + (i * 5) + " degrees", passed, end.ToString("F3"));
            }

            var forward = pier.forward;
            Place(pier.TransformPoint(new Vector3(0, .2f, 0)), forward);
            for (int frame = 0; frame < 180; frame++) Move(forward * .082f + Vector3.down * .04f);
            var pierEnd = pier.InverseTransformPoint(_player.transform.position);
            Check("Island-to-pier join and full deck reachable", pierEnd.z > 9.4f && pierEnd.z < 10 && _player.transform.position.y > 4, pierEnd.ToString("F3"));
            Move(forward * 30); Settle();
            Check("Pier end stops a large step into the sea", Supported(_player.transform.position) && Contains(_player.transform.position) && pier.InverseTransformPoint(_player.transform.position).z < 10);
            for (int side = -1; side <= 1; side += 2)
            foreach (float distance in new[] { 2f, 5f, 9f })
            {
                Place(pier.TransformPoint(new Vector3(0, 3, distance)), pier.right * side);
                Move(pier.right * side * 20); Settle();
                var end = _player.transform.position;
                Check("Pier side " + side + " at " + distance, Supported(end) && Contains(end) && end.y > 4, pier.InverseTransformPoint(end).ToString("F3"));
            }
            Place(pier.TransformPoint(new Vector3(0, 3, 7)), -forward);
            for (int frame = 0; frame < 200; frame++) Move(-forward * .082f + Vector3.down * .04f);
            Check("Return from pier onto island remains open", new Vector2(_player.transform.position.x, _player.transform.position.z).magnitude < 10 && _player.transform.position.y > 4);

            if (nativeInput)
            {
            // Exercise the actual Update path with the Input System, including sprint,
            // diagonal movement, jump and hidden UI. Keep saves/session scoring untouched.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>("Island boundary verification");
            mouse = InputSystem.AddDevice<Mouse>("Island boundary verification");
            _player.InputEnabled = true; _player.enabled = true; _player.LockCursor(true);
            Place(pier.TransformPoint(new Vector3(0, .2f, 8.8f)), forward);
            await NextFrame(); await NextFrame();
            float started = Time.time, maxHeight = _player.transform.position.y;
            var trail = new List<string>();
            bool dry = true, jumped = false, toggled = false;
            while (Time.time - started < 3)
            {
                var keys = new List<Key> { Key.W, Key.D, Key.LeftShift };
                if (!jumped && _motor.isGrounded) { keys.Add(Key.Space); jumped = true; }
                if (!toggled) { keys.Add(Key.Tab); toggled = true; }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys.ToArray()));
                await NextFrame();
                maxHeight = Mathf.Max(maxHeight, _player.transform.position.y);
                dry &= Supported(_player.transform.position) && _player.transform.position.y > 4 && Contains(_player.transform.position);
                trail.Add(_player.transform.position.ToString("F3"));
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await NextFrame();
            Check("Real sprint plus diagonal jump stays dry with hidden UI", dry && jumped && maxHeight > 4.6f && _player.UIHidden, new { maxHeight, frames = trail.Count });
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab)); await NextFrame();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await NextFrame();
            Routes.Add(new { name = "native input pier edge", trail });

            routeProbe = _player.gameObject.AddComponent<BlockoutRouteProbe>();
            float deadline = Time.realtimeSinceStartup + 55;
            while (!routeProbe.Finished && Time.realtimeSinceStartup < deadline) await Task.Delay(100);
            Check("Real door and 30-stair traversal", routeProbe.Finished && routeProbe.Passed,
                new { routeProbe.Waypoint, routeProbe.Result, position = _player.transform.position.ToString("F3") });
            Object.Destroy(routeProbe); routeProbe = null;
            await NextFrame();
            }
            else
            {
                // The preview has its own physics scene and the exact saved colliders.
                // No Play Mode toggle or change to the shared editor's open scene.
                foreach (var door in preview.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BlockoutInteractable>()).Where(s => s.Kind == BlockoutInteractable.Station.Door))
                    foreach (var collider in door.GetComponentsInChildren<Collider>()) collider.enabled = false;
                Place(markers[0].position, Vector3.forward);
                float speed = 0;
                int waypoint = 1;
                for (; waypoint < markers.Length; waypoint++)
                {
                    int frame;
                    for (frame = 0; frame < 600; frame++)
                    {
                        var delta = markers[waypoint].position - _player.transform.position;
                        delta.y = 0;
                        if (delta.magnitude < .17f) break;
                        speed = _motor.isGrounded ? -2 : speed - 18 * .02f;
                        Move(delta.normalized * Mathf.Min(.048f, delta.magnitude) + Vector3.up * speed * .02f);
                    }
                    if (frame == 600) break;
                }
                Check("Saved scene door-open route through all 30 stairs", waypoint == markers.Length && _player.transform.position.y > 9,
                    new { waypoint, position = _player.transform.position.ToString("F3") });
            }
            Check("No water recovery respawns", _player.Respawns == originalRespawns, new { before = originalRespawns, after = _player.Respawns });
            Check("No runtime errors", Errors.Count == 0, Errors.ToArray());
        }
        catch (Exception exception)
        {
            Check("Verification interrupted", false, exception.Message);
        }
        finally
        {
            if (routeProbe) Object.Destroy(routeProbe);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (originalKeyboard != null && originalKeyboard.added) originalKeyboard.MakeCurrent();
            if (originalMouse != null && originalMouse.added) originalMouse.MakeCurrent();
            InputSystem.settings.backgroundBehavior = originalBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInput;
            if (_player)
            {
                _player.ProbeVelocity = Vector3.zero;
                _player.enabled = originalEnabled; _player.InputEnabled = originalInput;
                _player.Teleport(originalPosition, originalLook);
            }
            if (session) session.enabled = originalSessionEnabled;
            Cursor.lockState = originalCursor; Cursor.visible = originalVisible;
            Application.logMessageReceived -= OnLog;
            if (preview.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);
        }
        var report = new { passed = _failures == 0, total = Checks.Count, failures = _failures, mode = nativeInput ? "PlayMode" : "Isolated preview physics", checks = Checks, routes = Routes, errors = Errors };
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Artifacts/IslandBoundary/" + (nativeInput ? "runtime.json" : "physics.json")));
        File.WriteAllText(output, Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
        return Newtonsoft.Json.JsonConvert.SerializeObject(new { report.passed, report.total, report.failures, output });
    }
}
