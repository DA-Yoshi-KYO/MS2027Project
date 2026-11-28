/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-28 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// 称号の条件の数値を入れておく ScriptableObject
/// ・コードを書き換えずに、Inspector で条件の回数を変えられる
/// ・Project 右クリック > Create > Result > Result Title Settings
///   → DB_ResultTitleSettings ができる。CS_UIResultView の _titleSettings に割り当てる
/// ・割り当てなかった場合は、CS_ResultTitleDecider の初期値(3回 / 50回 / 50回)で判定する
///
/// 条件と称号
///   警察に見つかった数が「警察いらずの上限」以下  → 警察いらずの暗躍者
///   奇襲の成功回数が「ワンショットの必要回数」以上 → ワンショットワンダウン
///   SAの使用回数が「名手の必要回数」以上          → ステルスアクションの名手
///   上の3つをすべて満たした                       → 稀代のダークヒーロー
/// </summary>
[CreateAssetMenu(menuName = "Result/Result Title Settings", fileName = "DB_ResultTitleSettings")]
public sealed class CSO_ResultTitleSettings : ScriptableObject
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("警察いらずの暗躍者")]
    [Tooltip("警察に見つかった数が、この回数「以下」なら獲得")]
    [Min(0)]
    [SerializeField]
    private int _policeFreeMaxCount = 3;

    [Header("ワンショットワンダウン")]
    [Tooltip("奇襲の成功回数が、この回数「以上」なら獲得")]
    [Min(1)]
    [SerializeField]
    private int _oneShotOneDownMinCount = 50;

    [Header("ステルスアクションの名手")]
    [Tooltip("SAの使用回数が、この回数「以上」なら獲得")]
    [Min(1)]
    [SerializeField]
    private int _stealthMasterMinCount = 50;

    [Header("稀代のダークヒーロー")]
    [Tooltip("OFF: 3つすべて満たした時、ダークヒーローに加えて3つの称号も出す(4つ)。ON: ダークヒーローだけ出す")]
    [SerializeField]
    private bool _showOnlyDarkHeroWhenAll = false;

    // =========================================================
    // 公開(読み取り専用)
    // =========================================================

    public int policeFreeMaxCount => _policeFreeMaxCount;
    public int oneShotOneDownMinCount => _oneShotOneDownMinCount;
    public int stealthMasterMinCount => _stealthMasterMinCount;
    public bool showOnlyDarkHeroWhenAll => _showOnlyDarkHeroWhenAll;
}
