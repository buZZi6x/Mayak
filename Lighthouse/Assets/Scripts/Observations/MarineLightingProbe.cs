#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace LighthouseGame
{
 public class MarineLightingProbe:MonoBehaviour
 {
  public bool Done;public string Failure;public bool BeaconOnly;readonly List<object> checks=new List<object>();
  Texture2D frame;Color[] offPixels;string output;string captureFolder="Marine";
  void Check(string name,bool passed,object detail=null){checks.Add(new{name,passed,detail});if(!passed)Failure=(Failure??"")+name+"; ";}
  IEnumerator Capture(string name)
  {
   yield return new WaitForSecondsRealtime(.7f);yield return new WaitForEndOfFrame();
   if(frame)Destroy(frame);frame=ScreenCapture.CaptureScreenshotAsTexture();
   string folder=Path.Combine(Application.dataPath,"../Screenshots/"+captureFolder);Directory.CreateDirectory(folder);
   File.WriteAllBytes(Path.Combine(folder,name+".png"),frame.EncodeToPNG());
  }
  IEnumerator Start()
  {
   if(BeaconOnly){yield return CheckBeaconRevision();yield break;}
   var env=CinematicEnvironment.Active;var s=env.Session;var d=s.Director;var e=d.Encounter;var p=s.Player;var beacon=env.GetComponent<LighthouseBeacon>();
   output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Artifacts/MarineOverhaul/lighting-performance.json"));
   s.Running=false;s.GameOver=false;p.enabled=false;p.InputEnabled=false;s.Observations.Automatic=false;s.Observations.Clear();p.View.fieldOfView=72;
   if(!env.CleanView)env.ToggleCleanView();env.AdvanceTime=false;
   e.Preview(false);e.FrozenPreview=true;env.SetWeather(CoastalWeather.Clear,true);env.SetHour(21.5f);s.Storm=false;e.Evaluate(29,true);e.RenderPose();
   p.Teleport(s.Observation.position,d.Boat.position+Vector3.up*8);yield return new WaitForSecondsRealtime(3);
   Time.timeScale=0;d.enabled=false;beacon.enabled=false;var savedRotation=d.BeamLight.transform.rotation;
   d.BeamLight.transform.rotation=Quaternion.LookRotation(d.Boat.position+Vector3.up*10-d.BeamLight.transform.position);
   d.LampLight.enabled=false;d.BeamLight.enabled=false;if(d.BeamMesh)d.BeamMesh.enabled=false;beacon.BroadBeam.enabled=false;beacon.Shaft.enabled=false;
   yield return Capture("V03-Beacon-OFF");offPixels=frame.GetPixels();
   d.LampLight.enabled=true;d.BeamLight.enabled=true;if(d.BeamMesh)d.BeamMesh.enabled=true;beacon.BroadBeam.enabled=true;if(beacon.Shaft)beacon.Shaft.enabled=false;
   yield return Capture("V03-Beacon-ON");var on=frame.GetPixels();double sum=0;int brighter=0;
   for(int i=0;i<on.Length;i++){float delta=on[i].grayscale-offPixels[i].grayscale;sum+=delta;if(delta>.04f)brighter++;}
   Check("Beacon visibly illuminates the FPS view",sum/on.Length>.002&&brighter>on.Length*.01,new{meanIncrease=sum/on.Length,litPixels=brighter});
   Check("Beacon reaches fleet range",d.BeamLight.range>=1000&&d.BeamLight.intensity>=600000&&d.BeamLight.spotAngle>=50&&(!beacon.Shaft||!beacon.Shaft.enabled));
   var atShip=d.BeamLight.transform.rotation;
   d.BeamLight.transform.rotation=Quaternion.AngleAxis(26,Vector3.up)*atShip;yield return Capture("V03-Beacon-side");
   d.BeamLight.transform.rotation=Quaternion.AngleAxis(-14,Vector3.up)*atShip;yield return Capture("V03-Beacon-through-water");
   d.BeamLight.transform.rotation=savedRotation;Time.timeScale=1;d.enabled=true;beacon.enabled=true;e.StopPreview();
   // Final clear-night and three-quarter silhouettes, stable exposure, actual FPS camera.
   foreach(int kind in new[]{0,4,5,6,7})
   {
    s.Observations.Clear();var v=s.Observations.Spawn(kind,true);v.Frozen=true;v.Evaluate(8);p.Teleport(s.Observation.position,v.Focus);
    yield return new WaitForSecondsRealtime(1);yield return Capture("V03-Event-"+kind);Check("Final night photo "+kind,v.VisibleIn(p.View));
   }
   s.Observations.Clear();s.Observations.SpawnGhost(true);s.Observations.Ghost.Frozen=true;s.Observations.Ghost.Evaluate(15);p.Teleport(s.Observation.position,s.Observations.Ghost.Focus);yield return Capture("V03-Ghost");
   // No recording or readback inside this measured rendering interval.
   s.Observations.Clear();e.Preview(true);env.SetHour(19.2f);e.Evaluate(30,true);
   var first=s.Observations.Spawn(2,true);var second=s.Observations.Spawn(3,true);p.Teleport(s.Observation.position,d.Boat.position+Vector3.up*10);
   yield return new WaitForSecondsRealtime(2);var frames=new List<float>();double begin=Time.realtimeSinceStartupAsDouble;double previous=begin;
   while(Time.realtimeSinceStartupAsDouble-begin<12){yield return null;double now=Time.realtimeSinceStartupAsDouble;frames.Add((float)((now-previous)*1000));previous=now;}
   frames.Sort();var timing=new{frames=frames.Count,meanMs=frames.Average(),p95Ms=frames[(int)(frames.Count*.95f)],maximumMs=frames[frames.Count-1],width=Screen.width,height=Screen.height,gpu=SystemInfo.graphicsDeviceName,context="Unity Editor, DX11, four traffic ships + attack + waterspout + maelstrom, no recording"};
   Check("Performance sample collected",frames.Count>60,timing);
   e.StopPreview();s.Observations.Clear();d.FreezePreview=true;p.enabled=true;p.InputEnabled=true;p.LockCursor(false);Done=true;
   File.WriteAllText(output,Newtonsoft.Json.JsonConvert.SerializeObject(new{Done,Failure,checks},Newtonsoft.Json.Formatting.Indented));
  }

  IEnumerator CheckBeaconRevision()
  {
   captureFolder="Beacon";output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Artifacts/BeaconRevision/verification.json"));Directory.CreateDirectory(Path.GetDirectoryName(output));
   var env=CinematicEnvironment.Active;var s=env.Session;var d=s.Director;var p=s.Player;var b=env.GetComponent<LighthouseBeacon>();var beam=d.BeamLight;var spin=beam.GetComponentInParent<BlockoutMotion>();
   Time.timeScale=1;s.Running=false;s.GameOver=false;s.Observations.Automatic=false;s.Observations.Clear();p.enabled=false;p.InputEnabled=false;p.View.fieldOfView=72;
   if(!env.CleanView)env.ToggleCleanView();env.AdvanceTime=false;d.Traffic.Frozen=true;
   d.Encounter.Preview(false);d.Encounter.FrozenPreview=true;d.Encounter.Evaluate(29,true);d.Encounter.RenderPose();env.SetWeather(CoastalWeather.Clear,true);env.SetHour(21.5f);
   p.Teleport(s.Observation.position,d.Boat.position+Vector3.up*8);spin.enabled=false;
   var aimed=Quaternion.LookRotation(d.Boat.position+Vector3.up*8-beam.transform.position);beam.transform.rotation=aimed;
   // Let HDRP's realtime sky and exposure settle before freezing the comparison.
   yield return new WaitForSecondsRealtime(3);Time.timeScale=0;d.enabled=false;b.enabled=false;
   d.LampLight.enabled=false;beam.enabled=false;b.BroadBeam.enabled=false;
   yield return Capture("Final-clear-OFF");offPixels=frame.GetPixels();
   d.LampLight.enabled=true;beam.enabled=true;b.BroadBeam.enabled=true;
   yield return Capture("Final-clear-ON");var on=frame.GetPixels();double sum=0;int brighter=0;
   for(int i=0;i<on.Length;i++){float delta=on[i].grayscale-offPixels[i].grayscale;sum+=delta;if(delta>.025f)brighter++;}
   Check("Native light visibly illuminates water and ships",sum/on.Length>.001&&brighter>on.Length*.005,new{meanIncrease=sum/on.Length,litPixels=brighter});
   beam.transform.rotation=Quaternion.AngleAxis(22,Vector3.up)*aimed;yield return Capture("Final-clear-wide");
   var wide=frame.GetPixels();Vector3 center=p.View.WorldToViewportPoint(d.Boat.position+Vector3.up*12);double wideGain=0;int samples=0;
   for(int y=Mathf.Max(0,(int)(center.y*frame.height)-45);y<Mathf.Min(frame.height,(int)(center.y*frame.height)+45);y++)
   for(int x=Mathf.Max(0,(int)(center.x*frame.width)-80);x<Mathf.Min(frame.width,(int)(center.x*frame.width)+80);x++){int i=y*frame.width+x;wideGain+=wide[i].grayscale-offPixels[i].grayscale;samples++;}
   Check("Ship remains lit 22 degrees away from beam axis",samples>0&&wideGain/samples>.006,new{meanIncrease=wideGain/Mathf.Max(1,samples),samples});
   Check("No synthetic cone renderer",!b.Shaft||!b.Shaft.enabled);
   Check("Native HDRP shadows and atmospheric scattering",beam.shadows==LightShadows.Soft&&beam.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>().affectsVolumetric&&b.BroadBeam.shadows==LightShadows.Soft);
   b.enabled=true;d.LampLight.enabled=false;yield return null;yield return null;
   Check("Lamp failure disables both light layers",!beam.enabled&&!b.BroadBeam.enabled&&(!b.Shaft||!b.Shaft.enabled));
   d.LampLight.enabled=true;yield return null;yield return null;Check("Lamp recovery restores both light layers",beam.enabled&&b.BroadBeam.enabled);
   d.enabled=true;Time.timeScale=1;spin.enabled=true;var initial=beam.transform.rotation;float begin=Time.time;
   yield return new WaitForSeconds(5);float elapsed=Time.time-begin;float degrees=Quaternion.Angle(initial,beam.transform.rotation);float period=360*elapsed/degrees;
   Check("Actual rotation takes 90 seconds per revolution",Mathf.Abs(period-90)<1,new{elapsed,degrees,period});
   spin.enabled=false;beam.transform.rotation=Quaternion.AngleAxis(22,Vector3.up)*aimed;
   foreach(var weather in new[]{CoastalWeather.Rain,CoastalWeather.Storm})
   {
    env.SetWeather(weather,true);env.SetHour(21.5f);yield return new WaitForSecondsRealtime(2.5f);yield return Capture("Final-"+weather.ToString().ToLower()+"-wide");
    Check("Light stays active in "+weather,beam.enabled&&b.BroadBeam.enabled&&(!b.Shaft||!b.Shaft.enabled));
   }
   var frames=new List<float>();double start=Time.realtimeSinceStartupAsDouble,previous=start;
   while(Time.realtimeSinceStartupAsDouble-start<8){yield return null;double now=Time.realtimeSinceStartupAsDouble;frames.Add((float)((now-previous)*1000));previous=now;}
   frames.Sort();Check("Rendering sample with native shadows collected",frames.Count>60,new{frames=frames.Count,meanMs=frames.Average(),p95Ms=frames[(int)(frames.Count*.95f)],maxMs=frames[frames.Count-1],width=Screen.width,height=Screen.height,gpu=SystemInfo.graphicsDeviceName});
   spin.enabled=true;d.Traffic.Frozen=false;d.Encounter.StopPreview();d.FreezePreview=true;p.enabled=true;p.InputEnabled=true;p.LockCursor(false);Done=true;
   File.WriteAllText(output,Newtonsoft.Json.JsonConvert.SerializeObject(new{Done,Failure,checks},Newtonsoft.Json.Formatting.Indented));
  }
  void OnDestroy(){Time.timeScale=1;if(frame)Destroy(frame);}
 }
}
#endif
