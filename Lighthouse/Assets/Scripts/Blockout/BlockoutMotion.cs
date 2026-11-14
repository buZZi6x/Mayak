using UnityEngine;

namespace LighthouseGame
{
    public class BlockoutMotion : MonoBehaviour
    {
        public const float SpinDegreesPerSecond = 28;
        public enum MotionType { Spin, Flame, Pump, Dial, Blanket, Telescope, Flag }
        public MotionType Type;
        public float Strength = 1;
        public float BaseLightIntensity = 2.3f;
        Vector3 position, scale, worldPosition;
        Quaternion rotation;
        Mesh mesh;
        Vector3[] vertices, animated;
        float pulseUntil, phase;
        public void Pulse(float seconds = 1) { pulseUntil = Time.time + seconds; }
        void Awake()
        {
            position = transform.localPosition; worldPosition = transform.position; rotation = transform.localRotation; scale = transform.localScale;
            phase = Mathf.Abs(name.GetHashCode() % 37) * .17f;
            if (Type == MotionType.Flag && TryGetComponent<MeshFilter>(out var f))
            { mesh = f.mesh; vertices = mesh.vertices; animated = new Vector3[vertices.Length]; }
        }
        void Update()
        {
            float t = Time.time, pulse = t < pulseUntil ? 1 : 0;
            switch (Type)
            {
                case MotionType.Spin: transform.Rotate(Vector3.up, SpinDegreesPerSecond * Strength * Time.deltaTime, Space.World); break;
                case MotionType.Flame:
                    transform.localScale = Vector3.Scale(scale, new Vector3(1 + .06f * Mathf.Sin(t * 7), 1 + .11f * Mathf.Sin(t * 9 + phase), 1 + .05f * Mathf.Sin(t * 11)));
                    if (TryGetComponent<Light>(out var l)) l.intensity = BaseLightIntensity * (1 + Mathf.Sin(t * 7) * .08f);
                    break;
                case MotionType.Pump: transform.localRotation = rotation * Quaternion.Euler(Mathf.Sin(t * (pulse > 0 ? 8 : 1.2f)) * (pulse > 0 ? 36 : 2), 0, 0); break;
                case MotionType.Dial: transform.localRotation = rotation * Quaternion.Euler(0, 0, Mathf.Sin(t * (pulse > 0 ? 12 : 2)) * (pulse > 0 ? 60 : 3)); break;
                case MotionType.Blanket: transform.position = worldPosition + Vector3.up * (Mathf.Sin(t * 1.4f + phase) * .012f + pulse * Mathf.Sin(t * 5) * .07f); break;
                case MotionType.Telescope: transform.localRotation = rotation * Quaternion.Euler(0, Mathf.Sin(t * .5f) * (pulse > 0 ? 12 : 2.5f), 0); break;
                case MotionType.Flag:
                    if (mesh)
                    {
                        for (int i = 0; i < vertices.Length; i++)
                        { animated[i] = vertices[i]; animated[i].z += Mathf.Sin(t * 4 + vertices[i].x * 5 + phase) * .16f * vertices[i].x * Strength; }
                        mesh.vertices = animated; mesh.RecalculateNormals();
                    }
                    break;
            }
        }
        void OnDestroy() { if (mesh) Destroy(mesh); }
    }
}
