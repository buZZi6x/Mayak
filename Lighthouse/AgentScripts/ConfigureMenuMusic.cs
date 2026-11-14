using System;
using System.Linq;
using LighthouseGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ConfigureMenuMusic
{
    public static object Main()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before configuring menu music.");
        const string path = "Assets/Audio/Menu/DecksideJig.mp3";
        AssetDatabase.ImportAsset(path);
        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
        var settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.Streaming;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = .85f;
        settings.preloadAudioData = false;
        importer.defaultSampleSettings = settings;
        importer.forceToMono = false;
        importer.SaveAndReimport();
        var original = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(KeeperMenu.ScenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(KeeperMenu.ScenePath, OpenSceneMode.Additive);
        try
        {
            var menu = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<KeeperMenu>()).Single();
            var source = scene.GetRootGameObjects().Select(g => g.GetComponent<AudioSource>()).FirstOrDefault(a => a && a.gameObject.name == "Menu Music · Deckside Jig");
            if (!source)
            {
                var music = new GameObject("Menu Music · Deckside Jig");
                SceneManager.MoveGameObjectToScene(music, scene);
                source = music.AddComponent<AudioSource>();
            }
            source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0;
            source.dopplerLevel = 0;
            source.volume = .35f;
            source.priority = 180;
            var serialized = new SerializedObject(menu);
            serialized.FindProperty("_music").objectReferenceValue = source;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return new { scene = scene.path, clip = source.clip.name, seconds = source.clip.length, channels = source.clip.channels, loop = source.loop, streaming = source.clip.loadType.ToString() };
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            if (original.isLoaded) SceneManager.SetActiveScene(original);
        }
    }
}
