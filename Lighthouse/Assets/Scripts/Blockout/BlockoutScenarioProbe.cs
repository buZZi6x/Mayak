#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace LighthouseGame
{
    public class BlockoutScenarioProbe : MonoBehaviour
    {
        public bool Finished, Passed;
        public string Result;
        BlockoutSession session;
        int previousRecord;
        readonly List<string> checks = new List<string>();
        void Check(bool value, string name) { checks.Add((value ? "PASS " : "FAIL ") + name); Debug.Log("BLOCKOUT_SCENARIO " + checks.Last()); }
        void StageKraken()
        {
            var e=session.Director.Encounter;if(!e)return;
            session.Observations.Automatic=false;session.Observations.Clear();e.ManualTimeline=true;e.SelectCreature(true);
            session.Director.FreezePreview=true;e.Evaluate(38,true);e.RenderPose();
            session.Player.Teleport(session.Observation.position,session.Director.Boat.position+Vector3.up*10);
        }
        IEnumerator Start()
        {
            session = FindAnyObjectByType<BlockoutSession>(); previousRecord = PlayerPrefs.GetInt("Lighthouse.Blockout.Best",0);
            session.Player.InputEnabled = false; session.BeginRun();
            yield return new WaitForSeconds(.3f);
            Check(session.Fuel == session.StartFuel && session.Preparation > 14 && !session.Director.BoatVisible && !session.Director.KrakenVisible, "15-second preparation does not consume fuel or spawn events");
            session.Capture(); Check(session.Shots.Count == 0,"No photo during preparation");
            session.Preparation = 0; session.Storm = true; session.NightTime = 39; session.Director.WillEat = true;
            yield return null;
            StageKraken();
            yield return null;
            session.Capture();
            yield return new WaitForEndOfFrame();yield return null;
            Check(session.Shots.Count == 1 && session.Shots[0].Key == "feast" && session.Shots[0].Price == 300 && session.Shots[0].Image, "Visible Kraken eating a visible ship: 300 points and real image");
            var pixels=session.Shots[0].Image.GetPixels();float average=pixels.Average(c=>c.grayscale);
            Check(average>.005f&&average<.95f&&pixels.Max(c=>c.grayscale)-pixels.Min(c=>c.grayscale)>.08f,"Captured post-processed photograph contains visible scene detail, without black or white exposure failure");
            Directory.CreateDirectory("Screenshots/Environment");File.WriteAllBytes("Screenshots/Environment/Verified-game-photo.png",ImageConversion.EncodeToPNG(session.Shots[0].Image));
            session.BusyUntil = 0; session.Capture();
            Check(session.Shots.Count == 2 && session.Shots[1].Price == 150 && session.Shots[0].Price == 300, "Repeat reduced to 150; previous photo value stays 300");
            session.Player.Teleport(session.Observation.position, session.Observation.position + Vector3.up * 80);
            for(int i=0;i<4;i++) { session.BusyUntil=0; session.Capture(); }
            Check(session.Shots.Count == 5 && session.Score == 450 && session.Shots.Skip(2).All(s=>s.Price==0),"Five-photo quota; empty photos consume a frame; score sums all frames");
            var radio=FindObjectsByType<BlockoutInteractable>().First(s=>s.Kind==BlockoutInteractable.Station.Radio);
            session.Player.Teleport(radio.transform.position + Vector3.forward*1.5f, radio.transform.position+Vector3.up);
            session.BusyUntil=0; radio.Use(session); yield return null;
            Check(session.Confirmed==450 && session.Shots.All(s=>s.Sent) && session.PaidHundreds==0 && !session.Busy,"Photos confirm automatically without paying fuel early or timed radio work");
            session.Radio.CloseControls(); radio.Use(session); session.Radio.CloseControls(); Check(session.Confirmed==450,"Using radio twice cannot duplicate points");
            var bed=FindObjectsByType<BlockoutInteractable>().First(s=>s.Kind==BlockoutInteractable.Station.Bed);
            session.Player.Teleport(bed.transform.position+Vector3.forward*1.1f,bed.transform.position+Vector3.up);
            float fuelBefore=session.Fuel; bed.Use(session); while(session.Sleeping) yield return null;
            Check(session.Day==2 && session.Shots.Count==0 && session.PaidHundreds==4 && Mathf.Abs(session.Fuel-(fuelBefore+40))<.4f && session.Preparation==0,"Sleep carries remaining fuel +40 seconds; five frames restored; no new preparation");
            session.Storm=true; session.NightTime=39; session.Director.WillEat=true; yield return null;
            StageKraken(); session.BusyUntil=0; session.Capture();
            Check(session.Shots.Count==1 && session.Shots[0].Price==300 && session.Score==750 && session.Bonus==30,"Next day resets repeats; cumulative hundred thresholds do not repay old bonuses");
            yield return new WaitForEndOfFrame();
            int day=session.Day; session.BusyUntil=0; bed.Use(session); Check(session.Day==day && session.Sleeping,"Sleep starts immediately with fewer than five frames");
            while(session.Sleeping) yield return null;
            var lamp=FindObjectsByType<BlockoutInteractable>().First(s=>s.Kind==BlockoutInteractable.Station.Lamp);
            session.Player.Teleport(lamp.transform.position+Vector3.right*1.3f,lamp.transform.position+Vector3.up); session.Lamp=0; session.Darkness=0; fuelBefore=session.Fuel; lamp.Use(session); yield return new WaitForSeconds(6.2f);
            Check(session.Lamp>59 && session.Fuel<fuelBefore-6,"Lamp service restores maintenance time without replenishing fuel");
            session.Lamp=0; session.Darkness=19.9f; yield return new WaitForSeconds(.3f);
            Check(session.GameOver,"Twenty seconds of warning flashes without repair ends the run");
            session.BeginRun(); Check(session.Fuel==session.StartFuel && session.Day==1 && session.Score==0 && session.Shots.Count==0,"New run resets fuel to the same starting pool");
            session.Running=false; session.Preparation=15; session.Player.Teleport(session.Observation.position,session.SeaEvent.position);
            Passed=checks.All(c=>c.StartsWith("PASS")); Finished=true; Result=string.Join("\n",checks);
            Directory.CreateDirectory("../Artifacts"); File.WriteAllText("../Artifacts/scenario-verification.json",JsonUtility.ToJson(new Evidence {Passed=Passed,Checks=checks.ToArray()},true));
            PlayerPrefs.SetInt("Lighthouse.Blockout.Best",previousRecord); PlayerPrefs.Save(); session.Best=previousRecord;
            Debug.Log("BLOCKOUT_SCENARIO_COMPLETE " + Passed);
        }
        [Serializable] class Evidence { public bool Passed; public string[] Checks; }
        void OnDestroy() { if(session) session.Player.InputEnabled=true; PlayerPrefs.SetInt("Lighthouse.Blockout.Best",previousRecord); }
    }
}
#endif
