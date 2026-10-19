Shader "LogicLegends/OpenArchive/TreeTimber"
{
    Properties {
        _BaseMap("Existing palette",2D)="white"{}
        _TimberMap("Painted grain, grooves, end grain, weather",2D)="gray"{}
        _CanopyMap("Leaf shapes and veins",2D)="gray"{}
        _Kind("0 trees, 1 bridge timber",Float)=0
        _BarkStrength("Bark texture strength",Range(0,1))=.9
        _WoodStrength("Bridge wood texture strength",Range(0,1))=.9
        _LeafStrength("Leaf texture strength",Range(0,1))=.85
        _BarkBrightness("Bark brightness",Range(.5,1.8))=1.2
        _BarkRepeat("Bark repeat along / across",Vector)=(1.7,.7,0,0)
        _WoodRepeat("Wood repeat along / across",Vector)=(3,.8,0,0)
        _LeafRepeat("Leaf repeat in model units",Float)=.85
        _PineRepeat("Pine needle repeat in model units",Float)=1.8
        _Relief("Fine grain relief in meters",Range(0,.05))=.012
        _Tint("Palette tint",Color)=(1,1,1,1)
    }
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "ArchiveStableLighting.hlsl"
    TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
    TEXTURE2D(_TimberMap);SAMPLER(sampler_TimberMap);
    TEXTURE2D(_CanopyMap);SAMPLER(sampler_CanopyMap);
    CBUFFER_START(UnityPerMaterial)
        half4 _Tint;float4 _BarkRepeat,_WoodRepeat;
        float4 _TimberMap_TexelSize;
        float _Kind,_BarkStrength,_WoodStrength,_LeafStrength,_BarkBrightness,_LeafRepeat,_PineRepeat,_Relief;
    CBUFFER_END
    ENDHLSL
    SubShader {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass {
            Name "TreeTimberForward" Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            struct A{float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;float4 detail:TEXCOORD2;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V{float4 p:SV_POSITION;float3 world:TEXCOORD0;half3 normal:TEXCOORD1;float2 uv:TEXCOORD2;float4 detail:TEXCOORD3;half fog:TEXCOORD4;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
            V Vert(A i){V o=(V)0;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(i.n);o.uv=i.uv;o.detail=i.detail;o.fog=ComputeFogFactor(o.p.z);return o;}
            half4 Frag(V i):SV_Target{
                UNITY_SETUP_INSTANCE_ID(i);half3 n=normalize(i.normal);
                half3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_Tint.rgb;
                float height=0,footprint=0;float4 detailDx=ddx(i.detail),detailDy=ddy(i.detail);
                if(_Kind<.5){
                    // Model-space coordinates survive static batching and keep the same texture phase on both LODs.
                    float3 model=i.detail.xyz;float3 modelNormal=cross(detailDx.xyz,detailDy.xyz);modelNormal/=max(length(modelNormal),1e-8);
                    float3 weights=pow(abs(modelNormal),4);weights/=max(.0001,weights.x+weights.y+weights.z);
                    if(i.detail.w>.5){
                        float repeat=max(.1,i.detail.w>1.5?_PineRepeat:_LeafRepeat);
                        half4 leaf=SAMPLE_TEXTURE2D_GRAD(_CanopyMap,sampler_CanopyMap,model.zy/repeat,detailDx.zy/repeat,detailDy.zy/repeat)*weights.x;
                        leaf+=SAMPLE_TEXTURE2D_GRAD(_CanopyMap,sampler_CanopyMap,model.xz/repeat,detailDx.xz/repeat,detailDy.xz/repeat)*weights.y;
                        leaf+=SAMPLE_TEXTURE2D_GRAD(_CanopyMap,sampler_CanopyMap,model.xy/repeat,detailDx.xy/repeat,detailDy.xy/repeat)*weights.z;
                        half pattern=i.detail.w>1.5?(.58+leaf.b):(.64+leaf.r*.52+leaf.g*.18);
                        albedo*=lerp(1,pattern,_LeafStrength)*(.94+leaf.a*.12);
                    }else{
                        float2 repeat=max(float2(.1,.1),_BarkRepeat.xy);
                        half4 wood=SAMPLE_TEXTURE2D_GRAD(_TimberMap,sampler_TimberMap,model.yz/repeat,detailDx.yz/repeat,detailDy.yz/repeat)*weights.x;
                        wood+=SAMPLE_TEXTURE2D_GRAD(_TimberMap,sampler_TimberMap,model.xz/repeat,detailDx.xz/repeat,detailDy.xz/repeat)*weights.y;
                        wood+=SAMPLE_TEXTURE2D_GRAD(_TimberMap,sampler_TimberMap,model.yx/repeat,detailDx.yx/repeat,detailDy.yx/repeat)*weights.z;
                        albedo*=lerp(1,.53+wood.r*1.12-wood.g*.32,_BarkStrength)*_BarkBrightness;
                        height=(wood.r-wood.g*.35)*_Relief;
                        footprint=max(length(detailDx.xyz),length(detailDy.xyz))*_TimberMap_TexelSize.z/min(repeat.x,repeat.y);
                    }
                }else{
                    bool cap=i.detail.z>.5;float2 repeat=cap?float2(1,1):max(float2(.1,.1),_WoodRepeat.xy);
                    half4 wood=SAMPLE_TEXTURE2D_GRAD(_TimberMap,sampler_TimberMap,i.detail.xy/repeat,detailDx.xy/repeat,detailDy.xy/repeat);
                    half grain=cap?wood.b:wood.r;half grooves=cap?0:wood.g;
                    albedo*=lerp(1,.53+grain*1.12-grooves*.32,_WoodStrength)*(.94+wood.a*.12)*i.detail.w;
                    height=(grain-grooves*.35)*_Relief;footprint=max(length(detailDx.xy/repeat),length(detailDy.xy/repeat))*_TimberMap_TexelSize.z;
                }
                n=ArchiveFineNormal(i.world,n,height,footprint);
                Light sun=ArchiveMainLight(i.world);half3 light=SampleSH(n)+sun.color*saturate(dot(n,sun.direction))*sun.shadowAttenuation;
                return half4(MixFog(albedo*light,i.fog),1);
            }
            ENDHLSL
        }
        Pass {
            Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"} ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection,_LightPosition;
            struct A{float4 p:POSITION;float3 n:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            float4 ShadowVert(A i):SV_POSITION{UNITY_SETUP_INSTANCE_ID(i);float3 p=TransformObjectToWorld(i.p.xyz);float3 n=TransformObjectToWorldNormal(i.n);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 d=normalize(_LightPosition-p);
                #else
                float3 d=_LightDirection;
                #endif
                float4 c=TransformWorldToHClip(ApplyShadowBias(p,n,d));
                #if UNITY_REVERSED_Z
                c.z=min(c.z,UNITY_NEAR_CLIP_VALUE*c.w);
                #else
                c.z=max(c.z,UNITY_NEAR_CLIP_VALUE*c.w);
                #endif
                return c;}
            half4 ShadowFrag():SV_Target{return 0;}
            ENDHLSL
        }
        Pass {
            Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            struct A{float4 p:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
            float4 DepthVert(A i):SV_POSITION{UNITY_SETUP_INSTANCE_ID(i);return TransformObjectToHClip(i.p.xyz);}
            half4 DepthFrag():SV_Target{return 0;}
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
