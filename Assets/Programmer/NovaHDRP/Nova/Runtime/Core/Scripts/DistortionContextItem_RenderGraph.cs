// Legacy URP renderer feature; HDRP uses the native DistortionVectors pass.
#if NOVA_LEGACY_URP
// --------------------------------------------------------------
// Copyright 2025 CyberAgent, Inc.
// --------------------------------------------------------------

#if UNITY_2023_3_OR_NEWER
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Nova.Runtime.Core.Scripts
{
    public class DistortionContextItem : ContextItem
    {
        public TextureHandle DistortedUvTexture;

        public override void Reset()
        {
            DistortedUvTexture = TextureHandle.nullHandle;
        }
    }
}
#endif

#endif
