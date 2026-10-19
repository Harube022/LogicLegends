Shader "LogicLegends/OpenArchive/TurquoiseWater"
{
    Properties
    {
        _FlowMap("Flow and foam detail", 2D) = "gray" {}
        _DeepColor("Deep turquoise", Color) = (0.025,0.49,0.64,1)
        _ShoreColor("Shallow turquoise", Color) = (0.18,0.72,0.70,1)
        _RippleColor("Ripple tint", Color) = (0.48,0.89,0.86,1)
        _WaveHeight("Wave height (m)", Range(0,0.06)) = 0.018
        _FoamColor("Shore foam", Color) = (0.68,0.94,0.87,1)
        _FoamStrength("Shoreline foam", Range(0,1)) = .4
        _FlowSpeed("Flow speed", Range(0,2)) = .45
        _VerticalFlow("Waterfall flow", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "OpaqueWater"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_FlowMap); SAMPLER(sampler_FlowMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor, _ShoreColor, _RippleColor, _FoamColor;
                float _WaveHeight, _FoamStrength, _FlowSpeed, _VerticalFlow;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; half depth:TEXCOORD1; half fog:TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A i)
            {
                V o = (V)0;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i,o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p = TransformObjectToWorld(i.positionOS.xyz);
                // Centimetre-scale movement, no scene/depth textures or reflections.
                p.y += sin(p.x*0.65 + p.z*0.43 + _Time.y*0.75) * _WaveHeight * i.color.r;
                o.positionCS = TransformWorldToHClip(p);
                o.world = p;
                o.depth = i.color.r;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float time = _Time.y*_FlowSpeed;
                float2 p = i.world.xz;
                half4 flow = SAMPLE_TEXTURE2D(_FlowMap,sampler_FlowMap,p*.13+float2(time*.035,-time*.06));
                half4 crossFlow = SAMPLE_TEXTURE2D(_FlowMap,sampler_FlowMap,p.yx*.21+float2(time*.055,time*.025));
                float phase = p.x*1.6+p.y*2.1+time*1.7+(flow.b-.5)*8;
                float ripple = sin(phase+sin(p.x*.73-p.y*.61-time*.8)*1.2);
                half glint = smoothstep(.91,1.0,ripple)*smoothstep(.35,.68,crossFlow.g)*.24;
                half shore = 1-smoothstep(.015,.19,i.depth);
                half3 col = lerp(_ShoreColor.rgb,_DeepColor.rgb,saturate(i.depth));
                col *= .96+flow.b*.08;
                half grazing=pow(1-saturate(normalize(GetWorldSpaceViewDir(i.world)).y),3);
                col=lerp(col,_RippleColor.rgb,grazing*.11+glint);
                half foam = shore*smoothstep(.42,.73,flow.g+crossFlow.g*.15)*_FoamStrength;
                float streak=sin(i.world.y*2.3+time*7+(flow.b-.5)*3);
                half falls=smoothstep(.2,.95,streak)*.35*_VerticalFlow;
                col=lerp(col,_FoamColor.rgb,saturate(foam+falls));
                return half4(MixFog(col,i.fog),1);
            }
            ENDHLSL
        }
    }
}
