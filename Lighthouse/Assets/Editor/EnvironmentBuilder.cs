using System;
using System.IO;
using System.Linq;
using System.Reflection;
using LighthouseGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public static class EnvironmentBuilder
{
    const string Root="Assets/Environment";
    static T Asset<T>(string name) where T:ScriptableObject
    {
        string path=Root+"/"+name+".asset";var asset=AssetDatabase.LoadAssetAtPath<T>(path);
        if(!asset){asset=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(asset,path);}return asset;
    }
    static Material Material(string name,string shader)
    {
        string path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,path);}return m;
    }
    static T Component<T>(GameObject go) where T:Component {return go.GetComponent<T>()??go.AddComponent<T>();}
    static T VolumePart<T>(VolumeProfile p) where T:VolumeComponent {if(!p.TryGet(out T c)) c=p.Add<T>(true);return c;}
    public static void ConfigurePipeline()
    {
        Directory.CreateDirectory(Root);Directory.CreateDirectory(Root+"/Materials");Directory.CreateDirectory(Root+"/Audio");
        var asset=Asset<HDRenderPipelineAsset>("Lighthouse HDRP");var settings=asset.currentPlatformRenderPipelineSettings;
        settings.supportWater=true;settings.waterSimulationResolution=WaterSimulationResolution.High256;settings.supportWaterDecals=true;
        settings.supportVolumetricClouds=true;settings.supportVolumetrics=true;settings.supportSSR=true;settings.supportSSRTransparent=true;settings.supportSSAO=true;settings.supportSSGI=true;
        settings.supportRayTracing=false;settings.supportTransparentDepthPrepass=true;settings.supportTransparentDepthPostpass=true;
        asset.currentPlatformRenderPipelineSettings=settings;EditorUtility.SetDirty(asset);
        PlayerSettings.colorSpace=ColorSpace.Linear;GraphicsSettings.defaultRenderPipeline=asset;
        int old=QualitySettings.GetQualityLevel();for(int i=0;i<QualitySettings.names.Length;i++){QualitySettings.SetQualityLevel(i,false);QualitySettings.renderPipeline=asset;}QualitySettings.SetQualityLevel(old,false);
        QualitySettings.antiAliasing=0;QualitySettings.vSyncCount=1;
        var type=typeof(HDRenderPipelineAsset).Assembly.GetType("UnityEngine.Rendering.HighDefinition.HDRenderPipelineGlobalSettings");
        type.GetMethod("Ensure",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{true});
    }
    static void ConvertMaterials()
    {
        foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/Art/Materials"}))
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if(m.name=="Water"||m.name=="EveningSky"||m.name=="Beam")continue;
            Color color=m.color;float metal=m.HasProperty("_Metallic")?m.GetFloat("_Metallic"):0;
            float smooth=m.HasProperty("_Smoothness")?m.GetFloat("_Smoothness"):m.GetFloat("_Glossiness");
            m.shader=Shader.Find("HDRP/Lit");m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);
            if(m.name=="Stone"||m.name=="PaleStone")m.SetFloat("_Smoothness",.17f);
            if(m.name=="Glass")
            {m.SetColor("_BaseColor",new Color(.86f,.94f,1,.025f));m.SetFloat("_SurfaceType",1);m.SetFloat("_BlendMode",0);m.SetFloat("_EnableFogOnTransparent",1);m.SetFloat("_TransparentZWrite",0);m.SetFloat("_Smoothness",.91f);m.SetFloat("_AlphaCutoffEnable",0);m.SetFloat("_DoubleSidedEnable",1);m.renderQueue=3000;}
            if(m.name=="Light"||m.name=="Eye")
            {m.SetColor("_EmissiveColor",color*(m.name=="Light"?1800:45));m.SetColor("_EmissiveColorLDR",color);m.SetFloat("_EmissiveExposureWeight",1);}
            HDMaterial.ValidateMaterial(m);EditorUtility.SetDirty(m);
        }
    }
    static CoastalWeatherPreset Preset(CoastalWeather kind,string title,float wind,float density,float shape,float erosion,float visibility,float rain,float wet,float foam,float sun,float swell,float altitude,float depth)
    {
        var p=Asset<CoastalWeatherPreset>("Weather "+kind);p.Kind=kind;p.DisplayName=title;p.WindKph=wind;p.CloudDensity=density;p.CloudShape=shape;p.CloudErosion=erosion;
        p.Visibility=visibility;p.Rain=rain;p.Wetness=wet;p.Foam=foam;p.SunDimmer=sun;p.Swell=swell;p.CloudAltitude=altitude;p.CloudDepth=depth;
        p.FogTint=new Color(.75f,.85f,.92f);EditorUtility.SetDirty(p);return p;
    }
    static Cubemap Stars()
    {
        string path=Root+"/Night sky stars.asset";var cube=AssetDatabase.LoadAssetAtPath<Cubemap>(path);
        const int size=512;if(cube&&cube.width==size)return cube;
        var generated=new Cubemap(size,TextureFormat.RGBAHalf,true);generated.name="Night sky stars";var rng=new System.Random(7319);
        for(int face=0;face<6;face++)
        {
            var pixels=new Color[size*size];for(int j=0;j<pixels.Length;j++)pixels[j]=new Color(.0008f,.0012f,.002f,1);
            for(int star=0;star<650;star++)
            {
                int x=rng.Next(3,size-3),y=rng.Next(3,size-3);float power=(float)Math.Pow(rng.NextDouble(),5)*8+.4f;
                var c=Color.Lerp(new Color(.62f,.77f,1),new Color(1,.84f,.65f),(float)rng.NextDouble());
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)pixels[(y+dy)*size+x+dx]+=c*(power*Mathf.Exp(-(dx*dx+dy*dy)*3.5f));
            }
            generated.SetPixels(pixels,(CubemapFace)face);
        }
        generated.Apply(true,false);if(cube){EditorUtility.CopySerialized(generated,cube);UnityEngine.Object.DestroyImmediate(generated);EditorUtility.SetDirty(cube);}else{cube=generated;AssetDatabase.CreateAsset(cube,path);}return cube;
    }
    static VolumeProfile AtmosphereProfile()
    {
        var p=Asset<VolumeProfile>("Coastal Atmosphere");
        var visual=VolumePart<VisualEnvironment>(p);visual.skyType.Override((int)SkyType.PhysicallyBased);visual.skyAmbientMode.Override(SkyAmbientMode.Dynamic);visual.windSpeed.Override(18);visual.windOrientation.Override(235);
        var sky=VolumePart<PhysicallyBasedSky>(p);sky.type.Override(PhysicallyBasedSkyModel.EarthAdvanced);sky.updateMode.Override(EnvironmentUpdateMode.Realtime);sky.updatePeriod.Override(.5f);sky.spaceEmissionTexture.Override(Stars());sky.aerosolAnisotropy.Override(.75f);
        var clouds=VolumePart<VolumetricClouds>(p);clouds.enable.Override(true);clouds.cloudControl.Override(VolumetricClouds.CloudControl.Simple);clouds.cloudSimpleMode.Override(VolumetricClouds.CloudSimpleMode.Quality);clouds.cloudPreset=VolumetricClouds.CloudPresets.Custom;
        clouds.numPrimarySteps.Override(96);clouds.numLightSteps.Override(8);clouds.shadows.Override(true);clouds.shadowResolution.Override(VolumetricClouds.CloudShadowResolution.Medium256);clouds.shadowDistance.Override(6000);clouds.microErosion.Override(true);clouds.multiScattering.Override(.65f);clouds.powderEffectIntensity.Override(.3f);
        var fog=VolumePart<Fog>(p);fog.enabled.Override(true);fog.enableVolumetricFog.Override(true);fog.baseHeight.Override(-3);fog.maximumHeight.Override(95);fog.meanFreePath.Override(12000);fog.depthExtent.Override(300);fog.anisotropy.Override(.45f);fog.screenResolutionPercentage.Override(12.5f);fog.volumeSliceCount.Override(96);
        var exp=VolumePart<Exposure>(p);exp.mode.Override(ExposureMode.Fixed);exp.fixedExposure.Override(12);
        VolumePart<Tonemapping>(p).mode.Override(TonemappingMode.ACES);
        var bloom=VolumePart<Bloom>(p);bloom.intensity.Override(.16f);bloom.scatter.Override(.62f);
        var grade=VolumePart<ColorAdjustments>(p);grade.contrast.Override(8);grade.saturation.Override(-6);
        var vignette=VolumePart<Vignette>(p);vignette.intensity.Override(.12f);vignette.smoothness.Override(.7f);
        var water=VolumePart<WaterRendering>(p);water.enable.Override(true);water.triangleSize.Override(20);water.ambientProbeDimmer.Override(.65f);
        var ssr=VolumePart<ScreenSpaceReflection>(p);ssr.enabled.Override(true);ssr.enabledTransparent.Override(true);
        var ao=VolumePart<ScreenSpaceAmbientOcclusion>(p);ao.intensity.Override(.55f);
        VolumePart<IndirectLightingController>(p).indirectDiffuseLightingMultiplier.Override(2.0f);
        foreach(var c in p.components){if(!AssetDatabase.Contains(c))AssetDatabase.AddObjectToAsset(c,p);EditorUtility.SetDirty(c);}EditorUtility.SetDirty(p);return p;
    }
    static HDAdditionalLightData LightData(Light light,LightUnit unit,float intensity)
    {
        var d=Component<HDAdditionalLightData>(light.gameObject);light.lightUnit=unit;light.intensity=intensity;return d;
    }
    public static void SetupCamera(Camera camera)
    {
        camera.allowHDR=true;camera.farClipPlane=10000;camera.allowMSAA=false;
        var d=Component<HDAdditionalCameraData>(camera.gameObject);d.volumeLayerMask=1;d.antialiasing=HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;d.taaSharpenStrength=.22f;d.taaAntiFlicker=.7f;
        d.customRenderingSettings=true;
        foreach(var field in new[]{FrameSettingsField.Water,FrameSettingsField.WaterDecals,FrameSettingsField.VolumetricClouds,FrameSettingsField.SSR,FrameSettingsField.TransparentSSR})
        {d.renderingPathCustomFrameSettings.SetEnabled(field,true);d.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field]=true;}
    }
    static Texture2D ParticleTexture()
    {
        string path=Root+"/Soft particle.asset";var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(texture)return texture;
        texture=new Texture2D(64,64,TextureFormat.RGBA32,true,true);var colors=new Color[4096];
        for(int y=0;y<64;y++)for(int x=0;x<64;x++){float r=new Vector2((x-31.5f)/31.5f,(y-31.5f)/31.5f).magnitude;colors[y*64+x]=new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-r*r),2));}
        texture.SetPixels(colors);texture.Apply();AssetDatabase.CreateAsset(texture,path);return texture;
    }
    static ParticleSystem Particles(string name,Transform parent,bool rain)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main;main.loop=true;main.playOnAwake=true;main.maxParticles=rain?5500:1200;main.startLifetime=rain?1.5f:2.5f;main.startSpeed=rain?0:2.8f;main.startSize=rain?.035f:.8f;main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.startColor=rain?new Color(.8f,.9f,1,.32f):new Color(.8f,.87f,.88f,.28f);
        var emission=ps.emission;emission.rateOverTime=rain?0:25;
        var shape=ps.shape;shape.shapeType=rain?ParticleSystemShapeType.Box:ParticleSystemShapeType.Donut;
        if(rain)shape.scale=new Vector3(26,1,26);else{shape.radius=16.5f;shape.donutRadius=.8f;go.transform.rotation=Quaternion.Euler(-90,0,0);}
        var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.x=0;velocity.y=rain?-23:1.5f;velocity.z=0;
        if(rain){var collision=ps.collision;collision.enabled=true;collision.type=ParticleSystemCollisionType.World;collision.mode=ParticleSystemCollisionMode.Collision3D;collision.quality=ParticleSystemCollisionQuality.Medium;collision.collidesWith=~(1<<2);collision.lifetimeLoss=1;collision.maxCollisionShapes=128;}
        var color=ps.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.15f),new GradientAlphaKey(0,1)});color.color=gradient;
        var renderer=ps.GetComponent<ParticleSystemRenderer>();var mat=Material(rain?"Rain streaks":"Salt spray","Lighthouse/HDRP Weather Particles");mat.SetTexture("_BaseMap",ParticleTexture());mat.SetColor("_Tint",rain?new Color(.65f,.75f,.84f,.27f):new Color(.68f,.78f,.82f,.23f));renderer.sharedMaterial=mat;
        renderer.renderMode=rain?ParticleSystemRenderMode.Stretch:ParticleSystemRenderMode.Billboard;renderer.lengthScale=rain?6:1;renderer.velocityScale=rain?.035f:0;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        return ps;
    }
    static void SetupAudio(CoastalAmbience audio)
    {
        audio.WaterBed=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"/Audio/Offshore.ogg");audio.WindLoop=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"/Audio/Wind.wav");
        audio.RainLight=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"/Audio/RainLight.ogg");audio.RainHeavy=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"/Audio/RainHeavy.ogg");audio.ThunderClip=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"/Audio/Thunder.wav");
        audio.SurfClips=Enumerable.Range(1,4).Select(i=>AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"/Audio/Surf_0"+i+".ogg")).ToArray();
        foreach(var clip in audio.SurfClips.Concat(new[]{audio.WaterBed,audio.WindLoop,audio.RainLight,audio.RainHeavy,audio.ThunderClip}).Where(c=>c))
        {
            var importer=(AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));var settings=importer.defaultSampleSettings;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.8f;settings.loadType=clip.length>20?AudioClipLoadType.Streaming:AudioClipLoadType.DecompressOnLoad;importer.defaultSampleSettings=settings;importer.SaveAndReimport();
        }
    }
    [MenuItem("Lighthouse/Upgrade ocean and atmosphere to HDRP")]
    public static string UpgradeScene()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play Mode first.");
        ConfigurePipeline();ConvertMaterials();
        var old=UnityEngine.Object.FindAnyObjectByType<CinematicEnvironment>();if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
        var go=new GameObject("Coastal environment · ocean, sky, weather and sound");var env=go.AddComponent<CinematicEnvironment>();
        env.Session=UnityEngine.Object.FindAnyObjectByType<BlockoutSession>();if(!env.Session)throw new Exception("Open the lighthouse scene first.");
        var director=env.Session.Director;
        env.Presets=new[]{Preset(CoastalWeather.Clear,"Ясно",16,.32f,.91f,.82f,15000,0,0,.09f,1,.8f,1450,2300),Preset(CoastalWeather.Cloudy,"Облачно",26,.46f,.78f,.75f,7500,0,.15f,.2f,.75f,1,1100,2600),Preset(CoastalWeather.Fog,"Морской туман",10,.42f,.55f,.65f,90,0,.45f,.06f,.72f,.45f,800,1300),Preset(CoastalWeather.Rain,"Дождь",35,.58f,.62f,.68f,1600,.65f,1,.28f,.65f,1.1f,700,3000),Preset(CoastalWeather.Storm,"Шторм",62,.72f,.74f,.72f,1000,1,1,.62f,.6f,1.25f,550,4200)};
        env.Atmosphere=go.AddComponent<Volume>();env.Atmosphere.isGlobal=true;env.Atmosphere.priority=10;env.Atmosphere.sharedProfile=AtmosphereProfile();
        env.Sun=director.Sun;LightData(env.Sun,LightUnit.Lux,80000).interactsWithSky=true;
        env.Moon=new GameObject("Moon · cold sea light").AddComponent<Light>();env.Moon.transform.SetParent(go.transform);env.Moon.type=LightType.Directional;env.Moon.shadows=LightShadows.None;var moon=LightData(env.Moon,LightUnit.Lux,3.5f);moon.interactsWithSky=true;moon.angularDiameter=.62f;
        env.Lightning=new GameObject("Lightning illumination").AddComponent<Light>();env.Lightning.transform.SetParent(go.transform);env.Lightning.type=LightType.Directional;env.Lightning.color=new Color(.72f,.83f,1);LightData(env.Lightning,LightUnit.Lux,0).interactsWithSky=false;
        env.Bolt=new GameObject("Branched lightning · photo event").AddComponent<LightningBolt>();env.Bolt.transform.SetParent(go.transform);env.Bolt.BoltMaterial=Material("Lightning channel","Lighthouse/HDRP Lightning");
        foreach(var light in UnityEngine.Object.FindObjectsByType<Light>().Where(l=>l.type!=LightType.Directional))
        {
            LightData(light,LightUnit.Lumen,light.type==LightType.Spot?220000:1700).volumetricDimmer=light.type==LightType.Spot?1.8f:.3f;
            var motion=light.GetComponent<BlockoutMotion>();if(motion)motion.BaseLightIntensity=1700;
        }
        if(director.BeamMesh){director.BeamMesh.gameObject.SetActive(false);director.BeamMesh=null;}
        var oldWater=GameObject.Find("WATER_Ocean");if(oldWater)oldWater.SetActive(false);
        env.Ocean=new GameObject("Atlantic ocean · spectral simulation").AddComponent<WaterSurface>();env.Ocean.transform.SetParent(go.transform);
        var presets=typeof(WaterSurface).Assembly.GetType("UnityEngine.Rendering.HighDefinition.WaterSurfacePresets");presets.GetMethod("ApplyWaterOceanPreset",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{env.Ocean});
        env.Ocean.scriptInteractions=true;env.Ocean.repetitionSize=400;env.Ocean.largeOrientationValue=235;env.Ocean.largeChaos=.82f;env.Ocean.startSmoothness=.96f;env.Ocean.endSmoothness=.88f;env.Ocean.smoothnessFadeStart=80;env.Ocean.smoothnessFadeDistance=1000;
        env.Ocean.refractionColor=new Color(.08f,.34f,.40f).linear;env.Ocean.scatteringColor=new Color(.055f,.27f,.30f).linear;env.Ocean.absorptionDistance=12;env.Ocean.maxRefractionDistance=2;env.Ocean.ambientScattering=.32f;env.Ocean.heightScattering=.3f;env.Ocean.directLightTipScattering=.7f;
        env.Ocean.deformation=true;env.Ocean.foam=true;env.Ocean.foamResolution=WaterSurface.WaterDecalRegionResolution.Resolution1024;env.Ocean.foamSmoothness=.3f;env.Ocean.decalRegionSize=new Vector2(256,256);env.Ocean.decalRegionAnchor=go.transform;env.Ocean.underWater=true;env.Ocean.volumeDepth=40;env.Ocean.underWaterRefraction=true;
        var shore=new GameObject("Shoreline foam and surge").AddComponent<WaterDecal>();shore.transform.SetParent(go.transform);shore.regionSize=new Vector2(64,64);shore.resolution=new Vector2Int(512,512);shore.updateMode=CustomRenderTextureUpdateMode.Realtime;shore.material=Material("Shoreline foam","Lighthouse/HDRP Coastal Foam");shore.deepFoamDimmer=.5f;env.ShoreFoam=shore;
        var buoyancy=Component<OceanBuoyancy>(director.Boat.gameObject);buoyancy.Ocean=env.Ocean;
        var wake=director.Boat.GetComponentInChildren<WaterDecal>();if(!wake){wake=new GameObject("Ship foam wake").AddComponent<WaterDecal>();wake.transform.SetParent(director.Boat,false);wake.transform.localPosition=new Vector3(0,0,7);}
        wake.regionSize=new Vector2(12,25);wake.resolution=new Vector2Int(256,256);wake.updateMode=CustomRenderTextureUpdateMode.Realtime;wake.material=Material("Ship wake","Lighthouse/HDRP Coastal Foam");wake.material.SetFloat("_Wake",1);wake.material.SetFloat("_AffectDeformation",0);wake.surfaceFoamDimmer=.3f;
        env.RainParticles=Particles("Rain volume · follows keeper",go.transform,true);env.SprayParticles=Particles("Breaking spray at rocks",go.transform,false);
        env.Ambience=go.AddComponent<CoastalAmbience>();SetupAudio(env.Ambience);Component<EnvironmentAudioMeter>(env.Session.Player.View.gameObject);
        SetupKeeperLighting(env);
        env.WetMaterials=CoastalArtBuilder.ExteriorMaterials();
        if(env.WetMaterials.Length==0)env.WetMaterials=new[]{"Stone","PaleStone","Wood","Iron","Roof"}.Select(n=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/"+n+".mat")).ToArray();
        SetupCamera(env.Session.Player.View);env.Session.HelpVisible=false;env.Session.Message="Свободный осмотр. F1 — начать вахту, F2 — погода, F3 — время суток.";env.CacheVolume(false);env.ApplyInstant();
        PlayerSettings.productName="Не дай погаснуть";AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return "HDRP ocean, physical sky, five weather profiles, precipitation, foam, buoyancy and ambience configured.";
    }
    static void SetupKeeperLighting(CinematicEnvironment env)
    {
        var rig=env.gameObject.AddComponent<KeeperLighting>();rig.Environment=env;
        Light Fill(string name,Vector3 position,Color color,float range)
        {
            var light=new GameObject(name).AddComponent<Light>();light.transform.SetParent(env.transform);light.transform.position=position;
            light.type=LightType.Point;light.range=range;light.color=color;light.shadows=LightShadows.None;
            LightData(light,LightUnit.Lumen,0).volumetricDimmer=.08f;return light;
        }
        rig.RoomFill=Fill("Soft interior bounce",new Vector3(0,12.2f,0),new Color(.82f,.90f,1),8);
        var markers=UnityEngine.Object.FindObjectsByType<Transform>();
        var desk=markers.FirstOrDefault(t=>t.name=="MARK_RadioTaskLight");
        if(desk)rig.DeskLight=Fill("Radio reading lamp",desk.position,new Color(1,.79f,.53f),3.5f);
        var door=markers.FirstOrDefault(t=>t.name=="MARK_EntryTaskLight");
        if(door)rig.DoorLight=Fill("Entrance lantern",door.position,new Color(1,.75f,.45f),5);
    }
}
