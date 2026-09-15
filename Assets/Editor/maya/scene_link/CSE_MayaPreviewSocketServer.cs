using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

/*
+=====================================
 ファイル名 : CSE_MayaPreviewSocketServer
 概要       : Maya Live Sync用TCP通信サーバー
 作者       : ヨシモト リョウ
 履歴       : 2026/09/14 新規作成
=====================================+
*/

[InitializeOnLoad]
public static class CSE_MayaPreviewSocketServer
{
    // =========================================================
    // 定数
    // =========================================================

    private const int PORT = 50001;

    // Maya側
    // 1 unit = 1cm
    //
    // Unity側
    // 1 unit = 1m
    //
    // 1cm → 0.01m
    private const float MAYA_TO_UNITY_SCALE = 0.01f;

    // Mayaから送られたモデルを入れる親
    private const string MAYA_MODEL_ROOT_NAME =
        "Maya_Model_Root";

    // Live Previewオブジェクト名のPrefix
    private const string LIVE_OBJECT_PREFIX =
        "MayaLive_";


    // =========================================================
    // JSONデータ
    // =========================================================

    [Serializable]
    private class CSST_VertexData
    {
        public float x;
        public float y;
        public float z;
    }


    [Serializable]
    private class CSST_MeshData
    {
        public string type;

        public string name;

        public int vertex_count;

        public int triangle_index_count;

        public CSST_VertexData[] vertices;

        public int[] triangles;
    }


    // =========================================================
    // Socket
    // =========================================================

    private static TcpListener s_listener;

    private static Thread s_serverThread;

    private static bool s_isRunning;


    // =========================================================
    // Thread → Unity MainThread
    // =========================================================

    private static readonly ConcurrentQueue<string>
        s_receivedMessages =
            new ConcurrentQueue<string>();


    // =========================================================
    // 初期化
    // =========================================================

    static CSE_MayaPreviewSocketServer()
    {
        // Unity MainThread
        EditorApplication.update += Update;

        // Script Reload時にSocket停止
        AssemblyReloadEvents.beforeAssemblyReload +=
            StopServer;
    }


    // =========================================================
    // Server開始
    // =========================================================

    [MenuItem("Tools/Maya Preview/Socket/Start Server")]
    private static void StartServer()
    {
        // すでに起動中
        if (s_isRunning)
        {
            Debug.LogWarning(
                "Maya Preview Socket : Server はすでに起動しています。"
            );

            return;
        }

        try
        {
            // localhostのみ
            s_listener =
                new TcpListener(
                    IPAddress.Loopback,
                    PORT
                );

            s_listener.Start();

            s_isRunning = true;

            // 通信用Thread
            s_serverThread =
                new Thread(
                    ServerLoop
                );

            s_serverThread.IsBackground =
                true;

            s_serverThread.Start();

            Debug.Log(
                "Maya Preview Socket : Server開始 127.0.0.1:"
                + PORT
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "Maya Preview Socket : Server開始失敗\n"
                + exception
            );

            StopServer();
        }
    }


    // =========================================================
    // Server停止
    // =========================================================

    [MenuItem("Tools/Maya Preview/Socket/Stop Server")]
    private static void StopServer()
    {
        s_isRunning = false;

        try
        {
            s_listener?.Stop();
        }
        catch
        {
            // 終了時なので無視
        }

        s_listener = null;

        s_serverThread = null;

        Debug.Log(
            "Maya Preview Socket : Server停止"
        );
    }


    // =========================================================
    // Socket Thread
    // =========================================================

    private static void ServerLoop()
    {
        while (s_isRunning)
        {
            try
            {
                // Mayaから接続
                using TcpClient client =
                    s_listener.AcceptTcpClient();

                using NetworkStream stream =
                    client.GetStream();

                using StreamReader reader =
                    new StreamReader(
                        stream,
                        Encoding.UTF8
                    );

                // 1メッセージ受信
                string message =
                    reader.ReadLine();

                if (!string.IsNullOrEmpty(message))
                {
                    s_receivedMessages.Enqueue(
                        message
                    );
                }
            }
            catch (SocketException)
            {
                if (!s_isRunning)
                {
                    return;
                }
            }
            catch (Exception exception)
            {
                if (s_isRunning)
                {
                    s_receivedMessages.Enqueue(
                        "ERROR:"
                        + exception.Message
                    );
                }
            }
        }
    }


    // =========================================================
    // Unity MainThread
    // =========================================================

    private static void Update()
    {
        while (
            s_receivedMessages.TryDequeue(
                out string message
            )
        )
        {
            // Socket Error
            if (message.StartsWith("ERROR:"))
            {
                Debug.LogError(
                    "Maya Preview Socket : "
                    + message
                );

                continue;
            }

            // データ処理
            ProcessMessage(
                message
            );
        }
    }


    // =========================================================
    // Message処理
    // =========================================================

    private static void ProcessMessage(
        string message
    )
    {
        // JSONではない場合
        if (!message.StartsWith("{"))
        {
            Debug.Log(
                "Mayaから受信 : "
                + message
            );

            return;
        }

        try
        {
            // JSON解析
            CSST_MeshData meshData =
                JsonUtility.FromJson<CSST_MeshData>(
                    message
                );

            if (meshData == null)
            {
                Debug.LogError(
                    "Maya Preview Socket : JSON解析失敗"
                );

                return;
            }

            // Mesh Data
            if (meshData.type == "MESH_DATA")
            {
                ProcessMeshData(
                    meshData
                );

                return;
            }

            Debug.LogWarning(
                "Maya Preview Socket : 未対応Type : "
                + meshData.type
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "Maya Preview Socket : JSON処理失敗\n"
                + exception
            );
        }
    }


    // =========================================================
    // Mesh Data処理
    // =========================================================

    private static void ProcessMeshData(
        CSST_MeshData meshData
    )
    {
        // =====================================================
        // データ確認
        // =====================================================

        if (
            meshData.vertices == null
            || meshData.vertices.Length == 0
        )
        {
            Debug.LogError(
                "Maya Preview Socket : Vertexがありません。"
            );

            return;
        }


        if (
            meshData.triangles == null
            || meshData.triangles.Length == 0
        )
        {
            Debug.LogError(
                "Maya Preview Socket : Triangleがありません。"
            );

            return;
        }


        // =====================================================
        // Maya Vertex → Unity Vertex
        // =====================================================

        Vector3[] vertices =
            new Vector3[
                meshData.vertices.Length
            ];

        for (
            int i = 0;
            i < meshData.vertices.Length;
            i++
        )
        {
            CSST_VertexData vertex =
                meshData.vertices[i];

            /*
             * Maya
             * X = Right
             * Y = Up
             * Z方向がUnityと異なる
             *
             * Maya(cm)
             * ↓
             * Unity(m)
             */

            vertices[i] =
                new Vector3(
                    vertex.x,
                    vertex.y,
                    -vertex.z
                )
                * MAYA_TO_UNITY_SCALE;
        }


        // =====================================================
        // Triangle変換
        // =====================================================

        int[] triangles =
            new int[
                meshData.triangles.Length
            ];

        /*
         * Z軸を反転したため、
         * Polygonの表裏も反転する。
         *
         * そのためTriangleの
         * 1番目と2番目を逆転させる。
         */

        for (
            int i = 0;
            i < meshData.triangles.Length;
            i += 3
        )
        {
            if (
                i + 2
                >= meshData.triangles.Length
            )
            {
                break;
            }

            triangles[i] =
                meshData.triangles[i];

            triangles[i + 1] =
                meshData.triangles[i + 2];

            triangles[i + 2] =
                meshData.triangles[i + 1];
        }


        // =====================================================
        // Unity Mesh生成
        // =====================================================

        CreateOrUpdateMesh(
            meshData.name,
            vertices,
            triangles
        );


        // =====================================================
        // Log
        // =====================================================

        Debug.Log(
            "Maya Live Mesh更新成功"
            + "\nObject : "
            + meshData.name
            + "\nVertex Count : "
            + vertices.Length
            + "\nTriangle Count : "
            + triangles.Length / 3
        );
    }


    // =========================================================
    // Mesh生成・更新
    // =========================================================

    private static void CreateOrUpdateMesh(
        string mayaObjectName,
        Vector3[] vertices,
        int[] triangles
    )
    {
        // =====================================================
        // Maya_Model_Root取得
        // =====================================================

        GameObject mayaModelRoot =
            GameObject.Find(
                MAYA_MODEL_ROOT_NAME
            );

        if (mayaModelRoot == null)
        {
            Debug.LogError(
                MAYA_MODEL_ROOT_NAME
                + " が見つかりません。"
            );

            return;
        }


        // =====================================================
        // Live Object検索
        // =====================================================

        string liveObjectName =
            LIVE_OBJECT_PREFIX
            + mayaObjectName;

        Transform liveTransform =
            mayaModelRoot.transform.Find(
                liveObjectName
            );


        GameObject liveObject;


        // =====================================================
        // 無ければ作成
        // =====================================================

        if (liveTransform == null)
        {
            liveObject =
                new GameObject(
                    liveObjectName
                );

            liveObject.transform.SetParent(
                mayaModelRoot.transform
            );

            liveObject.transform.localPosition =
                Vector3.zero;

            liveObject.transform.localRotation =
                Quaternion.identity;

            liveObject.transform.localScale =
                Vector3.one;


            // MeshFilter
            liveObject.AddComponent<MeshFilter>();

            // MeshRenderer
            MeshRenderer meshRenderer =
                liveObject.AddComponent<MeshRenderer>();


            // =================================================
            // HDRP Lit Material
            // =================================================

            Shader hdrpLitShader =
                Shader.Find(
                    "HDRP/Lit"
                );

            if (hdrpLitShader != null)
            {
                Material material =
                    new Material(
                        hdrpLitShader
                    );

                material.name =
                    "MayaPreview_HDRP_Material";

                // 少し明るめのグレー
                material.SetColor(
                    "_BaseColor",
                    new Color(
                        0.7f,
                        0.7f,
                        0.7f,
                        1.0f
                    )
                );

                meshRenderer.sharedMaterial =
                    material;
            }
            else
            {
                Debug.LogWarning(
                    "HDRP/Lit Shader が見つかりません。"
                );
            }
        }
        else
        {
            liveObject =
                liveTransform.gameObject;
        }


        // =====================================================
        // MeshFilter取得
        // =====================================================

        MeshFilter meshFilter =
            liveObject.GetComponent<MeshFilter>();

        if (meshFilter == null)
        {
            meshFilter =
                liveObject.AddComponent<MeshFilter>();
        }


        // =====================================================
        // 古いPreview Mesh削除
        // =====================================================

        Mesh oldMesh =
            meshFilter.sharedMesh;

        if (
            oldMesh != null
            && !AssetDatabase.Contains(
                oldMesh
            )
        )
        {
            UnityEngine.Object.DestroyImmediate(
                oldMesh
            );
        }


        // =====================================================
        // 新しいMesh作成
        // =====================================================

        Mesh mesh =
            new Mesh();

        mesh.name =
            "MayaLiveMesh_"
            + mayaObjectName;


        // Vertex
        mesh.vertices =
            vertices;


        // Triangle
        mesh.triangles =
            triangles;


        // Normal自動生成
        mesh.RecalculateNormals();


        // Bounds自動生成
        mesh.RecalculateBounds();


        // Mesh設定
        meshFilter.sharedMesh =
            mesh;


        // =====================================================
        // Scene再描画
        // =====================================================

        SceneView.RepaintAll();
    }
}