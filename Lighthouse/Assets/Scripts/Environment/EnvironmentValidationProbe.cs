#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace LighthouseGame
{
    // Editor-only integration probe; never included in the player build or saved scene.
    public class EnvironmentValidationProbe : MonoBehaviour
    {
        public bool Finished, Passed;
        public string Result;
        readonly List<string> checks=new List<string>();
        void Check(bool value,string name){checks.Add((value?"PASS ":"FAIL ")+name);Debug.Log("ENVIRONMENT_QA "+checks.Last());}
        [Serializable] class Evidence
        {
            public bool Passed;
            public string[] Checks;
            public string GPU, Resolution;
            public int Frames;
            public float MedianMs,P95Ms,MaxAudioPeak,MaxAudioRms,MinWaterHeight,MaxWaterHeight,MaxRoll;
        }
        IEnumerator Start()
        {
            var e=CinematicEnvironment.Active;var s=e.Session;var p=s.Player;
            e.AdvanceTime=false;e.enabled=true;s.Running=false;p.InputEnabled=false;p.enabled=false;
            p.Teleport(new Vector3(-3,10,-6),new Vector3(-12,5,-150));
            e.SetWeather(CoastalWeather.Storm,true);e.SetHour(13);s.Director.BeginDay(true);
            yield return new WaitForSeconds(8);
            var boat=s.Director.Boat.GetComponent<OceanBuoyancy>();var meter=p.View.GetComponent<EnvironmentAudioMeter>();
            var times=new List<float>();float end=Time.time+7,peak=0,rms=0,minH=1000,maxH=-1000,roll=0;
            while(Time.time<end)
            {
                times.Add(Time.unscaledDeltaTime*1000);peak=Mathf.Max(peak,meter.Peak);rms=Mathf.Max(rms,meter.Rms);
                if(boat.LastQuerySucceeded){minH=Mathf.Min(minH,boat.LastWaterHeight);maxH=Mathf.Max(maxH,boat.LastWaterHeight);roll=Mathf.Max(roll,Mathf.Abs(boat.SampledRoll));}
                yield return null;
            }
            Check(boat.LastQuerySucceeded&&maxH-minH>.08f&&roll>.2f,"Ship follows sampled GPU ocean height and slope over time");
            Check(e.RainParticles.particleCount>200&&!e.Ambience.Indoors,"Storm emits rain outdoors");
            Check(peak>.001f&&peak<.99f&&rms>.0001f,"Actual AudioListener mix is audible and does not clip during the measured storm");
            Check(e.Ambience.GetComponentsInChildren<AudioSource>().Count(a=>a.loop&&a.isPlaying&&a.clip)==4,"All four streaming ambience loops are playing");
            yield return new WaitForEndOfFrame();
            var foam=e.Ocean.GetFoamBuffer(out _);float foamMax=0;
            if(foam)
            {
                var rt=RenderTexture.GetTemporary(256,256,0,RenderTextureFormat.ARGB32);Graphics.Blit(foam,rt);
                var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(256,256,TextureFormat.RGBA32,false,true);
                tex.ReadPixels(new Rect(0,0,256,256),0,0);tex.Apply();foamMax=tex.GetPixels().Max(c=>c.r);
                RenderTexture.active=old;Destroy(tex);RenderTexture.ReleaseTemporary(rt);
            }
            Check(foam&&foamMax>.01f,"Water decals produce nonempty GPU foam texture (shore and ship wake)");
            p.Teleport(new Vector3(0,9.95f,0),new Vector3(0,11,-20));
            yield return new WaitForSeconds(2);
            Check(e.Ambience.Indoors&&e.RainParticles.particleCount==0,"Roof stops rain and clears particles carried in from outdoors");
            Check(e.Ambience.GetComponentsInChildren<AudioLowPassFilter>().All(f=>f.cutoffFrequency<3500),"Interior ambience is filtered through the shelter mix");
            int before=e.LightningCount;e.TriggerLightning();yield return new WaitForSeconds(4);
            Check(e.LightningCount==before+1&&e.Ambience.GetComponentsInChildren<AudioSource>().Any(a=>a.name=="Distant thunder"&&a.isPlaying),"Lightning triggers delayed thunder playback");
            p.Teleport(new Vector3(-3,10,-6),new Vector3(-12,5,-150));e.SetWeather(CoastalWeather.Clear);
            float windBefore=e.Wind;yield return new WaitForSeconds(.5f);
            Check(e.Wind<windBefore&&e.Wind>e.Presets[0].WindKph+1,"Weather changes interpolate instead of snapping wave energy");
            e.SetWeather(CoastalWeather.Clear,true);e.SetHour(0);yield return new WaitForSeconds(2);
            Check(e.Moon.shadows!=LightShadows.None&&e.Sun.shadows==LightShadows.None&&e.Moon.intensity>0,"Night switches the atmosphere and the single shadow atlas to moonlight");
            e.SetHour(13);yield return null;
            Check(e.Sun.shadows!=LightShadows.None&&e.Moon.shadows==LightShadows.None,"Day switches the single shadow atlas back to sunlight");
            times.Sort();Passed=checks.All(c=>c.StartsWith("PASS"));Finished=true;Result=string.Join("\n",checks);
            var evidence=new Evidence{Passed=Passed,Checks=checks.ToArray(),GPU=SystemInfo.graphicsDeviceName,Resolution=Screen.width+"x"+Screen.height,Frames=times.Count,MedianMs=times[times.Count/2],P95Ms=times[(int)(times.Count*.95f)],MaxAudioPeak=peak,MaxAudioRms=rms,MinWaterHeight=minH,MaxWaterHeight=maxH,MaxRoll=roll};
            Directory.CreateDirectory("../Artifacts/Environment");File.WriteAllText("../Artifacts/Environment/integration-verification.json",JsonUtility.ToJson(evidence,true));
            e.SetWeather(CoastalWeather.Clear,true);e.SetHour(17.2f);p.enabled=true;p.InputEnabled=true;
            Debug.Log("ENVIRONMENT_QA_COMPLETE "+Passed);
        }
    }
}
#endif
