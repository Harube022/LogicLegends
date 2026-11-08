Shader "Hidden/LogicLegends/OpeningReviewBlend"
{
    Properties{_MainTex("Previous",2D)="black"{} _NextTex("Current",2D)="black"{} _Blend("Blend",Float)=1 _Fade("Visibility",Float)=1}
    SubShader{Cull Off ZWrite Off ZTest Always Pass{
        CGPROGRAM
        #pragma vertex vert_img
        #pragma fragment frag
        #include "UnityCG.cginc"
        sampler2D _MainTex,_NextTex;float _Blend,_Fade;
        fixed4 frag(v2f_img i):SV_Target{return lerp(tex2D(_MainTex,i.uv),tex2D(_NextTex,i.uv),_Blend)*float4(_Fade,_Fade,_Fade,1);}
        ENDCG
    }}
}
