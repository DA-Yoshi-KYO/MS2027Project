using UnityEngine;
using UnityEngine.Rendering;

/*
 * 斬撃の軌跡を描く仮エフェクト
 * 刃先(見えない点)を円弧に沿って動かし、TrailRendererで軌跡を残す
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・本番の素材ができるまでの仮。通常攻撃の振り(FX_PlayerAttackSlash1〜5)で使っている
 * ・このオブジェクトの位置を中心に、Height の高さで、Radius だけ離れた円弧を描く
 *   角度は正面が0度、右がプラス。Start Angle → End Angle へ Sweep Time 秒で振る
 *   Tilt で振る面を正面の軸まわりに傾ける(0: 水平な横斬り、90: 縦斬り、45: 斜め)
 * ・TrailRendererは起動時に子に作る(Prefabに付けておく必要はない)
 *   色はTrailRendererの頂点カラーで付ける(Novaのパーティクル用マテリアルなど、頂点カラーを使うマテリアルを設定する)
 *   軌跡は刃先側が濃く、古い方へ向かって透明になる
 * ・CS_EffectManagerから IEffectPlayable として再生・停止される
 */
// ========================================

public class CS_EffectSlash : MonoBehaviour, IEffectPlayable
{
    [Header("見た目")]
    [SerializeField] private Material _material;                              // 軌跡のマテリアル(頂点カラーを使うもの)
    [SerializeField] private Color _color = new Color(0.8f, 0.95f, 1f, 1f);  // 軌跡の色
    [SerializeField][Min(0.01f)] private float _width = 0.4f;                // 軌跡の太さ(m)
    [SerializeField][Min(0.01f)] private float _trailTime = 0.12f;           // 軌跡が残る時間(秒)

    [Header("振り方")]
    [SerializeField][Min(0.1f)] private float _radius = 1.2f;       // 中心から刃先までの距離(m)
    [SerializeField] private float _height = 1f;                     // 振る高さ(m)
    [SerializeField] private float _startAngle = -80f;               // 振り始めの角度(度。正面が0、右がプラス)
    [SerializeField] private float _endAngle = 80f;                  // 振り終わりの角度(度)
    [SerializeField] private float _tilt = 0f;                       // 振る面の傾き(度。0で横斬り、90で縦斬り)
    [SerializeField][Min(0.01f)] private float _sweepTime = 0.1f;   // 振り始めから振り終わりまでの時間(秒)

    private Transform _tip;
    private TrailRenderer _trail;
    private Color _currentColor;
    private float _elapsed;
    private bool _isSweeping;

    // 振っている間と、軌跡が消えきるまでは見えている
    public bool isAlive => _isSweeping || _elapsed < _sweepTime + _trailTime;

    private void Awake()
    {
        CreateTrail();
        _currentColor = _color;
        _elapsed = _sweepTime + _trailTime;   // Playされるまでは何も出さない
    }

    private void Update()
    {
        if (!isAlive) return;

        _elapsed += Time.deltaTime;
        if (!_isSweeping) return;

        float rate = Mathf.Clamp01(_elapsed / _sweepTime);
        float eased = 1f - (1f - rate) * (1f - rate);   // 振り始めが速く、最後にゆるむ
        _tip.localPosition = GetTipPosition(Mathf.Lerp(_startAngle, _endAngle, eased));

        if (rate >= 1f) Stop();
    }

    public void Play()
    {
        _elapsed = 0f;
        _isSweeping = true;
        _currentColor = _color;
        ApplyColor();

        // 刃先を振り始めの位置に置いてから、前回の軌跡を消して描き始める
        _tip.localPosition = GetTipPosition(_startAngle);
        _trail.Clear();
        _trail.emitting = true;
    }

    public void Stop()
    {
        _isSweeping = false;
        _trail.emitting = false;
    }

    public void SetColor(Color color)
    {
        _currentColor = color;
        ApplyColor();
    }

    // 角度(度)から、刃先の位置(このオブジェクトから見た位置)を求める
    private Vector3 GetTipPosition(float angle)
    {
        Vector3 onArc = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * _radius;
        return Vector3.up * _height + Quaternion.Euler(0f, 0f, _tilt) * onArc;
    }

    private void CreateTrail()
    {
        GameObject tipObject = new GameObject("SlashTip");
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

    // 刃先側(新しい方)が濃く、古い方へ向かって透明にする
    private void ApplyColor()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(_currentColor, 0f), new GradientColorKey(_currentColor, 1f) },
            new[] { new GradientAlphaKey(_currentColor.a, 0f), new GradientAlphaKey(0f, 1f) });
        _trail.colorGradient = gradient;
    }
}
