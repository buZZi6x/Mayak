#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace LighthouseGame
{
 public class ObservationSoakProbe:MonoBehaviour
 {
  public bool Finished,Passed;public string Result;
  readonly List<string> checks=new List<string>();
  void Check(bool ok,string label){checks.Add((ok?"PASS ":"FAIL ")+label);Debug.Log("OBSERVATION_SOAK "+checks.Last());}
  IEnumerator Start()
  {
   var env=CinematicEnvironment.Active;var s=env.Session;var o=s.Observations;var p=s.Player;
   s.BeginRun();s.enabled=false;s.Preparation=0;s.Running=false;s.Director.enabled=false;s.Director.Evaluate(0);p.InputEnabled=false;
   o.Automatic=false;env.SetWeather(CoastalWeather.Clear,true);env.SetHour(21.5f);env.AdvanceTime=false;
   var a=o.Spawn(3,true);var b=o.Spawn(4,true);a.Frozen=b.Frozen=true;a.Evaluate(4);b.Evaluate(4);p.Teleport(s.Observation.position,a.Focus);
   yield return new WaitForSeconds(.7f);
   for(int i=0;i<6;i++){s.BusyUntil=0;s.Capture();yield return new WaitForEndOfFrame();yield return null;}
   Check(s.Shots.Count==5&&s.Score==735&&s.Bonus==70,"Two phenomena respect independent discounts, five-photo quota and cumulative fuel bonus (735 / +70)");
   Check(s.Shots[0].Price==380&&s.Shots[1].Price==190&&s.Shots[2].Price==95,"Photo prices remain fixed across repeated composite shots");
   s.BeginRun();s.Preparation=0;s.Running=false;o.Automatic=false;s.Director.Evaluate(0);p.Teleport(s.Observation.position,new Vector3(0,80,-320));
   env.Bolt.StrikeAt(new Vector3(0,0,-320));s.Capture();yield return new WaitForEndOfFrame();yield return null;
   Check(s.Shots.Count==1&&s.Shots[0].LightningBonus==100,"The real 0.18-second strike can still be photographed");
   File.WriteAllBytes("Screenshots/Events/Verified-short-lightning-photo.png",s.Shots[0].Image.EncodeToPNG());
   yield return new WaitForSeconds(.25f);Check(!env.Bolt.IsActive,"Photo hold does not prolong the strike past its frame");
   s.BeginRun();s.Preparation=0;s.Running=false;o.Automatic=false;s.Director.Evaluate(0);env.SetWeather(CoastalWeather.Clear,true);env.SetHour(21.5f);
   a=o.Spawn(0,true);b=o.Spawn(4,true);p.Teleport(s.Observation.position,new Vector3(0,35,-180));
   var times=new List<float>();float until=Time.time+8;yield return new WaitForSeconds(1);
   while(Time.time<until){times.Add(Time.unscaledDeltaTime*1000);yield return null;}
   Check(times.Count>100&&a.Showing&&b.Showing,"Two live animated phenomena render together during the performance sample");
   o.Clear();o.Automatic=true;o.ResetDay();int count=o.SpawnCount,max=0;until=Time.time+62;
   while(Time.time<until){max=Mathf.Max(max,o.ActiveCount);yield return null;}
   Check(o.SpawnCount-count>=3&&max<=2,"Automatic night schedule spawns at least three encounters, capped at two simultaneous effects");
   Check(o.Effects.Length==8&&UnityEngine.Object.FindObjectsByType<ObservationVfx>().Length==8,"VFX pool stays at eight reusable instances after repeated observations");
   o.Clear();o.Automatic=false;
   times.Sort();Passed=checks.All(c=>c.StartsWith("PASS"));Finished=true;
   Result=JsonUtility.ToJson(new Evidence{Passed=Passed,Checks=checks.ToArray(),Frames=times.Count,MedianMs=times[times.Count/2],P95Ms=times[(int)(times.Count*.95f)],GPU=SystemInfo.graphicsDeviceName,Resolution=Screen.width+"x"+Screen.height,Spawned=o.SpawnCount-count,MaxSimultaneous=max},true);
   File.WriteAllText("../Artifacts/EventsUpgrade/soak-verification.json",Result);
  }
  [Serializable]class Evidence{public bool Passed;public string[] Checks;public int Frames,Spawned,MaxSimultaneous;public float MedianMs,P95Ms;public string GPU,Resolution;}
 }
}
#endif
