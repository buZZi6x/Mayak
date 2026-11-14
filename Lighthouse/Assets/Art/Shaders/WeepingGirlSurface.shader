Shader "Lighthouse/HDRP Apparition"
{
    Properties
    {
        _Color("Ashen tint", Color)=(.35,.40,.39,1)
        _Rim("Soft edge light",Range(0,.3))=.06
        _Fabric("Fabric grain",Range(0,1))=0
        _BaseMap("Surface and opacity",2D)="white"{}
        _AlphaCutoff("Strand cutout",Range(0,1))=0
        _Visibility("Weather visibility",Float)=1000
    }
    SubShader
    {
        Tags { "RenderPipeline"="HDRenderPipeline" "Queue"="Transparent+8" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="ForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha ZWrite On ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            struct A { float3 p:POSITION; float3 n:NORMAL; float2 uv:TEXCOORD0; };
            struct V { float4 p:SV_POSITION; float3 w:TEXCOORD0; float3 n:TEXCOORD1; float2 uv:TEXCOORD2; float3 local:TEXCOORD3; };
            CBUFFER_START(UnityPerMaterial)
            float4 _Color; float _Rim, _Fabric, _Visibility, _AlphaCutoff;
            CBUFFER_END
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            V Vert(A i)
            {
                V o; o.w=TransformObjectToWorld(i.p);o.p=TransformWorldToHClip(o.w);
                o.n=TransformObjectToWorldNormal(i.n);o.uv=i.uv;o.local=i.p;return o;
            }
            float4 Frag(V i):SV_Target
            {
                float4 surface=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);
                clip(surface.a-_AlphaCutoff);
                float3 n=normalize(i.n), view=normalize(GetWorldSpaceNormalizeViewDir(i.w));
                // An apparition keeps a soft, bounded moon-like fill instead of becoming
                // a blown-out white prop whenever the lighthouse beam crosses the rock.
                float3 right=normalize(cross(float3(0,1,0),view)+float3(.0001,0,0));
                float3 key=normalize(view*.75-right*.55+float3(0,.72,0));
                float shade=.24+.68*saturate(dot(n,key));
                float rim=pow(1-saturate(abs(dot(n,view))),3)*_Rim;
                // The authored cloth has no UV unwrap. Keep subtle fabric variation
                // in object space, with the fine weave filtered at viewing distance.
                float3 fabric=i.local*float3(193,257,173);
                float weave=sin(fabric.x+fabric.z)*sin(fabric.y);
                float filter=1-saturate(max(fwidth(fabric.x+fabric.z),fwidth(fabric.y))*.5);
                float mottling=sin(i.local.x*29+i.local.z*17)*sin(i.local.y*41);
                float grain=1-_Fabric*(.025+.012*mottling+.018*weave*filter);
                float3 color=_Color.rgb*surface.rgb*(shade+rim)*grain;
                float distance=length(GetWorldSpaceViewDir(i.w));
                float fog=exp(-distance/max(30,_Visibility));
                return float4(color*fog,1);
            }
            ENDHLSL
        }
    }
}
