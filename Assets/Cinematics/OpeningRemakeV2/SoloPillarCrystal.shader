Shader "LogicLegends/Cinematics/SoloPillarCrystal"
{
    Properties
    {
        _Center("Mesh Center", Vector) = (0,0,0,0)
        _HalfSize("Mesh Half Size", Vector) = (1,1,1,0)
        _Restored("Restored", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Center, _HalfSize;
            float _Restored;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 crystalPosition : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.crystalPosition = (input.positionOS.xyz - _Center.xyz) / max(_HalfSize.xyz, .001);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half facet = .28 + .62 * saturate(dot(normal, normalize(float3(-.55, .7, -.4))));
                half rim = pow(1 - saturate(dot(normal, view)), 3);
                half3 dim = half3(.07, .18, .30);
                half3 teal = half3(.12, 1.25, 1.58);
                half3 color = lerp(dim, teal, _Restored) * (facet + .24 * rim);

                float y = input.crystalPosition.y;
                float azimuth = atan2(input.crystalPosition.z, input.crystalPosition.x);
                float zigzag = abs(frac(y * 4.2) - .5) * 1.2;
                float main = abs(sin(azimuth * 2 + zigzag + y * 1.35));
                float branch = abs(sin(azimuth * 2 + zigzag - y * 2.1 + 1.05));
                float scar = min(main, branch + step(.45, abs(y + .15)));
                half glow = (1 - smoothstep(.055, .20, scar)) * (1 - _Restored);
                half split = (1 - smoothstep(.035, .083 + fwidth(scar), scar)) * (1 - _Restored);
                half reach = 1 - smoothstep(.68, .94, abs(y));
                color = lerp(color + half3(.35, .085, .30) * glow * reach,
                             half3(.016, .014, .04), split * reach);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
