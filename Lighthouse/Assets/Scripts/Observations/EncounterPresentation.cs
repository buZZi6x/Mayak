using UnityEngine;
using UnityEngine.Rendering;
namespace LighthouseGame
{
    public class EncounterPresentation:MonoBehaviour
    {
        public AudioClip[] Clips;
        public Material SprayMaterial;
        public int AudioCues {get;private set;}
        public int SurfaceBursts {get;private set;}
        public ParticleSystem Spray {get;private set;}
        public float MeterRms {get;private set;}
        AudioSource[] voices;int voice;float last=-100,drip;
        float[] times={18.1f,29,32.5f,35.0f,36.8f,38.45f,40.1f,42,44.1f,48,53};
        int[] sounds={7,0,4,5,1,2,1,3,4,6,7};
        void Awake()
        {
            voices=new AudioSource[8];
            for(int i=0;i<voices.Length;i++)
            {
                var go=new GameObject("Sea event voice "+i);go.transform.SetParent(transform,false);var a=go.AddComponent<AudioSource>();voices[i]=a;
                a.playOnAwake=false;a.spatialBlend=1;a.rolloffMode=AudioRolloffMode.Custom;a.minDistance=22;a.maxDistance=650;
                a.SetCustomCurve(AudioSourceCurveType.CustomRolloff,new AnimationCurve(new Keyframe(0,1),new Keyframe(.15f,.8f),new Keyframe(.4f,.32f),new Keyframe(1,0)));
                a.dopplerLevel=0;a.spread=24;a.reverbZoneMix=.2f;
            }
            var particles=new GameObject("Sea impact spray · pooled world particles");particles.transform.SetParent(transform,false);particles.layer=2;
            Spray=particles.AddComponent<ParticleSystem>();Spray.Stop();var m=Spray.main;m.loop=false;m.playOnAwake=false;m.maxParticles=1700;m.startLifetime=new ParticleSystem.MinMaxCurve(1.3f,3.1f);m.startSize=new ParticleSystem.MinMaxCurve(.4f,1.5f);m.simulationSpace=ParticleSystemSimulationSpace.World;m.gravityModifier=.75f;m.startColor=new Color(.62f,.72f,.74f,.36f);
            var e=Spray.emission;e.enabled=false;var shape=Spray.shape;shape.enabled=false;
            var color=Spray.colorOverLifetime;color.enabled=true;var g=new Gradient();g.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.08f),new GradientAlphaKey(.45f,.45f),new GradientAlphaKey(0,1)});color.color=g;
            var size=Spray.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.35f,1,2.2f));
            var r=Spray.GetComponent<ParticleSystemRenderer>();r.sharedMaterial=SprayMaterial;r.shadowCastingMode=ShadowCastingMode.Off;r.renderMode=ParticleSystemRenderMode.Billboard;
        }
        public void ResetTimeline(float time=-100)
        {last=time;drip=0;if(voices!=null)foreach(var a in voices)a.Stop();if(Spray)Spray.Clear();}
        public void Evaluate(float clock,Vector3 center,Quaternion heading,bool attack,bool colossal)
        {
            if(clock<last)ResetTimeline(clock);
            if(clock-last<1)
            {
                for(int i=0;i<times.Length;i++)if(last<times[i]&&clock>=times[i]&&(attack||i==0))
                {
                    if(!colossal&&i>=4&&i<=7)continue;
                    var p=center+heading*new Vector3(i%2==0?-6:6,2,(i%3-1)*11)*(colossal?1:.4f);
                    if(Clips!=null&&Clips.Length>sounds[i]&&Clips[sounds[i]])
                    {
                        var a=voices[voice++%voices.Length];a.transform.position=p;a.clip=Clips[sounds[i]];a.pitch=colossal?Random.Range(.91f,1.02f):Random.Range(1.15f,1.3f);
                        var ambience=CinematicEnvironment.Active?CinematicEnvironment.Active.Ambience:null;
                        a.volume=(colossal?.82f:.55f)*(ambience?ambience.EffectsVolume:1);a.Play();AudioCues++;
                    }
                    Burst(p,colossal?95:40,colossal?12:6);SurfaceBursts++;
                }
            }
            if(clock>20&&clock<60)
            {
                drip+=Time.deltaTime;
                if(drip>.09f){drip=0;float a=clock*2.3f;var p=center+new Vector3(Mathf.Cos(a)*14,2,Mathf.Sin(a)*12);Burst(p,3,3);}
            }
            last=clock;
        }
        public void Burst(Vector3 p,int count,float speed)
        {
            if(!Spray)return;
            for(int i=0;i<count;i++)
            {
                var direction=Random.onUnitSphere;direction.y=Mathf.Abs(direction.y)*1.4f;
                var e=new ParticleSystem.EmitParams{position=p+Random.insideUnitSphere*1.6f,velocity=direction*Random.Range(speed*.3f,speed),startSize=Random.Range(.4f,1.5f)};Spray.Emit(e,1);
            }
        }
        void OnDisable(){ResetTimeline();}
    }
}
