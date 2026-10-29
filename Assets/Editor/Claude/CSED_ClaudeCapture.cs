using System.IO;
using UnityEditor;
using UnityEngine;

/*
 * シーンビュー・ゲームのカメラの映像をPNGに書き出すエディタ拡張(画づくりの確認用)
 * Tools/Claude/ の各メニューから実行する。Unity MCPのexecute_menu_itemからも呼べる
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・書き出し先は プロジェクト/Temp/ClaudeCapture/ (Tempはgitに入らない)
 * ・シーンビュー: 最後に使ったシーンビューのカメラで、1600×900に描き直して保存する(ウィンドウが隠れていても撮れる)
 * ・ゲームのカメラ: シーン内のClaudeShotCamera(撮影用。保存しない一時的なカメラ) → Camera.main → 有効なカメラの1つ目で描き直して保存する
 * ・HDRPでもCamera.Render()で描ける。描き終わったら元の出力先に戻す
 */
// ========================================

public static class CSED_ClaudeCapture
{
    private const int _width = 1600;
    private const int _height = 900;

    private static string folder => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", "ClaudeCapture");

    [MenuItem("Tools/Claude/Capture Scene View")]
    public static void CaptureSceneView()
    {
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null || view.camera == null)
        {
            Debug.LogWarning("CSED_ClaudeCapture: シーンビューがありません");
            return;
        }

        Save(view.camera, "scene.png");
    }

    [MenuItem("Tools/Claude/Capture Game Camera")]
    public static void CaptureGameCamera()
    {
        // 撮影用のカメラ(ClaudeShotCamera)があればそれを使う
        GameObject shot = GameObject.Find("ClaudeShotCamera");
        Camera camera = shot != null ? shot.GetComponent<Camera>() : Camera.main;
        if (camera == null)
        {
            foreach (Camera candidate in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (candidate.isActiveAndEnabled) { camera = candidate; break; }
            }
        }

        if (camera == null)
        {
            Debug.LogWarning("CSED_ClaudeCapture: カメラがありません");
            return;
        }

        Save(camera, "game.png");
    }

    private static void Save(Camera camera, string fileName)
    {
        RenderTexture texture = RenderTexture.GetTemporary(_width, _height, 24, RenderTextureFormat.ARGB32);
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;

        try
        {
            camera.targetTexture = texture;
            camera.Render();

            RenderTexture.active = texture;
            Texture2D image = new Texture2D(_width, _height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, _width, _height), 0, 0);
            image.Apply();

            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, fileName);
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            Debug.Log("CSED_ClaudeCapture: 保存しました " + path);
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(texture);
        }
    }
}
