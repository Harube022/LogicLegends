Shader "LogicLegends/OpenArchive/HeroSummon"
{
    Properties
    {
        [MainTexture]_BaseMap("Hero texture",2D)="white"{}
        [MainColor]_BaseColor("Hero color",Color)=(1,1,1,1)
        _RevealY("World reveal height",Float)=10000
        [HDR]_GlowColor("Summoning edge",Color)=(.3,1.1,1.25,1)
        _EdgeWidth("Edge width in metres",Range(.01,.5))=.14
        _NoiseScale("Edge grain scale",Range(.1,20))=4
    }
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            Name "HeroSummon" Tags{"LightMode"="UniversalForward"}
            Cull Back ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor,_GlowColor;
                float _RevealY,_EdgeWidth,_NoiseScale;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;
                float2 uv:TEXCOORD2;half fog:TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings o=(Varyings)0;UNITY_SETUP_INSTANCE_ID(input);UNITY_TRANSFER_INSTANCE_ID(input,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS=TransformObjectToWorld(input.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(input.normalOS);o.uv=TRANSFORM_TEX(input.uv,_BaseMap);
                o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // Stable world-space grain roughens a single rising edge. There is
                // no time-driven flicker or transparent full-body overdraw.
                float grain=sin(dot(input.positionWS.xz,float2(7.13,11.71))*_NoiseScale+input.positionWS.y*3.7);
                float distance=_RevealY-input.positionWS.y+grain*_EdgeWidth*.16;
                clip(distance);
                half3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb*_BaseColor.rgb;
                half3 normal=normalize(input.normalWS);Light light=GetMainLight();
                half diffuse=saturate(dot(normal,light.direction));
                half3 ambient=max(half3(.22,.22,.22),SampleSH(normal));
                half3 color=albedo*(ambient+light.color*diffuse*.72h);
                half edge=1-smoothstep(0,max(.001,_EdgeWidth),distance);
                color+=_GlowColor.rgb*edge;
                return half4(MixFog(color,input.fog),1);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
