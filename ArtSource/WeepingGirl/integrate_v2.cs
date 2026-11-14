using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using LighthouseGame;

if (EditorApplication.isPlaying) throw new Exception("Edit Mode required.");
var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.isDirty) throw new Exception("Save/reconcile the current scene before this one-off migration.");
const string assetRoot="Assets/Art/WeepingGirl/";
var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(assetRoot+"Models/WeepingGirl.fbx");
var importedBody=prefab ? prefab.GetComponentInChildren<SkinnedMeshRenderer>() : null;
if(!importedBody || !importedBody.sharedMesh) throw new Exception("Import the revised FBX first.");
for(int i=0;i<9;i++) if(importedBody.sharedMesh.GetBlendShapeIndex("Rise_"+i.ToString("00"))<0) throw new Exception("V2 pose missing: "+i);
var root=GameObject.Find("Weeping girl · pier encounter");
var encounter=root ? root.GetComponent<WeepingGirlEncounter>() : null;
if(!encounter)throw new Exception("The existing encounter root is required; this does not rebuild the scene.");
var props=new SerializedObject(encounter);
var oldFigure=props.FindProperty("_figure").objectReferenceValue as Transform;
if(!oldFigure)throw new Exception("Old figure reference must be reconciled explicitly.");
var cryClip=AssetDatabase.LoadAssetAtPath<AudioClip>(assetRoot+"Audio/Girl_Weeping_Recorded.wav");
var staticClip=AssetDatabase.LoadAssetAtPath<AudioClip>(assetRoot+"Audio/Girl_Static_Dense.wav");
if(!cryClip || !staticClip)throw new Exception("The recorded audio assets are required.");
string[] names={"Girl_Cloth","Girl_Hair","Girl_Skin","Girl_Dark","Girl_Eyes"};
var materials=names.Select(n=>AssetDatabase.LoadAssetAtPath<Material>(assetRoot+"Materials/"+n+".mat")).ToArray();
if(materials.Any(m=>!m))throw new Exception("Existing appearance materials are missing.");
var shader=Shader.Find("Lighthouse/HDRP Apparition");
Color[] colors={new Color(.48f,.51f,.49f),new Color(.40f,.42f,.44f),new Color(.56f,.59f,.55f),new Color(.012f,.013f,.014f),new Color(.18f,.19f,.18f)};
for(int i=0;i<materials.Length;i++)
{
    materials[i].shader=shader;
    materials[i].SetColor("_Color",colors[i]);
    materials[i].SetFloat("_Rim",i==1?.055f:.035f);
    materials[i].SetFloat("_Fabric",i==0?1:0);
    materials[i].SetFloat("_Visibility",1000);
    EditorUtility.SetDirty(materials[i]);
}
var hairTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(assetRoot+"Models/Girl_Hair_BaseMap.png");
if(!hairTexture)throw new Exception("The licensed hair RGBA texture is required.");
materials[1].SetTexture("_BaseMap",hairTexture);materials[1].SetFloat("_AlphaCutoff",.38f);
var hairImporter=AssetImporter.GetAtPath(assetRoot+"Models/Girl_Hair_BaseMap.png") as TextureImporter;
hairImporter.alphaIsTransparency=true;hairImporter.sRGBTexture=true;hairImporter.mipmapEnabled=true;hairImporter.SaveAndReimport();
var figure=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
figure.name="Weeping girl · apparition V2";
figure.transform.SetParent(root.transform,false);
figure.transform.localPosition=new Vector3(-2f,.05f,7.2f);
figure.transform.localRotation=Quaternion.Euler(0,150,0);
figure.transform.localScale=Vector3.one*1.1f;
foreach(var renderer in figure.GetComponentsInChildren<Renderer>())
{
    renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>materials.FirstOrDefault(a=>m&&m.name.Contains(a.name))??materials[0]).ToArray();
    renderer.shadowCastingMode=ShadowCastingMode.Off;
    renderer.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;
}
var body=figure.GetComponentInChildren<SkinnedMeshRenderer>();
body.updateWhenOffscreen=true;
body.SetBlendShapeWeight(body.sharedMesh.GetBlendShapeIndex("Rise_00"),100);
var cryObject=new GameObject("Girl · recorded spatial sobs");
cryObject.transform.SetParent(figure.transform,false);
cryObject.transform.localPosition=new Vector3(0,1.03f,0);
var cry=cryObject.AddComponent<AudioSource>();
cry.clip=cryClip;cry.loop=true;cry.playOnAwake=false;cry.spatialBlend=1;cry.volume=0;
cry.minDistance=3.5f;cry.maxDistance=45;cry.rolloffMode=AudioRolloffMode.Linear;cry.dopplerLevel=0;
cryObject.AddComponent<AudioLowPassFilter>().cutoffFrequency=8500;
var signal=props.FindProperty("_signal").objectReferenceValue as AudioSource;
if(!signal) { var go=new GameObject("Girl · recorded analogue static");go.transform.SetParent(root.transform,false);signal=go.AddComponent<AudioSource>(); }
signal.name="Girl · recorded analogue static";
signal.Stop();signal.clip=staticClip;signal.loop=true;signal.playOnAwake=false;signal.spatialBlend=0;signal.volume=0;signal.pitch=1;signal.dopplerLevel=0;
props.FindProperty("_figure").objectReferenceValue=figure.transform;
props.FindProperty("_body").objectReferenceValue=body;
props.FindProperty("_cry").objectReferenceValue=cry;
props.FindProperty("_signal").objectReferenceValue=signal;
props.FindProperty("_seatedFocus").vector3Value=new Vector3(0,.9934f,.0390f);
props.FindProperty("_standingFocus").vector3Value=new Vector3(0,1.5096f,.1600f);
props.ApplyModifiedPropertiesWithoutUndo();

// Separate natural rock levels support the pelvis and planted feet at pier height.
var mainStone=root.transform.Find("Girl · wavecut seat").gameObject;
var stoneMesh=mainStone.GetComponent<MeshFilter>().sharedMesh;
var stoneMaterial=mainStone.GetComponent<Renderer>().sharedMaterial;
void Stone(string name,Vector3 position,Vector3 scale)
{
    var existing=root.transform.Find(name);
    var stone=existing?existing.gameObject:new GameObject(name);
    stone.transform.SetParent(root.transform,false);
    stone.transform.localPosition=position;stone.transform.localScale=scale;
    stone.transform.localRotation=Quaternion.Euler(0,150,0);
    var filter=stone.GetComponent<MeshFilter>();if(!filter)filter=stone.AddComponent<MeshFilter>();filter.sharedMesh=stoneMesh;
    var renderer=stone.GetComponent<MeshRenderer>();if(!renderer)renderer=stone.AddComponent<MeshRenderer>();renderer.sharedMaterial=stoneMaterial;
    var collider=stone.GetComponent<MeshCollider>();if(!collider)collider=stone.AddComponent<MeshCollider>();collider.sharedMesh=stoneMesh;
}
Stone("Girl · upper seat ledge",new Vector3(-2.187f,.181f,7.524f),new Vector3(1f,.4f,.5f));
Stone("Girl · lower foot ledge",new Vector3(-1.96f,-.1435f,7.13f),new Vector3(1.4f,.45f,1.3f));
var retired=root.transform.Find("Girl · lunge and impact");
if(retired)UnityEngine.Object.DestroyImmediate(retired.gameObject);
UnityEngine.Object.DestroyImmediate(oldFigure.gameObject);
figure.name="Weeping girl · apparition";
EditorUtility.SetDirty(encounter);
EditorSceneManager.MarkSceneDirty(scene);
AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
return "V2 wired: "+body.sharedMesh.name+"; vertices="+body.sharedMesh.vertexCount+"; shapes="+string.Join(",",Enumerable.Range(0,body.sharedMesh.blendShapeCount).Select(body.sharedMesh.GetBlendShapeName))+"; roots="+scene.rootCount;
