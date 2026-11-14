using UnityEngine;
namespace LighthouseGame
{
    public enum CoastalWeather { Clear, Cloudy, Fog, Rain, Storm }
    [CreateAssetMenu(menuName="Lighthouse/Weather preset")]
    public class CoastalWeatherPreset : ScriptableObject
    {
        public CoastalWeather Kind;
        public string DisplayName;
        [Range(1,90)] public float WindKph=18;
        [Range(0,1)] public float CloudDensity=.35f;
        [Range(0,1)] public float CloudShape=.85f;
        [Range(0,1)] public float CloudErosion=.75f;
        public float CloudAltitude=1400, CloudDepth=2200, Visibility=12000;
        [Range(0,1)] public float Rain, Wetness, Foam=.15f;
        [Range(.1f,1)] public float SunDimmer=1;
        [Range(.1f,2)] public float Swell=1;
        public Color FogTint=new Color(.7f,.81f,.88f);
    }
}
