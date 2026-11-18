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
/// ・プレイヤーがSAに隠れている間だけ対象を検索する
/// ・CS_SAPoint.activePointsから検索する
/// ・距離計算にはCS_SAPoint.standPositionを使用する
/// ・現在隠れているSAポイント自身は候補から除外する
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
    public static event Action<
        CS_UISAHintModel
    > OnBound;

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

    private bool _isHiding;

    /// <summary>
    /// 現在プレイヤーが隠れているSAポイント。
    /// このポイントは案内対象から除外する。
    /// </summary>
    private CS_SAPoint _currentHidingPoint;

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

    public bool isHiding =>
        _isHiding;

    public CS_SAPoint currentHidingPoint =>
        _currentHidingPoint;

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

        _isHiding = false;

        _currentHidingPoint = null;
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
    // 隠れ状態
    // =========================================================

    /// <summary>
    /// プレイヤーがSAポイントに隠れ始めたことを設定する。
    ///
    /// currentHidingPointは現在隠れているSAポイント。
    /// このポイント自身は案内対象から除外する。
    /// </summary>
    public void StartHiding(
        CS_SAPoint currentHidingPoint)
    {
        bool stateChanged =
            !_isHiding;

        bool pointChanged =
            _currentHidingPoint !=
            currentHidingPoint;

        _isHiding = true;

        _currentHidingPoint =
            currentHidingPoint;

        if (stateChanged ||
            pointChanged)
        {
            RefreshTargets();
        }
    }

    /// <summary>
    /// プレイヤーがSAポイントから出たことを設定する。
    /// </summary>
    public void StopHiding()
    {
        if (!_isHiding &&
            _currentHidingPoint == null)
        {
            return;
        }

        _isHiding = false;

        _currentHidingPoint = null;

        ClearTargets();
    }

    /// <summary>
    /// 既存コードとの接続用。
    ///
    /// trueを渡した場合は現在SAが不明なため、
    /// 現在地SAを除外できない。
    /// 新規コードではStartHiding()を推奨する。
    /// </summary>
    public void SetHiding(bool isHiding)
    {
        if (isHiding)
        {
            StartHiding(
                currentHidingPoint: null
            );
        }
        else
        {
            StopHiding();
        }
    }

    /// <summary>
    /// 隠れ状態と現在SAを同時に設定する。
    /// </summary>
    public void SetHiding(
        bool isHiding,
        CS_SAPoint currentHidingPoint)
    {
        if (isHiding)
        {
            StartHiding(
                currentHidingPoint
            );
        }
        else
        {
            StopHiding();
        }
    }

    // =========================================================
    // SAポイント検索
    // =========================================================

    /// <summary>
    /// 範囲内のSAポイントを近い順に更新する。
    ///
    /// ・CS_SAPoint.activePointsから検索する
    /// ・距離計算にはstandPositionを使用する
    /// ・現在隠れているSAポイントは除外する
    /// </summary>
    public void RefreshTargets()
    {
        if (!_isHiding ||
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

        Vector3 playerPosition =
            _player.position;

        IReadOnlyList<CS_SAPoint> activePoints =
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

            // 現在隠れているSA自身は案内しない。
            if (saPoint ==
                _currentHidingPoint)
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

        _currentHidingPoint = null;

        _isHiding = false;
    }
}