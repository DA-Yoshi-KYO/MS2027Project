/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-28 | 初回作成
 * ================================================ */

using R3;
using UnityEngine;

/// <summary>
/// 悪人撃退スコアポップアップ1件分のModel。
/// スコアと表示開始位置を保持する。
/// </summary>
public sealed class CS_UIVillainScorePopupModel : CS_BaseModel
{
    private readonly ReactiveProperty<int> _score = new(0);
    private readonly ReactiveProperty<Vector3> _worldPosition = new(Vector3.zero);
    private readonly Subject<Unit> _playRequested = new();

    public ReadOnlyReactiveProperty<int> score => _score;
    public ReadOnlyReactiveProperty<Vector3> worldPosition => _worldPosition;
    public Observable<Unit> playRequested => _playRequested;

    public int currentScore => _score.Value;
    public Vector3 currentWorldPosition => _worldPosition.Value;

    /// <summary>
    /// 表示内容を設定して、ポップアップ再生を要求する。
    /// </summary>
    public void Show(int score, Vector3 worldPosition)
    {
        _score.Value = score;
        _worldPosition.Value = worldPosition;
        _playRequested.OnNext(Unit.Default);
    }

    public override void Dispose()
    {
        _score.Dispose();
        _worldPosition.Dispose();
        _playRequested.OnCompleted();
        _playRequested.Dispose();
    }
}
