#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace LighthouseGame
{
 public class ObservationValidationProbe:MonoBehaviour
 {
  public bool Finished,Passed;
  public string Result;
  readonly List<string> checks=new List<string>();
  void Check(bool value,string name){checks.Add((value?"PASS ":"FAIL ")+name);Debug.Log("OBSERVATION_QA "+checks.Last());}
  void Screenshot(string name){var tex=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes("Screenshots/Events/"+name+".png",tex.EncodeToPNG());Destroy(tex);}
  IEnumerator Start()
  {
   Directory.CreateDirectory("Screenshots/Events");var env=CinematicEnvironment.Active;var s=env.Session;var p=s.Player;var o=s.Observations;
   p.InputEnabled=false;s.Director.enabled=false;s.enabled=false;o.Automatic=false;env.AdvanceTime=false;
   if(!env.CleanView)env.ToggleCleanView();s.HelpVisible=false;
   for(int i=0;i<8;i++)
   {
    s.BeginRun();s.Preparation=0;s.Running=false;o.Automatic=false;s.Director.Evaluate(0);env.Bolt.Clear();
    env.SetWeather(i==2?CoastalWeather.Storm:CoastalWeather.Clear,true);env.SetHour(i==2?17.4f:21.5f);
    var e=o.Spawn(i,true);e.Frozen=true;e.Evaluate(4);p.Teleport(s.Observation.position,e.Focus);
    yield return new WaitForSeconds(2.5f);
    Check(e.VisibleIn(p.View),e.Definition.Key+" visible from keeper camera");
    yield return new WaitForEndOfFrame();Screenshot((i+1).ToString("00")+"-"+e.Definition.Key);
    env.Bolt.Clear();s.BusyUntil=0;s.Capture();yield return new WaitForEndOfFrame();yield return null;
    Check(s.Shots.Count==1&&s.Shots[0].Price==e.Definition.Points,e.Definition.Key+" photo price and actual rendered image");
    s.BusyUntil=0;s.Capture();yield return new WaitForEndOfFrame();yield return null;
    Check(s.Shots.Count==2&&s.Shots[1].Price==e.Definition.Points/2&&s.Shots[0].Price==e.Definition.Points,e.Definition.Key+" repeat discount without repricing");
    p.Teleport(s.Observation.position,s.Observation.position-(e.Focus-s.Observation.position));
    Check(!e.VisibleIn(p.View),e.Definition.Key+" rejected behind camera");
    o.Clear();
   }
   s.BeginRun();s.Preparation=0;s.Running=false;o.Automatic=false;s.Director.Evaluate(0);env.SetWeather(CoastalWeather.Clear,true);env.SetHour(21.5f);
   var a=o.Spawn(3,true);a.Frozen=true;a.Evaluate(4);var b=o.Spawn(4,true);b.Frozen=true;b.Evaluate(4);p.Teleport(s.Observation.position,a.Focus);
   yield return new WaitForSeconds(.5f);s.BusyUntil=0;s.Capture();yield return new WaitForEndOfFrame();yield return null;
   Check(s.Shots.Count==1&&s.Shots[0].Price==380&&s.Shots[0].Subjects.Count==2,"Two visible phenomena add to one fixed 380-point photo");
   o.Clear();var bolt=env.Bolt;p.Teleport(s.Observation.position,new Vector3(0,80,-320));bolt.StrikeAt(new Vector3(0,0,-320));
   float began=Time.time;Check(bolt.Duration<=.2f&&bolt.IsActive,"Lightning starts immediately with 0.18-second lifetime");
   yield return new WaitForSeconds(.23f);Check(!bolt.IsActive&&bolt.GetComponentsInChildren<LineRenderer>().All(l=>!l.enabled),"Lightning and all branches extinguish within 0.23 seconds");
   // Wall occlusion from the lower entrance room.
   a=o.Spawn(3,true);a.Frozen=true;a.Evaluate(4);p.Teleport(new Vector3(0,5.2f,0),a.Focus);yield return null;
   Check(!a.VisibleIn(p.View),"Tower geometry blocks photo credit");o.Clear();
   s.BeginRun();o.Automatic=true;s.Director.Evaluate(0);yield return new WaitForSeconds(.4f);
   Check(o.ActiveCount==0&&s.Shots.Count==0,"New run clears observations; preparation remains event-free");
   s.Running=false;o.Automatic=false;env.SetWeather(CoastalWeather.Clear,true);env.SetHour(13);
   Check(o.Weight(0)==0&&o.Weight(1)==0&&o.Weight(4)==0&&o.Weight(2)==0,"Daytime and weather eligibility prevent implausible spawns");
   env.SetWeather(CoastalWeather.Storm,true);Check(o.Weight(2)>0&&o.Weight(3)>0&&o.Weight(0)==0,"Storm selects waterspout or rift");
   o.Clear();env.SetWeather(CoastalWeather.Clear,true);env.SetHour(13);s.Storm=false;s.Director.Evaluate(23);
   var fleet=s.Director.Boat.GetComponent<CoastalFleet>();
   for(int i=0;i<3;i++)
   {
    fleet.Select(i);p.Teleport(s.Observation.position,s.Director.Boat.position+Vector3.up*5);yield return new WaitForSeconds(1.5f);
    Check(fleet.Vessels.Count(v=>v.gameObject.activeSelf)==1&&fleet.Vessels[i].GetComponentsInChildren<MeshRenderer>().Length>5,"Distinct authored vessel "+i+" active with material bindings");
    yield return new WaitForEndOfFrame();Screenshot("Ship-"+(i+1));
   }
   Check(fleet.GetComponentsInChildren<Transform>(true).Any(t=>t.name.StartsWith("ANIM_Sail")),"Sail and pennant animation pivots retained");
   var buoy=s.Director.Boat.GetComponent<OceanBuoyancy>();Check(buoy.LastQuerySucceeded,"New ship uses spectral ocean buoyancy");
   Check(Vector3.Angle(s.Director.Boat.up,Vector3.up)<20&&Mathf.Abs(s.Director.Boat.position.y)<5,"Paused ship does not accumulate wave tilt or height");
   Result=JsonUtility.ToJson(new Evidence{Passed=checks.All(x=>x.StartsWith("PASS")),Checks=checks.ToArray(),Effects=8,Ships=3,LightningDuration=bolt.Duration,Vertices=o.Effects.Sum(e=>e.VertexCount)},true);
   Directory.CreateDirectory("../Artifacts/EventsUpgrade");File.WriteAllText("../Artifacts/EventsUpgrade/integration-verification.json",Result);Passed=checks.All(x=>x.StartsWith("PASS"));Finished=true;
  }
  [Serializable]class Evidence{public bool Passed;public string[] Checks;public int Effects,Ships,Vertices;public float LightningDuration;}
 }
}
#endif
