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

    private volatile bool _serverRunning;


    // =========================================================
    // MainThread処理Queue
    // =========================================================

    private readonly ConcurrentQueue<string> _messageQueue =
        new ConcurrentQueue<string>();


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

    private void Start()
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

        while (
            _messageQueue.TryDequeue(
                out string message
            )
        )
        {
            ProcessMessage(
                message
            );
        }
    }


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
                        _messageQueue.Enqueue(
                            message
                        );
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
    // Message処理
    // =========================================================

    private void ProcessMessage(
        string message
    )
    {
        if (
            string.IsNullOrEmpty(
                message
            )
        )
        {
            return;
        }


        // JSON以外の接続確認用Message
        if (
            !message.StartsWith(
                "{"
            )
        )
        {
            Debug.Log(
                "Maya Preview Runtime : " +
                "Message受信 : " +
                message
            );

            return;
        }


        try
        {
            CSST_MeshData meshData =
                JsonUtility.FromJson<CSST_MeshData>(
                    message
                );


            if (meshData == null)
            {
                return;
            }


            if (
                meshData.type ==
                "MESH_DATA"
            )
            {
                ProcessMeshData(
                    meshData
                );
            }
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "Maya Preview Runtime : " +
                "JSON解析失敗\n" +
                exception
            );
        }
    }


    // =========================================================
    // Mesh Data処理
    // =========================================================

    private void ProcessMeshData(
        CSST_MeshData meshData
    )
    {
        if (
            meshData.vertices == null ||
            meshData.triangles == null
        )
        {
            Debug.LogError(
                "Maya Preview Runtime : " +
                "Mesh Dataが不正です。"
            );

            return;
        }


        // =====================================================
        // Vertex変換
        //
        // Maya
        // X Y Z
        //
        // Unity
        // X Y -Z
        //
        // Maya cm → Unity m
        // =====================================================

        Vector3[] unityVertices =
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


            unityVertices[i] =
                new Vector3(
                    vertex.x,
                    vertex.y,
                    -vertex.z
                )
                * MAYA_TO_UNITY_SCALE;
        }


        // =====================================================
        // Triangle
        //
        // Z反転したためWindingも反転
        // =====================================================

        int[] unityTriangles =
            new int[
                meshData.triangles.Length
            ];


        for (
            int i = 0;
            i + 2 < meshData.triangles.Length;
            i += 3
        )
        {
            unityTriangles[i] =
                meshData.triangles[i];


            unityTriangles[i + 1] =
                meshData.triangles[i + 2];


            unityTriangles[i + 2] =
                meshData.triangles[i + 1];
        }


        // =====================================================
        // Mesh生成・更新
        // =====================================================

        CreateOrUpdateMesh(
            meshData.name,
            unityVertices,
            unityTriangles
        );
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

        // The legacy payload has no face-vertex normals. Split triangle corners
        // so RecalculateNormals does not smooth across hard edges.
        Vector3[] faceVertices = new Vector3[unityTriangles.Length];
        int[] faceTriangles = new int[unityTriangles.Length];
        for (int i = 0; i < unityTriangles.Length; i++)
        {
            faceVertices[i] = unityVertices[unityTriangles[i]];
            faceTriangles[i] = i;
        }
        unityVertices = faceVertices;
        unityTriangles = faceTriangles;

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