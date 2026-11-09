Shader "LogicLegends/OpenArchive/Fog Horizon Skybox"
{
    Properties
    {
        _Tint("Tint Color",Color)=(.5,.5,.5,.5)
        [Gamma] _Exposure("Exposure",Range(0,8))=1
        _Rotation("Rotation",Range(0,360))=0
        [NoScaleOffset] _Tex("Cubemap",Cube)="grey"{}
        _NightMist("Night horizon mist",Range(0,1))=0
    }
    SubShader
    {
        Tags{"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"}
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            samplerCUBE _Tex;
            half4 _Tex_HDR,_Tint;
            half _Exposure,_NightMist;
            float _Rotation;
            struct A{float4 vertex:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V{float4 position:SV_POSITION;float3 direction:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            V Vert(A input)
            {
                V o;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float angle=radians(_Rotation),s=sin(angle),c=cos(angle);
                float3 rotated=float3(c*input.vertex.x-s*input.vertex.z,input.vertex.y,s*input.vertex.x+c*input.vertex.z);
                o.position=UnityObjectToClipPos(float4(rotated,1));o.direction=input.vertex.xyz;return o;
            }
            half4 Frag(V input):SV_Target
            {
                half3 sky=DecodeHDR(texCUBE(_Tex,input.direction),_Tex_HDR)*_Tint.rgb*unity_ColorSpaceDouble.rgb*_Exposure;
                // Merge distant scenery into the horizon rather than leaving pale
                // fog silhouettes cut against a completely black sky. No extra pass.
                float horizon=pow(1-saturate(abs(normalize(input.direction).y)*3.5),2);
                return half4(lerp(sky,unity_FogColor.rgb,horizon*_NightMist),1);
            }
            ENDHLSL
        }
    }
}
