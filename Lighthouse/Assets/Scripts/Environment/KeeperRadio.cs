using System;
using System.Collections.Generic;
using UnityEngine;

namespace LighthouseGame
{
    public enum RadioBand { Static, Coast, Weather, Distant }

    public static class RadioTuning
    {
        public const float Minimum = 88, Maximum = 108;
        public static float Clamp(float frequency) => float.IsNaN(frequency) || float.IsInfinity(frequency)
            ? 94.2f : Mathf.Round(Mathf.Clamp(frequency, Minimum, Maximum) * 10) / 10;
        public static RadioBand Band(float frequency)
        {
            if (Mathf.Abs(frequency - 94.2f) < .35f) return RadioBand.Coast;
            if (Mathf.Abs(frequency - 101.6f) < .35f) return RadioBand.Weather;
            if (Mathf.Abs(frequency - 88.6f) < .4f) return RadioBand.Distant;
            return RadioBand.Static;
        }
        public static float Signal(float frequency)
        {
            float distance = Mathf.Min(Mathf.Abs(frequency - 94.2f), Mathf.Abs(frequency - 101.6f), Mathf.Abs(frequency - 88.6f));
            return Mathf.Clamp01(1 - distance / .5f);
        }
    }

    // This receiver owns audio playback only. Event owners select and schedule forecasts.
    public sealed class KeeperRadio : MonoBehaviour
    {
        [Serializable] public sealed class Broadcast { public string key, station, text; }
        [Serializable] private sealed class Catalog { public Broadcast[] broadcasts; }
        public event Action Changed;
        public event Action ControlsRequested;
        public bool IsPowered { get; private set; } = true;
        public float Frequency { get; private set; } = 94.2f;
        public RadioBand Band => RadioTuning.Band(Frequency);
        public bool ControlsOpen { get; private set; }
        public string Transcript { get; private set; } = "";
        public string Subtitle { get; private set; } = "";
        public string CurrentBroadcast { get; private set; } = "";
        public string CurrentMusic => _musicIndex >= 0 && _music && _music.isPlaying ? MusicNames[_musicIndex] : "";
        public int BroadcastCount { get; private set; }
        public int MusicCount { get; private set; }
        public string StationName => !IsPowered ? "Выключено" : Band == RadioBand.Coast ? "Береговая служба"
            : Band == RadioBand.Weather ? "Метеопост" : Band == RadioBand.Distant ? "Дальний эфир" : "Помехи";
        private static readonly string[] MusicKeys = { "ashes-of-the-keep", "deckside-jig", "sun-faded-memory" };
        private static readonly string[] MusicNames = { "Ashes of the Keep", "Deckside Jig", "Sun-Faded Memory" };
        private readonly Dictionary<string, Broadcast> _broadcasts = new Dictionary<string, Broadcast>();
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private readonly List<int> _musicBag = new List<int>();
        private BlockoutSession _session;
        private CoastalAmbience _ambience;
        private AudioSource _noise, _voice, _ambient, _music;
        private Transform[] _dials;
        private Quaternion[] _dialRest;
        private float _nextBulletin, _nextMusic;
        private int _musicIndex = -1;
        private bool _wasSuspended, _cursorWasLocked;
        private bool _musicWasPlaying;
        private string[] _sentences = Array.Empty<string>();

        public void Initialize(BlockoutSession session, CoastalAmbience ambience, AudioSource noise, AudioSource voice)
        {
            _session = session; _ambience = ambience; _noise = noise; _voice = voice;
            _session.Radio = this;
            _session.RunBegan += ResetRun;
            _session.DayBegan += ResetDay;
            var data = Resources.Load<TextAsset>("KeeperRadio/Broadcasts");
            if (data)
                foreach (var entry in JsonUtility.FromJson<Catalog>(data.text).broadcasts)
                { _broadcasts[entry.key] = entry; _clips[entry.key] = Resources.Load<AudioClip>("KeeperRadio/" + entry.key); }
            _noise.clip = Resources.Load<AudioClip>("KeeperRadio/static");
            _voice.Stop(); _voice.loop = false; _voice.playOnAwake = false;
            _ambient = CreateSource("Radio · distant ambience", Resources.Load<AudioClip>("KeeperRadio/ambient"), true);
            _music = CreateSource("Radio · music", null, false);
            Configure(_noise, true); Configure(_voice, false);
            var motions = transform.GetComponentsInChildren<BlockoutMotion>();
            var dials = new List<Transform>();
            foreach (var motion in motions)
                if (motion.Type == BlockoutMotion.MotionType.Dial) { motion.enabled = false; dials.Add(motion.transform); }
            _dials = dials.ToArray(); _dialRest = new Quaternion[_dials.Length];
            for (int i = 0; i < _dials.Length; i++) _dialRest[i] = _dials[i].localRotation;
            ResetRun();
            gameObject.AddComponent<KeeperRadioView>().Initialize(this);
        }

        private AudioSource CreateSource(string label, AudioClip clip, bool loop)
        {
            var child = new GameObject(label); child.transform.SetParent(_noise.transform.parent, false);
            child.transform.position = _noise.transform.position;
            var source = child.AddComponent<AudioSource>(); source.clip = clip; Configure(source, loop); return source;
        }

        private static void Configure(AudioSource source, bool loop)
        {
            source.Stop(); source.playOnAwake = false; source.loop = loop; source.volume = 0;
            source.spatialBlend = 1; source.dopplerLevel = 0; source.spread = 0;
            source.rolloffMode = AudioRolloffMode.Linear; source.minDistance = 1.4f; source.maxDistance = 19;
        }

        public void SetPower(bool value)
        {
            if (IsPowered == value) return;
            IsPowered = value;
            if (!value) Silence(); else _nextMusic = Time.time + UnityEngine.Random.Range(12f, 25f);
            Changed?.Invoke();
        }

        public void Tune(float frequency)
        {
            frequency = RadioTuning.Clamp(frequency);
            if (Mathf.Approximately(Frequency, frequency)) return;
            var previous = Band; Frequency = frequency;
            if (previous != Band) StopVoice();
            if (_dials != null)
                for (int i = 0; i < _dials.Length; i++)
                    _dials[i].localRotation = _dialRest[i] * Quaternion.Euler(0, 0, Mathf.Lerp(-125, 125, (Frequency - 88) / 20));
            Changed?.Invoke();
        }

        public void RequestControls()
        {
            if (ControlsRequested == null) return;
            if (!_session || _session.MenuOpen || _session.Sleeping || _session.GameOver || ControlsOpen) return;
            _session.Player.CloseInspection();
            _cursorWasLocked = Cursor.lockState == CursorLockMode.Locked;
            ControlsOpen = true; _session.Player.LockCursor(false);
            ControlsRequested?.Invoke(); Changed?.Invoke();
        }

        public void CloseControls()
        {
            if (!ControlsOpen) return;
            ControlsOpen = false;
            if (_session && !_session.GameOver && !_session.MenuOpen) _session.Player.LockCursor(_cursorWasLocked);
            Changed?.Invoke();
        }

        public bool ConsiderForecast(string key, float leadSeconds)
        {
            return leadSeconds > 0 && TryBroadcast(key);
        }

        public bool ConsiderReport(string key) => TryBroadcast(key);

        private bool TryBroadcast(string key)
        {
            // No backlog: an unheard warning cannot surface after its event or on a later day.
            if (!CanReceive || Time.time < _nextBulletin || _voice.isPlaying
                || !_broadcasts.TryGetValue(key, out var entry) || !_clips.TryGetValue(key, out var clip) || !clip) return false;
            if ((entry.station == "weather" ? RadioBand.Weather : RadioBand.Coast) != Band) return false;
            if (UnityEngine.Random.value > .6f) return false;
            _voice.clip = clip; _voice.volume = .62f * Master * RadioTuning.Signal(Frequency); _voice.Play();
            Transcript = entry.text; CurrentBroadcast = key; BroadcastCount++;
            _sentences = System.Text.RegularExpressions.Regex.Split(Transcript, @"(?<=[.!?…])\s+");
            Subtitle = _sentences[0];
            _nextBulletin = Time.time + Mathf.Max(clip.length + 8, UnityEngine.Random.Range(28f, 48f));
            Changed?.Invoke(); return true;
        }

        public void CancelForecast(string key)
        {
            if (CurrentBroadcast == key) StopVoice();
        }

        private bool CanReceive => _session && IsPowered && _session.Running && !_session.MenuOpen
            && !_session.GameOver && !_session.Sleeping && _session.Preparation <= 0 && Time.timeScale > 0;
        private float Master => _ambience ? _ambience.EffectsGain : .75f;

        private void Update()
        {
            if (!_session) return;
            bool suspended = _session.MenuOpen || _session.GameOver || _session.Sleeping || !_session.Running;
            if (suspended)
            {
                if (!_wasSuspended) { CloseControls(); Silence(); }
                _wasSuspended = true; return;
            }
            _wasSuspended = false;
            if (ControlsOpen && (_session.Player.EncounterControlLocked
                || Vector3.Distance(_session.Player.transform.position, transform.position) > 4.5f)) CloseControls();
            if (!IsPowered) return;
            if (!_noise.isPlaying && _noise.clip) _noise.Play();
            if (!_ambient.isPlaying && _ambient.clip) _ambient.Play();
            if (!_voice.isPlaying && CurrentBroadcast.Length > 0) StopVoice();
            if (_voice.isPlaying) UpdateSubtitle();
            if (_musicWasPlaying && !_music.isPlaying) { _musicWasPlaying = false; Changed?.Invoke(); }
            float signal = RadioTuning.Signal(Frequency), blend = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 6);
            _noise.volume = Mathf.Lerp(_noise.volume, Mathf.Lerp(.3f, .035f, signal) * Master, blend);
            _voice.volume = Mathf.Lerp(_voice.volume, .62f * Master * signal, blend);
            _ambient.volume = Mathf.Lerp(_ambient.volume, .12f * signal * Master * (_voice.isPlaying || _music.isPlaying ? .12f : 1), blend);
            _music.volume = Mathf.Lerp(_music.volume, .32f * signal * Master * (_voice.isPlaying ? .13f : 1), blend);
            if (!_music.isPlaying && Time.time >= _nextMusic && signal > .7f && _session.Preparation <= 0)
            {
                if (UnityEngine.Random.value < .7f) PlayNextTrack();
                else _nextMusic = Time.time + UnityEngine.Random.Range(18f, 35f);
            }
        }

        public void PlayNextTrack()
        {
            if (!CanReceive || RadioTuning.Signal(Frequency) < .7f) return;
            if (_musicBag.Count == 0)
                for (int i = 0; i < MusicKeys.Length; i++) _musicBag.Add(i);
            int pick = UnityEngine.Random.Range(0, _musicBag.Count);
            if (_musicBag.Count > 1 && _musicBag[pick] == _musicIndex) pick = (pick + 1) % _musicBag.Count;
            _musicIndex = _musicBag[pick]; _musicBag.RemoveAt(pick);
            _music.clip = Resources.Load<AudioClip>("KeeperRadio/Music/" + MusicKeys[_musicIndex]);
            if (_music.clip) { _music.Play(); _musicWasPlaying = true; MusicCount++; _nextMusic = Time.time + _music.clip.length + UnityEngine.Random.Range(18f, 36f); }
            else _nextMusic = Time.time + 30;
            Changed?.Invoke();
        }

        private void ResetRun()
        {
            CloseControls(); Silence(); _musicBag.Clear(); _musicIndex = -1;
            _nextBulletin = 0; _nextMusic = Time.time + UnityEngine.Random.Range(18f, 32f);
        }
        private void ResetDay() { Silence(); _nextBulletin = 0; _nextMusic = Time.time + UnityEngine.Random.Range(12f, 25f); }
        private void UpdateSubtitle()
        {
            if (_sentences.Length == 0 || !_voice.clip) return;
            float position = _voice.time / _voice.clip.length * Transcript.Length;
            int offset = 0;
            foreach (string sentence in _sentences)
            {
                offset += sentence.Length + 1;
                if (position >= offset) continue;
                if (Subtitle != sentence) { Subtitle = sentence; Changed?.Invoke(); }
                break;
            }
        }
        private void StopVoice()
        {
            if (_voice) _voice.Stop();
            bool changed = Transcript.Length > 0;
            Transcript = ""; Subtitle = ""; CurrentBroadcast = ""; _sentences = Array.Empty<string>();
            if (changed) Changed?.Invoke();
        }
        public void Silence()
        {
            StopVoice();
            foreach (var source in new[] { _noise, _ambient, _music }) if (source) { source.Stop(); source.volume = 0; }
            if (_musicWasPlaying) { _musicWasPlaying = false; Changed?.Invoke(); }
        }
        private void OnDisable() { CloseControls(); Silence(); }
        private void OnDestroy()
        {
            if (!_session) return;
            _session.RunBegan -= ResetRun; _session.DayBegan -= ResetDay;
            if (_session.Radio == this) _session.Radio = null;
        }
    }
}
