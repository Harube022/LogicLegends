#ifndef OPEN_ARCHIVE_STABLE_LIGHTING
#define OPEN_ARCHIVE_STABLE_LIGHTING

// Suppress subpixel relief before it can turn into crawling, high-contrast normals.
float3 ArchiveFineNormal(float3 world,float3 normal,float height,float texelFootprint)
{
    float3 dx=ddx(world),dy=ddy(world);
    float3 a=cross(dy,normal),b=cross(normal,dx);
    float det=dot(dx,a);
    float3 gradient=sign(det)*(ddx(height)*a+ddy(height)*b)/max(abs(det),1e-8);
    gradient*=rsqrt(max(1,dot(gradient,gradient)/.16));
    gradient*=1-smoothstep(1.25,7,texelFootprint);
    return normalize(normal-gradient);
}

Light ArchiveMainLight(float3 world)
{
    // This overload includes URP's distance fade; the one-argument overload cuts off abruptly.
    return GetMainLight(TransformWorldToShadowCoord(world),world,half4(1,1,1,1));
}
// Match URP's Forward and Forward+ light iteration. Forward+ uses screen-space
// clusters instead of GetAdditionalLightsCount (which intentionally returns zero).
half3 ArchiveAdditionalDiffuse(float3 world,half3 normal,float4 screenPosition)
{
    half3 illumination=0;
    #if defined(_ADDITIONAL_LIGHTS)
    InputData inputData=(InputData)0;
    inputData.positionWS=world;
    inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(screenPosition);
    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for(uint lightIndex=0u;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);++lightIndex)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light=GetAdditionalLight(lightIndex,world,half4(1,1,1,1));
        illumination+=light.color*(saturate(dot(normal,light.direction))*light.distanceAttenuation*light.shadowAttenuation);
    }
    #endif
    uint count=GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(count)
        Light light=GetAdditionalLight(lightIndex,world,half4(1,1,1,1));
        illumination+=light.color*(saturate(dot(normal,light.direction))*light.distanceAttenuation*light.shadowAttenuation);
    LIGHT_LOOP_END
    #endif
    return illumination;
}
#endif
