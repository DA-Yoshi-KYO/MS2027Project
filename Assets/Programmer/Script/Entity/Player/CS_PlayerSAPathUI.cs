using System.Collections.Generic;
using UnityEngine;

/*
 * 動くSAポイント(CS_PathFollowerで動くSAポイント)の軌道(道筋)を、プレイヤーが近づいた時だけ表示する(Issue #507)ために、軌道表示UI(CS_UISAPathModel)へ渡すクラス
 * シーンにある動くSAポイントの道筋を軌道表示UIのModelに登録し、近いかどうかで出す・消すを切り替えるだけ
 * UI側(Model/Presenter/View)はこのクラス・CS_Player・CS_PathFollowerの存在を一切知らない
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・動かないSAポイントには出さない(CS_PathFollowerが付いていない物は対象にならない)
 * ・出す条件: プレイヤーから、道筋の一番近い点までの距離が_showDistance以内。離れて_showDistance + _hideMarginを超えると消す
 *   (境目でちらつかないよう、出す距離より消す距離を少し長くしている)。ふわっと出入りするフェードはViewが行う
 *   距離は_checkInterval秒ごとに調べる(毎フレームは調べない)
 * ・道筋は動かないので、軌道を登録する時に1回だけ点の並び(CS_PathFollower.GetCurvePoints。ワールドの座標にして)を取って使い回す
 *   パスをこのオブジェクトごと動かした場合は、登録し直さないと反映されない(再生中に動かさない前提)
 * ・シーンにあるCS_PathFollowerは、最初と_rescanInterval秒ごとに集め直す(FindObjectsByType。毎フレームは探さない)
 *   途中で増えた物は登録し、消えた物は外す
 * ・軌道は自分の画面に出すもの全部で1つのため、自分(isLocalPlayer)のプレイヤーだけがModelを作ってBindする
 *   isLocalPlayerはスポーン後に決まるため、毎フレーム確かめて、自分のプレイヤーになった時にBindする
 * ・見た目(色・太さ・点線の間隔・フェード)はUICanvas側(SAPath)のViewで決める
 * ・CS_PathFollower・CS_SAPointのロジックはこのクラスを知らない(外してもSAは動く)
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでも単体で動く。通信はしない
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
public class CS_PlayerSAPathUI : MonoBehaviour
{
    // 登録した軌道1つ分
    private class PathData
    {
        public int id;                  // Modelでの軌道の番号
        public Vector3[] points;        // 道筋の点の並び(ワールド)
        public bool isVisible;          // 今出しているか
    }

    [Header("表示")]
    [SerializeField][Min(0f)] private float _showDistance = 50f;     // 道筋の一番近い点がこの距離以内なら出す(m)
    [SerializeField][Min(0f)] private float _hideMargin = 5f;        // 出している軌道を消す距離に足す余裕(m。ちらつき防止)

    [Header("調べる間隔")]
    [SerializeField][Min(0.02f)] private float _checkInterval = 0.2f;     // 近いかを調べる間隔(秒)
    [SerializeField][Min(0.5f)] private float _rescanInterval = 5f;       // 動くSAポイントを集め直す間隔(秒)

    [Header("道筋")]
    [SerializeField][Range(1, 32)] private int _pointsPerSegment = 12;    // 道筋の1区間あたりの分割数(多いほどなめらか)

    private CS_Player _player;
    private CS_UISAPathModel _pathModel;
    private readonly Dictionary<CS_PathFollower, PathData> _paths = new Dictionary<CS_PathFollower, PathData>();
    private readonly List<CS_PathFollower> _removeList = new List<CS_PathFollower>();
    private float _checkTimer;
    private float _rescanTimer;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
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

        _checkTimer -= Time.unscaledDeltaTime;
        if (_checkTimer <= 0f)
        {
            _checkTimer = _checkInterval;
            UpdateVisibility();
        }
    }

    // 軌道表示UIのModelを作って公開し、今ある動くSAポイントを登録する
    private void BindPathUI()
    {
        _pathModel = new CS_UISAPathModel();
        _pathModel.Bind();
        _rescanTimer = _rescanInterval;
        _checkTimer = 0f;
        Rescan();
    }

    private void UnbindPathUI()
    {
        if (_pathModel == null) return;

        _paths.Clear();
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
            _pathModel.RemovePath(_paths[follower].id);
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

        _paths.Add(follower, new PathData { id = _pathModel.AddPath(worldPoints), points = worldPoints });
    }

    // プレイヤーから道筋の一番近い点までの距離で、出す・消すを決める
    private void UpdateVisibility()
    {
        Vector3 position = transform.position;
        foreach (PathData path in _paths.Values)
        {
            float distance = GetNearestDistance(path.points, position);
            float limit = path.isVisible ? _showDistance + _hideMargin : _showDistance;
            bool isVisible = distance <= limit;
            if (isVisible == path.isVisible) continue;

            path.isVisible = isVisible;
            _pathModel.SetVisible(path.id, isVisible);
        }
    }

    // 点の並びのうち、positionに一番近い点までの距離
    private static float GetNearestDistance(Vector3[] points, Vector3 position)
    {
        float nearestSqr = float.MaxValue;
        foreach (Vector3 point in points)
        {
            float sqr = (point - position).sqrMagnitude;
            if (sqr < nearestSqr) nearestSqr = sqr;
        }
        return Mathf.Sqrt(nearestSqr);
    }
}
