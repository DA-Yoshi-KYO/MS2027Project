using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/*
+=====================================
 ファイル名 : CSE_MayaPreviewImporter
 概要       : Mayaから送られたモデルの自動読み込み
 作者       : ヨシモト リョウ
 履歴       : 2026/09/13 新規作成
=====================================+
*/

public static class CSE_MayaPreviewImporter
{
    // =========================================================
    // 定数
    // =========================================================

    // Mayaから書き出されるFBX
    private const string FBX_PATH =
        "Assets/Designer/MayaPreview/Incoming/MayaPreview_Test.fbx";

    // Mayaモデルを配置する親
    private const string MAYA_MODEL_ROOT_NAME =
        "Maya_Model_Root";

    // FBX更新確認間隔
    private const double CHECK_INTERVAL =
        0.2;


    // =========================================================
    // 変数
    // =========================================================

    // 最後に確認したFBX更新日時
    private static DateTime s_lastWriteTime;

    // 次回確認時間
    private static double s_nextCheckTime;

    // 初期化済みか
    private static bool s_initialized;

    // =========================================================
    // 初期化
    // =========================================================

    private static void InitializeWatcher()
    {
        string fullPath =
            GetFullFBXPath();

        // FBXが存在する場合
        if (File.Exists(fullPath))
        {
            s_lastWriteTime =
                File.GetLastWriteTimeUtc(
                    fullPath
                );
        }
        else
        {
            s_lastWriteTime =
                DateTime.MinValue;
        }

        s_initialized = true;

        Debug.Log(
            "Maya Preview : FBX監視開始"
        );
    }


    // =========================================================
    // Editor Update
    // =========================================================

    private static void Update()
    {
        // 初期化されていない
        if (!s_initialized)
        {
            InitializeWatcher();
        }

        // 現在時刻
        double currentTime =
            EditorApplication.timeSinceStartup;

        // まだ確認時間ではない
        if (currentTime < s_nextCheckTime)
        {
            return;
        }

        // 次回確認時間
        s_nextCheckTime =
            currentTime + CHECK_INTERVAL;

        // FBX更新確認
        CheckFBXUpdate();
    }


    // =========================================================
    // FBX更新確認
    // =========================================================

    private static void CheckFBXUpdate()
    {
        string fullPath =
            GetFullFBXPath();

        // ファイルが存在しない
        if (!File.Exists(fullPath))
        {
            return;
        }

        // 現在の更新日時
        DateTime currentWriteTime =
            File.GetLastWriteTimeUtc(
                fullPath
            );

        // 変更なし
        if (currentWriteTime == s_lastWriteTime)
        {
            return;
        }

        // 更新日時を保存
        s_lastWriteTime =
            currentWriteTime;

        Debug.Log(
            "Maya Preview : FBX更新検出"
        );

        // Unityへ強制Import
        ImportFBX();
    }


    // =========================================================
    // FBX Import
    // =========================================================

    private static void ImportFBX()
    {
        try
        {
            // Unityへ強制Import
            AssetDatabase.ImportAsset(
                FBX_PATH,
                ImportAssetOptions.ForceUpdate
            );

            Debug.Log(
                "Maya Preview : FBX自動Import成功"
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "Maya Preview : FBX自動Import失敗\n" +
                exception
            );
        }
    }


    // =========================================================
    // FBX絶対パス取得
    // =========================================================

    private static string GetFullFBXPath()
    {
        // UnityプロジェクトRoot
        string projectRoot =
            Directory
                .GetParent(
                    Application.dataPath
                )
                .FullName;

        // Assetsから始まるUnityパスを
        // OS上の絶対パスへ変換
        string relativePath =
            FBX_PATH.Replace(
                "/",
                Path.DirectorySeparatorChar.ToString()
            );

        return Path.Combine(
            projectRoot,
            relativePath
        );
    }


    // =========================================================
    // 手動モデル配置
    // =========================================================

    [MenuItem("Tools/Maya Preview/Import Test Model")]
    private static void ImportTestModel()
    {
        // Maya_Model_Root取得
        GameObject mayaModelRoot =
            GameObject.Find(
                MAYA_MODEL_ROOT_NAME
            );

        if (mayaModelRoot == null)
        {
            Debug.LogError(
                MAYA_MODEL_ROOT_NAME +
                " が見つかりません。"
            );

            return;
        }

        // FBX取得
        GameObject modelAsset =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                FBX_PATH
            );

        if (modelAsset == null)
        {
            Debug.LogError(
                "Maya Preview FBX が見つかりません。\n" +
                FBX_PATH
            );

            return;
        }

        // シーンへ生成
        GameObject instance =
            PrefabUtility.InstantiatePrefab(
                modelAsset,
                mayaModelRoot.transform
            ) as GameObject;

        if (instance == null)
        {
            Debug.LogError(
                "Maya Previewモデルの生成に失敗しました。"
            );

            return;
        }

        // Transform初期化
        instance.transform.localPosition =
            Vector3.zero;

        instance.transform.localRotation =
            Quaternion.identity;

        instance.transform.localScale =
            Vector3.one;

        Debug.Log(
            "Maya Preview : モデル配置成功"
        );
    }
}