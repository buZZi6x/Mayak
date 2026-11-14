using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace LighthouseGame
{
    public sealed class MenuBackdrop : MonoBehaviour
    {
        private Material _material;
        private CustomPassVolume _volume;
        private float _nextGlitch, _glitchStart = -10, _duration = 1;
        private bool _glitches = true;
        public float Brightness { get; set; } = 1;
        public int Page { get; set; } = -1;
        public bool Glitches { get => _glitches; set { _glitches = value; if (!value) _glitchStart = -10; } }
        public float CurrentGlitch { get; private set; }

        public void Initialize(Camera camera)
        {
            _material = new Material(Resources.Load<Shader>("KeeperMenu/MenuSea"));
            _volume = gameObject.AddComponent<CustomPassVolume>();
            _volume.isGlobal = true; _volume.targetCamera = camera;
            _volume.injectionPoint = CustomPassInjectionPoint.AfterPostProcess;
            _volume.priority = -100;
            _volume.customPasses.Add(new FullScreenCustomPass { name = "Menu sea copy", fullscreenPassMaterial = _material,
                materialPassName = "Copy", targetColorBuffer = CustomPass.TargetBuffer.Custom, targetDepthBuffer = CustomPass.TargetBuffer.None });
            _volume.customPasses.Add(new FullScreenCustomPass { name = "Menu sea and red interference", fullscreenPassMaterial = _material,
                materialPassName = "Sea", targetDepthBuffer = CustomPass.TargetBuffer.None });
            _nextGlitch = Time.unscaledTime + 8;
        }

        public void PreviewGlitch() { if (Glitches) { _glitchStart = Time.unscaledTime; _duration = 1.1f; } }

        private void LateUpdate()
        {
            if (!_material) return;
            float clock = Time.unscaledTime;
            if (Page >= 0 && Glitches && clock > _nextGlitch)
            {
                _glitchStart = clock; _duration = Random.Range(.85f, 1.25f); _nextGlitch = clock + Random.Range(13f, 25f);
            }
            float phase = (clock - _glitchStart) / _duration;
            CurrentGlitch = Page >= 0 && Glitches && phase >= 0 && phase < 1 ? Mathf.Min(1, phase*10, (1-phase)*4) : 0;
            _material.SetFloat("_Glitch", CurrentGlitch); _material.SetFloat("_Clock", clock);
            _material.SetFloat("_Brightness", Brightness); _material.SetFloat("_Page", Page);
            _volume.enabled = Page >= 0 || !Mathf.Approximately(Brightness, 1);
        }

        private void OnDestroy() { if (_material) Destroy(_material); if (_volume) Destroy(_volume); }
    }
}
