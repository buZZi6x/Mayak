// One-time additive scene wiring. Run with `unity command script eval --file ...` in Edit Mode.
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using LighthouseGame;

if (EditorApplication.isPlaying) throw new Exception("Install the encounter in Edit Mode.");
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.path != "Assets/Scenes/LighthouseBlockout.unity") throw new Exception("Wrong scene.");
if (GameObject.Find("Weeping girl · pier encounter")) throw new Exception("Encounter already installed; edit its existing root.");
const string rootPath = "Assets/Art/WeepingGirl";
Directory.CreateDirectory(rootPath + "/Materials");
Directory.CreateDirectory(rootPath + "/Geometry");
AssetDatabase.Refresh();
var modelPath = rootPath + "/Models/WeepingGirl.fbx";
var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
if (!importer) throw new Exception("WeepingGirl.fbx is not ready.");
importer.importBlendShapes = true;
importer.importAnimation = false;
importer.isReadable = true;
importer.SaveAndReimport();
Material MakeMaterial(string name, Color color, float rim)
{
    string path = rootPath + "/Materials/" + name + ".mat";
    var m = AssetDatabase.LoadAssetAtPath<Material>(path);
    if (!m) { m = new Material(Shader.Find("Lighthouse/HDRP Apparition")); AssetDatabase.CreateAsset(m, path); }
    m.shader=Shader.Find("Lighthouse/HDRP Apparition");m.SetColor("_Color",color);m.SetFloat("_Rim",rim);
    m.SetFloat("_Fabric",name=="Girl_Cloth"?1:0);m.SetFloat("_Visibility",1000);EditorUtility.SetDirty(m);
    return m;
}
var cloth = MakeMaterial("Girl_Cloth", new Color(.30f,.355f,.35f),.065f);
var hair = MakeMaterial("Girl_Hair", new Color(.012f,.018f,.021f),.1f);
var skin = MakeMaterial("Girl_Skin", new Color(.43f,.49f,.47f),.05f);
var dark = MakeMaterial("Girl_Dark",new Color(.0015f,.002f,.003f),0);
var eyes = MakeMaterial("Girl_Eyes",new Color(.52f,.64f,.60f),.08f);
var materials = new[]{cloth,hair,skin,dark,eyes};
var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Coastal/Materials/Coast_Wood.mat");
var iron = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Coastal/Materials/Coast_Iron.mat");
var rock = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Coastal/Materials/Coast_Rock.mat");
var root = new GameObject("Weeping girl · pier encounter");
Undo.RegisterCreatedObjectUndo(root,"Add weeping girl encounter");
root.transform.SetPositionAndRotation(new Vector3(7.2f,4.2f,9.2f),Quaternion.Euler(0,36,0));
GameObject Part(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, bool solid = true)
{
    var obj = GameObject.CreatePrimitive(type); obj.name = name;
    obj.transform.SetParent(root.transform,false); obj.transform.localPosition = position; obj.transform.localScale = scale;
    obj.GetComponent<Renderer>().sharedMaterial = material;
    if (!solid) UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
    return obj;
}
// The pier begins in the existing southeast gap in the terrace rail, at the same floor height.
for (int i=0;i<36;i++)
{
    var board = Part("Pier · weathered board " + i,PrimitiveType.Cube,new Vector3(Mathf.Sin(i*3.1f)*.023f,.025f, i*.285f),new Vector3(1.9f,.15f,.27f),wood);
    board.transform.localRotation = Quaternion.Euler(0,Mathf.Sin(i*5.7f)*.7f,Mathf.Sin(i*2.3f)*.2f);
}
foreach (float side in new[]{-.77f,.77f})
{
    Part("Pier · underbeam",PrimitiveType.Cube,new Vector3(side,-.2f,4.95f),new Vector3(.16f,.3f,10.7f),wood);
    for(int i=0;i<4;i++)
    {
        float z=1.1f+i*2.8f;
        Part("Pier · tidal pile",PrimitiveType.Cylinder,new Vector3(side,-2.3f,z),new Vector3(.26f,2.75f,.26f),wood);
        Part("Pier · iron collar",PrimitiveType.Cylinder,new Vector3(side,.32f,z),new Vector3(.29f,.035f,.29f),iron,false);
    }
}
var seat = Part("Girl · wavecut seat",PrimitiveType.Sphere,new Vector3(-3.35f,-1.1f,7.4f),new Vector3(3.3f,2.3f,3.0f),rock,false);
var seatMesh = UnityEngine.Object.Instantiate(seat.GetComponent<MeshFilter>().sharedMesh);
var vertices = seatMesh.vertices;
for(int i=0;i<vertices.Length;i++)
{
    var v=vertices[i]; float rough=Mathf.Sin(v.x*29+v.z*17)*Mathf.Sin(v.y*31-v.x*13)*.045f;
    v*=1+rough; if(v.y>.32f) v.y=Mathf.Lerp(v.y,.43f,.82f); vertices[i]=v;
}
seatMesh.vertices=vertices;seatMesh.RecalculateNormals();seatMesh.RecalculateBounds();seatMesh.name="Wavecut seat";
AssetDatabase.CreateAsset(seatMesh,rootPath+"/Geometry/WavecutSeat.asset");
seat.GetComponent<MeshFilter>().sharedMesh=seatMesh;seat.AddComponent<MeshCollider>().sharedMesh=seatMesh;
for(int i=0;i<3;i++)
{
    var b=Part("Girl · shoreline stone "+i,PrimitiveType.Sphere,new Vector3(-4.1f+i*.8f,-3.1f,5.8f+i*1.8f),new Vector3(2.1f,2.8f,2.3f),rock);
    b.GetComponent<MeshFilter>().sharedMesh=seatMesh;
}
var figure = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
figure.name="Weeping girl · apparition"; figure.transform.SetParent(root.transform,false);
figure.transform.localPosition=new Vector3(-2.4f,-.53f,6.956f);
figure.transform.localRotation=Quaternion.Euler(0,115,0);
foreach(var renderer in figure.GetComponentsInChildren<Renderer>())
{
    renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>materials.FirstOrDefault(a=>m&&m.name.Contains(a.name))??cloth).ToArray();
    renderer.shadowCastingMode=ShadowCastingMode.Off;
}
var body=figure.GetComponentInChildren<SkinnedMeshRenderer>();
if(!body) throw new Exception("A blendshape mesh is required.");
body.updateWhenOffscreen=true;
// Blender may prefix a shape name with the mesh. Normalize just our two imported names if necessary.
string shapes=string.Join(",",Enumerable.Range(0,body.sharedMesh.blendShapeCount).Select(body.sharedMesh.GetBlendShapeName));
if(body.sharedMesh.GetBlendShapeIndex("Seated")<0||body.sharedMesh.GetBlendShapeIndex("Attack")<0)throw new Exception("Unexpected blendshapes: "+shapes);
body.SetBlendShapeWeight(body.sharedMesh.GetBlendShapeIndex("Seated"),100);
AudioSource Sound(string name,string filename,Transform parent,bool spatial,bool loop)
{
    var go=new GameObject(name);go.transform.SetParent(parent,false);
    var s=go.AddComponent<AudioSource>();s.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(rootPath+"/Audio/"+filename);
    s.playOnAwake=false;s.loop=loop;s.spatialBlend=spatial?1:0;s.volume=0;s.dopplerLevel=0;s.priority=spatial?48:12;
    s.minDistance=3;s.maxDistance=42;s.rolloffMode=AudioRolloffMode.Linear;
    if(spatial){go.transform.localPosition=Vector3.up*.8f;go.AddComponent<AudioLowPassFilter>().cutoffFrequency=8500;}
    return s;
}
var cry=Sound("Girl · spatial sobs","Girl_Weeping.wav",figure.transform,true,true);
var signal=Sound("Girl · signal tension","Girl_Signal.wav",root.transform,false,false);
var strike=Sound("Girl · lunge and impact","Girl_Lunge.wav",root.transform,false,false);
var effect=new Material(Shader.Find("Lighthouse/HDRP Weeping Girl Interference"));
AssetDatabase.CreateAsset(effect,rootPath+"/Materials/Girl_Interference.mat");
var encounter=root.AddComponent<WeepingGirlEncounter>();
var so=new SerializedObject(encounter);
void Ref(string field,UnityEngine.Object value){so.FindProperty(field).objectReferenceValue=value;}
Ref("_session",UnityEngine.Object.FindAnyObjectByType<BlockoutSession>());Ref("_figure",figure.transform);Ref("_body",body);
Ref("_interferenceTemplate",effect);Ref("_cry",cry);Ref("_signal",signal);Ref("_strike",strike);Ref("_ambience",UnityEngine.Object.FindAnyObjectByType<CoastalAmbience>());
so.ApplyModifiedPropertiesWithoutUndo();
AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
return "Encounter installed additively. Mesh: "+body.sharedMesh.vertexCount+" vertices; shapes: "+shapes+"; girl origin: "+figure.transform.position;
