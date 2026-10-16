using UnityEngine;

/*
 * ランダムイベントの発生地点
 * プランナーがフィールドに5個くらい置き、CS_RandomEventManagerがその中からランダムに選ぶ
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・空のGameObjectに付けて、イベントを起こしたい場所に置く
 * ・Sceneビューでは常に黄色の円(イベントの範囲の目安)が表示される
 *   範囲(radius)は、各イベントが「発生地点の周り」に何かを置く時の目安として使う
 *   警察に探知されないイベント(blockPoliceDetection)では、この範囲の中が探知されない範囲になる
 * ・同じ地点で同時に2つのイベントは起きない(開催中の地点は抽選から外れる)
 */
// ========================================

public class CS_RandomEventPoint : MonoBehaviour
{
    [SerializeField, Min(0f)]
    [Tooltip("イベントの範囲の目安(m)。各イベントが周りに物や悪人を置く時に使う")]
    private float _radius = 5f;

    private bool _isOccupied;

    public float radius => _radius;

    // この地点でイベントが開催中か(管理はCS_RandomEventManagerが行う)
    public bool isOccupied
    {
        get => _isOccupied;
        set => _isOccupied = value;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, _radius);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 3f);
    }
}
