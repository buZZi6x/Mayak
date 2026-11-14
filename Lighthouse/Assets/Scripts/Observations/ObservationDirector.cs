using System.Collections.Generic;
using UnityEngine;

namespace LighthouseGame
{
    [DefaultExecutionOrder(80)]
    public class ObservationDirector:MonoBehaviour
    {
        public BlockoutSession Session;
        public Material EffectMaterial;
        public Material WaterMaterial;
        public SkyGhostShip Ghost;
        public bool Automatic=true;
        public ObservationVfx[] Effects {get;private set;}
        public int SpawnCount {get;private set;}
        public int ActiveCount {get{int n=Ghost&&Ghost.Showing?1:0;if(Effects!=null)foreach(var e in Effects)if(e.Showing)n++;return n;}}
        public float NextAt {get;private set;}
        public int LastKind=-1;
        System.Random random;
        int preview=-1;
        bool readyLast;
        public int PlannedKind { get; private set; } = -1;
        private float _plannedAt;
        float[] lastUsed;
        private static readonly float[] GhostWeatherWeights = { 1, 1, 3, 1, 0 };
        void Awake()
        {
            random=new System.Random();Effects=new ObservationVfx[ObservationKernels.Catalog.Length];lastUsed=new float[Effects.Length+1];lastUsed[Effects.Length]=-1000;
            for(int i=0;i<Effects.Length;i++){var go=new GameObject(ObservationKernels.Catalog[i].Name);go.transform.SetParent(transform);Effects[i]=go.AddComponent<ObservationVfx>();Effects[i].Initialize(i,EffectMaterial,WaterMaterial);lastUsed[i]=-1000;}
            NextAt=Time.time+8;
        }
        float Rand(float a,float b)=>Mathf.Lerp(a,b,(float)random.NextDouble());
        public void ResetDay()
        {
            Clear();readyLast=false;LastKind=-1;PlannedKind=-1;if(lastUsed!=null)for(int i=0;i<lastUsed.Length;i++)lastUsed[i]=-1000;
            NextAt=Time.time+Rand(6,11);
        }
        public void Clear(){if(Effects!=null)foreach(var e in Effects)e.Hide();if(Ghost)Ghost.Hide();}
        void Update()
        {
            if(Session.Sleeping)return; // Existing phenomena complete behind the bed fade; no new spawns.
            bool ready=Session.SpecialEventsUnlocked&&(!Session.Running||(!Session.GameOver&&Session.Preparation<=0));
            if(!ready){if(readyLast||ActiveCount>0)Clear();readyLast=false;PlannedKind=-1;return;}
            if(!readyLast){NextAt=Time.time+Rand(6,11);readyLast=true;}
            if (PlannedKind >= 0 && (Weight(PlannedKind) <= 0 || Time.time - _plannedAt > 24))
            {
                Session.Radio?.CancelForecast(PlannedKind == Effects.Length ? "ghostship" : ObservationKernels.Catalog[PlannedKind].Key);
                PlannedKind = -1;
            }
            if (Automatic && PlannedKind < 0 && Time.time >= NextAt - 8 && ActiveCount < 2 && Session.CanStartEvent)
            {
                PlannedKind = Choose(); _plannedAt = Time.time;
                if (PlannedKind >= 0)
                    Session.Radio?.ConsiderForecast(PlannedKind == Effects.Length ? "ghostship" : ObservationKernels.Catalog[PlannedKind].Key, NextAt - Time.time);
            }
            if(!Automatic||Time.time<NextAt||ActiveCount>=2||!Session.CanStartEvent)return;
            int before=SpawnCount;int kind=PlannedKind >= 0 ? PlannedKind : Choose();if(kind==Effects.Length)SpawnGhost();else if(kind>=0)Spawn(kind);
            if (SpawnCount > before) PlannedKind = -1;
            NextAt=Time.time+(SpawnCount>before?Rand(18,27):1.2f);
        }
        public float Weight(int kind)
        {
            if (!Session.SpecialEventsUnlocked) return 0;
            var env=CinematicEnvironment.Active;
            int weather=env?(int)env.Weather:0;float hour=env?env.Hour:0;
            if(kind==Effects.Length)return Ghost&&(hour<=7||hour>=18)?GhostWeatherWeights[weather]:0;
            var d=ObservationKernels.Catalog[kind];
            if(d.Night&&hour>7&&hour<18)return 0;
            return d.Weather[weather];
        }
        int Choose()
        {
            float sum=0;for(int i=0;i<lastUsed.Length;i++)if(!Showing(i)&&i!=LastKind&&Time.time-lastUsed[i]>45)sum+=Weight(i);
            if(sum<=0)return -1;
            float choice=Rand(0,sum);
            for(int i=0;i<lastUsed.Length;i++)if(!Showing(i)&&i!=LastKind&&Time.time-lastUsed[i]>45){choice-=Weight(i);if(choice<=0)return i;}
            return -1;
        }
        bool Showing(int i)=>i==Effects.Length?Ghost&&Ghost.Showing:Effects[i].Showing;
        public void SpawnGhost(bool centered=false)
        {
            if (Session.Running && (!Session.CanStartEvent || ActiveCount >= 2)) return;
            if(!Ghost)return;Vector3 outward=Session.SeaEvent.position;outward.y=0;outward.Normalize();Vector3 position=outward*225;
            if(!centered&&!SeaPlacement.TryFind(Session.Player.View,outward,190,275,new Vector3(180,140,180),65,out position))return;
            Ghost.Show(position,Quaternion.LookRotation(-position.normalized));LastKind=Effects.Length;lastUsed[Effects.Length]=Time.time;SpawnCount++;
        }
        public ObservationVfx Spawn(int kind,bool centered=false)
        {
            if (Session.Running && (!Session.CanStartEvent || ActiveCount >= 2)) return null;
            kind=Mathf.Clamp(kind,0,Effects.Length-1);
            Vector3 outward=Session.SeaEvent.position;outward.y=0;outward.Normalize();
            float near=kind==0?280:kind==1?230:kind==2?340:kind==7?65:kind==3?65:kind==4?65:kind==5?75:100;
            float far=kind==0?370:kind==1?320:kind==2?470:kind==7?100:kind==3?100:kind==4?110:kind==5?115:155;
            float width=kind==0?350:kind==1?390:kind==2?300:kind==7?155:kind==6?155:110;
            float height=kind==0?230:kind==1?140:kind==2?570:kind==7?16:kind==3?16:50;
            if(kind==2&&CinematicEnvironment.Active.Atmosphere.profile.TryGet<UnityEngine.Rendering.HighDefinition.VolumetricClouds>(out var clouds))height=clouds.bottomAltitude.value*1.02f+20;
            Vector3 position=outward*((near+far)*.5f);
            if(!centered&&!SeaPlacement.TryFind(Session.Player.View,outward,near,far,new Vector3(width,height,width),height*.5f,out position))return null;
            // Recheck immediately before activation; never force a visible fallback.
            if(!centered&&!SeaPlacement.OutsideView(Session.Player.View,new Bounds(position+Vector3.up*height*.5f,new Vector3(width,height,width))))return null;
            Effects[kind].Show(position,Quaternion.LookRotation(-position.normalized));
            if(!centered&&!SeaPlacement.OutsideView(Session.Player.View,Effects[kind].GetComponent<MeshRenderer>().bounds)){Effects[kind].Hide();return null;}
            LastKind=kind;lastUsed[kind]=Time.time;SpawnCount++;return Effects[kind];
        }
        public void PreviewNext()
        {
            if(Session.Running)return;Automatic=false;Clear();preview=(preview+1)%(Effects.Length+(Ghost?1:0));if(Session.Director.Encounter)Session.Director.Encounter.StopPreview();Session.Director.FreezePreview=false;
            var env=CinematicEnvironment.Active;env.SetWeather(preview==2?CoastalWeather.Storm:CoastalWeather.Clear,true);env.SetHour(preview==2?17.5f:21.5f);env.AdvanceTime=false;
            if(preview==Effects.Length){SpawnGhost(true);Session.Player.Teleport(Session.Observation.position,Ghost.Focus);Session.Notify("Небесный призрачный корабль · 240 очков · F5 — следующее явление");return;}
            var e=Spawn(preview,true);Session.Player.Teleport(Session.Observation.position,e.Focus);
            Session.Notify(e.Definition.Name+" · "+e.Definition.Points+" очков · F5 — следующее явление");
        }
        public void CollectVisible(Camera camera,List<ObservationVfx> result)
        {if(Effects!=null)foreach(var e in Effects)if(e.VisibleIn(camera))result.Add(e);}
    }
}
