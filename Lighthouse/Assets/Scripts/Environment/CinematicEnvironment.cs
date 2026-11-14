using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace LighthouseGame
{
    [DefaultExecutionOrder(-100)]
    public class CinematicEnvironment : MonoBehaviour
    {
        public static CinematicEnvironment Active { get; private set; }
        public CoastalWeatherPreset[] Presets;
        public CoastalWeather Weather=CoastalWeather.Clear;
        [Range(0,24)] public float Hour=17.2f;
        public bool AdvanceTime=true, AutoWeather=false, CleanView;
        [Min(60)] public float DayDurationSeconds=1200;
        [Min(1)] public float TransitionSeconds=14;
        public Volume Atmosphere;
        public WaterSurface Ocean;
        public Light Sun, Moon, Lightning;
        public LightningBolt Bolt;
        public ParticleSystem RainParticles, SprayParticles;
        public WaterDecal ShoreFoam;
        public CoastalAmbience Ambience;
        public BlockoutSession Session;
        public Material[] WetMaterials;
        public float Wind, RainAmount, Wetness, Visibility, CloudDensity;
        public int LightningCount;
        PhysicallyBasedSky sky;
        VolumetricClouds clouds;
        Fog fog;
        Exposure exposure;
        VisualEnvironment visual;
        ColorAdjustments grade;
        float cloudShape, cloudErosion, cloudAltitude, cloudDepth, foam, sunDimmer, swell;
        Color fogTint;
        float nextWeather, nextLightning, flashEnd, flashPower;
        private int _scheduledWeather = -1;
        private bool _weatherForecastOffered, _lightningForecastOffered;
        public CoastalWeather ScheduledWeather => _scheduledWeather < 0 ? Weather : (CoastalWeather)_scheduledWeather;
        int observedDay;
        bool wasRunning, wasIndoors;
        float[] drySmoothness;
        float[] dryRemapMin, dryRemapMax;
        Color[] dryColors;
        private float _appliedWetness = float.NaN;
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int SmoothnessRemapMinId = Shader.PropertyToID("_SmoothnessRemapMin");
        private static readonly int SmoothnessRemapMaxId = Shader.PropertyToID("_SmoothnessRemapMax");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        BlockoutHUD hud;
        BlockoutLabel[] labels;
        System.Random rng;
        public string WeatherName => Presets != null && Presets.Length> (int)Weather ? Presets[(int)Weather].DisplayName : Weather.ToString();
        public string ClockLabel => Mathf.FloorToInt(Hour).ToString("00")+":"+Mathf.FloorToInt((Hour%1)*60).ToString("00");
        public bool LightningActive => (Bolt && Bolt.OccupiesEventSlot) || Time.time < flashEnd;
        void Awake()
        {
            Active=this; rng=new System.Random();
            CacheVolume(true); CacheMaterials();
            hud=FindAnyObjectByType<BlockoutHUD>(); labels=FindObjectsByType<BlockoutLabel>();
            ApplyInstant(); nextWeather=Time.time+150; nextLightning=Time.time+20;
        }
        public void CacheVolume(bool clone)
        {
            if(!Atmosphere) return;
            var p=clone?Atmosphere.profile:Atmosphere.sharedProfile;
            p.TryGet(out sky);p.TryGet(out clouds);p.TryGet(out fog);p.TryGet(out exposure);p.TryGet(out visual);p.TryGet(out grade);
        }
        void CacheMaterials()
        {
            if(WetMaterials==null) return;
            drySmoothness=new float[WetMaterials.Length];dryColors=new Color[WetMaterials.Length];
            dryRemapMin=new float[WetMaterials.Length];dryRemapMax=new float[WetMaterials.Length];
            for(int i=0;i<WetMaterials.Length;i++) { drySmoothness[i]=WetMaterials[i].GetFloat(SmoothnessId);dryColors[i]=WetMaterials[i].GetColor(BaseColorId);dryRemapMin[i]=WetMaterials[i].GetFloat(SmoothnessRemapMinId);dryRemapMax[i]=WetMaterials[i].GetFloat(SmoothnessRemapMaxId); }
            _appliedWetness = float.NaN;
        }
        public void SetWeather(CoastalWeather weather, bool instant=false)
        {
            if (Session && Session.IsTutorialDay) { weather = CoastalWeather.Clear; instant = true; }
            Weather=weather;
            if(Session) Session.Storm=weather==CoastalWeather.Storm;
            nextLightning=Time.time+UnityEngine.Random.Range(9f,19f); _lightningForecastOffered = false;
            Session?.Radio?.CancelForecast("lightning");
            if(instant) ApplyInstant();
        }
        public void BeginDay(int day)
        {
            observedDay = day;
            if (day == 1)
            {
                SetWeather(CoastalWeather.Clear, true);
                if (Bolt) Bolt.Clear();
                flashEnd = flashPower = 0;
                if (Lightning) Lightning.intensity = 0;
                if (RainParticles) RainParticles.Clear();
                if (SprayParticles) SprayParticles.Clear();
                if (Ambience) Ambience.ClearStorm();
                ApplyPrecipitation();
            }
            else if (Presets != null && Presets.Length > 0)
            {
                SetWeather((CoastalWeather)rng.Next(Presets.Length));
            }
            ScheduleWeather();
        }
        private void ScheduleWeather()
        {
            nextWeather = Time.time + UnityEngine.Random.Range(130f, 210f);
            _scheduledWeather = Presets.Length > 1 ? ((int)Weather + 1 + rng.Next(Presets.Length - 1)) % Presets.Length : 0;
            _weatherForecastOffered = false;
        }
        public void NextWeather()
        {
            SetWeather((CoastalWeather)(((int)Weather+1)%Presets.Length));
            if(Session) Session.Notify(WeatherName+" · плавная смена погоды");
        }
        public void SetHour(float hour) { Hour=Mathf.Repeat(hour,24); ApplyLighting(); }
        public void NextTime()
        {
            float[] hours={6.2f,9,13,17.4f,19.5f,0}; int closest=0;
            for(int i=1;i<hours.Length;i++) if(Mathf.Abs(Mathf.DeltaAngle(Hour*15,hours[i]*15))<Mathf.Abs(Mathf.DeltaAngle(Hour*15,hours[closest]*15))) closest=i;
            SetHour(hours[(closest+1)%hours.Length]);
            if(Session) Session.Notify("Время: "+ClockLabel+" · F6 — движение времени");
        }
        public void ToggleCleanView()
        {
            if (Session && Session.Player) Session.Player.ToggleUI();
        }
        public void ApplyInstant()
        {
            if(Presets==null||Presets.Length==0) return;
            if(sky==null) CacheVolume(false);
            BlendPreset(1);Wetness=Presets[(int)Weather].Wetness;ApplyLighting();ApplyWater();
        }
        void BlendPreset(float t)
        {
            var p=Presets[(int)Weather];
            Wind=Mathf.Lerp(Wind,p.WindKph,t);RainAmount=Mathf.Lerp(RainAmount,p.Rain,t);Wetness=Mathf.Lerp(Wetness,p.Wetness,t*.5f);
            Visibility=Mathf.Lerp(Visibility,p.Visibility,t);CloudDensity=Mathf.Lerp(CloudDensity,p.CloudDensity,t);
            cloudShape=Mathf.Lerp(cloudShape,p.CloudShape,t);cloudErosion=Mathf.Lerp(cloudErosion,p.CloudErosion,t);
            cloudAltitude=Mathf.Lerp(cloudAltitude,p.CloudAltitude,t);cloudDepth=Mathf.Lerp(cloudDepth,p.CloudDepth,t);
            foam=Mathf.Lerp(foam,p.Foam,t);sunDimmer=Mathf.Lerp(sunDimmer,p.SunDimmer,t);swell=Mathf.Lerp(swell,p.Swell,t);fogTint=Color.Lerp(fogTint,p.FogTint,t);
        }
        void Update()
        {
            if(!Ocean||!Atmosphere||Presets==null||Presets.Length==0) return;
            if(Session)
            {
                if(Session.Running&&!wasRunning){Hour=19.2f;observedDay=0;}
                if(Session.Running&&Session.Day!=observedDay)
                {
                    BeginDay(Session.Day);
                }
                wasRunning=Session.Running;
            }
            if(AdvanceTime) Hour=Mathf.Repeat(Hour+Time.deltaTime*24/DayDurationSeconds,24);
            bool dynamicWeather = Session && Session.Running
                ? Session.Day >= 2 && !Session.GameOver && !Session.Sleeping && Session.Preparation <= 0
                : AutoWeather;
            if (dynamicWeather && !_weatherForecastOffered && Time.time >= nextWeather - 14)
            {
                _weatherForecastOffered = true;
                string[] keys = { "weather-clear", "weather-cloudy", "weather-fog", "weather-rain", "weather-storm" };
                if (_scheduledWeather >= 0 && _scheduledWeather < keys.Length)
                    Session?.Radio?.ConsiderForecast(keys[_scheduledWeather], nextWeather - Time.time);
            }
            if(dynamicWeather&&Time.time>nextWeather)
            {
                // A scheduled transition always chooses another profile; the next day's roll is independent.
                int next = _scheduledWeather >= 0 ? _scheduledWeather : (int)Weather;
                SetWeather((CoastalWeather)next);ScheduleWeather();
            }
            BlendPreset(1-Mathf.Exp(-Time.deltaTime*3/TransitionSeconds));ApplyLighting();ApplyWater();ApplyPrecipitation();
            if (Weather == CoastalWeather.Storm && !_lightningForecastOffered && Time.time >= nextLightning - 10
                && Session && Session.CanStartEvent)
            { _lightningForecastOffered = true; Session.Radio?.ConsiderForecast("lightning", nextLightning - Time.time); }
            if(Weather==CoastalWeather.Storm&&Time.time>nextLightning&&(!Session||!Session.Running||(!Session.GameOver&&Session.Preparation<=0&&!Session.Sleeping))) TriggerLightning();
            if(Lightning) Lightning.intensity=Bolt?flashPower*Bolt.Brightness:(Time.time<flashEnd?flashPower:0);
            ApplyWetness();
        }
        private void ApplyWetness()
        {
            if (drySmoothness == null || Wetness == _appliedWetness) return;
            float colorMultiplier = Mathf.Lerp(1, .72f, Wetness);
            for (int i = 0; i < WetMaterials.Length; i++)
            {
                WetMaterials[i].SetFloat(SmoothnessId, Mathf.Lerp(drySmoothness[i], .72f, Wetness));
                WetMaterials[i].SetFloat(SmoothnessRemapMinId, Mathf.Lerp(dryRemapMin[i], .55f, Wetness));
                WetMaterials[i].SetFloat(SmoothnessRemapMaxId, Mathf.Lerp(dryRemapMax[i], .93f, Wetness));
                WetMaterials[i].SetColor(BaseColorId, dryColors[i] * colorMultiplier);
            }
            _appliedWetness = Wetness;
        }
        public void TriggerLightning()
        {
            if(!Lightning) return;
            if (Session && !Session.CanStartEvent) { nextLightning = Time.time + 1.2f; return; }
            LightningCount++;flashEnd=Time.time+.18f;flashPower=22000;
            if(Bolt)
            {
                Vector3 direction=Quaternion.Euler(0,UnityEngine.Random.Range(-72f,72f),0)*Vector3.back;
                Bolt.StrikeAt(direction*UnityEngine.Random.Range(260f,460f));
            }
            Lightning.transform.rotation=Quaternion.Euler(UnityEngine.Random.Range(25,70),UnityEngine.Random.Range(-80,80),0);
            if(Ambience) Ambience.Thunder(UnityEngine.Random.Range(1.1f,3.8f));
            nextLightning=Time.time+UnityEngine.Random.Range(18,38); _lightningForecastOffered = false;
        }
        void ApplyLighting()
        {
            if(sky==null||!Sun||!Moon) return;
            float elevation=Mathf.Sin((Hour-6)/24*Mathf.PI*2)*68;
            float daylight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-8,22,elevation));
            Sun.transform.rotation=Quaternion.Euler(elevation,(Hour-18)*12,0);
            Sun.intensity=Mathf.Lerp(0,85000,Mathf.SmoothStep(0,1,Mathf.InverseLerp(-3,14,elevation)))*Mathf.Max(.12f,sunDimmer);
            Sun.color=Color.Lerp(new Color(1,.63f,.34f),new Color(1,.97f,.9f),Mathf.InverseLerp(0,28,elevation));
            Moon.transform.rotation=Quaternion.Euler(20+Mathf.Max(0,-elevation)*.18f,Mathf.DeltaAngle(0,Hour*15)*.35f-12,0);
            // Thick clouds need a stronger source; clear skies must retain a true night value.
            float nightLux=Mathf.Lerp(1.6f,18f,Mathf.Pow(Mathf.InverseLerp(.32f,.72f,CloudDensity),2));
            Moon.intensity=Mathf.Lerp(nightLux,0,daylight); Moon.color=new Color(.72f,.83f,1);
            // HDRP selects its atmosphere light from the shadow-casting directional light.
            // Keep exactly one: otherwise the shadow atlas fails and the night sky stays black.
            bool moonlit=Sun.intensity<Moon.intensity;
            Sun.shadows=moonlit?LightShadows.None:LightShadows.Soft;
            Moon.shadows=moonlit?LightShadows.Soft:LightShadows.None;
            RenderSettings.sun=moonlit?Moon:Sun;
            float lightLevel=Mathf.Log(Mathf.Max(3.5f,Sun.intensity+Moon.intensity/5.14f),2);
            float outdoorExposure=Mathf.Lerp(.6f,12f,Mathf.InverseLerp(Mathf.Log(3.5f,2),Mathf.Log(85000,2),lightLevel));
            bool interior=Camera.main&&Physics.Raycast(Camera.main.transform.position,Vector3.up,15,~(1<<2),QueryTriggerInteraction.Ignore);
            float targetExposure=interior?Mathf.Max(2.8f,outdoorExposure):outdoorExposure;
            exposure.fixedExposure.value=Application.isPlaying?Mathf.Lerp(exposure.fixedExposure.value,targetExposure,1-Mathf.Exp(-Time.deltaTime*3)):targetExposure;
            float mist=1-Mathf.InverseLerp(90,1200,Visibility);
            float overcast=Mathf.Max(Mathf.InverseLerp(18,62,Wind),mist);
            if(grade){grade.colorFilter.Override(Color.Lerp(Color.white,new Color(.76f,.86f,1),overcast));grade.saturation.value=Mathf.Lerp(-6,-12,overcast);}
            sky.aerosolDensity.value=Mathf.Lerp(.035f,.11f,1-sunDimmer);
            sky.spaceEmissionMultiplier.value=Mathf.Lerp(.075f,0,daylight);
            sky.spaceRotation.value=new Vector3(18,Hour*15,14);
            sky.horizonTint.value=Color.Lerp(new Color(.72f,.85f,1),Color.white,daylight);
            clouds.densityMultiplier.value=CloudDensity;clouds.shapeFactor.value=cloudShape;clouds.erosionFactor.value=cloudErosion;
            clouds.bottomAltitude.value=cloudAltitude;clouds.altitudeRange.value=cloudDepth;
            clouds.shadowOpacity.value=Mathf.Lerp(.28f,.85f,CloudDensity);
            visual.windSpeed.value=Wind;visual.windOrientation.value=235;
            fog.meanFreePath.value=Mathf.Max(30,Visibility);fog.baseHeight.value=Mathf.Lerp(-3,12,mist);fog.maximumHeight.value=Mathf.Lerp(95,180,mist);
            fog.tint.value=fogTint; fog.albedo.value=Color.Lerp(new Color(.65f,.74f,.8f),Color.white,daylight);
        }
        void ApplyWater()
        {
            if(!Ocean)return;
            Ocean.largeWindSpeed=Wind;Ocean.ripplesWindSpeed=Mathf.Lerp(4,24,Mathf.InverseLerp(7,65,Wind));
            Ocean.largeBand0Multiplier=swell;Ocean.largeBand1Multiplier=Mathf.Lerp(.55f,1,Mathf.InverseLerp(10,60,Wind));
            Ocean.simulationFoamAmount=foam;Ocean.foamPersistenceMultiplier=Mathf.Lerp(.4f,.75f,Mathf.InverseLerp(10,65,Wind));
            if(ShoreFoam) {ShoreFoam.surfaceFoamDimmer=Mathf.Lerp(.55f,.95f,Mathf.InverseLerp(8,65,Wind));ShoreFoam.amplitude=Mathf.Lerp(.1f,.55f,Mathf.InverseLerp(8,65,Wind));}
        }
        void ApplyPrecipitation()
        {
            bool indoors=Camera.main&&Physics.Raycast(Camera.main.transform.position,Vector3.up,65,~(1<<2),QueryTriggerInteraction.Ignore);
            if(RainParticles)
            {
                if(indoors&&!wasIndoors) RainParticles.Clear();
                RainParticles.transform.position=Camera.main?Camera.main.transform.position+Vector3.up*11:Vector3.up*20;
                var e=RainParticles.emission;e.rateOverTime=indoors?0:RainAmount*2200;
                var v=RainParticles.velocityOverLifetime;v.x=-Wind*.1f;v.z=Wind*.05f;
            }
            if(SprayParticles){var e=SprayParticles.emission;e.rateOverTime=Mathf.Lerp(8,130,Mathf.InverseLerp(10,65,Wind));}
            if(Ambience) Ambience.SetMix(Wind,RainAmount,indoors,Hour);
            wasIndoors=indoors;
        }
        void OnDestroy()
        {
            if(Active==this) Active=null;
            if(drySmoothness!=null)for(int i=0;i<WetMaterials.Length;i++)if(WetMaterials[i]){WetMaterials[i].SetFloat(SmoothnessId,drySmoothness[i]);WetMaterials[i].SetFloat(SmoothnessRemapMinId,dryRemapMin[i]);WetMaterials[i].SetFloat(SmoothnessRemapMaxId,dryRemapMax[i]);WetMaterials[i].SetColor(BaseColorId,dryColors[i]);}
        }
    }
}
