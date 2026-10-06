/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-05 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// 手配度による警察の増援を、プレイヤー側の実装が無くても確認するためのデバッグ用クラス
/// ・Inspectorで手配度を変えると、CS_PoliceWantedManagerに知らせる
/// ・右上のメニュー(︙)の「信号を出す」で、プレイヤーの位置から信号を出す
/// ※デバッグ用なので、プレイヤー側の実装が入ったらシーンから外す
/// </summary>
public class CS_PoliceWantedDebugger : MonoBehaviour
{
    // プレイヤーを探し直す間隔(秒)。プレイヤーが出現するまで毎フレーム探さないようにする
    private const float _findInterval = 1.0f;

    // 最後に知らせた手配度
    private int _appliedLevel = 0;

    // 次にプレイヤーを探せるまでの残り時間
    private float _findTimer = 0.0f;

    [SerializeField]
    [Tooltip("手配度を変えるプレイヤー(空なら、シーンにいるプレイヤーを探して使う)")]
    private CS_PlayerHealth _player = null;

    [SerializeField, Range(0, 5)]
    [Tooltip("プレイヤーの手配度(変えるとCS_PoliceWantedManagerに知らせる)")]
    private int _wantedLevel = 0;

    private void Update()
    {
        if (_wantedLevel == _appliedLevel) return;
        if (!TryGetPlayer()) return;

        _appliedLevel = _wantedLevel;
        CS_PoliceWantedManager.SetWantedLevel(_player, _wantedLevel);
    }

    /// <summary>
    /// プレイヤーの位置から信号を出すメソッド(Inspectorのメニューから呼ぶ)
    /// </summary>
    [ContextMenu("信号を出す")]
    private void SendSignal()
    {
        if (!TryGetPlayer()) return;

        CS_PoliceSquad.NotifyIncident(_player.transform.position, _player);
    }

    /// <summary>
    /// 手配度を変えるプレイヤーを取得するメソッド
    /// </summary>
    /// <returns>プレイヤーがいればtrue</returns>
    private bool TryGetPlayer()
    {
        if (_player != null) return true;

        _findTimer -= Time.deltaTime;
        if (_findTimer > 0.0f) return false;
        _findTimer = _findInterval;

        _player = FindAnyObjectByType<CS_PlayerHealth>();
        return _player != null;
    }
}
