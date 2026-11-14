Shader "Lighthouse/BlockoutBeam"
{
    Properties { _Color ("Beam", Color) = (1,0.69,0.27,0.018) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; };
            struct v2f { float4 vertex:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float along:TEXCOORD2; };
            fixed4 _Color;
            v2f vert(appdata v)
            {
                v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.world=mul(unity_ObjectToWorld,v.vertex).xyz; o.normal=UnityObjectToWorldNormal(v.normal);
                float3 origin=mul(unity_ObjectToWorld,float4(0,0,0,1)).xyz;
                float3 axis=normalize(mul((float3x3)unity_ObjectToWorld,float3(0,0,1)));
                o.along=dot(o.world-origin,axis)/34+.5; return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float face=pow(saturate(abs(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world)))),.65);
                float fade=pow(1-saturate(i.along),1.5);
                return fixed4(_Color.rgb,_Color.a*face*fade);
            }
            ENDCG
        }
    }
}
