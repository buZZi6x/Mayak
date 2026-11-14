using System;
using System.IO;
using System.Linq;
using LighthouseGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public static class ObservationBuilder
{
 const string Root="Assets/Art/Fleet";
 static Material Material(string name,Color color,float metallic=0)
 {
  string path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(!m){m=new Material(Shader.Find("HDRP/Lit"));AssetDatabase.CreateAsset(m,path);}
  m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",metallic>.1f?.6f:.42f);
  if(name=="Fleet_Sail"){m.SetFloat("_DoubleSidedEnable",1);m.SetFloat("_CullMode",0);m.SetFloat("_CullModeForward",0);}
  if(name=="Fleet_Deck"||name=="Fleet_Sail")
  {var src=CoastalArtBuilder.FindMaterial(name=="Fleet_Deck"?"Coast_Wood":"Coast_Linen");if(src){m.SetTexture("_BaseColorMap",src.GetTexture("_BaseColorMap"));m.SetTexture("_NormalMap",src.GetTexture("_NormalMap"));m.SetFloat("_NormalScale",.35f);}}
  if(name=="Fleet_Light"){m.SetColor("_EmissiveColor",new Color(1,.5f,.1f)*5);}
  HDMaterial.ValidateMaterial(m);EditorUtility.SetDirty(m);return m;
 }
 static void Combine(Transform group,string prefix)
 {
  var renderers=group.GetComponentsInChildren<MeshRenderer>(true).Where(r=>!r.GetComponentsInParent<Transform>().TakeWhile(t=>t!=group).Any(t=>t.name.StartsWith("ANIM_"))).ToArray();
  foreach(var mat in renderers.SelectMany(r=>r.sharedMaterials).Distinct().ToArray())
  {
   var list=renderers.Where(r=>r&&r.sharedMaterial==mat).ToArray();if(list.Length==0)continue;
   var ci=list.Select(r=>new CombineInstance{mesh=r.GetComponent<MeshFilter>().sharedMesh,transform=group.worldToLocalMatrix*r.transform.localToWorldMatrix}).ToArray();
   var mesh=new Mesh{name=prefix+"_"+mat.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(ci,true,true);mesh.RecalculateBounds();
   string path=Root+"/Meshes/"+prefix+"_"+mat.name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
   if(old){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,path);
   var go=new GameObject("Combined · "+mat.name);go.transform.SetParent(group,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;
   foreach(var r in list)UnityEngine.Object.DestroyImmediate(r);
  }
 }
 [MenuItem("Lighthouse/Install observations and coastal fleet")]
 public static string Install()
 {
  if(EditorApplication.isPlaying)throw new Exception("Stop play mode first.");
  Directory.CreateDirectory(Root+"/Materials");Directory.CreateDirectory(Root+"/Meshes");AssetDatabase.Refresh();
  var colors=new[]{new Color(.035f,.10f,.14f),new Color(.38f,.055f,.025f),new Color(.04f,.24f,.23f),new Color(.7f,.55f,.36f),new Color(1,.9f,.72f),new Color(.65f,.4f,.1f),new Color(.045f,.055f,.06f),new Color(.73f,.69f,.54f),new Color(.08f,.27f,.32f),new Color(1,.65f,.18f)};
  string[] names={"HullNavy","HullRed","HullTeal","Deck","Sail","Brass","Iron","Ivory","Glass","Light"};
  var mats=names.Select((n,i)=>Material("Fleet_"+n,colors[i],n=="Brass"?.75f:n=="Iron"?.65f:0)).ToDictionary(m=>m.name);
  var session=UnityEngine.Object.FindAnyObjectByType<BlockoutSession>();if(!session)throw new Exception("Open lighthouse scene.");
  var boat=session.Director.Boat;
  foreach(var child in boat.Cast<Transform>().ToArray())if(!child.GetComponent<WaterDecal>())UnityEngine.Object.DestroyImmediate(child.gameObject);
  var fleet=boat.GetComponent<CoastalFleet>();if(!fleet)fleet=boat.gameObject.AddComponent<CoastalFleet>();
  fleet.Vessels=new Transform[3];string[] models={"Cutter","Brigantine","SteamTrawler"};
  for(int i=0;i<3;i++)
  {
   string path=Root+"/"+models[i]+".fbx";var imp=(ModelImporter)AssetImporter.GetAtPath(path);imp.isReadable=true;imp.importAnimation=false;imp.SaveAndReimport();
   var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),boat,false);go.name=models[i]+" · authored in Blender";fleet.Vessels[i]=go.transform;
   foreach(var r in go.GetComponentsInChildren<Renderer>()){r.sharedMaterials=r.sharedMaterials.Select(m=>mats.TryGetValue(m.name,out var replacement)?replacement:mats["Fleet_Ivory"]).ToArray();r.gameObject.layer=2;}
   var animated=go.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("ANIM_")).ToArray();
   Combine(go.transform,models[i]);for(int a=0;a<animated.Length;a++)Combine(animated[a],models[i]+"_Rig"+a);
   if(i==2)AddSmoke(go.transform);
   go.SetActive(i==0);
  }
  var buoy=boat.GetComponent<OceanBuoyancy>();if(buoy){buoy.Length=14;buoy.Width=4;}
  var wake=boat.GetComponentInChildren<WaterDecal>();if(wake){wake.transform.localPosition=new Vector3(0,0,10);wake.regionSize=new Vector2(14,38);}
  var old=session.GetComponent<ObservationDirector>();if(old)UnityEngine.Object.DestroyImmediate(old);var obs=session.gameObject.AddComponent<ObservationDirector>();obs.Session=session;session.Observations=obs;
  string vfxPath="Assets/Environment/Materials/Observation effects.mat";var vfx=AssetDatabase.LoadAssetAtPath<Material>(vfxPath);if(!vfx){vfx=new Material(Shader.Find("Lighthouse/HDRP Observations"));AssetDatabase.CreateAsset(vfx,vfxPath);}obs.EffectMaterial=vfx;
  var env=UnityEngine.Object.FindAnyObjectByType<CinematicEnvironment>();env.Bolt.Duration=.18f;
  session.Director.FreezePreview=false;
  EditorUtility.SetDirty(fleet);EditorUtility.SetDirty(env.Bolt);EditorUtility.SetDirty(session);
  EditorSceneManager.MarkSceneDirty(session.gameObject.scene);EditorSceneManager.SaveScene(session.gameObject.scene);AssetDatabase.SaveAssets();
  return "8 observations, 3 authored ships, 0.18-second lightning installed.";
 }
 static void AddSmoke(Transform vessel)
 {
  string texturePath=Root+"/Materials/Soft smoke.asset";var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
  if(!texture){texture=new Texture2D(64,64,TextureFormat.RGBA32,false);texture.name="Soft smoke";var pixels=new Color[4096];for(int y=0;y<64;y++)for(int x=0;x<64;x++){float r=new Vector2((x-31.5f)/31.5f,(y-31.5f)/31.5f).magnitude;pixels[y*64+x]=new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-r),2));}texture.SetPixels(pixels);texture.Apply();AssetDatabase.CreateAsset(texture,texturePath);}
  string path=Root+"/Materials/Funnel smoke.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(!material){material=new Material(Shader.Find("Lighthouse/HDRP Weather Particles"));AssetDatabase.CreateAsset(material,path);}material.SetTexture("_BaseMap",texture);material.SetColor("_Tint",new Color(.13f,.15f,.17f,.6f));
  var marker=vessel.GetComponentsInChildren<Transform>().First(t=>t.name.StartsWith("MARK_Smoke"));
  var go=new GameObject("Funnel smoke · world space");go.transform.SetParent(marker,false);var ps=go.AddComponent<ParticleSystem>();ps.Stop();var main=ps.main;main.loop=true;main.prewarm=true;main.startLifetime=5;main.startSpeed=.1f;main.startSize=new ParticleSystem.MinMaxCurve(.7f,1.2f);main.maxParticles=96;main.simulationSpace=ParticleSystemSimulationSpace.World;
  var emission=ps.emission;emission.rateOverTime=7;var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.3f;
  var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.x=.8f;velocity.y=1.4f;velocity.z=.6f;
  var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.5f,1,4));
  var alpha=ps.colorOverLifetime;alpha.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.7f,.12f),new GradientAlphaKey(0,1)});alpha.color=gradient;
  var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
 }
}
