using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LighthouseGame
{
    public static class BlockoutInput
    {
        public static Vector2 Move
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                if (k == null) return Vector2.zero;
                return Vector2.ClampMagnitude(new Vector2((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0), (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0)), 1);
#else
                return Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1);
#endif
            }
        }
        public static Vector2 Look
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current == null ? Vector2.zero : Mouse.current.delta.ReadValue() * .09f;
#else
                return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 2f;
#endif
            }
        }
        public static bool Press(string key)
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return false;
            switch (key)
            {
                case "Q": return k.qKey.wasPressedThisFrame;
                case "F": return k.fKey.wasPressedThisFrame;
                case "1": return k.digit1Key.wasPressedThisFrame || k.numpad1Key.wasPressedThisFrame;
                case "2": return k.digit2Key.wasPressedThisFrame || k.numpad2Key.wasPressedThisFrame;
                case "3": return k.digit3Key.wasPressedThisFrame || k.numpad3Key.wasPressedThisFrame;
                case "4": return k.digit4Key.wasPressedThisFrame || k.numpad4Key.wasPressedThisFrame;
                case "5": return k.digit5Key.wasPressedThisFrame || k.numpad5Key.wasPressedThisFrame;
                case "Left": return k.leftArrowKey.wasPressedThisFrame;
                case "Right": return k.rightArrowKey.wasPressedThisFrame;
            }
            if(key=="F5")return k.f5Key.wasPressedThisFrame;
            if(key=="F8")return k.f8Key.wasPressedThisFrame;
            if(key=="F9")return k.f9Key.wasPressedThisFrame;
            if(key=="F10")return k.f10Key.wasPressedThisFrame;
            if(key=="F11")return k.f11Key.wasPressedThisFrame;
            switch (key) { case "E": return k.eKey.wasPressedThisFrame; case "F1": return k.f1Key.wasPressedThisFrame; case "F2": return k.f2Key.wasPressedThisFrame; case "F3": return k.f3Key.wasPressedThisFrame; case "F4": return k.f4Key.wasPressedThisFrame; case "F6": return k.f6Key.wasPressedThisFrame; case "F7": return k.f7Key.wasPressedThisFrame; case "Esc": return k.escapeKey.wasPressedThisFrame; case "Tab": return k.tabKey.wasPressedThisFrame; case "Space": return k.spaceKey.wasPressedThisFrame; }
            return false;
#else
            if (key.Length == 1 && char.IsDigit(key[0]))
                return Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha0 + key[0] - '0')) || Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad0 + key[0] - '0'));
            KeyCode code = key == "Esc" ? KeyCode.Escape : key == "Left" ? KeyCode.LeftArrow : key == "Right" ? KeyCode.RightArrow : (KeyCode)System.Enum.Parse(typeof(KeyCode), key);
            return Input.GetKeyDown(code);
#endif
        }
        public static float Scroll
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current == null ? 0 : Mouse.current.scroll.ReadValue().y;
#else
                return Input.mouseScrollDelta.y;
#endif
            }
        }
        public static bool Shoot
        {
            get {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
                return Input.GetMouseButtonDown(0);
#endif
            }
        }
        public static bool Aim
        {
            get {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null && Mouse.current.rightButton.isPressed;
#else
                return Input.GetMouseButton(1);
#endif
            }
        }
        public static bool Sprint
        {
            get {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
#else
                return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
            }
        }
    }
}
