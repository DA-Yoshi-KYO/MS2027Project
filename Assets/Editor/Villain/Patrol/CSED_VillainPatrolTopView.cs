using UnityEditor;
using UnityEngine;

/*
 * 巡回エディタの「真上から見る」(Sceneビューを真上からの2D表示にする)
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・入れると、Sceneビューを真上からの平行投影にし、回転を固定して、路地裏・ルート・実行役が全部入る大きさに合わせる
 *   画面の上: 位置の基準が「路地裏」なら路地裏の縦(1の方向)、「スポーン位置」ならスポーン位置の前(Z+)
 * ・もう一度押すと、入れる前の視点(位置・向き・大きさ・投影・回転の固定)に戻す
 * ・入れたまま別のスポーン位置を選ぶと、そのスポーン位置に合わせ直す。スポーン位置以外を選ぶと元の視点に戻す
 * ・置く・動かす操作は普段どおり使える(Sceneビューの表示を変えるだけ)
 */
// ========================================

public static class CSED_VillainPatrolTopView
{
    private const float _frameMargin = 3f;   // 合わせる時の余白(m)
    private const float _minSize = 8f;       // 合わせる時の最小の大きさ

    private static SceneView _sceneView;
    private static bool _isActive;
    private static Vector3 _savedPivot;
    private static Quaternion _savedRotation;
    private static float _savedSize;
    private static bool _savedOrthographic;
    private static bool _savedRotationLocked;

    public static bool isActive => _isActive && _sceneView != null;

    public static void Toggle(SceneView sceneView)
    {
        if (isActive) Exit();
        else Enter(sceneView);
    }

    public static void Enter(SceneView sceneView)
    {
        if (sceneView == null) return;

        _sceneView = sceneView;
        _savedPivot = sceneView.pivot;
        _savedRotation = sceneView.rotation;
        _savedSize = sceneView.size;
        _savedOrthographic = sceneView.orthographic;
        _savedRotationLocked = sceneView.isRotationLocked;
        _isActive = true;
        Frame();
    }

    public static void Exit()
    {
        if (!_isActive) return;

        _isActive = false;
        if (_sceneView != null)
        {
            _sceneView.isRotationLocked = _savedRotationLocked;
            _sceneView.LookAt(_savedPivot, _savedRotation, _savedSize, _savedOrthographic, true);
        }
        _sceneView = null;
        CSED_VillainPatrolEditorState.NotifyChanged();
    }

    // 選んでいるスポーン位置の路地裏・ルート・実行役が全部入るよう、真上から合わせる
    public static void Frame()
    {
        if (!isActive) return;

        CS_VillainSpawnPoint spawnPoint = CSED_VillainPatrolEditorState.GetSpawnPoint();
        if (spawnPoint == null) return;

        bool hasAlley = CSED_VillainPatrolAlley.TryGet(spawnPoint, out CSED_VillainPatrolAlley alley);
        Bounds bounds = GetBounds(spawnPoint, hasAlley, alley);

        float yaw = spawnPoint.transform.eulerAngles.y;
        if (hasAlley && CSED_VillainPatrolEditorState.positionBasis == CSED_VillainPatrolEditorState.PositionBasis.Alley)
        {
            yaw = Quaternion.LookRotation(alley.length).eulerAngles.y;
        }

        float size = Mathf.Max(_minSize, Mathf.Max(bounds.extents.x, bounds.extents.z) + _frameMargin);
        _sceneView.isRotationLocked = true;
        _sceneView.LookAt(bounds.center, Quaternion.Euler(90f, yaw, 0f), size, true, true);
        CSED_VillainPatrolEditorState.NotifyChanged();
    }

    private static Bounds GetBounds(CS_VillainSpawnPoint spawnPoint, bool hasAlley, CSED_VillainPatrolAlley alley)
    {
        Bounds bounds = new Bounds(spawnPoint.transform.position, Vector3.zero);
        if (hasAlley)
        {
            bounds.Encapsulate(alley.origin);
            bounds.Encapsulate(alley.origin + alley.length + alley.width);
            bounds.Encapsulate(alley.origin + alley.length);
            bounds.Encapsulate(alley.origin + alley.width);
        }

        CS_VillainPatrolRoute route = spawnPoint.GetComponent<CS_VillainPatrolRoute>();
        CSO_VillainPatrolPattern pattern = route != null ? route.pattern : null;
        if (pattern == null) return bounds;

        foreach (CSO_VillainPatrolPattern.Route patrol in pattern.routes)
        {
            foreach (CSO_VillainPatrolPattern.Point point in patrol.points)
            {
                bounds.Encapsulate(route.ToWorld(point.localPosition));
            }
        }
        foreach (CSO_VillainPatrolPattern.Executor executor in pattern.executors)
        {
            bounds.Encapsulate(route.ToWorld(executor.localPosition));
            bounds.Encapsulate(route.ToWorld(executor.carryLocalPosition));
        }
        return bounds;
    }
}
