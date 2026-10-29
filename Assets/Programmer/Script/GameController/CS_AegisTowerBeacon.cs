using UnityEngine;

/*
 * 街の中央にあるAEGISのシンボルタワーから、光の柱とサーチライトを出すクラス【仮の演出】
 * メンテナンス中はオレンジで点滅し、メンテナンスが終わる(時間切れ)と水色で点きっぱなしになる
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・世界観: 治安維持AI「AEGIS」は、普段は街の中央のタワーから街を見張っている。ゲーム中はそのAIが定期メンテナンスで止まっている
 *   (Confluence「フィールド」: 中央にAEGISのシンボルタワーと広場があり、それを囲むように3つのエリアがある)
 * ・光の柱: タワーの位置(_towerPosition)から真上へ_height mの線。メンテナンス中はゆっくり明滅する
 * ・サーチライト: 柱の上から斜め下へ伸びる線が、ゆっくり回る(止まっているAIの名残として、メンテナンス中は暗く遅い)
 * ・どちらもLineRendererで描く。マテリアルは_material(HDRP/Unlit。仮でMT_DebugRange)。色はMaterialPropertyBlockで変える
 * ・時間はCS_TimerController(同じオブジェクト、またはシーン内)から読む。見た目だけで通信はしない
 * ・仮の演出。本番のタワー・ライトができたら、GameControllerManager.prefabからこのコンポーネントを外す
 */
// ========================================

public class CS_AegisTowerBeacon : MonoBehaviour
{
    [Header("場所")]
    [SerializeField] private Vector3 _towerPosition = Vector3.zero;   // タワーの足元(街の中央)
    [SerializeField][Min(1f)] private float _height = 140f;          // 光の柱の高さ(m)

    [Header("見た目")]
    [SerializeField] private Material _material;
    [SerializeField] private Color _maintenanceColor = new Color(1f, 0.55f, 0.1f);   // メンテナンス中(オレンジ)
    [SerializeField] private Color _onlineColor = new Color(0.3f, 0.9f, 1f);         // 動いている時(水色)
    [SerializeField][Min(0f)] private float _pillarWidth = 2.5f;
    [SerializeField][Min(0f)] private float _pulseSpeed = 1.2f;       // メンテナンス中の明滅の速さ(回/秒)

    [Header("サーチライト")]
    [SerializeField][Min(0f)] private float _searchLength = 160f;     // 柱の上から伸びる長さ(m)
    [SerializeField][Range(0f, 89f)] private float _searchTilt = 35f; // 水平から下へ傾ける角度(度)
    [SerializeField] private float _maintenanceTurnSpeed = 6f;        // メンテナンス中に回る速さ(度/秒)
    [SerializeField] private float _onlineTurnSpeed = 40f;            // 動いている時に回る速さ(度/秒)
    [SerializeField][Min(0f)] private float _searchWidth = 1.2f;

    private static readonly int _unlitColorId = Shader.PropertyToID("_UnlitColor");

    private CS_TimerController _timer;
    private LineRenderer _pillar;
    private LineRenderer _search;
    private MaterialPropertyBlock _block;
    private float _searchAngle;

    private void Start()
    {
        _timer = GetComponent<CS_TimerController>();
        if (_timer == null) _timer = FindAnyObjectByType<CS_TimerController>();

        _block = new MaterialPropertyBlock();
        _pillar = CreateLine("AegisBeacon_Pillar");
        _search = CreateLine("AegisBeacon_Searchlight");
        _pillar.SetPosition(0, _towerPosition);
        _pillar.SetPosition(1, _towerPosition + Vector3.up * _height);
    }

    private void OnDestroy()
    {
        if (_pillar != null) Destroy(_pillar.gameObject);
        if (_search != null) Destroy(_search.gameObject);
    }

    private void Update()
    {
        if (_pillar == null) return;

        bool isMaintenance = _timer == null || _timer.GetRemainTime() > 0f;

        // 光の柱: メンテナンス中はゆっくり明滅し、終わったら点きっぱなし
        float pulse = isMaintenance ? 0.55f + 0.45f * Mathf.Sin(Time.time * _pulseSpeed * Mathf.PI * 2f) : 1f;
        Color color = isMaintenance ? _maintenanceColor : _onlineColor;
        SetLine(_pillar, color * pulse, _pillarWidth * (0.7f + 0.3f * pulse));

        // サーチライト: 柱の上から斜め下へ、ゆっくり回る
        _searchAngle += (isMaintenance ? _maintenanceTurnSpeed : _onlineTurnSpeed) * Time.deltaTime;
        Vector3 top = _towerPosition + Vector3.up * _height;
        Vector3 direction = Quaternion.Euler(_searchTilt, _searchAngle, 0f) * Vector3.forward;
        _search.SetPosition(0, top);
        _search.SetPosition(1, top + direction * _searchLength);
        SetLine(_search, color * (isMaintenance ? 0.5f : 1f), _searchWidth);
    }

    private LineRenderer CreateLine(string name)
    {
        LineRenderer line = new GameObject(name).AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.sharedMaterial = _material;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    private void SetLine(LineRenderer line, Color color, float width)
    {
        line.widthMultiplier = width;
        line.GetPropertyBlock(_block);
        _block.SetColor(_unlitColorId, color);
        line.SetPropertyBlock(_block);
    }

    // 選択中に、タワーの位置と柱をシーンビューへ表示する(位置合わせ用)
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _maintenanceColor;
        Gizmos.DrawLine(_towerPosition, _towerPosition + Vector3.up * _height);
        Gizmos.DrawWireSphere(_towerPosition, 3f);
    }
}
