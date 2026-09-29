/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-28 | 初回作成
 * ================================================ */

using UnityEditor;
using UnityEngine;

/// <summary>
/// マップの生成
/// </summary>
public class CS_MiniMapCaptureExporter : MonoBehaviour
{
    [MenuItem("Tools/Export MiniMap")]
    static void ExportMiniMap()
    {
        RenderTexture rt = Selection.activeObject as RenderTexture;
        if (rt == null)
        {
            Debug.LogError("RenderTexture を選択してください");
            return;
        }

        RenderTexture currentRT = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();

        RenderTexture.active = currentRT;

        byte[] bytes = tex.EncodeToPNG();
        System.IO.File.WriteAllBytes("Assets/Programmer/RenderingTexture/MiniMap.png", bytes);
    }
}
