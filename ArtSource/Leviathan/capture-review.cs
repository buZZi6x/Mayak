using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using LighthouseGame;

if (!UnityEditor.EditorApplication.isPlaying) throw new Exception("Play Mode required.");
var session=UnityEngine.Object.FindAnyObjectByType<BlockoutSession>();
var encounter=UnityEngine.Object.FindAnyObjectByType<LeviathanEncounter>();
if (!session || !encounter) throw new Exception("Open the gameplay scene first.");
MonoBehaviour host=session;
foreach(var component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>())
    if(component.GetType().Name=="KeeperGameScene" || component is KeeperMenu)component.enabled=false;
var environment=CinematicEnvironment.Active;
string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Artifacts/Leviathan"));
Directory.CreateDirectory(output+"/ReviewFrames");
void Invoke(object target,string name,params object[] args) => target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);
session.BeginRun();session.Running=false;session.Preparation=0;session.Flash=0;session.MessageUntil=0;session.enabled=true;
session.Player.InputEnabled=false;session.Player.enabled=true;session.Player.LockCursor(false);
if (!session.Player.UIHidden) session.Player.ToggleUI();
session.Director.enabled=false;session.Director.Encounter.FrozenPreview=true;
session.Director.Encounter.ResetEncounter();session.Director.ShoalVisible=false;session.Director.Shoal.gameObject.SetActive(false);
session.Director.Traffic.Frozen=true;
foreach(var ship in session.Director.Traffic.Ships)ship.gameObject.SetActive(false);
foreach(var gull in UnityEngine.Object.FindObjectsByType<CoastalGull>())gull.gameObject.SetActive(false);
var girl=UnityEngine.Object.FindAnyObjectByType<WeepingGirlEncounter>();if(girl)girl.gameObject.SetActive(false);
session.Observations.Automatic=false;session.Observations.Clear();session.Observations.enabled=false;
environment.SetWeather(CoastalWeather.Clear,true);environment.SetHour(16);environment.AdvanceTime=false;
Vector3 outward=session.SeaEvent.position;outward.y=0;var anchor=outward.normalized*730;
session.Player.Teleport(session.Observation.position,anchor+Vector3.up*(65*LeviathanBreach.WorldScale));
session.Player.View.fieldOfView=72;
Time.timeScale=1;

IEnumerator Record()
{
    yield return new WaitForSeconds(1.2f);
    var times=new List<float>();
    var capture=session.Player.View.GetComponent<MarineAudioCapture>();
    if(!capture)capture=session.Player.View.gameObject.AddComponent<MarineAudioCapture>();
    capture.StartCapture();
    float start=Time.realtimeSinceStartup,next=0;
    Invoke(encounter,"BeginBreach",anchor);
    bool photographed=false;
    while(encounter.Showing)
    {
        yield return new WaitForEndOfFrame();
        float stamp=Time.realtimeSinceStartup-start;
        if(stamp>=next)
        {
            var image=ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(output+"/ReviewFrames/"+times.Count.ToString("D4")+".jpg",image.EncodeToJPG(91));
            if(!photographed && encounter.Age>=8.1f)File.WriteAllBytes(output+"/giant-gameplay.png",image.EncodeToPNG());
            UnityEngine.Object.Destroy(image);times.Add(stamp);next=stamp+1f/24;
        }
        if(!photographed && encounter.Age>=8.1f)
        {
            photographed=true;session.BusyUntil=0;session.Capture();
            yield return new WaitForEndOfFrame();yield return null;
            if(session.Shots.Count>0)File.WriteAllBytes(output+"/giant-photo-1000.png",session.Shots[0].Image.EncodeToPNG());
        }
    }
    capture.Finish(output+"/Leviathan-review-audio.wav");
    File.WriteAllText(output+"/review.json",Newtonsoft.Json.JsonConvert.SerializeObject(new {
        Done=true,worldScale=encounter.transform.lossyScale.x,frames=times.Count,frameTimes=times,
        photoPoints=session.Shots.Count>0?session.Shots[0].Price:0,photoKey=session.Shots.Count>0?session.Shots[0].Key:null,
        audioPeak=capture.Peak,audioRms=capture.Rms,shakeCleared=encounter.ShakeStrength==0,
        cameraPosition=session.Player.View.transform.position.ToString(),cameraRotation=session.Player.View.transform.rotation.ToString()
    },Newtonsoft.Json.Formatting.Indented));
}
host.StartCoroutine(Record());
return "Recording the final giant breach, live camera shake and audio";
