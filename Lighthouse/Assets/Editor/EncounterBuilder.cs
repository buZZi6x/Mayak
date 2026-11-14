using System;
using System.IO;
using System.Linq;
using LighthouseGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
public static class EncounterBuilder
{
 const string Root="Assets/Art/Encounters";
 static Material Mat(string name,Color c,string shader="HDRP/Lit")
 {
  string path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,path);}m.SetColor(shader=="HDRP/Lit"?"_BaseColor":"_Color",c);
  if(shader=="HDRP/Lit"){m.SetFloat("_Smoothness",name=="AbyssSkin"?.72f:.35f);HDMaterial.ValidateMaterial(m);}EditorUtility.SetDirty(m);return m;
 }
 static GameObject Import(string name,Transform parent)
 {
  string path=Root+"/"+name+".fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.isReadable=true;importer.importAnimation=false;importer.SaveAndReimport();var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),parent,false);go.name=name;return go;
 }
 [MenuItem("Lighthouse/Install Kraken encounters and distant coast")]
 public static string Install()
 {
  if(EditorApplication.isPlaying)throw new Exception("Stop Play first");Directory.CreateDirectory(Root+"/Materials");AssetDatabase.Refresh();
  var s=UnityEngine.Object.FindAnyObjectByType<BlockoutSession>();var d=s.Director;
  foreach(string n in new[]{"Encounter staging","Distant archipelago"}){var old=GameObject.Find(n);if(old)UnityEngine.Object.DestroyImmediate(old);}
  var oldEncounter=s.GetComponent<KrakenEncounter>();if(oldEncounter)UnityEngine.Object.DestroyImmediate(oldEncounter);
  var root=new GameObject("Encounter staging").transform;var e=s.gameObject.AddComponent<KrakenEncounter>();e.Director=d;d.Encounter=e;
  var readability=root.gameObject.AddComponent<EncounterReadability>();
  for(int i=0;i<2;i++){var go=new GameObject(i==0?"Sea bounce":"Sea rim");go.transform.SetParent(root);var light=go.AddComponent<Light>();light.type=LightType.Point;light.lightUnit=LightUnit.Candela;light.range=54;light.color=i==0?new Color(.80f,.87f,.94f):new Color(.55f,.70f,.84f);light.shadows=LightShadows.None;var hd=go.AddComponent<HDAdditionalLightData>();hd.affectsVolumetric=false;hd.affectSpecular=false;if(i==0)readability.SeaFill=light;else readability.SeaRim=light;}
  e.Skin=Mat("AbyssSkin",new Color(.55f,.73f,.68f));e.Skin.SetFloat("_Metallic",0);e.Skin.SetFloat("_Smoothness",.48f);e.Skin.SetFloat("_SmoothnessRemapMin",.25f);e.Skin.SetFloat("_SmoothnessRemapMax",.55f);e.Skin.SetTexture("_BaseColorMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/AbyssSkin.png"));HDMaterial.ValidateMaterial(e.Skin);
  e.Suckers=Mat("Sucker",new Color(.48f,.29f,.22f));var eyes=Mat("EyeGold",new Color(.8f,.37f,.025f));eyes.SetColor("_EmissiveColor",new Color(.6f,.19f,.015f)*1.2f);HDMaterial.ValidateMaterial(eyes);var pupil=Mat("Pupil",new Color(.004f,.008f,.008f));
  foreach(Transform child in d.Kraken.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
  var mantle=Import("KrakenMantle",d.Kraken);foreach(var r in mantle.GetComponentsInChildren<Renderer>()){string n=r.sharedMaterial.name;r.sharedMaterial=n=="Sucker"?e.Suckers:n=="EyeGold"?eyes:n=="Pupil"?pupil:e.Skin;r.gameObject.layer=2;}
  var foam=Mat("Kraken foam",Color.white,"Lighthouse/HDRP Coastal Foam");foam.SetFloat("_Encounter",1);foam.SetFloat("_AffectDeformation",0);
  e.Disturbance=new GameObject("Kraken churn · ocean foam").AddComponent<WaterDecal>();e.Disturbance.transform.SetParent(root);e.Disturbance.material=foam;e.Disturbance.regionSize=new Vector2(42,42);e.Disturbance.resolution=new Vector2Int(256,256);e.Disturbance.updateMode=CustomRenderTextureUpdateMode.Realtime;
  e.Debris=new GameObject("Floating wreckage · mast and hull planks").transform;e.Debris.SetParent(root);
  var wood=Mat("Exposed wreck timber",new Color(.75f,.57f,.35f));var coastalWood=CoastalArtBuilder.FindMaterial("Coast_Wood");if(coastalWood){wood.SetTexture("_BaseColorMap",coastalWood.GetTexture("_BaseColorMap"));wood.SetTexture("_NormalMap",coastalWood.GetTexture("_NormalMap"));HDMaterial.ValidateMaterial(wood);}
  for(int i=0;i<12;i++)
  {
   var ob=GameObject.CreatePrimitive(i==0?PrimitiveType.Cylinder:PrimitiveType.Cube);ob.name=i==0?"Broken mast":"Hull plank "+i;UnityEngine.Object.DestroyImmediate(ob.GetComponent<Collider>());ob.layer=2;ob.transform.SetParent(e.Debris,false);
   var mesh=UnityEngine.Object.Instantiate(ob.GetComponent<MeshFilter>().sharedMesh);mesh.name="Fragment"+i;var vs=mesh.vertices;for(int v=0;v<vs.Length;v++){if(i==0)vs[v]=Quaternion.Euler(0,0,90)*Vector3.Scale(vs[v],new Vector3(.4f,3.5f,.4f));else vs[v]=Vector3.Scale(vs[v],new Vector3(i<4?3.5f:1.8f,.2f,.5f));}mesh.vertices=vs;mesh.RecalculateBounds();mesh.RecalculateNormals();string path=Root+"/Fragment"+i+".asset";var prior=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(prior){EditorUtility.CopySerialized(mesh,prior);UnityEngine.Object.DestroyImmediate(mesh);mesh=prior;}else AssetDatabase.CreateAsset(mesh,path);ob.GetComponent<MeshFilter>().sharedMesh=mesh;ob.GetComponent<Renderer>().sharedMaterial=wood;
  }
  // Replace the old capsule markers with finned Blender fish.
  var tornPivot=new GameObject("Recognizable torn sail and broken yard").transform;tornPivot.SetParent(e.Debris,false);tornPivot.SetSiblingIndex(0);var torn=Import("WreckSail",tornPivot);var linen=Mat("Wreck sail",new Color(.84f,.73f,.52f));linen.SetFloat("_DoubleSidedEnable",1);linen.SetFloat("_CullMode",0);linen.SetFloat("_CullModeForward",0);HDMaterial.ValidateMaterial(linen);foreach(var r in torn.GetComponentsInChildren<Renderer>()){r.sharedMaterial=r.sharedMaterial.name=="WreckLinen"?linen:wood;r.gameObject.layer=2;}
  foreach(var child in d.Shoal.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
  var fishMat=Mat("SilverFish",new Color(.15f,.44f,.48f));fishMat.SetColor("_EmissiveColor",new Color(.05f,.55f,.59f)*2);HDMaterial.ValidateMaterial(fishMat);
  for(int i=0;i<7;i++){var pivot=new GameObject("Swimming fish "+i).transform;pivot.SetParent(d.Shoal,false);var fish=Import("SilverFish",pivot);foreach(var r in fish.GetComponentsInChildren<Renderer>()){r.sharedMaterial=r.sharedMaterial.name=="Pupil"?pupil:fishMat;r.gameObject.layer=2;}}
  // Reuse the authored brigantine silhouette, including its animated rig.
  var ghostRoot=new GameObject("Sky ghost ship · pooled apparition");ghostRoot.transform.SetParent(root);var ghost=ghostRoot.AddComponent<SkyGhostShip>();s.Observations.Ghost=ghost;
  var pivotShip=new GameObject("Spectral flight pivot").transform;pivotShip.SetParent(ghostRoot.transform,false);pivotShip.localScale=Vector3.one*1.35f;ghost.Vessel=pivotShip;
  var fleet=d.Boat.GetComponent<CoastalFleet>();var vessel=UnityEngine.Object.Instantiate(fleet.Vessels[1].gameObject,pivotShip,false);vessel.name="Spectral brigantine";vessel.SetActive(true);
  var spectralHull=Mat("Spectral hull",new Color(.16f,.48f,.52f),"Lighthouse/HDRP Spectral Vessel");var spectralSail=Mat("Spectral sails",new Color(.31f,.61f,.60f),"Lighthouse/HDRP Spectral Vessel");spectralSail.SetFloat("_Sail",1);
  ghost.Surfaces=vessel.GetComponentsInChildren<Renderer>(true);foreach(var r in ghost.Surfaces){r.sharedMaterials=r.sharedMaterials.Select(m=>m.name.Contains("Sail")?spectralSail:spectralHull).ToArray();r.shadowCastingMode=ShadowCastingMode.Off;r.gameObject.layer=2;}
  var motion=vessel.AddComponent<CoastalFleet>();motion.Vessels=new[]{vessel.transform}; // Existing sail pivots are retained.
  ghost.Wisps=new LineRenderer[3];var wisp=Mat("Spectral wake",Color.white,"Lighthouse/HDRP Spectral Wake");
  for(int i=0;i<3;i++){var line=new GameObject("Trailing spectral mist "+i).AddComponent<LineRenderer>();line.transform.SetParent(ghostRoot.transform);line.gameObject.layer=2;line.sharedMaterial=wisp;line.positionCount=36;line.startWidth=3.2f;line.endWidth=.1f;line.numCornerVertices=2;line.shadowCastingMode=ShadowCastingMode.Off;ghost.Wisps[i]=line;}
  var coast=new GameObject("Distant archipelago").transform;var outward=new Vector3(s.SeaEvent.position.x,0,s.SeaEvent.position.z).normalized;
  var basalt=Mat("Basalt",new Color(.25f,.30f,.32f));var moss=Mat("Moss",new Color(.25f,.30f,.22f));var snow=Mat("Snow",new Color(.57f,.64f,.65f));
  var stone=CoastalArtBuilder.FindMaterial("Coast_Rock");if(stone)foreach(var m in new[]{basalt,moss}){m.SetTexture("_BaseColorMap",stone.GetTexture("_BaseColorMap"));m.SetTexture("_NormalMap",stone.GetTexture("_NormalMap"));m.SetFloat("_NormalScale",1.2f);m.SetFloat("_Smoothness",.14f);HDMaterial.ValidateMaterial(m);}
  float[] angles={-66,-38,40,71,136,204};float[] distances={1500,2350,2050,1250,2400,2200};string[] models={"NeedleCape","BrokenRidge","BrokenRidge","BasaltIsland","NeedleCape","BasaltIsland"};
  for(int i=0;i<angles.Length;i++){var placement=new GameObject(models[i]+" · distant group "+i).transform;placement.SetParent(coast,false);var island=Import(models[i],placement);placement.position=Quaternion.Euler(0,angles[i],0)*outward*distances[i];placement.rotation=Quaternion.Euler(0,i*71,0);placement.localScale=Vector3.one*(i==0?.8f:i==2?1.2f:1);foreach(var r in island.GetComponentsInChildren<Renderer>()){r.sharedMaterials=r.sharedMaterials.Select(m=>m.name=="Moss"?moss:m.name=="Snow"?snow:basalt).ToArray();r.shadowCastingMode=ShadowCastingMode.Off;r.gameObject.layer=2;}}
  s.Player.View.farClipPlane=4000;d.FreezePreview=false;
  EditorUtility.SetDirty(s);EditorSceneManager.MarkSceneDirty(s.gameObject.scene);EditorSceneManager.SaveScene(s.gameObject.scene);AssetDatabase.SaveAssets();return "Kraken contact choreography, wreckage, escape, ghost ship and six distant islands installed";
 }
}
