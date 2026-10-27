/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-20 | 初回作成
 * ================================================ */

using Unity.Netcode;

/// <summary>
/// 警察が自分に気付いているかを、警戒UI(CS_PoliceDetectionController)へ知らせるクラス
/// ・警察の判断はサーバーでしか動かないので、状態をNetworkVariableで全員へ同期し、各クライアントがUIへ登録する
/// ・同期するのは「追跡中の相手のプレイヤー番号」と「駆け付け・探索中の相手のプレイヤー番号」(いなければ-1)
/// ・各クライアントは、自分(ローカルプレイヤー)の番号と比べて、表示を決める
///     追跡中の相手が自分 → Found(発見) / 駆け付け・探索中の相手が自分 → SignalDetected / どちらでもない → 表示しない
/// ・ローカルプレイヤーが警察より後に出現する場合は、ローカルプレイヤーが決まった時にも判定し直す
/// ・シーンにCS_PoliceDetectionControllerが無ければ何もしない(警戒UIの無いテストシーンでも動く)
/// </summary>
public class CS_PoliceDetectionSource : NetworkBehaviour
{
    /// <summary>
    /// 相手のプレイヤーがいない時の番号
    /// </summary>
    public const int noPlayer = -1;

    // 追跡中の相手のプレイヤー番号(書き込みはサーバーのみ)
    private readonly NetworkVariable<int> _foundPlayerNumber = new NetworkVariable<int>(noPlayer);

    // 駆け付け・探索中の相手のプレイヤー番号(書き込みはサーバーのみ)
    private readonly NetworkVariable<int> _signalPlayerNumber = new NetworkVariable<int>(noPlayer);

    // オフライン(NetworkManagerが動いていない)のテストシーンでは、NetworkVariableを使わずにここへ持つ
    private int _offlineFoundNumber = noPlayer;
    private int _offlineSignalNumber = noPlayer;

    // 登録先の警戒UI
    private CS_PoliceDetectionController _controller = null;

    // 警戒UIに登録する時のid(登録の対象になっていなければnull)
    private string _id = null;

    // 警戒UIに今反映している状態(Hiddenは登録していない状態)
    private CSE_PoliceDetectionState _appliedState = CSE_PoliceDetectionState.Patrol;

    // オフラインのテストシーン用
    private void Start()
    {
        if (IsSpawned) return;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return;

        Begin($"Police_{GetInstanceID()}");
    }

    public override void OnNetworkSpawn()
    {
        _foundPlayerNumber.OnValueChanged += OnNumberChanged;
        _signalPlayerNumber.OnValueChanged += OnNumberChanged;
        Begin($"Police_{NetworkObjectId}");
    }

    public override void OnNetworkDespawn()
    {
        _foundPlayerNumber.OnValueChanged -= OnNumberChanged;
        _signalPlayerNumber.OnValueChanged -= OnNumberChanged;
        End();
    }

    public override void OnDestroy()
    {
        End();
        base.OnDestroy();
    }

    /// <summary>
    /// 警察の今の状態を知らせるメソッド(サーバー、またはオフラインで呼ぶこと)
    /// </summary>
    /// <param name="foundPlayerNumber">追跡中の相手のプレイヤー番号(いなければ-1)</param>
    /// <param name="signalPlayerNumber">駆け付け・探索中の相手のプレイヤー番号(いなければ-1)</param>
    public void SetState(int foundPlayerNumber, int signalPlayerNumber)
    {
        _offlineFoundNumber = foundPlayerNumber;
        _offlineSignalNumber = signalPlayerNumber;

        // 同じ値を入れても同期されないので、毎回入れてよい
        if (IsSpawned && IsServer)
        {
            _foundPlayerNumber.Value = foundPlayerNumber;
            _signalPlayerNumber.Value = signalPlayerNumber;
        }

        Refresh();
    }

    // NetworkVariableが変わった時(クライアント)
    private void OnNumberChanged(int previous, int current)
    {
        Refresh();
    }

    // ローカルプレイヤーが決まった・変わった時
    private void OnLocalPlayerSet(CS_Player player)
    {
        Refresh();
    }

    /// <summary>
    /// 警戒UIへの登録の対象になるメソッド(出現した時に呼ぶ)
    /// </summary>
    /// <param name="id">警戒UIに登録する時のid(ミニマップと同じく「Police_」を付ける)</param>
    private void Begin(string id)
    {
        _id = id;
        CS_Player.onLocalPlayerSet += OnLocalPlayerSet;
        Refresh();
    }

    /// <summary>
    /// 警戒UIへの登録を解除するメソッド(解除しないとアイコンが残り続ける)
    /// </summary>
    private void End()
    {
        if (_id == null) return;

        CS_Player.onLocalPlayerSet -= OnLocalPlayerSet;
        if (_controller != null && _appliedState != CSE_PoliceDetectionState.Patrol) _controller.UnregisterPolice(_id);

        _appliedState = CSE_PoliceDetectionState.Patrol;
        _id = null;
    }

    /// <summary>
    /// 自分(ローカルプレイヤー)から見た状態を求め、警戒UIの登録を合わせるメソッド
    /// 一度Hiddenにすると登録が消えて復帰できないので、表示する時は毎回、登録し直す
    /// </summary>
    private void Refresh()
    {
        if (_id == null) return;

        CSE_PoliceDetectionState state = GetState();
        if (state == _appliedState) return;

        if (_controller == null) _controller = FindAnyObjectByType<CS_PoliceDetectionController>();
        if (_controller == null) return;

        _appliedState = state;
        if (state == CSE_PoliceDetectionState.Patrol) _controller.UnregisterPolice(_id);
        else _controller.RegisterPolice(_id, transform, state);
    }

    /// <summary>
    /// 自分(ローカルプレイヤー)から見た、この警察の表示の状態を求めるメソッド
    /// </summary>
    /// <returns>追跡中の相手が自分ならFound、駆け付け・探索中の相手が自分ならSignalDetected、どちらでもなければHidden</returns>
    private CSE_PoliceDetectionState GetState()
    {
        CS_Player localPlayer = CS_Player.localPlayer;
        if (localPlayer == null) return CSE_PoliceDetectionState.Patrol;

        int myNumber = localPlayer.playerNumber;
        int foundNumber = IsSpawned ? _foundPlayerNumber.Value : _offlineFoundNumber;
        int signalNumber = IsSpawned ? _signalPlayerNumber.Value : _offlineSignalNumber;

        if (foundNumber == myNumber) return CSE_PoliceDetectionState.Detected;
        if (signalNumber == myNumber) return CSE_PoliceDetectionState.Tracking;
        return CSE_PoliceDetectionState.Patrol;
    }
}
