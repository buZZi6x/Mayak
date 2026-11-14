Shader "Lighthouse/HDRP Observations"
{
 Properties { _Color("Color",Color)=(0.1,1,1,1) _Accent("Accent",Color)=(0.7,0.2,1,1) _Kind("Kind",Float)=0 _Phase("Phase",Float)=0 _Opacity("Opacity",Float)=1 }
 SubShader
 {
  Tags { "RenderPipeline"="HDRenderPipeline" "Queue"="Transparent+15" "RenderType"="Transparent" }
  Pass
  {
   Tags { "LightMode"="ForwardOnly" }
   Blend SrcAlpha OneMinusSrcAlpha ZWrite Off ZTest LEqual Cull Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma target 4.5
   #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
   #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
   struct A{float3 positionOS:POSITION;float2 uv:TEXCOORD0;float2 part:TEXCOORD1;};
   struct V{float4 positionCS:SV_POSITION;float2 q:TEXCOORD0;float part:TEXCOORD1;float distance:TEXCOORD2;float facing:TEXCOORD3;float3 world:TEXCOORD4;};
   CBUFFER_START(UnityPerMaterial)
   float4 _Color,_Accent;float _Kind,_Phase,_Opacity,_Visibility,_Daylight;
   CBUFFER_END
   V Vert(A i){V o;float3 world=TransformObjectToWorld(i.positionOS);o.world=world;o.positionCS=TransformWorldToHClip(world);o.q=i.uv;o.part=i.part.x;o.distance=length(world);o.facing=abs(dot(normalize(TransformObjectToWorldDir(float3(cos(i.uv.x*6.283),0,sin(i.uv.x*6.283)))),normalize(-world)));return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
   float fbm(float2 p){return noise(p)*.57+noise(p*2.03)*.28+noise(p*4.13)*.15;}
   float4 Frag(V i):SV_Target
   {
    float2 q=i.q;float a=1,t=_Phase;float3 c=lerp(_Color.rgb,_Accent.rgb,q.y);
    float w=pow(max(0,sin(q.y*PI)),.55);
    if(_Kind<.5)
    {
     float n=fbm(float2(q.x*46+t*.07,q.y*2+t*.035));
     float filaments=.45+.55*noise(float2(q.x*170+t*.08,q.y*.8));
     a=pow(saturate(1-q.y),2.4)*smoothstep(0,.10+noise(float2(q.x*24,t*.025))*.18,q.y)*(.32+n*.8)*pow(saturate(sin(q.x*PI)),1.5)*filaments;
     c=lerp(_Color.rgb,_Accent.rgb,smoothstep(.1,.7,q.y))*1.55;
    }
    else if(_Kind<1.5)
    {
     float life=frac((t-i.part*6.7)/22)*22;
     a=pow(saturate(1-q.x),1.3)*pow(w,2)*step(life,2.8)*smoothstep(0,.18,life)*(1-smoothstep(2.45,2.8,life));
     c=lerp(_Color.rgb,_Accent.rgb,exp(-q.x*10))*(3+exp(-q.x*45)*22);
    }
    else if(_Kind<2.5)
    {
     float n=fbm(float2(q.x*18+q.y*11-t*.4,q.y*28+t*.65));
     float spray=i.part>.5&&i.part<1.5?1:0;
     a=smoothstep(.16,.70,n)*(.7+spray*.1)*smoothstep(0,.025,q.y)*(1-smoothstep(.7,1,q.y))*pow(saturate(i.facing),.65);
     if(i.part>1.5)a*=.22;
     c=lerp(_Color.rgb,_Accent.rgb,n*.55+spray*.3)*lerp(.75,1.25,_Daylight);
    }
    else if(_Kind<3.5)
    {
     float n=fbm(float2(q.x*110-t*.6,q.y*5+sin(q.x*17+t)));
     a=pow(w,2)*smoothstep(.24,.64,n)*pow(saturate(sin(q.x*PI)),.6)*.9;
     c=lerp(_Color.rgb,_Accent.rgb,n)*lerp(1.2,1.7,_Daylight);
    }
    else if(_Kind<4.5)
    {
     float ribs=pow(saturate(.5+.5*cos(q.x*PI*16)),24);
     float pulse=.65+.35*sin(t*1.2+(i.part<3?i.part:floor((i.part-3)/8))*2.1-q.y*2);
     a=i.part<3?.10+.38*ribs+.30*pow(saturate(q.y),10):pow(w,1.5)*.48;
     c*=pulse*(i.part<3?1.5:1.1);
    }
    else if(_Kind<5.5)
    {
     float n=noise(float2(q.x*7+t*.3,q.y*17-t*.7));
     a=i.part<3?.55+.35*n:i.part<6?pow(w,3)*smoothstep(.4,.8,n)*.28:pow(saturate(sin(q.x*PI)),2)*pow(saturate(1-q.y),1.3)*n*.35;
     c=lerp(_Accent.rgb,_Color.rgb,i.part<3?.9:.55)*(i.part<3?3.8:1.4);
    }
    else if(_Kind<6.5)
    {
     float edge=q.y,body=pow(saturate(1-edge*edge),3);
     float veins=pow(saturate(.5+.5*sin(q.x*36+edge*16)),30);
     float wing=i.part<2||i.part>=10?1:0;
     a=wing>.5?.85+.08*body:i.part<8?.80:.90;
     float mottling=fbm(float2(q.x*25,q.y*14));
     c=lerp(_Color.rgb,_Accent.rgb,pow(saturate(edge),9)*.35+body*.23+mottling*.22)*(wing>.5?1.15:1.05);
     float3 normal=normalize(cross(ddx(i.world),ddy(i.world)));
     c*=.4+.6*abs(dot(normal,normalize(float3(.4,1,.25))));
     if(i.part>=2&&i.part<8)c=lerp(_Color.rgb,_Accent.rgb,.8)*1.25;
    }
    else
    {
     float n=fbm(float2(q.x*130-t*.24,q.y*9+sin(q.x*31+t*.5)));
     a=pow(w,3)*smoothstep(.42,.8,n)*pow(saturate(sin(q.x*PI)),.7)*.68;
     c*=1.9+pow(saturate(n-.65)*3,2)*3;
    }
    float transmission=exp(-i.distance/max(90,_Visibility)*.65);
    return float4(c,saturate(a*_Opacity*transmission));
   }
   ENDHLSL
  }
 }
}
