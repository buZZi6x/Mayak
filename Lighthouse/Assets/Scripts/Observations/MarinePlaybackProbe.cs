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
    public class MarinePlaybackProbe:MonoBehaviour
    {
        public bool Done,Passed;public string Failure;
        public float LastClock;public int Frames;
        string output;Texture2D pixels;RenderTexture small;
        readonly List<object> checks=new List<object>();readonly List<float> frameTimes=new List<float>();
        MarineAudioCapture audioCapture;InputSettings.EditorInputBehaviorInPlayMode oldEditor;InputSettings.BackgroundBehavior oldBackground;
        void Check(string name,bool value,object detail=null){checks.Add(new{name,passed=value,detail});if(!value)Failure=(Failure??"")+name+"; ";}
        void Key(BlockoutPlayer p,Key key){InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(key));InputSystem.Update();p.SendMessage("Update");InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());InputSystem.Update();}
        IEnumerator Start()
        {
            output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Artifacts/MarineOverhaul"));Directory.CreateDirectory(output+"/Playback");
            var env=CinematicEnvironment.Active;var s=env.Session;var p=s.Player;var e=s.Director.Encounter;var d=s.Director;
            oldEditor=InputSystem.settings.editorInputBehaviorInPlayMode;oldBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            p.enabled=false;p.InputEnabled=false;p.View.fieldOfView=72;s.Running=false;s.GameOver=false;s.Observations.Automatic=false;s.Observations.Clear();if(!env.CleanView)env.ToggleCleanView();
            Key(p,UnityEngine.InputSystem.Key.F11);float initial=e.Clock;yield return new WaitForSeconds(.6f);Check("F11 moving octopus preview",e.Clock>initial+.35f&&!e.Colossal&&d.Boat.GetComponent<CoastalFleet>().Selected==0);
            Key(p,UnityEngine.InputSystem.Key.F10);initial=e.Clock;yield return new WaitForSeconds(.6f);Check("F10 moving escape preview",e.Clock>initial+.35f&&e.Colossal&&!d.WillEat);
            Key(p,UnityEngine.InputSystem.Key.F8);initial=e.Clock;yield return new WaitForSeconds(.35f);Check("F8 cancels attack",Mathf.Abs(e.Clock-initial)<.001f&&!s.Storm);
            Key(p,UnityEngine.InputSystem.Key.F5);Check("F5 returns to phenomena",s.Observations.ActiveCount==1);
            d.Boat.GetComponent<CoastalFleet>().Select(1);Key(p,UnityEngine.InputSystem.Key.F9);
            env.SetWeather(CoastalWeather.Storm,true);env.SetHour(18.25f);s.Storm=true;env.AdvanceTime=false;
            initial=e.Clock;var position=d.Boat.position;Time.timeScale=0;yield return new WaitForSecondsRealtime(.25f);
            Check("Pause freezes attack and ship",Mathf.Abs(e.Clock-initial)<.001f&&Vector3.Distance(position,d.Boat.position)<.03f);Time.timeScale=1;
            yield return new WaitForSeconds(2);
            audioCapture=p.View.gameObject.AddComponent<MarineAudioCapture>();audioCapture.StartCapture();
            small=new RenderTexture(1280,720,0,RenderTextureFormat.ARGB32);small.Create();pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
            float firstClock=e.Clock,firstReal=Time.realtimeSinceStartup,next=e.Clock;int startCues=e.Presentation.AudioCues;int maxReleased=0,maxFloating=0;bool attackSeen=false,wreckSeen=false;float previousY=0,waveMotion=0;Vector3 previousPart=Vector3.zero;bool havePrevious=false;
            while(e.Clock<77)
            {
                LastClock=e.Clock;
                if(e.Destruction){maxReleased=Mathf.Max(maxReleased,e.Destruction.ReleasedCount);maxFloating=Mathf.Max(maxFloating,e.Destruction.FloatingCount);}
                attackSeen|=d.Feast&&e.Clock>36&&e.Clock<40;
                if(e.Clock>64&&e.Clock<75){wreckSeen|=e.WreckInFrame(p.View);if(e.Destruction.FloatingCount>0){var part=d.Boat.GetComponentsInChildren<Transform>().First(t=>t.name.StartsWith("BREAK_Plank"));if(havePrevious){waveMotion+=Mathf.Abs(part.position.y-previousY);}previousY=part.position.y;previousPart=part.position;havePrevious=true;}}
                if(e.Clock>=next)
                {
                    next=e.Clock+.09f;yield return new WaitForEndOfFrame();
                    var full=ScreenCapture.CaptureScreenshotAsTexture();var previous=RenderTexture.active;Graphics.Blit(full,small);RenderTexture.active=small;pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();RenderTexture.active=previous;Destroy(full);
                    File.WriteAllBytes(output+"/Playback/frame"+Frames.ToString("0000")+".jpg",pixels.EncodeToJPG(88));Frames++;frameTimes.Add(Time.realtimeSinceStartup-firstReal);
                }
                yield return null;
            }
            float seconds=Time.realtimeSinceStartup-firstReal;audioCapture.Finish(output+"/Attack-audio.wav");
            Check("Continuous attack breaks authored ship",attackSeen&&maxReleased>=19,new{maxReleased});
            Check("Aftermath floats on moving ocean",maxFloating>=14&&wreckSeen&&waveMotion>.3f,new{maxFloating,waveMotion});
            Check("Spatial phase audio plays once",e.Presentation.AudioCues-startCues>=9&&e.Presentation.AudioCues-startCues<=11,new{cues=e.Presentation.AudioCues-startCues,audioCapture.Peak,audioCapture.Rms,audioCapture.Samples});
            Check("Recorded mix audible without clipping",audioCapture.Peak>.01f&&audioCapture.Peak<.999f&&audioCapture.Rms>.001f);
            Check("Traffic survives sinking",d.Traffic.Ships.Count(v=>v.gameObject.activeInHierarchy)>=4);
            e.StopPreview();d.FreezePreview=true;
            Passed=Failure==null;Done=true;
            File.WriteAllText(output+"/playback.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{Done,Passed,Failure,Frames,firstClock,lastClock=LastClock,seconds,frameTimes,checks},Newtonsoft.Json.Formatting.Indented));
            p.enabled=true;p.InputEnabled=true;p.LockCursor(false);RestoreInput();
        }
        void RestoreInput(){InputSystem.settings.editorInputBehaviorInPlayMode=oldEditor;InputSystem.settings.backgroundBehavior=oldBackground;Time.timeScale=1;}
        void OnDestroy(){RestoreInput();if(pixels)Destroy(pixels);if(small){small.Release();Destroy(small);}if(audioCapture)Destroy(audioCapture);}
    }
    public class MarineAudioCapture:MonoBehaviour
    {
        readonly object gate=new object();readonly List<float[]> blocks=new List<float[]>();bool recording;int channels=2,sampleRate;double square;long count;float peak;
        public float Peak=>peak;public float Rms=>count==0?0:(float)Math.Sqrt(square/count);public long Samples=>count;
        public void StartCapture(){sampleRate=AudioSettings.outputSampleRate;lock(gate){blocks.Clear();recording=true;count=0;square=0;peak=0;}}
        void OnAudioFilterRead(float[] data,int channelCount)
        {
            lock(gate)
            {
                if(!recording)return;channels=channelCount;blocks.Add((float[])data.Clone());
                foreach(float f in data){square+=f*f;peak=Math.Max(peak,Math.Abs(f));}count+=data.Length;
            }
        }
        public void Finish(string path)
        {
            lock(gate)
            {
                recording=false;using(var file=new BinaryWriter(File.Create(path)))
                {
                    int bytes=(int)count*2;file.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));file.Write(36+bytes);file.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));file.Write(16);file.Write((short)1);file.Write((short)channels);file.Write(sampleRate);file.Write(sampleRate*channels*2);file.Write((short)(channels*2));file.Write((short)16);file.Write(System.Text.Encoding.ASCII.GetBytes("data"));file.Write(bytes);
                    foreach(var block in blocks)foreach(float f in block)file.Write((short)(Math.Max(-1,Math.Min(1,f))*32767));
                }
                blocks.Clear();
            }
        }
    }
}
#endif
