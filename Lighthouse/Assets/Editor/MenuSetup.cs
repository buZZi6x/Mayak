using System;
using System.IO;
using LighthouseGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;

public static class MenuSetup
{
    public static string Apply()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Menu wiring requires Edit Mode.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != KeeperMenu.ScenePath) throw new InvalidOperationException("Open the MainMenu scene before wiring its interface.");
        foreach (var name in new[] { "Commissioner", "CommissionerSemibold", "CormorantGaramond" }) PrepareFont(name);
        var camera = Camera.main;
        if (!camera) throw new InvalidOperationException("The menu scene must contain its own MainCamera.");
        var menu = UnityEngine.Object.FindAnyObjectByType<KeeperMenu>();
        if (!menu) menu = new GameObject("Main Menu · Маяк").AddComponent<KeeperMenu>();
        var serialized = new SerializedObject(menu);
        serialized.FindProperty("_camera").objectReferenceValue = camera;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        var logo = AssetDatabase.LoadMainAssetAtPath("Assets/Resources/KeeperMenu/mayak-wordmark.svg");
        return "Menu wired; fonts prepared; logo type=" + (logo ? logo.GetType().FullName : "missing");
    }

    private static void PrepareFont(string name)
    {
        string root = "Assets/Resources/KeeperMenu/Fonts/", path = root + name + "SDF.asset";
        var asset = AssetDatabase.LoadAssetAtPath<FontAsset>(path);
        if (asset) return;
        var source = AssetDatabase.LoadAssetAtPath<Font>(root + name + ".ttf");
        if (!source) throw new InvalidOperationException("Font not imported: " + name);
        asset = FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        asset.name = name + "SDF";
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.AddObjectToAsset(asset.material, asset);
        string characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя %.,:;!?+-—/()[]×←→⌄";
        asset.TryAddCharacters(characters, out string missing);
        foreach (var texture in asset.atlasTextures) if (texture && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(texture))) AssetDatabase.AddObjectToAsset(texture, asset);
        EditorUtility.SetDirty(asset);
        if (!string.IsNullOrEmpty(missing)) Debug.LogWarning(name + " optional glyphs unavailable: " + missing);
    }
}
