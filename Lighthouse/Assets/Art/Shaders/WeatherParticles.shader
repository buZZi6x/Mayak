Shader "Lighthouse/HDRP Weather Particles"
{
    Properties { _BaseMap("Texture",2D)="white"{} _Tint("Tint",Color)=(.5,.65,.75,.3) }
    SubShader
    {
        Tags { "RenderPipeline"="HDRenderPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags {"LightMode"="ForwardOnly"}
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);float4 _Tint;
            struct Attributes{float3 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            struct Varyings{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            Varyings Vert(Attributes i){Varyings o;o.positionCS=TransformWorldToHClip(TransformObjectToWorld(i.positionOS));o.uv=i.uv;o.color=i.color;return o;}
            float4 Frag(Varyings i):SV_Target{return SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_Tint*i.color;}
            ENDHLSL
        }
    }
}
