using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

/*
 * 悪人のHPと撃退処理を管理するクラス
 * プレイヤーの攻撃やアイテムからのダメージをIDamageable経由で受け取り、
 * HPが0になったら撃退し、死亡モーションを見せてから消す(Destroy)
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・HP上限はCS_VillainStats.maxHpを使う(実際の変更はCS_VillainStats側で行う)
 * ・現在HPはNetworkVariableで持つ(書き込みはサーバーのみ、読み取りは全員可)
 *   → 敵体力UIはonHpChangedを購読して使う
 * ・撃退の流れ(サーバー、またはオフラインで実行)
 *   1. HPが0になる
 *   2. onDefeated / onAnyVillainDefeated を呼ぶ
 *      → スコア加算はスコア側がonAnyVillainDefeatedを購読して行う
 *   3. 倒れた状態(isDead)にする。全クライアントで、反撃(CS_VillainCombat)と当たり判定を止める
 *      → 見た目側(CS_VillainVisual)はisDeadを見て死亡モーションを再生する
 *   4. despawnDelay 秒後(死亡モーションを見せた後)に、ネットワーク時はDespawn、オフライン時はDestroyする
 * ・isDefeated は倒れてから消えるまでの間もtrue(全クライアントで参照できる)
 *   警察・NPC・エイムアシストなどは、これを見て倒れている悪人を無視する
 * ・攻撃者付きのTakeDamage(damage, attacker)で受けると、onDamagedByで「誰が・実際にどれだけ減らしたか」を知らせる
 *   (攻撃者無しのTakeDamage(damage)はattackerがnullになる)。レイドのボスの貢献度の記録に使う
 * ・CS_VillainStatsより後に初期化する必要があるため、
 *   コンポーネントはCS_VillainStatsより下にアタッチする(RequireComponentで自動追加した場合はそうなる)
 */
// ========================================

[RequireComponent(typeof(CS_VillainStats))]
[DefaultExecutionOrder(1)] // オフライン時、CS_VillainStats.Startの後にStartを呼ぶため
public class CS_VillainHealth : NetworkBehaviour, IDamageable
{
    [SerializeField, Min(0f)]
    [Tooltip("撃退されてから消えるまでの時間(秒)。死亡モーションを見せる時間")]
    private float _despawnDelay = 1.5f;

    private CS_VillainStats _stats;
    private bool _isDefeated;

    // 書き込みはサーバーのみ(NetworkVariableのデフォルト)。読み取りは全員可
    private readonly NetworkVariable<float> _currentHp = new NetworkVariable<float>();
    private readonly NetworkVariable<bool> _isDead = new NetworkVariable<bool>();   // 倒れてから消えるまでの間か

    public float maxHp => _stats.maxHp;
    public float currentHp => _currentHp.Value;
    public bool isDefeated => _isDefeated || _isDead.Value;   // 撃退済みか(倒れてから消えるまでの間も含む。全クライアントで参照可)
    public bool isDead => _isDead.Value;                      // 倒れていて、死亡モーションを再生する間か(全クライアントで参照可)

    public event Action<float, float> onHpChanged;   // (current, max)
    public event Action onDamaged;                    // ダメージを受けて撃退されなかった時(サーバーのみ)
    public event Action onDefeated;                   // この悪人が撃退された時(サーバーのみ)
    public event Action<GameObject, float> onDamagedBy;   // (攻撃者 / 分からなければnull, 実際に減ったHP)。撃退より先に呼ばれる(サーバーのみ)。反撃の開始に使う

    // どの悪人が撃退されても呼ばれる(サーバーのみ)。スコア加算など、悪人全体を見る側の購読用
    public static event Action<CS_VillainHealth> onAnyVillainDefeated;

    private void Awake()
    {
        _stats = GetComponent<CS_VillainStats>();
    }

    // オフライン(NetworkManagerが動いていない)のテストシーン用
    private void Start()
    {
        if (IsSpawned) return;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return;

        _currentHp.Value = maxHp;
    }

    public override void OnNetworkSpawn()
    {
        _currentHp.OnValueChanged += HandleHpChanged;
        _isDead.OnValueChanged += HandleDeadChanged;
        _stats.onMaxHpChanged += HandleMaxHpChanged;

        // 途中参加したクライアントも、倒れている悪人は反撃・当たり判定を止める
        if (_isDead.Value) StopActing();

        if (IsServer)
        {
            _currentHp.Value = maxHp;
        }
    }

    public override void OnNetworkDespawn()
    {
        _currentHp.OnValueChanged -= HandleHpChanged;
        _isDead.OnValueChanged -= HandleDeadChanged;
        _stats.onMaxHpChanged -= HandleMaxHpChanged;
    }

    // IDamageable実装。攻撃側から呼ばれる(サーバー、またはオフラインで実行される想定)
    public void TakeDamage(float damage)
    {
        TakeDamage(damage, null);
    }

    // IDamageable実装(攻撃者付き)。プレイヤーの攻撃などは、こちらで攻撃者を渡してくる
    // attackerが分からないダメージ(nullの時)もHPは減る
    public void TakeDamage(float damage, GameObject attacker)
    {
        // ネットワーク時はサーバーのみが処理する(オフラインはそのまま通す)
        if (IsSpawned && !IsServer) return;
        if (_isDefeated) return;
        if (damage <= 0f) return;

        // 実際に減ったHP(残りHPを超えた分は含めない)を、攻撃者付きで知らせる(レイドの貢献度の記録などに使う)
        float appliedDamage = Mathf.Min(damage, _currentHp.Value);
        _currentHp.Value = Mathf.Max(0f, _currentHp.Value - damage);
        onDamagedBy?.Invoke(attacker, appliedDamage);

        if (_currentHp.Value <= 0f)
        {
            Defeat();
            return;
        }

        onDamaged?.Invoke();
    }

    // 撃退処理。通知して倒れた状態にし、死亡モーションを見せてから消す
    private void Defeat()
    {
        _isDefeated = true;
        _isDead.Value = true;

        onDefeated?.Invoke();
        onAnyVillainDefeated?.Invoke(this);

        // オフラインではOnValueChangedが呼ばれないので、ここで直接止める
        if (!IsSpawned) StopActing();

        StartCoroutine(DespawnAfterDelay());
    }

    private void HandleDeadChanged(bool previous, bool current)
    {
        if (current) StopActing();
    }

    // 倒れたら反撃(追跡・攻撃)と当たり判定を止める(全クライアント)
    // 当たり判定を消すので、倒れている間は攻撃が当たらず、プレイヤーの通り道もふさがない
    private void StopActing()
    {
        CS_VillainCombat combat = GetComponent<CS_VillainCombat>();
        if (combat != null) combat.enabled = false;

        foreach (Collider col in GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }
    }

    // 死亡モーションを見せてから消す(サーバー、またはオフライン)
    private IEnumerator DespawnAfterDelay()
    {
        yield return new WaitForSeconds(_despawnDelay);

        // Despawn(true)でサーバー・クライアント両方のオブジェクトが破棄される
        if (IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void HandleHpChanged(float previous, float current)
    {
        onHpChanged?.Invoke(current, maxHp);
    }

    // HP上限を変えた後に呼ぶ(サーバー、またはオフライン)
    // 上限が増えた分だけ現在HPも増やし(受けたダメージはそのまま残す)、上限を超えていれば切り詰める
    // (onMaxHpChangedはオフラインでは呼ばれないので、上限を変えた側から直接呼ぶ)
    public void ApplyMaxHpChange(float previousMaxHp)
    {
        if (IsSpawned && !IsServer) return;
        if (_isDefeated) return;

        float increase = Mathf.Max(0f, maxHp - previousMaxHp);
        _currentHp.Value = Mathf.Min(maxHp, _currentHp.Value + increase);
    }

    // HP上限が変わったとき、現在HPが上限を超えないようにする(サーバーのみ)
    private void HandleMaxHpChanged(float newMaxHp)
    {
        if (IsSpawned && !IsServer) return;
        if (_currentHp.Value <= newMaxHp) return;

        _currentHp.Value = newMaxHp;
    }
}
