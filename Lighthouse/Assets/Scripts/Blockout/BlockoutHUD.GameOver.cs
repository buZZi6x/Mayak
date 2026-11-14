using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace LighthouseGame
{
    public partial class BlockoutHUD
    {
        private static readonly CultureInfo ResultCulture = CultureInfo.GetCultureInfo("ru-RU");
        private VisualElement _gameOver, _endSheet, _endStats, _endActions;
        private Label _endTitle, _endReason, _endScore, _endBest;
        private Button _endRestart, _endMenu;
        private bool _gameOverShown;
        private Vector2 _endViewport;

        private void InitializeGameOver()
        {
            _gameOver = _root.Q("game-over");
            _endSheet = _root.Q("result-panel");
            _endStats = _root.Q("result-stats");
            _endActions = _root.Q("result-actions");
            _endTitle = _root.Q<Label>("result-title");
            _endReason = _root.Q<Label>("result-reason");
            _endScore = _root.Q<Label>("result-score");
            _endBest = _root.Q<Label>("result-best");
            _endRestart = _root.Q<Button>("restart");
            _endMenu = _root.Q<Button>("result-menu");
            _gameOverShown = false;
            _endRestart.clicked += RestartFromResult;
            _endMenu.clicked += MenuFromResult;
            _gameOver.RegisterCallback<NavigationMoveEvent>(StopResultNavigation, TrickleDown.TrickleDown);
            _gameOver.RegisterCallback<PointerDownEvent>(ClearResultFocus, TrickleDown.TrickleDown);
            ResizeGameOver(Screen.width, Screen.height);
        }

        private void ReleaseGameOver()
        {
            if (_endRestart != null) { _endRestart.clicked -= RestartFromResult; }
            if (_endMenu != null) { _endMenu.clicked -= MenuFromResult; }
            if (_gameOver != null)
            {
                _gameOver.UnregisterCallback<NavigationMoveEvent>(StopResultNavigation, TrickleDown.TrickleDown);
                _gameOver.UnregisterCallback<PointerDownEvent>(ClearResultFocus, TrickleDown.TrickleDown);
            }
            _gameOverShown = false;
        }

        private void RestartFromResult()
        {
            if (Session.GameOver && !Session.MenuOpen) { Session.BeginRun(); }
        }

        private void MenuFromResult()
        {
            if (Session.GameOver && !Session.MenuOpen) { Session.RequestMenu(); }
        }

        private void RenderGameOver()
        {
            _endScore.text = Session.Score.ToString("N0", ResultCulture);
            _endBest.text = Session.Best.ToString("N0", ResultCulture);
            _endReason.text = Session.EndReason;
            ResizeGameOver(_endViewport.x, _endViewport.y);
            SetShown(_gameOver, Session.GameOver);
            if (_gameOverShown == Session.GameOver) { return; }
            _gameOverShown = Session.GameOver;
            ClearResultFocus(null);
            if (_gameOverShown)
            {
                CancelDrag();
                HideTip();
                Player.CloseInspection();
                Session.Radio?.CloseControls();
                // Focus the outcome first; Tab moves to the two actions.
                _gameOver.schedule.Execute(() => { if (_gameOverShown) { _gameOver.Focus(); } });
            }
            else { _gameOver.panel?.focusController.focusedElement?.Blur(); }
        }

        private void ClearResultFocus(PointerDownEvent evt)
        {
            _endRestart.RemoveFromClassList("end-focus");
            _endMenu.RemoveFromClassList("end-focus");
        }

        private void FocusResult(Button button)
        {
            _endRestart.EnableInClassList("end-focus", button == _endRestart);
            _endMenu.EnableInClassList("end-focus", button == _endMenu);
            button.Focus();
        }

        private void StopResultNavigation(NavigationMoveEvent evt)
        {
            // KeyDown owns the two-button cycle; avoid a second navigation step.
            evt.StopPropagation();
            _root.panel.focusController.IgnoreEvent(evt);
        }

        private void HandleGameOverKey(KeyDownEvent evt)
        {
            var focused = _root.panel?.focusController.focusedElement;
            if (evt.keyCode == KeyCode.Tab || evt.keyCode == KeyCode.LeftArrow || evt.keyCode == KeyCode.RightArrow
                || evt.keyCode == KeyCode.UpArrow || evt.keyCode == KeyCode.DownArrow)
            {
                FocusResult(focused == _endRestart ? _endMenu : focused == _endMenu ? _endRestart : evt.shiftKey ? _endMenu : _endRestart);
            }
            else if (evt.keyCode == KeyCode.Home) { FocusResult(_endRestart); }
            else if (evt.keyCode == KeyCode.End) { FocusResult(_endMenu); }
            else if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter || evt.keyCode == KeyCode.Space)
            {
                if (focused == _endRestart) { RestartFromResult(); }
                else if (focused == _endMenu) { MenuFromResult(); }
            }
            // F1/Escape stay owned by the existing player input, exactly once.
            evt.StopPropagation();
            _root.panel?.focusController.IgnoreEvent(evt);
        }

        private void ResizeGameOver(float width, float height)
        {
            if (_endSheet == null || width <= 0 || height <= 0) { return; }
            _endViewport = new Vector2(width, height);
            bool narrow = width <= 680, shortScreen = !narrow && height <= 540;
            float unit = narrow ? 13 : shortScreen ? 12 : Mathf.Clamp(width * .01f, 13, 19);
            _endSheet.style.width = Mathf.Min(Mathf.Clamp(width * .48f, 640, 920), width - (narrow ? 32 : 40));
            _endSheet.style.paddingTop = narrow ? 38 : shortScreen ? 28 : unit * 2.8f;
            _endSheet.style.paddingBottom = narrow ? 32 : shortScreen ? 28 : unit * 2.65f;
            _endSheet.style.paddingLeft = _endSheet.style.paddingRight = narrow ? 26 : shortScreen ? 32 : unit * 2.4f;
            float titleSize = narrow ? Mathf.Clamp(width * .07f, 31, 46) : unit * 3.8f;
            _endTitle.style.fontSize = titleSize;
            _endTitle.style.height = titleSize * 1.1f;
            _endTitle.style.letterSpacing = -.028f * titleSize;
            _endReason.style.marginTop = narrow ? 14 : unit * .85f;
            _endReason.style.fontSize = narrow ? 15 : unit * 1.35f;
            _endReason.style.minHeight = (narrow ? 15 : unit * 1.35f) * 1.5f;
            _endStats.style.marginTop = narrow ? 32 : shortScreen ? 22 : unit * 2.5f;
            _endStats.style.marginBottom = narrow ? 32 : shortScreen ? 22 : unit * 2.7f;
            _endStats.Q(className: "end-current").style.marginRight = narrow ? 12 : unit * 1.5f;
            _endStats.Query<Label>(className: "end-stat-label").ForEach(label =>
            {
                label.style.fontSize = narrow ? 14 : unit * 1.3f;
                label.style.height = (narrow ? 14 : unit * 1.3f) * 1.4f;
                label.style.marginBottom = unit * .5f;
            });
            int magnitude = Mathf.Max(Session.Score, Session.Best);
            float scoreSize = narrow ? 58 : unit * 4.8f, bestSize = narrow ? 46 : unit * 3.9f;
            if (magnitude >= 100000000) { scoreSize = narrow ? 18 : unit * 2.3f; bestSize = narrow ? 18 : unit * 2; }
            else if (magnitude >= 100000) { scoreSize = narrow ? 32 : unit * 3.4f; bestSize = narrow ? 28 : unit * 2.8f; }
            _endScore.style.fontSize = scoreSize;
            _endBest.style.fontSize = bestSize;
            _endScore.style.height = _endBest.style.height = scoreSize;
            _endBest.style.paddingTop = scoreSize - bestSize;
            _endScore.style.letterSpacing = -.025f * scoreSize;
            _endBest.style.letterSpacing = -.025f * bestSize;
            _endActions.style.flexDirection = narrow ? FlexDirection.Column : FlexDirection.Row;
            _endRestart.style.marginRight = narrow ? 0 : unit * 1.3f;
            _endRestart.style.marginBottom = narrow ? 12 : 0;
            foreach (var button in new[] { _endRestart, _endMenu })
            {
                button.style.height = narrow ? 50 : unit * 3.7f;
                button.style.minHeight = narrow ? 50 : unit * 3.7f;
                button.style.paddingLeft = button.style.paddingRight = narrow ? 16 : unit * 1.15f;
                button.style.paddingTop = button.style.paddingBottom = narrow ? 12 : unit * .7f;
                button.Q<Label>(className: "end-action-label").style.fontSize = narrow ? 17 : unit * 1.3f;
                var key = button.Q<Label>(className: "end-key");
                key.style.minWidth = narrow ? 30 : unit * 1.9f;
                key.style.height = narrow ? 26 : unit * 1.8f;
                key.style.fontSize = narrow ? 12 : unit * .9f;
                key.style.paddingLeft = key.style.paddingRight = unit * .3f;
                key.style.marginLeft = unit * .8f;
            }
            float inset = unit * 1.15f;
            _endSheet.Query(className: "end-corner").ForEach(corner =>
            {
                if (corner.ClassListContains("end-tl") || corner.ClassListContains("end-bl")) { corner.style.left = inset; }
                else { corner.style.right = inset; }
                if (corner.ClassListContains("end-tl") || corner.ClassListContains("end-tr")) { corner.style.top = inset; }
                else { corner.style.bottom = inset; }
            });
        }
    }
}
