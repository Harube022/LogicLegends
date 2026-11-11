Shader "LogicLegends/Cinematics/RealmOcean"
{
    Properties { _MapTime("Film time",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _MapTime;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; };
            struct V { float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float2 plane:TEXCOORD1;float3 normalWS:TEXCOORD2; };
            V vert(A v) { V o;float2 p=v.positionOS.xz;float a=p.x*.055+p.y*.035-_MapTime*.55;float b=p.y*.08+_MapTime*.35;v.positionOS.y+=sin(a)*.48+sin(b)*.18;float3 n=normalize(float3(-cos(a)*.0264,1,-cos(a)*.0168-cos(b)*.0144));o.positionWS=TransformObjectToWorld(v.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);o.plane=p;o.normalWS=TransformObjectToWorldNormal(n);return o; }
            half4 frag(V i):SV_Target { float2 p=i.plane;float slow=.5+.25*sin(p.x*.009+sin(p.y*.013))*sin(p.y*.011+p.x*.004);float ripple=pow(saturate(sin(p.x*.37+_MapTime*.3)*sin(p.y*.42+sin(p.x*.07)-_MapTime*.5)),16);float fresnel=pow(1-saturate(dot(normalize(i.normalWS),normalize(_WorldSpaceCameraPos-i.positionWS))),3);half3 color=lerp(half3(.018,.075,.125),half3(.025,.21,.26),slow);color+=ripple*.018+fresnel*half3(.06,.15,.20);return half4(color,1); }
            ENDHLSL
        }
    }
}
