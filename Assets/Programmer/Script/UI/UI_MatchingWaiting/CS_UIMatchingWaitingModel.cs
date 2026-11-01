/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-10-28 | 初回作成(CS_MatchingWaitingUIの表示の状態をMVPに分けた)
 * ================================================ */

using R3;

/// <summary>
/// 待機シーン(WaitingforaMatch)の画面の Model。
///
/// ・参加人数・最大人数、開始ボタンを出すか、ボタンを押せるかを ReactiveProperty で保持する
/// ・CS_MatchingWaitingController が new して持ち、Presenter に渡す
///
/// ※ ModelはUIのクラスを一切参照しない(UI → Model の一方向)
/// </summary>
public sealed class CS_UIMatchingWaitingModel : CS_BaseModel
{
    private readonly ReactiveProperty<int> _playerCount = new(0);
    private readonly ReactiveProperty<int> _maxPlayers = new(4);
    private readonly ReactiveProperty<bool> _isStartVisible = new(false);
    private readonly ReactiveProperty<bool> _isInteractable = new(true);

    /// <summary>
    /// 今の参加人数
    /// </summary>
    public ReadOnlyReactiveProperty<int> playerCount => _playerCount;

    /// <summary>
    /// 入れる最大人数
    /// </summary>
    public ReadOnlyReactiveProperty<int> maxPlayers => _maxPlayers;

    /// <summary>
    /// 開始ボタンを出すか(ホストだけ出す)
    /// </summary>
    public ReadOnlyReactiveProperty<bool> isStartVisible => _isStartVisible;

    /// <summary>
    /// ボタンを押せるか(開始した後は押せない)
    /// </summary>
    public ReadOnlyReactiveProperty<bool> isInteractable => _isInteractable;

    public void SetPlayerCount(int count, int max)
    {
        _maxPlayers.Value = max;
        _playerCount.Value = count;
    }

    public void SetStartVisible(bool value)
    {
        _isStartVisible.Value = value;
    }

    public void SetInteractable(bool value)
    {
        _isInteractable.Value = value;
    }

    public override void Dispose()
    {
        _playerCount.Dispose();
        _maxPlayers.Dispose();
        _isStartVisible.Dispose();
        _isInteractable.Dispose();
    }
}
