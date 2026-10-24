using UnityEngine;
using UnityEngine.UI;

/*
 * 今ダッシュボタンを押したら飛べるハイドポイント(CS_PlayerHide.currentCandidate)に、アイコンを出すクラス【仮の見た目】
 * 自分の画面にだけ出す
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・仮の見た目。本番のアイコンができたら、このコンポーネントをPlayer.prefabから外して差し替える
 *   (CS_PlayerHideのロジックはこのクラスを知らない)
 * ・画面に重ねるCanvas(Screen Space - Overlay)へ、ハイドポイントの立つ位置の上(_markerHeight)に四角を出す
 *   (スキャンの仮の目印 CS_PlayerScanEffect と同じ作り)
 * ・自分のプレイヤー(CS_Player.isLocalPlayer)でだけ動く
 */
// ========================================

[RequireComponent(typeof(CS_PlayerHide))]
[RequireComponent(typeof(CS_Player))]
public class CS_PlayerHideMarker : MonoBehaviour
{
    [SerializeField] private Color _markerColor = new Color(0.3f, 0.9f, 1f, 0.9f);   // アイコンの色
    [SerializeField][Min(1f)] private float _markerSize = 28f;     // アイコンの大きさ(画面のピクセル)
    [SerializeField] private float _markerHeight = 1.6f;           // 立つ位置から、アイコンを出す高さ(m)

    private CS_Player _player;
    private CS_PlayerHide _hide;

    private Canvas _markerCanvas;
    private RectTransform _marker;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _hide = GetComponent<CS_PlayerHide>();
    }

    private void OnDestroy()
    {
        if (_markerCanvas != null) Destroy(_markerCanvas.gameObject);
    }

    private void LateUpdate()
    {
        CS_HidePoint candidate = _player.isLocalPlayer ? _hide.currentCandidate : null;
        Camera viewCamera = _player.playerCamera;
        if (candidate == null || viewCamera == null)
        {
            if (_marker != null) _marker.gameObject.SetActive(false);
            return;
        }

        Vector3 screen = viewCamera.WorldToScreenPoint(candidate.standPosition + Vector3.up * _markerHeight);
        if (_marker == null) CreateMarker();

        _marker.gameObject.SetActive(screen.z > 0f);   // カメラの後ろには出さない
        _marker.position = new Vector2(screen.x, screen.y);
    }

    private void CreateMarker()
    {
        GameObject canvasObject = new GameObject($"{name}_HideMarker");
        _markerCanvas = canvasObject.AddComponent<Canvas>();
        _markerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _markerCanvas.sortingOrder = -1;    // HUDより後ろに出す

        GameObject markerObject = new GameObject("Marker", typeof(RectTransform), typeof(Image));
        _marker = markerObject.GetComponent<RectTransform>();
        _marker.SetParent(_markerCanvas.transform, false);
        _marker.sizeDelta = new Vector2(_markerSize, _markerSize);

        Image image = markerObject.GetComponent<Image>();
        image.color = _markerColor;
        image.raycastTarget = false;
    }
}
