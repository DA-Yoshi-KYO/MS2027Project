/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-21 | 初回作成
 * ================================================ */

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World Spaceで表示するロックオンUIのView。
/// LockOnCanvasのルートへアタッチする。
/// </summary>
public class CS_UILockOnView :
    CS_BaseView<CS_UILockOnPresenter>
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("表示・非表示を切り替えるルート")]
    [SerializeField] private GameObject _markerRoot;

    [Header("ロックオン画像")]
    [SerializeField] private Image _lockOnImage;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        SetVisible(false);

        if (_lockOnImage != null)
            _lockOnImage.raycastTarget = false;
    }

    public override void SetPresenter(
        CS_UILockOnPresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    // =========================================================
    // 表示更新
    // =========================================================

    public void SetVisible(bool isVisible)
    {
        if (_markerRoot != null)
            _markerRoot.SetActive(isVisible);
    }

    /// <summary>
    /// World Space Canvasをカメラの正面へ向ける。
    /// </summary>
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
