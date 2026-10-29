/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-10-29 | 初回作成(Issue #342)
 * ================================================ */

using System.Collections.Generic;
using R3;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// エリア選択シーン(AreaSelect)の処理を担当するクラス
/// 各プレイヤーの選んだ開始エリアをサーバーが集め、全員が選ぶか時間切れで本編(ゲームシーン)へ移る
/// 画面の表示はMVP(CS_UIAreaSelectModel/Presenter/View)に任せ、このクラスはModelに状態を書くだけ
/// </summary>
// ========================================
/*
 * メモ
 * ・流れ(仕様「1プレイの流れ」): ロビー(WaitingforaMatch) → エリア選択(ここ) → 本編(MainScene)
 *   待機シーンのCS_SceneTransitionerの遷移先をこのシーンにしておく
 * ・シーンに置いたNetworkObjectに付ける(サーバーがNetworkSceneManagerでこのシーンを読み込むと全員で動く)
 * ・選択の権威はサーバーのみが持つ
 *   クライアントはボタンを押すとSelectAreaRpcでサーバーへ送る(何度でも選び直せる)
 *   サーバーは選択をクライアントIDごとに覚え、エリアごとの人数を_areaCountsで全員へ同期する
 *   選択の締め切り(サーバー時刻)も_endTimeで同期し、残り秒数は各端末で計算する
 * ・同じエリアは何人でも選べる
 * ・接続中の全員が選んだら、または_selectSeconds秒経ったら本編へ移る
 *   選ばなかった人はランダムなエリアにする(NPCのエリアはCS_PlayerSpawnerがランダムに決める)
 *   決まったエリアはCS_SpawnAreaStoreに入れて、ゲームシーンのCS_PlayerSpawnerが使う
 * ・選んでいる途中で抜けた人の選択は消す。ホストが抜けた・切断されたクライアントは部屋に入るシーンへ戻る
 * ・オフライン(このシーンを直接再生した時)は自分1人だけなので、選んだらすぐ本編へ移る
 */
// ========================================
public class CS_AreaSelectController : NetworkBehaviour
{
    [SerializeField][Tooltip("本編への遷移(遷移先はゲームシーン)")] private CS_SceneTransitioner _sceneTransitioner;
    [SerializeField][Tooltip("画面のView(同じGameObjectのPresenterを使う)")] private CS_UIAreaSelectView _view;
    [SerializeField][Tooltip("エリアの数")][Min(1)] private int _areaCount = 3;
    [SerializeField][Tooltip("選べる秒数")][Min(1f)] private float _selectSeconds = 15f;
    [SerializeField][Tooltip("戻り先のシーン名")] private string _roomConnectionSceneName = "RoomConnection";

    private readonly NetworkVariable<double> _endTime = new NetworkVariable<double>();   // 選択の締め切り(サーバー時刻)
    private readonly NetworkList<int> _areaCounts = new NetworkList<int>();              // エリアごとの選んだ人数

    private readonly Dictionary<ulong, int> _selections = new Dictionary<ulong, int>();  // サーバーだけが持つ、クライアントIDごとの選択
    private CS_UIAreaSelectModel _model;
    private DisposableBag _disposables;
    private double _offlineEndTime;
    private bool _isFinished;
    private bool _isLeaving;

    private static bool IsOnline =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    void Awake()
    {
        _model = new CS_UIAreaSelectModel(_areaCount);
        _offlineEndTime = Time.timeAsDouble + _selectSeconds;
    }

    void Start()
    {
        CS_UIAreaSelectPresenter presenter = _view.GetComponent<CS_UIAreaSelectPresenter>();
        presenter.BindModel(_model);
        presenter.onAreaRequested.Subscribe(OnAreaRequested).AddTo(ref _disposables);

        _model.SetRemainingSeconds(Mathf.CeilToInt(_selectSeconds));

        // 前のプレイの結果が残らないように消しておく(読むのはサーバーだけ)
        if (!IsOnline || NetworkManager.Singleton.IsServer)
        {
            CS_SpawnAreaStore.Clear();
        }

        if (IsOnline)
        {
            NetworkManager.Singleton.OnConnectionEvent += OnConnectionEvent;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        _areaCounts.OnListChanged += OnAreaCountsChanged;

        if (IsServer)
        {
            _endTime.Value = NetworkManager.ServerTime.Time + _selectSeconds;

            _areaCounts.Clear();
            for (int area = 0; area < _areaCount; area++)
            {
                _areaCounts.Add(0);
            }
        }

        RefreshAreaCounts();
    }

    public override void OnNetworkDespawn()
    {
        _areaCounts.OnListChanged -= OnAreaCountsChanged;
        base.OnNetworkDespawn();
    }

    public override void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnConnectionEvent -= OnConnectionEvent;
        }
        _disposables.Dispose();
        _model?.Dispose();

        base.OnDestroy();
    }

    void Update()
    {
        if (_isFinished || _isLeaving) return;

        double remaining = GetRemainingTime();
        _model.SetRemainingSeconds(Mathf.Max(0, Mathf.CeilToInt((float)remaining)));

        if (remaining > 0) return;

        // 時間切れ。締め切るのはサーバー(オフラインなら自分)だけ
        _model.SetInteractable(false);
        if (!IsOnline || IsServer) Finish();
    }

    // 締め切りまでの残り秒数
    private double GetRemainingTime()
    {
        if (!IsOnline) return _offlineEndTime - Time.timeAsDouble;

        // 同期が届くまでは締め切りが0なので、まだ始まっていない扱いにする
        if (!IsSpawned || _endTime.Value <= 0) return _selectSeconds;

        return _endTime.Value - NetworkManager.ServerTime.Time;
    }

    // ボタンが押された時(自分の端末で呼ばれる)
    private void OnAreaRequested(int area)
    {
        if (_isFinished || area < 0 || area >= _areaCount) return;

        _model.SetSelectedArea(area);

        if (!IsOnline)
        {
            SetSelection(NetworkManager.ServerClientId, area);
            return;
        }

        SelectAreaRpc(area);
    }

    // クライアントからサーバーへ選んだエリアを送る
    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void SelectAreaRpc(int area, RpcParams rpcParams = default)
    {
        if (_isFinished || area < 0 || area >= _areaCount) return;

        SetSelection(rpcParams.Receive.SenderClientId, area);
    }

    // 選択を覚えて人数を数え直し、全員が選んでいれば本編へ移る(サーバー・オフラインで呼ばれる)
    private void SetSelection(ulong clientId, int area)
    {
        _selections[clientId] = area;
        RecountAreas();

        if (HasEveryoneSelected()) Finish();
    }

    private void RecountAreas()
    {
        int[] counts = new int[_areaCount];
        foreach (int area in _selections.Values)
        {
            counts[area]++;
        }

        if (!IsOnline)
        {
            for (int area = 0; area < _areaCount; area++)
            {
                _model.SetAreaCount(area, counts[area]);
            }
            return;
        }

        // 同期されたリストの変更(OnAreaCountsChanged)でホストのModelも更新される
        for (int area = 0; area < _areaCount; area++)
        {
            if (_areaCounts[area] != counts[area]) _areaCounts[area] = counts[area];
        }
    }

    private bool HasEveryoneSelected()
    {
        if (!IsOnline) return _selections.Count > 0;

        foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
        {
            if (!_selections.ContainsKey(clientId)) return false;
        }
        return true;
    }

    // 選ばなかった人のエリアをランダムに決めて結果を保存し、本編へ移る(サーバー・オフラインで呼ばれる)
    private void Finish()
    {
        if (_isFinished) return;
        _isFinished = true;
        _model.SetInteractable(false);

        if (IsOnline)
        {
            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            {
                if (!_selections.ContainsKey(clientId)) _selections[clientId] = Random.Range(0, _areaCount);
            }
        }
        else if (_selections.Count == 0)
        {
            _selections[NetworkManager.ServerClientId] = Random.Range(0, _areaCount);
        }

        CS_SpawnAreaStore.Clear();
        foreach (KeyValuePair<ulong, int> selection in _selections)
        {
            CS_SpawnAreaStore.SetArea(selection.Key, selection.Value);
        }

        _sceneTransitioner.StartTransition();
    }

    private void OnAreaCountsChanged(NetworkListEvent<int> changeEvent)
    {
        RefreshAreaCounts();
    }

    private void RefreshAreaCounts()
    {
        for (int area = 0; area < _areaCount && area < _areaCounts.Count; area++)
        {
            _model.SetAreaCount(area, _areaCounts[area]);
        }
    }

    private void OnConnectionEvent(NetworkManager networkManager, ConnectionEventData data)
    {
        if (data.EventType != ConnectionEvent.ClientDisconnected) return;

        // クライアント側で自分が切断された(ホストが抜けた等)ときは部屋に入るシーンへ戻る
        if (!networkManager.IsServer)
        {
            Leave();
            return;
        }

        // 抜けた人の選択を消し、残りの全員が選び終わっていれば本編へ移る
        if (_isFinished || !_selections.Remove(data.ClientId)) return;

        RecountAreas();
        if (_selections.Count > 0 && HasEveryoneSelected()) Finish();
    }

    // 通信を終了して部屋に入るシーンへ戻る
    private void Leave()
    {
        if (_isLeaving) return;
        _isLeaving = true;

        NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene(_roomConnectionSceneName);
    }
}
