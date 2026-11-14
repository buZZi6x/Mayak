using UnityEngine;
namespace LighthouseGame
{
    public class EnvironmentAudioMeter:MonoBehaviour
    {
        public volatile float Rms,Peak;
        void OnAudioFilterRead(float[] data,int channels)
        {
            double sum=0;float peak=0;
            for(int i=0;i<data.Length;i++){float v=data[i];sum+=v*v;peak=Mathf.Max(peak,Mathf.Abs(v));}
            Rms=(float)System.Math.Sqrt(sum/data.Length);Peak=peak;
        }
    }
}
