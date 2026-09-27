/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-27 | 初回作成
 * ================================================ */

using System;
using UnityEngine;

/// <summary>
/// アイテムスロットの Model。
/// 
/// ・現在セットされているアイテムの情報（Sprite）を保持する
/// ・Bind / Unbind のイベントを発火して Presenter に通知する
/// ・CS_BaseModel を継承して Dispose を持つ（購読解除用）
/// 
/// ※アイテムスロットは購読する値が無いので Dispose は空で OK
/// </summary>
public class CS_UIItemSlotModel : CS_BaseModel
{
    /// <summary>
    /// アイテムがセットされた時に通知するイベント
    /// Presenter はこれを受け取って View を更新する
    /// </summary>
    public static event Action<CS_UIItemSlotModel> OnBound;

    /// <summary>
    /// アイテムが外れた時に通知するイベント
    /// Presenter はこれを受け取って View を非表示にする
    /// </summary>
    public static event Action OnUnbound;

    /// <summary>
    /// 内部で保持するアイコン画像
    /// 外部から勝手に書き換えられないように private にする
    /// </summary>
    private Sprite _iconSprite;

    /// <summary>
    /// 読み取り専用のアイコン画像
    /// Presenter はこの値を View に渡すだけ
    /// </summary>
    public Sprite iconSprite => _iconSprite;

    /// <summary>
    /// 現在スロットにセットされている Model
    /// アイテムスロットは 1 個想定なので static で保持する
    /// </summary>
    private static CS_UIItemSlotModel _current;

    /// <summary>
    /// アイコンをセットする（外部から呼ばれる）
    /// </summary>
    public void SetIcon(Sprite sprite)
    {
        _iconSprite = sprite;
    }

    /// <summary>
    /// 現在の Model を取得する（存在しなければ false）
    /// Presenter が「初期状態で表示するか」を判断するために使う
    /// </summary>
    public static bool TryGet(out CS_UIItemSlotModel model)
    {
        model = _current;
        return model != null;
    }

    /// <summary>
    /// Model をバインドする（アイテムがセットされた）
    /// Presenter に通知する
    /// </summary>
    public void Bind()
    {
        _current = this;
        OnBound?.Invoke(this);
    }

    /// <summary>
    /// Model のバインドを解除する（アイテムが外れた）
    /// Presenter に通知する
    /// </summary>
    public void Unbind()
    {
        _current = null;
        OnUnbound?.Invoke();
    }

    /// <summary>
    /// Dispose（購読解除）
    /// 
    /// アイテムスロットは購読する値が無いので空で OK。
    /// ReactiveProperty や Observable を使うようになったらここに解除処理を書く。
    /// </summary>
    public override void Dispose()
    {
        // 今は購読しているものが無いので空で OK
    }
}
