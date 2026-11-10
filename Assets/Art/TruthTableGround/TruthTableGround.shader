Shader "LogicLegends/TruthTable/GrassAndPaths"
{
    Properties
    {
        _GrassMap("Multiplayer painted grass",2D)="white"{}
        _DetailAtlas("Multiplayer earth detail atlas",2D)="gray"{}
        _MacroMap("Multiplayer broad color variation",2D)="gray"{}
        _PathMask("Baked ground paths (world XZ)",2D)="black"{}
        _GrassColor("Grass tint",Color)=(.48,.64,.29,1)
        _EarthColor("Warm dirt",Color)=(.5,.38,.24,1)
        _GrassReference("Grass texture average (linear)",Vector)=(.073633,.141184,.026107,1)
        _WorldRect("Mask world min X/Z and size X/Z",Vector)=(0,0,1,1)
        _TileMeters("Grass detail repeat in meters",Float)=3.8
        _Variation("Natural color variation",Range(0,1))=.65
    }
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Art/Materials/ArchiveStableLighting.hlsl"
    TEXTURE2D(_GrassMap);SAMPLER(sampler_GrassMap);
    TEXTURE2D(_DetailAtlas);SAMPLER(sampler_DetailAtlas);
    TEXTURE2D(_MacroMap);SAMPLER(sampler_MacroMap);
    TEXTURE2D(_PathMask);SAMPLER(sampler_PathMask);
    CBUFFER_START(UnityPerMaterial)
        half4 _GrassColor,_EarthColor,_GrassReference;
        float4 _WorldRect;
        float _TileMeters,_Variation;
    CBUFFER_END
    ENDHLSL
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            Name "GrassAndDirt" Tags {"LightMode"="UniversalForward"}
            ZWrite On Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            struct A {float4 p:POSITION;float3 n:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 p:SV_POSITION;float3 world:TEXCOORD0;half3 normal:TEXCOORD1;half fog:TEXCOORD2;half3 vertexLight:TEXCOORD3;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
            V Vert(A i)
            {
                V o=(V)0;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(i.n);o.fog=ComputeFogFactor(o.p.z);
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                o.vertexLight=VertexLighting(o.world,o.normal);
                #endif
                return o;
            }
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half3 n=normalize(i.normal),an=abs(n);
                // World-meter projection ignores the imported floor's stretched/palette UVs.
                float2 plane=an.y>=max(an.x,an.z)?i.world.xz:(an.x>an.z?i.world.zy:i.world.xy);
                float2 uv=plane/max(.25,_TileMeters);
                half3 a=SAMPLE_TEXTURE2D(_GrassMap,sampler_GrassMap,uv).rgb;
                #if defined(SHADER_API_MOBILE)
                // Use regular texture filtering on mobile and share the detail sample across both surfaces.
                half3 blades=clamp(a/max(_GrassReference.rgb,half3(.01,.01,.01)),.48,1.75);
                half3 earth=_EarthColor.rgb*lerp(.88,1.12,saturate(blades.g*.5));
                #else
                half3 b=SAMPLE_TEXTURE2D(_GrassMap,sampler_GrassMap,float2(-uv.y,uv.x)*.537+float2(.31,.67)).rgb;
                half3 blades=clamp(lerp(a,b,.23)/max(_GrassReference.rgb,half3(.01,.01,.01)),.48,1.75);
                float2 earthUV=plane/3.1;
                half4 earthDetail=SAMPLE_TEXTURE2D_GRAD(_DetailAtlas,sampler_DetailAtlas,(float2(1,0)+(frac(earthUV)*.96875+.015625))*.5,ddx(earthUV)*.484375,ddy(earthUV)*.484375);
                half3 earth=_EarthColor.rgb*(.83+earthDetail.r*.34)*(1-earthDetail.g*.16);
                #endif
                half4 macro=SAMPLE_TEXTURE2D(_MacroMap,sampler_MacroMap,i.world.xz*.008);
                half3 grass=_GrassColor.rgb*blades;
                grass*=lerp(1,lerp(.82,1.16,macro.b),_Variation);
                grass=lerp(grass,grass*half3(1.11,1.02,.79),saturate((macro.g-.52)*2)*_Variation);
                earth*=lerp(.91,1.08,macro.b);
                float2 maskUV=(i.world.xz-_WorldRect.xy)/_WorldRect.zw;
                half path=SAMPLE_TEXTURE2D(_PathMask,sampler_PathMask,maskUV).r;
                half top=smoothstep(.35,.75,n.y);
                half3 albedo=lerp(grass,earth,path*top);
                // Keep the low-poly bank facets; dirt/grass blends never add coplanar geometry.
                albedo=lerp(earth*.72,albedo,top);
                Light sun=ArchiveMainLight(i.world);
                half3 light=SampleSH(n)+sun.color*saturate(dot(n,sun.direction))*sun.shadowAttenuation;
                light+=ArchiveAdditionalDiffuse(i.world,n,i.p)+i.vertexLight;
                return half4(MixFog(albedo*light,i.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"} ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection,_LightPosition;
            struct A {float4 p:POSITION;float3 n:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            float4 ShadowVert(A i):SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(i);float3 p=TransformObjectToWorld(i.p.xyz);float3 n=TransformObjectToWorldNormal(i.n);
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
                return c;
            }
            half4 ShadowFrag():SV_Target{return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            struct A {float4 p:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
            float4 DepthVert(A i):SV_POSITION{UNITY_SETUP_INSTANCE_ID(i);return TransformObjectToHClip(i.p.xyz);}
            half4 DepthFrag():SV_Target{return 0;}
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
