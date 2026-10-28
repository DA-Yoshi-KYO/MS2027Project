using UnityEngine;

/*
 * 悪人の攻撃判定が出た瞬間に、こぶしの位置へエフェクトを出すクラス
 * CS_VillainCombatの攻撃の段階を見て、CS_EffectManagerで自分の画面に出すだけ
 * (ゲームロジック側はこのクラスの存在を知らない。外しても攻撃は動く)
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・出すエフェクト: VillainAttack(見た目はDB_EffectDataBaseで決める)
 *   攻撃の予告(赤い光。CS_VillainAttackVisual)が消えるのと同じ瞬間に出る。当たったかどうかは関係ない
 * ・攻撃の段階(CS_VillainCombat.attackPhase)は全員に同期されている(NetworkVariable)ので、
 *   各クライアントが自分の画面に出す(通信しない)
 * ・出す位置 = Bone(既定: 右手 hand.r)の位置 + Offset(悪人の向き基準。X: 右 / Y: 上 / Z: 前)
 *   ボーンが見つからない時は悪人の位置を基準にする(予告エフェクトと同じ決め方)
 */
// ========================================

[RequireComponent(typeof(CS_VillainCombat))]
public class CS_VillainAttackImpactEffect : MonoBehaviour
{
    [SerializeField] private string _bone = "hand.r";                    // エフェクトを出す基準のボーン名
    [SerializeField] private Vector3 _offset = new Vector3(0f, 0f, 0.3f); // 基準からのずれ(m。悪人の向き基準)

    private CS_VillainCombat _combat;
    private Transform _anchor;
    private CSE_VillainAttackPhase _previousPhase;

    private void Awake()
    {
        _combat = GetComponent<CS_VillainCombat>();
        _anchor = FindChild(transform, _bone);
        if (_anchor == null) _anchor = transform;
    }

    private void Update()
    {
        // 逃走などで反撃が止まっている時は出さない(CS_VillainAttackVisualと同じ)
        CSE_VillainAttackPhase phase = _combat.enabled ? _combat.attackPhase : CSE_VillainAttackPhase.None;
        bool isHitStarted = phase == CSE_VillainAttackPhase.Hit && _previousPhase != CSE_VillainAttackPhase.Hit;
        _previousPhase = phase;

        if (!isHitStarted) return;

        CS_EffectManager manager = CS_EffectManager.instance;
        if (manager == null) return;

        Vector3 position = _anchor.position + transform.rotation * _offset;
        manager.Play(CSE_EffectId.VillainAttack, position, transform.rotation);
    }

    private void OnDisable()
    {
        _previousPhase = CSE_VillainAttackPhase.None;
    }

    // 名前の一致する子孫を探す(名前が空・見つからなければnull)
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
