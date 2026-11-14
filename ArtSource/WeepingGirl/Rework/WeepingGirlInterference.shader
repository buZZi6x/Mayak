Shader "Lighthouse/HDRP Weeping Girl Interference"
{
    Properties { _Strength("Signal damage", Range(0,1)) = 0 _Blackout("Blackout",Range(0,1)) = 0 _SignalTime("Clock",Float) = 0 }
    HLSLINCLUDE
    #pragma vertex Vert
    #pragma target 4.5
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"
    float _Strength, _Blackout, _SignalTime;

    float SignalHash(float2 p)
    {
        float3 q = frac(float3(p.xyx) * .1031);
        q += dot(q, q.yzx + 33.33);
        return frac((q.x + q.y) * q.z);
    }

    float SignalNoise(float2 p)
    {
        float2 cell = floor(p), f = frac(p);
        f = f * f * (3 - 2 * f);
        return lerp(lerp(SignalHash(cell), SignalHash(cell + float2(1, 0)), f.x),
                    lerp(SignalHash(cell + float2(0, 1)), SignalHash(cell + 1), f.x), f.y);
    }

    float4 Interference(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = input.positionCS.xy * _ScreenSize.zw;
        float strength = saturate(_Strength);
        float tick = floor(_SignalTime * 30);
        float aspect = _ScreenSize.x / _ScreenSize.y;
        float2 raster = float2(300 * aspect, 300);
        float scanRow = floor(uv.y * 240);
        float lineNoise = SignalHash(float2(scanRow, tick * 3.1));
        float tearing = step(lerp(.997, .91, strength), lineNoise);
        float bandPosition = frac(_SignalTime * .43 + SignalHash(float2(floor(_SignalTime * 3), 7)));
        float band = 1 - smoothstep(.008, .048, abs(uv.y - bandPosition));
        float drop = step(.66, SignalHash(float2(floor(_SignalTime * 9), 73))) * band;
        float xShift = (lineNoise - .5) * (tearing * .065 + drop * .025) * strength;
        float2 damagedUV = saturate(uv + float2(xShift, 0));
        float3 scene = CustomPassSampleCustomColor(damagedUV).rgb;
        float luminance = dot(scene, float3(.2126, .7152, .0722));
        scene = lerp(scene, luminance.xxx, strength * .86);

        // Coarse monochrome grain and horizontal dropout, matching an analogue signal.
        // Applied after post-processing so TAA/exposure cannot smooth or amplify it away.
        float grain = SignalNoise(uv * raster + float2(tick * 17.3, tick * 29.7));
        float fine = SignalHash(floor(uv * raster * 1.9) + float2(tick * 43.7, 81));
        float snow = smoothstep(.26, .73, grain * .84 + fine * .16) * .82;
        float edge = smoothstep(.16, .66, length((uv - .5) * float2(1.18, 1)));
        float density = saturate(strength * (.2 + .31 * edge) + strength * strength * .60);
        float scan = .94 + .06 * sin(uv.y * raster.y * 3.14159 + tick);
        float darkSnow = step(grain, .20 + strength * .10);
        scene *= scan * (1 - drop * strength * .70) * (1 - darkSnow * strength * .55);
        float3 color = lerp(scene, snow.xxx, density);
        color *= 1 - edge * strength * .32;
        color = lerp(color, (snow * .65).xxx, tearing * strength * .46);
        return float4(max(color, 0) * (1 - saturate(_Blackout)), 1);
    }

    float4 Copy(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        return float4(CustomPassSampleCameraColor(input.positionCS.xy * _ScreenSize.zw, 0), 1);
    }
    ENDHLSL
    SubShader
    {
        Tags {"RenderPipeline"="HDRenderPipeline"}
        Pass
        {
            Name "Copy" ZWrite Off ZTest Always Cull Off Blend One Zero
            HLSLPROGRAM
            #pragma fragment Copy
            ENDHLSL
        }
        Pass
        {
            Name "Interference" ZWrite Off ZTest Always Cull Off Blend One Zero
            HLSLPROGRAM
            #pragma fragment Interference
            ENDHLSL
        }
    }
    Fallback Off
}
