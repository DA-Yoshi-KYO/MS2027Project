using System.Collections.Generic;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

/*
 * デバッグ用: ゲームの再生中に、Sceneビューをクリックした所へ自分のプレイヤーをテレポートさせるツール
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・再生中だけ、Sceneビューに「テレポート」のツールバー(Overlay)が出る
 *   (出ない時は、Sceneビュー右上の「︙」→ Overlays で「テレポート」にチェック)
 * ・「クリックでテレポート」を押している間、Sceneビューの地面をクリックすると、そこへ自分のプレイヤー(CS_Player.localPlayer)が移る
 *   何度でもクリックできる。もう一度押すか、Escで外す(再生を止めても外れる)
 *   押している間は、マウスの下の着地点に丸を出す
 * ・「Sceneビューの中心へ」: Sceneビューの中心の真下の地面へ移る
 * ・着地点: マウスの下のコライダー(トリガーと自分のプレイヤーは除く)。足元がそこに来るよう、体の中心の高さ分だけ上に置く
 * ・移し方はCS_PhaseSwapと同じ(CS_Player.SnapPositionで速度ごと止めて移し、オンラインならNetworkTransform.Teleportで補間せずに同期)
 *   自分のプレイヤーは自分が権限を持っている(NetworkTransformのAuthorityModeがOwner)ので、ホスト・クライアントどちらでも使える
 * ・ハイド中・SA中などの状態は解除しない(位置だけ変える)
 */
// ========================================

[InitializeOnLoad]
public static class CSED_PlayerTeleportTool
{
    private const float _rayDistance = 2000f;     // 着地点を探す距離(m)
    private const float _groundMargin = 0.05f;    // 着地点から少し浮かせる高さ(m。地面に埋まらないように)
    private const float _markerRadius = 0.6f;
    private static readonly int _controlHash = "PlayerTeleportTool".GetHashCode();
    private static readonly Color _markerColor = new Color(0.3f, 0.9f, 1f);

    private static bool _isActive;

    public static bool isActive
    {
        get => _isActive;
        set
        {
            _isActive = value;
            SceneView.RepaintAll();
        }
    }

    static CSED_PlayerTeleportTool()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.playModeStateChanged += _ => isActive = false;
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        if (!EditorApplication.isPlaying || !_isActive) return;

        CS_Player player = CS_Player.localPlayer;
        if (player == null) return;

        Event e = Event.current;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            isActive = false;
            e.Use();
            return;
        }

        // 押している間は、クリックをこちらで受け取る(Unityの選択操作にしない)
        int control = GUIUtility.GetControlID(_controlHash, FocusType.Passive);
        if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(control);

        bool hasGround = TryGetGround(HandleUtility.GUIPointToWorldRay(e.mousePosition), player, out Vector3 ground);
        switch (e.type)
        {
            case EventType.MouseMove:
                sceneView.Repaint();
                break;

            case EventType.MouseDown:
                if (e.button != 0 || e.alt || HandleUtility.nearestControl != control) break;
                if (hasGround) Teleport(player, ground);
                e.Use();
                break;

            case EventType.Repaint:
                if (!hasGround) break;
                Handles.color = _markerColor;
                Handles.DrawWireDisc(ground + Vector3.up * _groundMargin, Vector3.up, _markerRadius, 3f);
                Handles.DrawAAPolyLine(2f, ground, ground + Vector3.up * 2f);
                Handles.Label(ground + Vector3.up * 2.2f, "ここへテレポート");
                break;
        }
    }

    // Sceneビューの中心の真下の地面へテレポートする
    public static void TeleportToSceneViewCenter(SceneView sceneView)
    {
        CS_Player player = CS_Player.localPlayer;
        if (player == null || sceneView == null) return;

        Ray ray = new Ray(sceneView.pivot + Vector3.up * _rayDistance * 0.5f, Vector3.down);
        if (TryGetGround(ray, player, out Vector3 ground)) Teleport(player, ground);
        else Debug.LogWarning("CSED_PlayerTeleportTool: Sceneビューの中心の真下に地面が見つかりません");
    }

    // rayが最初に当たる地面(トリガーと自分のプレイヤーのコライダーは除く)
    private static bool TryGetGround(Ray ray, CS_Player player, out Vector3 ground)
    {
        ground = default;
        RaycastHit[] hits = Physics.RaycastAll(ray, _rayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        foreach (RaycastHit hit in hits)
        {
            if (hit.distance >= nearest || hit.collider.transform.IsChildOf(player.transform)) continue;
            nearest = hit.distance;
            ground = hit.point;
        }
        return nearest < float.MaxValue;
    }

    // 足元がgroundに来るよう、プレイヤーを移す(CS_PhaseSwapと同じ移し方)
    private static void Teleport(CS_Player player, Vector3 ground)
    {
        Vector3 destination = ground + Vector3.up * (GetFootOffset(player) + _groundMargin);
        player.SnapPosition(destination);

        if (player.IsSpawned && player.TryGetComponent(out NetworkTransform networkTransform))
        {
            networkTransform.Teleport(destination, player.transform.rotation, player.transform.localScale);
        }
    }

    // プレイヤーの位置(体の中心)から足元までの高さ
    private static float GetFootOffset(CS_Player player)
    {
        float offset = 0f;
        List<Collider> colliders = new List<Collider>();
        player.GetComponentsInChildren(colliders);
        foreach (Collider collider in colliders)
        {
            if (collider.isTrigger || !collider.enabled) continue;
            offset = Mathf.Max(offset, player.transform.position.y - collider.bounds.min.y);
        }
        return offset;
    }
}

// 再生中だけSceneビューに出る、テレポートのツールバー
[Overlay(typeof(SceneView), "PlayerTeleportOverlay", "テレポート", defaultDisplay = true)]
public class CSED_PlayerTeleportOverlay : Overlay, ITransientOverlay
{
    private IMGUIContainer _container;

    public bool visible => EditorApplication.isPlaying;

    public override VisualElement CreatePanelContent()
    {
        _container = new IMGUIContainer(OnGUI);
        _container.style.minWidth = 200f;
        return _container;
    }

    private void OnGUI()
    {
        CS_Player player = CS_Player.localPlayer;
        if (player == null)
        {
            EditorGUILayout.LabelField("自分のプレイヤーがいません", EditorStyles.miniLabel);
            return;
        }

        bool isActive = CSED_PlayerTeleportTool.isActive;
        if (GUILayout.Toggle(isActive, "クリックでテレポート", EditorStyles.miniButton) != isActive)
        {
            CSED_PlayerTeleportTool.isActive = !isActive;
        }
        if (GUILayout.Button("Sceneビューの中心へ", EditorStyles.miniButton))
        {
            CSED_PlayerTeleportTool.TeleportToSceneViewCenter(containerWindow as SceneView ?? SceneView.lastActiveSceneView);
        }

        EditorGUILayout.LabelField(CSED_PlayerTeleportTool.isActive ? "地面をクリック(Escで解除)" : $"位置 {player.transform.position:F1}", EditorStyles.miniLabel);
        _container?.MarkDirtyRepaint();
    }
}
