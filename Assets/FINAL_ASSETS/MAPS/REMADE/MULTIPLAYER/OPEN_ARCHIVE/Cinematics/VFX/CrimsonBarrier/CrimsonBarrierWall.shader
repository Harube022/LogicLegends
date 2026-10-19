Shader "LogicLegends/CrimsonMath/Veil"
{
    Properties {
        _DarkRed("Dark red", Color)=(.396,0,.035,1)
        _Crimson("Crimson", Color)=(.784,.063,.18,1)
        _Scarlet("Scarlet", Color)=(1,.188,.188,1)
        _RuneHighlights("Rune highlights", Color)=(1,.467,.467,1)
        _Glow("Glow",Float)=1.65
        _Opacity("Outside opacity",Range(0,.2))=.055
        _InsideOpacity("Inside opacity",Range(0,.1))=.018
        _Progress("Formation",Range(0,1))=1
        _Fade("Visibility",Range(0,1))=1
        _Clock("Animation time",Float)=0
    }
    SubShader {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass {
            Name "CrimsonVeil"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _DarkRed,_Crimson,_Scarlet,_RuneHighlights;
            float _Glow,_Opacity,_InsideOpacity,_Progress,_Fade,_Clock;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 positionWS:TEXCOORD1;float3 normalWS:TEXCOORD2;};
            V Vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.positionWS=TransformObjectToWorld(i.positionOS.xyz);o.normalWS=TransformObjectToWorldNormal(i.normalOS);o.uv=i.uv;return o;}
            half4 Frag(V i):SV_Target {
                float reveal=1-smoothstep(_Progress-.035,_Progress+.025,i.uv.y);
                float inside=step(dot(normalize(i.normalWS),GetWorldSpaceNormalizeViewDir(i.positionWS)),0);
                float edge=pow(saturate(abs(i.uv.y-.5)*2),12);
                float rise=pow(saturate(1-abs(frac(i.uv.y*2-_Clock*.18)-.5)*2),18);
                float flutes=pow(saturate(.5+.5*sin(i.uv.x*100.531)),28);
                float forming=exp2(-abs(i.uv.y-_Progress)*65)*(1-step(.999,_Progress));
                half3 color=lerp(_DarkRed.rgb,_Crimson.rgb,.4+rise*.35);
                color=lerp(color,_Scarlet.rgb,saturate(edge*.4+forming));
                float alpha=lerp(_Opacity,_InsideOpacity,inside)+(edge*.11+rise*.008+flutes*.012+forming*.22)*(1-inside*.65);
                return half4(color*_Glow,alpha*reveal*_Fade);
            }
            ENDHLSL
        }
    }
}
