// Shared world-space flow keeps neighboring water meshes moving together.
float waterHash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
float waterNoise(float2 p)
{
    float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
    return lerp(lerp(waterHash(i),waterHash(i+float2(1,0)),f.x),lerp(waterHash(i+float2(0,1)),waterHash(i+1),f.x),f.y);
}
float flowPattern(float2 p, float time)
{
    float2 q=p-float2(time,0);
    return waterNoise(q*float2(.7,3.0))*.65+waterNoise(q*float2(1.8,6.0)+float2(time*.35,7))*.35;
}
