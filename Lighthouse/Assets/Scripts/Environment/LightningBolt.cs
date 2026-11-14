using UnityEngine;
using UnityEngine.Rendering;

namespace LighthouseGame
{
    /// <summary>A pooled, branched strike. The same visible geometry is used for photography.</summary>
    public class LightningBolt : MonoBehaviour
    {
        public Material BoltMaterial;
        [Min(0)] public int PhotoBonus = 100;
        [Range(.08f, .5f)] public float Duration = .18f;
        public float Brightness => IsActive ? strength : 0;
        public int StrikeId { get; private set; }
        public Vector3 GroundPoint { get; private set; }
        public bool IsActive => Time.time < ends && strength > .06f;
        public bool OccupiesEventSlot => Time.time < ends || Time.frameCount <= heldThroughFrame;
        const int Segments = 97, Branches = 5;
        LineRenderer[] lines;
        Vector3[] channel = new Vector3[Segments];
        Vector3[][] forks = new Vector3[Branches][];
        float started, ends, strength;
        int heldThroughFrame = -1;
        void Awake() { EnsurePool(); }
        void EnsurePool()
        {
            if (lines != null) return;
            lines = new LineRenderer[(Branches + 1) * 2];
            for (int i = 0; i < lines.Length; i++)
            {
                var go = new GameObject(i % 2 == 0 ? "Lightning · hot channel" : "Lightning · blue corona");
                go.transform.SetParent(transform, false); go.layer = 2;
                var l = go.AddComponent<LineRenderer>(); lines[i] = l;
                l.sharedMaterial = BoltMaterial; l.useWorldSpace = true; l.alignment = LineAlignment.View;
                l.numCornerVertices = 2; l.numCapVertices = 2; l.textureMode = LineTextureMode.Stretch;
                l.shadowCastingMode = ShadowCastingMode.Off; l.receiveShadows = false; l.enabled = false;
            }
            for (int i = 0; i < Branches; i++) forks[i] = new Vector3[17];
        }
        public void StrikeAt(Vector3 ground, float duration = -1, int seed = 0)
        {
            EnsurePool(); StrikeId++; GroundPoint = ground; started = Time.time;
            ends = started + (duration > 0 ? duration : Duration); heldThroughFrame = -1;
            var rng = new System.Random(seed == 0 ? StrikeId * 7919 + Time.frameCount : seed);
            float Rand(float a, float b) => Mathf.Lerp(a, b, (float)rng.NextDouble());
            Vector3 top = ground + new Vector3(Rand(-65, 65), Rand(380, 530), Rand(-40, 40));
            float phase=Rand(0,6.28f);
            for (int i = 0; i < Segments; i++)
            {
                float t = i / (float)(Segments - 1);
                channel[i] = Vector3.Lerp(top, ground, t) + new Vector3(Mathf.Sin(t*14+phase)*22+Mathf.Sin(t*43+phase)*6+Rand(-3.5f,3.5f), Rand(-1.5f,1.5f), Mathf.Sin(t*17+phase)*15+Rand(-3,3)) * Mathf.Sin(t * Mathf.PI);
            }
            SetPair(0, channel, 1.15f);
            for (int f = 0; f < Branches; f++)
            {
                int from = 18 + f * 13; Vector3 start = channel[from];
                Vector3 end = start + new Vector3(Rand(-115, 115), Rand(-130, -60), Rand(-65, 65));
                for (int i = 0; i < forks[f].Length; i++)
                {
                    float t = i / (float)(forks[f].Length - 1);
                    forks[f][i] = Vector3.Lerp(start, end, t) + new Vector3(Mathf.Sin(t*12+f)*6+Rand(-3, 3), Rand(-3, 3), Rand(-3, 3)) * Mathf.Sin(t * Mathf.PI);
                }
                SetPair(f + 1, forks[f], .48f);
            }
            SetStrength(1);
        }
        void SetPair(int pair, Vector3[] points, float width)
        {
            for (int j = 0; j < 2; j++)
            {
                var line = lines[pair * 2 + j]; line.positionCount = points.Length; line.SetPositions(points);
                line.startWidth = width * (j == 0 ? 1 : 6); line.endWidth = width * (pair == 0 ? .65f : .1f) * (j == 0 ? 1 : 6);
            }
        }
        void SetStrength(float value)
        {
            strength = value;
            for (int i = 0; i < lines.Length; i++)
            {
                var c = i % 2 == 0 ? new Color(1, .98f, 1, value) : new Color(.38f, .56f, 1, value * .24f);
                lines[i].startColor = c; lines[i].endColor = c; lines[i].enabled = value > .01f;
            }
        }
        void Update()
        {
            if (lines == null) return;
            if (Time.frameCount <= heldThroughFrame) { SetStrength(1); return; }
            if (Time.time >= ends) { if (strength > 0) SetStrength(0); return; }
            float t = Time.time - started;
            float pulse = t < .035f ? 1 : t < .065f ? .08f : t < .095f ? .8f : .35f;
            SetStrength(pulse * Mathf.Clamp01((ends - Time.time) / .07f));
        }
        public bool VisibleIn(Camera view)
        {
            if (!view || !IsActive) return false;
            int visible = 0;
            // Closely sample the channel: a partial strike near the horizon is a valid photo.
            for (int i = 0; i < Segments - 1; i++)
                for (int j = 0; j < 3; j++)
                    if (PointVisible(view, Vector3.Lerp(channel[i], channel[i + 1], j / 3f)) && ++visible >= 2) return true;
            foreach (var branch in forks)
                foreach (var point in branch)
                    if (PointVisible(view, point) && ++visible >= 2) return true;
            return false;
        }
        static bool PointVisible(Camera view, Vector3 point)
        {
            Vector3 vp = view.WorldToViewportPoint(point);
            if (vp.z <= view.nearClipPlane || vp.z >= view.farClipPlane || vp.x < .02f || vp.x > .98f || vp.y < .02f || vp.y > .98f) return false;
            Vector3 ray = point - view.transform.position;
            return !Physics.Raycast(view.transform.position, ray.normalized, ray.magnitude, ~(1 << 2), QueryTriggerInteraction.Ignore);
        }
        public void HoldForPhoto() { heldThroughFrame = Time.frameCount; SetStrength(1); }
        public void Clear() { ends = -1; heldThroughFrame = -1; if (lines != null) SetStrength(0); }
    }
}
