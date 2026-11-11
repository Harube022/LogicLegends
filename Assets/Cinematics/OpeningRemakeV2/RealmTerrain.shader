Shader "LogicLegends/Cinematics/RealmTerrain"
{
    Properties { _BaseColor("Tint",Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; };
            struct V { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; half4 color:COLOR; float4 shadow:TEXCOORD1; };
            V vert(A v) { V o; float3 p=TransformObjectToWorld(v.positionOS.xyz);o.positionCS=TransformWorldToHClip(p);o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.color=v.color*_BaseColor;o.shadow=TransformWorldToShadowCoord(p);return o; }
            half4 frag(V i):SV_Target { half3 n=normalize(i.normalWS);Light l=GetMainLight(i.shadow);half diffuse=saturate(dot(n,l.direction));half3 light=0.34+SampleSH(n)*0.35+l.color*diffuse*0.76*lerp(0.55,1.0,l.shadowAttenuation);return half4(i.color.rgb*light,1); }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            struct A { float4 positionOS:POSITION;float3 normalOS:NORMAL; };
            float4 vertShadow(A v):SV_POSITION { float3 p=TransformObjectToWorld(v.positionOS.xyz);return TransformWorldToHClip(ApplyShadowBias(p,TransformObjectToWorldNormal(v.normalOS),_LightDirection)); }
            half4 fragShadow():SV_Target { return 0; }
            ENDHLSL
        }
    }
}
