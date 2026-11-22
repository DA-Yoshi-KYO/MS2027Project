/* ================================================
 * 動くSAポイントの軌道表示のModel
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-11-16 | 初回作成(Issue #507)
 * ================================================ */

using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

/// <summary>
/// 動くSAポイントの軌道(道筋)表示の Model。
///
/// ・軌道ごとに、道筋の点の並び(ワールド)と、表示の有無を保持する
/// ・軌道は自分の画面に出すもの全部で1つのModelなので、ハイドマーカーと同じく Bind() で公開する(番号なし)
/// ・AddPath で軌道を登録し(番号を返す)、SetVisible で出す・消す、RemovePath で外す
/// ・Bind / Unbind のイベントを発火して Presenter に通知する
///
/// ※ ModelはUIのクラスを一切参照しない(UI → Model の一方向)
/// </summary>
public sealed class CS_UISAPathModel : CS_BaseModel
{
    /// <summary>軌道1つ分のデータ</summary>
    public sealed class PathEntry
    {
        public int id;                  // 軌道の番号
        public Vector3[] points;        // 道筋の点の並び(ワールド)
        public bool isVisible;          // 出すか
    }

    // ---------------- 公開されたModel ----------------

    /// <summary>現在 Bind されている Model(画面に1つだけなので static で保持する)</summary>
    private static CS_UISAPathModel _current;

    /// <summary>Bind された時に通知するイベント</summary>
    public static event Action<CS_UISAPathModel> OnBound;

    /// <summary>Bind が外れた時に通知するイベント</summary>
    public static event Action OnUnbound;

    /// <summary>現在の Model を取得する(存在しなければ false)。Presenter が後から有効になった場合に使う</summary>
    public static bool TryGet(out CS_UISAPathModel model)
    {
        model = _current;
        return model != null;
    }

    // Domain Reloadを切っている場合に、前回の再生の状態が残らないようにする
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _current = null;
        OnBound = null;
        OnUnbound = null;
    }

    // ---------------- 状態 ----------------

    private readonly Dictionary<int, PathEntry> _entries = new();
    private readonly Subject<PathEntry> _added = new();
    private readonly Subject<int> _removed = new();
    private readonly Subject<PathEntry> _visibleChanged = new();
    private int _nextId;

    /// <summary>登録されている軌道すべて(Presenterが後から有効になった時に、今ある物を取り込むのに使う)</summary>
    public IReadOnlyCollection<PathEntry> entries => _entries.Values;

    /// <summary>軌道が登録された時</summary>
    public Observable<PathEntry> onAdded => _added;

    /// <summary>軌道が外された時(軌道の番号)</summary>
    public Observable<int> onRemoved => _removed;

    /// <summary>軌道を出す・消すが変わった時</summary>
    public Observable<PathEntry> onVisibleChanged => _visibleChanged;

    // ---------------- 公開 ----------------

    /// <summary>このModelを軌道の表示元として公開する</summary>
    public void Bind()
    {
        _current = this;
        OnBound?.Invoke(this);
    }

    /// <summary>公開をやめる(軌道は消える)</summary>
    public void Unbind()
    {
        // 別のModelで上書きされていなければ外す
        if (_current != this) return;

        _current = null;
        OnUnbound?.Invoke();
    }

    // ---------------- 値の変更 ----------------

    /// <summary>軌道を登録する(最初は消えている)。道筋の点の並び(ワールド)を渡し、軌道の番号を返す</summary>
    public int AddPath(IReadOnlyList<Vector3> worldPoints)
    {
        PathEntry entry = new PathEntry { id = _nextId++, points = new Vector3[worldPoints.Count], isVisible = false };
        for (int i = 0; i < worldPoints.Count; i++) entry.points[i] = worldPoints[i];

        _entries.Add(entry.id, entry);
        _added.OnNext(entry);
        return entry.id;
    }

    /// <summary>軌道を外す</summary>
    public void RemovePath(int id)
    {
        if (!_entries.Remove(id)) return;

        _removed.OnNext(id);
    }

    /// <summary>軌道を出す・消す(変わらなければ何もしない)</summary>
    public void SetVisible(int id, bool isVisible)
    {
        if (!_entries.TryGetValue(id, out PathEntry entry) || entry.isVisible == isVisible) return;

        entry.isVisible = isVisible;
        _visibleChanged.OnNext(entry);
    }

    public override void Dispose()
    {
        Unbind();
        _added.Dispose();
        _removed.Dispose();
        _visibleChanged.Dispose();
        _entries.Clear();
    }
}
