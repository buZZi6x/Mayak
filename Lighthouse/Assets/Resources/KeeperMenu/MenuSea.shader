Shader "Lighthouse/Menu Sea"
{
    Properties { _Glitch("Red interference",Range(0,1))=0 _Clock("Clock",Float)=0 _Brightness("Brightness",Float)=1 _Page("Page",Float)=-1 }
    HLSLINCLUDE
    #pragma vertex Vert
    #pragma target 4.5
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"
    float _Glitch, _Clock, _Brightness, _Page;
    float Hash(float p) { return frac(sin(p * 127.1) * 43758.5453); }
    float4 Copy(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        return float4(CustomPassSampleCameraColor(input.positionCS.xy * _ScreenSize.zw, 0), 1);
    }
    float4 Sea(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = input.positionCS.xy * _ScreenSize.zw;
        float band = floor(uv.y * 31);
        float cut = step(.77, Hash(band + floor(_Clock * 5))) * _Glitch;
        float2 shifted = uv;
        shifted.x += (Hash(band + floor(_Clock * 3)) - .5) * .08 * cut;
        float split = _Glitch * .005;
        float3 col = CustomPassSampleCustomColor(saturate(shifted)).rgb;
        col.r = CustomPassSampleCustomColor(saturate(shifted + float2(split,0))).r;
        col.b = CustomPassSampleCustomColor(saturate(shifted - float2(split,0))).b;
        // Match the approved browser effect in display space; HDRP's buffer is linear.
        col = LinearToSRGB(max(col, 0));
        float luma = dot(col,float3(.299,.587,.114));
        float3 red = lerp(float3(luma*1.7+.025,luma*.075,luma*.12),float3(.32,.007,.019),cut*.35);
        col = lerp(col,red,_Glitch*.95) * (1-cut*.22) * _Brightness;
        if (_Page >= 0)
        {
            float shade = _Page < .5 ? lerp(.65,.03,smoothstep(0,.76,uv.x)) : .46;
            shade = max(shade,lerp(.14,.70,pow(saturate(uv.y),2.5)));
            if (_Page > 1.5) shade = .91;
            col = lerp(col,float3(.012,.025,.035),shade);
        }
        return float4(SRGBToLinear(max(col,0)),1);
    }
    ENDHLSL
    SubShader
    {
        Tags { "RenderPipeline"="HDRenderPipeline" }
        Pass { Name "Copy" ZWrite Off ZTest Always Cull Off Blend One Zero
            HLSLPROGRAM
            #pragma fragment Copy
            ENDHLSL
        }
        Pass { Name "Sea" ZWrite Off ZTest Always Cull Off Blend One Zero
            HLSLPROGRAM
            #pragma fragment Sea
            ENDHLSL
        }
    }
    Fallback Off
}
