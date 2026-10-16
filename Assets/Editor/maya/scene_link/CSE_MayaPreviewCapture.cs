using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System.IO;

/*
+=====================================
 ファイル名 : CSE_MayaPreviewCapture
 概要       : Unity HDRP Preview映像の取得・自動更新
 作者       : ヨシモト リョウ
 履歴       : 2026/09/13 新規作成
=====================================+
*/

public static class CSE_MayaPreviewCapture
{
    // =========================================================
    // 定数
    // =========================================================

    // Preview更新間隔
    private const double CAPTURE_INTERVAL = 0.2;

    // RenderTexture名
    private const string RENDER_TEXTURE_NAME =
        "RT_MayaPreview";

    // Preview Camera名
    private const string CAMERA_NAME =
        "Main Camera";

    // 出力PNG名
    private const string OUTPUT_FILE_NAME =
        "MayaPreview_Test.png";


    // =========================================================
    // 変数
    // =========================================================

    // Preview用RenderTexture
    private static RenderTexture s_renderTexture;

    // Preview用Camera
    private static Camera s_previewCamera;

    // CPU読み取り用Texture
    private static Texture2D s_readbackTexture;

    // 次回Capture時間
    private static double s_nextCaptureTime;

    // 自動Capture中か
    private static bool s_isRunning;


    // =========================================================
    // 手動Capture
    // =========================================================

    [MenuItem("Tools/Maya Preview/Capture Test")]
    private static void CaptureTest()
    {
        if (!Setup())
        {
            return;
        }

        Capture();

        Debug.Log(
            "Maya Preview : 手動キャプチャ完了"
        );
    }


    // =========================================================
    // 自動Capture開始
    // =========================================================

    [MenuItem("Tools/Maya Preview/Start Auto Capture")]
    private static void StartAutoCapture()
    {
        if (s_isRunning)
        {
            Debug.LogWarning(
                "Maya Preview : Auto Capture はすでに実行中です。"
            );

            return;
        }

        if (!Setup())
        {
            return;
        }

        s_isRunning = true;

        // 即時実行
        s_nextCaptureTime = 0.0;

        EditorApplication.update -= AutoCaptureUpdate;
        EditorApplication.update += AutoCaptureUpdate;

        Debug.Log(
            "Maya Preview : Auto Capture 開始"
        );
    }


    // =========================================================
    // 自動Capture停止
    // =========================================================

    [MenuItem("Tools/Maya Preview/Stop Auto Capture")]
    private static void StopAutoCapture()
    {
        EditorApplication.update -= AutoCaptureUpdate;

        s_isRunning = false;

        ReleaseReadbackTexture();

        Debug.Log(
            "Maya Preview : Auto Capture 停止"
        );
    }


    // =========================================================
    // Editor Update
    // =========================================================

    private static void AutoCaptureUpdate()
    {
        if (!s_isRunning)
        {
            return;
        }

        double currentTime =
            EditorApplication.timeSinceStartup;

        if (currentTime < s_nextCaptureTime)
        {
            return;
        }

        s_nextCaptureTime =
            currentTime + CAPTURE_INTERVAL;

        Capture();
    }


    // =========================================================
    // 初期設定
    // =========================================================

    private static bool Setup()
    {
        // -----------------------------------------
        // RenderTexture取得
        // -----------------------------------------

        if (s_renderTexture == null)
        {
            string[] guids =
                AssetDatabase.FindAssets(
                    RENDER_TEXTURE_NAME +
                    " t:RenderTexture"
                );

            if (guids.Length == 0)
            {
                Debug.LogError(
                    RENDER_TEXTURE_NAME +
                    " が見つかりません。"
                );

                return false;
            }

            string assetPath =
                AssetDatabase.GUIDToAssetPath(
                    guids[0]
                );

            s_renderTexture =
                AssetDatabase.LoadAssetAtPath<RenderTexture>(
                    assetPath
                );
        }

        if (s_renderTexture == null)
        {
            Debug.LogError(
                "RenderTexture の取得に失敗しました。"
            );

            return false;
        }

        if (!s_renderTexture.IsCreated())
        {
            s_renderTexture.Create();
        }


        // -----------------------------------------
        // Camera取得
        // -----------------------------------------

        if (s_previewCamera == null)
        {
            GameObject cameraObject =
                GameObject.Find(
                    CAMERA_NAME
                );

            if (cameraObject == null)
            {
                Debug.LogError(
                    CAMERA_NAME +
                    " が見つかりません。"
                );

                return false;
            }

            s_previewCamera =
                cameraObject.GetComponent<Camera>();
        }

        if (s_previewCamera == null)
        {
            Debug.LogError(
                "Preview Camera の取得に失敗しました。"
            );

            return false;
        }


        // -----------------------------------------
        // Texture2D準備
        // -----------------------------------------

        SetupReadbackTexture();

        Debug.Log(
            "Maya Preview : Setup成功 (" +
            s_renderTexture.width +
            " x " +
            s_renderTexture.height +
            ")"
        );

        return true;
    }


    // =========================================================
    // Texture2D準備
    // =========================================================

    private static void SetupReadbackTexture()
    {
        if (s_renderTexture == null)
        {
            return;
        }

        if (
            s_readbackTexture != null &&
            s_readbackTexture.width ==
                s_renderTexture.width &&
            s_readbackTexture.height ==
                s_renderTexture.height
        )
        {
            return;
        }

        ReleaseReadbackTexture();

        s_readbackTexture =
            new Texture2D(
                s_renderTexture.width,
                s_renderTexture.height,
                TextureFormat.RGBA32,
                false
            );
    }


    // =========================================================
    // Capture
    // =========================================================

    private static void Capture()
    {
        if (s_renderTexture == null)
        {
            return;
        }

        if (s_previewCamera == null)
        {
            return;
        }

        if (s_readbackTexture == null)
        {
            SetupReadbackTexture();
        }

        // =====================================================
        // 重要
        //
        // Unity Editorが非フォーカスでも
        // HDRPへ明示的にRender要求を出す
        // =====================================================

        if (!RenderHDRP())
        {
            return;
        }

        RenderTexture previousRenderTexture =
            RenderTexture.active;

        try
        {
            // -----------------------------------------
            // RenderTextureをCPU側へ読み込み
            // -----------------------------------------

            RenderTexture.active =
                s_renderTexture;

            s_readbackTexture.ReadPixels(
                new Rect(
                    0,
                    0,
                    s_renderTexture.width,
                    s_renderTexture.height
                ),
                0,
                0,
                false
            );

            s_readbackTexture.Apply(
                false,
                false
            );

            // -----------------------------------------
            // PNG変換
            // -----------------------------------------

            byte[] pngData =
                s_readbackTexture.EncodeToPNG();

            // -----------------------------------------
            // 保存
            // -----------------------------------------

            string outputPath =
                GetOutputPath();

            File.WriteAllBytes(
                outputPath,
                pngData
            );
        }
        catch (System.Exception exception)
        {
            Debug.LogError(
                "Maya Preview : Capture失敗\n" +
                exception
            );
        }
        finally
        {
            RenderTexture.active =
                previousRenderTexture;
        }
    }


    // =========================================================
    // HDRP強制Render
    // =========================================================

    private static bool RenderHDRP()
    {
        try
        {
            // HDRPへ送る標準Render Request
            RenderPipeline.StandardRequest request =
                new RenderPipeline.StandardRequest();

            // Render結果の出力先
            request.destination =
                s_renderTexture;

            // 現在のRenderPipelineが対応しているか確認
            if (
                !RenderPipeline.SupportsRenderRequest(
                    s_previewCamera,
                    request
                )
            )
            {
                Debug.LogError(
                    "現在のRenderPipelineは " +
                    "StandardRequest に対応していません。"
                );

                return false;
            }

            // HDRPへ明示的に描画要求
            RenderPipeline.SubmitRenderRequest(
                s_previewCamera,
                request
            );

            return true;
        }
        catch (System.Exception exception)
        {
            Debug.LogError(
                "Maya Preview : HDRP Render失敗\n" +
                exception
            );

            return false;
        }
    }


    // =========================================================
    // PNG出力先
    // =========================================================

    private static string GetOutputPath()
    {
        string projectRoot =
            Directory
                .GetParent(
                    Application.dataPath
                )
                .FullName;

        string outputDirectory =
            Path.Combine(
                projectRoot,
                "Temp",
                "MayaPreview"
            );

        Directory.CreateDirectory(
            outputDirectory
        );

        return Path.Combine(
            outputDirectory,
            OUTPUT_FILE_NAME
        );
    }


    // =========================================================
    // Texture解放
    // =========================================================

    private static void ReleaseReadbackTexture()
    {
        if (s_readbackTexture == null)
        {
            return;
        }

        Object.DestroyImmediate(
            s_readbackTexture
        );

        s_readbackTexture = null;
    }
}