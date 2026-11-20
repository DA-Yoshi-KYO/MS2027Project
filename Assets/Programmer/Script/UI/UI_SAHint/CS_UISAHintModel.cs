/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-18 | 初回作成
 * ================================================ */

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SAヒントUIに必要な状態を保持するModel。
///
/// ・ローカルプレイヤーとプレイヤーカメラを保持する
/// ・ハイドポイントで隠れている間、または
///   SAで到着したSAポイント(種類を問わない)の上にいる間、ヒントを表示する
/// ・CS_SAPoint.activePointsから候補を検索する
/// ・距離計算にはCS_SAPoint.standPositionを使用する
/// ・現在いるSAポイント自身は候補から除外する
/// ・範囲内のSAポイントを近い順に最大指定数保持する
/// ・UIクラスは参照しない
///
/// Bind()はModelを所有するゲーム側が呼ぶ。
/// PresenterやViewからは呼ばない。
/// </summary>
public sealed class CS_UISAHintModel :
    CS_BaseModel
{
    // =========================================================
    // 公開中のModel
    // =========================================================

    private static CS_UISAHintModel _current;

    /// <summary>
    /// Modelが公開されたときの通知。
    /// </summary>
    public static event Action<CS_UISAHintModel>
        OnBound;

    /// <summary>
    /// Modelの公開が解除されたときの通知。
    /// </summary>
    public static event Action OnUnbound;

    /// <summary>
    /// 現在公開されているModelを取得する。
    /// </summary>
    public static bool TryGet(
        out CS_UISAHintModel model)
    {
        model = _current;

        return model != null;
    }

    /// <summary>
    /// Domain Reloadが無効でも、
    /// 前回の再生状態を残さない。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _current = null;

        OnBound = null;
        OnUnbound = null;
    }

    // =========================================================
    // 距離付きSAポイント
    // =========================================================

    /// <summary>
    /// 距離順に並べ替えるための一時データ。
    /// </summary>
    private readonly struct PointDistance
    {
        private readonly CS_SAPoint _point;

        private readonly float _squaredDistance;

        public CS_SAPoint point =>
            _point;

        public float squaredDistance =>
            _squaredDistance;

        public PointDistance(
            CS_SAPoint point,
            float squaredDistance)
        {
            _point = point;

            _squaredDistance =
                squaredDistance;
        }
    }

    // =========================================================
    // 状態
    // =========================================================

    private readonly Transform _player;

    private readonly Camera _playerCamera;

    private readonly CSO_UISAHintData _data;

    /// <summary>
    /// 現在表示対象になっているSAポイント。
    /// 距離が近い順に格納する。
    /// </summary>
    private readonly List<CS_SAPoint>
        _nearbyPoints =
            new List<CS_SAPoint>();

    /// <summary>
    /// 距離計算とソートに使用する一時バッファ。
    /// </summary>
    private readonly List<PointDistance>
        _pointDistanceBuffer =
            new List<PointDistance>();

    /// <summary>
    /// SAヒントが有効か。
    /// </summary>
    private bool _isHintActive;

    /// <summary>
    /// 現在いるSAポイント。
    /// このポイント自身は案内対象から除外する。
    /// </summary>
    private CS_SAPoint _currentSAPoint;

    // =========================================================
    // イベント
    // =========================================================

    /// <summary>
    /// 表示対象のSAポイント一覧が変化したときの通知。
    /// </summary>
    public event Action OnTargetsUpdated;

    // =========================================================
    // 公開プロパティ
    // =========================================================

    public Transform player =>
        _player;

    public Camera playerCamera =>
        _playerCamera;

    public CSO_UISAHintData data =>
        _data;

    /// <summary>
    /// Presenterが表示条件として使用する。
    /// ハイドポイントと信号機のどちらでもtrueになる。
    /// </summary>
    public bool isHiding =>
        _isHintActive;

    public bool isHintActive =>
        _isHintActive;

    public CS_SAPoint currentHidingPoint =>
        _currentSAPoint;

    public CS_SAPoint currentSAPoint =>
        _currentSAPoint;

    public int targetCount =>
        _nearbyPoints.Count;

    // =========================================================
    // コンストラクター
    // =========================================================

    public CS_UISAHintModel(
        Transform player,
        Camera playerCamera,
        CSO_UISAHintData data)
    {
        _player = player;

        _playerCamera =
            playerCamera;

        _data = data;

        _isHintActive = false;

        _currentSAPoint = null;

    }

    // =========================================================
    // Bind / Unbind
    // =========================================================

    /// <summary>
    /// このModelをSAヒントUIへ公開する。
    ///
    /// ローカルプレイヤーを所有するゲーム側が呼ぶ。
    /// PresenterやViewからは呼ばない。
    /// </summary>
    public void Bind()
    {
        if (_current == this)
        {
            return;
        }

        if (_current != null)
        {
            _current.Unbind();
        }

        _current = this;

        OnBound?.Invoke(this);
    }

    /// <summary>
    /// このModelの公開を解除する。
    /// </summary>
    public void Unbind()
    {
        if (_current != this)
        {
            return;
        }

        _current = null;

        OnUnbound?.Invoke();
    }

    // =========================================================
    // ヒントの開始・終了
    // =========================================================

    /// <summary>
    /// 現在いるSAポイントを設定し、ヒントを開始する。
    ///
    /// ・ハイドポイントで隠れている間
    /// ・SAで到着したSAポイント(信号機など種類を問わない)
    /// のどちらも、この入口から開始する。
    /// 渡したSAポイント自身は案内対象から除外する。
    /// </summary>
    public void StartHint(
        CS_SAPoint currentPoint)
    {
        if (currentPoint == null)
        {
            StopHint();

            return;
        }

        bool stateChanged =
            !_isHintActive;

        bool pointChanged =
            _currentSAPoint !=
            currentPoint;

        _isHintActive = true;

        _currentSAPoint =
            currentPoint;

        if (stateChanged ||
            pointChanged)
        {
            RefreshTargets();
        }
    }

    /// <summary>
    /// 現在のSAヒントを終了する。
    /// </summary>
    public void StopHint()
    {
        if (!_isHintActive &&
            _currentSAPoint == null)
        {
            return;
        }

        _isHintActive = false;

        _currentSAPoint = null;

        ClearTargets();
    }

    // =========================================================
    // 既存コードとの接続用
    // =========================================================

    /// <summary>
    /// StartHint()と同じ。
    /// </summary>
    public void StartHiding(
        CS_SAPoint hidePoint)
    {
        StartHint(hidePoint);
    }

    /// <summary>
    /// StopHint()と同じ。
    /// </summary>
    public void StopHiding()
    {
        StopHint();
    }

    /// <summary>
    /// 既存コードとの接続用。
    /// </summary>
    public void SetHiding(bool isHiding)
    {
        if (isHiding)
        {
            StartHint(
                currentPoint: null
            );
        }
        else
        {
            StopHint();
        }
    }

    /// <summary>
    /// 隠れ状態と現在のSAポイントを同時に設定する。
    /// </summary>
    public void SetHiding(
        bool isHiding,
        CS_SAPoint currentHidingPoint)
    {
        if (isHiding)
        {
            StartHint(
                currentHidingPoint
            );
        }
        else
        {
            StopHint();
        }
    }

    /// <summary>
    /// SAポイントの種類を問わず、
    /// 現在のSAヒントを強制終了する。
    ///
    /// 死亡、プレイヤー無効化、Model破棄時などに使用する。
    /// </summary>
    public void StopAllHints()
    {
        StopHint();
    }

    // =========================================================
    // SAポイント検索
    // =========================================================

    /// <summary>
    /// 範囲内のSAポイントを近い順に更新する。
    ///
    /// ・CS_SAPoint.activePointsから検索する
    /// ・距離計算にはstandPositionを使用する
    /// ・現在いるSAポイントは除外する
    /// ・SAReachBoxが設定されていない(SAできない)SAポイントは除外する
    /// ・プレイヤーから最小距離(minimumDistance)未満の近すぎるSAポイントは除外する
    /// </summary>
    public void RefreshTargets()
    {
        if (!_isHintActive ||
            _currentSAPoint == null ||
            _player == null ||
            _data == null)
        {
            ClearTargets();

            return;
        }

        _pointDistanceBuffer.Clear();

        float searchDistance =
            _data.searchDistance;

        float searchSquaredDistance =
            searchDistance *
            searchDistance;

        float minimumSquaredDistance =
            _data.minimumDistance *
            _data.minimumDistance;

        Vector3 playerPosition =
            _player.position;

        IReadOnlyList<CS_SAPoint>
            activePoints =
                CS_SAPoint.activePoints;

        if (activePoints == null)
        {
            ClearTargets();

            return;
        }

        for (int i = 0;
             i < activePoints.Count;
             i++)
        {
            CS_SAPoint saPoint =
                activePoints[i];

            if (saPoint == null)
            {
                continue;
            }

            if (!saPoint.isActiveAndEnabled)
            {
                continue;
            }

            // 現在いるSA自身は案内しない。
            if (saPoint ==
                _currentSAPoint)
            {
                continue;
            }

            // SAReachBoxが空のSAポイントはSAできないため案内しない。
            if (!saPoint.hasSAReachBox)
            {
                continue;
            }

            Vector3 targetPosition =
                saPoint.standPosition;

            float squaredDistance =
                (
                    targetPosition -
                    playerPosition
                ).sqrMagnitude;

            if (squaredDistance >
                searchSquaredDistance)
            {
                continue;
            }

            // 近すぎるSAポイントは案内しない。
            if (squaredDistance <
                minimumSquaredDistance)
            {
                continue;
            }

            _pointDistanceBuffer.Add(
                new PointDistance(
                    point: saPoint,
                    squaredDistance:
                        squaredDistance
                )
            );
        }

        // 距離が近い順に並べる。
        _pointDistanceBuffer.Sort(
            (left, right) =>
                left.squaredDistance.CompareTo(
                    right.squaredDistance
                )
        );

        int maximumDisplayCount =
            Mathf.Max(
                0,
                _data.maximumDisplayCount
            );

        int newCount =
            Mathf.Min(
                maximumDisplayCount,
                _pointDistanceBuffer.Count
            );

        if (!HasTargetListChanged(newCount))
        {
            return;
        }

        _nearbyPoints.Clear();

        for (int i = 0;
             i < newCount;
             i++)
        {
            _nearbyPoints.Add(
                _pointDistanceBuffer[i].point
            );
        }

        OnTargetsUpdated?.Invoke();
    }

    /// <summary>
    /// 指定番号の表示対象SAポイントを取得する。
    /// </summary>
    public CS_SAPoint GetTarget(int index)
    {
        if (index < 0 ||
            index >= _nearbyPoints.Count)
        {
            return null;
        }

        return _nearbyPoints[index];
    }

    /// <summary>
    /// 新しい検索結果と現在の一覧が異なるか確認する。
    /// </summary>
    private bool HasTargetListChanged(
        int newCount)
    {
        if (_nearbyPoints.Count !=
            newCount)
        {
            return true;
        }

        for (int i = 0;
             i < newCount;
             i++)
        {
            CS_SAPoint newPoint =
                _pointDistanceBuffer[i].point;

            if (_nearbyPoints[i] !=
                newPoint)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 表示対象をすべて解除する。
    /// </summary>
    private void ClearTargets()
    {
        _pointDistanceBuffer.Clear();

        if (_nearbyPoints.Count == 0)
        {
            return;
        }

        _nearbyPoints.Clear();

        OnTargetsUpdated?.Invoke();
    }

    // =========================================================
    // Dispose
    // =========================================================

    public override void Dispose()
    {
        Unbind();

        OnTargetsUpdated = null;

        _nearbyPoints.Clear();

        _pointDistanceBuffer.Clear();

        _isHintActive = false;

        _currentSAPoint = null;

    }
}