Shader "LogicLegends/OpenArchive/RoadShoulder"
{
    Properties
    {
        _PavingColor("Warm weathered limestone",Color)=(.77,.71,.49,1)
        _GrassColor("Existing meadow tint",Color)=(.94,1,.89,1)
        _DetailAtlas("Existing detail atlas",2D)="gray"{}
        _MacroMap("Existing ground variation",2D)="gray"{}
        _GrassMap("Existing grass albedo",2D)="white"{}
        _StoneMap("Existing stone grain",2D)="gray"{}
        _GrassReference("Existing grass average",Vector)=(.07,.14,.03,1)
        _PavingRepeat("Paving texture meters",Float)=6
        _GrassRepeat("Grass texture meters",Float)=3.8
        _GrassStrength("Grass texture strength",Range(0,1))=.94
        _StoneStrength("Stone grain",Range(0,1))=.64
        _EdgeWear("Organic worn edge",Range(0,.3))=.16
    }
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Art/Materials/ArchiveStableLighting.hlsl"
    TEXTURE2D(_DetailAtlas);SAMPLER(sampler_DetailAtlas);
    TEXTURE2D(_MacroMap);SAMPLER(sampler_MacroMap);
    TEXTURE2D(_GrassMap);SAMPLER(sampler_GrassMap);
    TEXTURE2D(_StoneMap);SAMPLER(sampler_StoneMap);
    CBUFFER_START(UnityPerMaterial)
        half4 _PavingColor,_GrassColor,_GrassReference;
        float _PavingRepeat,_GrassRepeat,_GrassStrength,_StoneStrength,_EdgeWear;
    CBUFFER_END
    ENDHLSL
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            Name "ContinuousRoadForward" Tags{"LightMode"="UniversalForward"}
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
            struct A{float4 p:POSITION;float3 n:NORMAL;half4 color:COLOR;float3 grass:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V{float4 p:SV_POSITION;float3 world:TEXCOORD0;half3 n:TEXCOORD1;half3 grass:TEXCOORD2;half weight:TEXCOORD3;half fog:TEXCOORD4;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
            V Vert(A i)
            {
                V o=(V)0;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.world);o.n=TransformObjectToWorldNormal(i.n);
                o.grass=i.grass;o.weight=i.color.r;o.fog=ComputeFogFactor(o.p.z);return o;
            }
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);half3 n=normalize(i.n);
                float2 p=i.world.xz/max(.1,_PavingRepeat),g=i.world.xz/max(.1,_GrassRepeat);
                half4 macro=SAMPLE_TEXTURE2D(_MacroMap,sampler_MacroMap,i.world.xz*.008);
                half4 detail=SAMPLE_TEXTURE2D_GRAD(_DetailAtlas,sampler_DetailAtlas,(float2(0,1)+(frac(p)*.96875+.015625))*.5,ddx(p)*.484375,ddy(p)*.484375);
                half2 rock=SAMPLE_TEXTURE2D(_StoneMap,sampler_StoneMap,p*1.6).rg;
                half3 paving=_PavingColor.rgb*(.66+detail.r*.65)*lerp(1,(.46+rock.r*1.08)*(1-rock.g*.42),_StoneStrength);
                float2 blocks=p*float2(2,3);blocks.x+=fmod(floor(blocks.y),2)*.5;
                float2 f=frac(blocks);float edge=min(min(f.x,1-f.x),min(f.y,1-f.y));
                float aa=max(fwidth(blocks.x),fwidth(blocks.y))*.65;
                half clarity=saturate(1-max(fwidth(blocks.x),fwidth(blocks.y))*1.7);
                half joint=(1-smoothstep(.018-aa,.036+aa,edge))*clarity;
                half variation=frac(sin(dot(floor(blocks),float2(17.43,39.71)))*437.53);
                paving*=lerp(.86,1.08,variation);paving=lerp(paving,paving*half3(.30,.32,.25),joint*.76);paving*=1-detail.g*.23;
                // This duplicates the existing meadow's opaque shading at the boundary,
                // using the original terrain's sampled vertex color, not green paving.
                half4 grassDetail=SAMPLE_TEXTURE2D_GRAD(_DetailAtlas,sampler_DetailAtlas,(frac(g)*.96875+.015625)*.5,ddx(g)*.484375,ddy(g)*.484375);
                half3 base=i.grass*_GrassColor.rgb;half grassGrain=.44+grassDetail.r*1.08;
                half3 painted=SAMPLE_TEXTURE2D(_GrassMap,sampler_GrassMap,g).rgb;
                half3 blades=clamp(painted/max(_GrassReference.rgb,half3(.01,.01,.01)),.38,1.85);
                half3 grass=lerp(base*grassGrain,base*blades,_GrassStrength);
                grass=lerp(grass,half3(.18,.105,.047)*grassGrain,saturate((macro.r-.64)*3)*.65);
                grass=lerp(grass,half3(.21,.245,.15)*grassGrain,saturate((macro.g-.71)*3.5)*.55);
                grass*=lerp(.88,1.12,macro.b);grass=lerp(grass,grass*half3(1.15,1.04,.70),saturate((macro.b-.62)*2));
                half noise=SAMPLE_TEXTURE2D(_MacroMap,sampler_MacroMap,i.world.xz*.23).a;
                half w=saturate(i.weight+(noise-.5)*_EdgeWear*4*i.weight*(1-i.weight));
                w=smoothstep(.10,.92,w);
                half dirt=sin(w*3.14159265)*.16;
                half3 albedo=lerp(grass,paving,w);albedo=lerp(albedo,albedo*half3(.80,.75,.55),dirt);
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
