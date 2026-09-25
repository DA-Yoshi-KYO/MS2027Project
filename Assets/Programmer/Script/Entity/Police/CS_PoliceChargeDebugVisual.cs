/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-09-25 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// 【プロトタイプ用のデバッグ表示】警察の攻撃チャージ中に、見た目の色を変えるクラス
/// チャージの進み具合に応じて青→赤のグラデーションで色が変わり、チャージが終わると元の色に戻る
/// マテリアル自体は書き換えず(MaterialPropertyBlockを使う)、このコンポーネントを外せば元の見た目になる
/// ※チャージの状態はサーバー(またはオフライン)にしか無いため、色が変わるのはホスト・オフラインの画面だけ
/// </summary>
[RequireComponent(typeof(CS_PoliceAttack))]
public class CS_PoliceChargeDebugVisual : MonoBehaviour
{
    // マテリアルの基本色のプロパティ(HDRP/Lit・URP/Litで共通の名前)
    private static readonly int _baseColorId = Shader.PropertyToID("_BaseColor");

    private CS_PoliceAttack _attack = null;

    // 色を変える見た目(子オブジェクトも含む)
    private Renderer[] _renderers = null;

    // 色の上書きに使い回すプロパティブロック
    private MaterialPropertyBlock _propertyBlock = null;

    // チャージの色を表示中か(チャージが終わった時に1回だけ元の色へ戻すために使う)
    private bool _isShowingCharge = false;

    [Header("＝＝＝ チャージ表示(デバッグ用) ＝＝＝")]
    [SerializeField]
    [Tooltip("チャージの進み具合に応じた色(左:チャージ開始 → 右:溜め終わり)")]
    private Gradient _chargeGradient = CreateDefaultGradient();

    private void Awake()
    {
        _attack = GetComponent<CS_PoliceAttack>();
        _renderers = GetComponentsInChildren<Renderer>();
        _propertyBlock = new MaterialPropertyBlock();
    }

    private void Update()
    {
        if (_attack.isCharging)
        {
            ApplyColor(_chargeGradient.Evaluate(_attack.chargeProgress));
            _isShowingCharge = true;
            return;
        }

        if (!_isShowingCharge) return;

        // チャージが終わったら、色の上書きを消して元の見た目に戻す
        ClearColor();
        _isShowingCharge = false;
    }

    private void OnDisable()
    {
        // コンポーネントを無効にした時も元の見た目に戻す
        ClearColor();
        _isShowingCharge = false;
    }

    /// <summary>
    /// 見た目の色を上書きするメソッド
    /// </summary>
    /// <param name="color">上書きする色</param>
    private void ApplyColor(Color color)
    {
        foreach (Renderer targetRenderer in _renderers)
        {
            targetRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(_baseColorId, color);
            targetRenderer.SetPropertyBlock(_propertyBlock);
        }
    }

    /// <summary>
    /// 色の上書きを消して、マテリアル本来の色に戻すメソッド
    /// </summary>
    private void ClearColor()
    {
        if (_renderers == null) return;

        foreach (Renderer targetRenderer in _renderers)
        {
            if (targetRenderer != null) targetRenderer.SetPropertyBlock(null);
        }
    }

    /// <summary>
    /// チャージの色の初期値(青→赤)を作るメソッド
    /// </summary>
    /// <returns>チャージの色のグラデーション</returns>
    private static Gradient CreateDefaultGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(0.1f, 0.3f, 1.0f), 0.0f), new GradientColorKey(new Color(1.0f, 0.1f, 0.1f), 1.0f) },
            new[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 1.0f) });
        return gradient;
    }
}
