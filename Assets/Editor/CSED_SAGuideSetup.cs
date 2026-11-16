using UnityEditor;
using UnityEngine;

/*
 * グラップルの矢印(Issue #505)・動くSAポイントの軌道(Issue #507)に使うアセットを作り、UICanvas・Playerに組み込む一時的なエディタスクリプト
 * YAMLを手で書かずに済ませるためのもの。組み込みが終わったら消してよい(メニュー Tools/SAGuideSetup)
 *
 * 制作者：　吉田京志郎
 */
internal static class CSED_SAGuideSetup
{
    private const string _arrowFolder = "Assets/Programmer/PlaceholderAsset/UI/GrappleArrow";
    private const string _pathFolder = "Assets/Programmer/PlaceholderAsset/UI/SAPath";
    private const string _chevronPath = _arrowFolder + "/GrappleArrowChevron.png";
    private const string _dashPath = _pathFolder + "/SAPathDash.png";
    private const string _arrowMaterialPath = _arrowFolder + "/MT_GrappleArrow.mat";
    private const string _pathMaterialPath = _pathFolder + "/MT_SAPathDash.mat";
    private const string _novaGlowPath = "Assets/Programmer/min/YosimotoRyo/NovaHDRPEffects/Materials/Glow_Additive.mat";

    private const string _playerPath = "Assets/Programmer/Prefab/Entity/Player/Player.prefab";
    private const string _uiCanvasPath = "Assets/Programmer/Prefab/UI/InGame/UICanvas.prefab";

    // ========================================
    // 1. 画像とマテリアル
    // ========================================

    [MenuItem("Tools/SAGuideSetup/1 Textures and Materials")]
    private static void SetupTexturesAndMaterials()
    {
        ImportTexture(_chevronPath, TextureWrapMode.Clamp);
        ImportTexture(_dashPath, TextureWrapMode.Repeat);

        MakeMaterial(_arrowMaterialPath, _chevronPath);
        MakeMaterial(_pathMaterialPath, _dashPath);

        AssetDatabase.SaveAssets();
        Debug.Log("[SAGuideSetup] 1 完了: 画像のインポート設定、MT_GrappleArrow、MT_SAPathDash");
    }

    private static void ImportTexture(string path, TextureWrapMode wrapMode)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = wrapMode;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
    }

    // Novaの加算マテリアルを複製して、画像を差し替える(頂点カラーで色と濃さを決める)
    private static void MakeMaterial(string path, string texturePath)
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(path) == null)
        {
            if (!AssetDatabase.CopyAsset(_novaGlowPath, path)) Debug.LogError($"[SAGuideSetup] コピーに失敗: {_novaGlowPath}");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        EditorUtility.SetDirty(material);
    }

    // ========================================
    // 2. UICanvasにPresenter・Viewを足す
    // ========================================

    [MenuItem("Tools/SAGuideSetup/2 UICanvas")]
    private static void SetupUiCanvas()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(_uiCanvasPath);

        if (root.GetComponentInChildren<CS_UIGrappleArrowPresenter>(true) == null)
        {
            GameObject arrow = new GameObject("GrappleArrow");
            arrow.layer = root.layer;
            arrow.transform.SetParent(root.transform, false);
            arrow.AddComponent<CS_UIGrappleArrowPresenter>();
            CS_UIGrappleArrowView view = arrow.GetComponent<CS_UIGrappleArrowView>();

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("_material").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(_arrowMaterialPath);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        if (root.GetComponentInChildren<CS_UISAPathPresenter>(true) == null)
        {
            GameObject path = new GameObject("SAPath");
            path.layer = root.layer;
            path.transform.SetParent(root.transform, false);
            path.AddComponent<CS_UISAPathPresenter>();
            CS_UISAPathView view = path.GetComponent<CS_UISAPathView>();

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("_material").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(_pathMaterialPath);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        PrefabUtility.SaveAsPrefabAsset(root, _uiCanvasPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("[SAGuideSetup] 2 完了: UICanvasにGrappleArrow・SAPathを追加");
    }

    // ========================================
    // 9. 動作確認用(Play中に、矢印・軌道の様子をログに出す。ログは1件にまとめて出す)
    // ========================================

    private static double _debugEnd;
    private static double _debugNext;
    private static bool _debugMoved;
    private static CS_Player _debugPlayer;
    private static readonly System.Text.StringBuilder _debugLog = new System.Text.StringBuilder();

    [MenuItem("Tools/SAGuideSetup/9 Debug (Play mode)")]
    private static void DebugRun()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[SAGuideDebug] Play中に実行してください"); return; }

        _debugPlayer = null;
        foreach (CS_Player player in Object.FindObjectsByType<CS_Player>(FindObjectsSortMode.None))
        {
            if (player.isLocalPlayer) _debugPlayer = player;
        }
        if (_debugPlayer == null) { Debug.LogWarning("[SAGuideDebug] 自分のプレイヤーがいません"); return; }

        _debugLog.Clear();
        _debugLog.AppendLine($"player={_debugPlayer.name} arrowUI={_debugPlayer.GetComponent<CS_PlayerGrappleArrowUI>() != null} pathUI={_debugPlayer.GetComponent<CS_PlayerSAPathUI>() != null}");
        _debugLog.AppendLine($"followers={Object.FindObjectsByType<CS_PathFollower>(FindObjectsSortMode.None).Length} grapples={Object.FindObjectsByType<CS_GrappleSAPoint>(FindObjectsSortMode.None).Length} arrowView={Object.FindObjectsByType<CS_UIGrappleArrowView>(FindObjectsSortMode.None).Length} pathView={Object.FindObjectsByType<CS_UISAPathView>(FindObjectsSortMode.None).Length}");

        _debugMoved = false;
        _debugNext = EditorApplication.timeSinceStartup;
        _debugEnd = EditorApplication.timeSinceStartup + 6.0;
        EditorApplication.update -= DebugUpdate;
        EditorApplication.update += DebugUpdate;
    }

    private static void DebugUpdate()
    {
        if (!Application.isPlaying || EditorApplication.timeSinceStartup > _debugEnd)
        {
            EditorApplication.update -= DebugUpdate;
            Debug.Log("[SAGuideDebug] まとめ\n" + _debugLog);
            return;
        }
        if (EditorApplication.timeSinceStartup < _debugNext) return;
        _debugNext = EditorApplication.timeSinceStartup + 0.5;

        // 最初の3秒は、グラップルの的の方へカメラを向ける(狙っている状態にするため)
        CS_GrappleSAPoint grapple = Object.FindFirstObjectByType<CS_GrappleSAPoint>();
        if (!_debugMoved && grapple != null)
        {
            Vector3 toTarget = grapple.standPosition - _debugPlayer.transform.position;
            Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
            System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(CS_Player).GetField("_yaw", flags).SetValue(_debugPlayer, Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg);
            typeof(CS_Player).GetField("_pitch", flags).SetValue(_debugPlayer, -Mathf.Atan2(toTarget.y, flat.magnitude) * Mathf.Rad2Deg * 0.5f);
        }

        // 残り3秒になったら、軌道を出す距離を1mにして、消えるか確かめる
        if (!_debugMoved && _debugEnd - EditorApplication.timeSinceStartup < 3.0)
        {
            _debugMoved = true;
            CS_PlayerSAPathUI ui = _debugPlayer.GetComponent<CS_PlayerSAPathUI>();
            System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(CS_PlayerSAPathUI).GetField("_showDistance", flags).SetValue(ui, 1f);
            typeof(CS_PlayerSAPathUI).GetField("_hideMargin", flags).SetValue(ui, 0f);
            _debugLog.AppendLine("-- 軌道を出す距離を1m・余裕0mにした(消えるはず)。グラップルを飛び始める: " + _debugPlayer.GetComponent<CS_PlayerHide>().TryBeginHide());
        }

        CS_PlayerHide hide = _debugPlayer.GetComponent<CS_PlayerHide>();
        string candidate = hide != null && hide.currentCandidate != null ? hide.currentCandidate.GetType().Name : "なし";

        CS_UIGrappleArrowView arrowView = Object.FindFirstObjectByType<CS_UIGrappleArrowView>();
        float fade = -1f;
        if (arrowView != null)
        {
            System.Reflection.FieldInfo fadeField = typeof(CS_UIGrappleArrowView).GetField("_fade", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            fade = (float)fadeField.GetValue(arrowView);
        }

        // 軌道: 登録数・出している数
        int registered = 0;
        int visible = 0;
        CS_PlayerSAPathUI pathUI = _debugPlayer.GetComponent<CS_PlayerSAPathUI>();
        if (pathUI != null)
        {
            System.Reflection.FieldInfo pathsField = typeof(CS_PlayerSAPathUI).GetField("_paths", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            System.Collections.IDictionary paths = (System.Collections.IDictionary)pathsField.GetValue(pathUI);
            registered = paths.Count;
            foreach (object value in paths.Values)
            {
                System.Reflection.FieldInfo visibleField = value.GetType().GetField("isVisible");
                if ((bool)visibleField.GetValue(value)) visible++;
            }
        }

        int enabledLines = 0;
        GameObject linesRoot = GameObject.Find("SAPathLines");
        if (linesRoot != null)
        {
            foreach (LineRenderer line in linesRoot.GetComponentsInChildren<LineRenderer>()) if (line.enabled) enabledLines++;
        }

        int enabledChevrons = 0;
        GameObject arrowRoot = GameObject.Find("GrappleArrowChevrons");
        if (arrowRoot != null)
        {
            foreach (MeshRenderer renderer in arrowRoot.GetComponentsInChildren<MeshRenderer>()) if (renderer.enabled) enabledChevrons++;
        }

        // 見えているか(カメラの描画対象か・画面のどこか・大きさ)
        Camera cam = _debugPlayer.playerCamera;
        if (cam != null)
        {
            _debugLog.AppendLine($"  cam={cam.name} pos={cam.transform.position:F1} fov={cam.fieldOfView:F0} cullingMask={cam.cullingMask} screen={Screen.width}x{Screen.height}");
            if (arrowRoot != null)
            {
                foreach (MeshRenderer renderer in arrowRoot.GetComponentsInChildren<MeshRenderer>())
                {
                    Vector3 v = cam.WorldToViewportPoint(renderer.transform.position);
                    float distance = Vector3.Distance(cam.transform.position, renderer.transform.position);
                    float pixels = renderer.bounds.size.magnitude / (2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad)) * Screen.height;
                    _debugLog.AppendLine($"  chevron {renderer.name} enabled={renderer.enabled} isVisible={renderer.isVisible} layer={renderer.gameObject.layer} viewport={v:F2} dist={distance:F1} sizePx~{pixels:F0} shader={renderer.sharedMaterial.shader.name} shaderError={UnityEditor.ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader)} meshColor0={renderer.GetComponent<MeshFilter>().sharedMesh.colors[0]}");
                }
            }
            if (linesRoot != null)
            {
                foreach (LineRenderer line in linesRoot.GetComponentsInChildren<LineRenderer>())
                {
                    Vector3 near = line.GetPosition(0);
                    float distance = Vector3.Distance(cam.transform.position, near);
                    _debugLog.AppendLine($"  line {line.name} enabled={line.enabled} isVisible={line.isVisible} layer={line.gameObject.layer} points={line.positionCount} first={near:F1} dist={distance:F1} width={line.widthMultiplier} startColor={line.startColor} bounds={line.bounds.size:F1} shader={line.sharedMaterial.shader.name}");
                }
            }
        }

        _debugLog.AppendLine($"pos={_debugPlayer.transform.position:F1} candidate={candidate} arrowFade={fade:F2} chevronsOn={enabledChevrons} paths={registered} visiblePaths={visible} linesOn={enabledLines}");
    }

    // ========================================
    // 3. Player.prefabにコンポーネントを足す
    // ========================================

    [MenuItem("Tools/SAGuideSetup/3 Player Prefab")]
    private static void SetupPlayerPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(_playerPath);
        if (root.GetComponent<CS_PlayerGrappleArrowUI>() == null) root.AddComponent<CS_PlayerGrappleArrowUI>();
        if (root.GetComponent<CS_PlayerSAPathUI>() == null) root.AddComponent<CS_PlayerSAPathUI>();

        PrefabUtility.SaveAsPrefabAsset(root, _playerPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("[SAGuideSetup] 3 完了: Player.prefabにCS_PlayerGrappleArrowUI・CS_PlayerSAPathUIを追加");
    }
}
