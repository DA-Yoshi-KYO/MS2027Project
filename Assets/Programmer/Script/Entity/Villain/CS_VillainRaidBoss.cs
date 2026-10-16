using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/*
 * レイドのボス(強い個体)の、貢献度によるスコア配分を行うクラス
 * プレイヤーごとに与えたダメージを記録し、倒された時に点数を配分する
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・ボスのプレハブ(VillainBoss)に、CS_VillainHealthと一緒に付ける
 * ・配分(仕様: 合計600点)
 *   与えたダメージ × pointsPerDamage(10点) を、ダメージを与えた各プレイヤーに
 *   とどめを刺したプレイヤーに、さらに killBonus(300点)
 *   ・ダメージは実際に減ったHPで数える(残りHPを超えた分は数えない)
 *   ・攻撃者が分からないダメージ(爆弾など)はHPは減るが、誰の貢献度にも含めない
 *   ・暗躍ボーナス(連続撃破の倍率)は掛けない
 * ・点数はCS_PlayerResultDataHolder.AddDefeatVillainScoreで足す(倒した数は増やさない)
 *   通常の撃破スコアの仕組み(CS_PlayerScoring)では、とどめを刺した人に「倒した数+1」だけ入る
 *   (IDefeatScoreで撃破スコアを0点にして、通常の点数が二重に入らないようにしている)
 * ・記録と配分はサーバー(またはオフライン)で行う(ダメージの処理がサーバーで行われるため)
 * ・時間切れで逃げた場合(犯罪完遂)は配分しない
 */
// ========================================

[RequireComponent(typeof(CS_VillainHealth))]
public class CS_VillainRaidBoss : NetworkBehaviour, IDefeatScore
{
    [SerializeField, Min(0f)]
    [Tooltip("与えたダメージ1あたりの点数")]
    private float _pointsPerDamage = 10f;

    [SerializeField, Min(0)]
    [Tooltip("とどめを刺したプレイヤーへの追加の点数")]
    private int _killBonus = 300;

    private CS_VillainHealth _health;
    private readonly Dictionary<CS_PlayerResultDataHolder, float> _damageByPlayer = new Dictionary<CS_PlayerResultDataHolder, float>();
    private CS_PlayerResultDataHolder _lastAttacker;   // 直前にダメージを与えたプレイヤー(倒された時のとどめの判定に使う)

    // 通常の撃破スコアは0点(点数は配分で入れる)
    public int defeatScore => 0;

    private void Awake()
    {
        _health = GetComponent<CS_VillainHealth>();
    }

    private void OnEnable()
    {
        _health.onDamagedBy += HandleDamagedBy;
        _health.onDefeated += HandleDefeated;
    }

    private void OnDisable()
    {
        _health.onDamagedBy -= HandleDamagedBy;
        _health.onDefeated -= HandleDefeated;
    }

    // プレイヤーごとに、実際に減らしたHPを足していく(攻撃者が分からない・プレイヤーでない時は数えない)
    private void HandleDamagedBy(GameObject attacker, float appliedDamage)
    {
        CS_PlayerResultDataHolder player = attacker != null ? attacker.GetComponentInParent<CS_PlayerResultDataHolder>() : null;
        _lastAttacker = player;
        if (player == null || appliedDamage <= 0f) return;

        _damageByPlayer.TryGetValue(player, out float total);
        _damageByPlayer[player] = total + appliedDamage;
    }

    // 倒された時に、ダメージに応じた点数と、とどめの点数を配分する
    private void HandleDefeated()
    {
        foreach (KeyValuePair<CS_PlayerResultDataHolder, float> pair in _damageByPlayer)
        {
            if (pair.Key == null) continue;   // 途中で抜けたプレイヤー

            int point = Mathf.RoundToInt(pair.Value * _pointsPerDamage);
            if (point > 0) pair.Key.AddDefeatVillainScore(point);
        }

        if (_lastAttacker != null && _killBonus > 0) _lastAttacker.AddDefeatVillainScore(_killBonus);
    }
}
