using UnityEditor;
using UnityEngine;

// Import settings for OwlAssistAI.fbx (exported from Blender by anim_owl.py).
// Put this file in any "Editor" folder; it runs automatically when the FBX is (re)imported.
public class OwlAssistAIImport : AssetPostprocessor
{
    bool IsOwl => assetPath.EndsWith("OwlAssistAI.fbx");

    void OnPreprocessModel()
    {
        if (!IsOwl) return;
        var mi = (ModelImporter)assetImporter;
        mi.useFileScale = true;                  // 1 Blender unit = 1 m
        mi.globalScale = 1f;
        mi.bakeAxisConversion = true;            // bake Blender Z-up -> Unity Y-up (no -90 X on the root)
        mi.animationType = ModelImporterAnimationType.Generic;
        mi.importAnimation = true;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
    }

    void OnPreprocessAnimation()
    {
        if (!IsOwl) return;
        var mi = (ModelImporter)assetImporter;
        var clips = mi.defaultClipAnimations;
        foreach (var c in clips)
        {
            c.name = "Fly_Loop";
            c.loopTime = true;                   // frame 30 == frame 0, so the loop is seamless
        }
        mi.clipAnimations = clips;
    }
}
