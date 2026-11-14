using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace LighthouseGame
{
    public partial class BlockoutPlayer
    {
        public event Action PresentationChanged;
        public bool UIHidden { get; private set; }
        public bool CameraEquipped { get; private set; } = true;
        public bool IsInspecting { get; private set; }
        public int SelectedSlot { get; private set; } = -1;
        public Light Flashlight { get; private set; }
        public bool FlashlightOn => Flashlight && Flashlight.enabled;
        public BlockoutShot SelectedPhoto => PhotoAt(SelectedSlot);
        private readonly BlockoutShot[] _photoSlots = new BlockoutShot[5];
        private static readonly string[] PhotoKeys = { "1", "2", "3", "4", "5" };
        private bool _inspectionWasLocked;

        private void OnEnable()
        {
            if (Session) Session.PhotosChanged += SyncPhotos;
        }

        private void OnDisable()
        {
            if (Session) Session.PhotosChanged -= SyncPhotos;
            CloseInspection();
        }

        private void InitializeEquipment()
        {
            var torch = new GameObject("Keeper flashlight");
            torch.transform.SetParent(View.transform, false);
            torch.transform.localPosition = new Vector3(.12f, -.12f, .12f);
            Flashlight = torch.AddComponent<Light>();
            Flashlight.type = LightType.Spot;
            Flashlight.color = new Color(1, .91f, .76f);
            Flashlight.range = 28;
            Flashlight.spotAngle = 52;
            Flashlight.innerSpotAngle = 26;
            Flashlight.shadows = LightShadows.Soft;
            torch.AddComponent<HDAdditionalLightData>();
            Flashlight.lightUnit = LightUnit.Lumen;
            Flashlight.intensity = 1600;
            Flashlight.enabled = false;
            Session.HelpVisible = false;
            SyncPhotos();
            RefreshCameraModel();
        }

        private void ReadEquipmentInput()
        {
            if (!InputEnabled) return;
            if (Session.GameOver)
            {
                if (BlockoutInput.Press("Esc")) { Session.RequestMenu(); }
                return;
            }
            if (BlockoutInput.Press("Tab")) ToggleUI();
            if (BlockoutInput.Press("Esc"))
            {
                if (Session.Radio && Session.Radio.ControlsOpen) Session.Radio.CloseControls();
                else if (IsInspecting) CloseInspection();
                else if (Session.GameOver) { Session.RequestMenu(); return; }
                else LockCursor(Cursor.lockState != CursorLockMode.Locked);
            }
            if (Session.GameOver || Session.Sleeping || EncounterControlLocked || (Session.Radio && Session.Radio.ControlsOpen)) return;
            for (int i = 0; i < 5; i++)
                if (BlockoutInput.Press(PhotoKeys[i])) SelectPhoto(i);
            if (BlockoutInput.Press("Q")) ToggleFlashlight();
            if (BlockoutInput.Press("E")) ToggleCamera();
            if (SelectedPhoto != null && !CameraEquipped)
            {
                if ((!BlockoutInput.Sprint && BlockoutInput.Press("Left")) || (Cursor.lockState == CursorLockMode.Locked && BlockoutInput.Scroll > 0)) CyclePhoto(-1);
                if ((!BlockoutInput.Sprint && BlockoutInput.Press("Right")) || (Cursor.lockState == CursorLockMode.Locked && BlockoutInput.Scroll < 0)) CyclePhoto(1);
            }
        }

        public BlockoutShot PhotoAt(int index) => index >= 0 && index < 5 ? _photoSlots[index] : null;

        private void SyncPhotos()
        {
            for (int i = 0; i < 5; i++)
                if (_photoSlots[i] != null && !Session.Shots.Contains(_photoSlots[i])) _photoSlots[i] = null;
            foreach (var photo in Session.Shots)
            {
                if (Array.IndexOf(_photoSlots, photo) >= 0) continue;
                int empty = Array.IndexOf(_photoSlots, null);
                if (empty >= 0) _photoSlots[empty] = photo;
            }
            if (SelectedPhoto == null)
            {
                SelectedSlot = -1;
                CloseInspection();
            }
            RaisePresentationChanged();
        }

        public void SelectPhoto(int index)
        {
            if (Session.GameOver || Session.Sleeping || Session.Busy || EncounterControlLocked || (Session.Radio && Session.Radio.ControlsOpen)) return;
            if (PhotoAt(index) == null) return;
            SelectedSlot = index;
            CameraEquipped = false;
            RefreshCameraModel();
            RaisePresentationChanged();
        }

        public void CyclePhoto(int direction)
        {
            for (int step = 1; step <= 5; step++)
            {
                int index = (SelectedSlot + direction * step + 10) % 5;
                if (PhotoAt(index) == null) continue;
                SelectPhoto(index);
                return;
            }
        }

        public void SwapPhotos(int from, int to)
        {
            if (Session.Busy || Session.Sleeping || Session.GameOver || EncounterControlLocked || (Session.Radio && Session.Radio.ControlsOpen) || from == to || PhotoAt(from) == null || to < 0 || to >= 5) return;
            var held = SelectedPhoto;
            (_photoSlots[from], _photoSlots[to]) = (_photoSlots[to], _photoSlots[from]);
            if (held != null) SelectedSlot = Array.IndexOf(_photoSlots, held);
            RaisePresentationChanged();
        }

        public void ToggleCamera()
        {
            if (Session.GameOver || Session.Sleeping || Session.Busy || EncounterControlLocked || (Session.Radio && Session.Radio.ControlsOpen)) return;
            CloseInspection();
            CameraEquipped = !CameraEquipped;
            RefreshCameraModel();
            RaisePresentationChanged();
        }

        public void RefreshCameraModel()
        {
            if (HeldCamera) HeldCamera.gameObject.SetActive(CameraEquipped && !Session.TakingPhoto && !Session.Sleeping && !EncounterControlLocked);
        }

        public void ToggleFlashlight()
        {
            if (!Flashlight || Session.GameOver || Session.Sleeping || EncounterControlLocked || (Session.Radio && Session.Radio.ControlsOpen)) return;
            Flashlight.enabled = !Flashlight.enabled;
            RaisePresentationChanged();
        }

        public void ToggleUI()
        {
            // Visibility is deliberately independent of input, equipment and the shutter.
            UIHidden = (Session.Radio && Session.Radio.ControlsOpen) || !UIHidden;
            if (UIHidden) { CloseInspection(); Session.Radio?.CloseControls(); }
            if (CinematicEnvironment.Active) CinematicEnvironment.Active.CleanView = UIHidden;
            RaisePresentationChanged();
        }

        public void Interact()
        {
            if (IsInspecting || Session.GameOver || Session.Sleeping || Session.Busy || EncounterControlLocked || (Session.Radio && Session.Radio.ControlsOpen) || !Focused) return;
            Focused.Use(Session);
            RaisePresentationChanged();
        }

        public void ShootFromUI()
        {
            if (CameraEquipped && !IsInspecting && !(Session.Radio && Session.Radio.ControlsOpen)) Session.Capture();
        }

        public void OpenInspection()
        {
            if (UIHidden || CameraEquipped || SelectedPhoto == null || IsInspecting || Session.Busy || Session.GameOver || EncounterControlLocked || (Session.Radio && Session.Radio.ControlsOpen)) return;
            _inspectionWasLocked = Cursor.lockState == CursorLockMode.Locked;
            IsInspecting = true;
            LockCursor(false);
            RaisePresentationChanged();
        }

        public void CloseInspection()
        {
            if (!IsInspecting) return;
            IsInspecting = false;
            if (!Session.GameOver) LockCursor(_inspectionWasLocked);
            RaisePresentationChanged();
        }

        private void RaisePresentationChanged() => PresentationChanged?.Invoke();
    }
}
