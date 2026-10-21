Shader "LogicLegends/OpenArchive/ExitPortal"
{
    Properties
    {
        [HDR] _Tint("Energy tint",Color)=(.1,2.2,2,1)
        _Glow("Glow",Range(.1,3))=1
        _Formation("Formation",Range(0,1))=1
        _Membrane("Membrane (0 for thin rings)",Float)=1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+20" }
        Pass
        {
            Name "PortalUnlit"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;half _Glow,_Formation,_Membrane;
            CBUFFER_END
            Varyings vert(Attributes v){Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;return o;}
            half4 frag(Varyings i):SV_Target
            {
                float2 p=i.uv*2-1;float r=length(p),a=atan2(p.y,p.x),t=_Time.y;
                float spiral=sin(a*5-r*26+t*2.1)*.5+.5;
                float ripples=pow(saturate(sin(r*47-a*3-t*2.8)),9);
                float edge=pow(saturate(r),6);
                float cloud=sin(p.x*9+p.y*7+t*.7)*sin(p.y*12-p.x*5-t*.5)*.5+.5;
                half3 deep=half3(.015,.07,.12);
                half3 membrane=lerp(deep,_Tint.rgb*.34,spiral*.55+cloud*.18)+_Tint.rgb*(edge*.5+ripples*.12)*_Glow;
                half3 energy=_Tint.rgb*_Glow*(.75+.25*sin(a*12-t*2));
                half alpha=lerp(.94,.83,spiral)*(1-smoothstep(.985,1,r));
                return half4(lerp(energy,membrane,_Membrane),lerp(.95,alpha,_Membrane)*_Formation);
            }
            ENDHLSL
        }
    }
}
