#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
namespace LighthouseGame
{
    public class MarineReviewProbe:MonoBehaviour
    {
        public bool Done;public string Failure;
        BlockoutSession s;CinematicEnvironment env;KrakenEncounter encounter;
        readonly List<object> checks=new List<object>();
        string root;Texture2D frame;
        void Start(){StartCoroutine(RunGuarded(Run()));}
        IEnumerator RunGuarded(IEnumerator routine)
        {
            while(true)
            {
                object next=null;bool more=false;
                try{more=routine.MoveNext();if(more)next=routine.Current;}
                catch(Exception e){Failure=e.ToString();break;}
                if(!more)break;yield return next;
            }
            Done=true;File.WriteAllText(Path.Combine(root,"verification.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{Done,Failure,checks},Newtonsoft.Json.Formatting.Indented));
        }
        void Check(string name,bool passed,object detail=null){checks.Add(new{name,passed,detail});}
        IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(.6f);yield return new WaitForEndOfFrame();
            if(!frame||frame.width!=Screen.width||frame.height!=Screen.height){if(frame)Destroy(frame);frame=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);}
            frame.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);frame.Apply();File.WriteAllBytes(Path.Combine(Application.dataPath,"../Screenshots/Marine/"+name+".png"),frame.EncodeToPNG());
        }
        IEnumerator Run()
        {
            root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Artifacts/MarineOverhaul"));Directory.CreateDirectory(root);Directory.CreateDirectory(Path.Combine(Application.dataPath,"../Screenshots/Marine"));
            s=FindAnyObjectByType<BlockoutSession>();env=CinematicEnvironment.Active;encounter=s.Director.Encounter;
            s.Player.enabled=false;s.Player.InputEnabled=false;s.Player.View.fieldOfView=72;s.Observations.Automatic=false;
            if(!env.CleanView)env.ToggleCleanView();env.AdvanceTime=false;
            Check("Four independent permanent ships",s.Director.Traffic.Ships.Length>=4&&s.Director.Traffic.Ships.All(v=>v&&v.gameObject.activeInHierarchy));
            var fleet=s.Director.Boat.GetComponent<CoastalFleet>();
            for(int i=1;i<fleet.Vessels.Length;i++)
            {
                fleet.Select(i);encounter.Preview(true);encounter.FrozenPreview=true;
                env.SetWeather(CoastalWeather.Cloudy,true);env.SetHour(14);s.Storm=true;
                encounter.Evaluate(35,true);encounter.RenderPose();
                if(i==1)yield return new WaitForSecondsRealtime(2);
                var renderers=fleet.Vessels[i].GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                Check("Ocean ship dimensions "+i,bounds.size.x>35||bounds.size.z>35,new{bounds=bounds.size.ToString(),renderers=renderers.Length});
                Check("Authored fracture groups "+i,encounter.Destruction&&fleet.Vessels[i].GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("BREAK_"))>=18);
                yield return Capture("V02-Ship"+i+"-grip");
                foreach(float clock in new[]{37.5f,40.5f,44.5f,51f,64f,76f})
                {
                    encounter.Evaluate(clock,true);encounter.RenderPose();yield return Capture("V02-Ship"+i+"-t"+clock.ToString("00.0",System.Globalization.CultureInfo.InvariantCulture));
                    if(clock==64)
                    {
                        // The capture intentionally froze poses while the HDRP ocean kept advancing.
                        // Re-evaluate now, then compare actual transforms with an independent sample in this frame.
                        encounter.RenderPose();
                        Check("Actual parts float "+i,encounter.Destruction.FloatingCount>=10,new{encounter.Destruction.ReleasedCount,encounter.Destruction.FloatingCount,encounter.Destruction.LargestWaveError});
                        float actualError=0;
                        foreach(var part in fleet.Vessels[i].GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("BREAK_Plank")))actualError=Mathf.Max(actualError,Mathf.Abs(part.position.y-ShipDestruction.Sample(part.position)-.16f));
                        Check("Actual fragment transforms follow HDRP water "+i,actualError<.1f,new{actualError});
                        Check("Wreck photo "+i,encounter.WreckInFrame(s.Player.View));
                    }
                }
                encounter.StopPreview();
            }
            // Different anatomy and photo keys on the same actual camera.
            encounter.Preview(true,false);encounter.FrozenPreview=true;encounter.Evaluate(38,true);encounter.RenderPose();env.SetWeather(CoastalWeather.Cloudy,true);env.SetHour(14);s.Storm=true;
            yield return Capture("V02-Octopus");s.Director.PhotoTarget(s.Player.View,out var key,out var label,out var price);
            Check("Octopus uses small vessel and own label",fleet.Selected==0&&!encounter.Colossal&&key.StartsWith("octopus"),new{key,label,price});
            encounter.StopPreview();
            // Night is a real weather/time configuration, with normal FOV.
            fleet.Select(1);encounter.Preview(true);encounter.FrozenPreview=true;env.SetWeather(CoastalWeather.Storm,true);env.SetHour(19.2f);encounter.Evaluate(38,true);encounter.RenderPose();
            yield return new WaitForSecondsRealtime(3);yield return Capture("V02-Kraken-night");
            s.Player.View.fieldOfView=40;yield return Capture("V02-Kraken-night-zoom");s.Player.View.fieldOfView=72;
            encounter.Evaluate(52,true);encounter.RenderPose();yield return Capture("V02-Kraken-night-sinking");
            encounter.StopPreview();s.Director.FreezePreview=true;
            // Every redesigned phenomenon gets a real world capture, stable exposure and independent motion check.
            for(int kind=0;kind<s.Observations.Effects.Length;kind++)
            {
                s.Observations.Clear();env.SetWeather(kind==2?CoastalWeather.Storm:kind==3?CoastalWeather.Cloudy:CoastalWeather.Clear,true);env.SetHour(kind==2||kind==3?14:21.5f);
                var e=s.Observations.Spawn(kind,true);e.Frozen=true;e.Evaluate(kind==1?7.8f:8);
                s.Player.Teleport(s.Observation.position,e.Focus);yield return new WaitForSecondsRealtime(2.4f);
                yield return Capture("V02-Event-"+kind);
                Check("Photographic evidence event "+kind,e.VisibleIn(s.Player.View),new{e.Definition.Name,e.VertexCount});
                var mesh=e.GetComponent<MeshFilter>().sharedMesh;var before=mesh.vertices;e.Evaluate(kind==1?8.4f:10);var after=mesh.vertices;
                Check("Animated geometry event "+kind,before.Where((v,i)=>(v-after[i]).sqrMagnitude>.01f).Count()>before.Length/10);
            }
            s.Observations.Clear();s.Observations.SpawnGhost(true);s.Observations.Ghost.Frozen=true;s.Observations.Ghost.Evaluate(15);s.Player.Teleport(s.Observation.position,s.Observations.Ghost.Focus);
            yield return Capture("V02-Ghost");Check("Ghost vessel photo",s.Observations.Ghost.VisibleIn(s.Player.View));s.Observations.Clear();
            // Admission cannot use the narrower photo crop to spawn in ordinary peripheral view.
            s.Player.Teleport(s.Observation.position,Vector3.back*100);var camera=s.Player.View;
            Check("In-view bounds rejected",!SeaPlacement.OutsideView(camera,new Bounds(camera.transform.position+camera.transform.forward*90,Vector3.one*20)));
            Check("Behind-camera bounds accepted",SeaPlacement.OutsideView(camera,new Bounds(camera.transform.position-camera.transform.forward*90,Vector3.one*20)));
            camera.fieldOfView=40;
            Check("Zoom keeps ordinary-view admission guard",!SeaPlacement.OutsideView(camera,new Bounds(camera.transform.position+Quaternion.AngleAxis(37,Vector3.up)*camera.transform.forward*90,Vector3.one*10)));
            camera.fieldOfView=72;
            int accepted=0,violation=0;var placements=new List<Vector3>();
            for(int i=0;i<40;i++)
            {
                s.Observations.Clear();var e=s.Observations.Spawn(i%8);if(!e)continue;accepted++;placements.Add(e.transform.position);
                var bounds=e.GetComponent<MeshRenderer>().bounds;if(!SeaPlacement.OutsideView(camera,bounds,0))violation++;
            }
            Check("Runtime spawns stay outside FPS view",accepted>5&&violation==0,new{accepted,violation});
            Check("Spawns vary distance and bearing",placements.Select(p=>Mathf.RoundToInt(p.magnitude)).Distinct().Count()>8);
            s.Observations.Clear();
            // Current destruction is reset before a new night, without orphaned audio or geometry.
            encounter.BeginNight();Check("Encounter reset",!s.Director.BoatVisible&&!s.Director.KrakenVisible&&!encounter.WreckVisible&&(!encounter.Presentation.Spray||encounter.Presentation.Spray.particleCount==0));
            // Lens rain: outside accumulation, then no new drops under the actual ceiling.
            env.SetWeather(CoastalWeather.Rain,true);env.SetHour(16);s.Player.Teleport(s.Observation.position,Vector3.back*120);yield return new WaitForSecondsRealtime(4);
            var lens=env.GetComponent<RainLens>();Check("Rain accumulates outside",!lens.Sheltered&&lens.Wetness>.5f,new{lens.Sheltered,lens.Wetness,env.RainAmount});yield return Capture("V02-Rain-outside");
            var bed=FindObjectsByType<BlockoutInteractable>().FirstOrDefault(x=>x.name.Contains("Bed"));
            Vector3 interior=bed?bed.transform.position+Vector3.right:Vector3.up*5;
            s.Player.Teleport(interior,interior+Vector3.forward);yield return new WaitForSecondsRealtime(3);
            Check("Rain drains under roof",lens.Sheltered&&lens.Wetness<.03f,new{lens.Sheltered,lens.Wetness,position=s.Player.transform.position.ToString()});yield return Capture("V02-Rain-inside");
            s.Player.Teleport(s.Observation.position,Vector3.back*140);env.SetWeather(CoastalWeather.Clear,true);env.SetHour(21.5f);
            yield return new WaitForSecondsRealtime(2);yield return Capture("V02-Night-fleet");
            Check("Permanent traffic survives all events",s.Director.Traffic.Ships.Count(v=>v.gameObject.activeInHierarchy)>=4);
            s.Player.enabled=true;s.Player.InputEnabled=true;s.Player.LockCursor(false);
        }
        void OnDestroy(){if(frame)Destroy(frame);}
    }
}
#endif
