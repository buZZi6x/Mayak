using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using Cursor = UnityEngine.Cursor;

namespace LighthouseGame
{
    [DefaultExecutionOrder(150)]
    public sealed partial class KeeperMenu : MonoBehaviour
    {
        public const string ScenePath = "Assets/Scenes/MainMenu.unity";
        [SerializeField] private Camera _camera;
        [SerializeField] private AudioSource _music;
        private UIDocument _document;
        private PanelSettings _panel;
        private VisualElement _root;
        private MenuBackdrop _backdrop;
        private AudioSource _testAudio;
        private MenuSettings _saved, _draft;
        private PhotoArchive _archive;
        private string _settingsPath, _page = "home", _section = "audio";
        private bool _started, _loading;
        private IVisualElementScheduledItem _clock;
        private Action _confirm, _cancel;
        private VisualElement _dialogReturn;
        private FontAsset _displayFont;
        private readonly List<Texture2D> _archiveImages = new List<Texture2D>();
        public VisualElement Root => _root;
        public PhotoArchive Archive => _archive;
        public bool IsOpen => isActiveAndEnabled && _root != null && !_loading;
        public string CurrentPage => _page;
        public MenuSettings AppliedSettings => _saved.Clone();

        private void OnEnable()
        {
            if (!_camera) { Debug.LogError("Main menu requires its scene camera.", this); enabled = false; return; }
            _loading = false;
            Application.runInBackground = true;
            _settingsPath = Path.Combine(Application.persistentDataPath, "menu-settings.json");
            _saved = MenuSettings.Load(_settingsPath, out string loadError); _draft = _saved.Clone();
            _archive = new PhotoArchive(Path.Combine(Application.persistentDataPath, "BestPhotos"), PlayerPrefs.GetInt("Lighthouse.Blockout.Best", 0));
            _document = GetComponent<UIDocument>();
            if (!_document) _document = gameObject.AddComponent<UIDocument>();
            _panel = ScriptableObject.CreateInstance<PanelSettings>();
            _panel.name = "Mayak menu runtime panel"; _panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            _panel.scale = Mathf.Max(.75f, Screen.width / 1280f); _panel.sortingOrder = 50;
            _panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("KeeperUI/Theme");
            _document.panelSettings = _panel; _document.enabled = true;
            _root = _document.rootVisualElement;
            _root.styleSheets.Add(Resources.Load<StyleSheet>("KeeperMenu/Menu"));
            Resources.Load<VisualTreeAsset>("KeeperMenu/Menu").CloneTree(_root);
            _root.style.unityFontDefinition = FontDefinition.FromSDFFont(Resources.Load<FontAsset>("KeeperMenu/Fonts/CommissionerSDF"));
            _displayFont = Resources.Load<FontAsset>("KeeperMenu/Fonts/CormorantGaramondSDF");
            foreach (var name in new[] { "gallery-title", "best-score", "dialog-title", "viewer-title", "viewer-price", "empty-title" })
            {
                var element = _root.Q(name) ?? _root.Q(className: name);
                if (element != null) element.style.unityFontDefinition = FontDefinition.FromSDFFont(_displayFont);
            }
            SetImage(_root.Q<Image>("wordmark"), "KeeperMenu/mayak-wordmark");
            SetImage(_root.Q<Image>("sea-chart"), "KeeperMenu/nautical-chart");
            foreach (var icon in _root.Query<Image>(className:"nav-pointer").ToList()) SetImage(icon,"KeeperMenu/menu-pointer");
            foreach (var icon in _root.Query<Image>(className:"icon-back").ToList()) SetImage(icon,"KeeperMenu/icon-back");
            foreach (var icon in _root.Query<Image>(className:"icon-next").ToList()) SetImage(icon,"KeeperMenu/icon-arrow");
            SetImage(_root.Q<Image>(className:"archive-compass"),"KeeperMenu/icon-compass");
            SetImage(_root.Q<Image>(className:"empty-photo"),"KeeperMenu/icon-photo");
            Bind("play", Play); Bind("empty-play", Play);
            Bind("open-settings", () => ShowPage("settings")); Bind("open-gallery", () => ShowPage("gallery"));
            Bind("settings-back", () => ShowPage("home")); Bind("gallery-back", () => ShowPage("home"));
            Bind("quit", AskQuit); Bind("dialog-confirm", () => _confirm?.Invoke()); Bind("dialog-cancel", () => _cancel?.Invoke());
            Bind("viewer-close", ClosePhoto); Bind("viewer-prev", () => OpenPhoto(_currentPhoto-1)); Bind("viewer-next", () => OpenPhoto(_currentPhoto+1));
            BuildSettings();
            var strongFont = Resources.Load<FontAsset>("KeeperMenu/Fonts/CommissionerSemiboldSDF");
            foreach (var value in _values.Values) value.style.unityFontDefinition = FontDefinition.FromSDFFont(strongFont);
            _root.RegisterCallback<GeometryChangedEvent>(Resize);
            _root.RegisterCallback<KeyDownEvent>(KeyDown);
            _root.RegisterCallback<PointerDownEvent>(PointerDown, TrickleDown.TrickleDown);
            _root.RegisterCallback<WheelEvent>(_ => CloseChoice());
            _backdrop = gameObject.AddComponent<MenuBackdrop>(); _backdrop.Initialize(_camera);
            _testAudio = gameObject.AddComponent<AudioSource>(); _testAudio.playOnAwake = false;
            _testAudio.clip = Resources.Load<AudioClip>("KeeperMenu/menu-shutter");
            _clock = _root.schedule.Execute(TickDisplayConfirmation).Every(100);
            ApplyRuntime(_saved);
            if (_music)
            {
                _music.loop = true;
                if (!_music.isPlaying) _music.Play();
            }
            Text("settings-status", loadError ?? "");
            if (_started) OpenHome();
        }

        private void Start()
        {
            _started = true;
            if (!Application.isEditor) Screen.SetResolution(_saved.Width, _saved.Height, DisplayMode(_saved.Display));
            OpenHome();
        }

        public void OpenHome()
        {
            if (_loading) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ShowPage("home");
            ApplyRuntime(_saved);
        }

        public void Play()
        {
            if (!IsOpen) return;
            try
            {
                if (!Application.CanStreamedLevelBeLoaded(KeeperGameScene.ScenePath))
                    throw new InvalidOperationException("Gameplay scene is not included in the build.");
                _loading = true;
                _root.SetEnabled(false);
                SceneManager.LoadSceneAsync(KeeperGameScene.ScenePath, LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                _loading = false;
                _root.SetEnabled(true);
                Debug.LogError("Cannot load gameplay scene: " + exception.Message, this);
                Dialog("Не удалось начать игру", "Не удалось загрузить игровую сцену.", "Назад", "Повторить", HideDialog, Play);
            }
        }

        public void ShowPage(string page)
        {
            if (_page == "settings") { CancelDisplayChange(); _draft = _saved.Clone(); ApplyRuntime(_saved); }
            CloseChoice(); HideDialog(); ClosePhoto();
            _page = page;
            foreach (var name in new[] { "home", "settings", "gallery" }) Show(name, name == page);
            _backdrop.Page = page == "home" ? 0 : page == "settings" ? 1 : 2;
            if (page == "settings") { _draft = _saved.Clone(); RenderSettings(); SelectTab("audio"); }
            if (page == "gallery") RenderGallery();
            if (page == "home")
            {
                var home=_root.Q("home");home.AddToClassList("arriving");
                home.schedule.Execute(()=>home.RemoveFromClassList("arriving")).ExecuteLater(20);
            }
            var scroll = _root.Q<ScrollView>(page); if (scroll != null) scroll.scrollOffset = Vector2.zero;
            _root.schedule.Execute(() => _root.Q<Button>(page == "home" ? "play" : page == "settings" ? "tab-audio" : "gallery-back")?.Focus());
        }

        private void Resize(GeometryChangedEvent evt)
        {
            _panel.scale = Mathf.Max(.75f, Screen.width / 1280f);
            float width = _root.resolvedStyle.width, height = _root.resolvedStyle.height;
            bool compact = width < 800;
            _root.Q("menu").EnableInClassList("compact", compact);
            var form = _root.Q("settings-form");
            form.style.minHeight = Mathf.Max(compact ? 930 : 720, height);
            if (!compact)
            {
                form.style.paddingTop = Mathf.Max(52, height*.072f); form.style.paddingBottom = Mathf.Max(63,height*.088f);
                _root.Q("settings-tabs").style.marginTop = Mathf.Max(29,height*.04f);
                _root.Q("settings-workspace").style.marginTop = Mathf.Max(32,height*.045f);
            }
            else
            {
                form.style.paddingTop=StyleKeyword.Null;form.style.paddingBottom=StyleKeyword.Null;
                _root.Q("settings-tabs").style.marginTop=StyleKeyword.Null;
                _root.Q("settings-workspace").style.marginTop=StyleKeyword.Null;
            }
            var featured=_root.Q("featured-row");
            if(featured!=null)featured.style.height=compact?new StyleLength(StyleKeyword.Auto):new StyleLength(Mathf.Max(350,height-235));
            _root.Q(className:"viewer-image-wrap").style.height = Mathf.Min(height*.66f,700);
            CloseChoice();
        }

        private void KeyDown(KeyDownEvent evt)
        {
            if (!IsOpen) return;
            if (evt.keyCode == KeyCode.Escape)
            {
                if (_openChoice != null) CloseChoice();
                else if (IsShown("dialog")) _cancel?.Invoke();
                else if (IsShown("photo-viewer")) ClosePhoto();
                else if (_page != "home") ShowPage("home");
                else AskQuit();
                evt.StopPropagation();
            }
            else if (IsShown("photo-viewer") && (evt.keyCode == KeyCode.LeftArrow || evt.keyCode == KeyCode.RightArrow))
            { OpenPhoto(_currentPhoto + (evt.keyCode == KeyCode.LeftArrow ? -1 : 1)); evt.StopPropagation(); }
            if (evt.keyCode == KeyCode.Tab && (IsShown("dialog") || IsShown("photo-viewer")))
            {
                var overlay = _root.Q(IsShown("dialog") ? "dialog" : "photo-viewer");
                var buttons = overlay.Query<Button>().ToList();
                int current = buttons.FindIndex(button => button == _root.panel.focusController.focusedElement);
                buttons[(current + (evt.shiftKey ? buttons.Count-1 : 1) + buttons.Count) % buttons.Count].Focus();
                evt.StopPropagation(); _root.panel.focusController.IgnoreEvent(evt);
            }
        }

        private void PointerDown(PointerDownEvent evt)
        {
            var target = evt.target as VisualElement;
            if (_openChoice != null && !_root.Q("choice-popup").Contains(target) && !_openChoice.Button.Contains(target)) CloseChoice();
        }

        private void AskQuit() => Dialog("Выйти из игры?", "", "Остаться", "Выйти", HideDialog, Quit);
        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        private void Dialog(string title, string copy, string cancel, string confirm, Action cancelled, Action confirmed)
        {
            CloseChoice(); _dialogReturn = _root.panel.focusController.focusedElement as VisualElement;
            Text("dialog-title",title); Text("dialog-copy",copy);
            _root.Q<Button>("dialog-cancel").text = cancel; _root.Q<Button>("dialog-confirm").text = confirm;
            _cancel = cancelled; _confirm = confirmed; Show("dialog",true); _root.Q<Button>("dialog-cancel").Focus();
        }
        private void HideDialog() { Show("dialog",false); _confirm = _cancel = null; _dialogReturn?.Focus(); _dialogReturn = null; }
        private void Bind(string name, Action action) => _root.Q<Button>(name).clicked += action;
        private void Text(string name, string value) => _root.Q<Label>(name).text = value;
        private void Show(string name, bool shown) => _root.Q(name).EnableInClassList("hidden",!shown);
        private bool IsShown(string name) => !_root.Q(name).ClassListContains("hidden");
        private static void SetImage(Image image, string resource)
        {
            var vector = Resources.Load<VectorImage>(resource);
            if (vector) image.vectorImage = vector; else image.image = Resources.Load<Texture2D>(resource);
            image.scaleMode = ScaleMode.ScaleToFit;
        }
        private void OnDisable()
        {
            if (_root == null) return;
            CancelDisplayChange(); ClosePhoto(); _clock?.Pause();
            if (_music) _music.Stop();
            _root.UnregisterCallback<GeometryChangedEvent>(Resize); _root.UnregisterCallback<KeyDownEvent>(KeyDown);
            _root.UnregisterCallback<PointerDownEvent>(PointerDown, TrickleDown.TrickleDown);
            ClearGallery(); _root.Clear(); _document.enabled = false;
            _sliders.Clear(); _values.Clear(); _toggles.Clear(); _choices.Clear();
            if (_panel) Destroy(_panel); if (_backdrop) Destroy(_backdrop); if (_testAudio) Destroy(_testAudio);
            _root = null;
        }
    }
}
