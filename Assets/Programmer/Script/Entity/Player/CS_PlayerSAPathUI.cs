using System.Collections.Generic;
using UnityEngine;

/*
 * 動くSAポイント(CS_PathFollowerで動くSAポイント)の軌道(道筋)を、そのSAポイントの強調UI(ハイドマーカーUI。RT MOVE!の目印)が出ている間だけ表示する(Issue #507)ために、軌道表示UI(CS_UISAPathModel)へ渡すクラス
 * シーンにある動くSAポイントの道筋を軌道表示UIのModelに登録し、今狙っているSAポイントに合わせて出す・消すを切り替えるだけ
 * UI側(Model/Presenter/View)はこのクラス・CS_Player・CS_PathFollowerの存在を一切知らない
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・動かないSAポイントには出さない(CS_PathFollowerが付いていない物は対象にならない)
 * ・出す条件: 強調UIが出ているSAポイント(CS_PlayerHide.currentCandidate。今ダッシュボタンで飛べる1つ)が、動くSAポイントの時に、その道筋だけを出す
 *   別のSAポイントに強調UIが移った時・強調UIが消えた時(currentCandidateがnullになった時)は、点線も消える。ふわっと出入りするフェードはViewが行う
 *   currentCandidateから道筋は、登録した時に作る辞書(動くSAポイント → 軌道)で引く。毎フレーム探し回らない
 * ・道筋は動かないので、軌道を登録する時に1回だけ点の並び(CS_PathFollower.GetCurvePoints。ワールドの座標にして)を取って使い回す
 *   パスをこのオブジェクトごと動かした場合は、登録し直さないと反映されない(再生中に動かさない前提)
 * ・シーンにあるCS_PathFollowerは、最初と_rescanInterval秒ごとに集め直す(FindObjectsByType。毎フレームは探さない)
 *   途中で増えた物は登録し、消えた物は外す
 * ・軌道は自分の画面に出すもの全部で1つのため、自分(isLocalPlayer)のプレイヤーだけがModelを作ってBindする
 *   isLocalPlayerはスポーン後に決まるため、毎フレーム確かめて、自分のプレイヤーになった時にBindする
 * ・見た目(色・太さ・フェード・ホログラムのシェーダー)はUICanvas側(SAPath)のViewとマテリアルで決める
 * ・CS_PathFollower・CS_SAPoint・CS_PlayerHideのロジックはこのクラスを知らない(外してもSAは動く)
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでも単体で動く。通信はしない
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
[RequireComponent(typeof(CS_PlayerHide))]
public class CS_PlayerSAPathUI : MonoBehaviour
{
    // 登録した軌道1つ分
    private class PathData
    {
        public int id;                  // Modelでの軌道の番号
        public CS_SAPoint saPoint;      // 動かすSAポイント(強調UIが出る相手)
    }

    [Header("集め直し")]
    [SerializeField][Min(0.5f)] private float _rescanInterval = 5f;       // 動くSAポイントを集め直す間隔(秒)

    [Header("道筋")]
    [SerializeField][Range(1, 32)] private int _pointsPerSegment = 12;    // 道筋の1区間あたりの分割数(多いほどなめらか)

    private CS_Player _player;
    private CS_PlayerHide _hide;
    private CS_UISAPathModel _pathModel;
    private readonly Dictionary<CS_PathFollower, PathData> _paths = new Dictionary<CS_PathFollower, PathData>();
    private readonly Dictionary<CS_SAPoint, PathData> _pathsBySaPoint = new Dictionary<CS_SAPoint, PathData>();   // 強調UIが出るSAポイントから軌道を引く
    private readonly List<CS_PathFollower> _removeList = new List<CS_PathFollower>();
    private PathData _shownPath;        // 今出している軌道(無ければnull)
    private float _rescanTimer;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _hide = GetComponent<CS_PlayerHide>();
    }

    private void OnDisable()
    {
        UnbindPathUI();
    }

    private void Update()
    {
        if (!_player.isLocalPlayer)
        {
            UnbindPathUI();   // NPC・他人のプレイヤーは軌道を出さない
            return;
        }

        if (_pathModel == null) BindPathUI();

        _rescanTimer -= Time.unscaledDeltaTime;
        if (_rescanTimer <= 0f)
        {
            _rescanTimer = _rescanInterval;
            Rescan();
        }

        UpdateVisibility();
    }

    // 軌道表示UIのModelを作って公開し、今ある動くSAポイントを登録する
    private void BindPathUI()
    {
        _pathModel = new CS_UISAPathModel();
        _pathModel.Bind();
        _rescanTimer = _rescanInterval;
        Rescan();
    }

    private void UnbindPathUI()
    {
        if (_pathModel == null) return;

        _paths.Clear();
        _pathsBySaPoint.Clear();
        _shownPath = null;
        _pathModel.Dispose();
        _pathModel = null;
    }

    // シーンにある動くSAポイントを集め直す(増えた物は登録、消えた物は外す)
    private void Rescan()
    {
        CS_PathFollower[] followers = FindObjectsByType<CS_PathFollower>(FindObjectsSortMode.None);
        foreach (CS_PathFollower follower in followers)
        {
            if (_paths.ContainsKey(follower)) continue;
            if (!follower.isActiveAndEnabled || follower.saPoint == null || follower.pointCount < 2) continue;   // 動いていない物は対象外

            Register(follower);
        }

        // 消えた物(Destroyされた物)を外す
        _removeList.Clear();
        foreach (CS_PathFollower follower in _paths.Keys)
        {
            if (follower == null) _removeList.Add(follower);
        }
        foreach (CS_PathFollower follower in _removeList)
        {
            PathData path = _paths[follower];
            if (_shownPath == path) _shownPath = null;
            _pathModel.RemovePath(path.id);
            _pathsBySaPoint.Remove(path.saPoint);
            _paths.Remove(follower);
        }
    }

    // 道筋を、ワールドの点の並びにして1回だけ取り、Modelに登録する
    private void Register(CS_PathFollower follower)
    {
        List<Vector3> localPoints = follower.GetCurvePoints(_pointsPerSegment);
        Vector3[] worldPoints = new Vector3[localPoints.Count];
        for (int i = 0; i < worldPoints.Length; i++)
        {
            worldPoints[i] = follower.transform.TransformPoint(localPoints[i]);
        }

        PathData path = new PathData { id = _pathModel.AddPath(worldPoints), saPoint = follower.saPoint };
        _paths.Add(follower, path);
        _pathsBySaPoint[path.saPoint] = path;
    }

    // 強調UIが出ているSAポイント(今狙っているSAポイント)の軌道だけを出す。変わった時だけModelへ伝える
    private void UpdateVisibility()
    {
        CS_SAPoint candidate = _hide.currentCandidate;
        PathData target = null;
        if (candidate != null) _pathsBySaPoint.TryGetValue(candidate, out target);

        if (target == _shownPath) return;

        if (_shownPath != null) _pathModel.SetVisible(_shownPath.id, false);
        if (target != null) _pathModel.SetVisible(target.id, true);
        _shownPath = target;
    }
}
