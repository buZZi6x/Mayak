#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace LighthouseGame
{
    // Added explicitly in Play Mode for integration checks, absent from the saved scene/build.
    public class CoastalArtValidationProbe : MonoBehaviour
    {
        public bool Finished, Passed;
        public string Result;
        readonly List<string> checks = new List<string>();
        void Check(bool value, string name) { checks.Add((value ? "PASS " : "FAIL ") + name); Debug.Log("COASTAL_ART_QA " + checks.Last()); }
        IEnumerator Start()
        {
            var e = CinematicEnvironment.Active; var s = e.Session; var p = s.Player; var bolt = e.Bolt;
            e.AdvanceTime = false; e.SetWeather(CoastalWeather.Storm, true); e.SetHour(17); e.enabled = false;
            s.BeginRun(); s.Preparation = 0; s.enabled = false; s.Director.enabled = false;
            p.InputEnabled = false; p.enabled = false; p.Teleport(s.Observation.position, new Vector3(0, 11, -320));
            s.Director.Evaluate(0); bolt.StrikeAt(new Vector3(0, 0, -320), 5, 77);
            yield return new WaitForSeconds(1);
            Check(bolt.VisibleIn(p.View) && bolt.GetComponentsInChildren<LineRenderer>().Count(l=>l.enabled)==12, "Branched strike is visible through the actual first-person camera");
            s.Capture(); yield return new WaitForEndOfFrame(); yield return null;
            Check(s.Shots.Count==1 && s.Shots[0].Price==100 && s.Shots[0].LightningBonus==100, "Lightning alone adds 100 points to an otherwise empty photo");
            var first=s.Shots[0]; Directory.CreateDirectory("Screenshots/Art");
            File.WriteAllBytes("Screenshots/Art/Verified-lightning-photo.png", first.Image.EncodeToPNG());
            s.Director.Evaluate(10); p.Teleport(s.Observation.position,s.Director.Boat.position+Vector3.up*2);
            bolt.StrikeAt(new Vector3(0,0,-320),5,79); s.BusyUntil=0;s.Capture();
            yield return new WaitForEndOfFrame();yield return null;
            Check(s.Shots[1].Key=="boat" && s.Shots[1].Price==110 && s.Shots[1].LightningBonus==50 && first.Price==100, "Ship 60 + repeated lightning 50 = 110; the previous photo remains fixed at 100");
            int before=s.Score;p.Teleport(s.Observation.position,new Vector3(0,11,100));s.BusyUntil=0;s.Capture();
            yield return new WaitForEndOfFrame();yield return null;
            Check(s.Shots[2].LightningBonus==0 && s.Score==before,"Lightning behind the camera earns no bonus");
            p.Teleport(new Vector3(0,4.5f,0),new Vector3(0,40,-320));
            Check(!bolt.VisibleIn(p.View),"Tower walls and floors occlude lightning for photography");
            p.Teleport(s.Observation.position,new Vector3(0,11,-320));s.Director.Evaluate(0);
            bolt.StrikeAt(new Vector3(0,0,-320),.1f,83);yield return new WaitForSeconds(.2f);
            s.BusyUntil=0;s.Capture();yield return new WaitForEndOfFrame();yield return null;
            Check(s.Shots[3].LightningBonus==0 && !bolt.VisibleIn(p.View),"Expired flash earns no bonus");
            bolt.StrikeAt(new Vector3(0,0,-320),2,89);s.BusyUntil=0;s.Capture();
            yield return new WaitForEndOfFrame();yield return null;
            Check(s.Shots[4].LightningBonus==25 && s.Score==235 && s.DayScore==235 && s.Bonus==20,"Lightning repeat penalty is independent; all photos sum to 235 and the next fuel bonus is 20 seconds");
            s.BusyUntil=0;s.Capture();Check(s.Shots.Count==5,"Lightning cannot bypass the five-photo limit");
            s.BeginRun();Check(!bolt.IsActive && s.Score==0 && s.Shots.Count==0,"A new run clears the previous strike, photographs and score");
            // Every art addition on a moving object must remain parented to that animation pivot.
            var motions=FindObjectsByType<BlockoutMotion>();
            foreach(string pivot in new[]{"ANIM_Lens","ANIM_Blanket","ANIM_RadioDial_-0.45","ANIM_RadioDial_0.45"})
            {
                var m=motions.FirstOrDefault(x=>x.name==pivot);
                Check(m && m.GetComponentsInChildren<Renderer>().Length>0,"Animated art pivot survives: "+pivot);
            }
            var animated=motions.Where(m=>m.Type!=BlockoutMotion.MotionType.Flag).ToArray();
            var poses=animated.Select(m=>m.transform.localToWorldMatrix).ToArray();
            foreach(var m in animated)m.Pulse(2);
            yield return new WaitForSeconds(.4f);
            Check(animated.Count(m=>m.transform.localToWorldMatrix!=poses[Array.IndexOf(animated,m)])>=7,"Lens, flame, pump, dials, paper and blanket still change pose over time");
            s.Running=false;s.enabled=true;s.Director.enabled=true;e.enabled=true;e.SetWeather(CoastalWeather.Clear,true);e.SetHour(17.2f);
            p.enabled=true;p.InputEnabled=true;
            Passed=checks.All(c=>c.StartsWith("PASS"));Finished=true;Result=string.Join("\n",checks);
            Directory.CreateDirectory("../Artifacts/ArtUpgrade");File.WriteAllText("../Artifacts/ArtUpgrade/integration-verification.json",JsonUtility.ToJson(new Evidence{Passed=Passed,Checks=checks.ToArray()},true));
        }
        [Serializable] class Evidence {public bool Passed;public string[] Checks;}
    }

    public class CoastalArtReviewCapture : MonoBehaviour
    {
        public bool Finished;
        public string Current;
        CinematicEnvironment env; BlockoutPlayer player;
        IEnumerator Shot(string name,Vector3 feet,Vector3 target,float hour=17.2f)
        {
            Current=name;env.SetHour(hour);player.Teleport(feet,target);
            yield return new WaitForSeconds(2.4f);
            yield return new WaitForEndOfFrame();
            var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes("Screenshots/Art/"+name+".png",image.EncodeToPNG());Destroy(image);
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory("Screenshots/Art");env=CinematicEnvironment.Active;player=env.Session.Player;
            env.Session.Running=false;env.Session.HelpVisible=false;player.InputEnabled=false;
            env.AdvanceTime=false;env.SetWeather(CoastalWeather.Clear,true);if(!env.CleanView)env.ToggleCleanView();
            yield return Shot("01-FPS-Entrance",new Vector3(0,4.4f,9.4f),new Vector3(0,8,0));
            var door=FindObjectsByType<BlockoutInteractable>().First(x=>x.Kind==BlockoutInteractable.Station.Door);door.Open=true;
            yield return Shot("02-FPS-Stairwell",new Vector3(-1.6f,4.5f,2.2f),new Vector3(-2.0f,6.7f,-1.5f),13);
            yield return Shot("03-FPS-Radio",new Vector3(3.15f,9.94f,.3f),new Vector3(4.54f,11.1f,-1.22f));
            yield return Shot("04-FPS-Lamp",new Vector3(2.1f,9.94f,0),new Vector3(0,11.1f,0));
            yield return Shot("05-FPS-Bed",new Vector3(-.75f,9.94f,-3.85f),new Vector3(-3.29f,10.65f,-3.29f));
            yield return Shot("06-FPS-Seaward-Passage",new Vector3(1.15f,9.94f,-3.52f),new Vector3(1.15f,10.8f,-6.2f));
            yield return Shot("07-FPS-Night-Interior",new Vector3(3.8f,9.94f,.7f),new Vector3(1.2f,11.3f,-2),0);
            yield return Shot("08-FPS-Rock-Edge",new Vector3(10.5f,4.5f,1),new Vector3(17,-.3f,-6));
            env.SetWeather(CoastalWeather.Storm,true);env.SetHour(17);player.Teleport(env.Session.Observation.position,new Vector3(0,55,-320));
            yield return new WaitForSeconds(3);
            env.Bolt.StrikeAt(new Vector3(0,0,-320),5,71);
            yield return new WaitForSeconds(.1f);yield return new WaitForEndOfFrame();
            var lightning=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes("Screenshots/Art/09-FPS-Lightning.png",lightning.EncodeToPNG());Destroy(lightning);
            env.Bolt.Clear();env.SetWeather(CoastalWeather.Clear,true);env.SetHour(17.2f);env.ToggleCleanView();player.InputEnabled=true;Finished=true;
        }
    }
}
#endif
