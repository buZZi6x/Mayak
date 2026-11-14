using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LighthouseGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class SeparateMenuScene
{
    public static object Main()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before separating scenes.");
        var game = SceneManager.GetActiveScene();
        if (game.path != KeeperGameScene.ScenePath || game.isDirty)
            throw new InvalidOperationException("Open the saved gameplay scene before separating it.");
        if (File.Exists(KeeperMenu.ScenePath)) throw new InvalidOperationException("MainMenu already exists; preserve its manual edits.");
        string backup = Path.GetFullPath("../Artifacts/UI/MainMenu/SceneSplit");
        Directory.CreateDirectory(backup);
        File.Copy(KeeperGameScene.ScenePath, Path.Combine(backup, "LighthouseBlockout.before.unity"), false);
        File.Copy("ProjectSettings/EditorBuildSettings.asset", Path.Combine(backup, "EditorBuildSettings.before.asset"), false);

        // Copy the authored scene once so every environment reference stays local.
        File.Copy(KeeperGameScene.ScenePath, KeeperMenu.ScenePath, false);
        AssetDatabase.ImportAsset(KeeperMenu.ScenePath);
        var menuScene = EditorSceneManager.OpenScene(KeeperMenu.ScenePath, OpenSceneMode.Single);
        var session = Object.FindAnyObjectByType<BlockoutSession>();
        var environment = Object.FindAnyObjectByType<CinematicEnvironment>();
        var traffic = Object.FindAnyObjectByType<MarineTraffic>();
        var menu = Object.FindAnyObjectByType<KeeperMenu>();
        var islands = menuScene.GetRootGameObjects().Single(g => g.name == "Distant archipelago");
        var cameraObject = Object.Instantiate(session.Player.View.gameObject);
        cameraObject.name = "Menu Camera";
        cameraObject.transform.SetParent(null);
        foreach (Transform child in cameraObject.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
        var meter = cameraObject.GetComponent<EnvironmentAudioMeter>();
        if (meter) Object.DestroyImmediate(meter);
        var camera = cameraObject.GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.SetPositionAndRotation(new Vector3(5, 8, -11.5f), Quaternion.LookRotation(new Vector3(17, -6.2f, -168.5f)));
        camera.fieldOfView = 65;
        camera.GetComponent<HDAdditionalCameraData>().volumeAnchorOverride = camera.transform;

        var keep = new HashSet<GameObject> { cameraObject, menu.gameObject, environment.gameObject, environment.Sun.gameObject, islands, traffic.gameObject };
        foreach (var component in environment.GetComponents<MonoBehaviour>())
            if (component is KeeperLighting || component is LighthouseBeacon || component is RainLens) Object.DestroyImmediate(component);
        foreach (var name in new[] { "Soft interior bounce", "Radio reading lamp", "Entrance lantern" })
        {
            var child = environment.transform.Find(name);
            if (child) Object.DestroyImmediate(child.gameObject);
        }
        environment.Session = null;
        environment.WetMaterials = Array.Empty<Material>();
        environment.Hour = 17.4f;
        environment.Weather = CoastalWeather.Clear;
        environment.AdvanceTime = environment.AutoWeather = false;
        environment.Ambience.MenuMultiplier = .12f;
        traffic.Session = null;
        foreach (var root in menuScene.GetRootGameObjects()) if (!keep.Contains(root)) Object.DestroyImmediate(root);
        var menuData = new SerializedObject(menu);
        menuData.FindProperty("_camera").objectReferenceValue = camera;
        menuData.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(menuScene);
        EditorSceneManager.SaveScene(menuScene);

        game = EditorSceneManager.OpenScene(KeeperGameScene.ScenePath, OpenSceneMode.Single);
        session = Object.FindAnyObjectByType<BlockoutSession>();
        var oldMenu = Object.FindAnyObjectByType<KeeperMenu>();
        var oldMenuObject = oldMenu.gameObject;
        Object.DestroyImmediate(oldMenu);
        if (oldMenuObject.GetComponents<Component>().Length == 1 && oldMenuObject.transform.childCount == 0) Object.DestroyImmediate(oldMenuObject);
        var gameplay = session.gameObject.AddComponent<KeeperGameScene>();
        var gameData = new SerializedObject(gameplay);
        gameData.FindProperty("_session").objectReferenceValue = session;
        gameData.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(game);
        EditorSceneManager.SaveScene(game);

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(KeeperMenu.ScenePath, true), new EditorBuildSettingsScene(KeeperGameScene.ScenePath, true) }
            .Concat(EditorBuildSettings.scenes.Where(s => s.path != KeeperMenu.ScenePath && s.path != KeeperGameScene.ScenePath)).ToArray();
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(KeeperMenu.ScenePath, OpenSceneMode.Single);
        return new { menu = KeeperMenu.ScenePath, gameplay = KeeperGameScene.ScenePath, backup,
            roots = SceneManager.GetActiveScene().GetRootGameObjects().Select(g => g.name).ToArray() };
    }
}
