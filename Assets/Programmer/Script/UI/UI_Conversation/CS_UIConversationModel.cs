/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-20 | 初回作成
 * ================================================ */

using R3;
using System;
using UnityEngine;

/// <summary>
/// 会話ウィンドウの状態を保持する Model
/// ・今出している文(text)と、表示中か(isVisible)を持つ
/// ・Bind() で自分を公開する。Presenter は OnBound で受け取って購読する
///   (Presenter / View のことは知らない)
/// ・Say(文, 秒数) が外から使う入口。固定会話・ランダム会話などはこれを呼ぶ
/// ・使う側が new して Bind し、使い終わったら Dispose する
/// </summary>
public class CS_UIConversationModel : CS_BaseModel
{
    // =========================================================
    // 公開された Model(static)
    // =========================================================

    // 今 Bind されている Model(会話ウィンドウは画面に1つなので1個だけ)
    private static CS_UIConversationModel _boundModel;

    // Bind された時の通知(Model)
    public static event Action<CS_UIConversationModel> OnBound;

    // Bind が外れた時の通知
    public static event Action OnUnbound;

    /// <summary>
    /// Bind 済みの Model を取得する
    /// 固定会話・ランダム会話など「Model を自分で作らない側」が、Say を呼ぶために使う
    /// </summary>
    public static bool TryGet(out CS_UIConversationModel model)
    {
        model = _boundModel;
        return model != null;
    }

    // Domain Reload 対策(Enter Play Mode 設定で static が残らないように)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _boundModel = null;
        OnBound = null;
        OnUnbound = null;
    }

    // =========================================================
    // 状態
    // =========================================================

    // 値を持つ変数。変更できるのはこのクラスの中だけ
    // ReactiveProperty = 値が変わった時に、購読している Presenter へ自動で知らせる変数
    private readonly ReactiveProperty<string> _text = new ReactiveProperty<string>(string.Empty);
    private readonly ReactiveProperty<bool> _isVisible = new ReactiveProperty<bool>(false);

    // 「文が来たよ」の通知。流れてくる値は、指定された表示秒数
    // (表示時間は Presenter が決めて計るので、Model は秒数を渡すだけ)
    private readonly Subject<float> _onSay = new Subject<float>();

    /// <summary>今出している文。非表示になっても文は残す(フェードアウト中に文字が消えないように)</summary>
    public ReadOnlyReactiveProperty<string> text => _text;

    /// <summary>ウィンドウを表示中かどうか</summary>
    public ReadOnlyReactiveProperty<bool> isVisible => _isVisible;

    /// <summary>Say が呼ばれた時の通知(値は指定秒数。0以下は「既定の秒数で」の意味)</summary>
    public Observable<float> onSay => _onSay;

    // =========================================================
    // 公開 / 公開解除
    // =========================================================

    /// <summary>
    /// 自分を会話ウィンドウ用の Model として公開する
    /// 既に別の Model が Bind されていた場合は、こちらに置き換える
    /// </summary>
    public void Bind()
    {
        if (_boundModel == this) return; // 二重 Bind は何もしない

        if (_boundModel != null)
        {
            Debug.LogWarning($"{nameof(CS_UIConversationModel)}: 既に Bind 済みの Model があるため置き換えます。");
        }

        _boundModel = this;
        OnBound?.Invoke(this);
    }

    /// <summary>公開を解除する(自分が公開中の Model でなければ何もしない)</summary>
    public void Unbind()
    {
        if (_boundModel != this) return;

        _boundModel = null;
        OnUnbound?.Invoke();
    }

    // =========================================================
    // 値の変更(外から使う入口)
    // =========================================================

    /// <summary>
    /// 文を表示する
    /// ・duration が 0 以下なら、Settings の既定秒数で消える
    /// ・表示中に呼ぶと、前の文は破棄されて新しい文に差し替わり、表示時間もやり直しになる
    /// </summary>
    public void Say(string newText, float duration = -1f)
    {
        // 空の文は表示する意味がないので無視する
        if (string.IsNullOrWhiteSpace(newText))
        {
            Debug.LogWarning($"{nameof(CS_UIConversationModel)}: 空の文が渡されたため無視します。");
            return;
        }

        _text.Value = newText;    // 文を更新 → View の文字が変わる
        _isVisible.Value = true;  // 表示中にする → View がフェードイン(既に true なら何も起きない)
        _onSay.OnNext(duration);  // Presenter に「消すタイマーをやり直して」と知らせる
    }

    /// <summary>
    /// 非表示にする(文は残す)
    /// ・Presenter が、表示時間が過ぎた時に呼ぶ
    /// ・外から「すぐ消したい」時に呼んでもよい
    /// </summary>
    public void Hide()
    {
        _isVisible.Value = false;
    }

    // =========================================================
    // 破棄
    // =========================================================

    public override void Dispose()
    {
        Unbind();
        _text.Dispose();
        _isVisible.Dispose();
        _onSay.Dispose();
    }
}
