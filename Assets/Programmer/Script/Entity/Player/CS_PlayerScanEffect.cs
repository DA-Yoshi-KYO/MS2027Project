using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/*
 * スキャン(CS_PlayerScan)の見た目を出すクラス【仮の見た目】
 * 広がっていく波の輪と、見えている悪人の目印(建物越しにも見える)を、自分の画面にだけ出す
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・仮の見た目。本番のエフェクト(波が建物や地面を這っていくもの)・悪人のシルエット表示ができたら、
 *   このコンポーネントをPlayer.prefabから外して差し替える(CS_PlayerScanのロジックはこのクラスを知らない)
 * ・波: スキャンした位置を中心に、地面の少し上へLineRendererで円を描き、CS_PlayerScan.currentWaveRadiusに合わせて広げる
 *   マテリアルは_waveMaterial(HDRP/Unlit。仮でMT_DebugRangeを使う)。色はMaterialPropertyBlockで_waveColorにする
 * ・悪人の目印: 画面に重ねるCanvas(Screen Space - Overlay)へ、見えている悪人の頭上の位置に菱形を出す
 *   画面に直接描くので、建物の向こうにいても見える(仕様「壁や障害物を越えて画面に表示」)
 *   カメラの後ろにいる悪人は出さない
 * ・自分のプレイヤー(CS_Player.isLocalPlayer)でだけ動く
 */
// ========================================

[RequireComponent(typeof(CS_PlayerScan))]
[RequireComponent(typeof(CS_Player))]
public class CS_PlayerScanEffect : MonoBehaviour
{
    [Header("波")]
    [SerializeField] private Material _waveMaterial;                        // 波の輪のマテリアル(HDRP/Unlit)
    [SerializeField] private Color _waveColor = new Color(0.3f, 0.9f, 1f);   // 波の色
    [SerializeField][Min(0.01f)] private float _waveWidth = 0.3f;           // 波の輪の太さ(m)
    [SerializeField][Min(8)] private int _waveSegments = 96;                // 輪の分割数
    [SerializeField] private float _waveHeight = 0.2f;                      // 地面(スキャンした位置)からの高さ(m)

    [Header("悪人の目印")]
    [SerializeField] private Color _markerColor = new Color(1f, 0.25f, 0.2f, 0.9f);    // 目印の色
    [SerializeField][Min(1f)] private float _markerSize = 22f;             // 目印の大きさ(画面のピクセル)
    [SerializeField] private float _markerHeight = 2.2f;                    // 悪人の足元から、目印を出す高さ(m)

    private static readonly int _unlitColorId = Shader.PropertyToID("_UnlitColor");

    private CS_Player _player;
    private CS_PlayerScan _scan;

    private LineRenderer _wave;
    private MaterialPropertyBlock _propertyBlock;
    private Vector3[] _wavePoints;

    private Canvas _markerCanvas;
    private readonly List<RectTransform> _markers = new List<RectTransform>();

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _scan = GetComponent<CS_PlayerScan>();
    }

    private void OnDestroy()
    {
        if (_wave != null) Destroy(_wave.gameObject);
        if (_markerCanvas != null) Destroy(_markerCanvas.gameObject);
    }

    private void LateUpdate()
    {
        if (!_player.isLocalPlayer) return;

        UpdateWave();
        UpdateMarkers();
    }

    // ========================================
    // 波
    // ========================================

    private void UpdateWave()
    {
        if (!_scan.isScanning)
        {
            if (_wave != null) _wave.enabled = false;
            return;
        }

        if (_wave == null) CreateWave();

        _wave.enabled = true;
        SetWaveCircle(_scan.scanOrigin + Vector3.up * _waveHeight, _scan.currentWaveRadius);

        Color color = _waveColor;
        color.a *= _scan.waveAlpha;
        _wave.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor(_unlitColorId, color);
        _wave.SetPropertyBlock(_propertyBlock);
        _wave.widthMultiplier = _waveWidth * _scan.waveAlpha;   // 消える時は細くしていく(不透明のマテリアルでも消えて見えるように)
    }

    private void CreateWave()
    {
        // プレイヤーの子にすると一緒に動いてしまうため、親は持たせない
        GameObject waveObject = new GameObject($"{name}_ScanWave");
        _wave = waveObject.AddComponent<LineRenderer>();
        _wave.useWorldSpace = true;
        _wave.loop = true;
        _wave.positionCount = _waveSegments;
        _wave.sharedMaterial = _waveMaterial;
        _wave.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _wave.receiveShadows = false;
        _propertyBlock = new MaterialPropertyBlock();
        _wavePoints = new Vector3[_waveSegments];
    }

    private void SetWaveCircle(Vector3 center, float radius)
    {
        for (int i = 0; i < _waveSegments; i++)
        {
            float angle = Mathf.PI * 2f * i / _waveSegments;
            _wavePoints[i] = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
        }

        _wave.SetPositions(_wavePoints);
    }

    // ========================================
    // 悪人の目印
    // ========================================

    private void UpdateMarkers()
    {
        Camera viewCamera = _player.playerCamera;
        int count = 0;

        if (viewCamera != null)
        {
            foreach (CS_VillainHealth villain in _scan.revealedVillains)
            {
                if (villain == null) continue;

                Vector3 screen = viewCamera.WorldToScreenPoint(villain.transform.position + Vector3.up * _markerHeight);
                if (screen.z <= 0f) continue;   // カメラの後ろ

                GetMarker(count).position = new Vector2(screen.x, screen.y);
                count++;
            }
        }

        // 使わなかった目印は隠す
        for (int i = 0; i < _markers.Count; i++)
        {
            _markers[i].gameObject.SetActive(i < count);
        }
    }

    // index番目の目印(足りなければ作る)
    private RectTransform GetMarker(int index)
    {
        if (_markerCanvas == null) CreateMarkerCanvas();

        while (_markers.Count <= index)
        {
            _markers.Add(CreateMarker());
        }

        return _markers[index];
    }

    private void CreateMarkerCanvas()
    {
        GameObject canvasObject = new GameObject($"{name}_ScanMarkers");
        _markerCanvas = canvasObject.AddComponent<Canvas>();
        _markerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _markerCanvas.sortingOrder = -1;    // HUDより後ろに出す
    }

    private RectTransform CreateMarker()
    {
        GameObject markerObject = new GameObject("Marker", typeof(RectTransform), typeof(Image));
        RectTransform rect = markerObject.GetComponent<RectTransform>();
        rect.SetParent(_markerCanvas.transform, false);
        rect.sizeDelta = new Vector2(_markerSize, _markerSize);
        rect.localRotation = Quaternion.Euler(0f, 0f, 45f);     // 四角を回して菱形にする

        Image image = markerObject.GetComponent<Image>();
        image.color = _markerColor;
        image.raycastTarget = false;
        return rect;
    }
}
