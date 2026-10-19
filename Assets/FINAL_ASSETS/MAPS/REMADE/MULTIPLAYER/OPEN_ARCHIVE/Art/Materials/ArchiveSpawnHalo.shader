Shader "LogicLegends/OpenArchive/SpawnHalo"
{
    Properties {
        [HDR] _Tint("Glow color",Color)=(.2,1.8,.65,.48)
        _Spin("Rotation degrees per second",Float)=8
        _Pulse("Pulse speed",Float)=1.6
    }
    SubShader {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
        Pass {
            Tags {"LightMode"="UniversalForward"}
            Blend SrcAlpha One
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Spin,_Pulse;
            CBUFFER_END
            struct A {float4 p:POSITION;float2 uv:TEXCOORD0;};
            struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;half fog:TEXCOORD1;};
            V Vert(A i){V o;float a=radians(_Spin)*_Time.y;float s=sin(a),c=cos(a);i.p.xz=mul(float2x2(c,-s,s,c),i.p.xz);o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;o.fog=ComputeFogFactor(o.p.z);return o;}
            half4 Frag(V i):SV_Target{half edge=sin(saturate(i.uv.y)*3.14159265);half pulse=.78+.22*sin(_Time.y*_Pulse);return half4(MixFog(_Tint.rgb,i.fog),_Tint.a*edge*pulse);}
            ENDHLSL
        }
    }
}
