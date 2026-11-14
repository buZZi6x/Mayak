#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace LighthouseGame
{
 public class EncounterPlaybackProbe:MonoBehaviour
 {
  public bool Finished,Passed;public string Result;readonly List<string> checks=new List<string>();int frames;
  void Check(bool ok,string text){checks.Add((ok?"PASS ":"FAIL ")+text);Debug.Log("PLAYBACK_QA "+checks.Last());}
  void Key(BlockoutPlayer player,UnityEngine.InputSystem.Key key)
  {InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(key));InputSystem.Update();player.SendMessage("Update");InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());InputSystem.Update();}
  void Frame()
  {
   var texture=ScreenCapture.CaptureScreenshotAsTexture();var small=RenderTexture.GetTemporary(1280,720);var prior=RenderTexture.active;Graphics.Blit(texture,small);RenderTexture.active=small;
   var output=new Texture2D(1280,720,TextureFormat.RGB24,false);output.ReadPixels(new Rect(0,0,1280,720),0,0);output.Apply();File.WriteAllBytes("Screenshots/Encounters/Playback/frame"+(frames++).ToString("0000")+".jpg",output.EncodeToJPG(85));RenderTexture.active=prior;RenderTexture.ReleaseTemporary(small);Destroy(output);Destroy(texture);
  }
  IEnumerator Start()
  {
   Directory.CreateDirectory("Screenshots/Encounters/Playback");var env=CinematicEnvironment.Active;var s=env.Session;var p=s.Player;var d=s.Director;var e=d.Encounter;
   var oldEditor=InputSystem.settings.editorInputBehaviorInPlayMode;var oldBackground=InputSystem.settings.backgroundBehavior;
   InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
   s.Running=false;s.enabled=false;s.GameOver=false;p.enabled=false;p.InputEnabled=false;s.Observations.Clear();s.Observations.Automatic=false;p.View.fieldOfView=72;if(!env.CleanView)env.ToggleCleanView();
   Key(p,UnityEngine.InputSystem.Key.F9);float initial=e.Clock;yield return new WaitForSeconds(.8f);Check(e.Clock>initial+.5f&&d.WillEat,"F9 input starts moving attack preview, not a frozen pose");
   float paused=e.Clock;Vector3 pose=d.Boat.position;Time.timeScale=0;yield return new WaitForSecondsRealtime(.3f);Check(Mathf.Abs(e.Clock-paused)<.001f&&Vector3.Distance(pose,d.Boat.position)<.02f,"Global timeScale pause freezes encounter and buoyancy");Time.timeScale=1;
   Key(p,UnityEngine.InputSystem.Key.F10);initial=e.Clock;yield return new WaitForSeconds(.8f);Check(e.Clock>initial+.5f&&!d.WillEat,"F10 input starts independent non-lethal branch");
   Key(p,UnityEngine.InputSystem.Key.F8);float stopped=e.Clock;yield return new WaitForSeconds(.35f);Check(Mathf.Abs(e.Clock-stopped)<.001f&&!s.Storm,"F8 cancels encounter preview and shows selected ship");
   Key(p,UnityEngine.InputSystem.Key.F5);Check(!d.FreezePreview&&s.Observations.ActiveCount==1,"F5 exits frozen fleet inspection");
   // Observe a real, unaccelerated attack, including every boundary after 49 seconds.
   d.Boat.GetComponent<CoastalFleet>().Select(1);Key(p,UnityEngine.InputSystem.Key.F9);env.SetHour(17);float next=e.Clock;bool sunk=false,resurrected=false,contact=false;float firstRecorded=0,lastRecorded=0;
   while(e.Clock<61)
   {
    if(e.Clock>=36&&e.Clock<42)contact|=d.Feast;
    if(e.Clock>=52){sunk=true;resurrected|=d.BoatVisible;}
    if(e.Clock>=30&&e.Clock<58&&e.Clock>=next){next=e.Clock+.125f;yield return new WaitForEndOfFrame();if(frames==0)firstRecorded=e.Clock;lastRecorded=e.Clock;Frame();}
    yield return null;
   }
   Check(contact&&sunk&&!resurrected,"Unaccelerated F9 attack completes contact, sinking, aftermath without reappearance");
   // Ghost bow and trailing wake agree with the actual movement direction.
   e.StopPreview();d.Evaluate(0);var o=s.Observations;o.SpawnGhost(true);var g=o.Ghost;g.Frozen=true;g.Evaluate(12);Vector3 before=g.Vessel.position;g.Evaluate(12.1f);Vector3 velocity=g.Vessel.position-before;
   Check(Vector3.Dot(-g.Vessel.forward,velocity.normalized)>.8f&&Vector3.Dot(g.Wisps[0].GetPosition(35)-g.Vessel.position,velocity)<0,"Ghost flies bow first, wake trails behind actual velocity");
   o.Clear();s.BeginRun();s.Preparation=0;s.enabled=true;s.Lamp=60;float fuel=s.Fuel,time=s.NightTime;Time.timeScale=0;yield return new WaitForSecondsRealtime(.25f);Check(s.Fuel==fuel&&s.NightTime==time,"Global pause preserves session fuel and clock");Time.timeScale=1;s.enabled=false;s.Running=false;
   InputSystem.settings.editorInputBehaviorInPlayMode=oldEditor;InputSystem.settings.backgroundBehavior=oldBackground;
   Passed=checks.All(c=>c.StartsWith("PASS"));Finished=true;Result=JsonUtility.ToJson(new Report{Passed=Passed,Checks=checks.ToArray(),Frames=frames,FirstClock=firstRecorded,LastClock=lastRecorded,PlaybackSeconds=lastRecorded-firstRecorded},true);File.WriteAllText("../Artifacts/EncountersUpgrade/playback-verification.json",Result);
  }
  void OnDestroy(){Time.timeScale=1;}
  [Serializable]class Report{public bool Passed;public string[] Checks;public int Frames;public float FirstClock,LastClock,PlaybackSeconds;}
 }
}
#endif
