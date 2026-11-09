using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * 付けたParticleSystemが再生された時、Shapeの範囲に少しでも入っているオブジェクトに
 * 子のParticleSystemを1つずつ付けて再生するクラス(爆発に巻き込まれた物から煙・火花を出す など)
 *
 * 制作者：　吉本竜
 */

// ========================================
/*
 * メモ
 * ・ParticleSystemと同じGameObjectに付ける
 * ・直下の子のParticleSystemを、そのままオブジェクトプールとして使う(設定は不要。Hierarchyの上から順に使う)
 *   起動時に全部非アクティブにしておく
 *   範囲に入ったオブジェクト1つに、空いている子を上から1つ割り当てる
 *     例: 範囲にA,B,C → Aにbom_hit_01、Bにbom_hit_02、Cにbom_hit_03
 *   割り当てる順番は、Shapeの中心に近いオブジェクトから
 *   空いている子が足りない時は、余ったオブジェクトには出さない
 *   再生が終わったら(粒が全部消えたら)、非アクティブにしてこのオブジェクトの子(元の位置・向き・大きさ)に戻す
 *   ループする子は終わらないので戻らない(Looping はオフにしておく)
 * ・Attach To Target
 *   オン : 対象の子にして一緒に動かす(対象が動いてもエフェクトが付いていく)
 *   オフ : 対象の位置に置くだけ(このオブジェクトの子のまま)
 *   どちらも、終わったらこのオブジェクトの子に戻る
 *   付けている途中で対象が消されると、子も一緒に消えるのでプールから外れる
 * ・再生の検出
 *   止まっている状態から再生された時と、ループしないParticleSystemが最初から再生し直された時に1回ずつ動く
 *   ループするParticleSystemは、ループの繰り返しでは動かない(止めてから再生し直した時だけ動く)
 *   手動で動かしたい時は Trigger() を呼ぶ
 * ・対象にするオブジェクト
 *   CS_ParticleShapeHitTarget が付いているオブジェクトだけが対象(地面などは付けなければ反応しない)
 *   範囲の判定にはそのオブジェクトか子のColliderを使う(子のColliderが入っても1つとして扱う)
 *   Require Target Component をオフにすると、Colliderがある物を全部対象にする(前の動き)
 * ・範囲の判定
 *   ShapeのPosition / Rotation / Scale と、このオブジェクトのTransformを考慮する
 *   Sphere / Hemisphere / Cone / Box / Circle / Donut / Rectangle に対応
 *   ConeはLengthまでの円錐、Circle / Rectangleは厚み(Flat Shape Thickness)を持たせた板として判定する
 *   それ以外のShape(Meshなど)は、中心から Fallback Radius の球で判定する
 *   Arc(扇形)は考慮しない(円全体として判定する)
 *   判定にはColliderを使うので、Colliderが付いていないオブジェクトは対象にならない
 *   自分自身(このオブジェクトとその子)のColliderは対象外
 * ・出す位置
 *   対象の Spawn Point が指定されていればそこ、無ければ範囲に入ったColliderの中心(Use Bounds Center オフならPivot)
 *   Attach To Target がオンなら、Spawn Point(無ければ対象)の子にする
 * ・Play Delay(再生開始のOffset)
 *   親が再生されてから子を再生するまでの秒数。Play Delay Random で毎回ランダムにずらせる
 *   範囲の判定と子の割り当ては親が再生された瞬間に行う
 * ・時間はTime.timeScaleの影響を受ける
 * ・ゲームを再生している時だけ動く(Scene ビューの Particles パネルのプレビューでは動かない)
 */
// ========================================

[RequireComponent(typeof(ParticleSystem))]
public class CS_ParticleShapeHitSpawner : MonoBehaviour
{
    private const int _coneProbeCount = 6;   // Coneの軸上で判定に使う点の数

    [Header("再生のタイミング")]
    [SerializeField][Min(0f)] private float _playDelay = 0f;        // 親の再生から子を再生するまでの秒数(Offset)
    [SerializeField][Min(0f)] private float _playDelayRandom = 0f;  // Play Delayに足すランダムな秒数(0〜この値)

    [Header("出す位置")]
    [SerializeField] private bool _useBoundsCenter = true;          // true: Colliderの中心 / false: オブジェクトのPivot
    [SerializeField] private Vector3 _positionOffset = Vector3.zero; // 出す位置からのずれ(ワールド座標。m)
    [SerializeField] private bool _attachToTarget = true;           // trueなら対象の子にして一緒に動かす

    [Header("範囲の判定")]
    [SerializeField] private bool _requireTargetComponent = true;   // trueならCS_ParticleShapeHitTargetが付いた物だけ対象
    [SerializeField] private LayerMask _targetLayers = ~0;          // 対象にするレイヤー
    [SerializeField] private QueryTriggerInteraction _triggerInteraction = QueryTriggerInteraction.Ignore; // TriggerのColliderを含めるか
    [SerializeField] private string _targetTag = "";                // 空でなければ、このタグのオブジェクトだけ対象
    [SerializeField][Min(0.01f)] private float _flatShapeThickness = 1f; // Circle / Rectangleの厚み(Shapeのローカル単位)
    [SerializeField][Min(0.01f)] private float _fallbackRadius = 1f;    // 対応していないShapeの判定半径(Shapeのローカル単位)
    [SerializeField][Min(1)] private int _maxTargets = 32;          // 一度に調べる対象の最大数

    // プールの子1つ分
    private class PoolItem
    {
        public ParticleSystem Effect;
        public Transform Transform;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public Vector3 LocalScale;
        public bool InUse;
    }

    private ParticleSystem _particleSystem;
    private readonly List<PoolItem> _pool = new List<PoolItem>();
    private readonly HashSet<Transform> _hitRoots = new HashSet<Transform>();
    private readonly Dictionary<Transform, Collider> _targetColliders = new Dictionary<Transform, Collider>(); // 対象 → 範囲に入ったCollider
    private Collider[] _overlapBuffer = new Collider[64];
    private bool _wasPlaying;
    private bool _needsCleanup;
    private float _prevTime;

    private void Awake()
    {
        _particleSystem = GetComponent<ParticleSystem>();
        CollectPool();
    }

    private void OnEnable()
    {
        // 有効/無効の切り替え中は親子関係やアクティブを変えられないので、次のLateUpdateで子を戻す
        _needsCleanup = true;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        _wasPlaying = false;
        _needsCleanup = true;

        // 対象に付けたまま再生中の子は止めておく(親子関係を戻すのは次に有効になった時)
        foreach (PoolItem item in _pool)
        {
            if (item.Transform == null) continue;
            item.Effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            item.InUse = false;
        }
    }

    private void LateUpdate()
    {
        if (_needsCleanup)
        {
            _needsCleanup = false;
            foreach (PoolItem item in _pool)
            {
                if (!item.InUse) ReturnToPool(item);
            }
        }

        bool isPlaying = _particleSystem.isPlaying;
        float time = _particleSystem.time;

        bool started = isPlaying && !_wasPlaying;
        bool restarted = isPlaying && _wasPlaying && !_particleSystem.main.loop && time < _prevTime;
        if (started || restarted) Trigger();

        _wasPlaying = isPlaying;
        _prevTime = time;
    }

    // 今のShapeの範囲を調べて、入っているオブジェクトに空いている子を上から1つずつ付けて再生する
    public void Trigger()
    {
        _pool.RemoveAll(item => item.Transform == null); // 対象と一緒に消えた子は外す
        if (_pool.Count == 0) return;

        List<Transform> targets = FindTargetsInShape();
        Vector3 center = GetShapeToWorld().MultiplyPoint3x4(Vector3.zero);
        targets.Sort((a, b) =>
            (GetSpawnPosition(a) - center).sqrMagnitude.CompareTo((GetSpawnPosition(b) - center).sqrMagnitude));

        foreach (Transform target in targets)
        {
            PoolItem item = GetFreeItem();
            if (item == null) break; // 子が足りない

            item.InUse = true;
            float delay = _playDelay + Random.Range(0f, _playDelayRandom);
            StartCoroutine(PlayOnTarget(item, target, delay));
        }
    }

    private void CollectPool()
    {
        _pool.Clear();
        foreach (Transform child in transform) // 非アクティブの子も含めてHierarchyの上から順
        {
            if (!child.TryGetComponent(out ParticleSystem effect)) continue;

            _pool.Add(new PoolItem
            {
                Effect = effect,
                Transform = child,
                LocalPosition = child.localPosition,
                LocalRotation = child.localRotation,
                LocalScale = child.localScale,
            });
            child.gameObject.SetActive(false); // 親と一緒に元の位置で再生されないように止めておく
        }
    }

    private PoolItem GetFreeItem()
    {
        foreach (PoolItem item in _pool)
        {
            if (!item.InUse && item.Transform != null) return item;
        }
        return null;
    }

    private IEnumerator PlayOnTarget(PoolItem item, Transform target, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        // 待っている間に対象が消えていたら出さずに戻す
        if (target == null || item.Transform == null)
        {
            ReturnToPool(item);
            yield break;
        }

        // 元の向き・大きさ(見た目)のまま対象の位置に置く
        Transform effectTransform = item.Transform;
        effectTransform.SetParent(transform, false);
        effectTransform.localPosition = item.LocalPosition;
        effectTransform.localRotation = item.LocalRotation;
        effectTransform.localScale = item.LocalScale;
        effectTransform.position = GetSpawnPosition(target);
        if (_attachToTarget) effectTransform.SetParent(GetAttachParent(target), true);

        effectTransform.gameObject.SetActive(true);
        item.Effect.Clear(true);
        item.Effect.Play(true);

        // 再生した最初のフレームは粒がまだ無いことがあるので、1フレーム待ってから終わりを待つ
        yield return null;
        while (item.Transform != null && item.Effect.IsAlive(true)) yield return null;

        ReturnToPool(item);
    }

    // 非アクティブにして、このオブジェクトの子の元の位置に戻す
    private void ReturnToPool(PoolItem item)
    {
        item.InUse = false;
        if (item.Transform == null) return; // 対象と一緒に消えた

        item.Effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        item.Transform.gameObject.SetActive(false);
        item.Transform.SetParent(transform, false);
        item.Transform.localPosition = item.LocalPosition;
        item.Transform.localRotation = item.LocalRotation;
        item.Transform.localScale = item.LocalScale;
    }

    private Vector3 GetSpawnPosition(Transform target)
    {
        Vector3 basePosition = target.position;
        if (target.TryGetComponent(out CS_ParticleShapeHitTarget marker) && marker.SpawnPoint != null)
        {
            basePosition = marker.SpawnPoint.position;
        }
        else if (_useBoundsCenter)
        {
            // 範囲に入ったCollider(無ければ対象自身のCollider)の中心
            if (!_targetColliders.TryGetValue(target, out Collider targetCollider) || targetCollider == null)
            {
                target.TryGetComponent(out targetCollider);
            }
            if (targetCollider != null) basePosition = targetCollider.bounds.center;
        }
        return basePosition + _positionOffset;
    }

    private static Transform GetAttachParent(Transform target)
    {
        if (target.TryGetComponent(out CS_ParticleShapeHitTarget marker) && marker.SpawnPoint != null) return marker.SpawnPoint;
        return target;
    }

    // ========================================
    // 範囲の判定
    // ========================================

    private List<Transform> FindTargetsInShape()
    {
        List<Transform> targets = new List<Transform>();
        _hitRoots.Clear();
        _targetColliders.Clear();

        Matrix4x4 shapeToWorld = GetShapeToWorld();
        Matrix4x4 worldToShape = shapeToWorld.inverse;
        ParticleSystem.ShapeModule shape = _particleSystem.shape;

        // まず大きな球で候補を集めてから、Shapeの形で細かく調べる
        Vector3 center = shapeToWorld.MultiplyPoint3x4(Vector3.zero);
        float worldRadius = GetLocalBoundingRadius(shape) * GetMaxScale(shapeToWorld);
        int count = OverlapSphere(center, worldRadius);

        Vector3[] probes = GetLocalProbes(shape);
        for (int i = 0; i < count && targets.Count < _maxTargets; i++)
        {
            Collider hit = _overlapBuffer[i];
            if (hit == null || hit.transform.IsChildOf(transform)) continue;

            Transform root;
            CS_ParticleShapeHitTarget marker = hit.GetComponentInParent<CS_ParticleShapeHitTarget>();
            if (marker != null)
            {
                if (!marker.ReceiveEffect) continue;
                root = marker.transform;
            }
            else
            {
                if (_requireTargetComponent) continue;
                root = hit.attachedRigidbody != null ? hit.attachedRigidbody.transform : hit.transform;
            }
            if (_hitRoots.Contains(root)) continue;
            if (!string.IsNullOrEmpty(_targetTag) && !root.CompareTag(_targetTag) && !hit.CompareTag(_targetTag)) continue;
            if (!IsColliderInShape(hit, shape, shapeToWorld, worldToShape, probes)) continue;

            _hitRoots.Add(root);
            _targetColliders[root] = hit;
            targets.Add(root);
        }
        return targets;
    }

    private int OverlapSphere(Vector3 center, float radius)
    {
        int count = Physics.OverlapSphereNonAlloc(center, radius, _overlapBuffer, _targetLayers, _triggerInteraction);
        while (count == _overlapBuffer.Length)
        {
            _overlapBuffer = new Collider[_overlapBuffer.Length * 2];
            count = Physics.OverlapSphereNonAlloc(center, radius, _overlapBuffer, _targetLayers, _triggerInteraction);
        }
        return count;
    }

    // Colliderの中でShape側の点に一番近い点が、Shapeの中に入っていれば「少しでも入っている」
    private bool IsColliderInShape(Collider hit, ParticleSystem.ShapeModule shape,
        Matrix4x4 shapeToWorld, Matrix4x4 worldToShape, Vector3[] probes)
    {
        foreach (Vector3 probe in probes)
        {
            Vector3 worldProbe = shapeToWorld.MultiplyPoint3x4(probe);
            Vector3 closest = GetClosestPoint(hit, worldProbe);
            if (IsInsideLocal(shape, worldToShape.MultiplyPoint3x4(closest))) return true;
        }
        return false;
    }

    private static Vector3 GetClosestPoint(Collider hit, Vector3 point)
    {
        // 凸でないMeshColliderはClosestPointが使えないので、Boundsで代わりにする
        if (hit is MeshCollider meshCollider && !meshCollider.convex) return hit.bounds.ClosestPoint(point);
        return hit.ClosestPoint(point);
    }

    private Matrix4x4 GetShapeToWorld()
    {
        ParticleSystem.ShapeModule shape = _particleSystem.shape;
        Matrix4x4 shapeLocal = Matrix4x4.TRS(shape.position, Quaternion.Euler(shape.rotation), shape.scale);
        return transform.localToWorldMatrix * shapeLocal;
    }

    private static float GetMaxScale(Matrix4x4 matrix)
    {
        float x = matrix.GetColumn(0).magnitude;
        float y = matrix.GetColumn(1).magnitude;
        float z = matrix.GetColumn(2).magnitude;
        return Mathf.Max(x, Mathf.Max(y, z));
    }

    private float GetLocalBoundingRadius(ParticleSystem.ShapeModule shape)
    {
        float halfThickness = _flatShapeThickness * 0.5f;
        switch (shape.shapeType)
        {
            case ParticleSystemShapeType.Sphere:
            case ParticleSystemShapeType.Hemisphere:
                return shape.radius;
            case ParticleSystemShapeType.Cone:
            case ParticleSystemShapeType.ConeVolume:
                float topRadius = GetConeRadiusAt(shape, shape.length);
                return Mathf.Sqrt(shape.length * shape.length + topRadius * topRadius);
            case ParticleSystemShapeType.Box:
            case ParticleSystemShapeType.BoxShell:
            case ParticleSystemShapeType.BoxEdge:
                return Mathf.Sqrt(0.75f);
            case ParticleSystemShapeType.Circle:
                return Mathf.Sqrt(shape.radius * shape.radius + halfThickness * halfThickness);
            case ParticleSystemShapeType.Donut:
                return shape.radius + shape.donutRadius;
            case ParticleSystemShapeType.Rectangle:
                return Mathf.Sqrt(0.5f + halfThickness * halfThickness);
            default:
                return _fallbackRadius;
        }
    }

    // Shapeのローカル座標で、この点がShapeの中に入っているか
    private bool IsInsideLocal(ParticleSystem.ShapeModule shape, Vector3 p)
    {
        const float epsilon = 0.0001f;
        float halfThickness = _flatShapeThickness * 0.5f;
        switch (shape.shapeType)
        {
            case ParticleSystemShapeType.Sphere:
                return p.magnitude <= shape.radius + epsilon;
            case ParticleSystemShapeType.Hemisphere:
                return p.z >= -epsilon && p.magnitude <= shape.radius + epsilon;
            case ParticleSystemShapeType.Cone:
            case ParticleSystemShapeType.ConeVolume:
                if (p.z < -epsilon || p.z > shape.length + epsilon) return false;
                return new Vector2(p.x, p.y).magnitude <= GetConeRadiusAt(shape, Mathf.Max(p.z, 0f)) + epsilon;
            case ParticleSystemShapeType.Box:
            case ParticleSystemShapeType.BoxShell:
            case ParticleSystemShapeType.BoxEdge:
                return Mathf.Abs(p.x) <= 0.5f + epsilon && Mathf.Abs(p.y) <= 0.5f + epsilon && Mathf.Abs(p.z) <= 0.5f + epsilon;
            case ParticleSystemShapeType.Circle:
                return Mathf.Abs(p.z) <= halfThickness + epsilon && new Vector2(p.x, p.y).magnitude <= shape.radius + epsilon;
            case ParticleSystemShapeType.Donut:
                float ring = new Vector2(p.x, p.y).magnitude - shape.radius;
                return new Vector2(ring, p.z).magnitude <= shape.donutRadius + epsilon;
            case ParticleSystemShapeType.Rectangle:
                return Mathf.Abs(p.x) <= 0.5f + epsilon && Mathf.Abs(p.y) <= 0.5f + epsilon && Mathf.Abs(p.z) <= halfThickness + epsilon;
            default:
                return p.magnitude <= _fallbackRadius + epsilon;
        }
    }

    private static float GetConeRadiusAt(ParticleSystem.ShapeModule shape, float height)
    {
        return shape.radius + height * Mathf.Tan(shape.angle * Mathf.Deg2Rad);
    }

    // Colliderの一番近い点を探す時の基準点(Shapeのローカル座標)
    private Vector3[] GetLocalProbes(ParticleSystem.ShapeModule shape)
    {
        switch (shape.shapeType)
        {
            case ParticleSystemShapeType.Hemisphere:
                return new[] { Vector3.zero, new Vector3(0f, 0f, shape.radius * 0.5f) };
            case ParticleSystemShapeType.Cone:
            case ParticleSystemShapeType.ConeVolume:
                Vector3[] probes = new Vector3[_coneProbeCount];
                for (int i = 0; i < _coneProbeCount; i++)
                {
                    probes[i] = new Vector3(0f, 0f, shape.length * i / (_coneProbeCount - 1));
                }
                return probes;
            default:
                return new[] { Vector3.zero };
        }
    }
}
