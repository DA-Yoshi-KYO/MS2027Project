/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-21 | 初回作成
 * 2026-11-01 | 増援(警戒状態の間、プレイヤーの近くに出る警察)の設定を追加
 * 2026-11-06 | 増援を、見つけた警察と挟む形の位置に、遠くから出すようにした(距離を伸ばし、並べる角度の間隔を追加)
 * 2026-11-06 | 追跡・目撃・照射されている時間による減点の設定(間隔・点数)と、レーザーに当たった時の減点を追加
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

    [Header("＝＝＝ 見られている時間の減点(警察に追跡される・ドローンに見られる・ライトに照らされる) ＝＝＝")]
    [SerializeField, Min(0.1f)]
    [Tooltip("警察に追跡されている間・ドローンに見られている間・ライトに照らされている間、この秒数ごとに減点する(秒)。それらが途切れたら、数えた時間は捨てる")]
    private float _exposurePenaltyInterval = 1.0f;
    public float exposurePenaltyInterval => _exposurePenaltyInterval;

    [SerializeField, Min(0)]
    [Tooltip("上の秒数ごとに減る点数。0なら減点しない")]
    private int _exposurePenaltyPoint = 1;
    public int exposurePenaltyPoint => _exposurePenaltyPoint;

    [Header("＝＝＝ レーザーに当たった時の減点 ＝＝＝")]
    [SerializeField, Min(0)]
    [Tooltip("防犯レーザーに当たった時に減る点数(当たるたびに1回)。0なら減点しない")]
    private int _laserPenaltyPoint = 1;
    public int laserPenaltyPoint => _laserPenaltyPoint;

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
    [Tooltip("増援が出る位置の、プレイヤーからの距離(m)。この距離の外縁に出す")]
    private float _reinforceDistance = 25.0f;
    public float reinforceDistance => _reinforceDistance;

    [SerializeField, Range(0f, 180f)]
    [Tooltip("増援を並べる角度の間隔(度)。増援は、プレイヤーから見て、見つけた警察の反対側(挟む形)に出る。複数いる時は、反対側の向きを中心に、この間隔で左右に並べる")]
    private float _reinforceSpreadAngle = 30.0f;
    public float reinforceSpreadAngle => _reinforceSpreadAngle;

    // 増援を出せるだけの設定(プレハブ・ステータス・基準ステータス・人数)が揃っているか
    public bool hasReinforcement => _reinforcePolice != null && _reinforceStatus != null && _playerBaseStats != null && _reinforceCount > 0;
}
