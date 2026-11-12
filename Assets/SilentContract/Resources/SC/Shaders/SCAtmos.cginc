#ifndef SC_ATMOS_INCLUDED
#define SC_ATMOS_INCLUDED
// Silent Contract — the shared haze model of SC/Sky and the post haze (Hidden/SC/PostFX): the colour the air has along a
// direction — golden towards a low sun with a bright forward glow round it, the sky's own tone away from it — so far
// objects melt into the horizon the way the sky meets the sea. Globals set by Env.cs.
float4 _SC_SunSkyDir;              // xyz: the sun as the sky shows it (world, unit)
float4 _SC_SunGlowColor;           // HDR linear colour of the sun glow
float4 _SC_HazeAway, _SC_HazeSun;  // haze colour looking away from / straight at the sun (linear)
float4 _SC_HazeShape;              // x: sun power of the colour blend, y: forward glow power, z: forward glow amount

float3 SC_HazeColor(float3 d)
{
    float mu = dot(d, _SC_SunSkyDir.xyz);
    float k = pow(saturate(mu * 0.5 + 0.5), max(_SC_HazeShape.x, 0.01));
    float3 c = lerp(_SC_HazeAway.rgb, _SC_HazeSun.rgb, k);
    c += _SC_SunGlowColor.rgb * _SC_HazeShape.z * pow(saturate(mu), max(_SC_HazeShape.y, 1.0));
    return c;
}
#endif
