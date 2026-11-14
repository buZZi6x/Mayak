using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace LighthouseGame
{
    [DefaultExecutionOrder(260)]
    public sealed class WeepingGirlEncounter : MonoBehaviour
    {
        public enum EncounterPhase { Weeping, Rising, Staring, Blackout, Finished }
        private const float RiseDuration = 3.2f;
        private const float StareDuration = 2.4f;
        private const float BlackoutDuration = .24f;
        private const float MaximumDistance = 45f;
        [SerializeField] private BlockoutSession _session;
        [SerializeField] private Transform _figure;
        [SerializeField] private SkinnedMeshRenderer _body;
        [SerializeField] private Material _interferenceTemplate;
        [SerializeField] private AudioSource _cry;
        [SerializeField] private AudioSource _signal;
        [SerializeField] private CoastalAmbience _ambience;
        [SerializeField] private Vector3 _seatedFocus = new Vector3(0, .95f, .2f);
        [SerializeField] private Vector3 _standingFocus = new Vector3(0, 1.52f, .04f);
        private readonly GazeExposure _gaze = new GazeExposure();
        private readonly RaycastHit[] _occlusionHits = new RaycastHit[16];
        private readonly int[] _riseIndices = new int[9];
        private Vector3 _restPosition;
        private Quaternion _restRotation;
        private float _phaseTime, _animationTime, _cryVolumeAtCommit;
        private int _lookUpIndex, _lookDownIndex, _lookLeftIndex, _lookRightIndex, _sobIndex;
        private bool _ownsControls, _admitted, _wasPaused, _terminalSignalHeld;
        private Material _interference;
        private CustomPassVolume _volume;
        private FullScreenCustomPass _copyPass, _pass;
        private AudioLowPassFilter _cryFilter;
        private MaterialPropertyBlock _weatherProperties;

        public EncounterPhase Phase { get; private set; }
        public float Exposure => _gaze.Seconds;
        public float PhaseTime => _phaseTime;
        public bool IsCommitted => Phase >= EncounterPhase.Rising && Phase <= EncounterPhase.Blackout;
        public bool IsVisible => _figure && _figure.gameObject.activeSelf;
        public Vector3 SeatedFocus => _restPosition + _restRotation * Vector3.Scale(_seatedFocus, _figure.lossyScale);
        public int AttackCount { get; private set; }

        private void Awake()
        {
            if (!_session || !_figure || !_body || !_body.sharedMesh || !_interferenceTemplate)
            {
                Debug.LogError("Weeping girl is missing its scene references.", this);
                enabled = false;
                return;
            }
            _restPosition = _figure.position;
            _restRotation = _figure.rotation;
            _cryFilter = _cry ? _cry.GetComponent<AudioLowPassFilter>() : null;
            _weatherProperties = new MaterialPropertyBlock();
            for (int i = 0; i < _riseIndices.Length; i++)
            {
                _riseIndices[i] = _body.sharedMesh.GetBlendShapeIndex("Rise_" + i.ToString("00"));
                if (_riseIndices[i] < 0)
                {
                    Debug.LogError("Weeping girl requires all nine authored rise poses.", this);
                    enabled = false;
                    return;
                }
            }
            _lookUpIndex = _body.sharedMesh.GetBlendShapeIndex("LookUp");
            _lookDownIndex = _body.sharedMesh.GetBlendShapeIndex("LookDown");
            _lookLeftIndex = _body.sharedMesh.GetBlendShapeIndex("LookLeft");
            _lookRightIndex = _body.sharedMesh.GetBlendShapeIndex("LookRight");
            _sobIndex = _body.sharedMesh.GetBlendShapeIndex("Sob");
            _interference = new Material(_interferenceTemplate) { name = "Weeping girl · analogue signal loss" };
            _volume = gameObject.AddComponent<CustomPassVolume>();
            _volume.isGlobal = true;
            // Grain is a damaged image, so it must survive exposure, TAA and motion blur.
            _volume.injectionPoint = CustomPassInjectionPoint.AfterPostProcess;
            _volume.targetCamera = _session.Player.View;
            _volume.priority = 100;
            _copyPass = new FullScreenCustomPass { name = "Weeping girl · preserve image", fullscreenPassMaterial = _interference, materialPassName = "Copy", targetColorBuffer = CustomPass.TargetBuffer.Custom, targetDepthBuffer = CustomPass.TargetBuffer.None };
            _pass = new FullScreenCustomPass { name = "Weeping girl · analogue interference", fullscreenPassMaterial = _interference, materialPassName = "Interference", targetDepthBuffer = CustomPass.TargetBuffer.None };
            _volume.customPasses.Add(_copyPass);
            _volume.customPasses.Add(_pass);
            _session.RunBegan += ResetEncounter;
            ResetEncounter();
        }

        public void ResetEncounter()
        {
            ReleaseControls();
            _gaze.Reset();
            Phase = EncounterPhase.Weeping;
            _phaseTime = 0;
            _animationTime = 0;
            _terminalSignalHeld = false;
            _admitted = !_session.Running;
            _figure.SetPositionAndRotation(_restPosition, _restRotation);
            _figure.gameObject.SetActive(_admitted);
            Pose(0);
            StopAudio();
            SetSignal(0, 0);
        }

        private bool Eligible => _session.Running && !_session.GameOver && !_session.Sleeping && _session.Preparation <= 0;

        private void LateUpdate()
        {
            if (_body)
            {
                _weatherProperties.SetFloat("_Visibility", CinematicEnvironment.Active ? CinematicEnvironment.Active.Visibility : 1000);
                _body.SetPropertyBlock(_weatherProperties);
            }
            if (Time.timeScale <= 0)
            {
                if (!_wasPaused) { PauseAudio(true); _wasPaused = true; }
                return;
            }
            if (_wasPaused) { PauseAudio(false); _wasPaused = false; }
            if (_session.GameOver)
            {
                if (Phase != EncounterPhase.Finished) CancelEncounter();
                return;
            }
            if (IsCommitted)
            {
                // After the threshold only a restarted/ended run can cancel the death.
                if (!_session.Running) { CancelEncounter(); return; }
                AdvanceEncounter(Time.deltaTime);
                return;
            }
            if (Phase == EncounterPhase.Finished) return;
            _animationTime += Time.deltaTime;
            if (_session.Running && (_session.Preparation > 0 || _session.Sleeping))
            {
                _gaze.Reset();
                _figure.gameObject.SetActive(false);
                if (_cry) _cry.Stop();
                return;
            }
            if (!_admitted)
            {
                var camera = _session.Player.View;
                Vector3 local = camera.transform.InverseTransformPoint(SeatedFocus);
                float halfHeight = Mathf.Max(0, local.z) * Mathf.Tan(Mathf.Max(72, camera.fieldOfView) * .5f * Mathf.Deg2Rad);
                if (local.z > -1 && Mathf.Abs(local.x) < halfHeight * camera.aspect + 1.3f && Mathf.Abs(local.y) < halfHeight + 1.3f) return;
                _admitted = true;
            }
            _figure.gameObject.SetActive(true);
            Pose(0);
            if (_sobIndex >= 0) _body.SetBlendShapeWeight(_sobIndex, (Mathf.Sin(_animationTime * 1.65f) * .5f + .5f) * 65);
            UpdateCrying();
            bool controlled = _session.Player.InputEnabled && !_session.Player.IsInspecting && Cursor.lockState == CursorLockMode.Locked;
            if (_gaze.Step(Time.deltaTime, IsLookedAt(_session.Player.View), Eligible && controlled)) BeginEncounter();
        }

        public bool IsLookedAt(Camera camera)
        {
            if (!camera || !IsVisible) return false;
            Vector3 center = SeatedFocus;
            Vector3 direction = center - camera.transform.position;
            float distance = direction.magnitude;
            if (distance < .3f || distance > MaximumDistance || Vector3.Angle(camera.transform.forward, direction) > 7f) return false;
            Vector3 bottom = camera.WorldToViewportPoint(center - Vector3.up * .7f);
            Vector3 top = camera.WorldToViewportPoint(center + Vector3.up * .15f);
            if (Mathf.Abs(top.y - bottom.y) < .012f) return false;
            int visible = 0;
            for (int i = 0; i < 3; i++)
            {
                Vector3 point = center + Vector3.up * (i == 0 ? .12f : -.12f) + _figure.right * (i == 1 ? -.13f : i == 2 ? .13f : 0);
                Vector3 viewport = camera.WorldToViewportPoint(point);
                if (viewport.z <= camera.nearClipPlane || viewport.x < .02f || viewport.x > .98f || viewport.y < .02f || viewport.y > .98f) continue;
                Vector3 delta = point - camera.transform.position;
                if (!IsOccluded(camera.transform.position, delta, .06f)) visible++;
            }
            return visible >= 2;
        }

        private bool IsOccluded(Vector3 origin, Vector3 delta, float margin)
        {
            int count = Physics.RaycastNonAlloc(origin, delta.normalized, _occlusionHits, Mathf.Max(0, delta.magnitude - margin), ~(1 << 2), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = _occlusionHits[i];
                if (hit.collider && !hit.collider.transform.IsChildOf(_session.Player.transform)) return true;
            }
            return count == _occlusionHits.Length;
        }

        private void BeginEncounter()
        {
            Phase = EncounterPhase.Rising;
            _phaseTime = 0;
            AttackCount++;
            _ownsControls = true;
            _session.Player.EncounterControlLocked = true;
            _session.Player.RefreshCameraModel();
            _cryVolumeAtCommit = _cry ? _cry.volume : 0;
            if (_signal) { _signal.volume = .025f * Master; _signal.Play(); }
            SetSignal(.025f, 0);
        }

        private void AdvanceEncounter(float deltaTime)
        {
            _phaseTime += deltaTime;
            if (Phase == EncounterPhase.Blackout)
            {
                SetSignal(0, 1);
                if (_phaseTime >= BlackoutDuration)
                {
                    Phase = EncounterPhase.Finished;
                    ReleaseControls();
                    StopAudio();
                    SetSignal(0, 0);
                    _session.EndRun("Она заметила твой взгляд.");
                }
                return;
            }
            float rise = Phase == EncounterPhase.Rising ? Mathf.Clamp01(_phaseTime / RiseDuration) : 1;
            // Smooth sampled anatomical poses; the root never leaves its stone.
            Pose(rise);
            if (_cry && _cry.isPlaying)
            {
                _cry.volume = _cryVolumeAtCommit * (1 - Mathf.Clamp01(_phaseTime / .45f));
                if (_phaseTime >= .45f) _cry.Stop();
            }
            float elapsed = Phase == EncounterPhase.Rising ? Mathf.Min(_phaseTime, RiseDuration) : RiseDuration + _phaseTime;
            float pressure = Mathf.Clamp01(elapsed / (RiseDuration + StareDuration));
            float intensity = Mathf.Lerp(.025f, 1, Mathf.Pow(pressure, 2.8f));
            if (_signal) _signal.volume = Mathf.Lerp(.025f, .68f, Mathf.Pow(pressure, 1.15f)) * Master;
            SetSignal(intensity, 0);
            if (Phase == EncounterPhase.Rising && _phaseTime >= RiseDuration)
            {
                Phase = EncounterPhase.Staring;
                _phaseTime = 0;
            }
            else if (Phase == EncounterPhase.Staring && _phaseTime >= StareDuration)
            {
                // Render complete signal loss before the black cut, including on slow frames.
                if (!_terminalSignalHeld)
                {
                    _terminalSignalHeld = true;
                    _phaseTime = StareDuration;
                    return;
                }
                if (_phaseTime < StareDuration + .18f) return;
                Phase = EncounterPhase.Blackout;
                _phaseTime = 0;
                _figure.gameObject.SetActive(false);
                StopAudio();
                SetSignal(0, 1);
            }
        }

        private void Pose(float rise)
        {
            for (int i = 0; i < _body.sharedMesh.blendShapeCount; i++) _body.SetBlendShapeWeight(i, 0);
            float frame = Mathf.Clamp01(rise) * (_riseIndices.Length - 1);
            int low = Mathf.Min(Mathf.FloorToInt(frame), _riseIndices.Length - 2);
            float t = frame - low, t2 = t * t, t3 = t2 * t;
            // Cubic weights keep velocity continuous between the authored bone poses.
            AddPoseWeight(low - 1, -.5f * t + t2 - .5f * t3);
            AddPoseWeight(low, 1 - 2.5f * t2 + 1.5f * t3);
            AddPoseWeight(low + 1, .5f * t + 2 * t2 - 1.5f * t3);
            AddPoseWeight(low + 2, -.5f * t2 + .5f * t3);
            Vector3 head = _figure.TransformPoint(_standingFocus);
            Vector3 direction = _session.Player.View.transform.position - head;
            float elevation = Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
            float gaze = Phase == EncounterPhase.Staring ? Mathf.SmoothStep(0, 1, _phaseTime / .8f) : 0;
            Vector3 localDirection = _figure.InverseTransformDirection(direction);
            float yaw = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
            float vertical = Mathf.Clamp(elevation / (elevation >= 0 ? 27f : 17f), -1, 1);
            float horizontal = Mathf.Clamp(yaw / 55f, -1, 1);
            // The authored extremes share the same neutral head; avoid adding two full extremes.
            float limit = Mathf.Max(1, Mathf.Abs(vertical) + Mathf.Abs(horizontal));
            vertical /= limit; horizontal /= limit;
            if (_lookUpIndex >= 0) _body.SetBlendShapeWeight(_lookUpIndex, Mathf.Max(0, vertical) * gaze * 100);
            if (_lookDownIndex >= 0) _body.SetBlendShapeWeight(_lookDownIndex, Mathf.Max(0, -vertical) * gaze * 100);
            if (_lookLeftIndex >= 0) _body.SetBlendShapeWeight(_lookLeftIndex, Mathf.Max(0, -horizontal) * gaze * 100);
            if (_lookRightIndex >= 0) _body.SetBlendShapeWeight(_lookRightIndex, Mathf.Max(0, horizontal) * gaze * 100);
        }

        private void AddPoseWeight(int pose, float weight)
        {
            int index = _riseIndices[Mathf.Clamp(pose, 0, _riseIndices.Length - 1)];
            if (index >= 0) _body.SetBlendShapeWeight(index, _body.GetBlendShapeWeight(index) + weight * 100);
        }

        private float Master => _ambience ? _ambience.Master : .75f;

        private void UpdateCrying()
        {
            if (!_cry || !_cry.clip) return;
            float distance = Vector3.Distance(_session.Player.View.transform.position, SeatedFocus);
            if (distance > 50) { _cry.Stop(); return; }
            _cry.volume = .8f * Master;
            if (!_cry.isPlaying) _cry.Play();
            if (_cryFilter)
            {
                Vector3 delta = SeatedFocus - _session.Player.View.transform.position;
                bool blocked = IsOccluded(_session.Player.View.transform.position, delta, .2f);
                _cryFilter.cutoffFrequency = Mathf.Lerp(_cryFilter.cutoffFrequency, blocked ? 850 : 8500, 1 - Mathf.Exp(-Time.deltaTime * 5));
            }
        }

        private void SetSignal(float strength, float black)
        {
            if (!_interference) return;
            _interference.SetFloat("_Strength", strength);
            _interference.SetFloat("_Blackout", black);
            _interference.SetFloat("_SignalTime", Time.time);
            _copyPass.enabled = _pass.enabled = strength > 0 || black > 0;
        }

        private void ReleaseControls()
        {
            if (!_ownsControls || !_session || !_session.Player) return;
            _session.Player.EncounterControlLocked = false;
            _session.Player.RefreshCameraModel();
            _ownsControls = false;
        }

        private void CancelEncounter()
        {
            ReleaseControls(); StopAudio(); SetSignal(0, 0); _gaze.Reset();
            Phase = EncounterPhase.Finished;
            if (_figure) _figure.gameObject.SetActive(false);
        }

        private void StopAudio() { if (_cry) _cry.Stop(); if (_signal) _signal.Stop(); }
        private void PauseAudio(bool pause)
        {
            if (_cry) { if (pause) _cry.Pause(); else _cry.UnPause(); }
            if (_signal) { if (pause) _signal.Pause(); else _signal.UnPause(); }
        }
        private void OnDisable() { if (_session) CancelEncounter(); }
        private void OnDestroy()
        {
            if (_session) _session.RunBegan -= ResetEncounter;
            if (_interference) Destroy(_interference);
            if (_volume) Destroy(_volume);
        }
    }
}
