Shader "LogicLegends/OpenArchive/NestGround"
{
    Properties
    {
        _StoneColor("Weathered courtyard stone",Color)=(.43,.46,.43,1)
        _EarthColor("Warm sleeping earth",Color)=(.36,.33,.27,1)
        _GroundCenter("Nest center in world space",Vector)=(0,0,0,0)
        _BlendRadii("Earth to stone radii",Vector)=(10,22,0,0)
        _StoneRepeat("Stone detail meters",Float)=7
        _GrainStrength("Stone grain",Range(0,1))=.5
        _DetailAtlas("Existing painted detail",2D)="gray"{}
        _MacroMap("Existing ground variation",2D)="gray"{}
        _StoneMap("Existing natural stone grain",2D)="gray"{}
    }
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Art/Materials/ArchiveStableLighting.hlsl"
    TEXTURE2D(_DetailAtlas);SAMPLER(sampler_DetailAtlas);
    TEXTURE2D(_MacroMap);SAMPLER(sampler_MacroMap);
    TEXTURE2D(_StoneMap);SAMPLER(sampler_StoneMap);
    CBUFFER_START(UnityPerMaterial)
        half4 _StoneColor,_EarthColor;
        float4 _GroundCenter,_BlendRadii;
        float _StoneRepeat,_GrainStrength;
    CBUFFER_END
    ENDHLSL
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            Name "ContinuousNestGround" Tags{"LightMode"="UniversalForward"}
            ZWrite On Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            struct A{float4 p:POSITION;float3 n:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V{float4 p:SV_POSITION;float3 world:TEXCOORD0;half3 n:TEXCOORD1;half fog:TEXCOORD2;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
            V Vert(A i)
            {
                V o=(V)0;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.world);o.n=TransformObjectToWorldNormal(i.n);
                o.fog=ComputeFogFactor(o.p.z);return o;
            }
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);half3 n=normalize(i.n);
                // Exactly four texture reads. Every surface uses the same world XZ
                // coordinates, so the old bed mesh boundary has no material seam.
                float2 uv=i.world.xz/max(.1,_StoneRepeat),earthUV=i.world.xz/4.3;
                half4 detail=SAMPLE_TEXTURE2D_GRAD(_DetailAtlas,sampler_DetailAtlas,(float2(0,1)+(frac(uv)*.96875+.015625))*.5,ddx(uv)*.484375,ddy(uv)*.484375);
                half4 earthDetail=SAMPLE_TEXTURE2D_GRAD(_DetailAtlas,sampler_DetailAtlas,(float2(1,0)+(frac(earthUV)*.96875+.015625))*.5,ddx(earthUV)*.484375,ddy(earthUV)*.484375);
                half4 macro=SAMPLE_TEXTURE2D(_MacroMap,sampler_MacroMap,i.world.xz*.008);
                half2 rock=SAMPLE_TEXTURE2D(_StoneMap,sampler_StoneMap,uv).rg;
                half3 stone=_StoneColor.rgb*(.75+detail.r*.5)*(1-detail.g*.20);
                stone*=lerp(1,(.75+rock.r*.5)*(1-rock.g*.20),_GrainStrength);
                half3 earth=_EarthColor.rgb*(.83+earthDetail.r*.34)*(1-earthDetail.g*.17);
                half variation=lerp(.91,1.09,macro.b);
                stone*=variation;earth*=variation;
                float radius=length(i.world.xz-_GroundCenter.xz)+(macro.r-.5)*1.4;
                half blend=smoothstep(_BlendRadii.x,_BlendRadii.y,radius);
                half3 albedo=lerp(earth,stone,blend);
                Light sun=ArchiveMainLight(i.world);
                half3 light=SampleSH(n)+sun.color*saturate(dot(n,sun.direction))*sun.shadowAttenuation;
                light+=ArchiveAdditionalDiffuse(i.world,n,i.p);
                return half4(MixFog(albedo*light,i.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags{"LightMode"="DepthOnly"} ZWrite On ColorMask R
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
