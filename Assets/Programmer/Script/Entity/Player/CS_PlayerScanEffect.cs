using UnityEngine;

/*
 * スキャン(CS_PlayerScan)の波の輪を出すクラス【仮の見た目】
 * 広がっていく波の輪を、自分の画面にだけ出す
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・仮の見た目。本番のエフェクト(波が建物や地面を這っていくもの)ができたら、
 *   このコンポーネントをPlayer.prefabから外して差し替える(CS_PlayerScanのロジックはこのクラスを知らない)
 * ・波: スキャンした位置を中心に、地面の少し上へLineRendererで円を描き、CS_PlayerScan.currentWaveRadiusに合わせて広げる
 *   マテリアルは_waveMaterial(HDRP/Unlit。仮でMT_DebugRangeを使う)。色はMaterialPropertyBlockで_waveColorにする
 * ・見えている悪人の目印はUIなので、MVP(CS_UIScanMarkerModel)で出す。Modelへ渡すのはCS_PlayerScanMarkerUI
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

    private static readonly int _unlitColorId = Shader.PropertyToID("_UnlitColor");

    private CS_Player _player;
    private CS_PlayerScan _scan;

    private LineRenderer _wave;
    private MaterialPropertyBlock _propertyBlock;
    private Vector3[] _wavePoints;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _scan = GetComponent<CS_PlayerScan>();
    }

    private void OnDestroy()
    {
        if (_wave != null) Destroy(_wave.gameObject);
    }

    private void LateUpdate()
    {
        if (!_player.isLocalPlayer) return;

        UpdateWave();
    }

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
}
