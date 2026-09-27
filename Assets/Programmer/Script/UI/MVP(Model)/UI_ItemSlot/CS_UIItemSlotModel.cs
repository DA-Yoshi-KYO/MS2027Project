/* ================================================
 *
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-27 | 初回作成
 * 2026-09-28 | アイコンをReactivePropertyで持ち、SetIconだけで表示が変わるように変更
 * ================================================ */

using System;
using R3;
using UnityEngine;

/// <summary>
/// アイテムスロットの Model。
///
/// ・現在セットされているアイテムのアイコン（Sprite）を ReactiveProperty で保持する
///   → SetIcon を呼ぶだけで View が自動で更新される（null ならスロットは空として非表示）
/// ・Bind / Unbind のイベントを発火して Presenter に通知する
/// ・CS_BaseModel を継承して Dispose を持つ
///
/// ※ ModelはUIのクラスを一切参照しない(UI → Model の一方向)
/// </summary>
public class CS_UIItemSlotModel : CS_BaseModel
{
    // ---------------- 公開されたModel ----------------

    /// <summary>
    /// 現在スロットに Bind されている Model
    /// アイテムスロットは 1 個想定なので static で保持する
    /// </summary>
    private static CS_UIItemSlotModel _current;

    /// <summary>
    /// Bind された時に通知するイベント
    /// Presenter はこれを受け取ってアイコンの購読を始める
    /// </summary>
    public static event Action<CS_UIItemSlotModel> OnBound;

    /// <summary>
    /// Bind が外れた時に通知するイベント
    /// Presenter はこれを受け取って View を非表示にする
    /// </summary>
    public static event Action OnUnbound;

    /// <summary>
    /// 現在の Model を取得する（存在しなければ false）
    /// Presenter が後から有効になった場合に使う
    /// </summary>
    public static bool TryGet(out CS_UIItemSlotModel model)
    {
        model = _current;
        return model != null;
    }

    //Domain Reloadを切っている場合に、前回の再生の状態が残らないようにする
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _current = null;
        OnBound = null;
        OnUnbound = null;
    }

    // ---------------- 状態 ----------------

    /// <summary>
    /// 内部で保持するアイコン画像（null = アイテム無し）
    /// </summary>
    private readonly ReactiveProperty<Sprite> _icon;

    /// <summary>
    /// 読み取り専用のアイコン画像
    /// Presenter はこれを購読して View に渡す
    /// </summary>
    public ReadOnlyReactiveProperty<Sprite> icon => _icon;

    public CS_UIItemSlotModel(Sprite initialIcon = null)
    {
        _icon = new ReactiveProperty<Sprite>(initialIcon);
    }

    // ---------------- 公開 ----------------

    /// <summary>
    /// このModelをスロットの表示元として公開する
    /// </summary>
    public void Bind()
    {
        _current = this;
        OnBound?.Invoke(this);
    }

    /// <summary>
    /// 公開をやめる（スロットは非表示になる）
    /// </summary>
    public void Unbind()
    {
        //別のModelで上書きされていなければ外す
        if (_current != this) return;

        _current = null;
        OnUnbound?.Invoke();
    }

    // ---------------- 値の変更 ----------------

    /// <summary>
    /// アイコンをセットする（null を渡すとスロットは空になり非表示）
    /// Bind 済みなら即座に表示が更新される
    /// </summary>
    public void SetIcon(Sprite sprite)
    {
        _icon.Value = sprite;
    }

    /// <summary>
    /// スロットを空にする（SetIcon(null) と同じ）
    /// </summary>
    public void ClearIcon()
    {
        SetIcon(null);
    }

    public override void Dispose()
    {
        Unbind();
        _icon.Dispose();
    }
}
