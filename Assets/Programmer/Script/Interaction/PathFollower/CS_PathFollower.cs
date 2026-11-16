using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/*
 * パスに沿ってSAポイントを動かすクラス
 * SAポイントの親に付ける。子の制御点(CS_PathFollowerPoint)をなめらかな曲線でつなぎ、その上でSAポイントを動かす
 * 動かすのはSAポイント(CS_SAPoint)だけ。立つ位置(StandPoint)・SAできる範囲(SAReachBox)も、SAポイントの子なので一緒に動く
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・置き方
 *     MovingTrafficLight (CS_PathFollower。動かない)
 *       ├ TrafficLight (SAポイントのプレハブ。これが動く)
 *       └ PathPoints   (空のオブジェクト。名前は何でもよい)
 *           ├ Point_0 (CS_PathFollowerPoint)
 *           └ Point_1 …
 *   制御点はSAポイントの子にしない(一緒に動いてしまうため)。CS_PathFollowerの子に置く
 *   曲線の座標は、このオブジェクト(CS_PathFollower)の座標系で持つ。このオブジェクトごと動かしても、パスはついてくる
 * ・位置は「経過時間」だけで決める(マルチで全員が同じ位置にいるため)
 *   前回の位置や、フレームの間隔(deltaTime)は使わない。同じ入力(点・待ち時間・速さ・動き方・時間)なら、いつ・誰が計算しても同じ位置になる
 * ・同期: 位置の送信やNetworkTransformは使わない。基準時刻は、全員で共通の時刻にする
 *   ネットワークにつながっている(サーバー・ホスト、または接続済みのクライアント)間は、NetcodeのサーバーTime(NetworkManager.ServerTime)
 *   ネットワーク未接続(ソロ・オフライン)の間は、Unityの時間(Time.timeAsDouble)
 *   マルチでは、全員のシーンに同じ物を同じ位置に置いておけば、同じ時刻には同じ位置になる
 * ・曲線はCatmull-Rom(制御点を必ず通るなめらかな曲線)。区間(点iから点i+1まで)ごとに曲線の長さを測っておき、
 *   「速さ(m/秒)で進んだ距離」から曲線上の位置を求める(曲線の途中で速さが変わらない)
 *   曲線の端(一方向・往復の最初と最後)は、1つ先の点を折り返して仮に置いて計算する
 * ・点ごとの値(CS_PathFollowerPoint)
 *   待ち時間(秒): その点に着いてから、動き出すまで止まっている時間
 *   速さ(m/秒): 動き出す点から、次に向かう点までの速さ。行きは「その点から次の点まで」、往復の帰り道は「その点から1つ前の点まで」
 *   最後の点の速さは、周回(最後の点から最初の点へ戻る区間)と往復(帰り道の最初の区間)で使う。一方向では使わない
 * ・動き方のカーブ(イーズ): 区間の中の進み方(CS_PathFollowerPointの_goCurve・_backMode・_backCurve)
 *   区間の時間(長さ÷速さ)・待ち時間は、カーブでは変わらない。区間の中で、時間の進み具合を距離の進み具合にする時だけ、カーブを通す
 *   区間の持ち主は、区間の始点の点。往復の帰り道は、帰りの選び方で決める(Copy: 行きと同じ / Inverse: 行きの逆再生 / Custom: 帰り専用)
 *   帰りの速さは、カーブとは違い、出発する点(次の点)の値を使う。カーブが端で0・1にならなくても、頭を0、終わりを1にそろえる
 * ・時間の流れ
 *   一方向: 待つ0 → 動く0 → 待つ1 → 動く1 → … → 最後の点で止まる(最後の点の待ち時間は使わない)
 *   周回:   待つ0 → 動く0 → … → 動く(最後→0)を繰り返す
 *   往復:   待つ0 → 動く0 → … → 待つ(最後) → 動く(最後→最後-1) → 待つ(最後-1) → … → 動く(1→0)を繰り返す
 *           帰り道も、途中の点に着いたらその点の待ち時間だけ止まる(始点の待ちは、次の周期の頭の1回)
 * ・動かす向きは変えない(位置だけ動かす)
 * ・パスの点(制御点)は、再生を始める時に読み込む。再生中に点を動かしても反映されない
 * ・SAポイントは物理を使わず、Updateで位置を動かす。CS_PlayerHideは飛んでいる間、毎物理フレーム立つ位置を読み直すため、動く的にもついていく
 * ・エディタ(CSED_PathFollowerEditor)は、再生していない間にRefreshForEditorで点を読み直して、曲線・長さ・時間を表示する
 */
// ========================================

public class CS_PathFollower : MonoBehaviour
{
    // 1つの点の入力(座標はこのオブジェクトの座標系)
    private struct PointData
    {
        public Vector3 position;        // 位置
        public float waitTime;          // 着いてから動き出すまでの待ち時間(秒)
        public float speedToNext;       // 次の点までの速さ(m/秒)
        public AnimationCurve goCurve;              // 行き(この点から次の点へ)の進み方
        public CSE_PathFollowEaseMode backMode;     // 帰りの進み方の選び方
        public AnimationCurve backCurve;            // 帰りの進み方(Customの時だけ使う)
    }

    // 時間の流れの1区切り(待つか、1つの区間を動くか)
    private struct Step
    {
        public double start;        // 始まる時間(秒。周期の頭から)
        public double duration;     // かかる時間(秒)
        public int segment;         // 動く区間(待つ時は-1)
        public bool reverse;        // 区間を逆向きに動くか(往復の帰り道)
        public int point;           // 待つ点(動く時は-1)
    }

    private const int _samplesPerSegment = 24;      // 区間の長さを測る時の、曲線の分割数
    private const float _minSpeed = 0.01f;          // 速さの下限(0で割らないように)
    private const float _minSegmentLength = 0.0001f;    // これより短い区間は動かない扱いにする(m)

    [Header("動かす物")]
    [SerializeField] private CS_SAPoint _saPoint;                      // 動かすSAポイント(空なら子から探す)

    [Header("動き方")]
    [SerializeField] private CSE_PathFollowMode _mode = CSE_PathFollowMode.PingPong;   // 一方向・周回・往復

    private PointData[] _points = new PointData[0];
    private int _segmentCount;
    private float[][] _arcTable = new float[0][];       // 区間ごとの、曲線の始まりからの長さ(分割ごと。[区間][分割数 + 1])
    private float[] _segmentLength = new float[0];      // 区間ごとの長さ(m)
    private readonly List<Step> _steps = new List<Step>();
    private double _cycleDuration;

    public CS_SAPoint saPoint => _saPoint;
    public CSE_PathFollowMode mode => _mode;
    public int pointCount => _points.Length;
    public int segmentCount => _segmentCount;
    public double cycleDuration => _cycleDuration;      // 1周(一方向なら、最後の点に着くまで)にかかる時間(秒)

    // 区間の長さの合計(m。往復の帰り道は数えない)
    public float totalLength
    {
        get
        {
            float sum = 0f;
            for (int i = 0; i < _segmentLength.Length; i++) sum += _segmentLength[i];
            return sum;
        }
    }

    // 今の速度(m/秒。ワールド)。経過時間の前後の位置の差から求める(待ち時間の間は0)。動く足場に乗った物の見た目の補正などに使う
    public Vector3 worldVelocity
    {
        get
        {
            if (_points.Length < 2) return Vector3.zero;

            const double half = 0.02;   // 前後に見る時間(秒)
            double now = GetBaseTime();
            Vector3 local = (EvaluateAt(now + half) - EvaluateAt(now - half)) / (float)(half * 2.0);
            return transform.TransformVector(local);
        }
    }

    private void Awake()
    {
        if (_saPoint == null) _saPoint = GetComponentInChildren<CS_SAPoint>(true);
        if (_saPoint == null)
        {
            Debug.LogWarning($"{name}: 動かすSAポイント(CS_SAPoint)が子にありません。動きません", this);
            enabled = false;
            return;
        }

        Rebuild();
        if (_points.Length < 2)
        {
            Debug.LogWarning($"{name}: パスの点(CS_PathFollowerPoint)が2つ以上必要です。動きません", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        // 最初のフレームから正しい位置にいる(1フレームだけ元の位置に見えないように)
        Apply();
    }

    private void Update()
    {
        Apply();
    }

    // 今の位置をSAポイントへ反映する
    private void Apply()
    {
        if (_points.Length < 2 || _saPoint == null) return;

        _saPoint.transform.position = transform.TransformPoint(EvaluateAt(GetBaseTime()));
    }

    // ========================================
    // 時刻
    // ========================================

    // 今の基準時刻(秒)。ネットワークにつながっていればサーバーTime、そうでなければUnityの時間
    public static double GetBaseTime()
    {
        NetworkManager manager = NetworkManager.Singleton;
        bool isConnected = manager != null && manager.IsListening && (manager.IsServer || manager.IsConnectedClient);
        return isConnected ? manager.ServerTime.Time : Time.timeAsDouble;
    }

    // ========================================
    // 制御点
    // ========================================

    // 子の制御点を、ヒエラルキーの上から順に返す
    public List<CS_PathFollowerPoint> GetPoints()
    {
        List<CS_PathFollowerPoint> points = new List<CS_PathFollowerPoint>();
        GetComponentsInChildren(true, points);
        return points;
    }

    // エディタ用: 再生していない間だけ、子の制御点を読み直す(再生中は、始める時に読み込んだままにする)
    public void RefreshForEditor()
    {
        if (!Application.isPlaying) Rebuild();
    }

    // 子の制御点を読み込み、曲線・区間の長さ・時間の流れを作り直す
    private void Rebuild()
    {
        List<CS_PathFollowerPoint> points = GetPoints();
        _points = new PointData[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            _points[i] = new PointData
            {
                position = transform.InverseTransformPoint(points[i].transform.position),
                waitTime = points[i].waitTime,
                speedToNext = points[i].speedToNext,
                goCurve = points[i].goCurve,
                backMode = points[i].backMode,
                backCurve = points[i].backCurve,
            };
        }

        // 周回は最後→最初の区間もある(点が2つだけでも、行って戻る2区間になる)
        if (_points.Length < 2) _segmentCount = 0;
        else _segmentCount = _mode == CSE_PathFollowMode.Loop ? _points.Length : _points.Length - 1;

        _arcTable = new float[_segmentCount][];
        _segmentLength = new float[_segmentCount];
        for (int i = 0; i < _segmentCount; i++) MeasureSegment(i);

        BuildSteps();
    }

    // ========================================
    // 位置を求める(経過時間 → 位置。このオブジェクトの座標系)
    // ========================================

    // 基準時刻baseTimeでの位置を返す(周期の頭が0。負の時間は0として扱う)
    public Vector3 EvaluateAt(double baseTime)
    {
        double time = baseTime;
        if (_points.Length == 0) return Vector3.zero;
        if (_steps.Count == 0 || _cycleDuration <= 0.0) return _points[0].position;
        if (time < 0.0) time = 0.0;

        if (_mode == CSE_PathFollowMode.Once)
        {
            if (time >= _cycleDuration) return _points[_points.Length - 1].position;
        }
        else
        {
            time %= _cycleDuration;
        }

        // 今の区切りを探す(点は多くないので、前から順に調べる)
        Step step = _steps[_steps.Count - 1];
        for (int i = 0; i < _steps.Count; i++)
        {
            if (time < _steps[i].start + _steps[i].duration)
            {
                step = _steps[i];
                break;
            }
        }

        if (step.segment < 0) return _points[step.point].position;

        // 区間の中の時間の進み具合を、カーブを通して、距離の進み具合にする(帰り道は、進む向きの割合を、始点からの距離に直す)
        float timeProgress = Mathf.Clamp01((float)((time - step.start) / step.duration));
        float progress = GetEase(step.segment, step.reverse, timeProgress);
        if (step.reverse) progress = 1f - progress;
        return EvaluateSegmentByDistance(step.segment, progress * _segmentLength[step.segment]);
    }

    // 区間segmentの、時間の進み具合t(0〜1)での、進んだ距離の割合(0〜1。進む向きの割合)。reverseは往復の帰り道か
    // 区間の持ち主は、区間の始点の点(segment)。帰りは、点の帰りの選び方(Copy / Inverse / Custom)で決める
    private float GetEase(int segment, bool reverse, float t)
    {
        PointData point = _points[segment];
        if (!reverse) return EvaluateEaseCurve(point.goCurve, t);

        switch (point.backMode)
        {
            case CSE_PathFollowEaseMode.Inverse:
                return 1f - EvaluateEaseCurve(point.goCurve, 1f - t);
            case CSE_PathFollowEaseMode.Custom:
                return EvaluateEaseCurve(point.backCurve, t);
            default:
                return EvaluateEaseCurve(point.goCurve, t);
        }
    }

    // カーブの値(0〜1)。カーブが端で0・1にならなくても、頭を0、終わりを1にそろえて、0〜1に収める(区間のつなぎ目で位置が飛ばないように)
    // カーブが無い・端の値が同じ(そろえられない)時は、直線(一定の速さ)にする
    private static float EvaluateEaseCurve(AnimationCurve curve, float t)
    {
        if (curve == null || curve.length == 0) return t;

        float start = curve.Evaluate(0f);
        float range = curve.Evaluate(1f) - start;
        if (Mathf.Abs(range) < 0.0001f) return t;

        return Mathf.Clamp01((curve.Evaluate(t) - start) / range);
    }

    // 区間segmentの、始まりからdistance(m)進んだ位置
    private Vector3 EvaluateSegmentByDistance(int segment, float distance)
    {
        float[] table = _arcTable[segment];
        distance = Mathf.Clamp(distance, 0f, _segmentLength[segment]);

        // distanceが入る分割を探し、その中を直線で補って、曲線のパラメータ(0〜1)にする
        int index = 0;
        while (index < _samplesPerSegment - 1 && table[index + 1] < distance) index++;

        float span = table[index + 1] - table[index];
        float fraction = span > 0f ? (distance - table[index]) / span : 0f;
        float t = (index + fraction) / _samplesPerSegment;
        return EvaluateCurve(segment, t);
    }

    // ========================================
    // 曲線
    // ========================================

    // 区間segmentの、曲線のパラメータt(0〜1)での位置(Catmull-Rom)
    private Vector3 EvaluateCurve(int segment, float t)
    {
        Vector3 p0 = GetControlPosition(segment - 1);
        Vector3 p1 = GetControlPosition(segment);
        Vector3 p2 = GetControlPosition(segment + 1);
        Vector3 p3 = GetControlPosition(segment + 2);

        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
    }

    // 曲線の計算に使う、index番目の点の位置(範囲の外は、周回なら反対側の点、それ以外は端を折り返した位置)
    private Vector3 GetControlPosition(int index)
    {
        int count = _points.Length;
        if (_mode == CSE_PathFollowMode.Loop)
        {
            return _points[((index % count) + count) % count].position;
        }
        if (index < 0) return 2f * _points[0].position - _points[1].position;
        if (index >= count) return 2f * _points[count - 1].position - _points[count - 2].position;
        return _points[index].position;
    }

    // 区間の長さを測る
    private void MeasureSegment(int segment)
    {
        float[] table = new float[_samplesPerSegment + 1];
        Vector3 previous = EvaluateCurve(segment, 0f);
        for (int i = 1; i <= _samplesPerSegment; i++)
        {
            Vector3 current = EvaluateCurve(segment, i / (float)_samplesPerSegment);
            table[i] = table[i - 1] + Vector3.Distance(previous, current);
            previous = current;
        }

        _arcTable[segment] = table;
        _segmentLength[segment] = table[_samplesPerSegment];
    }

    // 区間segmentの長さ(m)
    public float GetSegmentLength(int segment)
    {
        return segment >= 0 && segment < _segmentCount ? _segmentLength[segment] : 0f;
    }

    // 区間segmentにかかる時間(秒)。reverseは、往復の帰り道(区間を逆向きに動く)か
    // 速さは、動き出す側の点の値を使う(行きは点segment、帰りは点segment + 1)
    public float GetSegmentDuration(int segment, bool reverse = false)
    {
        if (segment < 0 || segment >= _segmentCount) return 0f;

        int from = reverse ? segment + 1 : segment;
        return _segmentLength[segment] / Mathf.Max(_points[from].speedToNext, _minSpeed);
    }

    // 曲線全体を、線で描くための点の並び(区間ごとにperSegment分割。このオブジェクトの座標系)
    public List<Vector3> GetCurvePoints(int perSegment)
    {
        List<Vector3> result = new List<Vector3>();
        perSegment = Mathf.Max(1, perSegment);
        if (_segmentCount == 0)
        {
            if (_points.Length > 0) result.Add(_points[0].position);
            return result;
        }

        for (int segment = 0; segment < _segmentCount; segment++)
        {
            // 区間のつなぎ目の点が二重にならないよう、最初の区間だけ頭の点も入れる
            for (int i = segment == 0 ? 0 : 1; i <= perSegment; i++)
            {
                result.Add(EvaluateCurve(segment, i / (float)perSegment));
            }
        }
        return result;
    }

    // ========================================
    // 時間の流れ
    // ========================================

    // 待つ・動くの並びを作り、1周の時間を決める
    private void BuildSteps()
    {
        _steps.Clear();
        _cycleDuration = 0.0;
        int count = _points.Length;
        if (count == 0) return;

        switch (_mode)
        {
            case CSE_PathFollowMode.Once:
                for (int i = 0; i < count - 1; i++)
                {
                    AddWait(i);
                    AddMove(i, false);
                }
                break;

            case CSE_PathFollowMode.Loop:
                for (int i = 0; i < count; i++)
                {
                    AddWait(i);
                    AddMove(i, false);
                }
                break;

            case CSE_PathFollowMode.PingPong:
                for (int i = 0; i < count - 1; i++)
                {
                    AddWait(i);
                    AddMove(i, false);
                }
                AddWait(count - 1);
                for (int i = count - 2; i >= 0; i--)
                {
                    AddMove(i, true);
                    if (i > 0) AddWait(i);      // 帰り道も、途中の点で待つ(始点の待ちは、次の周期の頭)
                }
                break;
        }
    }

    private void AddWait(int point)
    {
        float wait = Mathf.Max(0f, _points[point].waitTime);
        if (wait <= 0f) return;

        _steps.Add(new Step { start = _cycleDuration, duration = wait, segment = -1, reverse = false, point = point });
        _cycleDuration += wait;
    }

    private void AddMove(int segment, bool reverse)
    {
        if (_segmentLength[segment] < _minSegmentLength) return;

        double duration = GetSegmentDuration(segment, reverse);
        _steps.Add(new Step { start = _cycleDuration, duration = duration, segment = segment, reverse = reverse, point = -1 });
        _cycleDuration += duration;
    }

    // ========================================
    // シーンビューの表示
    // ========================================

    // このオブジェクトか、その子(制御点など)を選んでいる時に、パスの曲線を描く
    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        bool isSelected = false;
        foreach (Transform selected in UnityEditor.Selection.transforms)
        {
            if (selected.IsChildOf(transform)) isSelected = true;
        }
        if (!isSelected) return;

        RefreshForEditor();
        List<Vector3> curve = GetCurvePoints(16);
        Gizmos.color = new Color(1f, 0.6f, 0.1f);
        for (int i = 1; i < curve.Count; i++)
        {
            Gizmos.DrawLine(transform.TransformPoint(curve[i - 1]), transform.TransformPoint(curve[i]));
        }
#endif
    }
}
