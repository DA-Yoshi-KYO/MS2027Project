using UnityEngine;

/*
 * 攻撃1段分のデータ(ScriptableObject)
 * 1段目、2段目、3段目それぞれ別のアセットを作成して使う
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ■ 数値だけ変えたいとき
 *   Projectウィンドウで右クリック → Create → Combat → Attack Data でアセットを作り、数値を変える
 *
 * ■ ユニークな処理を足したいとき(攻撃力の変化、追加効果など)
 *   このクラスを継承し、以下をoverrideした新しいScriptableObjectを作る
 *     CalculateDamage : ダメージ量の計算(例: 弱った敵に特効、距離で威力を変える)
 *     OnHit           : 命中時の追加効果(例: ノックバック、状態異常)
 *   例)
 *     [CreateAssetMenu(menuName = "Combat/Attack Data (Finisher)")]
 *     public class CSO_AttackDataFinisher : CSO_AttackData
 *     {
 *         public override float CalculateDamage(AttackContext context, IDamageable target)
 *         {
 *             return base.CalculateDamage(context, target) * 2f;
 *         }
 *     }
 *
 * ■ 時間の考え方(秒)
 *   |-- hitDelay --|  ← 攻撃開始から判定が出るまで
 *   |------- duration -------|  ← 攻撃モーション全体(この間の入力は先行入力になる)
 *                            |-- comboWindow --|  ← 攻撃終了後、次の段へ派生できる受付時間
 */
// ========================================

[CreateAssetMenu(fileName = "DB_AttackData", menuName = "Combat/Attack Data")]
public class CSO_AttackData : ScriptableObject
{
    [Header("威力")]
    [SerializeField] private float _damage = 10f;

    [Header("時間(秒)")]
    [SerializeField] private float _hitDelay = 0.15f;       // 攻撃開始から判定が出るまで
    [SerializeField] private float _duration = 0.4f;        // 攻撃モーションの長さ
    [SerializeField] private float _comboWindow = 0.5f;     // 次の段へ派生できる受付時間

    [Header("攻撃範囲")]
    [SerializeField] private float _hitRange = 1.2f;        // 正面へどれだけ離れた位置に判定を出すか
    [SerializeField] private float _hitRadius = 0.9f;       // 判定(球)の半径

    [Header("必殺ゲージ")]
    [SerializeField] private float _gaugeGain = 10f;         // ヒット1回につき溜まる必殺ゲージ量

    public float damage => _damage;
    public float hitDelay => _hitDelay;
    public float duration => _duration;
    public float comboWindow => _comboWindow;
    public float hitRange => _hitRange;
    public float hitRadius => _hitRadius;
    public float gaugeGain => _gaugeGain;

    // ダメージ量を計算する(ユニークな計算をしたいときはoverrideする)
    public virtual float CalculateDamage(AttackContext context, IDamageable target)
    {
        return _damage;
    }

    // 命中したときの追加効果(ユニークな効果をつけたいときはoverrideする)
    public virtual void OnHit(AttackContext context, IDamageable target)
    {
    }

    // 判定が攻撃モーションの外に出ないよう、入力値を補正する
    private void OnValidate()
    {
        _duration = Mathf.Max(0.05f, _duration);
        _hitDelay = Mathf.Clamp(_hitDelay, 0f, _duration);
        _comboWindow = Mathf.Max(0f, _comboWindow);
        _gaugeGain = Mathf.Max(0f, _gaugeGain);
    }
}
