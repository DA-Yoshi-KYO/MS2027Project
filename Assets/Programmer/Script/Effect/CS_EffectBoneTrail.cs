using UnityEngine;
using UnityEngine.Rendering;

/*
 * キャラクターのボーン(こぶしなど)に付いていき、TrailRendererで軌跡を残す仮エフェクト
 * 通常攻撃の殴り(FX_PlayerPunchTrailR / L)で使っている
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・本番の素材ができるまでの仮
 * ・付いていく先(CS_EffectManagerのfollow。通常攻撃ならプレイヤー)の一番上の親から、Bone Nameのボーンを探して付いていく
 *   ボーンは再生するたびに探し直す(プールで使い回すので、前回と違うキャラクターに付くことがある)
 *   見つからない時(名前が空・モデル差し替えで名前が変わった時)は、一番上の親の Fallback Offset の位置に出す
 *   (Fallback Offsetは一番上の親の向き基準。X: 右 / Y: 上 / Z: 前)
 * ・Emit Time 秒だけ軌跡を出し、その後は残った軌跡が Trail Time 秒で消える
 * ・TrailRendererは起動時に子に作る(Prefabに付けておく必要はない)
 *   色はTrailRendererの頂点カラーで付ける(Novaのパーティクル用マテリアルなど、頂点カラーを使うマテリアルを設定する)
 *   軌跡は新しい方が濃く、古い方へ向かって透明になる
 * ・CS_EffectManagerから IEffectPlayable として再生・停止される
 */
// ========================================

public class CS_EffectBoneTrail : MonoBehaviour, IEffectPlayable
{
    [Header("付いていくボーン")]
    [SerializeField] private string _boneName = "mixamorig:RightHand";               // 付いていくボーンの名前
    [SerializeField] private Vector3 _fallbackOffset = new Vector3(0.2f, 1.2f, 0.5f); // ボーンが無い時に出す位置(一番上の親の向き基準。m)

    [Header("見た目")]
    [SerializeField] private Material _material;                              // 軌跡のマテリアル(頂点カラーを使うもの)
    [SerializeField] private Color _color = new Color(0.8f, 0.95f, 1f, 1f);  // 軌跡の色
    [SerializeField][Min(0.01f)] private float _width = 0.25f;               // 軌跡の太さ(m)
    [SerializeField][Min(0.01f)] private float _trailTime = 0.1f;            // 軌跡が残る時間(秒)
    [SerializeField][Min(0.01f)] private float _emitTime = 0.25f;            // 軌跡を出す時間(秒)

    private Transform _tip;
    private TrailRenderer _trail;
    private Transform _root;   // ボーンを探した一番上の親
    private Transform _bone;   // 付いていくボーン(見つからなければnull)
    private Color _currentColor;
    private float _elapsed;
    private bool _isEmitting;

    // 軌跡を出している間と、軌跡が消えきるまでは見えている
    public bool isAlive => _isEmitting || _elapsed < _emitTime + _trailTime;

    private void Awake()
    {
        CreateTrail();
        _currentColor = _color;
        _elapsed = _emitTime + _trailTime;   // Playされるまでは何も出さない
    }

    // アニメーションで動いた後のボーンに合わせる
    private void LateUpdate()
    {
        if (!isAlive) return;

        _elapsed += Time.deltaTime;
        if (!_isEmitting) return;

        _tip.position = GetTipPosition();
        if (_elapsed >= _emitTime) Stop();
    }

    public void Play()
    {
        FindBone();

        _elapsed = 0f;
        _isEmitting = true;
        _currentColor = _color;
        ApplyColor();

        // 刃先をこぶしの位置に置いてから、前回の軌跡を消して描き始める
        _tip.position = GetTipPosition();
        _trail.Clear();
        _trail.emitting = true;
    }

    public void Stop()
    {
        _isEmitting = false;
        _trail.emitting = false;
    }

    public void SetColor(Color color)
    {
        _currentColor = color;
        ApplyColor();
    }

    // 付いていく先の一番上の親から、ボーンを探す(前回と同じ親なら探し直さない)
    private void FindBone()
    {
        Transform root = transform.root;
        if (root == _root && _bone != null) return;

        _root = root;
        _bone = null;
        if (string.IsNullOrEmpty(_boneName)) return;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == _boneName)
            {
                _bone = child;
                return;
            }
        }
    }

    private Vector3 GetTipPosition()
    {
        if (_bone != null) return _bone.position;

        return _root.position + _root.rotation * _fallbackOffset;
    }

    private void CreateTrail()
    {
        GameObject tipObject = new GameObject("TrailTip");
        tipObject.transform.SetParent(transform, false);
        _tip = tipObject.transform;

        _trail = tipObject.AddComponent<TrailRenderer>();
        _trail.material = _material;
        _trail.time = _trailTime;
        _trail.widthMultiplier = _width;
        _trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.1f));
        _trail.minVertexDistance = 0.02f;
        _trail.numCapVertices = 2;
        _trail.shadowCastingMode = ShadowCastingMode.Off;
        _trail.receiveShadows = false;
        _trail.emitting = false;
    }

    // 新しい方が濃く、古い方へ向かって透明にする
    private void ApplyColor()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(_currentColor, 0f), new GradientColorKey(_currentColor, 1f) },
            new[] { new GradientAlphaKey(_currentColor.a, 0f), new GradientAlphaKey(0f, 1f) });
        _trail.colorGradient = gradient;
    }
}
