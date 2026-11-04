#ifndef NOVA_PARTICLESUBERUNLITFORWARD_INCLUDED
#define NOVA_PARTICLESUBERUNLITFORWARD_INCLUDED
#include "NovaHDRPCommon.hlsl"
#include "ParticlesUberUnlit.hlsl"
#include "NovaHDRPFog.hlsl"
Varyings vert(Attributes input)
{
    float3 positionWS;
    return vertUnlit(input, positionWS, true, false);
}
half4 frag(Varyings input) : SV_Target
{
    half4 color = fragUnlit(input, true);
    #if !defined(NOVA_UI) && !defined(_ALPHAMODULATE_ENABLED)
    color.rgb *= GetCurrentExposureMultiplier();
    #endif
    return NovaApplyFog(color, input.positionHCS);
}
#endif
