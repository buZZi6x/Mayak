using UnityEngine;

namespace LighthouseGame
{
    [RequireComponent(typeof(CharacterController))]
    public partial class BlockoutPlayer : MonoBehaviour
    {
        public Camera View;
        public Transform HeldCamera;
        public Transform Spawn;
        public Transform LookAtSpawn;
        public BlockoutSession Session;
        public BlockoutInteractable Focused;
        public bool InputEnabled = true;
        public float LookSensitivity { get; internal set; } = 1;
        public bool InvertLookY { get; internal set; }
        public bool EncounterControlLocked { get; internal set; }
        public Vector3 ProbeVelocity;
        public float Travelled;
        public int Respawns;
        CharacterController motor;
        Vector3 velocity, heldBase;
        float pitch, kick;

        void Awake()
        {
            motor = GetComponent<CharacterController>();
            heldBase = HeldCamera ? HeldCamera.localPosition : Vector3.zero;
            Application.runInBackground = true;
        }
        void Start() { InitializeIslandBoundary(); InitializeEquipment(); LockCursor(true); }
        public void LockCursor(bool value) { Cursor.lockState = value ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !value; }
        public void Teleport(Vector3 position, Vector3 lookAt)
        {
            RemoveLeviathanShake();
            if (!motor) motor = GetComponent<CharacterController>();
            motor.enabled = false; transform.position = position; motor.enabled = true;
            velocity = Vector3.zero;
            var direction = lookAt - (position + Vector3.up * 1.65f);
            transform.rotation = Quaternion.Euler(0, Quaternion.LookRotation(direction).eulerAngles.y, 0);
            pitch = Mathf.Clamp(-Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg, -80, 80);
            View.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        }
        void Update()
        {
            RemoveLeviathanShake();
            if (Session.MenuOpen) return;
            ReadEquipmentInput();
            if (Session.MenuOpen) return;
            if (BlockoutInput.Press("F1")) Session.BeginRun();
            if (BlockoutInput.Press("F2") && !Session.Running) Session.TogglePreviewWeather();
            var environment=CinematicEnvironment.Active;
            if(environment)
            {
                if(BlockoutInput.Press("F3")&&!Session.Running)environment.NextTime();
                if(BlockoutInput.Press("F4"))environment.ToggleCleanView();
                if(BlockoutInput.Press("F7")&&!Session.Running)environment.TriggerLightning();
                if(BlockoutInput.Press("F5")&&!Session.Running&&Session.Observations)Session.Observations.PreviewNext();
                if(BlockoutInput.Press("F9")&&!Session.Running&&Session.Director.Encounter)Session.Director.Encounter.Preview(true);
                if(BlockoutInput.Press("F10")&&!Session.Running&&Session.Director.Encounter)Session.Director.Encounter.Preview(false);
                if(BlockoutInput.Press("F11")&&!Session.Running&&Session.Director.Encounter)Session.Director.Encounter.Preview(true,false);
                if(BlockoutInput.Press("F8")&&!Session.Running)
                {
                    var fleet=Session.Director.Boat.GetComponent<CoastalFleet>();
                    if(Session.Director.Encounter){Session.Director.Encounter.StopPreview();Session.Director.Encounter.ManualTimeline=true;}
                    if(fleet){if(Session.Observations){Session.Observations.Clear();Session.Observations.Automatic=false;}fleet.Select(fleet.Selected+1);if(Session.Director.Encounter)Session.Director.Encounter.SelectCreature(fleet.Selected>0);Session.Director.FreezePreview=true;Session.Storm=false;Session.Director.Evaluate(23);environment.SetWeather(CoastalWeather.Clear,true);environment.SetHour(16.2f);Session.Player.Teleport(Session.Observation.position,Session.Director.Boat.position+Vector3.up*10);Session.Notify(fleet.VesselName+" · F8 — следующий корабль");}
                }
                if(BlockoutInput.Press("F6")&&!Session.Running){environment.AdvanceTime=!environment.AdvanceTime;Session.Notify(environment.AdvanceTime?"Время движется":"Время суток зафиксировано");}
            }
            bool control = InputEnabled && Cursor.lockState == CursorLockMode.Locked && !Session.GameOver && !Session.Sleeping && !IsInspecting && !EncounterControlLocked && !(Session.Radio && Session.Radio.ControlsOpen);
            if (control)
            {
                Vector2 look = BlockoutInput.Look * LookSensitivity;
                if (InvertLookY) look.y = -look.y;
                transform.Rotate(0, look.x, 0); pitch = Mathf.Clamp(pitch - look.y, -82, 82);
                View.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            }
            ApplyLeviathanShake();
            Vector2 input = control ? BlockoutInput.Move : Vector2.zero;
            Vector3 motion = ProbeVelocity.sqrMagnitude > .001f ? ProbeVelocity : (transform.right * input.x + transform.forward * input.y) * (BlockoutInput.Sprint ? 4.1f : 2.7f);
            if (Session.Sleeping || EncounterControlLocked || Session.GameOver) motion = Vector3.zero;
            if (motor.isGrounded && velocity.y < 0) velocity.y = -2f;
            if (control && BlockoutInput.Press("Space") && motor.isGrounded && !Session.Sleeping) velocity.y = 4.5f;
            velocity.y -= 18f * Time.deltaTime;
            Vector3 before = transform.position;
            if (!EncounterControlLocked && !Session.Sleeping) MoveOnIsland((motion + velocity) * Time.deltaTime);
            else velocity = Vector3.zero;
            Travelled += Vector3.Distance(before, transform.position);
            if (transform.position.y < -2f && Spawn)
            {
                Respawns++; Teleport(Spawn.position, LookAtSpawn.position); Session.Notify("Вернулись на площадку у входа.");
            }
            var previousFocus = Focused;
            Focused = null;
            if (Physics.Raycast(View.transform.position, View.transform.forward, out RaycastHit hit, 3.4f, ~(1 << 2), QueryTriggerInteraction.Collide))
                Focused = hit.collider.GetComponentInParent<BlockoutInteractable>();
            if (previousFocus != Focused) RaisePresentationChanged();
            if (control && BlockoutInput.Press("F")) Interact();
            if (control && BlockoutInput.Shoot && !Session.Sleeping)
            {
                if (CameraEquipped) Session.Capture();
                else if (!UIHidden && SelectedPhoto != null) OpenInspection();
            }
            if (!EncounterControlLocked) View.fieldOfView = Mathf.Lerp(View.fieldOfView, control && CameraEquipped && BlockoutInput.Aim ? 40 : 72, Time.deltaTime * 10);
            kick = Mathf.MoveTowards(kick, 0, Time.deltaTime * 4);
            if (HeldCamera)
            {
                float bob = motion.magnitude > .1f && motor.isGrounded ? Mathf.Sin(Time.time * 10) * .012f : 0;
                var aimed = control && CameraEquipped && BlockoutInput.Aim;
                HeldCamera.localPosition = Vector3.Lerp(HeldCamera.localPosition, heldBase + new Vector3(aimed ? -.18f : 0, bob + (aimed ? .04f : 0), -kick * .065f), Time.deltaTime * 14);
                HeldCamera.localRotation = Quaternion.Euler(-kick * 9, 0, Mathf.Sin(Time.time * 1.7f) * .6f);
            }
        }
        public void ShutterKick() { kick = 1; }
    }
}
