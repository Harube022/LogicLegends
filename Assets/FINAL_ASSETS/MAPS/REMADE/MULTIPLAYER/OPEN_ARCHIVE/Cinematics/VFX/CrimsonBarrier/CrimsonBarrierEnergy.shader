Shader "LogicLegends/CrimsonMath/Energy"
{
    Properties {
        [NoScaleOffset] _MainTex("Baked math SDF atlas",2D)="white"{}
        _Glyph("SDF glyphs",Float)=0
        _Particle("Soft particle texture",Float)=0
        _Streak("Rising streaks",Float)=0
        _Cull("Cull",Float)=0
        _DarkRed("Dark red",Color)=(.396,0,.035,1)
        _Crimson("Crimson",Color)=(.784,.063,.18,1)
        _Scarlet("Scarlet",Color)=(1,.188,.188,1)
        _RuneHighlights("Rune highlights",Color)=(1,.467,.467,1)
        _Glow("Glow",Float)=1.65
        _Progress("Formation",Range(0,1))=1
        _Fade("Visibility",Range(0,1))=1
        _Break("Fragment separation",Range(0,1))=0
        _Clock("Animation time",Float)=0
        _BarrierCenter("World centre and radius",Vector)=(0,0,0,4.8)
    }
    SubShader {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent"}
        Pass {
            Name "CrimsonEnergy"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            half4 _DarkRed,_Crimson,_Scarlet,_RuneHighlights;
            float4 _BarrierCenter;
            float _Glyph,_Particle,_Streak,_Glow,_Progress,_Fade,_Break,_Clock;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;float2 seed:TEXCOORD1;half4 color:COLOR;};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float2 seed:TEXCOORD1;half visible:TEXCOORD2;half4 color:COLOR;};
            V Vert(A i){
                V o;float3 p=i.positionOS.xyz;
                p.y+=_Streak*(frac(_Clock*.15+i.seed.x)*7.1-i.seed.y*8+.1);
                float breakup=_Break*_Break*(.35+i.seed.x);
                p+=i.normalOS*breakup*1.4; p.y+=breakup*(.5+i.seed.x)*2;
                float3 world=TransformObjectToWorld(p);
                float outside=step(_BarrierCenter.w+.05,length(GetCameraPositionWS().xz-_BarrierCenter.xz));
                float back=step(dot(TransformObjectToWorldNormal(i.normalOS),GetWorldSpaceNormalizeViewDir(world)),0);
                // Correctly oriented interior text is for cameras inside the prison. Hiding it
                // from outside avoids two opposing formula panels overlapping the character.
                float hidden=lerp(back,1-step(.5,i.color.g),_Glyph)*outside;
                o.visible=lerp(1-hidden,1,_Particle);
                o.positionCS=TransformObjectToHClip(p);o.uv=i.uv;o.seed=i.seed;o.color=i.color;return o;
            }
            half4 Frag(V i):SV_Target {
                float d=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a;
                float width=max(fwidth(d)*.6,.012);
                float face=smoothstep(.5-width,.5+width,d);
                float halo=smoothstep(.17,.48,d)*.2;
                float coverage=lerp(1,saturate(face+halo),_Glyph)*lerp(1,d,_Particle);
                float reveal=smoothstep(i.seed.y-.1,i.seed.y+.05,_Progress);
                float pulse=.94+.06*sin(_Clock*1.4+i.seed.x*6.283);
                half3 glow=lerp(_Scarlet.rgb,_RuneHighlights.rgb,lerp(.45,face,_Glyph));
                glow=lerp(_Crimson.rgb,glow,saturate(i.color.r));
                return half4(glow*_Glow*pulse,coverage*i.color.a*_Fade*reveal*(1-_Break*.35)*i.visible);
            }
            ENDHLSL
        }
    }
}
