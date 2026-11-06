Shader "LogicLegends/OpenArchive/DistantArchipelago"
{
    Properties
    {
        _Tint("Landscape tint",Color)=(1,1,1,1)
        _Haze("Distance haze",Range(0,1))=.48
        _NightHaze("Night silhouette haze",Range(0,1))=.7
        _SurfaceMap("Existing natural surface masks",2D)="gray"{}
        _SurfaceScale("World-space surface scale",Float)=.035
        _DetailStrength("Surface variation",Range(0,1))=.65
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "DistantLandscape"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On Cull Back
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_SurfaceMap); SAMPLER(sampler_SurfaceMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Haze, _NightHaze, _SurfaceScale, _DetailStrength;
            CBUFFER_END
            struct A { float4 position:POSITION; float3 normal:NORMAL; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 position:SV_POSITION; half4 color:TEXCOORD0; float4 world:TEXCOORD1; half3 normal:TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A input)
            {
                V output=(V)0;
                UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input,output); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 world=TransformObjectToWorld(input.position.xyz);
                output.position=TransformWorldToHClip(world);
                half3 normal=TransformObjectToWorldNormal(input.normal);
                Light sun=GetMainLight();
                half strength=max(sun.color.r,max(sun.color.g,sun.color.b));
                half daylight=saturate(strength*.75);
                half shape=.60+.40*saturate(dot(normal,sun.direction));
                half3 terrain=input.color.rgb*_Tint.rgb*shape*lerp(.13,1,daylight);
                half distanceHaze=smoothstep(140,690,distance(world,_WorldSpaceCameraPos))*_Haze;
                // A separate background atmosphere leaves the existing dense night
                // gameplay fog untouched. Distant land becomes a restrained silhouette.
                half haze=lerp(_NightHaze,distanceHaze,daylight);
                output.color=half4(terrain,haze);output.world=float4(world,daylight);output.normal=normal;
                return output;
            }
            half4 Frag(V input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 normal=normalize(input.normal);half3 weights=abs(normal);
                // One existing mipmapped mask sample, projected onto the dominant
                // face. Vertical cliffs avoid stretched UVs without three samples.
                float2 uv=weights.y>.55?input.world.xz:(weights.x>weights.z?input.world.zy:input.world.xy);
                half2 grain=SAMPLE_TEXTURE2D(_SurfaceMap,sampler_SurfaceMap,uv*_SurfaceScale).rg;
                half variation=lerp(.64,1.18,grain.r)*(1-grain.g*.18);
                half cliff=1-smoothstep(.35,.75,weights.y);
                half strata=.94+.06*sin(input.world.y*.50+grain.r*3);
                half3 terrain=input.color.rgb*lerp(1,variation,_DetailStrength)*lerp(1,strata,cliff);
                half3 atmosphere=unity_FogColor.rgb*lerp(.48,1,input.world.w);
                return half4(lerp(terrain,atmosphere,input.color.a),1);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
