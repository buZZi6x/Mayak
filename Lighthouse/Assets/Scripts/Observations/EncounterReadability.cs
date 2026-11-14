using UnityEngine;
namespace LighthouseGame
{
 [DefaultExecutionOrder(190)]
 public class EncounterReadability:MonoBehaviour
 {
  public Light SeaFill;public Light SeaRim;
  void LateUpdate()
  {
   var env=CinematicEnvironment.Active;if(!env)return;
   // Broad, non-atmospheric bounce keeps photographic subjects legible at night.
   float elevation=Mathf.Sin((env.Hour-6)/24*Mathf.PI*2)*68;
   float night=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(-3,12,elevation));
   var boat=env.Session.Director.Boat;var center=boat.position;center.y=.5f;SeaFill.transform.position=center+new Vector3(14,28,32);SeaRim.transform.position=center+new Vector3(-20,25,-28);
   SeaFill.range=110;SeaRim.range=105;
   SeaFill.intensity=Mathf.Lerp(18000000,42000,night);SeaRim.intensity=Mathf.Lerp(6000000,30000,night);
  }
 }
}
