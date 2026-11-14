using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
namespace LighthouseGame
{
    [DefaultExecutionOrder(120)]
    public class OceanBuoyancy:MonoBehaviour
    {
        public WaterSurface Ocean;
        public float Length=6, Width=2.5f;
        [Range(0,1)] public float Influence=1;
        public bool LastQuerySucceeded;
        public float LastWaterHeight, SampledPitch, SampledRoll;
        WaterSearchParameters parameters;
        float smoothedHeight;
        Quaternion tilt=Quaternion.identity;
        Vector3 lastOutputPosition, basePosition;
        Quaternion lastOutputRotation, baseRotation;
        bool applied;
        void OnEnable(){applied=false;}
        float Sample(Vector3 p)
        {
            parameters.targetPositionWS=p;parameters.startPositionWS=p;parameters.error=.04f;parameters.maxIterations=8;parameters.includeDeformation=true;
            WaterSearchResult result=default;
            LastQuerySucceeded=Ocean&&Ocean.ProjectPointOnWaterSurface(parameters,out result);
            if(LastQuerySucceeded)return result.projectedPositionWS.y;
            return LastWaterHeight;
        }
        void LateUpdate()
        {
            if(!Ocean) return;
            // Movement may be paused for observation. Never add the previous wave
            // offset again when no director supplied a fresh base pose this frame.
            Vector3 p=transform.position;
            if(applied&&(p-lastOutputPosition).sqrMagnitude<.000001f)p=basePosition;
            Quaternion rotation=transform.rotation;
            if(applied&&Quaternion.Angle(rotation,lastOutputRotation)<.001f)rotation=baseRotation;
            basePosition=p;baseRotation=rotation;
            float h=Sample(p);LastWaterHeight=h;
            float front=Sample(p+transform.forward*Length*.5f),back=Sample(p-transform.forward*Length*.5f);
            float right=Sample(p+transform.right*Width*.5f),left=Sample(p-transform.right*Width*.5f);
            SampledPitch=-Mathf.Atan2(front-back,Length)*Mathf.Rad2Deg;SampledRoll=Mathf.Atan2(right-left,Width)*Mathf.Rad2Deg;
            float t=1-Mathf.Exp(-Time.deltaTime*3.5f);smoothedHeight=Mathf.Lerp(smoothedHeight,h,t);
            tilt=Quaternion.Slerp(tilt,Quaternion.Euler(SampledPitch,0,SampledRoll),t);
            lastOutputPosition=p+Vector3.up*smoothedHeight*Influence;lastOutputRotation=rotation*Quaternion.Slerp(Quaternion.identity,tilt,Influence);
            transform.SetPositionAndRotation(lastOutputPosition,lastOutputRotation);applied=true;
        }
    }
}
