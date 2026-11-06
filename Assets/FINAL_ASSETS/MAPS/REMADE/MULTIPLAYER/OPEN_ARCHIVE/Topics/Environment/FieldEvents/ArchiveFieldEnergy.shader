Shader "LogicLegends/OpenArchive/FieldEnergy"
{
    Properties
    {
        [HDR]_Tint("Tint",Color)=(.08,1,.84,1)
        _Glow("Glow",Range(0,4))=1
    }
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
        Pass
        {
            Name "FieldEnergy" Tags{"LightMode"="UniversalForward"}
            // Preserve the warning hue on bright paving as well as at night.
            // Thin ribbons remain translucent without washing into white.
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint; half _Glow;
            CBUFFER_END
            struct A {float4 position:POSITION;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 position:SV_POSITION;half4 color:COLOR;half fog:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
            V Vert(A input)
            {
                V o=(V)0;
                UNITY_SETUP_INSTANCE_ID(input);UNITY_TRANSFER_INSTANCE_ID(input,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.position=TransformObjectToHClip(input.position.xyz);o.color=input.color;
                o.fog=ComputeFogFactor(o.position.z);return o;
            }
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                // Only narrow mesh ribbons and inlays use this pass. No layered
                // smoke billboards, depth texture, noise texture or realtime light.
                return half4(MixFog(i.color.rgb*_Tint.rgb*_Glow,i.fog),i.color.a*_Tint.a);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
