Shader "LogicLegends/OpenArchive/DetailedSurface"
{
    Properties
    {
        _BaseMap("Existing palette", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1,1,1,1)
        _DetailAtlas("Packed painted detail", 2D) = "gray" {}
        _MacroMap("Ground and moss variation", 2D) = "gray" {}
        _GrassMap("Painted grass albedo", 2D) = "white" {}
        _GrassTextureStrength("Visible grass texture", Range(0,1)) = 0
        _GrassReference("Grass average (linear RGB)", Vector) = (.09,.14,.035,1)
        _StoneMap("Rock grain and natural crevices (RG)", 2D) = "gray" {}
        _StoneTextureStrength("Visible stone texture", Range(0,1)) = 0
        _EmissionMap("Original emissive palette", 2D) = "black" {}
        _EmissionColor("Emission tint", Color) = (1,1,1,1)
        _EmissionStrength("Emission", Range(0,1)) = 0
        _VertexColor("Use existing vertex colors", Range(0,1)) = 0
        _SurfaceStyle("0 grass, 1 stone, 2 cliff, 3 earth, 4 wood, 5 foliage, 6 paving", Float) = 1
        _TileMeters("Texture repeat in world meters", Float) = 5
        _DetailStrength("Painted detail", Range(0,1)) = .65
        _MossAmount("Moss", Range(0,1)) = .2
        _MossHeight("Moss base height", Float) = 3
    }
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "ArchiveStableLighting.hlsl"
    TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
    TEXTURE2D(_DetailAtlas); SAMPLER(sampler_DetailAtlas);
    TEXTURE2D(_MacroMap); SAMPLER(sampler_MacroMap);
    TEXTURE2D(_GrassMap); SAMPLER(sampler_GrassMap);
    TEXTURE2D(_StoneMap); SAMPLER(sampler_StoneMap);
    TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
    CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor, _EmissionColor;
        half4 _GrassReference;
        float _GrassTextureStrength;
        float _StoneTextureStrength;
        float _EmissionStrength, _VertexColor, _SurfaceStyle, _TileMeters, _DetailStrength, _MossAmount, _MossHeight;
    CBUFFER_END
    ENDHLSL
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "DetailedForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            struct A {float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 p:SV_POSITION;float3 world:TEXCOORD0;half3 normal:TEXCOORD1;float2 uv:TEXCOORD2;half4 color:COLOR;half fog:TEXCOORD3;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
            V Vert(A i){V o=(V)0;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(i.n);
                o.uv=TRANSFORM_TEX(i.uv,_BaseMap);o.color=lerp(half4(1,1,1,1),i.color,_VertexColor);o.fog=ComputeFogFactor(o.p.z);return o;}
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half3 n=normalize(i.normal);half3 an=abs(n);
                // One dominant projection keeps the low-poly facets and avoids stretching
                // on the kit's palette-point UVs. It costs one detail sample, not three.
                float2 plane=an.y>=max(an.x,an.z)?i.world.xz:(an.x>an.z?i.world.zy:i.world.xy);
                float2 uv=plane/max(.1,_TileMeters);
                float tile=_SurfaceStyle>5.5?2:((_SurfaceStyle<.5||_SurfaceStyle>4.5)?0:(_SurfaceStyle>3.5?3:(_SurfaceStyle>2.5?1:2)));
                float2 cell=float2(fmod(tile,2),floor(tile*.5));
                float2 atlasUV=(cell+(frac(uv)*.96875+.015625))*.5;
                half4 detail=SAMPLE_TEXTURE2D_GRAD(_DetailAtlas,sampler_DetailAtlas,atlasUV,ddx(uv)*.484375,ddy(uv)*.484375);
                half4 macro=SAMPLE_TEXTURE2D(_MacroMap,sampler_MacroMap,i.world.xz*.008);
                half3 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb*i.color.rgb;
                half grain=lerp(1,.66+detail.r*.65,_DetailStrength);
                if(_SurfaceStyle<.5)grain=.44+detail.r*1.08;
                half3 albedo=base*grain;
                if(_StoneTextureStrength>.01 && ((_SurfaceStyle>.5 && _SurfaceStyle<2.5)||_SurfaceStyle>5.5)){
                    half2 rock=SAMPLE_TEXTURE2D(_StoneMap,sampler_StoneMap,uv*(_SurfaceStyle>5.5?1.6:1)).rg;
                    half rockGrain=(.46+rock.r*1.08)*(1-rock.g*.42);
                    albedo*=lerp(1,rockGrain,_StoneTextureStrength);
                }
                if(_SurfaceStyle<.5){
                    if(_GrassTextureStrength>.01){
                        half3 painted=SAMPLE_TEXTURE2D(_GrassMap,sampler_GrassMap,uv).rgb;
                        half3 blades=clamp(painted/max(_GrassReference.rgb,half3(.01,.01,.01)),.38,1.85);
                        albedo=lerp(albedo,base*blades,_GrassTextureStrength);
                    }
                    half earth=saturate((macro.r-.64)*3.0);
                    half rocky=saturate((macro.g-.71)*3.5);
                    albedo=lerp(albedo,half3(.18,.105,.047)*grain,earth*.65);
                    albedo=lerp(albedo,half3(.21,.245,.15)*grain,rocky*.55);
                    albedo*=lerp(.88,1.12,macro.b);
                    albedo=lerp(albedo,albedo*half3(1.15,1.04,.70),saturate((macro.b-.62)*2));
                }
                if(_SurfaceStyle>.5 && _SurfaceStyle<3.5){
                    albedo*=1-detail.g*_DetailStrength*.55;
                    half top=saturate(n.y*.8+.18);
                    half low=saturate((_MossHeight-i.world.y)*.13+.4);
                    half moss=detail.b*_MossAmount*saturate(top+low*.4);
                    albedo=lerp(albedo,albedo*half3(.49,.78,.23),moss);
                    if(_SurfaceStyle>1.5 && _SurfaceStyle<2.5){
                        half strata=sin(i.world.y*1.6+detail.r*2)*.06;
                        albedo*=1+strata;
                    }
                }
                if(_SurfaceStyle>3.5 && _SurfaceStyle<4.5)albedo*=1-detail.g*.5;
                if(_SurfaceStyle>4.5 && _SurfaceStyle<5.5)albedo*=lerp(.93,1.07,macro.a);
                if(_SurfaceStyle>5.5){
                    float2 paving=uv*float2(2,3);
                    paving.x+=fmod(floor(paving.y),2)*.5;
                    float2 f=frac(paving);
                    float edge=min(min(f.x,1-f.x),min(f.y,1-f.y));
                    float aa=max(fwidth(paving.x),fwidth(paving.y))*.65;
                    half joint=1-smoothstep(.018-aa,.036+aa,edge);
                    half bevel=1-smoothstep(.035-aa,.09+aa,edge);
                    half clarity=saturate(1-max(fwidth(paving.x),fwidth(paving.y))*1.7);
                    joint*=clarity;bevel*=clarity;
                    float2 block=floor(paving);
                    half variation=frac(sin(dot(block,float2(17.43,39.71)))*437.53);
                    albedo*=lerp(.84,1.11,variation);
                    half lightEdge=1-smoothstep(.04-aa,.095+aa,min(f.x,1-f.y));
                    albedo*=1+(.12*lightEdge-.08*bevel)*clarity;
                    albedo=lerp(albedo,albedo*half3(.30,.32,.25),joint*.84);
                    albedo*=1-detail.g*.27;
                }
                Light sun=ArchiveMainLight(i.world);
                half3 illumination=SampleSH(n)+sun.color*saturate(dot(n,sun.direction))*sun.shadowAttenuation;
                half3 emission=0;
                if(_EmissionStrength>.01)emission=SAMPLE_TEXTURE2D(_EmissionMap,sampler_EmissionMap,i.uv).rgb*_EmissionColor.rgb*_EmissionStrength;
                return half4(MixFog(albedo*illumination+emission,i.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags {"LightMode"="ShadowCaster"}
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex VertShadow
            #pragma fragment FragShadow
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection, _LightPosition;
            struct A {float4 p:POSITION;float3 n:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            float4 VertShadow(A i):SV_POSITION {UNITY_SETUP_INSTANCE_ID(i);float3 p=TransformObjectToWorld(i.p.xyz);float3 n=TransformObjectToWorldNormal(i.n);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 direction=normalize(_LightPosition-p);
                #else
                float3 direction=_LightDirection;
                #endif
                float4 c=TransformWorldToHClip(ApplyShadowBias(p,n,direction));
                #if UNITY_REVERSED_Z
                c.z=min(c.z,UNITY_NEAR_CLIP_VALUE*c.w);
                #else
                c.z=max(c.z,UNITY_NEAR_CLIP_VALUE*c.w);
                #endif
                return c;}
            half4 FragShadow():SV_Target{return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags {"LightMode"="DepthOnly"}
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex VertDepth
            #pragma fragment FragDepth
            #pragma multi_compile_instancing
            struct A {float4 p:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
            float4 VertDepth(A i):SV_POSITION{UNITY_SETUP_INSTANCE_ID(i);return TransformObjectToHClip(i.p.xyz);}
            half4 FragDepth():SV_Target{return 0;}
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
