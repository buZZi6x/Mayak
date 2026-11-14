Shader "Lighthouse/BlockoutWater"
{
    Properties
    {
        _Color ("Deep water", Color) = (0.025,0.16,0.23,1)
        _Crest ("Wave crests", Color) = (0.14,0.48,0.53,1)
        _WaveStrength ("Wave height", Float) = 0.28
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 world : TEXCOORD0; UNITY_FOG_COORDS(1) };
            fixed4 _Color, _Crest;
            float _WaveStrength;
            v2f vert(appdata v)
            {
                v2f o; float3 p = mul(unity_ObjectToWorld, v.vertex).xyz;
                p.y += (sin(p.x*.25 + _Time.y*.9) + sin(p.z*.31 - _Time.y*1.2)) * _WaveStrength * .5;
                o.world = p; o.pos = mul(UNITY_MATRIX_VP, float4(p,1)); UNITY_TRANSFER_FOG(o,o.pos); return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float wave = sin(i.world.x*.23 + _Time.y*.9) * sin(i.world.z*.34 - _Time.y*1.2);
                float glint = pow(saturate(wave), 12) * .65;
                float radius = length(i.world.xz);
                float foam = (1-smoothstep(13,18,radius)) * smoothstep(10,13,radius) * (.3+.25*sin(radius*6-_Time.y*2));
                fixed4 col = lerp(_Color,_Crest, .18 + glint + foam);
                UNITY_APPLY_FOG(i.fogCoord,col); return col;
            }
            ENDCG
        }
    }
}
