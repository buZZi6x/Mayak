#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

namespace LighthouseGame
{
    // Explicitly attached by the validation session; never shipped as gameplay.
    public sealed class WeepingGirlScenarioProbe : MonoBehaviour
    {
        public bool Done, Passed;
        public string Failure;
        private BlockoutSession _session;
        private BlockoutPlayer _player;
        private WeepingGirlEncounter _girl;
        private Vector3 _viewpoint;
        private string _output;
        private int _record;
        private bool _wasPlayerEnabled, _wasInputEnabled, _oldUiHidden, _oldFlashlight;
        private readonly List<object> _checks = new List<object>();
        private readonly List<object> _phases = new List<object>();
        private MarineAudioCapture _audio;
        private InputSettings.EditorInputBehaviorInPlayMode _oldEditorInput;
        private InputSettings.BackgroundBehavior _oldBackground;
        private Keyboard _keyboard, _originalKeyboard;
        private int _pendingFrames;
        private readonly List<Task> _frameWrites = new List<Task>();

        private void Check(string name, bool passed, object detail = null)
        {
            _checks.Add(new { name, passed, detail });
            if (!passed) Failure += name + "; ";
            Debug.Log("WEEPING_GIRL " + (passed ? "PASS " : "FAIL ") + name);
        }

        private T Field<T>(string name) => (T)typeof(WeepingGirlEncounter).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_girl);
        private float BakedHeight()
        {
            var body=Field<SkinnedMeshRenderer>("_body");var baked=new Mesh();body.BakeMesh(baked);
            float bottom=float.PositiveInfinity,top=float.NegativeInfinity;
            foreach(var point in baked.vertices)
            {
                float y=body.transform.TransformPoint(point).y;bottom=Mathf.Min(bottom,y);top=Mathf.Max(top,y);
            }
            Destroy(baked);return top-bottom;
        }

        private bool FeetSupported()
        {
            var figure=Field<Transform>("_figure");
            foreach(float side in new[]{-.169f,.169f})
            {
                Vector3 foot=figure.TransformPoint(new Vector3(side,0,.03f));
                if(!Physics.Raycast(foot+Vector3.up*.12f,Vector3.down,out var hit,.25f,~(1<<2),QueryTriggerInteraction.Ignore) || Mathf.Abs(hit.point.y-foot.y)>.06f) return false;
            }
            return true;
        }
        private void Aim(bool towards)
        {
            _player.Teleport(_viewpoint, towards ? _girl.SeatedFocus : _viewpoint + Vector3.right * 20 + Vector3.up * 1.65f);
            _player.LockCursor(true);
        }

        private IEnumerator StartEncounter()
        {
            _session.BeginRun(); _session.Preparation = 0; _session.Fuel = 110; _session.Lamp = 55;
            SelectAppearanceDay();
            _player.InputEnabled = true; _player.enabled = false;
            Aim(false); yield return null; yield return null;
            Aim(true); yield return null;
        }

        private void SelectAppearanceDay()
        {
            // The staging probe tests the encounter itself, after a successful day-4 roll.
            _session.Day = WeepingGirlAppearance.FirstDay;
            var appearance = Field<WeepingGirlAppearance>("_appearance");
            appearance.Reset();
            appearance.BeginDay(_session.Day, 0);
            _girl.ResetEncounter();
        }

        private IEnumerator WaitFor(WeepingGirlEncounter.EncounterPhase phase)
        {
            float deadline = Time.realtimeSinceStartup + 12;
            while (_girl.Phase != phase && Time.realtimeSinceStartup < deadline) yield return null;
            Check("Reached phase " + phase, _girl.Phase == phase);
        }

        private IEnumerator F1()
        {
            // Let the normal player loop consume the event. A manual Update from
            // an unfocused Editor can update only its separate input-state buffer.
            _keyboard.MakeCurrent();
            _player.enabled = true;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.F1));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null; yield return null;
            _player.enabled = false;
        }

        private bool Clean() => !_player.EncounterControlLocked && _girl.Phase == WeepingGirlEncounter.EncounterPhase.Weeping &&
            _girl.Exposure == 0 && Field<Material>("_interference").GetFloat("_Strength") == 0 &&
            Field<Material>("_interference").GetFloat("_Blackout") == 0 &&
            !Field<AudioSource>("_signal").isPlaying;

        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(_output + "/" + name + ".png", texture.EncodeToPNG());
            Destroy(texture);
        }

        private void RecordFrame(int frame, string keyFrame, RenderTexture full, RenderTexture small)
        {
            var previousTarget=RenderTexture.active;
            ScreenCapture.CaptureScreenshotIntoRenderTexture(full);
            if(SystemInfo.graphicsUVStartsAtTop) Graphics.Blit(full,small,new Vector2(1,-1),new Vector2(0,1));
            else Graphics.Blit(full,small);
            RenderTexture.active=previousTarget;
            _pendingFrames++;
            AsyncGPUReadback.Request(small,0,TextureFormat.RGBA32,request =>
            {
                try
                {
                    if(request.hasError) { Failure += "Recording GPU readback failed; "; return; }
                    byte[] pixels=request.GetData<byte>().ToArray();
                    string filename=_output+"/Playback/frame"+frame.ToString("0000")+".jpg";
                    _frameWrites.Add(Task.Run(() =>
                    {
                        File.WriteAllBytes(filename,ImageConversion.EncodeArrayToJPG(pixels,GraphicsFormat.R8G8B8A8_UNorm,1280,720,0,90));
                        if(keyFrame!=null)File.WriteAllBytes(_output+"/"+keyFrame+".png",ImageConversion.EncodeArrayToPNG(pixels,GraphicsFormat.R8G8B8A8_UNorm,1280,720));
                    }));
                }
                finally { _pendingFrames--; }
            });
        }

        private IEnumerator Start()
        {
            _output = Path.GetFullPath(Path.Combine(Application.dataPath,"../../Artifacts/WeepingGirlV2"));
            Directory.CreateDirectory(_output + "/Playback");
            _session = FindAnyObjectByType<BlockoutSession>(); _girl = FindAnyObjectByType<WeepingGirlEncounter>(); _player = _session.Player;
            _record = PlayerPrefs.GetInt("Lighthouse.Blockout.Best",0);
            _wasPlayerEnabled = _player.enabled; _wasInputEnabled = _player.InputEnabled; _oldUiHidden = _player.UIHidden;
            _oldFlashlight=_player.FlashlightOn;
            _oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode; _oldBackground = InputSystem.settings.backgroundBehavior;
            if (!_girl) { Failure = "Scene has no encounter"; Finish(); yield break; }
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            _originalKeyboard = Keyboard.current;
            _keyboard = InputSystem.AddDevice<Keyboard>("Weeping girl verification keyboard");
            _player.enabled = false; _player.InputEnabled = true;
            _viewpoint = _girl.transform.TransformPoint(new Vector3(0,.13f,3.1f));
            var environment = CinematicEnvironment.Active;
            environment.AdvanceTime = false; environment.SetWeather(CoastalWeather.Clear,true); environment.SetHour(21.8f);
            _session.Running = false; _session.GameOver = false; _girl.ResetEncounter();
            if(!_player.FlashlightOn)_player.ToggleFlashlight();
            if (!_player.UIHidden) _player.ToggleUI();
            Aim(true); yield return new WaitForSeconds(.7f);
            var mesh=Field<SkinnedMeshRenderer>("_body").sharedMesh;
            bool allPoses = true;
            for (int i=0;i<9;i++) allPoses &= mesh.GetBlendShapeIndex("Rise_"+i.ToString("00"))>=0;
            Check("Model has nine rise poses and no lunge pose", allPoses && mesh.GetBlendShapeIndex("Attack")<0);
            var crySource=Field<AudioSource>("_cry"); var signalSource=Field<AudioSource>("_signal");
            Check("Recorded crying and static assets are wired", crySource && crySource.clip && signalSource && signalSource.clip && crySource.clip.name=="Girl_Weeping_Recorded" && signalSource.clip.name.StartsWith("Girl_Static_") && signalSource.loop);
            Check("Spatial crying clip plays from the apparition", Field<AudioSource>("_cry").clip && Field<AudioSource>("_cry").isPlaying && Field<AudioSource>("_cry").spatialBlend == 1);
            Check("Girl visible with ordinary FOV from walkable pier", _girl.IsLookedAt(_player.View), new { focus = _girl.SeatedFocus.ToString(), camera = _player.View.transform.position.ToString() });
            float seatedHeight=BakedHeight();
            Check("Seated figure has human scale rather than miniature scale",seatedHeight>1.1f && seatedHeight<1.5f,seatedHeight);
            Physics.SyncTransforms();Check("Both feet have rock support at the pier level",FeetSupported());
            yield return Shot("01-seated-clear");
            yield return new WaitForSeconds(3.2f);
            Check("Free exploration cannot start a lethal encounter", !_girl.IsCommitted && _girl.Exposure == 0 && !_session.GameOver);
            _session.BeginRun(); Aim(true); yield return new WaitForSeconds(.4f);
            Check("Preparation hides the apparition and never charges gaze", !_girl.IsVisible && _girl.Exposure == 0 && !_session.GameOver);
            SelectAppearanceDay();
            _session.Preparation = 0; yield return new WaitForSeconds(.2f);
            Check("Admission waits while its position is being watched", !_girl.IsVisible);
            Aim(false); yield return null; yield return null; Aim(true); yield return null;
            Check("Admission occurs outside player view", _girl.IsVisible);
            _player.View.fieldOfView = 40; Check("Zoom preserves gaze visibility", _girl.IsLookedAt(_player.View)); _player.View.fieldOfView = 72;
            Aim(false); Check("Behind-camera figure is not a gaze target", !_girl.IsLookedAt(_player.View)); Aim(true);
            _player.Teleport(new Vector3(0,4.4f,0),_girl.SeatedFocus); Physics.SyncTransforms();
            Check("Actual lighthouse wall blocks the apparition", !_girl.IsLookedAt(_player.View));
            _player.Teleport(_girl.SeatedFocus+Vector3.right*60-Vector3.up*1.65f,_girl.SeatedFocus);
            Check("Unreadable distant apparition is harmless", !_girl.IsLookedAt(_player.View)); Aim(true);
            var barrier = GameObject.CreatePrimitive(PrimitiveType.Cube); barrier.name = "Probe only · occluder";
            barrier.transform.position = Vector3.Lerp(_player.View.transform.position, _girl.SeatedFocus,.5f);
            barrier.transform.localScale = new Vector3(3,3,.5f); barrier.transform.rotation = Quaternion.LookRotation(_girl.SeatedFocus-_player.View.transform.position);
            Physics.SyncTransforms(); Check("Opaque wall blocks all gaze samples", !_girl.IsLookedAt(_player.View));
            yield return new WaitForSeconds(3.2f); Check("Looking through a wall never accumulates exposure", _girl.Exposure == 0 && !_girl.IsCommitted);
            Destroy(barrier); yield return null; Physics.SyncTransforms();
            _session.Sleeping = true; yield return new WaitForSeconds(.2f); Check("Sleep suspends the encounter", _girl.Exposure == 0 && !_girl.IsVisible); _session.Sleeping = false;
            _player.LockCursor(false); yield return new WaitForSeconds(.3f); Check("Unlocked cursor does not charge gaze", _girl.Exposure == 0); _player.LockCursor(true);
            yield return new WaitForSeconds(2.5f); Check("A short uninterrupted stare remains safe", _girl.Exposure > 2.3f && !_girl.IsCommitted, _girl.Exposure);
            float frozenExposure = _girl.Exposure; Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(.3f); Check("Global pause freezes gaze", Mathf.Approximately(_girl.Exposure,frozenExposure)); Time.timeScale = 1;
            Aim(false); yield return new WaitForSeconds(.2f); Check("Looking away resets near-threshold exposure", _girl.Exposure == 0); Aim(true);
            yield return WaitFor(WeepingGirlEncounter.EncounterPhase.Rising);
            Check("Commitment locks controls and starts interference", _player.EncounterControlLocked && Field<Material>("_interference").GetFloat("_Strength") > 0);
            Check("Signal cue plays on commitment",Field<AudioSource>("_signal").clip && Field<AudioSource>("_signal").isPlaying);
            Time.timeScale = 0; float frozenPhase = _girl.PhaseTime; yield return new WaitForSecondsRealtime(.3f);
            Check("Global pause freezes committed sequence", Mathf.Approximately(frozenPhase,_girl.PhaseTime)); Time.timeScale = 1;
            yield return F1(); Check("Actual F1 during rise cancels camera, sound and old gaze", Clean() && _session.Preparation > 14 && !_session.GameOver);
            yield return StartEncounter(); _player.View.fieldOfView=40;
            Vector3 zoomPosition=_player.View.transform.position; Quaternion zoomRotation=_player.View.transform.rotation;
            yield return WaitFor(WeepingGirlEncounter.EncounterPhase.Staring);
            float standingHeight=BakedHeight();
            Check("Standing figure retains normal human height",standingHeight>1.65f && standingHeight<1.95f,standingHeight);
            Check("Standing stare keeps the original zoom and camera pose",Mathf.Abs(_player.View.fieldOfView-40)<.001f && Vector3.Distance(zoomPosition,_player.View.transform.position)<.001f && Quaternion.Angle(zoomRotation,_player.View.transform.rotation)<.01f);
            Check("Crying fades out while recorded static continues",!Field<AudioSource>("_cry").isPlaying && Field<AudioSource>("_signal").isPlaying);
            yield return F1(); Check("Actual F1 during stare cancels pending death", Clean() && !_session.GameOver);
            _player.View.fieldOfView=72;
            yield return new WaitForSeconds(.7f); Check("Previous stare cannot kill the new run", !_session.GameOver && !_player.EncounterControlLocked);
            yield return StartEncounter(); yield return WaitFor(WeepingGirlEncounter.EncounterPhase.Rising);
            _session.Fuel = 0; yield return null; yield return null;
            Check("Competing fuel death cancels apparition without a second attack", _session.GameOver && !_player.EncounterControlLocked && _girl.Phase == WeepingGirlEncounter.EncounterPhase.Finished && !Field<AudioSource>("_signal").isPlaying);
            yield return StartEncounter(); yield return WaitFor(WeepingGirlEncounter.EncounterPhase.Blackout);
            yield return F1(); Check("Actual F1 during blackout restores a clean run", Clean() && Field<Material>("_interference").GetFloat("_Blackout") == 0);
            yield return StartEncounter(); yield return WaitFor(WeepingGirlEncounter.EncounterPhase.Rising);
            Aim(false); _player.LockCursor(false); _session.Sleeping=true;
            yield return new WaitForSeconds(.3f);
            Check("After three seconds looking away, cursor release and sleep cannot cancel commitment",_girl.IsCommitted && _player.EncounterControlLocked);
            _session.Sleeping=false;
            yield return WaitFor(WeepingGirlEncounter.EncounterPhase.Finished);
            Check("Irreversible stare ends with the intended reason and stopped sound",_session.GameOver && _session.EndReason=="Она заметила твой взгляд." && !Field<AudioSource>("_signal").isPlaying);

            // Capture one continuous encounter through the real gameplay condition and renderer.
            _viewpoint=_girl.transform.TransformPoint(new Vector3(0,.13f,4.6f));
            yield return StartEncounter();
            _audio = _player.View.gameObject.AddComponent<MarineAudioCapture>(); _audio.StartCapture();
            int attacks = _girl.AttackCount, frame = 0;
            float started = Time.realtimeSinceStartup, nextFrame = 0;
            var previousPhase = _girl.Phase;
            bool stood = false, stared = false, lost = false, blacked = false;
            Vector3 fixedCamera=_player.View.transform.position, fixedFigure=Field<Transform>("_figure").position;
            Quaternion fixedRotation=_player.View.transform.rotation;
            Quaternion fixedFigureRotation=Field<Transform>("_figure").rotation;
            float fov=_player.View.fieldOfView, maxFigureDrift=0, maxFigureAngle=0, maxCameraDrift=0, maxCameraAngle=0, maxFovDrift=0;
            float previousStrength=0; bool monotonic=true;
            var full=RenderTexture.GetTemporary(Screen.width,Screen.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var small=RenderTexture.GetTemporary(1280,720,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            while (!_session.GameOver && Time.realtimeSinceStartup-started < 16)
            {
                yield return new WaitForEndOfFrame();
                if (_girl.Phase != previousPhase)
                {
                    _phases.Add(new { phase = _girl.Phase.ToString(), time = Time.realtimeSinceStartup-started }); previousPhase = _girl.Phase;
                }
                if (_girl.Phase == WeepingGirlEncounter.EncounterPhase.Rising) stood = true;
                maxFigureDrift=Mathf.Max(maxFigureDrift,Vector3.Distance(fixedFigure,Field<Transform>("_figure").position));
                maxFigureAngle=Mathf.Max(maxFigureAngle,Quaternion.Angle(fixedFigureRotation,Field<Transform>("_figure").rotation));
                maxCameraDrift=Mathf.Max(maxCameraDrift,Vector3.Distance(fixedCamera,_player.View.transform.position));
                maxCameraAngle=Mathf.Max(maxCameraAngle,Quaternion.Angle(fixedRotation,_player.View.transform.rotation));
                maxFovDrift=Mathf.Max(maxFovDrift,Mathf.Abs(fov-_player.View.fieldOfView));
                float signal=Field<Material>("_interference").GetFloat("_Strength");
                if (_girl.Phase==WeepingGirlEncounter.EncounterPhase.Rising || _girl.Phase==WeepingGirlEncounter.EncounterPhase.Staring)
                { monotonic &= signal+.0001f>=previousStrength;previousStrength=signal; }
                string keyFrame = null;
                if (_girl.Phase == WeepingGirlEncounter.EncounterPhase.Staring && !stared) { stared=true; keyFrame="02-staring"; }
                if (_girl.Phase == WeepingGirlEncounter.EncounterPhase.Staring && _girl.PhaseTime>1.8f && !lost) { lost=true; keyFrame="03-signal-loss"; }
                if (_girl.Phase == WeepingGirlEncounter.EncounterPhase.Blackout && !blacked) { blacked=true; keyFrame="04-blackout"; }
                if (Time.realtimeSinceStartup >= nextFrame || keyFrame != null)
                {
                    nextFrame = Time.realtimeSinceStartup + 1f/30;
                    RecordFrame(frame,keyFrame,full,small);
                    _phases.Add(new { frame, time = Time.realtimeSinceStartup-started }); frame++;
                }
            }
            yield return Shot("05-game-over");
            yield return new WaitForSeconds(.4f); _audio.Finish(_output+"/playback-audio.wav");
            while(_pendingFrames>0 || _frameWrites.Exists(task=>!task.IsCompleted)) yield return null;
            RenderTexture.ReleaseTemporary(full); RenderTexture.ReleaseTemporary(small);
            Check("Recorded frame readbacks and encodes completed",_frameWrites.Count==frame && !_frameWrites.Exists(task=>task.IsFaulted),new {frames=frame});
            Check("Real gaze plays stationary rise, stare, signal loss, blackout and Game Over", stood && stared && lost && blacked && _session.GameOver,new { _session.Message });
            Check("Exactly one commitment per encounter",_girl.AttackCount == attacks+1);
            Check("Girl root never moves or pivots on its stone",maxFigureDrift<.001f && maxFigureAngle<.01f,new {maxFigureDrift,maxFigureAngle});
            Check("Encounter never moves, rotates or zooms the camera",maxCameraDrift<.001f && maxCameraAngle<.01f && maxFovDrift<.001f,new{maxCameraDrift,maxCameraAngle,maxFovDrift});
            Check("Interference continuously increases across rise and stare",monotonic && previousStrength>.97f,previousStrength);
            Check("Recorded mix audible without digital clipping",_audio.Peak>.005f && _audio.Peak<.999f,new { _audio.Peak,_audio.Rms,_audio.Samples });
            Check("Game Over releases camera and clears lens signal and static audio",!_player.EncounterControlLocked && Field<Material>("_interference").GetFloat("_Strength")==0 && !Field<AudioSource>("_signal").isPlaying);
            var hud=FindAnyObjectByType<BlockoutHUD>();
            Check("Game Over shows the apparition reason even with hidden HUD",_player.UIHidden && hud && hud.Root.Q<UnityEngine.UIElements.Label>("result-reason").text == "Она заметила твой взгляд." && hud.Root.Q("hud").resolvedStyle.display == UnityEngine.UIElements.DisplayStyle.Flex);
            File.WriteAllText(_output+"/playback-timing.json",Newtonsoft.Json.JsonConvert.SerializeObject(new { frames=frame, seconds=Time.realtimeSinceStartup-started, events=_phases },Newtonsoft.Json.Formatting.Indented));
            // Art checks in safe preview, at the same real player viewpoint.
            _session.Running=false; _session.GameOver=false; _girl.ResetEncounter(); Aim(true);
            environment.SetWeather(CoastalWeather.Fog,true); yield return new WaitForSeconds(.65f); yield return Shot("06-seated-fog");
            environment.SetWeather(CoastalWeather.Storm,true); yield return new WaitForSeconds(.65f); yield return Shot("07-seated-storm");
            environment.SetWeather(CoastalWeather.Clear,true); environment.SetHour(18.8f); yield return new WaitForSeconds(.6f); yield return Shot("08-pier-dusk");
            _viewpoint = new Vector3(3.5f,9.94f,4.75f);
            yield return StartEncounter(); environment.SetHour(21.8f);
            Check("Upper gallery has a visible gaze target", _girl.IsLookedAt(_player.View));
            yield return WaitFor(WeepingGirlEncounter.EncounterPhase.Staring); Time.timeScale=0; yield return Shot("09-gallery-staring"); Time.timeScale=1;
            yield return new WaitForSeconds(1.8f); Time.timeScale=0; yield return Shot("10-gallery-signal-loss"); Time.timeScale=1;
            yield return F1();
            _session.Running=false;_girl.ResetEncounter();_viewpoint=_girl.transform.TransformPoint(new Vector3(0,.13f,3.1f));Aim(true);
            Finish();
        }

        private void Finish()
        {
            Passed=string.IsNullOrEmpty(Failure);Done=true;
            File.WriteAllText(_output+"/runtime.json",Newtonsoft.Json.JsonConvert.SerializeObject(new { Done,Passed,Failure,checks=_checks },Newtonsoft.Json.Formatting.Indented));
            Restore(); Debug.Log("WEEPING_GIRL_COMPLETE " + Passed);
        }

        private void Restore()
        {
            Time.timeScale=1;
            if(_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
            if(_originalKeyboard!=null && _originalKeyboard.added) _originalKeyboard.MakeCurrent();
            InputSystem.settings.editorInputBehaviorInPlayMode=_oldEditorInput;InputSystem.settings.backgroundBehavior=_oldBackground;
            if(_player){_player.enabled=_wasPlayerEnabled;_player.InputEnabled=_wasInputEnabled;if(_player.UIHidden!=_oldUiHidden)_player.ToggleUI();_player.LockCursor(false);}
            if(_player && _player.FlashlightOn!=_oldFlashlight)_player.ToggleFlashlight();
            PlayerPrefs.SetInt("Lighthouse.Blockout.Best",_record);if(_session)_session.Best=_record;PlayerPrefs.Save();
        }
        private void OnDestroy(){if(_player)Restore();if(_audio)Destroy(_audio);}
    }
}
#endif
