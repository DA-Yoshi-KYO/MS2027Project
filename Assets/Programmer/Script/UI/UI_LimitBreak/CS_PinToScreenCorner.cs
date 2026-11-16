/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-13 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// World Space の Canvas を、カメラ画面の指定位置（初期値は左下）に固定する。
/// カメラはプレイヤー生成後に SetTargetCamera() で受け取る（Find は使わない）。
/// </summary>
public class CS_PinToScreenCorner : MonoBehaviour
{
    [Header("配置設定")]
    [SerializeField] private float _distance = 5f;                                   // カメラからの距離
    [SerializeField] private Vector2 _viewportPosition = new Vector2(0.1f, 0.1f);    // 画面左下からの割合(0〜1)

    private Camera _targetCamera;

    // 公開用（式形式メンバー + バッキングフィールド）
    public float distance => _distance;
    public Vector2 viewportPosition => _viewportPosition;
    public Camera targetCamera => _targetCamera;

    /// <summary>
    /// プレイヤー生成時にカメラを渡す。
    /// </summary>
    public void SetTargetCamera(Camera camera)
    {
        _targetCamera = camera;
        UpdateTransform(); // 生成直後から正しい位置に表示する
    }

    /// <summary>
    /// プレイヤー破棄時などに追従を解除する。
    /// </summary>
    public void ClearTargetCamera()
    {
        _targetCamera = null;
    }

    private void LateUpdate()
    {
        // カメラ未設定（プレイヤー生成前）、または破棄済みの場合は何もしない
        if (_targetCamera == null) return;

        UpdateTransform();
    }

    /// <summary>
    /// ビューポート座標をワールド座標に変換し、位置と向きをカメラに合わせる。
    /// </summary>
    private void UpdateTransform()
    {
        if (_targetCamera == null) return;

        Vector3 viewportPoint = new Vector3(_viewportPosition.x, _viewportPosition.y, _distance);
        transform.position = _targetCamera.ViewportToWorldPoint(viewportPoint);
        transform.rotation = _targetCamera.transform.rotation;
    }
}