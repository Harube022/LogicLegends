Shader "LogicLegends/TruthTable/NightVisibility"
{
 Properties { [PerRendererData] _MainTex("World view",2D)="black"{} }
 SubShader
 {
  Tags {"Queue"="Overlay" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"}
  Cull Off ZWrite Off ZTest Always
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #include "UnityCG.cginc"
   sampler2D _MainTex;
   float4 _MainTex_TexelSize;
   float4x4 _WorldFromClip;
   float4 _Player,_Forward,_Cone,_ViewPosition; // half-width cosine, outer cosine, darkness, blur mix
   float4 _Lamps[6]; // xyz: fixture, w: protected pool radius
   float _Strength,_BlurPixels,_NearRadius;
   int _LampCount;
   struct a {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
   struct v {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;};
   v vert(a i){v o;o.vertex=UnityObjectToClipPos(i.vertex);o.uv=i.uv;return o;}
   fixed4 frag(v i):SV_Target
   {
    float2 uv=i.uv;fixed3 original=tex2D(_MainTex,uv).rgb;
    if(_Strength<.001)return fixed4(original,1);
    // Project the view ray onto the player's ground plane. No depth prepass/copy is needed.
    #if defined(UNITY_REVERSED_Z)
      float farDepth=0;
    #else
      float farDepth=1;
    #endif
    float4 distant=mul(_WorldFromClip,float4(uv*2-1,farDepth,1));distant.xyz/=max(abs(distant.w),.000001)*sign(distant.w);
    float3 ray=distant.xyz-_ViewPosition.xyz;
    float t=(_Player.y-.1-_ViewPosition.y)/(abs(ray.y)>.00001?ray.y:.00001);
    float3 ground=_ViewPosition.xyz+ray*(t>0?min(t,1):1);
    float4 world=float4(ground,1);
    float2 direction=world.xz-_Player.xz;float distanceToPlayer=length(direction);
    float facing=dot(direction/max(distanceToPlayer,.001),_Forward.xz);
    float clear=smoothstep(_Cone.y,_Cone.x,facing);
    clear=max(clear,1-smoothstep(_NearRadius*.7,_NearRadius,distanceToPlayer));
    [unroll] for(int k=0;k<6;k++)
    {
     if(k>=_LampCount)break;
     float d=distance(world.xz,_Lamps[k].xz);
     clear=max(clear,1-smoothstep(_Lamps[k].w*.55,_Lamps[k].w,d));
    }
    // Bright lantern cores and white world labels remain legible at the edge of the view.
    clear=max(clear,smoothstep(.58,.88,dot(original,float3(.2126,.7152,.0722))));
    float outside=(1-saturate(clear))*_Strength;
    fixed3 color=original;
    if(outside*_Cone.w>.001&&_BlurPixels>.001)
    {
     float2 step=_MainTex_TexelSize.xy*_BlurPixels;
     fixed3 soft=(original*2+tex2D(_MainTex,uv+float2(step.x,0)).rgb+tex2D(_MainTex,uv-float2(step.x,0)).rgb+tex2D(_MainTex,uv+float2(0,step.y)).rgb+tex2D(_MainTex,uv-float2(0,step.y)).rgb)/6;
     color=lerp(original,soft,outside*_Cone.w);
    }
    return fixed4(color*(1-outside*_Cone.z),1);
   }
   ENDCG
  }
 }
}
