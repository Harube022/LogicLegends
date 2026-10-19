Shader "LogicLegends/OpenArchive/NaturalSurfaces"
{
    Properties {
        _BaseMap("Original palette",2D)="white"{}
        _SurfaceMap("Rock grain, crevices, leaves, veins",2D)="gray"{}
        _BaseColor("Tint",Color)=(.55,.59,.56,1)
        _PaletteMix("Use original plant palette",Range(0,1))=0
        _Kind("0 stone, 1 foliage, 2 bank",Float)=0
        _Repeat("Detail repeat in world meters",Float)=2.8
        _Detail("Surface detail",Range(0,1))=.6
        _Relief("Fine surface relief (meters)",Range(0,.15))=.012
        _Moss("Soft moss patches",Range(0,1))=.22
        _WaterLevel("Waterline in world meters",Float)=-2.875
    }
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "ArchiveStableLighting.hlsl"
    TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
    TEXTURE2D(_SurfaceMap);SAMPLER(sampler_SurfaceMap);
    CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor;float4 _SurfaceMap_TexelSize;float _PaletteMix,_Kind,_Repeat,_Detail,_Relief,_Moss,_WaterLevel;
    CBUFFER_END
    ENDHLSL
    SubShader {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass {
            Name "NaturalForward" Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            struct A{float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V{float4 p:SV_POSITION;float3 world:TEXCOORD0;half3 normal:TEXCOORD1;float2 uv:TEXCOORD2;half4 color:COLOR;half fog:TEXCOORD3;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
            V Vert(A i){V o=(V)0;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(i.n);o.uv=i.uv;o.color=i.color;o.fog=ComputeFogFactor(o.p.z);return o;}
            half4 Frag(V i):SV_Target{
                UNITY_SETUP_INSTANCE_ID(i);half3 n=normalize(i.normal);half3 weights=pow(abs(n),4);weights/=max(.001,weights.x+weights.y+weights.z);
                float3 uv=i.world/max(.1,_Repeat);
                half4 detail=SAMPLE_TEXTURE2D(_SurfaceMap,sampler_SurfaceMap,uv.zy)*weights.x+SAMPLE_TEXTURE2D(_SurfaceMap,sampler_SurfaceMap,uv.xz)*weights.y+SAMPLE_TEXTURE2D(_SurfaceMap,sampler_SurfaceMap,uv.xy)*weights.z;
                half3 palette=lerp(half3(1,1,1),SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb,_PaletteMix);
                half3 albedo=_BaseColor.rgb*palette*i.color.rgb;
                if(_Kind>.5&&_Kind<1.5){
                    half leaf=i.color.a;albedo*=lerp(1,.55+detail.b*.95-detail.a*.12,leaf*_Detail);
                }else{
                    albedo*=lerp(1,.58+detail.r*.88-detail.g*.36,_Detail);
                    float height=(detail.r-detail.g*.45)*_Relief;
                    float footprint=max(length(ddx(uv)),length(ddy(uv)))*_SurfaceMap_TexelSize.z;
                    n=ArchiveFineNormal(i.world,n,height,footprint);
                    half wet=saturate((_WaterLevel+.7-i.world.y)*1.2);albedo*=lerp(1,.62,wet);
                    half moss=saturate((detail.b-.49)*2)*_Moss*saturate(n.y+.35);
                    albedo=lerp(albedo,albedo*half3(.61,.88,.48),moss);
                    if(_Kind>1.5){half lip=saturate((i.world.y-_WaterLevel-1.0)*.5);albedo=lerp(albedo,albedo*half3(.67,.82,.53),lip*detail.b*.23);}
                }
                Light sun=ArchiveMainLight(i.world);half3 light=SampleSH(n)+sun.color*saturate(dot(n,sun.direction))*sun.shadowAttenuation;
                if(_Kind>1.5)light=lerp(light,SampleSH(half3(0,1,0))+sun.color*.38*sun.shadowAttenuation,.65);
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
}
