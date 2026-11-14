Shader "Lighthouse/HDRP Beacon Volume"
{
 Properties { _Color("Warm scattered light",Color)=(1,.74,.39,1) _Strength("Strength",Float)=.3 _Length("Length",Float)=520 _Slope("Slope",Float)=.1 }
 SubShader
 {
  Tags {"RenderPipeline"="HDRenderPipeline" "Queue"="Transparent+20" "RenderType"="Transparent"}
  Pass
  {
   Tags {"LightMode"="ForwardOnly"} Cull Front Blend One One ZWrite Off ZTest Always
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma target 4.5
   #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
   #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
   #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariablesFunctions.hlsl"
   float4 _Color;float _Strength,_Length,_Slope;
   struct A{float3 p:POSITION;};struct V{float4 p:SV_POSITION;float3 local:TEXCOORD0;};
   V Vert(A a){V o;o.p=TransformWorldToHClip(TransformObjectToWorld(a.p));o.local=a.p;return o;}
   float4 Frag(V i):SV_Target
   {
    float3 eye=TransformWorldToObject(GetCameraRelativePositionWS(_WorldSpaceCameraPos));
    float3 ray=normalize(i.local-eye);float distance=length(i.local-eye);float sum=0;
    // Keep the volume in front of the water when its exit face is behind the water.
    float depth=LoadCameraDepth(uint2(i.p.xy));
    float3 scene=ComputeWorldSpacePosition(i.p.xy*_ScreenSize.zw,depth,UNITY_MATRIX_I_VP);
    float sceneDistance=length(TransformWorldToObject(scene)-eye);
    distance=min(distance,sceneDistance);
    // Integration through the tapered cone gives a soft volume from either side.
    [unroll]for(int k=0;k<32;k++)
    {
     float t=distance*(k+.5)/32;float3 p=eye+ray*t;
     float r=max(.25,p.z*_Slope),radial=length(p.xy)/r;
     float density=exp(-radial*radial*5)*(1-smoothstep(.72,1.08,radial))*saturate(p.z/7)*pow(saturate(1-p.z/_Length),2.3);
     density*=step(0,p.z)*step(p.z,_Length);
     sum+=density*distance/32/(18+p.z*.65);
    }
    return float4(_Color.rgb*min(sum,2.5)*_Strength,0);
   }
   ENDHLSL
  }
 }
}
