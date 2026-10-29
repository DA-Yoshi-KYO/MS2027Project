using UnityEngine;

/*
 * パリーに成功した時に、体の位置へエフェクトを出すクラス
 * CS_PlayerParryのイベントを受けて、CS_EffectManagerで自分の画面に出すだけ
 * (ゲームロジック側はこのクラスの存在を知らない。外してもパリーは動く)
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・出すエフェクト: PlayerParry(見た目はDB_EffectDataBaseで決める)
 * ・パリーのイベント(CS_PlayerParry.onParried)は全クライアントで起きるので、各クライアントが自分の画面に出す(通信しない)
 * ・出す位置は体の中心(Colliderがあればその中心、無ければ足元から Height の高さ)から、カメラの方へ Camera Offset だけ近づけた所
 *   (体の中心のままだと、エフェクトが体のモデルに隠れてしまうため)
 * ・パリーの演出(スロー・画面端を暗くする)はCS_PlayerParryEffectが行う。こちらはエフェクトを出すだけ
 */
// ========================================

[RequireComponent(typeof(CS_PlayerParry))]
public class CS_PlayerParrySuccessEffect : MonoBehaviour
{
    [SerializeField] private float _height = 1f;         // Colliderが無い時に、エフェクトを出す高さ(足元から。m)
    [SerializeField] private float _cameraOffset = 0.5f; // 体の中心から、カメラの方へ近づける距離(m)

    private CS_PlayerParry _parry;
    private Collider _bodyCollider;

    private void Awake()
    {
        _parry = GetComponent<CS_PlayerParry>();
        _bodyCollider = GetComponent<Collider>();
    }

    private void OnEnable()
    {
        _parry.onParried += HandleParried;
    }

    private void OnDisable()
    {
        _parry.onParried -= HandleParried;
    }

    private void HandleParried(GameObject attacker)
    {
        CS_EffectManager manager = CS_EffectManager.instance;
        if (manager == null) return;

        manager.Play(CSE_EffectId.PlayerParry, GetEffectPosition(), transform.rotation);
    }

    // 体の中心から、カメラの方へ少し近づけた位置(体に隠れないように)
    private Vector3 GetEffectPosition()
    {
        Vector3 center = GetBodyCenter();

        Camera mainCamera = Camera.main;
        if (mainCamera == null) return center;

        Vector3 toCamera = mainCamera.transform.position - center;
        if (toCamera.sqrMagnitude < 0.0001f) return center;

        return center + toCamera.normalized * Mathf.Min(_cameraOffset, toCamera.magnitude);
    }

    private Vector3 GetBodyCenter()
    {
        if (_bodyCollider != null && _bodyCollider.enabled) return _bodyCollider.bounds.center;

        return transform.position + Vector3.up * _height;
    }
}
