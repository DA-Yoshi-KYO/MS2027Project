/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-19 | 初回作成
 * ================================================ */

using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 警察の攻撃の予告エフェクト(赤い光)を出すクラス
/// 悪人(CS_VillainAttackVisual)と同じエフェクトを使い、攻撃判定が出る一定時間前から出して、攻撃判定が出る時に消す
///   エフェクトを出し始める時刻 = 攻撃判定が出る時刻 - 予告エフェクトの時間
/// チャージの状態はサーバー(またはオフライン)にしか無いため、出す・消すをRPCで全員の画面に伝える
/// </summary>
[RequireComponent(typeof(CS_PoliceAttack))]
public class CS_PoliceAttackEffect : NetworkBehaviour
{
    private CS_PoliceAttack _attack = null;

    // 予告エフェクト(普段は非表示)
    private GameObject _effect = null;

    // エフェクトを出す基準の位置(ボーンが見つからない時は警察自身)
    private Transform _anchor = null;

    // サーバー(またはオフライン)が最後に出した指示(同じ指示を何度も送らないために使う)
    private bool _requestedState = false;

    [Header("＝＝＝ 攻撃の予告エフェクト ＝＝＝")]
    [SerializeField, Min(0f)]
    [Tooltip("攻撃判定が出る何秒前から、予告エフェクトを出すか(攻撃の溜め時間より長い場合は、溜めの最初から出す)")]
    private float _warningLeadTime = 0.3f;

    [SerializeField]
    [Tooltip("予告エフェクトのプレハブ(悪人の攻撃予告と同じ VillainAttackWarning を設定する)。未設定ならエフェクトは出ない")]
    private GameObject _warningEffectPrefab = null;

    [SerializeField]
    [Tooltip("エフェクトを出す基準のボーン名(こぶしなど)。空・見つからない時は警察の中心")]
    private string _warningEffectBone = "";

    [SerializeField]
    [Tooltip("基準からのずれ(m)。警察の向き基準で X: 右 / Y: 上 / Z: 前")]
    private Vector3 _warningEffectOffset = new Vector3(0.0f, 0.3f, 0.6f);

    private void Awake()
    {
        _attack = GetComponent<CS_PoliceAttack>();

        if (_warningEffectPrefab != null)
        {
            _effect = Instantiate(_warningEffectPrefab, transform);
            _effect.SetActive(false);
        }

        _anchor = FindChild(transform, _warningEffectBone);
        if (_anchor == null) _anchor = transform;
    }

    private void Update()
    {
        // チャージするのはサーバー(またはオフライン)だけ。クライアントはRPCで指示を受ける
        if (CS_PoliceSquad.IsNetworkClientOnly()) return;

        bool isWarning = _attack.isCharging && _attack.chargeRemaining <= _warningLeadTime;
        if (isWarning == _requestedState) return;

        _requestedState = isWarning;
        if (IsSpawned) SetWarningRpc(isWarning);
        else SetWarning(isWarning);
    }

    // アニメーションで動いた後のボーンに合わせるため、位置はLateUpdateで毎フレーム合わせる
    private void LateUpdate()
    {
        if (_effect == null || !_effect.activeSelf) return;

        _effect.transform.position = _anchor.position + transform.rotation * _warningEffectOffset;
    }

    private void OnDisable()
    {
        SetWarning(false);
    }

    // 全員(サーバー含む)で予告エフェクトを出す・消す
    [Rpc(SendTo.Everyone)]
    private void SetWarningRpc(bool isShown)
    {
        SetWarning(isShown);
    }

    /// <summary>
    /// 予告エフェクトを出す・消すメソッド
    /// </summary>
    /// <param name="isShown">出す場合はtrue</param>
    private void SetWarning(bool isShown)
    {
        if (_effect == null || _effect.activeSelf == isShown) return;

        _effect.SetActive(isShown);
    }

    /// <summary>
    /// 名前の一致する子孫を探すメソッド
    /// </summary>
    /// <param name="parent">探す元</param>
    /// <param name="childName">探す名前</param>
    /// <returns>見つかった子孫(名前が空・見つからなければnull)</returns>
    private static Transform FindChild(Transform parent, string childName)
    {
        if (string.IsNullOrEmpty(childName)) return null;

        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName) return child;
        }
        return null;
    }
}
