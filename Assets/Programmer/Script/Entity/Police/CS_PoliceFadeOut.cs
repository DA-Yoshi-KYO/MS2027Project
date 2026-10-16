/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-05 | 初回作成
 * ================================================ */

using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 手配度が下がった時に、警察(増援)を消える演出の後に消すクラス
/// 演出は全員の画面で行い、消すのはサーバー(またはオフライン)だけで行う
/// ※今は小さくなって消える仮の演出(モデルが入ったら透明になる演出などに差し替える)
/// </summary>
[RequireComponent(typeof(CS_PoliceBrain))]
public class CS_PoliceFadeOut : NetworkBehaviour
{
    // 消える演出を始める前の大きさ
    private Vector3 _baseScale = Vector3.one;

    // 消える演出の経過時間
    private float _fadeTimer = 0.0f;

    // 消える演出中か
    private bool _isFading = false;

    [SerializeField, Min(0.1f)]
    [Tooltip("消える演出にかける時間(秒)")]
    private float _fadeDuration = 1.0f;

    /// <summary>
    /// 行動を止め、消える演出を始めるメソッド(サーバー、またはオフラインで呼ぶこと)
    /// </summary>
    public void StartFadeOut()
    {
        if (_isFading) return;

        GetComponent<CS_PoliceBrain>().StopActing();

        // ネットワークに出現済みなら、全員の画面で演出する
        if (IsSpawned)
        {
            StartFadeOutRpc();
            return;
        }

        BeginFade();
    }

    // 全員(サーバー含む)で消える演出を始める
    [Rpc(SendTo.Everyone)]
    private void StartFadeOutRpc()
    {
        BeginFade();
    }

    private void Update()
    {
        if (!_isFading) return;

        _fadeTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(_fadeTimer / _fadeDuration);
        transform.localScale = _baseScale * (1.0f - progress);

        // 演出が終わったら消す(消すのはサーバー、またはオフラインだけ)
        if (progress < 1.0f || CS_PoliceSquad.IsNetworkClientOnly()) return;

        _isFading = false;
        CS_PoliceSpawnUtility.Despawn(gameObject);
    }

    /// <summary>
    /// 消える演出を始めるメソッド
    /// </summary>
    private void BeginFade()
    {
        _baseScale = transform.localScale;
        _fadeTimer = 0.0f;
        _isFading = true;
    }
}
