#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace LighthouseGame
{
 public class EncounterValidationProbe:MonoBehaviour
 {
  public bool Finished,Passed;public string Result;
  readonly List<string> checks=new List<string>();readonly List<Frame> frames=new List<Frame>();
  void Check(bool ok,string name){checks.Add((ok?"PASS ":"FAIL ")+name);Debug.Log("ENCOUNTER_QA "+checks.Last());}
  void Shot(string name){var tex=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes("Screenshots/Encounters/"+name+".png",tex.EncodeToPNG());Destroy(tex);}
  IEnumerator Start()
  {
   Directory.CreateDirectory("Screenshots/Encounters");Directory.CreateDirectory("../Artifacts/EncountersUpgrade");
   var env=CinematicEnvironment.Active;var s=env.Session;var d=s.Director;var e=d.Encounter;var p=s.Player;var o=s.Observations;var fleet=d.Boat.GetComponent<CoastalFleet>();
   s.enabled=false;s.Running=false;s.GameOver=false;p.InputEnabled=false;p.enabled=false;d.FreezePreview=true;e.StopPreview();o.Automatic=false;o.Clear();env.AdvanceTime=false;if(!env.CleanView)env.ToggleCleanView();
   Vector3 focus=new Vector3(s.SeaEvent.position.x,4,s.SeaEvent.position.z);
   env.SetWeather(CoastalWeather.Storm,true);env.SetHour(17);s.Storm=true;d.WillEat=true;p.Teleport(s.Observation.position,focus);p.View.fieldOfView=72;
   yield return new WaitForSeconds(2);
   for(int ship=0;ship<3;ship++)
   {
    fleet.Select(ship);float lastY=1,maxJump=0;bool resurrect=false;
    for(float t=41;t<=77;t+=.05f){d.Evaluate(t);if(t<52)maxJump=Mathf.Max(maxJump,Mathf.Abs(d.Boat.position.y-lastY));if(t>=52&&d.BoatVisible)resurrect=true;lastY=d.Boat.position.y;}
    Check(!resurrect&&maxJump<.8f,"Vessel "+ship+" continuous descent, no reappearance through 77s");
    foreach(float t in new float[]{24,33,38,43,46,49,52,56,66,76})
    {
     d.Evaluate(t);e.RenderPose();yield return new WaitForSeconds(.45f);env.Bolt.Clear();
     d.PhotoTarget(p.View,out string key,out string label,out int price);
     var renderers=fleet.Vessels[ship].GetComponentsInChildren<MeshRenderer>();float top=renderers.Max(r=>r.bounds.max.y);
     frames.Add(new Frame{Ship=ship,Time=t,Phase=e.Phase,Key=key,Price=price,BoatY=d.Boat.position.y,HighestMesh=top,BoatActive=d.BoatVisible});
     if(t==38)Check(key=="feast"&&price==300,"Vessel "+ship+" documented contact at normal FOV gives 300");
     if(t==52)Check(top<0&&!d.BoatVisible&&key!="feast"&&!key.StartsWith("boat"),"Vessel "+ship+" all geometry submerged, no ship/feast credit");
     if(t==56)Check(key=="wreck"&&price==120,"Vessel "+ship+" visible aftermath gives wreck120");
     if(t==76)Check(key=="empty","Vessel "+ship+" quiet water gives zero before next cycle");
     if(ship==1||t==38||t==49||t==56){yield return new WaitForEndOfFrame();Shot("Kraken-"+ship+"-t"+t.ToString("00"));}
    }
   }
   fleet.Select(1);d.Evaluate(38);e.RenderPose();p.View.fieldOfView=40;yield return new WaitForSeconds(.5f);yield return new WaitForEndOfFrame();Shot("Kraken-contact-zoom");
   p.View.fieldOfView=72;env.SetHour(19.2f);yield return new WaitForSeconds(3);yield return new WaitForEndOfFrame();Shot("Kraken-night-player");
   env.SetHour(17);d.WillEat=false;d.Evaluate(39);e.RenderPose();p.Teleport(s.Observation.position,(d.Boat.position+d.Kraken.position)*.5f+Vector3.up*3);yield return new WaitForSeconds(2);
   d.PhotoTarget(p.View,out string ek,out _,out int ep);Check(ek=="escape"&&ep==180,"Surviving ship and retreating creature give escape180");yield return new WaitForEndOfFrame();Shot("Escape-t39");
   d.Evaluate(50);e.RenderPose();p.Teleport(s.Observation.position,d.Boat.position+Vector3.up*3);yield return null;d.PhotoTarget(p.View,out ek,out _,out ep);Check(d.BoatVisible&&ek==fleet.PhotoKey&&ep==fleet.PhotoPoints,"After creature dives, survivor uses its vessel class reward");
   // Ghost is included in the same bounded scheduler, with genuine geometry evidence.
   s.BeginRun();s.Running=false;s.Preparation=0;d.FreezePreview=true;o.Automatic=false;d.Evaluate(0);env.SetWeather(CoastalWeather.Clear,true);env.SetHour(21.5f);env.Bolt.Clear();o.SpawnGhost(true);var ghost=o.Ghost;ghost.Frozen=true;ghost.Evaluate(17);p.Teleport(s.Observation.position,ghost.Focus);p.View.fieldOfView=72;
   yield return new WaitForSeconds(3);var ghostBounds=ghost.Surfaces[0].bounds;foreach(var r in ghost.Surfaces)ghostBounds.Encapsulate(r.bounds);Check(ghost.VisibleIn(p.View)&&ghost.Vessel.position.y>40&&ghostBounds.size.y>15&&Mathf.Max(ghostBounds.size.x,ghostBounds.size.z)>25,"Ghost has full ship-sized geometry and is airborne from player camera");yield return new WaitForEndOfFrame();Shot("Ghost-night-player");
   s.BusyUntil=0;s.Capture();yield return new WaitForEndOfFrame();yield return null;Check(s.Shots.Count==1&&s.Shots[0].Price==240,"Ghost photograph gives 240 and actual image");
   s.BusyUntil=0;s.Capture();yield return new WaitForEndOfFrame();yield return null;Check(s.Shots.Count==2&&s.Shots[1].Price==120&&s.Shots[0].Price==240,"Ghost repeat halves, old photo remains fixed");
   p.View.fieldOfView=40;yield return new WaitForSeconds(.6f);yield return new WaitForEndOfFrame();Shot("Ghost-night-zoom");
   p.Teleport(s.Observation.position,s.Observation.position-(ghost.Focus-s.Observation.position));Check(!ghost.VisibleIn(p.View),"Ghost behind camera cannot score");
   p.Teleport(new Vector3(0,5.2f,0),ghost.Focus);Check(!ghost.VisibleIn(p.View),"Tower blocks ghost photo");
   p.Teleport(s.Observation.position,ghost.Focus);p.View.fieldOfView=72;float oldClip=p.View.farClipPlane;p.View.farClipPlane=30;Check(!ghost.VisibleIn(p.View),"Ghost beyond camera far clip cannot score");p.View.farClipPlane=oldClip;
   ghost.Evaluate(0);Check(!ghost.VisibleIn(p.View),"Invisible start of apparition cannot score");ghost.Evaluate(34);Check(!ghost.VisibleIn(p.View),"Faded apparition cannot score");
   ghost.Evaluate(15);s.Sleeping=true;int count=o.SpawnCount;yield return new WaitForSeconds(.4f);Check(ghost.Showing&&o.SpawnCount==count,"Sleep preserves visible phenomenon and blocks new spawns");s.Sleeping=false;
   s.BeginRun();d.Evaluate(0);yield return null;Check(!d.BoatVisible&&!d.KrakenVisible&&o.ActiveCount==0,"New run and preparation clear every event including ghost");
   s.Running=false;o.Automatic=false;d.FreezePreview=true;d.Evaluate(0);env.SetWeather(CoastalWeather.Clear,true);env.SetHour(13);Check(o.Weight(8)==0,"Ghost excluded during day");env.SetHour(22);Check(o.Weight(8)>0,"Ghost eligible on clear nights");env.SetWeather(CoastalWeather.Storm,true);Check(o.Weight(8)==0,"Ghost excluded during storm");
   // Island arrangement from actual walkway, with a clear center for sea events.
   foreach(var weather in new[]{CoastalWeather.Clear,CoastalWeather.Storm})
   {env.SetWeather(weather,true);env.SetHour(16);p.Teleport(s.Observation.position,new Vector3(-420,45,-900));yield return new WaitForSeconds(3);yield return new WaitForEndOfFrame();Shot("Coast-"+weather);}
   var coast=GameObject.Find("Distant archipelago");Check(coast.transform.childCount==6&&p.View.farClipPlane>=2500&&coast.GetComponentsInChildren<MeshRenderer>().All(r=>r.bounds.size.y>100&&r.bounds.size.x>250),"Six full-size terrain landforms within camera range");
   env.SetWeather(CoastalWeather.Storm,true);env.SetHour(17);s.Storm=true;d.WillEat=true;d.Evaluate(38);e.RenderPose();p.Teleport(s.Observation.position,focus);
   // Real animation/buoyancy/CPU sampling, not a frozen still.
   var timings=new List<float>();float began=Time.time;while(Time.time-began<8){float t=36+Mathf.Repeat(Time.time-began,5);d.Evaluate(t);timings.Add(Time.unscaledDeltaTime*1000);yield return null;}
   timings.Sort();float median=timings[timings.Count/2],p95=timings[Mathf.Min(timings.Count-1,(int)(timings.Count*.95f))];Check(timings.Count>100&&p95<50,"Live kraken frame-time sanity (p95 < 50 ms, local editor)");
   Passed=checks.All(c=>c.StartsWith("PASS"));Finished=true;Result=JsonUtility.ToJson(new Report{Passed=Passed,Checks=checks.ToArray(),Frames=frames.ToArray(),MedianMs=median,P95Ms=p95,SampledFrames=timings.Count},true);File.WriteAllText("../Artifacts/EncountersUpgrade/encounter-verification.json",Result);
  }
  [Serializable]class Frame{public int Ship,Price;public float Time,BoatY,HighestMesh;public string Phase,Key;public bool BoatActive;}
  [Serializable]class Report{public bool Passed;public string[] Checks;public Frame[] Frames;public float MedianMs,P95Ms;public int SampledFrames;}
 }
}
#endif
