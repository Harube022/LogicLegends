Shader "LogicLegends/OpenArchive/Flashlight"
{
    Properties
    {
        _Color ("Warm light", Color) = (1,0.91,0.7,0.05)
        _Mode ("0 Beam / 1 Surface glow", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+20" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Mode;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half opacity;
                if (_Mode > 0.5h)
                {
                    float2 p = input.uv * 2 - 1;
                    float radiusSquared = dot(p,p);
                    opacity = saturate(1 - radiusSquared);
                    opacity = opacity * opacity * exp2(-radiusSquared * 2);
                }
                else
                {
                    float3 view = SafeNormalize(GetCameraPositionWS() - input.positionWS);
                    half center = pow(saturate(abs(dot(normalize(input.normalWS), view))), 0.8h);
                    half lengthFade = smoothstep(0.0h,0.055h,input.uv.y) * pow(saturate(1 - input.uv.y),1.25h);
                    opacity = center * lengthFade;
                }
                return half4(_Color.rgb, saturate(_Color.a * opacity));
            }
            ENDHLSL
        }
    }
}
