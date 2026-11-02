/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-10-28 | 初回作成(CS_PlayerScanEffectの仮の目印をMVPに分けた)
 * 2026-11-02 | 相手ごとの種類・位置・距離・犯罪完遂度を持つようにした
 * ================================================ */

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// スキャンの目印(スキャンで見えている悪人グループ・警察・巡回ドローンの頭上に出す印)の Model。
///
/// ・目印1つ分の表示内容(CS_UIScanMarkerData: 種類・位置・距離・犯罪完遂度)の一覧を保持する
///   → SetTargets を呼ぶだけで目印の数・位置・表示が変わる(空なら目印なし)
///   相手が動くので、使う側が毎フレーム一覧を渡し、Presenter が毎フレーム読んで表示する
/// ・悪人はグループごとに1つ(位置はメンバーの中心)。使う側でまとめてから渡す
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

    private readonly List<CS_UIScanMarkerData> _targets = new List<CS_UIScanMarkerData>();

    /// <summary>
    /// 目印の表示内容の一覧
    /// </summary>
    public IReadOnlyList<CS_UIScanMarkerData> targets => _targets;

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
    /// 目印の表示内容の一覧を入れ替える
    /// </summary>
    public void SetTargets(IEnumerable<CS_UIScanMarkerData> targets)
    {
        _targets.Clear();
        if (targets == null) return;

        _targets.AddRange(targets);
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
