using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LighthouseGame
{
    [DefaultExecutionOrder(150)]
    public sealed class KeeperGameScene : MonoBehaviour
    {
        public const string ScenePath = "Assets/Scenes/LighthouseBlockout.unity";
        [SerializeField] private BlockoutSession _session;
        private PhotoArchive _archive;
        private MenuBackdrop _backdrop;
        private bool _loading, _started;
        public PhotoArchive Archive => _archive;
        public MenuSettings AppliedSettings { get; private set; }

        private void OnEnable()
        {
            if (!_session || !_session.Player)
            {
                Debug.LogError("Gameplay scene requires its session and player references.", this);
                enabled = false;
                return;
            }
            _archive = new PhotoArchive(Path.Combine(Application.persistentDataPath, "BestPhotos"), _session.Best);
            _session.PhotoCaptured += PhotoCaptured;
            _session.StateChanged += SessionChanged;
            _session.MenuRequested += ReturnToMenu;
            if (_started) ApplySettings();
        }

        private void Start()
        {
            _started = true;
            ApplySettings();
            _session.BeginRun();
        }

        private void ApplySettings()
        {
            AppliedSettings = MenuSettings.Load(Path.Combine(Application.persistentDataPath, "menu-settings.json"), out _);
            var settings = AppliedSettings;
            AudioListener.volume = settings.Master / 100f;
            var ambience = CinematicEnvironment.Active ? CinematicEnvironment.Active.Ambience : null;
            if (ambience)
            {
                ambience.UserVolume = settings.Ambience / 100f;
                ambience.MenuMultiplier = 1;
                ambience.EffectsVolume = settings.Effects / 100f;
            }
            QualitySettings.vSyncCount = settings.VSync ? 1 : 0;
            Application.targetFrameRate = settings.FrameLimit == 0 ? -1 : settings.FrameLimit;
            _session.Player.LookSensitivity = settings.Sensitivity;
            _session.Player.InvertLookY = settings.InvertY;
            if (!Application.isEditor)
                Screen.SetResolution(settings.Width, settings.Height, settings.Display == 0 ? FullScreenMode.FullScreenWindow : settings.Display == 1 ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed);
            if (!_backdrop)
            {
                _backdrop = gameObject.AddComponent<MenuBackdrop>();
                _backdrop.Initialize(_session.Player.View);
            }
            _backdrop.Page = -1;
            _backdrop.Brightness = settings.Brightness / 100f;
            _backdrop.Glitches = false;
        }

        public void ReturnToMenu()
        {
            if (_loading || !_session.GameOver) return;
            try
            {
                if (!Application.CanStreamedLevelBeLoaded(KeeperMenu.ScenePath))
                    throw new InvalidOperationException("Main menu scene is not included in the build.");
                _loading = true;
                _session.MenuOpen = true;
                _session.Player.LockCursor(false);
                _archive.RecordBest(_session.Best);
                SceneManager.LoadSceneAsync(KeeperMenu.ScenePath, LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                _loading = false;
                _session.MenuOpen = false;
                Debug.LogError("Cannot load main menu scene: " + exception.Message, this);
                _session.Notify("Не удалось открыть главное меню. Попробуйте ещё раз.");
            }
        }

        private void PhotoCaptured(BlockoutShot shot, bool isRun)
        {
            if (!_archive.Record(shot, _session.Score, isRun) && !string.IsNullOrEmpty(_archive.Error))
                _session.Notify(_archive.Error);
        }

        private void SessionChanged()
        {
            if (_session.GameOver) _archive.RecordBest(_session.Best);
        }

        private void OnDisable()
        {
            if (_session)
            {
                _session.PhotoCaptured -= PhotoCaptured;
                _session.StateChanged -= SessionChanged;
                _session.MenuRequested -= ReturnToMenu;
            }
            if (_backdrop) Destroy(_backdrop);
        }
    }
}
