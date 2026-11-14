using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace LighthouseGame
{
    [DefaultExecutionOrder(220)]
    public class LighthouseBeacon : MonoBehaviour
    {
        public BlockoutDirector Director;
        // Retain the serialized reference for older scenes; scattering now comes from HDRP fog.
        public Renderer Shaft;
        public Light BroadBeam;

        [SerializeField, Min(1)] private float _rotationPeriodSeconds = 90;
        private HDAdditionalLightData _beamData;
        private HDAdditionalLightData _spillData;

        public float RotationPeriodSeconds => _rotationPeriodSeconds;

        private void Awake()
        {
            ConfigureOptics();
        }

        public void ConfigureOptics()
        {
            if (!Director || !Director.BeamLight) return;
            _beamData = ConfigureLight(Director.BeamLight, 1000, 52, 6, 2048);
            if (BroadBeam) _spillData = ConfigureLight(BroadBeam, 750, 88, 14, 1024);

            var rotation = Director.BeamLight.GetComponentInParent<BlockoutMotion>();
            if (rotation && rotation.Type == BlockoutMotion.MotionType.Spin)
                rotation.Strength = 360 / (BlockoutMotion.SpinDegreesPerSecond * Mathf.Max(1, _rotationPeriodSeconds));

            if (Shaft) Shaft.enabled = false;
        }

        private static HDAdditionalLightData ConfigureLight(Light light, float range, float outerAngle, float innerAngle, int shadowResolution)
        {
            light.type = LightType.Spot;
            light.range = range;
            light.spotAngle = outerAngle;
            light.innerSpotAngle = innerAngle;
            light.lightUnit = UnityEngine.Rendering.LightUnit.Candela;
            light.color = Color.white;
            light.useColorTemperature = true;
            light.colorTemperature = 4300;
            light.shapeRadius = .35f;
            light.shadows = LightShadows.Soft;
            var data = light.GetComponent<HDAdditionalLightData>();
            if (!data) data = light.gameObject.AddComponent<HDAdditionalLightData>();
            // Start outside the lantern/gallery: their near housing must not stamp magnified
            // rectangular shadows across the sea. Distant vessels still cast real shadows.
            data.shadowNearPlane = 7;
            data.applyRangeAttenuation = true;
            data.affectDiffuse = true;
            data.affectSpecular = true;
            data.affectsVolumetric = true;
            data.fadeDistance = 1600;
            data.volumetricFadeDistance = 1200;
            data.shadowFadeDistance = range;
            data.volumetricShadowDimmer = 1;
            data.softnessScale = 1.5f;
            data.SetShadowResolutionOverride(true);
            data.SetShadowResolution(shadowResolution);
            return data;
        }

        private void LateUpdate()
        {
            var environment = CinematicEnvironment.Active;
            if (!environment || !Director || !Director.BeamLight) return;

            bool isLit = Director.LampLight && Director.LampLight.enabled;
            float daylight = Mathf.Clamp01(environment.Sun.intensity / 85000);
            var beam = Director.BeamLight;
            beam.enabled = isLit;
            beam.intensity = Mathf.Lerp(650000, 12000000, daylight);
            if (BroadBeam)
            {
                BroadBeam.enabled = isLit;
                BroadBeam.intensity = beam.intensity * .30f;
            }

            // The atmosphere supplies weather-dependent density, extinction and occlusion.
            // Keep airborne scattering restrained; the main evidence is light on actual surfaces.
            if (_beamData) _beamData.volumetricDimmer = .22f;
            if (_spillData) _spillData.volumetricDimmer = .12f;
            if (Shaft) Shaft.enabled = false;
        }
    }
}
