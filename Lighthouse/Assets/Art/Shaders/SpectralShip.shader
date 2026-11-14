Shader "Lighthouse/HDRP Spectral Vessel"
{
 Properties{_Color("Mist",Color)=(.12,.58,.62,1) _Opacity("Opacity",Float)=0 _Sail("Sail",Float)=0}
 SubShader
 {
  Tags{"RenderPipeline"="HDRenderPipeline" "Queue"="Transparent+12" "RenderType"="Transparent"}
  Pass
  {
   Tags{"LightMode"="ForwardOnly"} Blend SrcAlpha OneMinusSrcAlpha ZWrite On Cull Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma target 4.5
   #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
   #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
   struct A{float3 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;};
   struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;float2 uv:TEXCOORD2;};
   CBUFFER_START(UnityPerMaterial) float4 _Color;float _Opacity,_Sail; CBUFFER_END
   V Vert(A i){V o;o.w=TransformObjectToWorld(i.p);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(i.n);o.uv=i.uv;return o;}
   float hash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
   float noise(float3 p){float3 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(lerp(hash(a),hash(a+float3(1,0,0)),f.x),lerp(hash(a+float3(0,1,0)),hash(a+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(a+float3(0,0,1)),hash(a+float3(1,0,1)),f.x),lerp(hash(a+float3(0,1,1)),hash(a+1),f.x),f.y),f.z);}
   float4 Frag(V i):SV_Target
   {
    float3 view=normalize(GetWorldSpaceNormalizeViewDir(i.w));float rim=pow(1-abs(dot(normalize(i.n),view)),2);
    float3 absolute=GetAbsolutePositionWS(i.w);
    float mist=noise(absolute*.11+float3(_Time.y*.035,0,0))*.7+noise(absolute*.31-float3(0,_Time.y*.06,0))*.3;
    float fading=smoothstep(.08,.33,mist);
    float a=lerp(.72,.56,_Sail)*fading*_Opacity;
    clip(a-.035);
    float shade=.35+.65*abs(dot(normalize(i.n),normalize(float3(-.3,1,.6))));
    float3 tint=lerp(float3(.18,.30,.32),float3(.49,.62,.60),_Sail);
    return float4(tint*(shade+rim*.45)*(0.85+mist*.2),a);
   }
   ENDHLSL
  }
 }
}
