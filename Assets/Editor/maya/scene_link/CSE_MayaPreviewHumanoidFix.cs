using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/*
+=====================================
 File   : CSE_MayaPreviewHumanoidFix
 Summary: Humanoid rigs of the Maya preview exports (Assets/Designer/Mesh/...): Unity's automatic
          bone mapping sometimes puts hair / skirt / cloth joints into humanoid slots (Giro: Jaw ->
          Jnt_C_Hair2), which makes the mouth animation move the hair. After such a model is
          imported as Humanoid, those slots are emptied and the model is imported again:
            - any slot mapped to a joint whose name says hair / skirt / cloth / ribbon / tail /
              acc / weapon / cape / breast / bust
            - Jaw: only kept on a joint whose name says jaw / chin
            - LeftEye / RightEye: only kept on a joint whose name says eye
          Mappings set by hand in "Configure..." that pass these rules are left as they are.
 History: 2026/11/27 New
=====================================+
*/
public class CSE_MayaPreviewHumanoidFix : AssetPostprocessor
{
    private const string Root = "Assets/Designer/";

    private static readonly string[] NotBody =
        { "hair", "skirt", "cloth", "ribbon", "tail", "acc", "weapon", "cape", "breast", "bust" };

    private static readonly HashSet<string> Pending = new HashSet<string>();

    private void OnPostprocessModel(GameObject model)
    {
        if (!assetPath.StartsWith(Root, StringComparison.OrdinalIgnoreCase)) return;
        var importer = assetImporter as ModelImporter;
        if (importer == null || importer.animationType != ModelImporterAnimationType.Human) return;
        // The automatic mapping is stored in the importer once this import finishes: fix it right after.
        string path = assetPath;
        if (Pending.Add(path)) EditorApplication.delayCall += () => Fix(path);
    }

    public static void Fix(string path)
    {
        Pending.Remove(path);
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null || importer.animationType != ModelImporterAnimationType.Human) return;
        HumanDescription description = importer.humanDescription;
        if (description.human == null || description.human.Length == 0) return;

        var kept = new List<HumanBone>();
        var removed = new List<string>();
        foreach (HumanBone bone in description.human)
        {
            if (Wrong(bone)) removed.Add(bone.humanName + " (" + bone.boneName + ")");
            else kept.Add(bone);
        }
        if (removed.Count == 0) return;

        description.human = kept.ToArray();
        importer.humanDescription = description;
        importer.SaveAndReimport();
        Debug.Log("Maya Preview Import : Humanoid の割り当てを外しました " + path + "\n  " + string.Join("\n  ", removed));
    }

    private static bool Wrong(HumanBone bone)
    {
        string joint = (bone.boneName ?? "").ToLowerInvariant();
        foreach (string word in NotBody)
            if (joint.Contains(word)) return true;
        if (bone.humanName == "Jaw") return !(joint.Contains("jaw") || joint.Contains("chin"));
        if (bone.humanName == "LeftEye" || bone.humanName == "RightEye") return !joint.Contains("eye");
        return false;
    }
}
