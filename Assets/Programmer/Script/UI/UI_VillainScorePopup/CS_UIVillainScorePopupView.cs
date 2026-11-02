/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-28 | 初回作成
 * ================================================ */

using TMPro;
using UnityEngine;

/// <summary>
/// World Space上の悪人撃退スコアポップアップView。
/// 表示内容・位置・透明度だけを反映する。
/// </summary>
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasGroup))]
public sealed class CS_UIVillainScorePopupView :
    CS_BaseView<CS_UIVillainScorePopupPresenter>
{
    [Header("スコア表示")]
    [SerializeField] private TextMeshProUGUI _scoreText;

    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        if (_scoreText != null)
            _scoreText.raycastTarget = false;
    }

    public override void SetPresenter(
        CS_UIVillainScorePopupPresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    public void SetScore(int score)
    {
        if (_scoreText != null)
            _scoreText.text = $"{score:+#;-#;0}点";
    }

    public void SetWorldPosition(Vector3 worldPosition)
    {
        transform.position = worldPosition;
    }

    public void SetAlpha(float alpha)
    {
        if (_canvasGroup != null)
            _canvasGroup.alpha = Mathf.Clamp01(alpha);
    }

    public void LookAtCamera(Camera targetCamera)
    {
        if (targetCamera == null)
            return;

        Vector3 direction =
            transform.position - targetCamera.transform.position;

        if (direction.sqrMagnitude <= 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(
            direction,
            targetCamera.transform.up
        );
    }
}
