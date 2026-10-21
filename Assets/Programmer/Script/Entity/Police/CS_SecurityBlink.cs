/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-21 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// 防犯装置の見た目の色を、普段は青、見つけた時(接触した時)は赤と青の点滅にするクラス
/// 防犯レーザー・ストリートライト・巡回ドローンで共通して使う
/// 色はMaterialPropertyBlockで上書きする(マテリアル自体は書き換えない)
/// </summary>
public class CS_SecurityBlink : MonoBehaviour
{
    // HDRP/Unlitの色と、Litの基本色のプロパティ
    private static readonly int _unlitColorId = Shader.PropertyToID("_UnlitColor");
    private static readonly int _baseColorId = Shader.PropertyToID("_BaseColor");

    // 色を変える見た目(付いていなければ、子オブジェクトの最初のRenderer)
    private Renderer _renderer = null;
    private MaterialPropertyBlock _propertyBlock = null;

    // 点滅の残り時間と、点滅を始めてからの経過時間
    private float _flashRemaining = 0.0f;
    private float _flashElapsed = 0.0f;

    // 今の色が赤か(色を変える時だけ上書きするために使う)
    private bool _isShowingAlert = false;

    [Header("＝＝＝ 色 ＝＝＝")]
    [SerializeField]
    [Tooltip("普段の色")]
    private Color _normalColor = new Color(0.1f, 0.45f, 1.0f, 1.0f);

    [SerializeField]
    [Tooltip("点滅の時の、もう一方の色")]
    private Color _alertColor = new Color(1.0f, 0.05f, 0.05f, 1.0f);

    [SerializeField, Min(0f)]
    [Tooltip("光の明るさ(1より大きいとBloomで光って見える。Unlitの色にだけ掛ける)")]
    private float _intensity = 3.0f;

    [SerializeField, Min(0.02f)]
    [Tooltip("赤と青が切り替わる間隔(秒)")]
    private float _blinkInterval = 0.2f;

    // 点滅中か
    public bool isFlashing => _flashRemaining > 0.0f;

    private void Awake()
    {
        _renderer = GetComponentInChildren<Renderer>();
        _propertyBlock = new MaterialPropertyBlock();
        ApplyColor(false, true);
    }

    private void Update()
    {
        if (_flashRemaining <= 0.0f) return;

        // 点滅は、赤から始まり、一定間隔で青と入れ替わる。終わったら青に戻る
        _flashRemaining -= Time.deltaTime;
        _flashElapsed += Time.deltaTime;
        bool isAlert = _flashRemaining > 0.0f && Mathf.FloorToInt(_flashElapsed / _blinkInterval) % 2 == 0;
        ApplyColor(isAlert, false);
    }

    private void OnDisable()
    {
        _flashRemaining = 0.0f;
        ApplyColor(false, true);
    }

    /// <summary>
    /// 赤と青の点滅を始めるメソッド(点滅中に呼ぶと、最初からやり直す)
    /// </summary>
    /// <param name="duration">点滅する時間(秒)</param>
    public void StartFlash(float duration)
    {
        _flashRemaining = duration;
        _flashElapsed = 0.0f;
        ApplyColor(true, false);
    }

    /// <summary>
    /// 見た目の色を反映するメソッド
    /// </summary>
    /// <param name="isAlert">赤にするか(falseなら青)</param>
    /// <param name="isForced">今の色と同じでも、反映するか</param>
    private void ApplyColor(bool isAlert, bool isForced)
    {
        if (_renderer == null || (!isForced && isAlert == _isShowingAlert)) return;

        _isShowingAlert = isAlert;
        Color color = isAlert ? _alertColor : _normalColor;

        _renderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor(_unlitColorId, new Color(color.r * _intensity, color.g * _intensity, color.b * _intensity, color.a));
        _propertyBlock.SetColor(_baseColorId, color);
        _renderer.SetPropertyBlock(_propertyBlock);
    }
}
