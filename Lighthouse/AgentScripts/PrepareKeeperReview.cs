using System.Threading.Tasks;
using UnityEngine;
using LighthouseGame;
public static class PrepareKeeperReview
{
 public static async Task<string> Main(){
 var s=Object.FindAnyObjectByType<BlockoutSession>();
 s.Player.InputEnabled=false;s.Running=false;s.GameOver=false;s.Observations.Automatic=false;
 CinematicEnvironment.Active.AdvanceTime=false;CinematicEnvironment.Active.SetHour(0);
 s.Player.Teleport(s.Observation.position,s.SeaEvent.position+Vector3.up*6);
 await Task.Delay(600);
 for(int i=s.Shots.Count;i<3;i++){s.BusyUntil=0;s.Capture();await Task.Delay(300);}
 s.BusyUntil=0;s.MessageUntil=0;s.Player.SelectPhoto(1);s.Player.LockCursor(false);
 return "Three actual photos ready for layout review";
 }
}
