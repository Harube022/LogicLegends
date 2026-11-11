Shader "LogicLegends/OpenArchive/MonsterSignal"
{
    Properties
    {
        [HDR] _Tint("Tint",Color)=(.55,1.1,1.3,1)
        _Opacity("Opacity",Range(0,1))=1
        _Kind("Bubble 0 / alert 1",Float)=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent"}
        Pass
        {
            Tags {"LightMode"="SRPDefaultUnlit"}
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half fog:TEXCOORD1;};
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;half _Opacity,_Kind;
            CBUFFER_END
            Varyings vert(Attributes v){Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;o.fog=ComputeFogFactor(o.positionCS.z);return o;}
            half4 frag(Varyings i):SV_Target
            {
                float2 p=i.uv*2-1;half3 color;half alpha;
                if(_Kind<.5)
                {
                    float radius=length(p/float2(.83,.88));
                    float rim=(1-smoothstep(.985,1.025,radius))*smoothstep(.87,.94,radius);
                    float glass=(1-smoothstep(.93,1,radius))*.055;
                    float highlight=exp(-dot((p-float2(-.30,.39))/float2(.13,.22),(p-float2(-.30,.39))/float2(.13,.22))*2.0);
                    float glint=exp(-dot((p-float2(.36,-.39))*17,(p-float2(.36,-.39))*17));
                    float crescent=(1-smoothstep(.65,.70,length((p-float2(.13,-.08))/float2(1,.92))))*smoothstep(.70,.76,radius)*.17;
                    alpha=saturate(rim*.78+glass+highlight*.75+glint*.55+crescent);
                    color=lerp(_Tint.rgb,_Tint.rgb*.38+half3(1,1,1),saturate(highlight+glint));
                }
                else
                {
                    float2 q=abs(p-float2(0,.18))-float2(.095,.37);
                    float stem=1-smoothstep(-.01,.025,length(max(q,0))+min(max(q.x,q.y),0)-.038);
                    float dotShape=1-smoothstep(.115,.145,length(p-float2(0,-.51)));
                    float glyph=max(stem,dotShape);
                    float2 outlineQ=abs(p-float2(0,.18))-float2(.13,.41);
                    float outline=max(1-smoothstep(0,.025,length(max(outlineQ,0))+min(max(outlineQ.x,outlineQ.y),0)-.038),1-smoothstep(.16,.19,length(p-float2(0,-.51))));
                    float halo=exp(-dot(p/float2(.42,.83),p/float2(.42,.83))*2)*.18;
                    alpha=saturate(outline*.8+halo);color=lerp(half3(.12,.035,.008),_Tint.rgb,glyph);
                }
                clip(alpha*_Opacity-.006);
                return half4(MixFog(color,i.fog),alpha*_Opacity);
            }
            ENDHLSL
        }
    }
}
