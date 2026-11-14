using System.Collections.Generic;
using UnityEngine;

namespace LighthouseGame
{
    // A small, persistent colony: birds leave and return along the outside of the
    // tower. They never respawn or teleport when the player looks away.
    public sealed class CoastalGull : MonoBehaviour
    {
        public enum Activity { Resting, TakingOff, Circling, Landing }

        [SerializeField] private BlockoutPlayer _player;
        [SerializeField] private CinematicEnvironment _environment;
        [SerializeField] private Transform _perch;
        [SerializeField] private SkinnedMeshRenderer _body;
        [SerializeField] private AudioSource _voice;
        [SerializeField] private AudioSource _wings;
        [SerializeField] private AudioClip[] _calls;
        [SerializeField] private bool _startAirborne;
        [SerializeField] private float _orbitRadius = 26;
        [SerializeField] private float _orbitHeight = 18;
        [SerializeField] private float _phase;

        private const float FleeDistance = 4.5f;
        private const float ReturnDistance = 7f;
        private const int ObstacleMask = ~(1 << 2);
        private Activity _activity;
        private float _stateTime, _restUntil, _returnAfter, _angle, _nextCall;
        private float _fold, _animationTime, _pathDuration, _nextVisibilityCheck;
        private float _wingPhase, _flapStrength = 1;
        private Vector3 _pathStart, _pathControlA, _pathControlB, _pathEnd;
        private Quaternion _restRotation;
        private AudioLowPassFilter _voiceFilter, _wingFilter;
        private int _upShape, _downShape, _foldShape, _lastCall = -1;
        private bool _playerVisible;
        private Mesh _photoPose;
        private readonly List<Vector3> _photoVertices = new List<Vector3>();
        private int _photoFrame = -1;

        public const int PhotoPoints = 20;
        public bool VisibleIn(Camera camera) => PhotoEvidence.Skinned(camera, _body, ref _photoPose, _photoVertices);
        public void HoldForPhoto() { _photoFrame = Time.frameCount; }

        public Activity CurrentActivity => _activity;
        public Transform Perch => _perch;
        public int StartleCount { get; private set; }
        public int LandingCount { get; private set; }
        public int ObstacleAvoidanceCount { get; private set; }

        private void Start()
        {
            if (!_player || !_perch || !_body || !_voice || !_wings)
            {
                Debug.LogError("Coastal gull needs a player, perch, mesh and two audio sources.", this);
                enabled = false;
                return;
            }
            _upShape = _body.sharedMesh.GetBlendShapeIndex("Up");
            _downShape = _body.sharedMesh.GetBlendShapeIndex("Down");
            _foldShape = _body.sharedMesh.GetBlendShapeIndex("Fold");
            _voiceFilter = _voice.GetComponent<AudioLowPassFilter>();
            _wingFilter = _wings.GetComponent<AudioLowPassFilter>();
            _angle = Mathf.Atan2(_perch.position.z, _perch.position.x);
            _animationTime = _phase;
            _wingPhase = _phase * Mathf.PI * 2;
            _nextCall = Time.time + 2 + _phase * 2;
            _restRotation = _perch.rotation;
            _fold = _startAirborne ? 0 : 1;
            if (_startAirborne)
            {
                _angle += _phase;
                _activity = Activity.Circling;
                transform.position = OrbitPosition(_angle);
                transform.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(_angle), 0, Mathf.Cos(_angle)));
                _returnAfter = Time.time + 14 + _phase * 3;
            }
            else
            {
                Settle();
            }
            AnimateWings(0);
        }

        private void Update()
        {
            if (_photoFrame == Time.frameCount) return;
            float dt = Mathf.Min(Time.deltaTime, .1f);
            _stateTime += dt;
            _animationTime += dt;
            if ((_activity == Activity.Resting || _activity == Activity.Landing) && ShouldFlee())
            {
                TakeOff(true);
            }

            switch (_activity)
            {
                case Activity.Resting:
                    transform.rotation = _restRotation * Quaternion.Euler(
                        Mathf.Sin(_animationTime * 1.2f) * 1.5f,
                        Mathf.Sin(_animationTime * .45f) * 8, 0);
                    if (Time.time > _restUntil) TakeOff(false);
                    break;
                case Activity.TakingOff:
                case Activity.Landing:
                    FollowApproach(dt);
                    break;
                case Activity.Circling:
                    Circle(dt);
                    break;
            }
            AnimateWings(dt);
            UpdateAudio(dt);
        }

        private bool ShouldFlee()
        {
            Vector3 delta = _player.transform.position - transform.position;
            if (Mathf.Abs(delta.y) > 3f || new Vector2(delta.x, delta.z).sqrMagnitude > FleeDistance * FleeDistance)
            {
                return false;
            }
            if (Time.time >= _nextVisibilityCheck)
            {
                _nextVisibilityCheck = Time.time + .12f;
                Vector3 from = transform.position + Vector3.up * .35f;
                Vector3 to = _player.transform.position + Vector3.up;
                _playerVisible = !Physics.Linecast(from, to, out RaycastHit hit, ObstacleMask, QueryTriggerInteraction.Ignore)
                    || hit.transform.IsChildOf(_player.transform);
            }
            return _playerVisible;
        }

        private bool CanReturn()
        {
            if ((_player.transform.position - _perch.position).sqrMagnitude < ReturnDistance * ReturnDistance)
                return false;
            // Check the actual support each time; never land on an absent collider.
            return Physics.Raycast(_perch.position + Vector3.up * .2f, Vector3.down, out _, .4f,
                ObstacleMask, QueryTriggerInteraction.Ignore);
        }

        private Vector3 OrbitPosition(float angle)
        {
            return new Vector3(Mathf.Cos(angle) * _orbitRadius,
                _orbitHeight + Mathf.Sin(angle * 2 + _phase) * .85f, Mathf.Sin(angle) * _orbitRadius);
        }

        private void TakeOff(bool startled)
        {
            if (startled) StartleCount++;
            Vector3 outward = new Vector3(transform.position.x, 0, transform.position.z).normalized;
            if (outward.sqrMagnitude < .1f) outward = Vector3.forward;
            _angle = Mathf.Atan2(outward.z, outward.x);
            Vector3 end = OrbitPosition(_angle);
            _pathStart = transform.position;
            // The initial lift clears terrace and gallery rails before crossing them.
            _pathControlA = _pathStart + Vector3.up * 4 + outward * .65f;
            _pathControlB = end - outward * 5;
            _pathEnd = end;
            _pathDuration = Mathf.Max(3.5f, Vector3.Distance(_pathStart, end) / 5.5f);
            _stateTime = 0;
            _activity = Activity.TakingOff;
            _returnAfter = Time.time + Random.Range(19f, 32f);
            _wings.pitch = Random.Range(.93f, 1.07f);
            _wings.Play();
            if (startled) Call();
        }

        private void Circle(float dt)
        {
            float speed = 6.4f + (_environment ? Mathf.InverseLerp(10, 65, _environment.Wind) : 0);
            _angle += speed / _orbitRadius * dt;
            MoveBird(OrbitPosition(_angle), dt, -9);
            float perchAngle = Mathf.Atan2(_perch.position.z, _perch.position.x);
            if (Time.time > _returnAfter && Mathf.Abs(Mathf.DeltaAngle(_angle * Mathf.Rad2Deg, perchAngle * Mathf.Rad2Deg)) < 5 && CanReturn())
            {
                Vector3 outward = new Vector3(_perch.position.x, 0, _perch.position.z).normalized;
                _pathStart = transform.position;
                _pathEnd = _perch.position;
                _pathControlA = _pathStart + new Vector3(-Mathf.Sin(_angle), 0, Mathf.Cos(_angle)) * 3;
                _pathControlB = _pathEnd + outward * 4 + Vector3.up * 4;
                _pathDuration = Mathf.Max(5f, Vector3.Distance(_pathStart, _pathEnd) / 4);
                _stateTime = 0;
                _activity = Activity.Landing;
            }
        }

        private void FollowApproach(float dt)
        {
            if (_activity == Activity.Landing && !CanReturn())
            {
                TakeOff(false);
                return;
            }
            float t = Mathf.Clamp01(_stateTime / _pathDuration);
            float u = 1 - t;
            Vector3 next = u * u * u * _pathStart + 3 * u * u * t * _pathControlA
                + 3 * u * t * t * _pathControlB + t * t * t * _pathEnd;
            Vector3 travel = next - transform.position;
            // Keep the feet clear of the perch itself during the final approach.
            if (travel.sqrMagnitude > .000001f && Physics.SphereCast(transform.position + Vector3.up * .32f,
                .18f, travel.normalized, out _, travel.magnitude, ObstacleMask, QueryTriggerInteraction.Ignore))
            {
                ObstacleAvoidanceCount++;
                // Lift in place if a changed scene blocks the authored outward route.
                _pathStart = transform.position;
                _pathControlA = _pathStart + Vector3.up * 6;
                _pathEnd = OrbitPosition(_angle);
                _pathControlB = _pathEnd + Vector3.up * 3;
                _pathDuration = 5;
                _stateTime = 0;
                _activity = Activity.TakingOff;
                _returnAfter = Time.time + 20;
                return;
            }
            MoveBird(next, dt, 0);
            if (t >= 1)
            {
                if (_activity == Activity.Landing)
                {
                    LandingCount++;
                    Settle();
                }
                else
                {
                    _activity = Activity.Circling;
                    _stateTime = 0;
                }
            }
        }

        private void MoveBird(Vector3 next, float dt, float bank)
        {
            Vector3 direction = next - transform.position;
            if (direction.sqrMagnitude > .00001f)
            {
                Quaternion target = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 0, bank);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, 1 - Mathf.Exp(-dt * 5));
            }
            transform.position = next;
        }

        private void Settle()
        {
            _activity = Activity.Resting;
            _stateTime = 0;
            transform.SetPositionAndRotation(_perch.position, _restRotation);
            _restUntil = Time.time + Random.Range(26f, 48f);
        }

        private void AnimateWings(float dt)
        {
            float targetFold = _activity == Activity.Resting ? 1 : 0;
            if (_activity == Activity.Landing)
                targetFold = Mathf.InverseLerp(.83f, 1, _stateTime / _pathDuration);
            _fold = Mathf.MoveTowards(_fold, targetFold, dt * 2.8f);
            bool gliding = _activity == Activity.Circling && Mathf.Sin(_animationTime * .65f) > -.3f;
            float frequency = _activity == Activity.TakingOff ? 4.2f : _activity == Activity.Landing ? 3.2f : 2.6f;
            _wingPhase = Mathf.Repeat(_wingPhase + dt * frequency * Mathf.PI * 2, Mathf.PI * 2);
            _flapStrength = Mathf.MoveTowards(_flapStrength, gliding ? .10f : 1, dt * 3);
            float flap = Mathf.Sin(_wingPhase) * _flapStrength * (1 - _fold);
            if (_upShape >= 0) _body.SetBlendShapeWeight(_upShape, Mathf.Max(0, flap) * 100);
            if (_downShape >= 0) _body.SetBlendShapeWeight(_downShape, Mathf.Max(0, -flap) * 100);
            if (_foldShape >= 0) _body.SetBlendShapeWeight(_foldShape, _fold * 100);
        }

        private void UpdateAudio(float dt)
        {
            bool indoors = _environment && _environment.Ambience && _environment.Ambience.Indoors;
            float master = _environment && _environment.Ambience ? _environment.Ambience.AmbientGain : .75f;
            _voice.volume = master * (indoors ? .20f : .42f);
            _wings.volume = master * (indoors ? .16f : .38f);
            float cutoff = indoors ? 2400 : 17000;
            float blend = 1 - Mathf.Exp(-dt * 3);
            if (_voiceFilter) _voiceFilter.cutoffFrequency = Mathf.Lerp(_voiceFilter.cutoffFrequency, cutoff, blend);
            if (_wingFilter) _wingFilter.cutoffFrequency = Mathf.Lerp(_wingFilter.cutoffFrequency, cutoff, blend);
            if (Time.time >= _nextCall)
            {
                // Space individual calls out; the colony is never a constant chorus.
                bool night = _environment && (_environment.Hour < 6 || _environment.Hour > 21);
                _nextCall = Time.time + Random.Range(night ? 35f : 13f, night ? 65f : 26f);
                if ((_player.transform.position - transform.position).sqrMagnitude < 75 * 75) Call();
            }
        }

        private void Call()
        {
            if (_calls == null || _calls.Length == 0 || _voice.isPlaying) return;
            int index = Random.Range(0, _calls.Length);
            if (index == _lastCall && _calls.Length > 1) index = (index + 1) % _calls.Length;
            _lastCall = index;
            _voice.pitch = Random.Range(.94f, 1.06f);
            _voice.clip = _calls[index];
            _voice.Play();
        }

        private void OnDisable()
        {
            if (_voice) _voice.Stop();
            if (_wings) _wings.Stop();
        }

        private void OnDestroy()
        {
            if (_photoPose) Destroy(_photoPose);
        }
    }
}
