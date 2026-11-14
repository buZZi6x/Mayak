using UnityEngine;
namespace LighthouseGame
{
    // Practical fixtures plus a soft bounced-light approximation keep the workstations legible.
    [DefaultExecutionOrder(100)]
    public class KeeperLighting : MonoBehaviour
    {
        public CinematicEnvironment Environment;
        public Light RoomFill, DeskLight, DoorLight;
        Material flame;
        void Start()
        {
            var renderer=Environment.Session.Director.FlameMesh;
            if(renderer){flame=renderer.material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;}
        }
        void LateUpdate()
        {
            if(!Environment)return;
            float day=Mathf.Clamp01(Environment.Sun.intensity/85000f);
            if(RoomFill)RoomFill.intensity=Mathf.Lerp(180,60000,day);
            if(DeskLight)DeskLight.intensity=Mathf.Lerp(65,8000,day);
            if(DoorLight)DoorLight.intensity=Mathf.Lerp(30,4800,day);
            if(flame)flame.SetColor("_EmissiveColor",new Color(1,.62f,.22f)*Mathf.Lerp(22,2200,day));
        }
        void OnDestroy(){if(flame)Destroy(flame);}
    }
}
