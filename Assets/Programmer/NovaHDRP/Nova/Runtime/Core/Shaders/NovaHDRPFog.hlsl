#ifndef NOVA_HDRP_FOG_INCLUDED
#define NOVA_HDRP_FOG_INCLUDED
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/AtmosphericScattering/AtmosphericScattering.hlsl"

float4 NovaApplyFog(float4 color, float4 positionCS)
{
    #ifndef NOVA_UI
    // Opaque fog is composited by HDRP after the opaque pass.
    if (_RenderType > 1.5)
    {
        PositionInputs posInput = GetPositionInput(positionCS.xy, _ScreenSize.zw,
            positionCS.z, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);
        float3 fogColor, fogOpacity;
        EvaluateAtmosphericScattering(posInput, GetWorldSpaceNormalizeViewDir(posInput.positionWS), fogColor, fogOpacity);
        if (_TransparentBlendMode < 0.5)
            color.rgb = color.rgb * (1.0 - fogOpacity) + fogColor;
        else if (_TransparentBlendMode < 1.5)
            color.rgb *= 1.0 - fogOpacity;
        else
            color.rgb = lerp(color.rgb, 1.0, fogOpacity);
    }
    #endif
    return color;
}
#endif
