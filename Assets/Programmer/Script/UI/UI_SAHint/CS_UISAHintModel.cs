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
///   SA移動で信号機へ到着した後にヒントを表示する
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
    // SAヒントを開始した種類
    // =========================================================

    /// <summary>
    /// SAヒントがどの状態から開始されたか。
    /// </summary>
    private enum SAHintSource
    {
        None,
        HidePoint,
        TrafficLight
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

    /// <summary>
    /// ヒントを開始した入口。
    /// </summary>
    private SAHintSource _hintSource;

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

    /// <summary>
    /// ハイドポイント用の入口から開始されたか。
    /// </summary>
    public bool isHidePointHint =>
        _hintSource ==
        SAHintSource.HidePoint;

    /// <summary>
    /// 信号機用の入口から開始されたか。
    /// </summary>
    public bool isTrafficLightHint =>
        _hintSource ==
        SAHintSource.TrafficLight;

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

        _hintSource =
            SAHintSource.None;
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
    // ハイドポイント用
    // =========================================================

    /// <summary>
    /// プレイヤーがハイドポイントで隠れ始めたときに呼ぶ。
    ///
    /// 既存のCS_PlayerHide.currentHidePointとの接続用。
    /// </summary>
    public void StartHiding(
        CS_SAPoint hidePoint)
    {
        SetCurrentSAPoint(
            currentSAPoint: hidePoint,
            source: SAHintSource.HidePoint
        );
    }

    /// <summary>
    /// プレイヤーがハイドポイントから出たときに呼ぶ。
    ///
    /// 現在のヒントがハイドポイント由来の場合だけ終了する。
    /// 信号機用ヒントは停止しない。
    /// </summary>
    public void StopHiding()
    {
        if (_hintSource !=
            SAHintSource.HidePoint)
        {
            return;
        }

        StopHint();
    }

    /// <summary>
    /// 既存コードとの接続用。
    /// </summary>
    public void SetHiding(bool isHiding)
    {
        if (isHiding)
        {
            StartHiding(
                hidePoint: null
            );
        }
        else
        {
            StopHiding();
        }
    }

    /// <summary>
    /// 隠れ状態と現在のハイドポイントを同時に設定する。
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
    // 信号機用
    // =========================================================

    /// <summary>
    /// SA移動で信号機へ到着したときに呼ぶ。
    ///
    /// 歩いて信号機へ近づいた場合には呼ばず、
    /// SA移動の到着完了処理からだけ呼ぶ。
    /// </summary>
    public void StartTrafficLightHint(
        CS_SAPoint trafficLightPoint)
    {
        SetCurrentSAPoint(
            currentSAPoint:
                trafficLightPoint,
            source:
                SAHintSource.TrafficLight
        );
    }

    /// <summary>
    /// 信号機から離れたとき、
    /// または次のSA移動を開始するときに呼ぶ。
    ///
    /// 現在のヒントが信号機由来の場合だけ終了する。
    /// ハイドポイント用ヒントは停止しない。
    /// </summary>
    public void StopTrafficLightHint()
    {
        if (_hintSource !=
            SAHintSource.TrafficLight)
        {
            return;
        }

        StopHint();
    }

    // =========================================================
    // 共通の表示状態
    // =========================================================

    /// <summary>
    /// 現在いるSAポイントと、
    /// ヒントを開始した入口を設定する。
    /// </summary>
    private void SetCurrentSAPoint(
        CS_SAPoint currentSAPoint,
        SAHintSource source)
    {
        if (currentSAPoint == null)
        {
            // StartHiding(null)などの場合は
            // 現在の入口に対応する停止処理を行う。
            if (source ==
                SAHintSource.HidePoint)
            {
                StopHiding();
            }
            else if (source ==
                     SAHintSource.TrafficLight)
            {
                StopTrafficLightHint();
            }

            return;
        }

        bool stateChanged =
            !_isHintActive;

        bool pointChanged =
            _currentSAPoint !=
            currentSAPoint;

        bool sourceChanged =
            _hintSource != source;

        _isHintActive = true;

        _currentSAPoint =
            currentSAPoint;

        _hintSource = source;

        if (stateChanged ||
            pointChanged ||
            sourceChanged)
        {
            RefreshTargets();
        }
    }

    /// <summary>
    /// 現在のSAヒントを終了する共通処理。
    /// </summary>
    private void StopHint()
    {
        if (!_isHintActive &&
            _currentSAPoint == null &&
            _hintSource ==
            SAHintSource.None)
        {
            return;
        }

        _isHintActive = false;

        _currentSAPoint = null;

        _hintSource =
            SAHintSource.None;

        ClearTargets();
    }

    /// <summary>
    /// ハイドポイント・信号機の種類を問わず、
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

        _isHintActive = false;

        _currentSAPoint = null;

        _hintSource =
            SAHintSource.None;
    }
}