using UnityEngine;

namespace LighthouseGame
{
    // Arc length keeps the spine's spacing constant throughout the breach.
    public static class LeviathanBreach
    {
        public const float WorldScale = 3;
        public const float Radius = 110;
        public const float Speed = 32;
        public const float SpineLength = 156;
        public const int BoneCount = 27;
        public const float Lifetime = 23;
        public const float EntryTime = 1.5f;
        public static float DiveTime => EntryTime + Mathf.PI * Radius / Speed;

        public static float ShakeEnvelope(float age)
        {
            float fade = Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / .7f))
                * Mathf.SmoothStep(0, 1, Mathf.Clamp01((Lifetime - age) / 2.5f));
            float entry = Mathf.Exp(-Mathf.Pow((age - EntryTime) / 1.1f, 2));
            float impact = Mathf.Exp(-Mathf.Pow((age - DiveTime) / 1.5f, 2));
            return fade * (.32f + .68f * Mathf.Max(entry, impact));
        }

        public static Vector3 Point(float age, float behindHead = 0)
        {
            float distance = (age - EntryTime) * Speed - behindHead;
            if (distance < 0) return new Vector3(0, distance - 8, -Radius);
            if (distance > Mathf.PI * Radius) return new Vector3(0, -(distance - Mathf.PI * Radius) - 8, Radius);
            float theta = distance / Radius;
            return new Vector3(0, Mathf.Sin(theta) * Radius - 8, -Mathf.Cos(theta) * Radius);
        }

        public static Quaternion Rotation(float age, float behindHead = 0)
        {
            float theta = Mathf.Clamp(((age - EntryTime) * Speed - behindHead) / Radius, 0, Mathf.PI);
            return Quaternion.LookRotation(new Vector3(0, Mathf.Cos(theta), Mathf.Sin(theta)),
                new Vector3(0, Mathf.Sin(theta), -Mathf.Cos(theta)));
        }
    }
}
