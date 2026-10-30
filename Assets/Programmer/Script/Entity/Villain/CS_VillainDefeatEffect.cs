using UnityEngine;

/*
 * 悪人を倒した時(撃破)に、体の位置へエフェクトを出すクラス
 * CS_VillainHealthの倒れた状態を見て、CS_EffectManagerで自分の画面に出すだけ
 * (ゲームロジック側はこのクラスの存在を知らない。外しても動きは変わらない)
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・出すエフェクト: VillainDefeat(見た目はDB_EffectDataBaseで決める)
 * ・倒れた状態(isDead)は全員に同期されている(NetworkVariable)ので、各クライアントが自分の画面に出す(通信しない)
 * ・倒れたかは毎フレーム前の状態と比べて調べる(オフラインでは変更の通知が来ないため)
 *   最初のフレームは、同期された状態を覚えるだけにする(途中から参加した時に、倒れている最中の悪人で出ないように)
 * ・出す位置は足元から Height の高さから、カメラの方へ Camera Offset だけ近づけた所
 *   (倒れるとColliderが無効になるので、Colliderの中心は使わない)
 */
// ========================================

[RequireComponent(typeof(CS_VillainHealth))]
public class CS_VillainDefeatEffect : MonoBehaviour
{
    [SerializeField] private float _height = 1f;         // エフェクトを出す高さ(足元から。m)
    [SerializeField] private float _cameraOffset = 0.5f; // 体の中心から、カメラの方へ近づける距離(m)

    private CS_VillainHealth _health;
    private bool _hasChecked;
    private bool _wasDead;

    private void Awake()
    {
        _health = GetComponent<CS_VillainHealth>();
    }

    private void LateUpdate()
    {
        bool isDead = _health.isDead;
        bool isDefeated = _hasChecked && !_wasDead && isDead;
        _wasDead = isDead;
        _hasChecked = true;

        if (!isDefeated) return;

        CS_EffectManager manager = CS_EffectManager.instance;
        if (manager == null) return;

        manager.Play(CSE_EffectId.VillainDefeat, GetEffectPosition(), transform.rotation);
    }

    // 体の中心から、カメラの方へ少し近づけた位置(体に隠れないように)
    private Vector3 GetEffectPosition()
    {
        Vector3 center = transform.position + Vector3.up * _height;

        Camera mainCamera = Camera.main;
        if (mainCamera == null) return center;

        Vector3 toCamera = mainCamera.transform.position - center;
        if (toCamera.sqrMagnitude < 0.0001f) return center;

        return center + toCamera.normalized * Mathf.Min(_cameraOffset, toCamera.magnitude);
    }
}
