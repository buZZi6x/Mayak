#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace LighthouseGame
{
 public class MarineLifecycleProbe:MonoBehaviour
 {
  public bool Done,Passed;public string Failure;
  readonly List<object> checks=new List<object>();
  void Check(string name,bool ok,object detail=null){checks.Add(new{name,passed=ok,detail});if(!ok)Failure=(Failure??"")+name+"; ";}
  Bounds Actual(Transform root){var rr=root.GetComponentsInChildren<MeshRenderer>();var b=rr[0].bounds;foreach(var r in rr)b.Encapsulate(r.bounds);return b;}
  IEnumerator Start()
  {
   var env=CinematicEnvironment.Active;var s=env.Session;var d=s.Director;var e=d.Encounter;var p=s.Player;var fleet=d.Boat.GetComponent<CoastalFleet>();
   p.enabled=false;p.InputEnabled=false;p.View.fieldOfView=72;s.Running=false;s.GameOver=false;d.FreezePreview=true;s.Observations.Automatic=false;s.Observations.Clear();d.Traffic.Frozen=true;
   env.SetWeather(CoastalWeather.Cloudy,true);env.SetHour(14);env.AdvanceTime=false;
   int count=0,visible=0,overlap=0;var distances=new List<float>();
   for(int type=0;type<2;type++)for(int trial=0;trial<12;trial++)
   {
    e.BeginNight();e.SelectCreature(type==0);s.Storm=true;d.WillEat=true;float clock=0;
    p.Teleport(s.Observation.position,Quaternion.Euler(0,trial*30,0)*Vector3.forward*180);
    e.Evaluate(clock,false);
    for(int attempt=0;attempt<80&&!d.BoatVisible;attempt++){clock+=.25f;e.Evaluate(clock,true);}
    e.RenderPose();
    if(d.BoatVisible){count++;distances.Add(d.Boat.position.magnitude);if(!SeaPlacement.OutsideView(p.View,Actual(fleet.Vessels[fleet.Selected]),0))visible++;if(!SeaPlacement.ClearOfTraffic(d.Boat.position,type==0?69:41))overlap++;}
    yield return null;
   }
   Check("Natural actor admission outside actual camera",count==24&&visible==0,new{count,visible});
   Check("Actor start clears all permanent traffic",overlap==0,new{overlap});
   Check("Actor start distances vary",distances.Select(x=>Mathf.RoundToInt(x)).Distinct().Count()>8);
   e.BeginNight();e.SelectCreature(true);s.Sleeping=true;for(int i=0;i<50;i++)e.Evaluate(i*.5f,true);
   Check("Sleep cannot admit a new actor",!d.BoatVisible&&!d.KrakenVisible);s.Sleeping=false;
   e.BeginNight();e.SelectCreature(true);s.Storm=true;d.WillEat=true;float time=0;
   p.Teleport(s.Observation.position,Vector3.forward*200);
   while(!d.BoatVisible&&time<40){time+=.25f;e.Evaluate(time,true);}
   while(e.Clock<16.5f&&time<80){p.Teleport(s.Observation.position,d.Boat.position+Vector3.up*10);time+=.25f;e.Evaluate(time,true);yield return null;}
   var before=d.Boat.position;
   for(int i=0;i<160;i++){p.Teleport(s.Observation.position,d.Boat.position+Vector3.up*10);time+=.25f;e.Evaluate(time,true);yield return null;}
   Check("Watched victim sails while creature waits",!d.KrakenVisible&&Vector3.Distance(before,d.Boat.position)>8,new{clock=e.Clock,distance=Vector3.Distance(before,d.Boat.position)});
   var away=s.Observation.position-(d.Boat.position-s.Observation.position);p.Teleport(s.Observation.position,away);
   for(int i=0;i<8;i++){time+=.25f;e.Evaluate(time,true);yield return null;}
   Check("Creature emerges after gaze leaves",d.KrakenVisible&&e.Clock>17,new{clock=e.Clock});
   e.BeginNight();e.SelectCreature(false);s.Storm=true;d.WillEat=false;time=0;p.Teleport(s.Observation.position,Vector3.forward*200);
   while(!d.BoatVisible&&time<40){time+=.25f;e.Evaluate(time,true);}
   for(int i=0;i<60;i++){var target=d.Kraken.position;target.y=8;p.Teleport(s.Observation.position,target);time+=.25f;e.Evaluate(time,true);e.RenderPose();yield return null;}
   Check("Escaping octopus waits at its actual body location",!d.KrakenVisible&&e.Clock<17);
   away=s.Observation.position-(d.Kraken.position-s.Observation.position);p.Teleport(s.Observation.position,away);
   for(int i=0;i<8;i++){time+=.25f;e.Evaluate(time,true);e.RenderPose();yield return null;}
   Check("Escaping octopus admission after gaze leaves",d.KrakenVisible&&e.Clock>17);
   e.BeginNight();e.SelectCreature(true);s.Storm=false;d.WillEat=false;time=0;p.Teleport(s.Observation.position,Vector3.forward*200);
   while(!d.BoatVisible&&time<40){time+=.25f;e.Evaluate(time,true);}
   bool disappeared=false;
   while(e.Clock<93&&time<160){p.Teleport(s.Observation.position,d.Boat.position+Vector3.up*10);time+=.5f;e.Evaluate(time,true);disappeared|=!d.BoatVisible;yield return null;}
   Check("Watched survivor passes end of timeline",!disappeared&&d.BoatVisible&&e.Clock>=93,new{clock=e.Clock});
   p.Teleport(s.Observation.position,s.Observation.position-(d.Boat.position-s.Observation.position));time+=.5f;e.Evaluate(time,true);
   Check("Survivor cleanup waits until offscreen",!d.BoatVisible);
   // Exact hull triangles, not an arbitrary bounding rectangle, occlude a photo.
   e.Preview(true);e.FrozenPreview=true;e.Evaluate(38,true);e.RenderPose();
   var traffic=d.Traffic.Ships[0];var saved=traffic.transform.position;var rotation=traffic.transform.rotation;
   p.Teleport(s.Observation.position,d.Boat.position+Vector3.up*10);
   var ray=(d.Boat.position-p.View.transform.position).normalized;
   traffic.transform.SetPositionAndRotation(p.View.transform.position+ray*20-Vector3.up*3,Quaternion.LookRotation(Vector3.Cross(Vector3.up,ray)));
   traffic.transform.localScale=Vector3.one*3;Physics.SyncTransforms();yield return null;
   var sw=System.Diagnostics.Stopwatch.StartNew();bool hidden=!e.ShipInFrame(p.View)&&!e.CreatureInFrame(p.View);sw.Stop();
   Check("Foreground ship occludes hidden attack evidence",hidden,new{milliseconds=sw.Elapsed.TotalMilliseconds});
   traffic.transform.localScale=Vector3.one;traffic.transform.SetPositionAndRotation(saved,rotation);
   Check("Uncovered attack remains photographable",e.ShipInFrame(p.View)&&e.CreatureInFrame(p.View));
   var armArray=(Vector3[])typeof(KrakenEncounter).GetField("armSamples",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(e);
   for(int i=0;i<armArray.Length;i++)armArray[i]=p.View.transform.position-p.View.transform.forward*30;
   p.Teleport(s.Observation.position,d.Kraken.position+Vector3.up*10);
   Check("Visible mantle alone is photographic evidence",e.CreatureInFrame(p.View));
   // A collapsed set of feeder points must never make a creature photograph.
   e.Preview(true,false);e.FrozenPreview=true;e.Evaluate(38,true);e.RenderPose();
   var samples=(Vector3[])typeof(KrakenEncounter).GetField("armSamples",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(e);
   var off=p.View.transform.position-p.View.transform.forward*30;var centre=p.View.transform.position+p.View.transform.forward*40;
   for(int i=0;i<samples.Length;i++)samples[i]=i<8*65?off:centre;
   e.OctopusBody.gameObject.SetActive(false);Check("Collapsed octopus feeders give no photo",!e.CreatureInFrame(p.View));e.OctopusBody.gameObject.SetActive(true);
   e.StopPreview();s.Sleeping=false;d.FreezePreview=true;d.Traffic.Frozen=false;p.enabled=true;p.InputEnabled=true;p.LockCursor(false);
   Done=true;Passed=Failure==null;var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Artifacts/MarineOverhaul/lifecycle.json"));
   File.WriteAllText(output,Newtonsoft.Json.JsonConvert.SerializeObject(new{Done,Passed,Failure,checks},Newtonsoft.Json.Formatting.Indented));
  }
 }
}
#endif
