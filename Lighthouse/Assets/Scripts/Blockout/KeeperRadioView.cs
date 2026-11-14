using UnityEngine;
using UnityEngine.UIElements;

namespace LighthouseGame
{
    public sealed class KeeperRadioView : MonoBehaviour
    {
        private KeeperRadio _receiver;
        private UIDocument _document;
        private PanelSettings _panel;
        private VisualElement _root, _overlay, _stage, _knob;
        private Slider _wave;
        private int _pointer = -1;
        private Vector2 _dragStart;
        private float _startFrequency;
        public VisualElement Root => _root;

        public void Initialize(KeeperRadio receiver) { _receiver = receiver; if (isActiveAndEnabled) Setup(); }
        private void OnEnable() { if (_receiver) Setup(); }
        private void Setup()
        {
            if (_root != null) return;
            _document = gameObject.AddComponent<UIDocument>();
            _panel = ScriptableObject.CreateInstance<PanelSettings>();
            _panel.name = "Keeper radio"; _panel.scaleMode = PanelScaleMode.ConstantPixelSize; _panel.sortingOrder = 25;
            _panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("KeeperUI/Theme");
            _document.panelSettings = _panel;
            _root = _document.rootVisualElement; _root.pickingMode = PickingMode.Ignore;
            _root.styleSheets.Add(Resources.Load<StyleSheet>("KeeperRadio/Radio"));
            Resources.Load<VisualTreeAsset>("KeeperRadio/Radio").CloneTree(_root);
            _root.style.unityFontDefinition = FontDefinition.FromSDFFont(Resources.Load<UnityEngine.TextCore.Text.FontAsset>("KeeperMenu/Fonts/CommissionerSDF"));
            _overlay = _root.Q("radio-overlay"); _stage = _root.Q("radio-stage"); _knob = _root.Q("radio-knob");
            var plate = _root.Q<Image>("radio-plate"); plate.image = Resources.Load<Texture2D>("KeeperRadio/receiver-plate"); plate.scaleMode = ScaleMode.StretchToFill;
            _wave = _root.Q<Slider>("radio-wave"); _wave.pageSize = .1f;
            _wave.RegisterValueChangedCallback(e => _receiver.Tune(e.newValue));
            _root.Q<Button>("radio-on").clicked += () => _receiver.SetPower(true);
            _root.Q<Button>("radio-off").clicked += () => _receiver.SetPower(false);
            _root.Q<Button>("radio-close").clicked += _receiver.CloseControls;
            _knob.RegisterCallback<PointerDownEvent>(BeginDrag);
            _knob.RegisterCallback<PointerMoveEvent>(Drag);
            _knob.RegisterCallback<PointerUpEvent>(_ => EndDrag());
            _knob.RegisterCallback<PointerCancelEvent>(_ => EndDrag());
            _knob.RegisterCallback<PointerCaptureOutEvent>(_ => _pointer = -1);
            _knob.RegisterCallback<WheelEvent>(e => { _receiver.Tune(_receiver.Frequency + (e.delta.y < 0 ? .1f : -.1f)); e.StopPropagation(); });
            _knob.RegisterCallback<KeyDownEvent>(TurnKey);
            _root.RegisterCallback<GeometryChangedEvent>(Resize);
            // Escape is owned by the player input, so a single press cannot close and unlock twice.
            _overlay.RegisterCallback<KeyDownEvent>(e => e.StopPropagation());
            _receiver.Changed += Render; _receiver.ControlsRequested += Open;
            Render();
        }

        private void Open()
        {
            Render(); Resize(null); _wave.Focus();
        }
        private void Render()
        {
            if (_root == null) return;
            _overlay.style.display = _receiver.ControlsOpen ? DisplayStyle.Flex : DisplayStyle.None;
            _overlay.EnableInClassList("off", !_receiver.IsPowered);
            _root.Q<Label>("radio-station").text = _receiver.StationName.ToUpperInvariant();
            _root.Q<Label>("radio-frequency").text = _receiver.Frequency.ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("ru-RU")) + " МГц";
            _root.Q<Label>("radio-transcript").text = _receiver.Subtitle;
            _root.Q<Label>("radio-music").text = _receiver.IsPowered && _receiver.Band != RadioBand.Static ? _receiver.CurrentMusic : "";
            _wave.SetValueWithoutNotify(_receiver.Frequency);
            _root.Q("radio-knob-rotor").style.rotate = new Rotate((_receiver.Frequency - 94.2f) * 12.5f);
            if (!_receiver.ControlsOpen) EndDrag();
        }
        private void Resize(GeometryChangedEvent evt)
        {
            float width = _root.resolvedStyle.width, height = _root.resolvedStyle.height;
            if (float.IsNaN(width) || width <= 0 || height <= 0) return;
            float stageWidth = width < height ? width * 1.4f : Mathf.Min(width, height * 1672f / 941f);
            float stageHeight = stageWidth * 941f / 1672f;
            _stage.style.width = stageWidth; _stage.style.height = stageHeight;
            _stage.style.left = (width - stageWidth) / 2; _stage.style.top = (height - stageHeight) / 2;
            _root.Q("radio-station").style.fontSize = stageWidth * .0185f;
            _root.Q("radio-frequency").style.fontSize = stageWidth * .018f;
            _root.Q("radio-transcript").style.fontSize = stageWidth * (width < height ? .025f : .018f);
            _root.Q("radio-music").style.fontSize = stageWidth * .011f;
            _root.Q("radio-on").style.fontSize = _root.Q("radio-off").style.fontSize = stageWidth * .017f;
            _root.Q("radio-close").style.fontSize = Mathf.Clamp(stageWidth * .03f, 24, 46);
        }
        private void BeginDrag(PointerDownEvent e)
        {
            if (e.button != 0) return;
            _pointer = e.pointerId; _dragStart = e.position; _startFrequency = _receiver.Frequency;
            _knob.CapturePointer(_pointer); _knob.Focus(); e.StopPropagation();
        }
        private void Drag(PointerMoveEvent e)
        {
            if (_pointer != e.pointerId) return;
            Vector2 delta = (Vector2)e.position - _dragStart;
            _receiver.Tune(_startFrequency + (delta.x + delta.y) * .065f); e.StopPropagation();
        }
        private void EndDrag()
        {
            int pointer = _pointer; _pointer = -1;
            if (pointer >= 0 && _knob != null && _knob.HasPointerCapture(pointer)) _knob.ReleasePointer(pointer);
        }
        private void TurnKey(KeyDownEvent e)
        {
            float value = _receiver.Frequency;
            switch (e.keyCode)
            {
                case KeyCode.Home: value = 88; break;
                case KeyCode.End: value = 108; break;
                case KeyCode.RightArrow: case KeyCode.UpArrow: value += .1f; break;
                case KeyCode.LeftArrow: case KeyCode.DownArrow: value -= .1f; break;
                case KeyCode.PageUp: value++; break;
                case KeyCode.PageDown: value--; break;
                default: return;
            }
            _receiver.Tune(value); e.StopPropagation();
        }
        private void OnDisable()
        {
            EndDrag();
            if (_receiver) { _receiver.Changed -= Render; _receiver.ControlsRequested -= Open; _receiver.CloseControls(); }
            if (_root != null) { _root.UnregisterCallback<GeometryChangedEvent>(Resize); _root.Clear(); _root = null; }
            if (_document) Destroy(_document);
            if (_panel) Destroy(_panel);
        }
    }
}
