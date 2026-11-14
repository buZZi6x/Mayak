using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
namespace LighthouseGame
{
    [DefaultExecutionOrder(210)]
    public class RainLens:MonoBehaviour
    {
        public Material Template;
        public CinematicEnvironment Environment;
        public float Wetness {get;private set;}
        public bool Sheltered {get;private set;}
        Material material;CustomPassVolume volume;FullScreenCustomPass pass;
        void Awake()
        {
            material=new Material(Template){name="Rain on keeper lens · runtime"};
            volume=gameObject.AddComponent<CustomPassVolume>();volume.isGlobal=true;volume.injectionPoint=CustomPassInjectionPoint.BeforePostProcess;
            pass=new FullScreenCustomPass{name="Rain lenses and sliding trails",fullscreenPassMaterial=material,fetchColorBuffer=true,materialPassName="RainLens"};volume.customPasses.Add(pass);
        }
        void LateUpdate()
        {
            var camera=Environment.Session.Player.View;
            Sheltered=Physics.Raycast(camera.transform.position,Vector3.up,65,~(1<<2),QueryTriggerInteraction.Ignore);
            float target=Sheltered?0:Environment.RainAmount;
            Wetness=Environment.Session.IsTutorialDay?0:Mathf.MoveTowards(Wetness,target,Time.deltaTime*(Sheltered?.55f:.3f));
            material.SetFloat("_Wetness",Wetness);material.SetFloat("_RainTime",Time.time);pass.enabled=Wetness>.001f;
        }
        void OnDestroy(){if(material)Destroy(material);if(volume)Destroy(volume);}
    }
}
