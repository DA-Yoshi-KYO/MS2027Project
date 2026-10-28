using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using UnityEngine;
using UnityEngine.Rendering;


/*
+=====================================
 ファイル名 : CS_MayaPreviewRuntimeServer
 概要       : MayaからMeshデータを受信し、
              Runtime上でMeshを生成・更新する
 作者       : ヨシモト リョウ
 履歴       : 2026/09/14 新規作成
=====================================+
*/

public class CS_MayaPreviewRuntimeServer : MonoBehaviour
{
    // =========================================================
    // 定数
    // =========================================================

    // Maya → Unity
    private const int PORT = 50001;

    // Mayaはcm、Unityはm
    private const float MAYA_TO_UNITY_SCALE = 1.0f;

    private const string MAYA_MODEL_ROOT_NAME =
        "Maya_Model_Root";

    private const string LIVE_OBJECT_PREFIX =
        "MayaLive_";


    // =========================================================
    // Mesh受信用データCS_MayaPreviewFrameSender.cs だけ全文これにしてください。
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

    private TcpListener _listener;

    private Thread _serverThread;
    private TcpClient _activeClient;

    private volatile bool _serverRunning;


    // =========================================================
    // MainThread処理Queue
    // =========================================================

    private readonly ConcurrentQueue<CSST_ReceivedMessage> _messageQueue =
        new ConcurrentQueue<CSST_ReceivedMessage>();


    // =========================================================
    // 受信スレッドで変換済みのデータ
    //
    // JSON解析・座標変換・頂点分割は重いので
    // MainThreadではなく受信スレッドで済ませておく
    // (MainThreadが止まらない = 画面がカクつかない)
    // =========================================================

    private class CSST_ReceivedMessage
    {
        public string text;          // JSON以外の接続確認用Message
        public string meshName;      // Mesh更新の場合のみ
        public Vector3[] vertices;
        public int[] triangles;
    }


    // =========================================================
    // Unity Object
    // =========================================================

    private GameObject _mayaModelRoot;

    private Material _previewMaterial;


    // HDRP Frame送信
    private CS_MayaPreviewFrameSender _frameSender;


    // =========================================================
    // Awake
    // =========================================================

    private void Awake()
    {
        // Standaloneでもバックグラウンド動作
        Application.runInBackground = true;

        // 操作していない間は描画回数を落として PC を軽くする (見た目は変わらない)
        if (GetComponent<CS_MayaPreviewIdleThrottle>() == null)
        {
            gameObject.AddComponent<CS_MayaPreviewIdleThrottle>();
        }


        // =====================================================
        // Maya Model Root取得
        // =====================================================

        _mayaModelRoot =
            GameObject.Find(
                MAYA_MODEL_ROOT_NAME
            );


        // 万が一無ければ生成
        if (_mayaModelRoot == null)
        {
            _mayaModelRoot =
                new GameObject(
                    MAYA_MODEL_ROOT_NAME
                );


            Debug.LogWarning(
                "Maya Preview Runtime : " +
                MAYA_MODEL_ROOT_NAME +
                " が無かったため生成しました。"
            );
        }


        // =====================================================
        // FrameSender取得
        // =====================================================

        _frameSender =
            GetComponent<CS_MayaPreviewFrameSender>();


        if (_frameSender == null)
        {
            Debug.LogError(
                "Maya Preview Runtime : " +
                "CS_MayaPreviewFrameSender が見つかりません。"
            );
        }


        // =====================================================
        // Preview Material
        // =====================================================

        Shader shader =
            Shader.Find(
                "HDRP/Lit"
            );


        if (shader != null)
        {
            _previewMaterial =
                new Material(
                    shader
                );


            _previewMaterial.name =
                "MAT_MayaPreview_Runtime";


            if (
                _previewMaterial.HasProperty(
                    "_BaseColor"
                )
            )
            {
                _previewMaterial.SetColor(
                    "_BaseColor",
                    new Color(
                        0.7f,
                        0.7f,
                        0.7f,
                        1.0f
                    )
                );
            }
        }
        else
        {
            Debug.LogError(
                "Maya Preview Runtime : " +
                "HDRP/Lit Shader が見つかりません。"
            );
        }


        Debug.Log(
            "Maya Preview Runtime : 初期化"
        );
    }


    // =========================================================
    // Start
    // =========================================================

    private void OnEnable()
    {
        StartServer();
    }


    // =========================================================
    // Update
    // =========================================================

    private void Update()
    {
        // Socket Thread側から受信したMessageを
        // Unity MainThreadで処理する

        // 同じObjectの更新が1フレーム内に複数届いた場合は
        // 最後の1回だけ反映する (結果は同じで無駄な更新をしない)
        _pendingMeshes.Clear();
        _pendingOrder.Clear();

        while (
            _messageQueue.TryDequeue(
                out CSST_ReceivedMessage received
            )
        )
        {
            CS_MayaPreviewIdleThrottle.Wake();

            if (received.meshName == null)
            {
                Debug.Log(
                    "Maya Preview Runtime : " +
                    "Message受信 : " +
                    received.text
                );

                continue;
            }

            if (!_pendingMeshes.ContainsKey(received.meshName))
            {
                _pendingOrder.Add(received.meshName);
            }

            _pendingMeshes[received.meshName] = received;
        }

        foreach (string meshName in _pendingOrder)
        {
            CSST_ReceivedMessage pending = _pendingMeshes[meshName];

            CreateOrUpdateMesh(
                pending.meshName,
                pending.vertices,
                pending.triangles
            );
        }
    }

    private readonly System.Collections.Generic.Dictionary<string, CSST_ReceivedMessage> _pendingMeshes =
        new System.Collections.Generic.Dictionary<string, CSST_ReceivedMessage>();

    private readonly System.Collections.Generic.List<string> _pendingOrder =
        new System.Collections.Generic.List<string>();


    // =========================================================
    // Socket Server開始
    // =========================================================

    private void StartServer()
    {
        if (_serverRunning)
        {
            return;
        }


        try
        {
            _listener =
                new TcpListener(
                    IPAddress.Loopback,
                    PORT
                );


            _listener.Start();


            _serverRunning = true;


            _serverThread =
                new Thread(
                    ServerLoop
                );


            _serverThread.IsBackground =
                true;


            _serverThread.Start();


            Debug.Log(
                "Maya Preview Runtime : " +
                "Socket Server開始 127.0.0.1:" +
                PORT
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "Maya Preview Runtime : " +
                "Socket Server開始失敗\n" +
                exception
            );
        }
    }


    // =========================================================
    // Socket Server Loop
    // =========================================================

    private void ServerLoop()
    {
        while (_serverRunning)
        {
            TcpClient client = null;


            try
            {
                client =
                    _listener.AcceptTcpClient();
                _activeClient = client;
                if (!_serverRunning) { client.Close(); break; }
                client.ReceiveTimeout = 10000;


                using (
                    NetworkStream stream =
                        client.GetStream()
                )
                using (
                    StreamReader reader =
                        new StreamReader(
                            stream,
                            Encoding.UTF8
                        )
                )
                {
                    // Maya側は
                    // JSON + "\n"
                    // で送信している
                    string message =
                        reader.ReadLine();


                    if (
                        !string.IsNullOrEmpty(
                            message
                        )
                    )
                    {
                        CSST_ReceivedMessage received =
                            PrepareMessage(
                                message
                            );

                        if (received != null)
                        {
                            _messageQueue.Enqueue(
                                received
                            );
                        }
                    }
                }
            }
            catch (SocketException)
            {
                if (!_serverRunning)
                {
                    break;
                }
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception exception)
            {
                if (_serverRunning)
                {
                    Debug.LogError(
                        "Maya Preview Runtime : " +
                        "Socket受信エラー\n" +
                        exception
                    );
                }
            }
            finally
            {
                try
                {
                    client?.Close();
                }
                catch
                {
                    // 無視
                }
            }
        }
    }


    // =========================================================
    // Message変換 (受信スレッドで実行)
    //
    // JsonUtility.FromJson はUnity公式で
    // バックグラウンドスレッドから呼んでよいことになっている
    // =========================================================

    private static CSST_ReceivedMessage PrepareMessage(
        string message
    )
    {
        if (string.IsNullOrEmpty(message))
        {
            return null;
        }

        // JSON以外の接続確認用Message
        if (!message.StartsWith("{"))
        {
            return new CSST_ReceivedMessage { text = message };
        }

        try
        {
            CSST_MeshData meshData =
                JsonUtility.FromJson<CSST_MeshData>(
                    message
                );

            if (
                meshData == null ||
                meshData.type != "MESH_DATA"
            )
            {
                return null;
            }

            if (
                meshData.vertices == null ||
                meshData.triangles == null
            )
            {
                Debug.LogError(
                    "Maya Preview Runtime : " +
                    "Mesh Dataが不正です。"
                );

                return null;
            }

            // =================================================
            // Maya X Y Z → Unity X Y -Z
            // Z反転したのでWindingも反転
            //
            // さらに面ごとに頂点を分割する
            // (送信データに法線が無いので、分割しないと
            //  RecalculateNormalsでハードエッジが丸まる)
            //
            // 以前はMainThreadでやっていた処理を
            // そのままここへ移しただけなので結果は同じ
            // =================================================

            CSST_VertexData[] src = meshData.vertices;
            int[] tri = meshData.triangles;

            int count = tri.Length - (tri.Length % 3);

            Vector3[] faceVertices = new Vector3[count];
            int[] faceTriangles = new int[count];

            for (int i = 0; i < count; i += 3)
            {
                int i0 = tri[i];
                int i1 = tri[i + 2];
                int i2 = tri[i + 1];

                faceVertices[i] =
                    new Vector3(src[i0].x, src[i0].y, -src[i0].z) * MAYA_TO_UNITY_SCALE;
                faceVertices[i + 1] =
                    new Vector3(src[i1].x, src[i1].y, -src[i1].z) * MAYA_TO_UNITY_SCALE;
                faceVertices[i + 2] =
                    new Vector3(src[i2].x, src[i2].y, -src[i2].z) * MAYA_TO_UNITY_SCALE;

                faceTriangles[i] = i;
                faceTriangles[i + 1] = i + 1;
                faceTriangles[i + 2] = i + 2;
            }

            return new CSST_ReceivedMessage
            {
                meshName = meshData.name ?? "",
                vertices = faceVertices,
                triangles = faceTriangles,
            };
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "Maya Preview Runtime : " +
                "JSON解析失敗\n" +
                exception
            );

            return null;
        }
    }


    // =========================================================
    // Mesh生成 / 更新
    // =========================================================

    private void CreateOrUpdateMesh(
        string mayaObjectName,
        Vector3[] unityVertices,
        int[] unityTriangles
    )
    {
        if (_mayaModelRoot == null)
        {
            return;
        }


        string unityObjectName =
            LIVE_OBJECT_PREFIX +
            mayaObjectName;


        // =====================================================
        // 既存Object検索
        // =====================================================

        Transform existingTransform =
            _mayaModelRoot.transform.Find(
                unityObjectName
            );


        GameObject targetObject;


        if (existingTransform != null)
        {
            targetObject =
                existingTransform.gameObject;
        }
        else
        {
            // =================================================
            // 新規生成
            // =================================================

            targetObject =
                new GameObject(
                    unityObjectName
                );


            targetObject.transform.SetParent(
                _mayaModelRoot.transform,
                false
            );


            targetObject.AddComponent<MeshFilter>();


            MeshRenderer renderer =
                targetObject.AddComponent<MeshRenderer>();


            if (_previewMaterial != null)
            {
                renderer.sharedMaterial =
                    _previewMaterial;
            }
        }


        // =====================================================
        // Component取得
        // =====================================================

        MeshFilter meshFilter =
            targetObject.GetComponent<MeshFilter>();


        MeshRenderer meshRenderer =
            targetObject.GetComponent<MeshRenderer>();


        if (
            meshRenderer != null &&
            meshRenderer.sharedMaterial == null &&
            _previewMaterial != null
        )
        {
            meshRenderer.sharedMaterial =
                _previewMaterial;
        }


        // =====================================================
        // Mesh取得 / 生成
        // =====================================================

        Mesh mesh =
            meshFilter.sharedMesh;


        if (mesh == null)
        {
            mesh =
                new Mesh();


            mesh.name =
                unityObjectName +
                "_Mesh";


            mesh.MarkDynamic();


            meshFilter.sharedMesh =
                mesh;
        }


        // =====================================================
        // Mesh更新
        // =====================================================

        // 面ごとの頂点分割は受信スレッド (PrepareMessage) で済ませてある

        mesh.Clear();


        // 65535頂点を超える場合
        if (
            unityVertices.Length >
            65535
        )
        {
            mesh.indexFormat =
                IndexFormat.UInt32;
        }
        else
        {
            mesh.indexFormat =
                IndexFormat.UInt16;
        }


        mesh.vertices =
            unityVertices;


        mesh.triangles =
            unityTriangles;


        mesh.RecalculateNormals();

        mesh.RecalculateBounds();
        CS_MayaPreviewSelection.NotifyMeshChanged(mesh);


        // =====================================================
        // Mesh更新完了
        // =====================================================

        Debug.Log(
            "Maya Preview Runtime : Mesh更新\n" +
            "Object : " +
            mayaObjectName +
            "\nVertex : " +
            unityVertices.Length +
            "\nTriangle : " +
            (
                unityTriangles.Length / 3
            )
        );


        // =====================================================
        // ★重要
        //
        // Mesh更新後のHDRP画像を
        // Mayaへ1枚だけ返す
        // =====================================================

        if (_frameSender != null)
        {
            _frameSender.RequestFrameAfterRender();
        }
    }


    // =========================================================
    // Server停止
    // =========================================================

    private void StopServer()
    {
        if (!_serverRunning)
        {
            return;
        }


        _serverRunning = false;
        try { _activeClient?.Close(); } catch { }
        _activeClient = null;


        try
        {
            _listener?.Stop();
        }
        catch
        {
            // 無視
        }


        _listener = null;


        try
        {
            if (
                _serverThread != null &&
                _serverThread.IsAlive
            )
            {
                _serverThread.Join(
                    500
                );
            }
        }
        catch
        {
            // 無視
        }


        _serverThread = null;


        Debug.Log(
            "Maya Preview Runtime : Socket Server停止"
        );
    }


    // =========================================================
    // OnDestroy
    // =========================================================

    private void OnDisable()
    {
        StopServer();
    }

    private void OnDestroy()
    {
        StopServer();


        if (_previewMaterial != null)
        {
            Destroy(
                _previewMaterial
            );


            _previewMaterial = null;
        }
    }


    // =========================================================
    // Application終了
    // =========================================================

    private void OnApplicationQuit()
    {
        StopServer();
    }
}