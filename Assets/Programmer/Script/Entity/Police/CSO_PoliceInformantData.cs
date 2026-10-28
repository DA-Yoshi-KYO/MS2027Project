/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-28 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// 内通フォン(他プレイヤーの近くに警察を呼ぶアイテム)のデータを管理するScriptableObject
/// </summary>
[CreateAssetMenu(fileName = "DB_PoliceInformant", menuName = "Police/Informant Data")]
public class CSO_PoliceInformantData : ScriptableObject
{
    [Header("＝＝＝ 呼ばれる警察 ＝＝＝")]
    [SerializeField]
    [Tooltip("呼ばれる警察のプレハブ(CS_PoliceBrainが付いたもの)")]
    private CS_PoliceBrain _policePrefab = null;
    public CS_PoliceBrain policePrefab => _policePrefab;

    [SerializeField]
    [Tooltip("呼ばれる警察のステータス(速度・視野・攻撃力)")]
    private CSO_PoliceStatus _policeStatus = null;
    public CSO_PoliceStatus policeStatus => _policeStatus;

    [SerializeField]
    [Tooltip("速度倍率の基準にするプレイヤーのステータス(moveSpeedを基準速度にする)")]
    private CSO_PlayerStats _playerBaseStats = null;
    public CSO_PlayerStats playerBaseStats => _playerBaseStats;

    [SerializeField, Min(1)]
    [Tooltip("他プレイヤー1人につき、呼ばれる警察の人数")]
    private int _policeCountPerPlayer = 1;
    public int policeCountPerPlayer => _policeCountPerPlayer;

    [Header("＝＝＝ 出現位置 ＝＝＝")]
    [SerializeField, Min(0f)]
    [Tooltip("プレイヤーから、警察が出現する位置までの最小の距離(m)。警察の視野距離より遠くにすると、出現した瞬間には見つからない")]
    private float _spawnDistanceMin = 12.0f;
    public float spawnDistanceMin => _spawnDistanceMin;

    [SerializeField, Min(0f)]
    [Tooltip("プレイヤーから、警察が出現する位置までの最大の距離(m)")]
    private float _spawnDistanceMax = 18.0f;
    public float spawnDistanceMax => _spawnDistanceMax;

    [Header("＝＝＝ 追跡 ＝＝＝")]
    [SerializeField, Min(0.1f)]
    [Tooltip("プレイヤーの現在地を取得して追跡する時間(秒)。この間にプレイヤーを見つけて警戒状態にできなければ、警察は消える")]
    private float _homingDuration = 10.0f;
    public float homingDuration => _homingDuration;
}
