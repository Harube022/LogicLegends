Shader "LogicLegends/OpenArchive/WaterfallRibbon"
{
    Properties {
        _FlowMap("Packed flow and foam",2D)="gray"{}
        _DeepColor("Water shadow",Color)=(.07,.39,.48,1)
        _WaterColor("Water body",Color)=(.25,.69,.73,1)
        _FoamColor("Turbulent foam",Color)=(.82,.97,.96,1)
        _Speed("Downward flow speed",Range(.1,3))=.95
        _Foam("Foam",Range(0,1))=.65
        _Ripple("Sheet movement",Range(0,.04))=.012
    }
    SubShader {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest"}
        Pass {
            Name "WaterfallForward" Tags {"LightMode"="UniversalForward"} Cull Off ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_FlowMap);SAMPLER(sampler_FlowMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor,_WaterColor,_FoamColor;float _Speed,_Foam,_Ripple;
            CBUFFER_END
            struct A{float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V{float4 p:SV_POSITION;float3 world:TEXCOORD0;float2 uv:TEXCOORD1;half3 normal:TEXCOORD2;half pool:TEXCOORD3;half fog:TEXCOORD4;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
            V Vert(A i){V o=(V)0;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                i.p.xyz+=i.n*sin(i.uv.y*3.14159)*sin(i.uv.x*19+i.uv.y*12-_Time.y*5)*_Ripple*(1-i.color.r);
                o.world=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.world);o.uv=i.uv;o.normal=TransformObjectToWorldNormal(i.n);o.pool=i.color.r;o.fog=ComputeFogFactor(o.p.z);return o;}
            half4 Frag(V i):SV_Target{
                UNITY_SETUP_INSTANCE_ID(i);float time=_Time.y*_Speed;
                half4 flow=SAMPLE_TEXTURE2D(_FlowMap,sampler_FlowMap,float2(i.uv.x*2.3,i.uv.y*.85+time*.8));
                half4 small=SAMPLE_TEXTURE2D(_FlowMap,sampler_FlowMap,float2(i.uv.x*5.1+flow.g*.12,i.uv.y*2.9+time*1.35));
                half3 col;
                if(i.pool>.5){half froth=smoothstep(.27,.68,small.g);col=lerp(_WaterColor.rgb,_FoamColor.rgb,froth*.62);}
                else{
                    half edge=min(i.uv.x,1-i.uv.x);clip(edge-(.005+flow.b*.016));
                    half streak=saturate(flow.r*.85+small.r*.22);
                    col=lerp(_DeepColor.rgb,_WaterColor.rgb,.25+streak*.75);
                    half foam=pow(saturate(flow.r*1.13),4)*.62+pow(saturate(small.g*1.06),5)*.12;
                    foam+=smoothstep(.88,1,i.uv.y)*(.28+flow.g*.4);
                    foam+=(1-smoothstep(0,.23,i.uv.y))*(.35+small.g*.45);
                    col=lerp(col,_FoamColor.rgb,saturate(foam*_Foam));
                }
                half3 n=normalize(i.normal);Light sun=GetMainLight();half brightness=.78+abs(dot(n,sun.direction))*.22;
                return half4(MixFog(col*brightness,i.fog),1);
            }
            ENDHLSL
        }
    }
}
