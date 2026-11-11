/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-10-28 | 初回作成(CS_RoomConnectionUIの表示の状態をMVPに分けた)
 * ================================================ */

using R3;

/// <summary>
/// 部屋に入るシーン(RoomConnection)の画面の Model。
///
/// ・状態表示の文字と、ボタン・入力欄を押せるかどうかを ReactiveProperty で保持する
/// ・CS_RoomConnectionController が new して持ち、Presenter に渡す
///
/// ※ ModelはUIのクラスを一切参照しない(UI → Model の一方向)
/// </summary>
public sealed class CS_UIRoomConnectionModel : CS_BaseModel
{
    private readonly ReactiveProperty<string> _status = new(string.Empty);
    private readonly ReactiveProperty<bool> _isInteractable = new(true);

    /// <summary>
    /// 状態表示の文字(英語。TMPのフォントが日本語非対応のため)
    /// </summary>
    public ReadOnlyReactiveProperty<string> status => _status;

    /// <summary>
    /// ボタン・入力欄を押せるか(接続中は押せない)
    /// </summary>
    public ReadOnlyReactiveProperty<bool> isInteractable => _isInteractable;

    public void SetStatus(string message)
    {
        _status.Value = message ?? string.Empty;
    }

    public void SetInteractable(bool value)
    {
        _isInteractable.Value = value;
    }

    public override void Dispose()
    {
        _status.Dispose();
        _isInteractable.Dispose();
    }
}
