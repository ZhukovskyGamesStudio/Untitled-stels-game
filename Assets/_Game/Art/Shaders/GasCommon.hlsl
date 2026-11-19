// The gas outside the house as a field in the world (set by Stealth.Level.GasView; the same formula is in GasView.Density):
// none at the outer walls, full _GasParams.y metres out, full up to _GasParams.z above the ground, thinning above that.
#ifndef STEALTH_GAS_COMMON
#define STEALTH_GAS_COMMON

float _GasOn;
float4 _GasBox;       // xy centre of the house (x, z), zw half size
float4 _GasParams;    // x density per metre, y clear distance, z full layer height, w falloff height
float4 _GasColor;     // rgb colour of the gas, a ground height
float4 _GasSkyParams; // x sky exposure (0 = no sky), y camera inside the house (windows show the outside), z how far the sky is
TEXTURECUBE(_GasSky);
SAMPLER(sampler_GasSky);

float GasDensity(float3 p)
{
    float2 q = max(abs(p.xz - _GasBox.xy) - _GasBox.zw, 0.0);
    float ramp = saturate(length(q) / max(_GasParams.y, 0.01));
    float above = max(p.y - _GasColor.a - _GasParams.z, 0.0);
    float street = step(_GasColor.a - 1.5, p.y); // none underground (LevelLayout.Underground): the basement's tunnels, the sewer
    return _GasParams.x * ramp * street * exp(-above / max(_GasParams.w, 0.01));
}

// Optical depth along a ray of length len: steps grow with the distance (fine near the eye), jittered per pixel.
float GasDepth(float3 origin, float3 dir, float len, float jitter)
{
    const int N = 32;
    float tau = 0.0;
    float t0 = 0.0;
    [loop] for (int i = 1; i <= N; i++)
    {
        float a = (float)i / N;
        float t1 = len * a * a;
        tau += GasDensity(origin + dir * lerp(t0, t1, jitter)) * (t1 - t0);
        t0 = t1;
        if (tau > 8.0) break;
    }
    return tau;
}

float GasSkyDistance() { return _GasSkyParams.z > 1.0 ? _GasSkyParams.z : 200.0; }

float3 GasSky(float3 dir)
{
    return SAMPLE_TEXTURECUBE_LOD(_GasSky, sampler_GasSky, dir, 0).rgb * _GasSkyParams.x;
}

#endif
