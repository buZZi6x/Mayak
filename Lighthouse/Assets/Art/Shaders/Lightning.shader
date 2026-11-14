Shader "Lighthouse/HDRP Lightning"
{
    SubShader
    {
        Tags { "RenderPipeline"="HDRenderPipeline" "Queue"="Transparent+20" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="ForwardOnly" }
            Blend SrcAlpha One ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            Varyings Vert(Attributes i) { Varyings o; o.positionCS=TransformWorldToHClip(TransformObjectToWorld(i.positionOS)); o.uv=i.uv; o.color=i.color; return o; }
            float4 Frag(Varyings i):SV_Target
            {
                // An exposure-independent hot channel remains readable in the day and night.
                float edge=pow(saturate(1-abs(i.uv.y*2-1)),1.6);
                return float4(i.color.rgb*18, i.color.a*edge);
            }
            ENDHLSL
        }
    }
}
