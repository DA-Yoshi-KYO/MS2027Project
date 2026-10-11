/* ================================================
 * HDRP Toon - Shadow Matte をメインライト(Directional)だけにする
 * ------------------------------------------------
 * Tools > Toon > Shadow Matte: Directional Only
 *  スポット/ポイントライトの影は NTE_Toon.hlsl 側で滑らかにぼかして計算しているため、
 *  Shadow Matte (HDRP 標準の影の上塗り) からは外す。外さないと同じ影が
 *  ギザギザのまま二重にかかる。SHG_NTE_* を使う全マテリアルに適用。
 * ================================================ */
using UnityEditor;
using UnityEngine;

public static class ToonShadowMatteFilter
{
    const string GraphDir = "Assets/Programmer/Scene/Test/Toon/Shader/Graph/";
    const int LightFeatureDirectional = 16384;   // LIGHTFEATUREFLAGS_DIRECTIONAL

    [MenuItem("Tools/Toon/Shadow Matte: Directional Only")]
    static void Run()
    {
        float value = System.BitConverter.Int32BitsToSingle(LightFeatureDirectional);
        int count = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Material"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/")) continue;
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null || m.shader == null) continue;
            var sp = AssetDatabase.GetAssetPath(m.shader);
            if (!sp.StartsWith(GraphDir + "SHG_NTE_") || !m.HasProperty("_ShadowMatteFilter")) continue;
            Undo.RecordObject(m, "Shadow Matte filter");
            m.SetFloat("_ShadowMatteFilter", value);
            EditorUtility.SetDirty(m);
            count++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[ToonShadowMatteFilter] Directional only: " + count + " materials");
    }
}
