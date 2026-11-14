Shader "Lighthouse/HDRP Marine Surface"
{
 Properties { _AffectFoam("Foam",Float)=1 _AffectDeformation("Deformation",Float)=1 _Kind("Kind",Float)=3 _Phase("Clock",Float)=0 _Opacity("Opacity",Float)=1 }
 SubShader
 {
  Tags {"RenderPipeline"="HDRenderPipeline" "ShaderGraphTargetId"="WaterDecalSubTarget"} Cull Off ZWrite Off ZTest Always
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
  float _Kind,_Phase,_Opacity;
  struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
  V Vert(uint id:SV_VertexID){V o;o.positionCS=GetFullScreenTriangleVertexPosition(id);o.uv=GetFullScreenTriangleTexCoord(id);return o;}
  float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
  float Noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);}
  float4 Foam(V i):SV_Target
  {
   float2 p=(i.uv-.5)*90;float r=length(p),a=atan2(p.y,p.x),t=_Phase;
   float n=Noise(p*.7+float2(t*.3,-t*.2));float foam=0;
   if(_Kind<2.5){float ring=exp(-pow((r-16-sin(a*4-t)*2)/8,2));foam=ring*smoothstep(.26,.67,n);}
   else if(_Kind<3.5){float spiral=pow(saturate(sin(a*4+r*.55-t*2)),9);foam=spiral*smoothstep(4,9,r)*(1-smoothstep(24,39,r))*(.3+n*.7);}
   else{float front=pow(saturate(sin(p.y*.7+sin(p.x*.15)*2+t*.8)),12);foam=front*smoothstep(.35,.75,n)*(1-smoothstep(35,45,abs(p.x)));}
   return float4(foam*_Opacity*.6,foam*_Opacity*.18,0,1);
  }
  float4 Deform(V i):SV_Target
  {
   float r=length((i.uv-.5)*90);float h=_Kind>2.5&&_Kind<3.5?-3.2*exp(-r*r/70)+sin(r*.8-_Phase*1.2)*exp(-pow((r-22)/12,2))*.25:0;
   return float4(h*_Opacity,0,0,1);
  }
  ENDHLSL
  Pass {Name "Foam" HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Foam
   #pragma target 4.5
   ENDHLSL
  }
  Pass {Name "Deformation" HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Deform
   #pragma target 4.5
   ENDHLSL
  }
 }
}
