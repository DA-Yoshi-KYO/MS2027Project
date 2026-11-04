/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-10-28 | 初回作成(CS_PlayerScanEffectの仮の目印をMVPに分けた)
 * ================================================ */

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// スキャンの目印(スキャンで見えている悪人の頭上に出す印)の Model。
///
/// ・目印を出す相手(悪人の Transform)の一覧を保持する
///   → SetTargets を呼ぶだけで目印の数・位置が変わる(空なら目印なし)
///   相手が動くので、Presenter が毎フレーム一覧を読んで位置を追う
/// ・目印は自分の画面にだけ出すので、アイテムスロットと同じく Bind() で公開する(番号なし)
/// ・Bind / Unbind のイベントを発火して Presenter に通知する
///
/// ※ ModelはUIのクラスを一切参照しない(UI → Model の一方向)
/// </summary>
public sealed class CS_UIScanMarkerModel : CS_BaseModel
{
    // ---------------- 公開されたModel ----------------

    /// <summary>
    /// 現在 Bind されている Model(画面に1つだけなので static で保持する)
    /// </summary>
    private static CS_UIScanMarkerModel _current;

    /// <summary>
    /// Bind された時に通知するイベント
    /// </summary>
    public static event Action<CS_UIScanMarkerModel> OnBound;

    /// <summary>
    /// Bind が外れた時に通知するイベント
    /// </summary>
    public static event Action OnUnbound;

    /// <summary>
    /// 現在の Model を取得する(存在しなければ false)
    /// Presenter が後から有効になった場合に使う
    /// </summary>
    public static bool TryGet(out CS_UIScanMarkerModel model)
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

    private readonly List<Transform> _targets = new List<Transform>();

    /// <summary>
    /// 目印を出す相手の一覧
    /// </summary>
    public IReadOnlyList<Transform> targets => _targets;

    // ---------------- 公開 ----------------

    /// <summary>
    /// このModelを目印の表示元として公開する
    /// </summary>
    public void Bind()
    {
        _current = this;
        OnBound?.Invoke(this);
    }

    /// <summary>
    /// 公開をやめる(目印はすべて非表示になる)
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
    /// 目印を出す相手の一覧を入れ替える(null・破棄済みのものは入れない)
    /// </summary>
    public void SetTargets(IEnumerable<Transform> targetTransforms)
    {
        _targets.Clear();
        if (targetTransforms == null) return;

        foreach (Transform target in targetTransforms)
        {
            if (target != null) _targets.Add(target);
        }
    }

    /// <summary>
    /// 目印をすべて消す
    /// </summary>
    public void ClearTargets()
    {
        _targets.Clear();
    }

    public override void Dispose()
    {
        Unbind();
        _targets.Clear();
    }
}
