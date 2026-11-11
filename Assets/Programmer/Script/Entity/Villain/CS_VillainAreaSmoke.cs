using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.VFX;

/*
 * 悪人エリアの紫の瘴気。出現点から路地裏の入口まで、地面を這って流れる
 * スポーン位置(CS_VillainSpawnPoint)に付け、そこに悪人グループがいる間だけ瘴気を出す
 *
 * 制作者：　Claude
 */

// ========================================
/*
 * メモ
 * ・使い方はClaudeDocs/悪人エリアの煙の使い方.md参照
 * ・瘴気は出現点のまわり(Spawn Radius)で生まれ、道に沿って一定の速さ(Flow Speed)で入口まで流れ、入口で薄れて消える
 *   流れながら路地の幅(左右の壁まで)いっぱいに広がる
 * ・入口は子のVillainAreaSmoke(VisualEffect 1つ)のTransform。出現点ごとにField.prefabで置く
 * ・道はゲーム開始時に、出現点から入口までNavMesh.CalculatePathで求める(曲がり角を最大8点。足りなければ長い区間の真ん中に点を足して8点)
 *   道が見つからない時は、出現点から入口まで真っすぐにする
 *   点ごとに、真下の地面(NavMesh)の高さに合わせ、左右の壁までの距離を測る
 *   (NavMesh.RaycastでNavMeshの端まで、Physics.Raycastで建物のコライダーまでを測り、近い方を使う。最大は _width の半分)
 * ・道は8x2のテクスチャ(1行目: RGB = 入口から見た位置, A = 始点からの道のり / 2行目: R・G = 左・右の壁までの距離)にしてVFXに渡す
 * ・粒は入口まで流れ切るまで生きるので、量(Spawn Rate)は道のりと速さから、生きている粒が約4500 × Density になるように決める
 * ・グループの有無はサーバーにしか無いので、サーバー(CS_VillainSpawner)がSetActiveでNetworkVariableに書き込み、
 *   全クライアントが瘴気を出す・止める(CS_VillainCrimeGaugeと同じ流れ)
 * ・止める時はVisualEffect.Stop(発生だけ止まり、今ある瘴気は寿命まで残る)にして、自然に消えるようにする
 * ・子のVillainAreaSmokeが非表示の時は瘴気を使わない(道も求めない)。表示はVillainSpawnPoint.prefabで切り替える
 * ・見た目(速さ・高さ・粒の大きさ・量など)は全部共通で、VEG_VillainAreaSmoke.vfxで変える
 */
// ========================================

public class CS_VillainAreaSmoke : NetworkBehaviour
{
    private static readonly int _pathTextureId = Shader.PropertyToID("Path Texture");
    private static readonly int _pathCountId = Shader.PropertyToID("Path Count");
    private static readonly int _pathLengthId = Shader.PropertyToID("Path Length");
    private static readonly int _widthId = Shader.PropertyToID("Width");
    private static readonly int _heightId = Shader.PropertyToID("Height");
    private static readonly int _spawnRadiusId = Shader.PropertyToID("Spawn Radius");
    private static readonly int _spawnRateId = Shader.PropertyToID("Spawn Rate");
    private static readonly int _flowSpeedId = Shader.PropertyToID("Flow Speed");
    private static readonly int _densityId = Shader.PropertyToID("Density");
    private static readonly int _boundsCenterId = Shader.PropertyToID("Bounds_center");
    private static readonly int _boundsSizeId = Shader.PropertyToID("Bounds_size");

    private const int _maxPathPoints = 8;              // VFXに渡す道の点の数の上限(テクスチャの幅)
    private const float _navMeshSampleRadius = 3f;     // 出現点・入口をNavMeshの上に合わせる時に探す半径(m)
    private const float _boundsMargin = 3f;            // 描画範囲を道の外側に広げる量(m)
    private const float _baseAliveCount = 4500f;       // Density 1の時の、生きている粒の数の目安(VFXのCapacity 6000に収まるように)
    private const float _extraLength = 5f;             // 粒が入口の先まで流れる分(m)。VFXのFlow Spawnの寿命と同じ値
    private const float _groundSearchHeight = 8f;      // 道の点を地面に合わせる時に探す距離(m)
    private const float _minHalfWidth = 0.5f;          // 道の左右の幅の下限(m)
    private const float _wallRayHeight = 1f;           // 左右の建物を探す高さ(地面から。m)
    private const float _minSpawnRadius = 1.5f;        // 瘴気が生まれる円の半径の下限(m)

    [SerializeField]
    [Tooltip("瘴気のVisualEffect(子のVillainAreaSmoke)。このTransformが路地裏の入口の位置と向き")]
    private VisualEffect _smoke;

    [SerializeField, Min(0f)]
    [Tooltip("入口の幅(m)。瘴気が横に広がる最大の幅")]
    private float _width = 6f;

    [SerializeField, Min(0f)]
    [Tooltip("入口で瘴気が立ち上る高さ(m)")]
    private float _height = 4.5f;

    // 書き込みはサーバーのみ(NetworkVariableのデフォルト)
    private readonly NetworkVariable<bool> _isActive = new NetworkVariable<bool>();

    private readonly List<Vector3> _pathPoints = new List<Vector3>();   // 道(ワールド座標。地面の高さに合わせた点)
    private readonly List<Vector2> _sideWidths = new List<Vector2>();   // 道の点ごとの、左右の壁までの距離(m)
    private Texture2D _pathTexture;

    // 子のVillainAreaSmokeが表示されていて、瘴気を使うか
    private bool isSmokeEnabled => _smoke != null && _smoke.gameObject.activeInHierarchy;

    private void Start()
    {
        if (!isSmokeEnabled) return;

        BuildPath();
        ApplyToSmoke();
        Apply();
    }

    public override void OnNetworkSpawn()
    {
        _isActive.OnValueChanged += HandleActiveChanged;

        // 途中参加したクライアントも、今の状態で表示する
        Apply();
    }

    public override void OnNetworkDespawn()
    {
        _isActive.OnValueChanged -= HandleActiveChanged;
    }

    public override void OnDestroy()
    {
        if (_pathTexture != null) Destroy(_pathTexture);
        base.OnDestroy();
    }

    // 瘴気を出す・止める(サーバー、またはオフライン)
    public void SetActive(bool isActive)
    {
        if (IsSpawned && !IsServer) return;

        _isActive.Value = isActive;

        // オフラインではOnValueChangedが呼ばれないので、ここで直接反映する
        if (!IsSpawned) Apply();
    }

    private void HandleActiveChanged(bool previous, bool current) => Apply();

    // 今の状態を瘴気に反映する(全クライアント)
    private void Apply()
    {
        if (!isSmokeEnabled) return;

        if (_isActive.Value) _smoke.Play();
        else _smoke.Stop();
    }

    // 出現点から入口までの道を求める(見つからなければ真っすぐ)
    private void BuildPath()
    {
        _pathPoints.Clear();
        Vector3 start = SnapToNavMesh(transform.position);
        Vector3 end = SnapToNavMesh(_smoke.transform.position);

        NavMeshPath path = new NavMeshPath();
        bool hasPath = NavMesh.CalculatePath(start, end, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
        if (hasPath) _pathPoints.AddRange(path.corners);

        if (_pathPoints.Count < 2)
        {
            _pathPoints.Clear();
            _pathPoints.Add(start);
            _pathPoints.Add(end);
        }

        ReducePoints(_pathPoints, _maxPathPoints);
        AddMidPoints(_pathPoints, _maxPathPoints);

        // 点ごとに真下の地面(NavMesh)の高さに合わせる(直線のままだと坂や段で浮くため)
        for (int i = 0; i < _pathPoints.Count; i++) _pathPoints[i] = SnapToGround(_pathPoints[i]);

        MeasureWidths();
    }

    // 点が多すぎる時は、始点と終点を残して間を間引く
    private static void ReducePoints(List<Vector3> points, int maxCount)
    {
        if (points.Count <= maxCount) return;

        List<Vector3> reduced = new List<Vector3>(maxCount);
        for (int i = 0; i < maxCount; i++)
        {
            int index = Mathf.RoundToInt((float)i / (maxCount - 1) * (points.Count - 1));
            reduced.Add(points[index]);
        }
        points.Clear();
        points.AddRange(reduced);
    }

    // 点が足りない時は、一番長い区間の真ん中に点を足していく(幅と地面の高さを測る場所を増やすため)
    private static void AddMidPoints(List<Vector3> points, int maxCount)
    {
        while (points.Count < maxCount)
        {
            int longest = 0;
            float longestLength = -1f;
            for (int i = 0; i < points.Count - 1; i++)
            {
                float segmentLength = Vector3.Distance(points[i], points[i + 1]);
                if (segmentLength <= longestLength) continue;
                longest = i;
                longestLength = segmentLength;
            }
            points.Insert(longest + 1, (points[longest] + points[longest + 1]) * 0.5f);
        }
    }

    // 点ごとに、道の左右の壁までの距離を測る。入口の幅の半分より広くはしない
    private void MeasureWidths()
    {
        _sideWidths.Clear();
        float maxHalfWidth = Mathf.Max(_width * 0.5f, _minHalfWidth);
        for (int i = 0; i < _pathPoints.Count; i++)
        {
            Vector3 prev = _pathPoints[Mathf.Max(i - 1, 0)];
            Vector3 next = _pathPoints[Mathf.Min(i + 1, _pathPoints.Count - 1)];
            Vector3 tangent = next - prev;
            tangent.y = 0f;
            tangent = tangent.sqrMagnitude > 0f ? tangent.normalized : Vector3.forward;
            Vector3 side = new Vector3(-tangent.z, 0f, tangent.x);   // VFXのFlow Along Pathと同じ向き(+が右)

            float left = MeasureToEdge(_pathPoints[i], -side, maxHalfWidth);
            float right = MeasureToEdge(_pathPoints[i], side, maxHalfWidth);
            _sideWidths.Add(new Vector2(left, right));
        }
    }

    // NavMeshの端と、建物などのコライダーの近い方までの距離(建物の下までNavMeshがつながっている所があるため、両方で測る)
    private static float MeasureToEdge(Vector3 origin, Vector3 direction, float maxDistance)
    {
        Vector3 target = origin + direction * maxDistance;
        float distance = NavMesh.Raycast(origin, target, out NavMeshHit hit, NavMesh.AllAreas) ? hit.distance : maxDistance;

        Vector3 rayOrigin = origin + Vector3.up * _wallRayHeight;
        if (Physics.Raycast(rayOrigin, direction, out RaycastHit wallHit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            distance = wallHit.distance;
        }
        return Mathf.Max(distance, _minHalfWidth);
    }

    // 道・幅・高さ・量・描画範囲をVFXに渡す
    private void ApplyToSmoke()
    {
        Transform smokeTransform = _smoke.transform;

        // 1行目: RGB = 入口から見た位置, A = 始点からの道のり / 2行目: R = 左の壁まで, G = 右の壁まで(m)
        _pathTexture = new Texture2D(_maxPathPoints, 2, TextureFormat.RGBAFloat, false, true)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        Color[] pixels = new Color[_maxPathPoints * 2];
        float length = 0f;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        for (int i = 0; i < _maxPathPoints; i++)
        {
            int index = Mathf.Min(i, _pathPoints.Count - 1);
            Vector3 local = smokeTransform.InverseTransformPoint(_pathPoints[index]);
            if (i > 0 && i < _pathPoints.Count) length += Vector3.Distance(_pathPoints[i - 1], _pathPoints[i]);
            pixels[i] = new Color(local.x, local.y, local.z, length);
            pixels[_maxPathPoints + i] = new Color(_sideWidths[index].x, _sideWidths[index].y, 0f, 0f);
            bounds.Encapsulate(local);
        }
        _pathTexture.SetPixels(pixels);
        _pathTexture.Apply(false, true);

        _smoke.SetTexture(_pathTextureId, _pathTexture);
        _smoke.SetFloat(_pathCountId, _pathPoints.Count);
        _smoke.SetFloat(_pathLengthId, length);
        _smoke.SetFloat(_widthId, _width);
        _smoke.SetFloat(_heightId, _height);

        // 粒は入口の先まで流れ切るまで生きるので、粒の数 ≒ Spawn Rate × ((道のり + _extraLength) ÷ 速さ)
        // 生きている粒がおよそ _baseAliveCount × Density になるように量を決める
        float speed = _smoke.HasFloat(_flowSpeedId) ? Mathf.Max(_smoke.GetFloat(_flowSpeedId), 0.05f) : 1f;
        float density = _smoke.HasFloat(_densityId) ? Mathf.Max(_smoke.GetFloat(_densityId), 0f) : 1f;
        float lifeTime = (length + _extraLength) / speed;
        _smoke.SetFloat(_spawnRateId, _baseAliveCount * density / Mathf.Max(lifeTime, 0.1f));

        // 出現点の円(グループの並ぶ半径)が狭い時は、少なくとも_minSpawnRadius
        if (TryGetComponent(out CS_VillainSpawnPoint spawnPoint))
        {
            _smoke.SetFloat(_spawnRadiusId, Mathf.Max(spawnPoint.groupRadius, _minSpawnRadius));
        }

        // 描画範囲 = 道 + 入口 + 余白
        bounds.Encapsulate(new Vector3(-_width * 0.5f, _height, 0f));
        bounds.Encapsulate(new Vector3(_width * 0.5f, 0f, 0f));
        bounds.Expand(_boundsMargin * 2f);
        _smoke.SetVector3(_boundsCenterId, bounds.center);
        _smoke.SetVector3(_boundsSizeId, bounds.size);
    }

    private static Vector3 SnapToNavMesh(Vector3 position)
    {
        return NavMesh.SamplePosition(position, out NavMeshHit hit, _navMeshSampleRadius, NavMesh.AllAreas) ? hit.position : position;
    }

    // 真下(上下 _groundSearchHeight の範囲)のNavMeshの高さに合わせる。見つからなければそのまま
    private static Vector3 SnapToGround(Vector3 position)
    {
        if (!NavMesh.SamplePosition(position, out NavMeshHit hit, _groundSearchHeight, NavMesh.AllAreas)) return position;
        return new Vector3(position.x, hit.position.y, position.z);
    }

    // 選択中、入口の幅と、道(プレイ中のみ。道はゲーム開始時に求めるため)を表示する
    private void OnDrawGizmosSelected()
    {
        if (_smoke == null) return;

        Gizmos.color = new Color(0.7f, 0.3f, 1f);
        Transform smokeTransform = _smoke.transform;
        Vector3 halfWidth = smokeTransform.right * _width * 0.5f;
        Gizmos.DrawLine(smokeTransform.position - halfWidth, smokeTransform.position + halfWidth);
        Gizmos.DrawLine(transform.position, smokeTransform.position);

        for (int i = 1; i < _pathPoints.Count; i++) Gizmos.DrawLine(_pathPoints[i - 1], _pathPoints[i]);
    }
}
