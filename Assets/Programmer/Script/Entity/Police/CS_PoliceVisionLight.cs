/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-11-28 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 警察の視界を、懐中電灯の光のように立体で表示するクラス(警察のプレハブに付ける。見た目だけで、警察の判断は変えない)
/// ・発見視界: 目の位置から広がる、断面が丸い光の束。縁と先端だけが、少しにじんで消える。壁に当たる所で光が止まる
///   見つかる条件は範囲の中ならどこでも同じなので、左右の濃さは同じにする(中心だけを明るくしない)
///   地面と壁には、縁のはっきりしない、光の当たった跡を出す
///   視界の端は薄くなるので、見えている光より、視界の方が少しだけ広い
/// ・巡回時の視界(怪しんで近づいてくる範囲): 地面に、薄い光のもれのように表示する
/// ・色は状態で変える: 巡回中 / 怪しんでいる・駆け付けている・探している / 追跡中
/// ・スタンしている間・消える演出の間は、見ていないので表示しない
/// ・自分(ローカルプレイヤー)から決めた距離より遠い警察は表示しない(遠い端ほど薄くする)
/// 警察の判断と視界の値はサーバー(またはオフライン)にしか無いので、視界の広さと状態はNetworkVariableで全員へ同期し、各クライアントが自分の画面に作る
/// 光のオブジェクトは、警察の子にしない(スキャンの強調表示など、警察の見た目をまとめて扱う処理に、光を巻き込まないため)
/// </summary>
[RequireComponent(typeof(CS_PoliceBrain), typeof(CS_PoliceVision))]
public class CS_PoliceVisionLight : NetworkBehaviour
{
    // 表示する状態(同期する値)
    private enum LightMode : byte
    {
        Off,        // 表示しない(準備前・スタン中・消える演出中)
        Patrol,     // 巡回・待機
        Wary,       // 怪しんでいる・駆け付けている・探している
        Chase,      // 追跡中
    }

    // 光の形(壁に当たる距離)を調べ直す間隔(秒)。位置と向きは毎フレーム合わせる
    private const float _rebuildInterval = 0.1f;

    // 光を調べる向きの間隔(度)と、本数の範囲
    private const float _degreesPerRay = 4.0f;
    private const int _minSegments = 8;
    private const int _maxSegments = 70;

    // 光の束・地面の光を、目から先端まで区切る数
    private const int _lengthSteps = 10;

    // 光の束が、目の高さより上に膨らむ高さ(m)
    private const float _beamTopRise = 0.25f;

    // 地面を探す高さ(警察の中心から下へ。m)と、地面に重ならないよう浮かせる高さ(m)
    private const float _groundSearchDistance = 3.0f;
    private const float _groundOffset = 0.06f;

    // 壁に当たった時に、壁にめり込まないよう手前にする距離(m)
    private const float _wallOffset = 0.05f;

    // 距離で薄くし始める位置(表示する距離に対する割合)
    private const float _fadeStartRate = 0.75f;

    // 視界の広さ(x: 発見の角度 / y: 発見の距離 / z: 巡回時の角度 / w: 巡回時の距離)と状態。書き込みはサーバーのみ
    private readonly NetworkVariable<Vector4> _syncedRanges = new NetworkVariable<Vector4>();
    private readonly NetworkVariable<byte> _syncedMode = new NetworkVariable<byte>();

    // オフライン(NetworkManagerが動いていない)の時の値
    private Vector4 _offlineRanges = Vector4.zero;
    private LightMode _offlineMode = LightMode.Off;

    private CS_PoliceBrain _brain = null;
    private CS_PoliceVision _vision = null;

    // 光の見た目(警察の子にしないオブジェクト)
    private GameObject _lightObject = null;
    private MeshRenderer _renderer = null;
    private Mesh _mesh = null;

    private readonly List<Vector3> _vertices = new List<Vector3>();
    private readonly List<Color> _colors = new List<Color>();
    private readonly List<int> _triangles = new List<int>();

    // 頂点ごとの、見る角度で薄くする強さ(光の束は1: 輪郭がぼける / 地面や壁の光は0)
    private readonly List<Vector2> _softWeights = new List<Vector2>();

    // 向きごとの調べた結果(向き・光が届く距離・壁に当たったか・左右の端に近い分の薄さ)
    private readonly List<RaySample> _samples = new List<RaySample>();

    private struct RaySample
    {
        public Vector3 direction;
        public float length;
        public bool isHit;
        public float across;        // 視界の中心を0、左右の端を1とした位置
        public float sideFade;
    }

    private float _rebuildTimer = 0.0f;
    private LightMode _builtMode = LightMode.Off;
    private float _builtFade = -1.0f;

    [Header("＝＝＝ 表示 ＝＝＝")]
    [SerializeField]
    [Tooltip("光のマテリアル(MT_PoliceVisionLight)。未設定なら表示しない")]
    private Material _material = null;

    [SerializeField, Min(1f)]
    [Tooltip("自分のプレイヤーからこの距離(m)以内の警察の視界を表示する(端に近いほど薄くする)")]
    private float _showDistance = 40.0f;

    [SerializeField]
    [Tooltip("巡回時の視界(怪しんで近づいてくる範囲)も、地面に薄く表示するか")]
    private bool _showSuspicionRange = true;

    [SerializeField]
    [Tooltip("地面の光だけを表示するか。オフにすると、目の位置から広がる光の束と、壁に当たる光も表示する(どちらも、壁の向こうには光が届かない)")]
    private bool _groundOnly = true;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("地面の光だけを表示する時の、発見視界の地面の濃さ")]
    private float _groundOnlyAlpha = 0.55f;

    [Header("＝＝＝ 色(状態ごと) ＝＝＝")]
    [SerializeField]
    private Color _patrolColor = new Color(0.35f, 0.65f, 1.0f);

    [SerializeField]
    [Tooltip("怪しんでいる・駆け付けている・探している時の色")]
    private Color _waryColor = new Color(1.0f, 0.75f, 0.15f);

    [SerializeField]
    private Color _chaseColor = new Color(1.0f, 0.2f, 0.15f);

    [Header("＝＝＝ 光の濃さ ＝＝＝")]
    [SerializeField, Range(0f, 1f)]
    [Tooltip("光の束の、目の近くの濃さ(先端へ行くほど薄くなる)")]
    private float _lightBeamAlpha = 0.42f;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("光の束の、先端の近くの濃さ")]
    private float _lightBeamTipAlpha = 0.12f;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("地面の、光が当たっている所の濃さ")]
    private float _lightGroundAlpha = 0.36f;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("壁の、光が当たっている所の濃さ")]
    private float _lightWallAlpha = 0.38f;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("巡回時の視界の、地面の光のもれの濃さ")]
    private float _lightSpillAlpha = 0.09f;

    [Header("＝＝＝ ぼかし(内側 = 発見視界。弱めにして、見つかる範囲が分かるようにする) ＝＝＝")]
    [SerializeField, Range(0.02f, 1f)]
    [Tooltip("発見視界の、左右の端のぼけ具合。端から、この割合の幅をかけて薄くする(大きいほど、ぼける)")]
    private float _innerEdgeSoftness = 0.18f;

    [SerializeField, Range(0.02f, 1f)]
    [Tooltip("発見視界の、先端のぼけ具合。先端から、この割合の長さをかけて消す(大きいほど、ぼける)")]
    private float _innerTipSoftness = 0.12f;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("発見視界の光の束の、斜めから見た輪郭のぼけ具合(0ならぼかさない)")]
    private float _innerSilhouetteSoftness = 0.15f;

    [Header("＝＝＝ ぼかし(外側 = 巡回時の視界。強めにして、光のもれに見せる) ＝＝＝")]
    [SerializeField, Range(0.02f, 1f)]
    [Tooltip("巡回時の視界の、左右の端のぼけ具合(大きいほど、ぼける)")]
    private float _outerEdgeSoftness = 0.6f;

    [SerializeField, Range(0.02f, 1f)]
    [Tooltip("巡回時の視界の、先端のぼけ具合(大きいほど、ぼける)")]
    private float _outerTipSoftness = 0.55f;

    private void Awake()
    {
        _brain = GetComponent<CS_PoliceBrain>();
        _vision = GetComponent<CS_PoliceVision>();
    }

    private void Update()
    {
        // 判断をしているマシン(サーバー、またはオフライン)で、今の視界の広さと状態を知らせる
        if (CS_PoliceSquad.IsNetworkClientOnly()) return;

        _offlineRanges = new Vector4(_vision.viewAngle, _vision.viewDistance, _vision.suspicionAngle, _vision.suspicionDistance);
        _offlineMode = GetCurrentMode();
        if (!IsSpawned || !IsServer) return;

        // 同じ値を入れても同期されないので、毎回入れてよい
        _syncedRanges.Value = _offlineRanges;
        _syncedMode.Value = (byte)_offlineMode;
    }

    private void LateUpdate()
    {
        bool isClientOnly = CS_PoliceSquad.IsNetworkClientOnly();
        Vector4 ranges = isClientOnly ? _syncedRanges.Value : _offlineRanges;
        LightMode mode = isClientOnly ? (LightMode)_syncedMode.Value : _offlineMode;

        float fade = GetDistanceFade();
        if (_material == null || mode == LightMode.Off || ranges.y <= 0.0f || fade <= 0.0f)
        {
            SetVisible(false);
            return;
        }

        EnsureLightObject();
        SetVisible(true);

        // 光の位置と向きは、毎フレーム、目の位置と水平の向きに合わせる
        Vector3 forward = new Vector3(transform.forward.x, 0.0f, transform.forward.z);
        Quaternion yaw = forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward) : Quaternion.identity;
        _lightObject.transform.SetPositionAndRotation(transform.position + Vector3.up * _vision.eyeHeight, yaw);

        // 形は間隔をあけて作り直す(状態が変わった時と、薄さが大きく変わった時は、すぐ作り直す)
        _rebuildTimer -= Time.deltaTime;
        if (_rebuildTimer > 0.0f && mode == _builtMode && Mathf.Abs(fade - _builtFade) < 0.1f) return;

        _rebuildTimer = _rebuildInterval;
        _builtMode = mode;
        _builtFade = fade;
        Rebuild(ranges, GetColor(mode), fade);
    }

    public override void OnDestroy()
    {
        if (_lightObject != null) Destroy(_lightObject);
        if (_mesh != null) Destroy(_mesh);
        base.OnDestroy();
    }

    private void OnDisable()
    {
        SetVisible(false);
    }

    // 今の警察の状態から、表示する状態を決める(サーバー、またはオフライン)
    private LightMode GetCurrentMode()
    {
        if (!_brain.isActing || _brain.isStunned) return LightMode.Off;

        switch (_brain.state)
        {
            case CSE_PoliceMoveState.Chase: return LightMode.Chase;
            case CSE_PoliceMoveState.Suspect:
            case CSE_PoliceMoveState.Rush:
            case CSE_PoliceMoveState.Search: return LightMode.Wary;
            default: return LightMode.Patrol;
        }
    }

    private Color GetColor(LightMode mode)
    {
        if (mode == LightMode.Chase) return _chaseColor;
        return mode == LightMode.Wary ? _waryColor : _patrolColor;
    }

    // 自分のプレイヤー(いなければカメラ)からの距離で決める濃さ(0なら表示しない)
    private float GetDistanceFade()
    {
        Transform viewer = null;
        foreach (CS_Player player in CS_Player.activePlayers)
        {
            if (player == null || !player.isLocalPlayer) continue;

            viewer = player.transform;
            break;
        }
        if (viewer == null && Camera.main != null) viewer = Camera.main.transform;
        if (viewer == null) return 0.0f;

        float distance = Vector3.Distance(viewer.position, transform.position);
        return Mathf.Clamp01((_showDistance - distance) / (_showDistance * (1.0f - _fadeStartRate)));
    }

    private void SetVisible(bool visible)
    {
        if (_renderer != null && _renderer.enabled != visible) _renderer.enabled = visible;
        if (!visible) _builtMode = LightMode.Off;   // 次に表示する時は、すぐ作り直す
    }

    // 光のオブジェクトを、最初に表示する時に作る
    private void EnsureLightObject()
    {
        if (_lightObject != null) return;

        _lightObject = new GameObject($"PoliceVisionLight ({name})");
        // 警察と同じシーンに置く(シーンを切り替えた時に、残らないようにする)
        if (gameObject.scene.IsValid()) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_lightObject, gameObject.scene);

        _mesh = new Mesh { name = "PoliceVisionLight" };
        _mesh.MarkDynamic();
        _lightObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
        _renderer = _lightObject.AddComponent<MeshRenderer>();
        _renderer.sharedMaterial = _material;
        _renderer.shadowCastingMode = ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
    }

    // 光の形を作り直す(座標は、目の位置が原点で、警察の正面が+Z)
    private void Rebuild(Vector4 ranges, Color color, float fade)
    {
        _vertices.Clear();
        _colors.Clear();
        _triangles.Clear();
        _softWeights.Clear();

        Vector3 eye = _lightObject.transform.position;
        Quaternion yaw = _lightObject.transform.rotation;
        float floorY = FindGroundHeight() + _groundOffset - eye.y;

        // 発見視界: 光の束・地面の光・壁の光
        CollectSamples(eye, yaw, ranges.x, ranges.y, _innerEdgeSoftness);
        if (_groundOnly)
        {
            // 地面だけの時は、手元もすぐ明るくする(手元が安全に見えないように)
            AddGroundLight(floorY, 0.0f, _groundOnlyAlpha, _innerTipSoftness, 0.08f, color, fade);
        }
        else
        {
            AddBeam(floorY, color, fade);
            AddGroundLight(floorY, 0.0f, _lightGroundAlpha, _innerTipSoftness, 0.3f, color, fade);
            AddWallLight(ranges.y, floorY, color, fade);
        }

        // 巡回時の視界: 発見視界より外側へ、もれる光
        if (_showSuspicionRange && ranges.w > ranges.y)
        {
            CollectSamples(eye, yaw, ranges.z, ranges.w, _outerEdgeSoftness);
            AddGroundLight(floorY + 0.01f, ranges.y * 0.6f, _lightSpillAlpha, _outerTipSoftness, 0.3f, color, fade);
        }

        _mesh.Clear();
        _mesh.SetVertices(_vertices);
        _mesh.SetColors(_colors);
        _mesh.SetUVs(0, _softWeights);
        _mesh.SetTriangles(_triangles, 0);
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();
    }

    // 向きごとに、光が届く距離と、左右の端に近い分の薄さを調べる
    private void CollectSamples(Vector3 eye, Quaternion yaw, float angle, float distance, float edgeSoftness)
    {
        int segments = Mathf.Clamp(Mathf.CeilToInt(angle / _degreesPerRay), _minSegments, _maxSegments);
        bool isFullCircle = angle >= 359.0f;

        _samples.Clear();
        for (int i = 0; i <= segments; i++)
        {
            float degrees = -angle * 0.5f + angle * i / segments;
            RaySample sample;
            sample.direction = Quaternion.Euler(0.0f, degrees, 0.0f) * Vector3.forward;
            sample.length = MeasureLength(eye, yaw * sample.direction, distance, out sample.isHit);
            sample.across = angle > 0.0f ? Mathf.Abs(degrees) / (angle * 0.5f) : 0.0f;
            sample.sideFade = isFullCircle ? 1.0f : 1.0f - Smooth(1.0f - edgeSoftness, 1.0f, sample.across);
            _samples.Add(sample);
        }
    }

    // 目から広がる光の束を足す。断面は丸く(左右の端ほど上下が細くなる)、束の中心の線は、先端へ向かって下がる
    private void AddBeam(float floorY, Color color, float fade)
    {
        AddBeamSurface(floorY, color, fade, 1.0f);    // 上の面
        AddBeamSurface(floorY, color, fade, -1.0f);   // 下の面
    }

    private void AddBeamSurface(float floorY, Color color, float fade, float side)
    {
        int start = _vertices.Count;
        float halfThickness = Mathf.Abs(floorY) * 0.5f + _beamTopRise;

        foreach (RaySample sample in _samples)
        {
            float across = Mathf.Clamp01(sample.across);
            float round = Mathf.Sqrt(1.0f - across * across);

            for (int k = 0; k <= _lengthSteps; k++)
            {
                float rate = (float)k / _lengthSteps;
                float axisY = floorY * 0.5f * rate;
                float y = Mathf.Max(floorY, axisY + side * halfThickness * rate * round);
                Vector3 point = sample.direction * (sample.length * rate);
                point.y = y;

                float alpha = Mathf.Lerp(_lightBeamAlpha, _lightBeamTipAlpha, rate) * TipFade(rate, sample.isHit, _innerTipSoftness) * sample.sideFade;
                AddVertex(point, WithAlpha(color, alpha * fade), _innerSilhouetteSoftness);
            }
        }
        AddGridTriangles(start, _samples.Count, _lengthSteps + 1);
    }

    // 地面の、光が当たっている所を足す。左右の濃さは同じで、端と先端だけが、にじむように消える
    // nearSoftness: 塗り始めから、この割合の長さをかけて、明るくなる
    private void AddGroundLight(float floorY, float startDistance, float strength, float tipSoftness, float nearSoftness, Color color, float fade)
    {
        int start = _vertices.Count;
        foreach (RaySample sample in _samples)
        {
            float from = Mathf.Min(startDistance, sample.length);
            for (int k = 0; k <= _lengthSteps; k++)
            {
                float rate = (float)k / _lengthSteps;
                Vector3 point = sample.direction * Mathf.Lerp(from, sample.length, rate);
                point.y = floorY;

                // 塗り始めは少しずつ明るくし、遠くへ行くほど少し薄くする(懐中電灯を、少し下へ向けて照らした時の形)
                float shape = Smooth(0.0f, nearSoftness, rate) * Mathf.Lerp(1.0f, 0.65f, rate) * TipFade(rate, sample.isHit, tipSoftness);
                AddVertex(point, WithAlpha(color, strength * shape * sample.sideFade * fade), 0.0f);
            }
        }
        AddGridTriangles(start, _samples.Count, _lengthSteps + 1);
    }

    // 壁に当たっている所の光を足す(真ん中の高さが一番明るく、上下へ消える。近い壁ほど明るい)
    private void AddWallLight(float distance, float floorY, Color color, float fade)
    {
        const int rows = 5;
        int start = _vertices.Count;
        float top = _beamTopRise * 2.0f;
        foreach (RaySample sample in _samples)
        {
            float closeness = Mathf.Lerp(1.0f, 0.35f, distance > 0.0f ? sample.length / distance : 1.0f);
            for (int r = 0; r < rows; r++)
            {
                float height = (float)r / (rows - 1);
                Vector3 point = sample.direction * sample.length;
                point.y = Mathf.Lerp(floorY, top, height);

                float shape = Mathf.Sin(Mathf.Lerp(0.12f, 1.0f, height) * Mathf.PI);
                float alpha = sample.isHit ? _lightWallAlpha * shape * closeness * sample.sideFade : 0.0f;
                AddVertex(point, WithAlpha(color, alpha * fade), 0.0f);
            }
        }
        AddGridTriangles(start, _samples.Count, rows);
    }

    // 先端へ向かって消していく割合。壁に当たっている時は、壁まで光が届くので、消しきらない
    private static float TipFade(float rate, bool isHit, float softness)
    {
        float tip = 1.0f - Smooth(1.0f - softness, 1.0f, rate);
        return isHit ? Mathf.Max(tip, 0.45f) : tip;
    }

    // valueがfromからtoへ進むのに合わせて、0から1へ、なめらかに変わる値
    private static float Smooth(float from, float to, float value)
    {
        return Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(from, to, value));
    }

    // その向きに、光が届く距離(壁などに当たれば、そこまで)
    private static float MeasureLength(Vector3 eye, Vector3 direction, float distance, out bool isHit)
    {
        isHit = Physics.Raycast(eye, direction, out RaycastHit hit, distance, CS_PoliceLayers.obstacleLayers, QueryTriggerInteraction.Ignore);
        return isHit ? Mathf.Max(0.0f, hit.distance - _wallOffset) : distance;
    }

    // 警察の足元の地面の高さ(見つからなければ、目の高さから2m下とする)
    private float FindGroundHeight()
    {
        return Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, _groundSearchDistance,
            CS_PoliceLayers.obstacleLayers, QueryTriggerInteraction.Ignore)
            ? hit.point.y : _lightObject.transform.position.y - 2.0f;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private void AddVertex(Vector3 position, Color color, float softWeight)
    {
        _vertices.Add(position);
        _colors.Add(color);
        _softWeights.Add(new Vector2(softWeight, 0.0f));
    }

    // 列(columns)と行(rows)に並べて足した頂点を、三角形でつなぐ(頂点を共有するので、面の向きがなめらかになる)
    private void AddGridTriangles(int start, int columns, int rows)
    {
        for (int c = 0; c < columns - 1; c++)
        {
            for (int r = 0; r < rows - 1; r++)
            {
                int a = start + c * rows + r;
                int b = a + rows;
                _triangles.Add(a);
                _triangles.Add(a + 1);
                _triangles.Add(b + 1);
                _triangles.Add(a);
                _triangles.Add(b + 1);
                _triangles.Add(b);
            }
        }
    }
}
