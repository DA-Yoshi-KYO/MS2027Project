using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

    // Mayaはcm、Unityはm。Mayaの実寸のまま表示する（Maya 170cm → Unity 1.7m、FBX取り込みと同じ）
    private const float MAYA_TO_UNITY_SCALE = 0.01f;

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
    // MESH_DATA_V2（UV・法線・マテリアル名付き）
    //
    // positions : x,y,z の並び（Maya座標）
    // normals   : x,y,z の並び（positionsと同じ数 or 空）
    // uvs       : u,v の並び（頂点数×2 or 空）
    // submeshes : マテリアルごとの三角形リスト
    // =========================================================

    [Serializable]
    private class CSST_MessageHeader
    {
        public string type;

        // MESH_REMOVE で消すObject名
        public string name;

        // SESSION : マテリアル設定の保存先（Mayaプロジェクト/unityPreview/materials）
        public string materialFolder;
    }


    [Serializable]
    private class CSST_SubMeshData
    {
        public string material;

        public int[] triangles;
    }


    [Serializable]
    private class CSST_MeshDataV2
    {
        public string type;

        public string name;

        public float[] positions;

        public float[] normals;

        public float[] uvs;

        public CSST_SubMeshData[] submeshes;
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

    private readonly ConcurrentQueue<string> _messageQueue =
        new ConcurrentQueue<string>();


    // =========================================================
    // Unity Object
    // =========================================================

    private GameObject _mayaModelRoot;

    private Material _previewMaterial;


    // HDRP Frame送信
    private CS_MayaPreviewFrameSender _frameSender;

    // Mayaツール側の画像表示は廃止したため既定はOFF
    // （毎回のGPU読み戻しと送信失敗ログを出さない）
    [SerializeField]
    private bool _sendFramesToMaya = false;


    // =========================================================
    // Material割り当て
    // =========================================================

    // Maya → Unity Material名の一覧（Resourcesから読み込み）
    private CS_MayaPreviewMaterialCatalog _materialCatalog;

    // 一覧に無い名前を目立たせる用
    private Material _missingMaterial;

    // Mayaのマテリアル名ごとのMaterial（自動HDRP/Lit、Inspectorで変更・保存）
    private CS_MayaPreviewMaterialLibrary _library;

    // シーン内で動いているServer（重複したPreview_Systemは無効化する）
    // 2つ目がLibraryを作るとInspectorがそちらを見てしまい、Shader設定が出なくなるため
    private static CS_MayaPreviewRuntimeServer s_active;
    private bool _duplicate;

    // 同じ警告を何度も出さない
    private readonly HashSet<string> _warnedMaterialNames =
        new HashSet<string>();

    // Object名 → 前回割り当てたMaterial名の並び
    // （変わっていなければ割り当て直さず、Inspectorでの調整を残す）
    private readonly Dictionary<string, string> _appliedMaterialKeys =
        new Dictionary<string, string>();


    // =========================================================
    // Awake
    // =========================================================

    private void Awake()
    {
        if (s_active != null && s_active != this)
        {
            _duplicate = true;
            enabled = false;
            var duplicateSender = GetComponent<CS_MayaPreviewFrameSender>();
            if (duplicateSender != null) duplicateSender.enabled = false;
            Debug.LogWarning(
                "Maya Preview Runtime : CS_MayaPreviewRuntimeServer が重複しています（" + name +
                "）。こちらは無効化しました。シーンに1つだけ置いてください。"
            );
            return;
        }

        s_active = this;

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


        // =====================================================
        // Material一覧
        // =====================================================

        _materialCatalog =
            CS_MayaPreviewMaterialCatalog.Load();


        if (_materialCatalog == null)
        {
            Debug.LogWarning(
                "Maya Preview Runtime : Material一覧が見つかりません。" +
                "Editorで Tools/Maya Preview/Material Catalog/Rebuild を実行してからビルドしてください。"
            );
        }


        _library =
            new CS_MayaPreviewMaterialLibrary(
                _materialCatalog
            );

        // シェーダー変更などでMaterialが作り直されたら割り当て直す
        _library.MaterialReplaced +=
            RefreshMaterial;


        if (shader != null)
        {
            _missingMaterial =
                new Material(
                    shader
                );


            _missingMaterial.name =
                "MAT_MayaPreview_Missing";


            if (_missingMaterial.HasProperty("_BaseColor"))
            {
                _missingMaterial.SetColor(
                    "_BaseColor",
                    new Color(1.0f, 0.0f, 1.0f, 1.0f)
                );
            }
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
        if (_duplicate)
        {
            return;
        }

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


        // Inspectorで変えたマテリアル設定をまとめて保存
        _library?.Tick(
            Time.unscaledTime
        );
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
            CSST_MessageHeader header =
                JsonUtility.FromJson<CSST_MessageHeader>(
                    message
                );


            if (
                header != null &&
                header.type ==
                "MESH_DATA_V2"
            )
            {
                ProcessMeshDataV2(
                    JsonUtility.FromJson<CSST_MeshDataV2>(
                        message
                    )
                );

                return;
            }


            if (
                header != null &&
                header.type ==
                "SESSION"
            )
            {
                _library?.SetFolder(
                    header.materialFolder
                );

                return;
            }


            if (
                header != null &&
                header.type ==
                "MATERIAL_DEF"
            )
            {
                ProcessMaterialDef(
                    JsonUtility.FromJson<CS_MayaPreviewMaterialLibrary.MaterialDef>(
                        message
                    )
                );

                return;
            }


            if (
                header != null &&
                header.type ==
                "MESH_REMOVE"
            )
            {
                RemoveLiveObject(
                    header.name
                );

                return;
            }


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

        if (_sendFramesToMaya && _frameSender != null)
        {
            _frameSender.RequestFrameAfterRender();
        }
    }


    // =========================================================
    // MESH_DATA_V2処理
    // =========================================================

    private void ProcessMeshDataV2(
        CSST_MeshDataV2 meshData
    )
    {
        if (
            meshData == null ||
            meshData.positions == null ||
            meshData.positions.Length % 3 != 0 ||
            meshData.submeshes == null
        )
        {
            Debug.LogError(
                "Maya Preview Runtime : MESH_DATA_V2が不正です。"
            );

            return;
        }


        int vertexCount =
            meshData.positions.Length / 3;


        bool hasNormals =
            meshData.normals != null &&
            meshData.normals.Length == meshData.positions.Length;


        bool hasUvs =
            meshData.uvs != null &&
            meshData.uvs.Length == vertexCount * 2;


        // =====================================================
        // 頂点変換（Maya X Y Z → Unity X Y -Z）
        // =====================================================

        var vertices = new Vector3[vertexCount];
        var normals = hasNormals ? new Vector3[vertexCount] : null;
        var uvs = hasUvs ? new Vector2[vertexCount] : null;


        for (int i = 0; i < vertexCount; i++)
        {
            int p = i * 3;

            vertices[i] =
                new Vector3(
                    meshData.positions[p],
                    meshData.positions[p + 1],
                    -meshData.positions[p + 2]
                )
                * MAYA_TO_UNITY_SCALE;


            if (hasNormals)
            {
                normals[i] =
                    new Vector3(
                        meshData.normals[p],
                        meshData.normals[p + 1],
                        -meshData.normals[p + 2]
                    );
            }


            if (hasUvs)
            {
                uvs[i] =
                    new Vector2(
                        meshData.uvs[i * 2],
                        meshData.uvs[i * 2 + 1]
                    );
            }
        }


        // =====================================================
        // Submesh（Z反転したためWindingも反転）
        // =====================================================

        var subMeshTriangles = new List<int[]>();
        var materialNames = new List<string>();


        foreach (CSST_SubMeshData subMesh in meshData.submeshes)
        {
            if (
                subMesh == null ||
                subMesh.triangles == null ||
                subMesh.triangles.Length < 3
            )
            {
                continue;
            }


            int count =
                subMesh.triangles.Length - subMesh.triangles.Length % 3;

            var triangles = new int[count];


            for (int i = 0; i < count; i += 3)
            {
                int a = subMesh.triangles[i];
                int b = subMesh.triangles[i + 1];
                int c = subMesh.triangles[i + 2];


                if (
                    (uint)a >= (uint)vertexCount ||
                    (uint)b >= (uint)vertexCount ||
                    (uint)c >= (uint)vertexCount
                )
                {
                    Debug.LogError(
                        "Maya Preview Runtime : 頂点番号が範囲外です。 " +
                        meshData.name
                    );

                    return;
                }


                triangles[i] = a;
                triangles[i + 1] = c;
                triangles[i + 2] = b;
            }


            subMeshTriangles.Add(triangles);
            materialNames.Add(subMesh.material ?? string.Empty);
        }


        if (subMeshTriangles.Count == 0)
        {
            Debug.LogWarning(
                "Maya Preview Runtime : 三角形がありません。 " +
                meshData.name
            );

            return;
        }


        // =====================================================
        // Mesh更新
        // =====================================================

        GameObject targetObject =
            GetOrCreateLiveObject(
                meshData.name
            );


        if (targetObject == null)
        {
            return;
        }


        MeshFilter meshFilter =
            targetObject.GetComponent<MeshFilter>();


        Mesh mesh =
            meshFilter.sharedMesh;


        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = targetObject.name + "_Mesh";
            mesh.MarkDynamic();
            meshFilter.sharedMesh = mesh;
        }


        mesh.Clear();

        mesh.indexFormat =
            vertexCount > 65535
                ? IndexFormat.UInt32
                : IndexFormat.UInt16;

        mesh.SetVertices(vertices);


        if (hasNormals)
        {
            mesh.SetNormals(normals);
        }


        if (hasUvs)
        {
            mesh.SetUVs(0, uvs);
        }


        mesh.subMeshCount =
            subMeshTriangles.Count;


        for (int i = 0; i < subMeshTriangles.Count; i++)
        {
            mesh.SetTriangles(
                subMeshTriangles[i],
                i,
                false
            );
        }


        if (!hasNormals)
        {
            mesh.RecalculateNormals();
        }


        // トゥーンShaderはTangentを使うため、UVがある時は計算する
        if (hasUvs)
        {
            mesh.RecalculateTangents();
        }


        mesh.RecalculateBounds();
        CS_MayaPreviewSelection.NotifyMeshChanged(mesh);


        // =====================================================
        // Material割り当て
        // =====================================================

        ApplyMaterials(
            targetObject,
            materialNames
        );


        Debug.Log(
            "Maya Preview Runtime : Mesh更新 (V2)\n" +
            "Object : " + meshData.name +
            "\nVertex : " + vertexCount +
            "\nMaterial : " + string.Join(", ", materialNames)
        );


        if (_sendFramesToMaya && _frameSender != null)
        {
            _frameSender.RequestFrameAfterRender();
        }
    }


    // =========================================================
    // MATERIAL_DEF（Mayaで作ったマテリアル）
    // =========================================================

    private void ProcessMaterialDef(
        CS_MayaPreviewMaterialLibrary.MaterialDef def
    )
    {
        if (
            def == null ||
            _library == null ||
            string.IsNullOrEmpty(def.name)
        )
        {
            return;
        }


        // Mayaの色・ベースマップ・ノーマルだけ反映（シェーダーと値はPreviewHost側の設定を保つ）
        _library.SetMayaData(
            def
        );
    }


    // 指定名のMaterialを使っているLive Objectに割り当て直す
    private void RefreshMaterial(
        string materialName
    )
    {
        if (_mayaModelRoot == null)
        {
            return;
        }


        foreach (var pair in new List<KeyValuePair<string, string>>(_appliedMaterialKeys))
        {
            string[] names = pair.Value.Split('|');

            if (
                Array.FindIndex(
                    names,
                    n => string.Equals(
                        CS_MayaPreviewMaterialCatalog.NormalizeName(n),
                        materialName,
                        StringComparison.OrdinalIgnoreCase
                    )
                ) < 0
            )
            {
                continue;
            }


            Transform target =
                _mayaModelRoot.transform.Find(
                    pair.Key
                );

            MeshRenderer meshRenderer =
                target != null ? target.GetComponent<MeshRenderer>() : null;

            if (meshRenderer == null)
            {
                continue;
            }


            var materials = new Material[names.Length];

            for (int i = 0; i < names.Length; i++)
            {
                materials[i] =
                    ResolveMaterial(
                        names[i]
                    );
            }


            meshRenderer.sharedMaterials =
                materials;
        }


        if (_sendFramesToMaya && _frameSender != null)
        {
            _frameSender.RequestFrameAfterRender();
        }
    }


    // =========================================================
    // Live Object削除（Mayaで削除・名前変更されたメッシュ）
    // =========================================================

    private void RemoveLiveObject(
        string mayaObjectName
    )
    {
        if (
            _mayaModelRoot == null ||
            string.IsNullOrEmpty(mayaObjectName)
        )
        {
            return;
        }


        string unityObjectName =
            LIVE_OBJECT_PREFIX +
            mayaObjectName;


        Transform target =
            _mayaModelRoot.transform.Find(
                unityObjectName
            );


        if (target == null)
        {
            return;
        }


        MeshFilter meshFilter =
            target.GetComponent<MeshFilter>();


        if (
            meshFilter != null &&
            meshFilter.sharedMesh != null
        )
        {
            Destroy(
                meshFilter.sharedMesh
            );
        }


        _appliedMaterialKeys.Remove(
            unityObjectName
        );


        Destroy(
            target.gameObject
        );


        Debug.Log(
            "Maya Preview Runtime : Mesh削除 " +
            mayaObjectName
        );
    }


    // =========================================================
    // Live Object取得 / 生成
    // =========================================================

    private GameObject GetOrCreateLiveObject(
        string mayaObjectName
    )
    {
        if (_mayaModelRoot == null)
        {
            return null;
        }


        string unityObjectName =
            LIVE_OBJECT_PREFIX +
            mayaObjectName;


        Transform existingTransform =
            _mayaModelRoot.transform.Find(
                unityObjectName
            );


        if (existingTransform != null)
        {
            GameObject existing = existingTransform.gameObject;

            if (existing.GetComponent<MeshFilter>() == null)
            {
                existing.AddComponent<MeshFilter>();
            }

            if (existing.GetComponent<MeshRenderer>() == null)
            {
                existing.AddComponent<MeshRenderer>();
            }

            return existing;
        }


        GameObject targetObject =
            new GameObject(
                unityObjectName
            );


        targetObject.transform.SetParent(
            _mayaModelRoot.transform,
            false
        );


        targetObject.AddComponent<MeshFilter>();
        targetObject.AddComponent<MeshRenderer>();

        return targetObject;
    }


    // =========================================================
    // Material割り当て
    //
    // 名前が空      → 灰色のPreview Material
    // 一覧に無い    → マゼンタ（Missing）
    // 一覧にある    → Unityで作ったMaterialをそのまま使う
    // =========================================================

    private void ApplyMaterials(
        GameObject targetObject,
        List<string> materialNames
    )
    {
        MeshRenderer meshRenderer =
            targetObject.GetComponent<MeshRenderer>();


        if (meshRenderer == null)
        {
            return;
        }


        string key =
            string.Join("|", materialNames);


        // 同じ割り当てなら触らない（Inspectorで調整中の値を消さない）
        if (
            _appliedMaterialKeys.TryGetValue(targetObject.name, out string appliedKey) &&
            appliedKey == key &&
            meshRenderer.sharedMaterials.Length == materialNames.Count
        )
        {
            return;
        }


        var materials = new Material[materialNames.Count];


        for (int i = 0; i < materialNames.Count; i++)
        {
            materials[i] =
                ResolveMaterial(
                    materialNames[i]
                );
        }


        meshRenderer.sharedMaterials =
            materials;


        _appliedMaterialKeys[targetObject.name] =
            key;
    }


    private Material ResolveMaterial(
        string materialName
    )
    {
        string normalized =
            CS_MayaPreviewMaterialCatalog.NormalizeName(
                materialName
            );


        if (normalized.Length == 0)
        {
            return _previewMaterial;
        }


        // Mayaのマテリアル名ごとに自動生成（初回はHDRP/Lit）
        Material material =
            _library != null
                ? _library.GetOrCreate(normalized)
                : _materialCatalog != null
                    ? _materialCatalog.Find(normalized)
                    : null;


        if (material != null)
        {
            return material;
        }


        if (_warnedMaterialNames.Add(normalized))
        {
            Debug.LogWarning(
                "Maya Preview Runtime : Material \"" + normalized +
                "\" がUnityの一覧にありません。マゼンタで表示します。"
            );
        }


        return _missingMaterial != null
            ? _missingMaterial
            : _previewMaterial;
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
        if (s_active == this)
        {
            s_active = null;
        }

        StopServer();


        if (_previewMaterial != null)
        {
            Destroy(
                _previewMaterial
            );


            _previewMaterial = null;
        }


        if (_missingMaterial != null)
        {
            Destroy(
                _missingMaterial
            );


            _missingMaterial = null;
        }


        _library?.Dispose();
        _library = null;
    }


    // =========================================================
    // Application終了
    // =========================================================

    private void OnApplicationQuit()
    {
        _library?.SaveDirty();
        StopServer();
    }
}