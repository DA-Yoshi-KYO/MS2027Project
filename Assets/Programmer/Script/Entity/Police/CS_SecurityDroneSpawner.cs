/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-21 | 初回作成
 * ================================================ */

using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 巡回ドローンを出現させるクラス。プランナーが指定した場所に置いて使う
/// ・ゲーム開始時に、この位置にドローンを1機出現させ、巡回ポイント・巡回範囲・データを渡す
/// ・巡回ポイントは、3Dの位置のTransform(高さも使う)を順に並べる。この子オブジェクトにすると管理しやすい
/// 出現はサーバー(またはオフライン)だけで行い、ネットワーク接続中はクライアントにも出現させる
/// </summary>
public class CS_SecurityDroneSpawner : MonoBehaviour
{
    // 出現させたドローン
    private CS_SecurityDrone _drone = null;

    [Header("＝＝＝ 出現 ＝＝＝")]
    [SerializeField]
    [Tooltip("出現させるドローンのプレハブ(SecurityDrone)")]
    private CS_SecurityDrone _dronePrefab = null;

    [SerializeField]
    [Tooltip("巡回ドローンのデータ(速度の倍率・視界・追跡・スタン)")]
    private CSO_SecurityDroneData _data = null;

    [SerializeField]
    [Tooltip("速度の基準にするプレイヤーのステータス(moveSpeedを基準速度にする)")]
    private CSO_PlayerStats _playerBaseStats = null;

    [Header("＝＝＝ 巡回 ＝＝＝")]
    [SerializeField]
    [Tooltip("巡回ポイント(3D。順に回り、最後まで行ったら最初に戻る。高さも使う)")]
    private Transform[] _patrolPoints = new Transform[0];

    [SerializeField]
    [Tooltip("巡回範囲(任意)。ドローンはこの中から出ない。「2Fだけ」「裏道だけ」などにしたい時に、その場所を囲むコライダー(Trigger推奨)を設定する")]
    private Collider _patrolArea = null;

    private void Start()
    {
        if (CS_PoliceSquad.IsNetworkClientOnly()) return;

        if (!IsSettingValid())
        {
            Debug.LogError("CS_SecurityDroneSpawner: 未設定の項目があります(プレハブ・データ・プレイヤーのステータス・巡回ポイントに None が無いかも確認してください)", this);
            return;
        }

        _drone = Spawn();
        _drone.Setting(_data, _playerBaseStats, _patrolPoints, _patrolArea);
    }

    /// <summary>
    /// ドローンを出現させるメソッド(ネットワーク接続中のサーバーは、クライアントにも出現させる)
    /// </summary>
    private CS_SecurityDrone Spawn()
    {
        CS_SecurityDrone drone = Instantiate(_dronePrefab, transform.position, transform.rotation);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer
            && drone.TryGetComponent(out NetworkObject networkObject))
        {
            networkObject.Spawn(true);
        }
        return drone;
    }

    /// <summary>
    /// 必要な項目がすべて設定されているかを判定するメソッド
    /// </summary>
    private bool IsSettingValid()
    {
        if (_dronePrefab == null || _data == null || _playerBaseStats == null) return false;
        if (_patrolPoints.Length == 0) return false;

        // 配列の途中に未設定(None)の要素があると実行中にエラーになるので、出現前に弾く
        return !System.Array.Exists(_patrolPoints, point => point == null);
    }

    private void OnDrawGizmos()
    {
        // 巡回ポイントと、その順番(最後は最初へ戻る)をSceneビューに表示する
        if (_patrolPoints == null || _patrolPoints.Length == 0) return;

        Gizmos.color = new Color(1.0f, 0.8f, 0.2f, 0.9f);
        for (int i = 0; i < _patrolPoints.Length; i++)
        {
            Transform current = _patrolPoints[i];
            Transform next = _patrolPoints[(i + 1) % _patrolPoints.Length];
            if (current == null || next == null) continue;

            Gizmos.DrawSphere(current.position, 0.2f);
            Gizmos.DrawLine(current.position, next.position);
        }
    }
}
