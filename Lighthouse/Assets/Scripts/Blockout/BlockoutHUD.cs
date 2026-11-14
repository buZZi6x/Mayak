using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace LighthouseGame
{
    [DefaultExecutionOrder(100)]
    public partial class BlockoutHUD : MonoBehaviour
    {
        public BlockoutSession Session; // Preserve the existing scene and builder reference.
        public VisualElement Root => _root;
        public bool IsDragging => _dragFrom >= 0;
        private UIDocument _document;
        private PanelSettings _panel;
        private Font _font;
        private VisualElement _root, _hud, _tooltip;
        private readonly VisualElement[] _slots = new VisualElement[5];
        private BlockoutPlayer Player => Session.Player;
        private int _dragFrom = -1, _pointerId, _dropSlot = -1;
        private bool _dragMoved;
        private Vector2 _dragStart;
        private VisualElement _captor;

        private void OnEnable()
        {
            if (!Session) Session = FindAnyObjectByType<BlockoutSession>();
            _document = GetComponent<UIDocument>();
            if (!_document) _document = gameObject.AddComponent<UIDocument>();
            _panel = ScriptableObject.CreateInstance<PanelSettings>();
            _panel.name = "Keeper HUD runtime panel";
            _panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            _panel.sortingOrder = 10;
            _panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("KeeperUI/Theme");
            _document.panelSettings = _panel;
            _document.enabled = true;
            _root = _document.rootVisualElement;
            _root.pickingMode = PickingMode.Ignore;
            _root.styleSheets.Add(Resources.Load<StyleSheet>("KeeperUI/Hud"));
            Resources.Load<VisualTreeAsset>("KeeperUI/Hud").CloneTree(_root);
            var authoredFont = Resources.Load<UnityEngine.TextCore.Text.FontAsset>("KeeperMenu/Fonts/CommissionerSDF");
            if (authoredFont) _root.style.unityFontDefinition = FontDefinition.FromSDFFont(authoredFont);
            else
            {
                _font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial" }, 16);
                _root.style.unityFontDefinition = FontDefinition.FromFont(_font);
            }
            _hud = _root.Q("hud"); _tooltip = _root.Q("tooltip");
            _root.Query<Image>().ForEach(img =>
            {
                if (!img.name.StartsWith("icon-")) return;
                string icon = img.name.Substring(5);
                img.image = Icon(icon == "prev" || icon == "next" ? "chevron" : icon);
            });
            var template = Resources.Load<VisualTreeAsset>("KeeperUI/PhotoSlot");
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                var container = template.CloneTree(); container.AddToClassList("slot-container");
                _root.Q("slots").Add(container);
                var slot = container.Q("photo-slot"); slot.name = "slot-" + (i + 1); _slots[i] = slot;
                slot.Q<Label>("number").text = (i + 1).ToString();
                slot.Q<Image>("empty-icon").image = Icon("photo");
                slot.Q<Image>("sent").image = Icon("check");
                slot.Q<Image>("photo").scaleMode = ScaleMode.ScaleAndCrop;
                slot.RegisterCallback<PointerDownEvent>(e => BeginDrag(e, index));
                slot.RegisterCallback<PointerMoveEvent>(MoveDrag);
                slot.RegisterCallback<PointerUpEvent>(EndDrag);
                slot.RegisterCallback<PointerCaptureOutEvent>(_ => CancelDrag());
                slot.RegisterCallback<PointerCancelEvent>(_ => CancelDrag());
                slot.RegisterCallback<PointerEnterEvent>(_ => ShowPhotoTip(index, slot));
                slot.RegisterCallback<PointerLeaveEvent>(_ => HideTip());
                slot.RegisterCallback<KeyDownEvent>(e =>
                {
                    if (e.shiftKey && (e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.RightArrow))
                    {
                        int next = (index + (e.keyCode == KeyCode.LeftArrow ? 4 : 1)) % 5;
                        Player.SwapPhotos(index, next); _slots[next].Focus(); e.StopPropagation();
                    }
                });
            }
            Bind("torch-button", Player.ToggleFlashlight); Bind("camera-button", Player.ToggleCamera);
            Bind("clean-button", Player.ToggleUI); Bind("held", Player.OpenInspection);
            Bind("object-action", Player.Interact); Bind("door", Player.Interact);
            Bind("inspect-close", Player.CloseInspection); Bind("inspect-prev", () => Player.CyclePhoto(-1));
            Bind("inspect-next", () => Player.CyclePhoto(1));
            InitializeGameOver();
            _root.Q("inspector").RegisterCallback<PointerDownEvent>(e =>
            { if (e.target == _root.Q("inspector")) Player.CloseInspection(); });
            _root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            _root.RegisterCallback<WheelEvent>(OnWheel);
            _root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            _root.Q("shade").generateVisualContent += DrawShade;
            Tip("torch-button", "Фонарь", "Освещает предметы перед вами. Работает и с камерой в руках.", "Q  Включить / выключить");
            Tip("camera-button", "Камера", "На день — пять кадров. Стоимость фиксируется при съёмке.", "E  Достать / убрать · ЛКМ  Снять · ПКМ  Приблизить");
            Tip("clean-button", "Чистый вид", "Скрывает только интерфейс. Можно продолжать снимать и взаимодействовать.", "Tab  Скрыть / вернуть UI");
            Tip("fuel-stat", "Топливо", "Остаток переходит на следующий день. За каждые новые 100 очков после сна — ещё 10 секунд.", "Расход начинается после подготовки");
            Tip("score-stat", "Счёт вахты", "Сумма стоимости всех снимков за забег. Повторная съёмка категории даёт половину предыдущей цены.", "Снимки подтверждаются автоматически");
            Tip("held", "Рассмотреть фотографию", "Откройте снимок крупно. Колесо и стрелки переключают имеющиеся фотографии.", "ЛКМ  Рассмотреть · 1–5  Выбрать");
            Session.StateChanged += RenderState; Session.PhotosChanged += RenderPhotos;
            Session.PhotoVisibilityChanged += RenderVisibility; Session.EffectsChanged += RenderEffects;
            Session.LampRepairProgressChanged += RenderRepairProgress;
            Player.PresentationChanged += RenderPresentation;
            Session.HelpVisible = false;
            RenderState(); RenderPhotos(); RenderPresentation(); RenderEffects(); HideTip();
        }

        private void OnDisable()
        {
            ReleaseGameOver();
            CancelDrag();
            if (Session)
            {
                Session.StateChanged -= RenderState; Session.PhotosChanged -= RenderPhotos;
                Session.PhotoVisibilityChanged -= RenderVisibility; Session.EffectsChanged -= RenderEffects;
                Session.LampRepairProgressChanged -= RenderRepairProgress;
                if (Player) Player.PresentationChanged -= RenderPresentation;
            }
            if (_root != null)
            {
                _root.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
                _root.UnregisterCallback<WheelEvent>(OnWheel); _root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
                _root.Clear();
            }
            if (_document) _document.enabled = false;
            if (_panel) Destroy(_panel);
            if (_font) Destroy(_font);
            _root = null;
        }

        private void Bind(string name, System.Action action) => _root.Q<Button>(name).clicked += action;
        private static Texture2D Icon(string name) => Resources.Load<Texture2D>("KeeperUI/Icons/" + name);
        private void Text(string name, string text) => _root.Q<Label>(name).text = text;
        private void Show(string name, bool value) => SetShown(_root.Q(name), value);
        private static void SetShown(VisualElement element, bool value) => element.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;

        private void RenderState()
        {
            int fuel = Mathf.CeilToInt(Session.Fuel);
            Text("fuel", (fuel / 60).ToString("00") + ":" + (fuel % 60).ToString("00")); Text("score", Session.Score.ToString());
            Text("notice", Session.Message); Show("notice", Time.time < Session.MessageUntil && !Session.GameOver);
            Text("work-status", "Действие… " + Mathf.CeilToInt(Mathf.Max(0, Session.BusyUntil - Time.time)) + " с");
            Show("work-status", !Session.GameOver && Session.Busy && !Session.IsRepairingLamp);
            Text("lamp-warning", "До поломки маяка · " + Mathf.CeilToInt(Session.LampWarningRemaining) + " с");
            Show("lamp-warning", Session.LampNeedsRepair);
            RenderGameOver();
            Show("viewfinder", Player.CameraEquipped && !Session.GameOver);
            RenderObject(); RenderVisibility();
        }

        private void RenderPhotos()
        {
            for (int i = 0; i < 5; i++)
            {
                var slot = _slots[i]; var shot = Player.PhotoAt(i);
                slot.EnableInClassList("empty", shot == null);
                slot.EnableInClassList("selected", shot != null && i == Player.SelectedSlot && !Player.CameraEquipped);
                slot.Q<Image>("photo").image = shot?.Image;
                SetShown(slot.Q("photo"), shot != null); SetShown(slot.Q("caption"), shot != null); SetShown(slot.Q("empty-icon"), shot == null);
                if (shot == null) continue;
                if (shot.Image && _root.panel != null) RuntimePanelUtils.SetTextureDirty(_root.panel, shot.Image);
                slot.Q<Label>("price").text = "+" + shot.Price;
                SetShown(slot.Q("sent"), shot.Sent); SetShown(slot.Q("pending"), !shot.Sent);
            }
            Text("exposures", "КАМЕРА   ·   Осталось кадров: " + (5 - Session.Shots.Count));
            var selected = Player.SelectedPhoto;
            Show("held", selected != null && !Player.CameraEquipped && !Player.IsInspecting);
            if (selected == null) return;
            _root.Q<Image>("held-image").image = selected.Image; _root.Q<Image>("held-image").scaleMode = ScaleMode.ScaleAndCrop;
            _root.Q<Image>("inspect-image").image = selected.Image; _root.Q<Image>("inspect-image").scaleMode = ScaleMode.ScaleToFit;
            Text("held-title", selected.Label); Text("held-price", "+" + selected.Price);
            Text("inspect-title", selected.Label); Text("inspect-price", "+" + selected.Price + " очк.");
            Text("inspect-index", "СНИМОК " + (Player.SelectedSlot + 1));
            Text("inspect-status", selected.Sent ? "Передан в штаб" : "Ожидает отправки в штаб");
        }

        private void RenderPresentation()
        {
            if (Player.UIHidden || Player.IsInspecting) CancelDrag();
            HideTip(); Text("torch-action", Player.FlashlightOn ? "Выключить" : "Включить");
            Text("camera-action", Player.CameraEquipped ? "Убрать" : "Достать");
            _root.Q("torch-dot").EnableInClassList("on", Player.FlashlightOn);
            _root.Q("camera-dot").EnableInClassList("on", Player.CameraEquipped);
            Show("viewfinder", Player.CameraEquipped && !Session.GameOver); Show("inspector", Player.IsInspecting);
            RenderPhotos(); RenderObject(); RenderVisibility();
        }

        private void RenderVisibility()
        {
            SetShown(_hud, !Session.GameOver && !Player.UIHidden && !Session.TakingPhoto);
            SetShown(_gameOver, Session.GameOver);
            Show("sleep-fade", !Session.TakingPhoto); Show("flash", !Session.TakingPhoto);
        }
        private void RenderEffects()
        { _root.Q("sleep-fade").style.opacity = Session.Fade; _root.Q("flash").style.opacity = Session.Flash; }

        private void RenderRepairProgress()
        {
            Show("lamp-repair-progress", Session.IsRepairingLamp);
            _root.Q("lamp-repair-fill").style.width = Length.Percent(Session.LampRepairProgress * 100);
        }

        private void RenderObject()
        {
            var target = Session.IsRepairingLamp ? Session.RepairStation : Player.Focused;
            bool available = target && !Session.GameOver && !Player.IsInspecting;
            bool door = available && target.Kind == BlockoutInteractable.Station.Door;
            Show("door", door); Show("world-card", available && !door);
            if (!available) return;
            if (door) { Text("door-label", target.Open ? "— Закрыть дверь" : "— Открыть дверь"); return; }
            string title, copy, action, duration, icon;
            switch (target.Kind)
            {
                case BlockoutInteractable.Station.Lamp:
                    title = "Лампа маяка"; icon = "lamp";
                    action = Session.IsRepairingLamp ? "Ремонт маяка…" : Session.LampNeedsRepair ? "Починить маяк" : "Ремонт не требуется";
                    duration = Session.LampNeedsRepair && !Session.IsRepairingLamp ? "6 с" : "";
                    copy = "Частое мигание предупреждает о поломке через 20 секунд. Во время ремонта отсчёт приостановлен."; break;
                case BlockoutInteractable.Station.Radio:
                    title = "Радиостанция"; icon = "radio"; action = "Настроить радио"; duration = "";
                    copy = "Голоса берега, погода и музыка. Настройте волну или выключите приёмник."; break;
                case BlockoutInteractable.Station.Bed:
                    title = "Кровать смотрителя"; icon = "bed"; action = "Лечь спать"; duration = "";
                    copy = "Сразу начать следующий день. Остаток топлива и бонус за снимки переносятся."; break;
                default:
                    title = "Подзорная труба"; icon = "telescope"; action = "Наблюдать"; duration = "";
                    copy = "Рассмотрите море. Достаньте камеру, чтобы сохранить увиденное."; break;
            }
            Text("object-title", title); Text("object-description", copy); Text("object-action-label", action); Text("object-duration", duration);
            _root.Q<Image>("object-icon").image = Icon(icon);
            _root.Q<Button>("object-action").SetEnabled(!Session.Busy && !Session.Sleeping
                && (target.Kind != BlockoutInteractable.Station.Lamp || (Session.LampNeedsRepair && Session.Preparation <= 0)));
            RenderRepairProgress();
        }

        private void Tip(string name, string title, string copy, string footer)
        {
            var target = _root.Q(name);
            target.RegisterCallback<PointerEnterEvent>(_ => ShowTip(target, title, copy, footer, null));
            target.RegisterCallback<PointerLeaveEvent>(_ => HideTip());
        }
        private void ShowPhotoTip(int index, VisualElement target)
        {
            var shot = Player.PhotoAt(index);
            ShowTip(target, shot?.Label ?? "Свободный слот", shot != null ? "+" + shot.Price + " очк. · " + (shot.Sent ? "Передан в штаб" : "Ожидает отправки") : "Здесь появится один из пяти снимков текущего дня.",
                shot != null ? (index + 1) + "  Взять · Двойной клик  Рассмотреть · Перетащить  Переставить" : "E  Камера · ЛКМ  Снимок", shot?.Image);
        }
        private void ShowTip(VisualElement target, string title, string copy, string footer, Texture2D photo)
        {
            if (Player.UIHidden || Player.IsInspecting || IsDragging || Cursor.lockState == CursorLockMode.Locked) return;
            Text("tip-title", title); Text("tip-copy", copy); Text("tip-footer", footer);
            _root.Q<Image>("tip-image").image = photo; Show("tip-image", photo); SetShown(_tooltip, true);
            var bounds = target.worldBound;
            _tooltip.style.left = Mathf.Clamp(bounds.center.x - 146, 12, Mathf.Max(12, _root.resolvedStyle.width - 304));
            float height = photo ? 290 : 170;
            _tooltip.style.top = bounds.y > height + 20 ? bounds.y - height - 12 : bounds.yMax + 12;
        }
        private void HideTip() { if (_tooltip != null) SetShown(_tooltip, false); }

        private void BeginDrag(PointerDownEvent e, int index)
        {
            if (e.button != 0 || Cursor.lockState == CursorLockMode.Locked || Session.Busy) return;
            if (e.clickCount == 2) { Player.SelectPhoto(index); Player.OpenInspection(); e.StopPropagation(); return; }
            if (Player.PhotoAt(index) == null) return;
            CancelDrag(); HideTip(); _dragFrom = index; _pointerId = e.pointerId; _dragStart = e.position;
            _captor = _slots[index]; _captor.CapturePointer(_pointerId); e.StopPropagation();
        }
        private void MoveDrag(PointerMoveEvent e)
        {
            if (_dragFrom < 0 || e.pointerId != _pointerId) return;
            _dragMoved |= Vector2.Distance(_dragStart, e.position) > 7;
            _slots[_dragFrom].EnableInClassList("dragging", _dragMoved); _dropSlot = -1;
            for (int i = 0; i < 5; i++)
            {
                bool hit = _dragMoved && _slots[i].worldBound.Contains(e.position);
                _slots[i].EnableInClassList("drop-target", hit); if (hit) _dropSlot = i;
            }
            e.StopPropagation();
        }
        private void EndDrag(PointerUpEvent e)
        {
            if (_dragFrom < 0 || e.pointerId != _pointerId) return;
            int from = _dragFrom, to = _dropSlot; bool moved = _dragMoved; CancelDrag();
            if (moved && to >= 0) Player.SwapPhotos(from, to); else if (!moved) Player.SelectPhoto(from);
            e.StopPropagation();
        }
        public void CancelDrag()
        {
            _dragFrom = -1; _dropSlot = -1; _dragMoved = false;
            var captor = _captor; _captor = null;
            if (captor != null && captor.HasPointerCapture(_pointerId)) captor.ReleasePointer(_pointerId);
            foreach (var slot in _slots) { slot?.RemoveFromClassList("dragging"); slot?.RemoveFromClassList("drop-target"); }
        }
        private void OnKeyDown(KeyDownEvent e)
        {
            if (Session.GameOver) { HandleGameOverKey(e); return; }
            if (e.keyCode == KeyCode.Escape) CancelDrag();
            if (e.keyCode == KeyCode.Tab) e.StopPropagation();
        }
        private void OnWheel(WheelEvent e)
        {
            if (Session.GameOver || Player.SelectedPhoto == null || Player.CameraEquipped || Player.UIHidden) return;
            Player.CyclePhoto(e.delta.y > 0 ? 1 : -1); e.StopPropagation();
        }
        private void OnApplicationFocus(bool focus) { if (!focus) { CancelDrag(); HideTip(); } }

        private void OnGeometryChanged(GeometryChangedEvent e)
        {
            float width = e.newRect.width, height = e.newRect.height;
            ResizeGameOver(width, height);
            float pad = Mathf.Clamp(width * .025f, 22, 52), slotWidth = Mathf.Clamp(width * .0755f, width < 600 ? 62 : 88, 142);
            _hud.EnableInClassList("compact", width < 1050);
            _hud.EnableInClassList("wide", width >= 1800);
            _root.Q("stats").style.top = pad; _root.Q("stats").style.right = pad;
            _root.Q("equipment").style.left = pad; _root.Q("clean-button").style.right = pad;
            foreach (var slot in _slots) { slot.style.width = slotWidth; slot.style.height = slotWidth * .88f; }
            float heldWidth = Mathf.Clamp(width * .173f, 210, 334);
            _root.Q("held").style.width = heldWidth; _root.Q("held-image").style.height = (heldWidth - 14) / 1.58f;
            _root.Q("inspect-image").style.height = Mathf.Min(height * .58f, Mathf.Min(920, width * .84f) * .53f);
            _root.Q("fuel").style.fontSize = Mathf.Clamp(width * .015f, 19, 27);
            _root.Q("score").style.fontSize = Mathf.Clamp(width * .018f, 24, 34); HideTip();
        }
        private void DrawShade(MeshGenerationContext context)
        {
            var rect = context.visualElement.contentRect;
            var mesh = context.Allocate(8, 12);
            for (int band = 0; band < 2; band++)
            {
                float top = band == 0 ? 0 : rect.height * .62f, bottom = band == 0 ? rect.height * .22f : rect.height;
                Color32 a = band == 0 ? new Color32(5, 12, 23, 181) : new Color32(5, 10, 18, 0);
                Color32 b = band == 0 ? new Color32(5, 12, 23, 0) : new Color32(5, 10, 18, 199);
                mesh.SetNextVertex(new Vertex { position = new Vector3(0, top, Vertex.nearZ), tint = a });
                mesh.SetNextVertex(new Vertex { position = new Vector3(rect.width, top, Vertex.nearZ), tint = a });
                mesh.SetNextVertex(new Vertex { position = new Vector3(rect.width, bottom, Vertex.nearZ), tint = b });
                mesh.SetNextVertex(new Vertex { position = new Vector3(0, bottom, Vertex.nearZ), tint = b });
                ushort n = (ushort)(band * 4);
                foreach (ushort j in new ushort[] { 0, 1, 2, 2, 3, 0 }) mesh.SetNextIndex((ushort)(n + j));
            }
        }
    }
}
