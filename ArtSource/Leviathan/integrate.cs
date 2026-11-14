using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using LighthouseGame;

// One-off content integration; does not run any scene builder.
if (EditorApplication.isPlaying) throw new Exception("Edit Mode required.");
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.path != "Assets/Scenes/LighthouseBlockout.unity" || scene.isDirty)
    throw new Exception("The clean LighthouseBlockout scene is required.");
if (UnityEngine.Object.FindAnyObjectByType<LeviathanEncounter>()) throw new Exception("Leviathan is already installed.");
const string assets = "Assets/Art/Leviathan/";
var source = AssetDatabase.LoadAssetAtPath<GameObject>(assets + "Leviathan.fbx");
if (!source) throw new Exception("Import the authored Leviathan FBX first.");
string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Artifacts/Leviathan"));
Directory.CreateDirectory(output);
File.Copy(scene.path, Path.Combine(output, "scene-before.unity"), false);
Directory.CreateDirectory(assets + "Materials");
AssetDatabase.Refresh();
var model = UnityEngine.Object.Instantiate(source);
model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
model.transform.localScale = Vector3.one;
var filters = model.GetComponentsInChildren<MeshFilter>();
var parts = new Dictionary<string, List<CombineInstance>>();
foreach (var filter in filters)
{
    var renderer = filter.GetComponent<MeshRenderer>();
    for (int s = 0; s < filter.sharedMesh.subMeshCount; s++)
    {
        string name = renderer.sharedMaterials[s].name;
        if (!parts.ContainsKey(name)) parts[name] = new List<CombineInstance>();
        parts[name].Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = s, transform = filter.transform.localToWorldMatrix });
    }
}
string[] names = parts.Keys.OrderBy(x => x).ToArray();
var materialColors = new Dictionary<string, Color> {
    {"Leviathan_Skin", new Color(.06f,.19f,.21f)}, {"Leviathan_Belly",new Color(.34f,.43f,.38f)},
    {"Leviathan_Ridge",new Color(.11f,.28f,.30f)}, {"Leviathan_Bone",new Color(.58f,.62f,.50f)},
    {"Leviathan_Mouth",new Color(.012f,.022f,.025f)}, {"Leviathan_Glow",new Color(.10f,.55f,.55f)} };
var materials = new Material[names.Length];
var combinedParts = new Mesh[names.Length];
for (int i = 0; i < names.Length; i++)
{
    var material = new Material(Shader.Find("HDRP/Lit")) { name = names[i] };
    material.SetColor("_BaseColor", materialColors[names[i]]);
    material.SetFloat("_Smoothness", .42f);
    if (names[i] == "Leviathan_Skin" || names[i] == "Leviathan_Ridge")
        material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Marine/AbyssDermis.png"));
    if (names[i] == "Leviathan_Glow") material.SetColor("_EmissiveColor",new Color(.13f,.85f,.8f)*2);
    HDMaterial.ValidateMaterial(material);
    AssetDatabase.CreateAsset(material,assets+"Materials/"+names[i]+".mat");
    materials[i] = material;
    combinedParts[i] = new Mesh { indexFormat = IndexFormat.UInt32 };
    combinedParts[i].CombineMeshes(parts[names[i]].ToArray(), true, true);
}
var mesh = new Mesh { name = "Leviathan · continuous skinned anatomy", indexFormat = IndexFormat.UInt32 };
mesh.CombineMeshes(combinedParts.Select(m=>new CombineInstance {mesh=m,transform=Matrix4x4.identity}).ToArray(),false,false);
var visual = new GameObject("Leviathan · distant breach");
visual.layer = 2;
var bones = new Transform[LeviathanBreach.BoneCount];
var bindposes = new Matrix4x4[bones.Length];
for (int i=0;i<bones.Length;i++)
{
    bones[i] = new GameObject("Spine_"+i.ToString("00")).transform;
    bones[i].SetParent(visual.transform,false);
    bones[i].localPosition = Vector3.back*(i*LeviathanBreach.SpineLength/(bones.Length-1));
    bindposes[i] = bones[i].worldToLocalMatrix*visual.transform.localToWorldMatrix;
}
var weights = new BoneWeight[mesh.vertexCount];
var vertices = mesh.vertices;
for (int i=0;i<vertices.Length;i++)
{
    float b = Mathf.Clamp(-vertices[i].z/LeviathanBreach.SpineLength*(bones.Length-1),0,bones.Length-1);
    int lower = Mathf.FloorToInt(b), upper = Mathf.Min(lower+1,bones.Length-1);
    weights[i] = new BoneWeight {boneIndex0=lower,boneIndex1=upper,weight0=1-(b-lower),weight1=b-lower};
}
mesh.boneWeights=weights;mesh.bindposes=bindposes;mesh.RecalculateBounds();
AssetDatabase.CreateAsset(mesh,assets+"LeviathanSkinned.asset");
var body=visual.AddComponent<SkinnedMeshRenderer>();
body.sharedMesh=mesh;body.sharedMaterials=materials;body.bones=bones;body.rootBone=visual.transform;
body.updateWhenOffscreen=true;body.quality=SkinQuality.Bone2;body.enabled=false;
PrefabUtility.SaveAsPrefabAsset(visual,assets+"LeviathanVisual.prefab");
var encounter=visual.AddComponent<LeviathanEncounter>();
var serialized=new SerializedObject(encounter);
serialized.FindProperty("_session").objectReferenceValue=UnityEngine.Object.FindAnyObjectByType<BlockoutSession>();
serialized.FindProperty("_body").objectReferenceValue=body;
var spine=serialized.FindProperty("_spine");spine.arraySize=bones.Length;
for(int i=0;i<bones.Length;i++)spine.GetArrayElementAtIndex(i).objectReferenceValue=bones[i];
serialized.FindProperty("_sprayMaterial").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Material>("Assets/Environment/Materials/Salt spray.mat");
serialized.FindProperty("_foamMaterial").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Encounters/Materials/Kraken foam.mat");
serialized.FindProperty("_splashClip").objectReferenceValue=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Marine/Audio/SeaDisplacement.wav");
serialized.FindProperty("_roarClip").objectReferenceValue=AssetDatabase.LoadAssetAtPath<AudioClip>(assets+"Audio/Leviathan_Roar.wav");
serialized.ApplyModifiedPropertiesWithoutUndo();
UnityEngine.Object.DestroyImmediate(model);
foreach(var part in combinedParts)UnityEngine.Object.DestroyImmediate(part);
AssetDatabase.SaveAssets();
EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
File.WriteAllText(Path.Combine(output,"scene-wiring.json"),JsonUtility.ToJson(new LeviathanIntegrationReport {
    Scene=scene.path,Vertices=mesh.vertexCount,Bones=bones.Length,Materials=names },true));
return new {scene=scene.path, roots=scene.rootCount,vertices=mesh.vertexCount,bones=bones.Length,materials=names,bounds=mesh.bounds.ToString()};

[Serializable]
public sealed class LeviathanIntegrationReport
{
    public string Scene;
    public int Vertices, Bones;
    public string[] Materials;
}
