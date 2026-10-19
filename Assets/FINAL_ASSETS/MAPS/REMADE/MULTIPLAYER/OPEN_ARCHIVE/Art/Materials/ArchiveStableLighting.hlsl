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
#endif
