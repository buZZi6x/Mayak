Shader "Lighthouse/HDRP Spectral Wake"
{
 SubShader
 {
  Tags{"RenderPipeline"="HDRenderPipeline" "Queue"="Transparent+14"}
  Pass
  {
   Tags{"LightMode"="ForwardOnly"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma target 4.5
   #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
   #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
   struct A{float3 p:POSITION;float2 uv:TEXCOORD0;float4 c:COLOR;};struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;float4 c:COLOR;};
   V Vert(A i){V o;o.p=TransformWorldToHClip(TransformObjectToWorld(i.p));o.uv=i.uv;o.c=i.c;return o;}
   float4 Frag(V i):SV_Target{float a=pow(saturate(1-abs(i.uv.y*2-1)),2);a*=.35+.65*pow(.5+.5*sin(i.uv.x*40-_TimeParameters.x+sin(i.uv.y*17)),2);return float4(i.c.rgb,i.c.a*a);}
   ENDHLSL
  }
 }
}
