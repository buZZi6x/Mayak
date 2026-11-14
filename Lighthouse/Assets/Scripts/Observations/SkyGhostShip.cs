using UnityEngine;
namespace LighthouseGame
{
 public class SkyGhostShip:MonoBehaviour
 {
  public Transform Vessel;public Renderer[] Surfaces;public LineRenderer[] Wisps;
  public bool Showing {get;private set;}public bool Frozen;public float Age {get;private set;}public float Opacity {get;private set;}
  public const int Points=240;public const float Lifetime=34;
  Vector3 anchor;Quaternion basis;float started;int heldFrame=-1;
  MaterialPropertyBlock properties;Vector3[] line=new Vector3[36];
  void Awake(){properties=new MaterialPropertyBlock();Hide();}
  public Vector3 Focus=>Vessel.position+Vector3.up*7;
  public void Show(Vector3 position,Quaternion rotation){anchor=position;basis=rotation;started=Time.time;Frozen=false;Showing=true;Vessel.gameObject.SetActive(true);Evaluate(0);}
  public void Hide(){Showing=false;if(Vessel)Vessel.gameObject.SetActive(false);if(Wisps!=null)foreach(var w in Wisps)w.enabled=false;}
  public void HoldForPhoto(){heldFrame=Time.frameCount;}
  void Update(){if(!Showing||Frozen||heldFrame==Time.frameCount)return;float age=Time.time-started;if(age>=Lifetime){Hide();return;}Evaluate(age);}
  public void Evaluate(float age)
  {
   Age=age;float u=age/Lifetime;Opacity=Mathf.SmoothStep(0,1,Mathf.Clamp01(age/3))*Mathf.SmoothStep(0,1,Mathf.Clamp01((Lifetime-age)/4));
   Vector3 p=anchor+basis*new Vector3(Mathf.Lerp(-46,46,u),48+Mathf.Sin(u*Mathf.PI)*9,Mathf.Sin(u*Mathf.PI*2)*10);
   Vessel.SetPositionAndRotation(p,basis*Quaternion.Euler(Mathf.Sin(age*.4f)*2,-90+Mathf.Cos(u*Mathf.PI*2)*12,Mathf.Sin(age*.32f)*3));
   properties.SetFloat("_Opacity",Opacity);foreach(var r in Surfaces)if(r)r.SetPropertyBlock(properties);
   for(int k=0;k<Wisps.Length;k++)
   {
    var w=Wisps[k];w.enabled=Showing;w.startColor=new Color(.14f,.67f,.69f,Opacity*.22f);w.endColor=new Color(.12f,.34f,.54f,0);
    for(int i=0;i<line.Length;i++){float v=i/(float)(line.Length-1);line[i]=Vessel.TransformPoint(new Vector3((k-1)*1.8f+Mathf.Sin(v*10+age*.7f+k)*v*2,-.7f-v*3,8+v*40));}w.SetPositions(line);
   }
  }
  public bool VisibleIn(Camera camera)=>Showing&&Opacity>.2f&&PhotoEvidence.Vessel(camera,Vessel);
 }
}
