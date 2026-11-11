Shader "LogicLegends/Solo/FoliageCutout"
{
    Properties
    {
        _BaseMap("Original foliage texture",2D)="white"{}
        _Cutoff("Leaf coverage",Range(0,1))=.5
        _Wind("Gentle wind",Range(0,1))=.3
        _WindSpeed("Wind speed",Float)=1
        _TintGradient("Original gradient enabled",Float)=0
        _Gradient("Original gradient",Float)=1.4
        _TopColor("Original top tint",Color)=(.3,.6,.2,1)
        _GroundColor("Original bottom tint",Color)=(.2,.4,.1,1)
    }
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
    CBUFFER_START(UnityPerMaterial)
        float4 _TopColor,_GroundColor;
        float _Cutoff,_Wind,_WindSpeed,_TintGradient,_Gradient;
    CBUFFER_END
    float3 FoliagePosition(float3 p,float2 uv)
    {
        float phase=_Time.y*_WindSpeed+p.x*1.7+p.z*1.3;
        p.x+=sin(phase)*_Wind*.035*saturate(uv.y);return p;
    }
    ENDHLSL
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest"}
        // Discard transparent texels before depth is written. No blended invisible cards.
        Pass
        {
            Name "Foliage" Tags {"LightMode"="UniversalForward"}
            Cull Off ZWrite On Blend One Zero
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            struct A {float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 p:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;float4 fogLight:TEXCOORD3;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
            V Vert(A a)
            {
                V o=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_TRANSFER_INSTANCE_ID(a,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                // Small, bounded displacement; never depend on camera position or projective w.
                a.p.xyz=FoliagePosition(a.p.xyz,a.uv);
                o.world=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(a.n);o.uv=a.uv;
                o.fogLight=float4(ComputeFogFactor(o.p.z),VertexLighting(o.world,o.normal));return o;
            }
            float4 Frag(V i,FRONT_FACE_TYPE facing:FRONT_FACE_SEMANTIC):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 tex=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);clip(tex.a-_Cutoff);
                float3 n=SafeNormalize(i.normal)*IS_FRONT_VFACE(facing,1,-1);
                float3 albedo=lerp(tex.rgb,lerp(_GroundColor.rgb,_TopColor.rgb,saturate(i.uv.y*_Gradient)),saturate(_TintGradient));
                Light key=GetMainLight(TransformWorldToShadowCoord(i.world),i.world,half4(1,1,1,1));
                float3 lighting=SampleSH(n)+key.color*(saturate(dot(n,key.direction))*.8+.2)*key.distanceAttenuation*key.shadowAttenuation;
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                    lighting+=i.fogLight.yzw;
                #endif
                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData=(InputData)0;
                    inputData.positionWS=i.world;inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);
                    #if USE_CLUSTER_LIGHT_LOOP
                    [loop] for(uint lightIndex=0u;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);++lightIndex)
                    {
                        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                        Light l=GetAdditionalLight(lightIndex,i.world,half4(1,1,1,1));
                        lighting+=l.color*saturate(dot(n,l.direction))*l.distanceAttenuation*l.shadowAttenuation;
                    }
                    #endif
                    uint count=GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(count)
                        Light l=GetAdditionalLight(lightIndex,i.world,half4(1,1,1,1));
                        lighting+=l.color*saturate(dot(n,l.direction))*l.distanceAttenuation*l.shadowAttenuation;
                    LIGHT_LOOP_END
                #endif
                return float4(MixFog(albedo*lighting,i.fogLight.x),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags {"LightMode"="DepthOnly"}
            Cull Off ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            struct DA {float4 p:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct DV {float4 p:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
            DV DepthVert(DA a)
            {
                DV o=(DV)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_TRANSFER_INSTANCE_ID(a,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.p=TransformObjectToHClip(FoliagePosition(a.p.xyz,a.uv));o.uv=a.uv;return o;
            }
            float DepthFrag(DV i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);clip(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).a-_Cutoff);return i.p.z;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
