using System.Collections;
using UnityEngine;
namespace LighthouseGame
{
    public class CoastalAmbience:MonoBehaviour
    {
        public AudioClip WaterBed, WindLoop, RainLight, RainHeavy, ThunderClip;
        public AudioClip[] SurfClips;
        [Range(0,1)] public float Master=.75f;
        public float UserVolume { get; set; } = 1;
        public float MenuMultiplier { get; set; } = 1;
        public float EffectsVolume { get; set; } = 1;
        public float AmbientGain => Master * UserVolume * MenuMultiplier;
        public float EffectsGain => Master * EffectsVolume;
        public bool Indoors;
        public float WaterLevel, WindLevel, RainLevel;
        AudioSource water,wind,rain,heavy,thunder;
        AudioSource[] surf;
        AudioLowPassFilter[] filters;
        float nextWave, windKph, rainAmount, waterMix, windMix, rainMix, thunderMix;
        float[] surfMix;
        AudioSource Source(string label,AudioClip clip,bool loop)
        {
            var go=new GameObject(label);go.transform.SetParent(transform,false);
            var source=go.AddComponent<AudioSource>();source.clip=clip;source.loop=loop;source.playOnAwake=false;source.volume=0;source.priority=100;source.spatialBlend=0;
            var filter=go.AddComponent<AudioLowPassFilter>();filter.cutoffFrequency=20000;filter.lowpassResonanceQ=1;
            if(loop&&clip){source.time=Random.Range(0,clip.length*.8f);source.Play();}
            return source;
        }
        void Awake()
        {
            water=Source("Offshore water · stereo",WaterBed,true);wind=Source("Wind through gallery",WindLoop,true);
            rain=Source("Fine rain",RainLight,true);heavy=Source("Rain on exposed stone",RainHeavy,true);thunder=Source("Distant thunder",ThunderClip,false);
            surf=new AudioSource[6];
            surfMix=new float[surf.Length];
            for(int i=0;i<surf.Length;i++)
            {
                surf[i]=Source("Surf at rock "+i,null,false);surf[i].spatialBlend=1;surf[i].minDistance=11;surf[i].maxDistance=100;
                float angle=i*Mathf.PI*2/surf.Length;surf[i].transform.position=new Vector3(Mathf.Cos(angle)*18,1,Mathf.Sin(angle)*18);
            }
            filters=GetComponentsInChildren<AudioLowPassFilter>(); nextWave=Time.time+.5f;
        }
        public void SetMix(float windValue,float rainValue,bool indoors,float hour)
        {windKph=windValue;rainAmount=rainValue;Indoors=indoors;}
        void Update()
        {
            float storm=Mathf.InverseLerp(10,65,windKph), shelter=Indoors?.42f:1;
            float t=1-Mathf.Exp(-Time.deltaTime*1.8f);
            // Weather fades smoothly; the user's volume applies immediately, including active waves.
            waterMix=Mathf.Lerp(waterMix,Mathf.Lerp(.21f,.32f,storm)*shelter,t);
            windMix=Mathf.Lerp(windMix,Mathf.Lerp(.07f,.42f,storm)*shelter,t);
            rainMix=Mathf.Lerp(rainMix,rainAmount*shelter,t);
            WaterLevel=waterMix*AmbientGain;WindLevel=windMix*AmbientGain;RainLevel=rainMix*AmbientGain;
            water.volume=WaterLevel;wind.volume=WindLevel;rain.volume=RainLevel*.35f;heavy.volume=RainLevel*storm*.4f;
            thunder.volume=thunderMix*AmbientGain;
            for(int i=0;i<surf.Length;i++)surf[i].volume=surfMix[i]*AmbientGain;
            foreach(var f in filters)f.cutoffFrequency=Mathf.Lerp(f.cutoffFrequency,Indoors?2600:19000,t);
            if(Time.time>nextWave&&SurfClips!=null&&SurfClips.Length>0)
            {
                int index=Random.Range(0,surf.Length);var source=surf[index];
                if(!source.isPlaying){source.clip=SurfClips[Random.Range(0,SurfClips.Length)];surfMix[index]=Mathf.Lerp(.27f,.56f,storm)*shelter;source.volume=AmbientGain*surfMix[index];source.pitch=Random.Range(.92f,1.05f);source.Play();}
                nextWave=Time.time+Random.Range(1.8f,4.2f);
            }
        }
        public void Thunder(float delay) { if(ThunderClip) StartCoroutine(PlayThunder(delay)); }
        public void ClearStorm()
        {
            StopAllCoroutines();
            rainAmount = RainLevel = rainMix = 0;
            if (rain) rain.volume = 0;
            if (heavy) heavy.volume = 0;
            if (thunder) thunder.Stop();
        }
        IEnumerator PlayThunder(float delay)
        {
            yield return new WaitForSeconds(delay);thunderMix=Indoors?.44f:.7f;thunder.volume=AmbientGain*thunderMix;thunder.panStereo=Random.Range(-.45f,.45f);thunder.pitch=Random.Range(.84f,1.02f);thunder.PlayOneShot(ThunderClip);
        }
    }
}
