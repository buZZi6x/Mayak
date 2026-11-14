using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
if(EditorApplication.isPlaying) throw new System.Exception("Edit Mode required");
var shader=Shader.Find("Lighthouse/HDRP Apparition");
if(!shader)throw new System.Exception("Import the apparition shader first");
string[] names={"Girl_Cloth","Girl_Hair","Girl_Skin","Girl_Dark","Girl_Eyes"};
Color[] colors={new Color(.30f,.355f,.35f),new Color(.012f,.018f,.021f),new Color(.43f,.49f,.47f),new Color(.0015f,.002f,.003f),new Color(.52f,.64f,.60f)};
for(int i=0;i<names.Length;i++)
{
    var m=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/WeepingGirl/Materials/"+names[i]+".mat");
    m.shader=shader;m.SetColor("_Color",colors[i]);m.SetFloat("_Rim",i==0?.065f:i==1?.1f:i==2?.05f:i==3?0:.08f);
    m.SetFloat("_Fabric",i==0?1:0);m.SetFloat("_Visibility",1000);m.renderQueue=3008;EditorUtility.SetDirty(m);
}
var figure=GameObject.Find("Weeping girl · apparition");
figure.transform.localPosition=new Vector3(-2.4f,-.53f,6.956f);
figure.GetComponentInChildren<SkinnedMeshRenderer>().motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;
EditorSceneManager.MarkSceneDirty(figure.scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(figure.scene);
return "Spectral surface, rock-edge seating and crisp silhouette saved";
