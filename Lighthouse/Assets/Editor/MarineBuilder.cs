using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using LighthouseGame;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
public static class MarineBuilder
{
    const string Root="Assets/Art/Marine";
    static Material Mat(string name,Color color,string shader="HDRP/Lit")
    {
        string path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,path);}m.SetColor(shader=="HDRP/Lit"?"_BaseColor":"_Color",color);
        if(shader=="HDRP/Lit")
        {
            bool metal=name.Contains("Iron")||name.Contains("Brass");m.SetFloat("_Metallic",metal?.72f:0);m.SetFloat("_Smoothness",metal?.56f:name.Contains("Skin")?.62f:.34f);
            if(name.Contains("Sail")||name.Contains("Skin")){m.SetFloat("_DoubleSidedEnable",1);m.SetFloat("_CullMode",0);m.SetFloat("_CullModeForward",0);}
            if(name.Contains("Hull")||name.Contains("Deck")||name.Contains("Fracture")||name.Contains("Sail"))
            {
                var source=CoastalArtBuilder.FindMaterial(name.Contains("Sail")?"Coast_Linen":"Coast_Wood");
                if(source){m.SetTexture("_BaseColorMap",source.GetTexture("_BaseColorMap"));m.SetTexture("_NormalMap",source.GetTexture("_NormalMap"));m.SetFloat("_NormalScale",.5f);}
            }
            if(name.Contains("Light")||name.Contains("Glass")){m.SetColor("_EmissiveColor",new Color(1,.44f,.12f)*(name.Contains("Light")?2.4f:.7f));}
            HDMaterial.ValidateMaterial(m);
        }
        EditorUtility.SetDirty(m);return m;
    }
    static Transform Owner(Transform t,Transform model)
    {while(t!=model){if(t.name.StartsWith("BREAK_")||t.name.StartsWith("ANIM_")||t.name.StartsWith("STATE_"))return t;t=t.parent;}return model;}
    static void CombinePieces(Transform model,string prefix)
    {
        var renderers=model.GetComponentsInChildren<MeshRenderer>(true);
        foreach(var group in renderers.GroupBy(r=>Owner(r.transform,model)))
        {
            int index=0;
            foreach(var materials in group.GroupBy(r=>r.sharedMaterial))
            {
                var render=materials.ToArray();var target=group.Key;
                var ci=render.Select(r=>new CombineInstance{mesh=r.GetComponent<MeshFilter>().sharedMesh,transform=target.worldToLocalMatrix*r.transform.localToWorldMatrix}).ToArray();
                var mesh=new Mesh{name=prefix+"_"+target.name+"_"+index,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(ci,true,true);mesh.RecalculateBounds();
                string path=Root+"/Meshes/"+mesh.name+".asset";var prior=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(prior){EditorUtility.CopySerialized(mesh,prior);UnityEngine.Object.DestroyImmediate(mesh);mesh=prior;}else AssetDatabase.CreateAsset(mesh,path);
                var go=new GameObject("Surface · "+materials.Key.name);go.layer=2;go.transform.SetParent(target,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=materials.Key;
                foreach(var r in render){UnityEngine.Object.DestroyImmediate(r.GetComponent<MeshFilter>());UnityEngine.Object.DestroyImmediate(r);}index++;
            }
        }
        foreach(var t in model.GetComponentsInChildren<Transform>(true).Reverse().ToArray())
            if(t!=model&&t.childCount==0&&!t.name.StartsWith("BREAK_")&&!t.name.StartsWith("ANIM_")&&!t.name.StartsWith("MARK_")&&!t.GetComponent<Renderer>())UnityEngine.Object.DestroyImmediate(t.gameObject);
    }
    static GameObject Model(string name,Transform parent,Dictionary<string,Material> materials)
    {
        string path=Root+"/"+name+".fbx";var imp=(ModelImporter)AssetImporter.GetAtPath(path);imp.isReadable=true;imp.importAnimation=false;imp.SaveAndReimport();
        var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),parent,false);go.name=name+" · Blender V02";
        foreach(var r in go.GetComponentsInChildren<MeshRenderer>()){r.sharedMaterials=r.sharedMaterials.Select(m=>materials.TryGetValue(m.name,out var v)?v:materials["Marine_Deck"]).ToArray();r.gameObject.layer=2;}
        CombinePieces(go.transform,name);
        foreach(var t in go.GetComponentsInChildren<Transform>(true))if(t.name.StartsWith("STATE_Broken"))t.gameObject.SetActive(false);
        return go;
    }
    [MenuItem("Lighthouse/Install marine V02")]
    public static string Install()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play Mode first.");
        Directory.CreateDirectory(Root+"/Materials");Directory.CreateDirectory(Root+"/Meshes");AssetDatabase.Refresh();
        var s=UnityEngine.Object.FindAnyObjectByType<BlockoutSession>();var d=s.Director;var env=UnityEngine.Object.FindAnyObjectByType<CinematicEnvironment>();
        var palette=new Dictionary<string,Color>{["HullNavy"]=new Color(.12f,.21f,.25f),["HullRed"]=new Color(.38f,.12f,.065f),["HullTeal"]=new Color(.13f,.28f,.24f),["Deck"]=new Color(.73f,.56f,.35f),["Sail"]=new Color(.88f,.82f,.66f),["Brass"]=new Color(.43f,.29f,.1f),["Iron"]=new Color(.025f,.03f,.032f),["Ivory"]=new Color(.63f,.57f,.43f),["Glass"]=new Color(.045f,.11f,.13f),["Light"]=new Color(1,.56f,.17f),["Fracture"]=new Color(.87f,.69f,.38f),["DeepSkin"]=new Color(.30f,.43f,.45f),["Ridge"]=new Color(.055f,.09f,.11f),["Flesh"]=new Color(.24f,.14f,.12f),["Eye"]=new Color(.67f,.31f,.037f)};
        var mats=palette.ToDictionary(p=>"Marine_"+p.Key,p=>Mat("Marine_"+p.Key,p.Value));
        mats["Marine_DeepSkin"].SetTexture("_BaseColorMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/AbyssDermis.png"));HDMaterial.ValidateMaterial(mats["Marine_DeepSkin"]);
        var oldTraffic=GameObject.Find("Marine traffic · four independent routes");if(oldTraffic)UnityEngine.Object.DestroyImmediate(oldTraffic);
        var fleet=d.Boat.GetComponent<CoastalFleet>();
        foreach(var vessel in fleet.Vessels.Skip(1).Where(v=>v).ToArray())UnityEngine.Object.DestroyImmediate(vessel.gameObject);
        var cutter=fleet.Vessels[0];fleet.Vessels=new Transform[4];fleet.Vessels[0]=cutter;
        string[] names={"MeridianFrigate","PilgrimBarque","NorthernSteamer"};
        for(int i=0;i<3;i++)
        {
            var ship=Model(names[i],d.Boat,mats);fleet.Vessels[i+1]=ship.transform;
            var destruction=ship.AddComponent<ShipDestruction>();destruction.Vessel=d.Boat;
        }
        fleet.Select(1);d.Boat.gameObject.SetActive(true);
        var wake=d.Boat.GetComponentInChildren<WaterDecal>(true);if(wake){wake.regionSize=new Vector2(28,100);wake.transform.localPosition=new Vector3(0,0,30);}
        var root=new GameObject("Marine traffic · four independent routes");var traffic=root.AddComponent<MarineTraffic>();traffic.Session=s;traffic.Ships=new CoastalFleet[4];d.Traffic=traffic;
        for(int i=0;i<4;i++)
        {
            var ship=UnityEngine.Object.Instantiate(d.Boat.gameObject,root.transform);ship.name="Merchant route "+(i+1);ship.SetActive(true);traffic.Ships[i]=ship.GetComponent<CoastalFleet>();traffic.Ships[i].Select(1+i%3);
            foreach(var destruction in ship.GetComponentsInChildren<ShipDestruction>(true))UnityEngine.Object.DestroyImmediate(destruction);
            var outward=new Vector3(s.SeaEvent.position.x,0,s.SeaEvent.position.z).normalized;ship.transform.position=Quaternion.Euler(0,-83+i*53,0)*outward*(140+i*47);
        }
        var encounter=d.Encounter;encounter.OctopusBody=d.Kraken.GetChild(0);encounter.OctopusBody.name="Big octopus · preserved original";
        var prior=d.Kraken.Find("AbyssalKraken · Blender V02");if(prior)UnityEngine.Object.DestroyImmediate(prior.gameObject);
        encounter.ColossalBody=Model("AbyssalKraken",d.Kraken,mats).transform;encounter.ColossalSkin=mats["Marine_DeepSkin"];encounter.ColossalSuckers=mats["Marine_Flesh"];
        encounter.Colossal=true;encounter.ManualTimeline=false;encounter.OctopusBody.gameObject.SetActive(false);
        var presentation=s.GetComponent<EncounterPresentation>();if(!presentation)presentation=s.gameObject.AddComponent<EncounterPresentation>();encounter.Presentation=presentation;
        string[] audio={"TimberStrain","MastBreakA","MastBreakB","HullRupture","TentacleImpactA","TentacleImpactB","HullSubmerging","SeaDisplacement"};presentation.Clips=audio.Select(n=>AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"/Audio/"+n+".wav")).ToArray();
        foreach(var clip in presentation.Clips){var importer=(AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.85f;importer.defaultSampleSettings=settings;importer.forceToMono=true;importer.SaveAndReimport();}
        presentation.SprayMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Environment/Materials/Salt spray.mat");
        RebuildGhost(s,fleet);
        s.Observations.WaterMaterial=Mat("Marine surface",Color.white,"Lighthouse/HDRP Marine Surface");
        InstallBeacon(d,env);InstallStars(env);
        var rain=env.GetComponent<RainLens>();if(!rain)rain=env.gameObject.AddComponent<RainLens>();rain.Environment=env;rain.Template=Mat("Rain lenses",Color.white,"Lighthouse/HDRP Rain Lens");
        d.FreezePreview=false;
        EditorUtility.SetDirty(s);EditorUtility.SetDirty(env);EditorSceneManager.MarkSceneDirty(s.gameObject.scene);EditorSceneManager.SaveScene(s.gameObject.scene);AssetDatabase.SaveAssets();
        return "Marine V02 installed: four traffic vessels, three breakable ocean ships, two creatures, spatial destruction audio, beacon and lens rain.";
    }
    static void RebuildGhost(BlockoutSession s,CoastalFleet fleet)
    {
        var ghost=s.Observations.Ghost;if(!ghost)return;
        foreach(Transform child in ghost.Vessel.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
        ghost.Vessel.localScale=Vector3.one;
        var ship=UnityEngine.Object.Instantiate(fleet.Vessels[2].gameObject,ghost.Vessel,false);ship.SetActive(true);
        foreach(var d in ship.GetComponentsInChildren<ShipDestruction>(true))UnityEngine.Object.DestroyImmediate(d);
        var hull=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Encounters/Materials/Spectral hull.mat");var sail=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Encounters/Materials/Spectral sails.mat");
        ghost.Surfaces=ship.GetComponentsInChildren<Renderer>(true);foreach(var r in ghost.Surfaces){r.sharedMaterials=r.sharedMaterials.Select(m=>m.name.Contains("Sail")?sail:hull).ToArray();r.shadowCastingMode=ShadowCastingMode.Off;}
        var animation=ship.AddComponent<CoastalFleet>();animation.Vessels=new[]{ship.transform};animation.Selected=0;
    }
    static void InstallStars(CinematicEnvironment env)
    {
        const int size=512;string path=Root+"/Quiet night stars.asset";var cube=AssetDatabase.LoadAssetAtPath<Cubemap>(path);if(!cube){cube=new Cubemap(size,TextureFormat.RGBAHalf,true);AssetDatabase.CreateAsset(cube,path);}
        var random=new System.Random(7114);
        for(int face=0;face<6;face++)
        {
            var pixels=new Color[size*size];
            for(int star=0;star<115;star++)
            {
                int x=random.Next(2,size-2),y=random.Next(2,size-2);float power=.25f+(float)Math.Pow(random.NextDouble(),7)*6;
                for(int j=-1;j<=1;j++)for(int i=-1;i<=1;i++)pixels[(y+j)*size+x+i]+=new Color(.75f,.84f,1)*power*Mathf.Exp(-(i*i+j*j)*4);
            }
            cube.SetPixels(pixels,(CubemapFace)face);
        }
        cube.Apply(true,false);EditorUtility.SetDirty(cube);env.Atmosphere.sharedProfile.TryGet<PhysicallyBasedSky>(out var sky);sky.spaceEmissionTexture.Override(cube);sky.spaceRotation.Override(new Vector3(18,env.Hour*15,14));EditorUtility.SetDirty(env.Atmosphere.sharedProfile);
    }
    static void InstallBeacon(BlockoutDirector d,CinematicEnvironment env)
    {
        var beacon=env.GetComponent<LighthouseBeacon>();if(!beacon)beacon=env.gameObject.AddComponent<LighthouseBeacon>();beacon.Director=d;
        var old=d.BeamLight.transform.Find("Beacon volumetric shaft");if(old)beacon.Shaft=old.GetComponent<Renderer>();
        if(!beacon.BroadBeam)
        {
            var broad=new GameObject("Beacon soft spill").AddComponent<Light>();broad.transform.SetParent(d.BeamLight.transform,false);beacon.BroadBeam=broad;
        }
        beacon.ConfigureOptics();
        d.BeamLight.intensity=650000;beacon.BroadBeam.intensity=195000;
        d.BeamLight.GetComponent<HDAdditionalLightData>().volumetricDimmer=.22f;
        beacon.BroadBeam.GetComponent<HDAdditionalLightData>().volumetricDimmer=.12f;
        if(env.Atmosphere.sharedProfile.TryGet<Fog>(out var fog)){fog.depthExtent.Override(800);EditorUtility.SetDirty(env.Atmosphere.sharedProfile);}
    }

    [MenuItem("Lighthouse/Update beacon optics only")]
    public static string UpdateBeaconOptics()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before updating beacon optics.");
        var env=UnityEngine.Object.FindAnyObjectByType<CinematicEnvironment>();
        if(!env)throw new InvalidOperationException("Open LighthouseBlockout first.");
        InstallBeacon(env.Session.Director,env);
        EditorSceneManager.MarkSceneDirty(env.gameObject.scene);EditorSceneManager.SaveScene(env.gameObject.scene);AssetDatabase.SaveAssets();
        return "Beacon: 90-second revolution, 52-degree main light, 88-degree spill, native HDRP scattering.";
    }
}
