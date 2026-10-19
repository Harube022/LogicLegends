Shader "LogicLegends/OpenArchive/WaterfallFoam"
{
    Properties {_FlowMap("Foam breakup",2D)="gray"{} _Color("Foam tint",Color)=(.79,.97,.95,.75) _Speed("Ripple speed",Float)=.8}
    SubShader {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
        Pass {
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_FlowMap);SAMPLER(sampler_FlowMap);
            CBUFFER_START(UnityPerMaterial) half4 _Color;float _Speed; CBUFFER_END
            struct A{float4 p:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;half fog:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID};
            V Vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;o.fog=ComputeFogFactor(o.p.z);return o;}
            half4 Frag(V i):SV_Target{UNITY_SETUP_INSTANCE_ID(i);float2 p=i.uv*2-1;float radius=length(p);float t=_Time.y*_Speed;
                half noise=SAMPLE_TEXTURE2D(_FlowMap,sampler_FlowMap,i.uv*2+float2(t*.06,-t*.04)).g;
                half rings=pow(saturate(sin(radius*24-t*5)*.5+.5),6);
                half disk=(1-smoothstep(.34,.97,radius))*(.25+noise*.5);
                half alpha=saturate(disk+rings*(1-smoothstep(.45,1,radius))*.35)*_Color.a;
                return half4(MixFog(_Color.rgb,i.fog),alpha);}
            ENDHLSL
        }
    }
}
