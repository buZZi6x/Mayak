using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace LighthouseGame
{
    [DefaultExecutionOrder(90)]
    public sealed class LeviathanEncounter : MonoBehaviour
    {
        public const int PhotoPoints = 1000;
        public const string PhotoKey = "leviathan";
        [SerializeField] private BlockoutSession _session;
        [SerializeField] private SkinnedMeshRenderer _body;
        [SerializeField] private Transform[] _spine;
        [SerializeField] private Material _sprayMaterial;
        [SerializeField] private Material _foamMaterial;
        [SerializeField] private AudioClip _splashClip;
        [SerializeField] private AudioClip _roarClip;
        private readonly LeviathanAppearance _appearance = new LeviathanAppearance();
        private readonly List<Vector3> _photoVertices = new List<Vector3>();
        private Mesh _photoPose;
        private ParticleSystem _spray;
        private readonly WaterDecal[] _foam = new WaterDecal[2];
        private AudioSource _audio;
        private AudioSource _roar;
        private bool _audioPaused;
        private Quaternion _heading;
        private Vector3 _anchor;
        private float _wait, _retry, _previousAge, _drip;
        private int _heldFrame = -1;
        private bool _radioForecastOffered;

        public bool Showing { get; private set; }
        public float Age { get; private set; }
        public int AppearanceDay => _appearance.Day;
        public bool Pending => _appearance.Pending;
        public bool Consumed => _appearance.Consumed;
        public int SpawnCount { get; private set; }
        public int RoarCount { get; private set; }
        public float ShakeStrength { get; private set; }
        public Bounds LastAdmissionBounds { get; private set; }
        public Vector3 Focus => _anchor + Vector3.up * (65 * LeviathanBreach.WorldScale);

        private void Awake()
        {
            if (!_session || !_body || _spine == null || _spine.Length != LeviathanBreach.BoneCount)
            {
                Debug.LogError("Leviathan requires its session, skinned body and 27 spine bones.", this);
                enabled = false;
                return;
            }
            CreateWaterEffects();
            _session.RunBegan += ResetRun;
            ResetRun();
        }

        private void ResetRun()
        {
            Hide();
            _appearance.Reset();
            SpawnCount = 0;
            RoarCount = 0;
        }

        private void Update()
        {
            if (!_session) return;
            if (_session.GameOver || _session.Sleeping)
            {
                if (Showing) Hide();
                return;
            }
            bool paused = _session.MenuOpen || Time.timeScale <= 0;
            if (paused != _audioPaused)
            {
                _audioPaused = paused;
                if (paused) { if (_audio) _audio.Pause(); if (_roar) _roar.Pause(); }
                else { if (_audio) _audio.UnPause(); if (_roar) _roar.UnPause(); }
            }
            if (paused || Time.deltaTime <= 0) return;
            if (_session.Running && _appearance.Day != _session.Day)
            {
                Hide();
                _appearance.BeginDay(_session.Day, _session.Day >= LeviathanAppearance.FirstDay ? Random.value : 1);
                _wait = Random.Range(12f, 28f);
                _radioForecastOffered = false;
                _retry = 0;
            }
            if (Showing)
            {
                if (_heldFrame == Time.frameCount || _session.TakingPhoto) return;
                Evaluate(Age + Time.deltaTime);
                if (Age >= LeviathanBreach.Lifetime) Hide();
                return;
            }
            if (!_session.Running || !_appearance.Pending || _session.Preparation > 0) return;
            _wait -= Time.deltaTime;
            _retry -= Time.deltaTime;
            if (!_radioForecastOffered && _wait > 0 && _wait <= 12 && _session.CanStartEvent)
            { _radioForecastOffered = true; _session.Radio?.ConsiderForecast(PhotoKey, _wait); }
            if (_wait > 0 || _retry > 0 || !_session.CanStartEvent) return;
            _retry = 1.2f;
            Vector3 outward = _session.SeaEvent.position;
            outward.y = 0;
            // A separate distant layer, well beyond the fleet and other sea creatures.
            var size = new Vector3(340, 200, 340) * LeviathanBreach.WorldScale;
            if (!SeaPlacement.TryFind(_session.Player.View, outward.normalized, 650, 850, size, 55 * LeviathanBreach.WorldScale, out var position)) return;
            if (!_appearance.TryConsume()) return;
            LastAdmissionBounds = new Bounds(position + Vector3.up * (55 * LeviathanBreach.WorldScale), size);
            BeginBreach(position);
        }

        private void BeginBreach(Vector3 position)
        {
            _anchor = position;
            _anchor.y = 0;
            _heading = Quaternion.LookRotation(Vector3.Cross(Vector3.up, position.normalized));
            transform.SetPositionAndRotation(_anchor, _heading);
            transform.localScale = Vector3.one * LeviathanBreach.WorldScale;
            Showing = true;
            SpawnCount++;
            Age = 0;
            _previousAge = 0;
            _drip = 0;
            _body.enabled = true;
            _body.localBounds = new Bounds(new Vector3(0, -160, 0), new Vector3(110, 590, 290));
            Evaluate(0);
        }

        private void Evaluate(float age)
        {
            Age = age;
            for (int i = 0; i < _spine.Length; i++)
            {
                float offset = i * LeviathanBreach.SpineLength / (_spine.Length - 1);
                Vector3 point = LeviathanBreach.Point(age, offset);
                // Subtle lateral tail motion, strongest at the trailing end.
                point.x = Mathf.Sin(age * 1.7f - i * .38f) * Mathf.Pow(i / (float)(_spine.Length - 1), 1.5f) * 3;
                _spine[i].localPosition = point;
                _spine[i].localRotation = LeviathanBreach.Rotation(age, offset);
            }
            float entry = LeviathanBreach.EntryTime;
            float dive = LeviathanBreach.DiveTime;
            if (_previousAge < entry && age >= entry)
            {
                Splash(0, 210, 28, .65f);
                if (_roar && _roarClip)
                {
                    _roar.clip = _roarClip;
                    _roar.Play();
                    RoarCount++;
                }
            }
            if (_previousAge < dive && age >= dive) Splash(1, 360, 38, .9f);
            _drip += Mathf.Max(0, age - _previousAge);
            if (_drip > .1f && age > entry && age < dive + LeviathanBreach.SpineLength / LeviathanBreach.Speed)
            {
                _drip = 0;
                for (int i = 0; i < _spine.Length; i += 2)
                {
                    var p = _spine[i].position;
                    if (p.y > 4) Emit(p, 2, 1.2f, .65f);
                    if (Mathf.Abs(p.y) < 13 * LeviathanBreach.WorldScale) Emit(new Vector3(p.x, SurfaceHeight(p) + 1, p.z), 8, 18, 2.6f);
                }
            }
            for (int i = 0; i < _foam.Length; i++)
            {
                if (!_foam[i]) continue;
                float since = age - (i == 0 ? entry : dive);
                bool active = since >= 0 && since < 10;
                _foam[i].gameObject.SetActive(active);
                if (!active) continue;
                _foam[i].transform.position = SurfacePosition(i);
                _foam[i].regionSize = Vector2.one * ((36 + since * 9) * LeviathanBreach.WorldScale);
                _foam[i].surfaceFoamDimmer = Mathf.Clamp01((10 - since) / 5) * .85f;
                _foam[i].deepFoamDimmer = .25f;
            }
            if (_roar)
            {
                _roar.transform.position = _spine[0].position;
                var ambience = CinematicEnvironment.Active ? CinematicEnvironment.Active.Ambience : null;
                _roar.volume = ambience ? ambience.EffectsVolume : 1;
            }
            ShakeStrength = LeviathanBreach.ShakeEnvelope(age);
            float pitch = (Mathf.PerlinNoise(7.1f, age * 6.3f) - .5f) * 2;
            float yaw = (Mathf.PerlinNoise(age * 7.9f, 23.4f) - .5f) * 2;
            float roll = (Mathf.PerlinNoise(53.2f, age * 5.7f) - .5f) * 2;
            _session.Player.SetLeviathanShake(new Vector3(pitch, yaw * .7f, roll * 1.1f) * ShakeStrength,
                new Vector3(yaw, pitch, 0) * (.05f * ShakeStrength));
            _previousAge = age;
        }

        public bool VisibleIn(Camera camera) => Showing && PhotoEvidence.Skinned(camera, _body, ref _photoPose, _photoVertices);
        public void HoldForPhoto() => _heldFrame = Time.frameCount;

        private void Hide()
        {
            Showing = false;
            if (_body) _body.enabled = false;
            if (_spray) _spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_audio) _audio.Stop();
            if (_roar) _roar.Stop();
            _audioPaused = false;
            ShakeStrength = 0;
            if (_session && _session.Player) _session.Player.SetLeviathanShake(Vector3.zero, Vector3.zero);
            foreach (var foam in _foam) if (foam) foam.gameObject.SetActive(false);
        }

        private Vector3 SurfacePosition(int index)
        {
            Vector3 point = _anchor + _heading * new Vector3(0, 0, (index == 0 ? -LeviathanBreach.Radius : LeviathanBreach.Radius) * LeviathanBreach.WorldScale);
            point.y = SurfaceHeight(point) + .1f;
            return point;
        }

        private static float SurfaceHeight(Vector3 point)
        {
            var env = CinematicEnvironment.Active;
            if (!env || !env.Ocean) return 0;
            var query = new WaterSearchParameters { targetPositionWS = point, startPositionWS = point,
                error = .1f, maxIterations = 8, includeDeformation = true };
            return env.Ocean.ProjectPointOnWaterSurface(query, out var result) ? result.projectedPositionWS.y : 0;
        }

        private void Splash(int index, int count, float speed, float volume)
        {
            var point = SurfacePosition(index);
            Emit(point, count, speed, 4);
            if (!_audio || !_splashClip) return;
            _audio.transform.position = point;
            var ambience = CinematicEnvironment.Active ? CinematicEnvironment.Active.Ambience : null;
            _audio.volume = volume * (ambience ? ambience.EffectsVolume : 1);
            _audio.PlayOneShot(_splashClip);
        }

        private void Emit(Vector3 position, int count, float speed, float size)
        {
            if (!_spray) return;
            size *= LeviathanBreach.WorldScale;
            speed *= Mathf.Sqrt(LeviathanBreach.WorldScale);
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = Random.onUnitSphere;
                direction.y = Mathf.Abs(direction.y) * 1.7f;
                _spray.Emit(new ParticleSystem.EmitParams {
                    position = position + new Vector3(Random.Range(-9f, 9f), 0, Random.Range(-9f, 9f)) * LeviathanBreach.WorldScale,
                    velocity = direction * Random.Range(speed * .3f, speed), startSize = Random.Range(size * .4f, size) }, 1);
            }
        }

        private void CreateWaterEffects()
        {
            var go = new GameObject("Leviathan · salt spray");
            go.transform.SetParent(transform, false);
            go.layer = 2;
            _spray = go.AddComponent<ParticleSystem>();
            _spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _spray.main;
            main.playOnAwake = false; main.loop = false; main.maxParticles = 1800;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2, 4.5f);
            main.startColor = new Color(.66f, .8f, .83f, .5f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.gravityModifier = .8f;
            var emission = _spray.emission; emission.enabled = false;
            var shape = _spray.shape; shape.enabled = false;
            var color = _spray.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .08f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _sprayMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            for (int i = 0; i < _foam.Length; i++)
            {
                var water = new GameObject(i == 0 ? "Breach foam" : "Dive foam");
                water.transform.SetParent(transform, false);
                water.transform.localScale = Vector3.one / LeviathanBreach.WorldScale;
                _foam[i] = water.AddComponent<WaterDecal>();
                _foam[i].material = _foamMaterial;
                _foam[i].resolution = new Vector2Int(128, 128);
                _foam[i].updateMode = CustomRenderTextureUpdateMode.Realtime;
                water.SetActive(false);
            }
            var voice = new GameObject("Leviathan · distant water impact");
            voice.transform.SetParent(transform, false);
            _audio = voice.AddComponent<AudioSource>();
            _audio.playOnAwake = false; _audio.spatialBlend = 1; _audio.pitch = .72f;
            _audio.dopplerLevel = 0; _audio.spread = 20; _audio.minDistance = 80; _audio.maxDistance = 1500;
            _audio.rolloffMode = AudioRolloffMode.Custom;
            _audio.SetCustomCurve(AudioSourceCurveType.CustomRolloff, new AnimationCurve(
                new Keyframe(0, 1), new Keyframe(.5f, .45f), new Keyframe(1, 0)));
            var roar = new GameObject("Leviathan · recorded voice");
            roar.transform.SetParent(transform, false);
            _roar = roar.AddComponent<AudioSource>();
            _roar.playOnAwake = false; _roar.loop = false; _roar.spatialBlend = 1;
            _roar.dopplerLevel = 0; _roar.spread = 35; _roar.minDistance = 180; _roar.maxDistance = 2000;
            _roar.rolloffMode = AudioRolloffMode.Custom;
            _roar.SetCustomCurve(AudioSourceCurveType.CustomRolloff, new AnimationCurve(
                new Keyframe(0, 1), new Keyframe(.4f, .8f), new Keyframe(.75f, .3f), new Keyframe(1, 0)));
            var lowPass = roar.AddComponent<AudioLowPassFilter>(); lowPass.cutoffFrequency = 4200;
        }

        private void OnDisable() => Hide();
        private void OnDestroy()
        {
            if (_session) _session.RunBegan -= ResetRun;
            if (_photoPose) Destroy(_photoPose);
        }
    }
}
