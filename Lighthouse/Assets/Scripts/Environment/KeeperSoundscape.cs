using UnityEngine;

namespace LighthouseGame
{
    // Explicit scene references keep every sound at its visible physical source.
    public sealed class KeeperSoundscape : MonoBehaviour
    {
        [SerializeField] private BlockoutSession _session;
        [SerializeField] private CoastalAmbience _ambience;
        [SerializeField] private AudioSource _shutter;
        [SerializeField] private AudioSource _burner;
        [SerializeField] private AudioSource _radio;
        [SerializeField] private AudioSource _radioTransmit;
        [SerializeField] private AudioSource _door;
        [SerializeField] private AudioSource _lampLever;
        [SerializeField] private AudioSource _galleryCreak;
        [SerializeField] private AudioSource _flag;
        [SerializeField] private BlockoutInteractable _doorStation;
        private AudioLowPassFilter _flagFilter;
        private AudioLowPassFilter _creakFilter;
        private float _nextCreak;
        private bool _doorWasOpen;

        public int ShutterCount { get; private set; }
        public int RadioTransmissionCount { get; private set; }

        private void Start()
        {
            _doorWasOpen = _doorStation && _doorStation.Open;
            _nextCreak = Time.time + Random.Range(8f, 18f);
            _flagFilter = _flag ? _flag.GetComponent<AudioLowPassFilter>() : null;
            _creakFilter = _galleryCreak ? _galleryCreak.GetComponent<AudioLowPassFilter>() : null;
            StartLoop(_burner);
            if (_radio && _radioTransmit)
            {
                var station = _radio.GetComponentInParent<BlockoutInteractable>();
                if (station)
                {
                    var receiver = station.gameObject.GetComponent<KeeperRadio>();
                    if (!receiver) receiver = station.gameObject.AddComponent<KeeperRadio>();
                    receiver.Initialize(_session, _ambience, _radio, _radioTransmit);
                }
            }
            StartLoop(_flag);
        }

        private static void StartLoop(AudioSource source)
        {
            if (!source || !source.clip) return;
            source.time = Random.Range(0, source.clip.length * .8f);
            source.Play();
        }

        public void PhotoTaken()
        {
            if (!_shutter || !_shutter.clip) return;
            ShutterCount++;
            _shutter.volume = .8f * Master;
            _shutter.PlayOneShot(_shutter.clip);
        }

        public void WorkStarted(BlockoutInteractable.Station station, float duration)
        {
            if (station == BlockoutInteractable.Station.Lamp && _lampLever)
            {
                _lampLever.volume = .23f * Master;
                _lampLever.pitch = Random.Range(.92f, 1.04f);
                _lampLever.Play();
            }
        }

        private float Master => _ambience ? _ambience.EffectsGain : .75f;

        private void Update()
        {
            if (!_session) return;
            float dt = 1 - Mathf.Exp(-Time.deltaTime * 4);
            var environment = CinematicEnvironment.Active;
            float wind = environment ? Mathf.InverseLerp(7, 65, environment.Wind) : .2f;
            bool lampOn = _session.Director && _session.Director.LampLight && _session.Director.LampLight.enabled;
            if (_burner) _burner.volume = Mathf.Lerp(_burner.volume, lampOn ? .24f * Master : 0, dt);
            bool sheltered = _ambience && _ambience.Indoors;
            if (_flag) _flag.volume = Mathf.Lerp(_flag.volume, Mathf.Lerp(.07f, .30f, wind) * Master * (sheltered ? .35f : 1), dt);
            if (_flagFilter) _flagFilter.cutoffFrequency = Mathf.Lerp(_flagFilter.cutoffFrequency, sheltered ? 2600 : 16000, dt);
            if (_creakFilter) _creakFilter.cutoffFrequency = Mathf.Lerp(_creakFilter.cutoffFrequency, sheltered ? 4000 : 16000, dt);

            if (_doorStation && _doorWasOpen != _doorStation.Open)
            {
                _doorWasOpen = _doorStation.Open;
                if (_door)
                {
                    _door.volume = .35f * Master;
                    _door.pitch = _doorWasOpen ? 1 : .88f;
                    _door.Play();
                }
            }
            if (_galleryCreak && Time.time >= _nextCreak)
            {
                _galleryCreak.volume = Mathf.Lerp(.055f, .16f, wind) * Master;
                _galleryCreak.pitch = Random.Range(.72f, .96f);
                _galleryCreak.Play();
                _nextCreak = Time.time + Random.Range(12f, 26f) / Mathf.Lerp(1, 1.5f, wind);
            }
        }

        private void OnDisable()
        {
            AudioSource[] sources = { _burner, _radio, _radioTransmit, _door, _lampLever, _galleryCreak, _flag, _shutter };
            foreach (var source in sources) if (source) source.Stop();
        }
    }
}
