using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LighthouseGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class BlockoutBuilder
{
    public const string ScenePath = "Assets/Scenes/LighthouseBlockout.unity";
    const string Models = "Assets/Art/Blockout/";
    static readonly Dictionary<string, Color> Palette = new Dictionary<string, Color>
    {
        {"Stone",new Color(.30f,.36f,.40f)}, {"PaleStone",new Color(.63f,.66f,.63f)}, {"Ivory",new Color(.84f,.79f,.64f)},
        {"Red",new Color(.62f,.17f,.09f)}, {"Roof",new Color(.14f,.23f,.25f)}, {"Iron",new Color(.07f,.12f,.15f)},
        {"Wood",new Color(.37f,.23f,.12f)}, {"Brass",new Color(.85f,.52f,.12f)}, {"Light",new Color(1,.65f,.19f)},
        {"Blue",new Color(.10f,.30f,.57f)}, {"Teal",new Color(.10f,.53f,.53f)}, {"Glass",new Color(.32f,.66f,.74f,.10f)},
        {"White",new Color(.94f,.91f,.78f)}, {"Kraken",new Color(.60f,.25f,.12f)}, {"Eye",new Color(.96f,.76f,.24f)}
    };
    static Dictionary<string, Material> materials;
    static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
    static Material MakeMaterial(string name, string shader)
    {
        string path = "Assets/Art/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
        return m;
    }
    static void PrepareMaterials()
    {
        Directory.CreateDirectory("Assets/Art/Materials");
        materials = new Dictionary<string, Material>();
        foreach (var item in Palette)
        {
            Material m = MakeMaterial(item.Key, "Standard"); m.color = item.Value; m.SetFloat("_Glossiness", .24f);
            if (item.Key == "Brass") { m.SetFloat("_Metallic", .65f); m.SetFloat("_Glossiness", .5f); }
            if (item.Key == "Light" || item.Key == "Eye") { m.SetColor("_EmissionColor", item.Value * 1.3f); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; m.EnableKeyword("_EMISSION"); }
            if (item.Key == "Glass")
            {
                m.SetFloat("_Mode", 2); m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0); m.DisableKeyword("_ALPHATEST_ON"); m.EnableKeyword("_ALPHABLEND_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON"); m.renderQueue = 3000;
            }
            materials[item.Key] = m; EditorUtility.SetDirty(m);
        }
        materials["Water"] = MakeMaterial("Water", "Lighthouse/BlockoutWater");
        materials["Beam"] = MakeMaterial("Beam", "Lighthouse/BlockoutBeam");
        materials["Beam"].color = new Color(1,.69f,.27f,.018f);
    }
    static GameObject Model(string filename, string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CoastalArtBuilder.ModelPath(filename));
        if (!prefab) throw new Exception("Missing Blender export: " + filename);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab); go.name = name;
        foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sharedMaterials = renderer.sharedMaterials.Select(m => CoastalArtBuilder.FindMaterial(m.name) ?? (materials.TryGetValue(m.name, out Material replacement) ? replacement : materials["Ivory"])).ToArray();
            if (renderer.name.StartsWith("COLL_")) renderer.enabled = false;
            if (renderer.sharedMaterials.Contains(materials["Glass"])) { renderer.gameObject.layer = 2; renderer.shadowCastingMode = ShadowCastingMode.Off; }
        }
        return go;
    }
    static BlockoutMotion Motion(Transform root, string name, BlockoutMotion.MotionType type)
    { var motion = Find(root, name).gameObject.AddComponent<BlockoutMotion>(); motion.Type = type; return motion; }
    static void Label(Transform root, string marker, string content, Color color)
    {
        var go = new GameObject(content.Replace('\n', ' ')); go.transform.position = Find(root, marker).position; go.layer = 2;
        var mesh = go.AddComponent<TextMesh>(); mesh.text = content; mesh.fontSize = 64; mesh.characterSize = .025f; mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center; mesh.color = color;
        mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); go.GetComponent<MeshRenderer>().sharedMaterial = mesh.font.material;
        go.AddComponent<BlockoutLabel>();
    }
    [MenuItem("Lighthouse/Rebuild Blender blockout scene")]
    public static string BuildScene()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play Mode before rebuilding the scene.");
        foreach (string file in new[] { "LighthouseEnvironment", "Boat", "Kraken", "Camera" })
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(CoastalArtBuilder.ModelPath(file));
            if (!importer) throw new Exception("Import Blender exports first.");
            if (!importer.isReadable || importer.importAnimation) { importer.isReadable = true; importer.importAnimation = false; importer.SaveAndReimport(); }
        }
        PrepareMaterials();
        CoastalArtBuilder.PrepareMaterials();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var environment = Model("LighthouseEnvironment", "Lighthouse · Blender source"); var root = environment.transform;
        foreach (MeshFilter mesh in environment.GetComponentsInChildren<MeshFilter>())
        {
            bool station = mesh.GetComponentsInParent<Transform>().Any(t => t.name.StartsWith("STATION_"));
            if (!mesh.name.StartsWith("VIS_") && (mesh.name.StartsWith("SOLID_") || mesh.name.StartsWith("COLL_") || mesh.name.StartsWith("GLASS_") || station))
                mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = CoastalArtBuilder.CollisionMesh(mesh.sharedMesh);
        }
        foreach (var item in new[] {
            ("STATION_Door", BlockoutInteractable.Station.Door, "дверь"),
            ("STATION_Lamp", BlockoutInteractable.Station.Lamp, "обслужить лампу · 6 с"),
            ("STATION_Radio", BlockoutInteractable.Station.Radio, "отправить кадры · 4 с"),
            ("STATION_Bed", BlockoutInteractable.Station.Bed, "закончить день · нужно 5 кадров"),
            ("STATION_Telescope", BlockoutInteractable.Station.Telescope, "наблюдение · ПКМ для приближения") })
        { var station = Find(root, item.Item1).gameObject.AddComponent<BlockoutInteractable>(); station.Kind = item.Item2; station.Label = item.Item3; }
        Motion(root, "ANIM_Lens", BlockoutMotion.MotionType.Spin);
        Motion(root, "ANIM_Flame", BlockoutMotion.MotionType.Flame);
        Motion(root, "ANIM_Pump", BlockoutMotion.MotionType.Pump);
        Motion(root, "ANIM_Blanket", BlockoutMotion.MotionType.Blanket);
        Motion(root, "ANIM_ReportPaper", BlockoutMotion.MotionType.Blanket);
        Motion(root, "ANIM_Telescope", BlockoutMotion.MotionType.Telescope);
        Motion(root, "ANIM_Flag", BlockoutMotion.MotionType.Flag);
        foreach (var t in root.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("ANIM_RadioDial"))) t.gameObject.AddComponent<BlockoutMotion>().Type = BlockoutMotion.MotionType.Dial;
        Label(root, "MARK_DoorLabel", "ВХОД\nE — открыть", new Color(1,.89f,.65f));
        Label(root, "MARK_LampLabel", "ЛАМПА\nУХОД ЗА СВЕТОМ", new Color(1,.78f,.36f));
        Label(root, "MARK_RadioLabel", "РАДИО\nОТПРАВИТЬ КАДРЫ", new Color(.35f,1,.94f));
        Label(root, "MARK_BedLabel", "КРОВАТЬ\nСЛЕДУЮЩИЙ ДЕНЬ", new Color(.5f,.74f,1));
        Label(root, "MARK_TelescopeLabel", "НАБЛЮДЕНИЕ\n5 КАДРОВ НА ДЕНЬ", new Color(1,.92f,.69f));
        var sun = new GameObject("Dusk sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.color = new Color(1,.82f,.63f); sun.intensity = .85f; sun.transform.rotation = Quaternion.Euler(32,-30,0); sun.shadows = LightShadows.Soft;
        RenderSettings.ambientMode = AmbientMode.Trilight; RenderSettings.ambientSkyColor = new Color(.34f,.45f,.55f); RenderSettings.ambientEquatorColor = new Color(.26f,.31f,.35f); RenderSettings.ambientGroundColor = new Color(.18f,.16f,.14f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogColor = new Color(.36f,.49f,.55f); RenderSettings.fogDensity = .0018f;
        var sky = MakeMaterial("EveningSky", "Skybox/Procedural"); sky.SetColor("_SkyTint", new Color(.36f,.50f,.61f)); sky.SetFloat("_Exposure", 1.1f); sky.SetFloat("_AtmosphereThickness", .7f); RenderSettings.skybox = sky; RenderSettings.sun = sun;
        QualitySettings.shadowDistance = 120; QualitySettings.antiAliasing = 4; QualitySettings.vSyncCount = 1;
        var bulb = new GameObject("Lamp warm light").AddComponent<Light>(); bulb.transform.position = Find(root,"STATION_Lamp").position + Vector3.up * 1.7f; bulb.type = LightType.Point; bulb.range = 17; bulb.intensity = 2.3f; bulb.color = new Color(1,.64f,.24f); bulb.shadows = LightShadows.Soft;
        bulb.gameObject.AddComponent<BlockoutMotion>().Type = BlockoutMotion.MotionType.Flame;
        var beam = new GameObject("Rotating lighthouse beam").AddComponent<Light>(); beam.type = LightType.Spot; beam.range = 190; beam.spotAngle = 23; beam.intensity = 8; beam.color = bulb.color;
        beam.transform.SetParent(Find(root,"ANIM_Lens"), false); beam.transform.rotation = Quaternion.LookRotation(Vector3.forward + Vector3.down * .06f);
        var fill = new GameObject("Entry light").AddComponent<Light>(); fill.type = LightType.Point; fill.transform.position = Find(root,"MARK_PlayerLook").position + Vector3.up; fill.intensity = 2; fill.range = 9; fill.color = new Color(1,.74f,.43f);
        var session = new GameObject("Night watch · systems").AddComponent<BlockoutSession>();
        var playerObject = new GameObject("Keeper · first person"); var motor = playerObject.AddComponent<CharacterController>();
        motor.height = 1.8f; motor.radius = .27f; motor.center = new Vector3(0,.9f,0); motor.stepOffset = .34f; motor.skinWidth = .025f; motor.slopeLimit = 50;
        var player = playerObject.AddComponent<BlockoutPlayer>(); player.Session = session; session.Player = player;
        var camera = new GameObject("Keeper Camera").AddComponent<Camera>(); camera.transform.SetParent(playerObject.transform, false); camera.transform.localPosition = new Vector3(0,1.65f,0); camera.tag = "MainCamera"; camera.nearClipPlane = .04f; camera.farClipPlane = 700; camera.fieldOfView = 72; camera.gameObject.AddComponent<AudioListener>(); player.View = camera;
        var held = new GameObject("Handheld camera rig").transform; held.SetParent(camera.transform, false); held.localPosition = new Vector3(.30f,-.28f,.60f); held.localScale = Vector3.one * .55f; player.HeldCamera = held;
        var cameraModel = Model("Camera", "Camera · Blender"); cameraModel.transform.SetParent(held, false);
        foreach (var renderer in cameraModel.GetComponentsInChildren<Renderer>()) { renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.gameObject.layer = 2; }
        player.Spawn = Find(root,"MARK_PlayerSpawn"); player.LookAtSpawn = Find(root,"MARK_PlayerLook"); player.Teleport(player.Spawn.position, player.LookAtSpawn.position);
        session.Observation = Find(root,"MARK_Observation"); session.Observation.position += Vector3.right * 1.15f; session.SeaEvent = Find(root,"MARK_SeaEvent");
        var director = session.gameObject.AddComponent<BlockoutDirector>(); session.Director = director; director.Session = session; director.SeaAnchor = session.SeaEvent; director.Sun = sun; director.LampLight = bulb; director.Water = materials["Water"];
        director.Sky = sky; director.BeamLight = beam; director.BeamMesh = Find(root,"VIS_LightBeam").GetComponent<Renderer>(); director.FlameMesh = Find(root,"ANIM_Flame").GetComponent<Renderer>(); director.BeamMesh.shadowCastingMode = ShadowCastingMode.Off; director.BeamMesh.gameObject.layer = 2;
        director.Boat = new GameObject("Passing ship · animation root").transform; var boat = Model("Boat","Boat · Blender"); boat.transform.SetParent(director.Boat, false); Motion(boat.transform,"ANIM_BoatFlag",BlockoutMotion.MotionType.Flag);
        director.Kraken = new GameObject("Kraken · animation root").transform; var kraken = Model("Kraken","Kraken · Blender"); kraken.transform.SetParent(director.Kraken, false);
        director.Shoal = new GameObject("Luminous shoal · blockout markers").transform;
        for (int i = 0; i < 7; i++) { var fish = GameObject.CreatePrimitive(PrimitiveType.Capsule); fish.name = "Luminous fish " + (i+1); UnityEngine.Object.DestroyImmediate(fish.GetComponent<Collider>()); fish.transform.SetParent(director.Shoal, false); fish.transform.localScale = new Vector3(.4f,.9f,.4f); fish.GetComponent<Renderer>().sharedMaterial = materials["Light"]; }
        director.Boat.position = session.SeaEvent.position; director.Kraken.position = session.SeaEvent.position + Vector3.down*12;
        session.gameObject.AddComponent<BlockoutHUD>().Session = session;
        PlayerSettings.companyName = "Jem 11"; PlayerSettings.productName = "Не дай погаснуть — блокинг"; PlayerSettings.defaultScreenWidth = 1440; PlayerSettings.defaultScreenHeight = 810; PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.resizableWindow = true;
        Directory.CreateDirectory("Assets/Scenes"); EditorSceneManager.SaveScene(scene,ScenePath); EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath,true) }; AssetDatabase.SaveAssets();
        EnvironmentBuilder.UpgradeScene();
        if(File.Exists("Assets/Art/Fleet/Brigantine.fbx"))ObservationBuilder.Install();
        if(File.Exists("Assets/Art/Encounters/KrakenMantle.fbx"))EncounterBuilder.Install();
        if (SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.LookAt(new Vector3(0,7,0),Quaternion.Euler(22,-35,0),34);
        return JsonUtility.ToJson(new SceneEvidence { Scene = ScenePath, Spawn = player.Spawn.position, Observation = session.Observation.position, Sea = session.SeaEvent.position, Meshes = environment.GetComponentsInChildren<MeshFilter>().Length, Colliders = environment.GetComponentsInChildren<Collider>().Length, Stations = environment.GetComponentsInChildren<BlockoutInteractable>().Length },true);
    }
    [Serializable] class SceneEvidence { public string Scene; public Vector3 Spawn, Observation, Sea; public int Meshes, Colliders, Stations; }
    [MenuItem("Lighthouse/Build Windows blockout")]
    public static string BuildWindows()
    {
        Directory.CreateDirectory("Builds/Windows");
        var scenes = File.Exists(KeeperMenu.ScenePath) ? new[] {KeeperMenu.ScenePath, ScenePath} : new[] {ScenePath};
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes, locationPathName = "Builds/Windows/Lighthouse.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Build failed: " + report.summary.result + ", errors=" + report.summary.totalErrors);
        File.Copy(Path.GetFullPath(Path.Combine(Application.dataPath, "../../THIRD_PARTY_NOTICES.md")), "Builds/Windows/THIRD_PARTY_NOTICES.md", true);
        return "Build succeeded: Builds/Windows/Lighthouse.exe; bytes=" + report.summary.totalSize;
    }
}
