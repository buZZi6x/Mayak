Shader "Lighthouse/HDRP Rain Lens"
{
 Properties { _Wetness("Wetness",Range(0,1))=0 _RainTime("Clock",Float)=0 }
 HLSLINCLUDE
 #pragma vertex Vert
 #pragma target 4.5
 #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"
 float _Wetness,_RainTime;
 float HashRain(float n){return frac(sin(n*127.1)*43758.5453);}
 float4 Rain(Varyings i):SV_Target
 {
  UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
  float2 uv=i.positionCS.xy*_ScreenSize.zw;
  float2 aspect=float2(_ScreenSize.x/_ScreenSize.y,1),offset=0;float rim=0,mask=0;
  [unroll]for(int d=0;d<28;d++)
  {
   float seed=d*7.31,cycle=_RainTime*(.055+HashRain(seed+3)*.04)+HashRain(seed);
   float phase=frac(cycle),appear=smoothstep(0,.07,phase)*(1-smoothstep(.8,1,phase));
   float2 center=float2(.025+HashRain(seed+1)*.95,.98-phase*1.12);
   // A clear photographic centre, with small occasional droplets at its edge.
   float central=1-.70*exp(-dot((center-.5)*float2(3.5,4),(center-.5)*float2(3.5,4)));
   float radius=lerp(.007,.022,HashRain(seed+9))*central;
   float2 delta=(uv-center)*aspect;delta.x+=sin(phase*9+seed)*radius*.12;
   delta.y*=.78;float r=length(delta)/radius;
   float lens=(1-smoothstep(.68,1,r))*appear*_Wetness;
   offset+=delta*1.75*lens;rim+=exp(-pow((r-.80)/.075,2))*appear*.12;
   float trail=exp(-pow(delta.x/(radius*.19),2))*smoothstep(0,.015,delta.y)*(1-smoothstep(.025,.14,delta.y));
   offset.x+=sin(delta.y*95+seed)*trail*.0009*_Wetness;
   mask=max(mask,lens);rim+=trail*.025;
  }
  float3 scene=CustomPassSampleCameraColor(saturate(uv+offset),0);
  float3 base=CustomPassSampleCameraColor(uv,0);
  return float4(lerp(base,scene,saturate(mask*1.6))+rim*_Wetness*.055,1);
 }
 ENDHLSL
 SubShader
 {
  Tags {"RenderPipeline"="HDRenderPipeline"}
  Pass { Name "RainLens" ZWrite Off ZTest Always Cull Off Blend One Zero
   HLSLPROGRAM
   #pragma fragment Rain
   ENDHLSL
  }
 }
 Fallback Off
}
