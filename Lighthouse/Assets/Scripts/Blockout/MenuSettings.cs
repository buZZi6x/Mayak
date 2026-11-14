using System;
using System.IO;
using UnityEngine;

namespace LighthouseGame
{
    [Serializable]
    public sealed class MenuSettings
    {
        public int Version = 1;
        public int Master = 80, Ambience = 75, Effects = 80;
        public int Display, Width = 1920, Height = 1080, FrameLimit = 60;
        public bool VSync = true, Glitches = true, InvertY;
        public int Brightness = 100;
        public float Sensitivity = 1;

        public MenuSettings Clone() => (MenuSettings)MemberwiseClone();
        public bool SameAs(MenuSettings other) => other != null && JsonUtility.ToJson(this) == JsonUtility.ToJson(other);
        public bool SameDisplay(MenuSettings other) => Display == other.Display && Width == other.Width && Height == other.Height;

        public void Normalize()
        {
            Master = Mathf.Clamp(Master, 0, 100); Ambience = Mathf.Clamp(Ambience, 0, 100); Effects = Mathf.Clamp(Effects, 0, 100);
            Display = Mathf.Clamp(Display, 0, 2); Brightness = Mathf.Clamp(Brightness, 70, 130);
            if (!((Width == 1280 && Height == 720) || (Width == 1920 && Height == 1080) ||
                  (Width == 2560 && Height == 1440) || (Width == 3840 && Height == 2160))) { Width = 1920; Height = 1080; }
            if (FrameLimit != 0 && FrameLimit != 30 && FrameLimit != 60 && FrameLimit != 120 && FrameLimit != 144) FrameLimit = 60;
            Sensitivity = float.IsNaN(Sensitivity) || float.IsInfinity(Sensitivity) ? 1 : Mathf.Clamp(Sensitivity, .2f, 3);
        }

        public static MenuSettings Load(string path, out string error)
        {
            error = null;
            if (!File.Exists(path)) return new MenuSettings();
            try
            {
                var value = JsonUtility.FromJson<MenuSettings>(File.ReadAllText(path));
                if (value == null || value.Version != 1) throw new InvalidDataException("Unsupported settings version.");
                value.Normalize(); return value;
            }
            catch (Exception exception)
            {
                error = "Не удалось прочитать настройки. Загружены значения по умолчанию.";
                Debug.LogWarning(error + " " + exception.Message);
                return new MenuSettings();
            }
        }

        public bool Save(string path, out string error)
        {
            Normalize();
            try { MenuFiles.WriteAtomic(path, JsonUtility.ToJson(this, true)); error = null; return true; }
            catch (Exception exception) { error = "Не удалось сохранить настройки: " + exception.Message; return false; }
        }
    }

    internal static class MenuFiles
    {
        internal static void WriteAtomic(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, content);
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
    }
}
