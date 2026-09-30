/* ================================================
 * HDRP Toon Shader - Main Light Direction Sender
 * ================================================
 * 制作者：吉本竜
 * ------------------------------------------------
 * 2026-09-30 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// Directional Light の向きを取得し、
/// Toon Shader で使用する MainLightDirection を
/// MaterialPropertyBlock 経由で対象 Renderer に渡すクラス。
///
/// Toon Shader 側では、
/// 「表面法線」と「光源方向」の内積を計算することで
/// 明部・暗部の判定に使用する。
/// </summary>
public class CS_ToonLightDirection : MonoBehaviour
{
    /// <summary>
    /// Toon Shader の基準として使用する Directional Light。
    /// 通常はシーン内のメインライトを指定する。
    /// </summary>
    [SerializeField]
    private Light mainLight;

    /// <summary>
    /// MainLightDirection を渡す対象 Renderer。
    /// キャラクターが複数 Renderer で構成されている場合は、
    /// Body、Hair、Face などをすべて登録する。
    /// </summary>
    [SerializeField]
    private Renderer[] targetRenderers;

    /// <summary>
    /// Shader Graph 側の MainLightDirection の
    /// Reference 名を ID 化したもの。
    ///
    /// Shader.PropertyToID を使用することで、
    /// Update 内で毎フレーム文字列検索を行うことを避ける。
    /// </summary>
    private static readonly int MainLightDirectionID =
        Shader.PropertyToID("_MainLightDirection");

    /// <summary>
    /// Material を複製せずに、
    /// Renderer ごとの Shader Parameter を変更するために使用する。
    /// </summary>
    private MaterialPropertyBlock propertyBlock;

    /// <summary>
    /// 初期化処理。
    /// MaterialPropertyBlock を一度だけ生成する。
    /// </summary>
    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
    }

    /// <summary>
    /// 毎フレーム Directional Light の向きを取得し、
    /// 対象 Renderer の Shader に渡す。
    /// </summary>
    private void Update()
    {
        // Main Light が設定されていない場合は処理しない。
        if (mainLight == null)
        {
            return;
        }

        /*
         * Directional Light の transform.forward は、
         * 「光が進んでいく方向」を表す。
         *
         * 一方、Shader の N・L 計算では
         *
         *   N = Surface Normal
         *   L = Surface から Light 側へ向かう方向
         *
         * が必要になる。
         *
         * そのため、Directional Light の forward を反転して
         * Shader 用の Light Direction として使用する。
         */
        Vector3 lightDirection = -mainLight.transform.forward;

        // 登録されている全 Renderer に Light Direction を渡す。
        foreach (Renderer targetRenderer in targetRenderers)
        {
            // 未設定の Renderer は無視する。
            if (targetRenderer == null)
            {
                continue;
            }

            /*
             * Renderer が現在持っている
             * MaterialPropertyBlock の内容を取得する。
             *
             * 他の処理が設定している値を消さないため、
             * SetPropertyBlock の前に取得しておく。
             */
            targetRenderer.GetPropertyBlock(propertyBlock);

            /*
             * Shader Graph の _MainLightDirection に
             * World Space の Light Direction を設定する。
             */
            propertyBlock.SetVector(
                MainLightDirectionID,
                lightDirection
            );

            /*
             * 更新した MaterialPropertyBlock を
             * Renderer に反映する。
             */
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}