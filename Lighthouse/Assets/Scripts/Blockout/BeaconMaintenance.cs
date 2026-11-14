using UnityEngine;

namespace LighthouseGame
{
    public static class BeaconMaintenance
    {
        public const float WarningSeconds = 20;
        public const float RepairSeconds = 6;

        public static void Tick(ref float lamp, ref float darkness, float seconds, bool repairing)
        {
            if (repairing || seconds <= 0) return;

            // Carry only the part of this frame after the first warning flash into the deadline.
            float healthySeconds = Mathf.Min(Mathf.Max(0, lamp), seconds);
            lamp = Mathf.Max(0, lamp - healthySeconds);
            darkness = Mathf.Min(WarningSeconds, darkness + seconds - healthySeconds);
        }
    }
}
