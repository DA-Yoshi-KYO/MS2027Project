using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * 付けたParticleSystemが再生された時、Shapeの範囲に少しでも入っているオブジェクトの位置で
 * 子のParticleSystemを出して再生するクラス(爆発に巻き込まれた物から煙・火花を出す など)
 *
 * 制作者：　吉本竜
 */

// ========================================
/*
 * メモ
 * ・ParticleSystemと同じGameObjectに付ける
 * ・子のParticleSystem(Child Effects。空なら直下の子のParticleSystemを全部使う)はお手本として使う
 *   起動時に非アクティブにして、親と一緒に元の位置で再生されないようにする
 *   範囲に入ったオブジェクト1つごとに、お手本を複製してそのオブジェクトの位置でアクティブにして再生する
 *   複製は再生が終わったら(粒が全部消えたら)自動で消える(ループするお手本は消えないので注意)
 * ・再生の検出
 *   止まっている状態から再生された時と、ループしないParticleSystemが最初から再生し直された時に1回ずつ動く
 *   ループするParticleSystemは、ループの繰り返しでは動かない(止めてから再生し直した時だけ動く)
 *   手動で動かしたい時は Trigger() を呼ぶ
 * ・範囲の判定
 *   ShapeのPosition / Rotation / Scale と、このオブジェクトのTransformを考慮する
 *   Sphere / Hemisphere / Cone / Box / Circle / Donut / Rectangle に対応
 *   ConeはLengthまでの円錐、Circle / Rectangleは厚み(Flat Shape Thickness)を持たせた板として判定する
 *   それ以外のShape(Meshなど)は、中心から Fallback Radius の球で判定する
 *   Arc(扇形)は考慮しない(円全体として判定する)
 *   判定にはColliderを使うので、Colliderが付いていないオブジェクトは対象にならない
 *   自分自身(このオブジェクトとその子)のColliderは対象外
 * ・Play Delay(再生開始のOffset)
 *   親が再生されてから子を再生するまでの秒数。Play Delay Random で毎回ランダムにずらせる
 *   範囲の判定は親が再生された瞬間に行う
 * ・時間はTime.timeScaleの影響を受ける
 * ・ゲームを再生している時だけ動く(Scene ビューの Particles パネルのプレビューでは動かない)
 */
// ========================================

[RequireComponent(typeof(ParticleSystem))]
public class CS_ParticleShapeHitSpawner : MonoBehaviour
{
    private const int _coneProbeCount = 6;   // Coneの軸上で判定に使う点の数

    [Header("出すエフェクト")]
    [SerializeField] private List<ParticleSystem> _childEffects = new List<ParticleSystem>(); // お手本(空なら直下の子のParticleSystemを全部使う)

    [Header("再生のタイミング")]
    [SerializeField][Min(0f)] private float _playDelay = 0f;        // 親の再生から子を再生するまでの秒数(Offset)
    [SerializeField][Min(0f)] private float _playDelayRandom = 0f;  // Play Delayに足すランダムな秒数(0〜この値)

    [Header("出す位置")]
    [SerializeField] private bool _useBoundsCenter = true;          // true: Colliderの中心 / false: オブジェクトのPivot
    [SerializeField] private Vector3 _positionOffset = Vector3.zero; // 出す位置からのずれ(ワールド座標。m)
    [SerializeField] private bool _attachToTarget = false;          // trueなら対象の子にして一緒に動かす

    [Header("範囲の判定")]
    [SerializeField] private LayerMask _targetLayers = ~0;          // 対象にするレイヤー
    [SerializeField] private QueryTriggerInteraction _triggerInteraction = QueryTriggerInteraction.Ignore; // TriggerのColliderを含めるか
    [SerializeField] private string _targetTag = "";                // 空でなければ、このタグのオブジェクトだけ対象
    [SerializeField][Min(0.01f)] private float _flatShapeThickness = 1f; // Circle / Rectangleの厚み(Shapeのローカル単位)
    [SerializeField][Min(0.01f)] private float _fallbackRadius = 1f;    // 対応していないShapeの判定半径(Shapeのローカル単位)
    [SerializeField][Min(1)] private int _maxTargets = 32;          // 一度に出す対象の最大数

    private ParticleSystem _particleSystem;
    private readonly List<ParticleSystem> _templates = new List<ParticleSystem>();
    private readonly HashSet<Transform> _hitRoots = new HashSet<Transform>();
    private Collider[] _overlapBuffer = new Collider[64];
    private bool _wasPlaying;
    private float _prevTime;

    private void Awake()
    {
        _particleSystem = GetComponent<ParticleSystem>();
        CollectTemplates();

        // お手本は親と一緒に元の位置で再生されないように止めておく
        foreach (ParticleSystem template in _templates)
        {
            template.gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        _wasPlaying = false;
    }

    private void LateUpdate()
    {
        bool isPlaying = _particleSystem.isPlaying;
        float time = _particleSystem.time;

        bool started = isPlaying && !_wasPlaying;
        bool restarted = isPlaying && _wasPlaying && !_particleSystem.main.loop && time < _prevTime;
        if (started || restarted) Trigger();

        _wasPlaying = isPlaying;
        _prevTime = time;
    }

    // 今のShapeの範囲を調べて、入っているオブジェクトに子のエフェクトを出す
    public void Trigger()
    {
        if (_templates.Count == 0) return;

        List<Transform> targets = FindTargetsInShape();
        foreach (Transform target in targets)
        {
            float delay = _playDelay + Random.Range(0f, _playDelayRandom);
            StartCoroutine(SpawnAfterDelay(target, GetSpawnPosition(target), delay));
        }
    }

    private void CollectTemplates()
    {
        _templates.Clear();
        foreach (ParticleSystem effect in _childEffects)
        {
            if (effect != null && effect != _particleSystem) _templates.Add(effect);
        }
        if (_templates.Count > 0) return;

        foreach (Transform child in transform)
        {
            if (child.TryGetComponent(out ParticleSystem effect)) _templates.Add(effect);
        }
    }

    private IEnumerator SpawnAfterDelay(Transform target, Vector3 position, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        // 待っている間に動いていたら今の位置に出す(対象が消えていたら判定した時の位置に出す)
        if (target != null) position = GetSpawnPosition(target);

        foreach (ParticleSystem template in _templates)
        {
            if (template == null) continue;

            Transform parent = _attachToTarget && target != null ? target : null;
            GameObject clone = Instantiate(template.gameObject, position, template.transform.rotation, parent);
            clone.name = template.name + "_" + (target != null ? target.name : "Target");
            clone.transform.localScale = GetLocalScale(template.transform.lossyScale, parent);
            clone.AddComponent<DestroyWhenFinished>();
            clone.SetActive(true);

            ParticleSystem effect = clone.GetComponent<ParticleSystem>();
            effect.Clear(true);
            effect.Play(true);
        }
    }

    // お手本と同じ見た目の大きさになるように、親の大きさで割る
    private static Vector3 GetLocalScale(Vector3 worldScale, Transform parent)
    {
        if (parent == null) return worldScale;

        Vector3 parentScale = parent.lossyScale;
        return new Vector3(
            parentScale.x != 0f ? worldScale.x / parentScale.x : worldScale.x,
            parentScale.y != 0f ? worldScale.y / parentScale.y : worldScale.y,
            parentScale.z != 0f ? worldScale.z / parentScale.z : worldScale.z);
    }

    private Vector3 GetSpawnPosition(Transform target)
    {
        Vector3 basePosition = target.position;
        if (_useBoundsCenter && target.TryGetComponent(out Collider targetCollider))
        {
            basePosition = targetCollider.bounds.center;
        }
        return basePosition + _positionOffset;
    }

    // ========================================
    // 範囲の判定
    // ========================================

    private List<Transform> FindTargetsInShape()
    {
        List<Transform> targets = new List<Transform>();
        _hitRoots.Clear();

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

            Transform root = hit.attachedRigidbody != null ? hit.attachedRigidbody.transform : hit.transform;
            if (_hitRoots.Contains(root)) continue;
            if (!string.IsNullOrEmpty(_targetTag) && !root.CompareTag(_targetTag) && !hit.CompareTag(_targetTag)) continue;
            if (!IsColliderInShape(hit, shape, shapeToWorld, worldToShape, probes)) continue;

            _hitRoots.Add(root);
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

    // 複製したエフェクトに付けて、粒が全部消えたら自分を消す
    // (親が止まったり消えたりしても、出したエフェクトは最後まで再生させる)
    private class DestroyWhenFinished : MonoBehaviour
    {
        private ParticleSystem _effect;
        private bool _started;

        private void Awake()
        {
            _effect = GetComponent<ParticleSystem>();
        }

        private void LateUpdate()
        {
            if (_effect == null) return;

            // 再生した最初のフレームは粒がまだ無いことがあるので、1フレーム待つ
            if (!_started)
            {
                _started = true;
                return;
            }
            if (!_effect.IsAlive(true)) Destroy(gameObject);
        }
    }
}
