/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-10-19 | 初回作成
 * ================================================ */

using UnityEngine;

// ========================================
/*
 * メモ
 * ・遊べる範囲を表すクラス(ソロ用の配置プレハブに置く)
 * ・範囲は同じオブジェクトのBoxColliderの大きさで決める(Is Triggerにしておくこと)
 *   プレイヤーを範囲内に閉じ込めるのは、別に置く見えない壁の役目
 * ・警察の増援など、範囲の外に出現させたくない物がContains()で確認する
 *   範囲がシーンに無い時(マルチ)は、どこでも範囲内として扱う
 */
// ========================================
[RequireComponent(typeof(BoxCollider))]
public class CS_PlayArea : MonoBehaviour
{
    private static CS_PlayArea _instance;

    private BoxCollider _area;

    void Awake()
    {
        _area = GetComponent<BoxCollider>();
        _instance = this;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // 位置が遊べる範囲の中か(範囲が無ければ常にtrue)
    public static bool Contains(Vector3 position)
    {
        if (_instance == null || !_instance.isActiveAndEnabled) return true;

        // 高さは問わず、水平方向だけで判定する
        Bounds bounds = _instance._area.bounds;
        return position.x >= bounds.min.x && position.x <= bounds.max.x
            && position.z >= bounds.min.z && position.z <= bounds.max.z;
    }
}
