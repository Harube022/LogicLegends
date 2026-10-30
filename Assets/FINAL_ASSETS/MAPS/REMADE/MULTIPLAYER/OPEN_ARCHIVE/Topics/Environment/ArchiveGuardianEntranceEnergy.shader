Shader "LogicLegends/OpenArchive/GuardianEntranceEnergy"
{
    Properties
    {
        [HDR]_Tint("Species tint",Color)=(1,.1,.05,1)
        _Glow("Glow",Range(0,3))=1.7
        [HideInInspector]_SrcBlend("Source blend",Float)=5
        [HideInInspector]_DstBlend("Destination blend",Float)=1
        [HideInInspector]_ZWrite("Depth write",Float)=0
    }
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
        Pass
        {
            Name "EntranceEnergy" Tags{"LightMode"="UniversalForward"}
            Blend [_SrcBlend] [_DstBlend] ZWrite [_ZWrite] ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;half _Glow;
            CBUFFER_END
            struct A{float4 position:POSITION;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V{float4 position:SV_POSITION;half4 color:COLOR;half fog:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
            V Vert(A input)
            {
                V o=(V)0;UNITY_SETUP_INSTANCE_ID(input);UNITY_TRANSFER_INSTANCE_ID(input,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.position=TransformObjectToHClip(input.position.xyz);o.color=input.color;o.fog=ComputeFogFactor(o.position.z);return o;
            }
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                // Black fog for additive energy; opaque dust uses the same pooled
                // unlit shader with One/Zero blending and modest earth-tone color.
                return half4(MixFogColor(i.color.rgb*_Tint.rgb*_Glow,half3(0,0,0),i.fog),i.color.a*_Tint.a);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
