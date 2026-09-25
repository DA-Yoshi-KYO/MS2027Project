using UnityEngine;

/*
 * 悪人グループのスポーン位置
 * プランナーがフィールド(路地裏など、警察が巡回しないエリア)に配置する
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・グループのメンバーは、この位置を中心に半径groupRadiusの円周上に並べて生成される
 * ・生成されたグループが1人でも残っている間は使用中になり、次のグループは生成されない
 *   (使用中かどうかの管理はCS_VillainSpawnerが行う)
 * ・グループが全滅すると、しばらくリスポーンを待つ状態(isCoolingDown)になる
 *   待ち時間の開始と、待ち中でも生成してよいかの判断はCS_VillainSpawnerが行う
 * ・Sceneビューで選択すると、生成範囲が赤い円で表示される
 */
// ========================================

public class CS_VillainSpawnPoint : MonoBehaviour
{
    [SerializeField, Min(0f)]
    [Tooltip("グループのメンバーを並べる円の半径(m)")]
    private float _groupRadius = 1.5f;

    private bool _isOccupied;
    private float _cooldownEndTime;   // リスポーンの待ちが終わる時刻(Time.time)

    public float groupRadius => _groupRadius;

    // グループが生成されていて、まだ残っているか
    public bool isOccupied
    {
        get => _isOccupied;
        set => _isOccupied = value;
    }

    // グループの全滅後、リスポーンを待っている間か
    public bool isCoolingDown => Time.time < _cooldownEndTime;
    public float cooldownEndTime => _cooldownEndTime;

    // 全滅したので、duration秒間リスポーンを待つ
    public void StartCooldown(float duration)
    {
        _cooldownEndTime = Time.time + duration;
    }

    // グループのi人目(0始まり)を置く位置を返す
    public Vector3 GetMemberPosition(int index, int memberCount)
    {
        float angle = 360f / memberCount * index;
        Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * _groupRadius;
        return transform.position + offset;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _groupRadius);
    }
}
