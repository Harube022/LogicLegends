Shader "LogicLegends/FountainWater"
{
    Properties
    {
        _BaseColor ("Water Color", Color) = (0.10, 0.46, 0.60, 1)
        _HighlightColor ("Ripple Highlight", Color) = (0.36, 0.75, 0.82, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.88
        _WaveHeight ("Wave Height", Range(0, 0.15)) = 0.035
        _WaveSpeed ("Wave Speed", Range(0, 5)) = 1.3
        _RippleScale ("Ripple Density", Range(1, 40)) = 18
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }

        Pass
        {
            Name "FountainWater"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _HighlightColor;
                half _Opacity;
                float _WaveHeight;
                float _WaveSpeed;
                float _RippleScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half crest : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float angle = input.uv.x * 6.2831853;
                float phase = input.uv.y * _RippleScale - _Time.y * _WaveSpeed + sin(angle) * 0.7;
                float edgeFade = sin(input.uv.y * 3.14159265);
                input.positionOS.y += sin(phase) * _WaveHeight * edgeFade;

                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.crest = (half)(0.5 + 0.5 * sin(phase));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half ripple = input.crest * 0.22h;
                half3 color = lerp(_BaseColor.rgb, _HighlightColor.rgb, ripple);
                return half4(color, _Opacity);
            }
            ENDHLSL
        }
    }
}
