Shader "Lighthouse/HDRP Coastal Foam"
{
    Properties
    {
        _AffectFoam("Affects Foam",Float)=1
        _AffectDeformation("Affects Deformation",Float)=1
        _Wake("Wake",Float)=0
        _Encounter("Encounter churn",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="HDRenderPipeline" "ShaderGraphTargetId"="WaterDecalSubTarget" }
        Cull Off ZWrite Off ZTest Always
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
        float _Wake,_Encounter;
        struct Varyings{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
        Varyings Vert(uint id:SV_VertexID){Varyings o;o.positionCS=GetFullScreenTriangleVertexPosition(id);o.uv=GetFullScreenTriangleTexCoord(id);return o;}
        float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
        float Noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);}
        float Ring(float2 uv)
        {
            float2 p=(uv-.5)*64;float r=length(p);float angle=atan2(p.y,p.x);
            float shore=15.8+sin(angle*3)*.7+sin(angle*7)*.45;
            float surge=sin(_TimeParameters.x*.75+angle*.4)*.65;
            return exp(-pow((r-shore-1.2-surge)/2.1,2));
        }
        float Wake(float2 uv)
        {
            float2 p=(uv-.5)*float2(12,25);float spread=1+max(p.y+9,0)*.25;
            float tail=smoothstep(-11,-6,p.y)*(1-smoothstep(5,12,p.y));
            float edge=exp(-pow((abs(p.x)-spread)/.65,2));return tail*(edge+exp(-p.x*p.x*1.4)*.4);
        }
        float4 Foam(Varyings i):SV_Target
        {
            float n=Noise(i.uv*38+float2(_TimeParameters.x*.19,-_TimeParameters.x*.11));
            float mask=lerp(Ring(i.uv),Wake(i.uv),_Wake)*smoothstep(.2,.7,n);
            float2 p=(i.uv-.5)*42;float r=length(p);float angle=atan2(p.y,p.x);float swirl=exp(-pow((r-9-sin(angle*4+_TimeParameters.x*.7)*1.7)/3.5,2));
            mask=lerp(mask,swirl*smoothstep(.28,.7,n)+exp(-r*r/22)*n*.4,_Encounter);
            return float4(mask*.7,mask*.25,0,1);
        }
        float4 Deformation(Varyings i):SV_Target
        {
            float r=length((i.uv-.5)*64);float wave=sin(r*2-_TimeParameters.x*1.6)*Ring(i.uv)*.35;
            return float4(lerp(wave,0,_Wake),0,0,1);
        }
        ENDHLSL
        Pass { Name "Foam" HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Foam
            #pragma target 4.5
            ENDHLSL
        }
        Pass { Name "Deformation" HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Deformation
            #pragma target 4.5
            ENDHLSL
        }
    }
}
