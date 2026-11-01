/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-21 | 初回作成
 * 2026-11-01 | 増援(警戒状態の間、プレイヤーの近くに出る警察)の設定を追加
 * ================================================ */

using UnityEngine;

/// <summary>
/// 警戒状態(プレイヤーが警察陣営に見つかった状態)のうち、警察側の設定を管理するScriptableObject
/// 警戒状態が続く時間は、プレイヤー側のCS_PlayerAlertで設定する
/// 警戒状態になったプレイヤーの近くに出る増援の設定も持つ
/// </summary>
[CreateAssetMenu(menuName = "Police/Alert Data")]
public class CSO_PoliceAlertData : ScriptableObject
{
    [SerializeField, Min(0f)]
    [Tooltip("付近の範囲(m)。プレイヤーが見つかった場所からこの距離以内にいる警察が、その場所へ向かう")]
    private float _alertRadius = 30.0f;
    public float alertRadius => _alertRadius;

    [Header("＝＝＝ 増援(警戒状態の間、プレイヤーの近くに出る警察) ＝＝＝")]
    [SerializeField]
    [Tooltip("増援の警察のプレハブ(CS_PoliceBrainが付いたもの)。未設定なら、増援は出ない")]
    private CS_PoliceBrain _reinforcePolice = null;
    public CS_PoliceBrain reinforcePolice => _reinforcePolice;

    [SerializeField]
    [Tooltip("増援の警察のステータス(速度・視野・攻撃力)")]
    private CSO_PoliceStatus _reinforceStatus = null;
    public CSO_PoliceStatus reinforceStatus => _reinforceStatus;

    [SerializeField]
    [Tooltip("速度倍率の基準にするプレイヤーのステータス(moveSpeedを基準速度にする)")]
    private CSO_PlayerStats _playerBaseStats = null;
    public CSO_PlayerStats playerBaseStats => _playerBaseStats;

    [SerializeField, Min(0)]
    [Tooltip("プレイヤーが警戒状態になった時に出る、増援の人数。0なら出ない")]
    private int _reinforceCount = 2;
    public int reinforceCount => _reinforceCount;

    [SerializeField, Min(0f)]
    [Tooltip("増援が出る位置の、プレイヤーからの距離(m)。この距離の外縁の、歩ける方向からランダムに選ぶ")]
    private float _reinforceDistance = 10.0f;
    public float reinforceDistance => _reinforceDistance;

    // 増援を出せるだけの設定(プレハブ・ステータス・基準ステータス・人数)が揃っているか
    public bool hasReinforcement => _reinforcePolice != null && _reinforceStatus != null && _playerBaseStats != null && _reinforceCount > 0;
}
