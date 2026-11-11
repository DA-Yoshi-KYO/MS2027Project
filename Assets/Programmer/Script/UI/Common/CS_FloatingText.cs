using TMPro;
using UnityEngine;

/*
 * ワールド空間に出す、昇りながら消える文字(「POWER UP!!」など)
 * Spawnで生成すると、常にカメラの方を向きながら上に昇り、だんだん薄くなって消える
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・使い方 : CS_FloatingText.Spawn(位置, 設定) を呼ぶだけ(各クライアントの手元で表示する。ネットワークの同期はしない)
 * ・文字はTextMeshPro(3D)で作る。フォントはTMP Settingsのデフォルトフォント
 * ・duration 秒かけて riseDistance(m) 昇り、後半(fadeStartRate以降)で透明になって消える
 * ・ビルボード : 毎フレーム、メインカメラと同じ向きにする(文字が常に正面から読める)
 */
// ========================================

public class CS_FloatingText : MonoBehaviour
{
    // 表示の設定(呼び出し側のInspectorで調整する)
    [System.Serializable]
    public class Settings
    {
        [SerializeField]
        [Tooltip("表示する文字")]
        private string _text = "POWER UP!!";

        [SerializeField]
        [Tooltip("文字の色")]
        private Color _color = new Color(1f, 0.75f, 0.1f);

        [SerializeField, Min(0.01f)]
        [Tooltip("文字の大きさ")]
        private float _fontSize = 4f;

        [SerializeField, Min(0f)]
        [Tooltip("消えるまでに昇る距離(m)")]
        private float _riseDistance = 0.8f;

        [SerializeField, Min(0.01f)]
        [Tooltip("表示してから消えるまでの時間(秒)")]
        private float _duration = 0.5f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("この割合の時間がたってから薄くなり始める")]
        private float _fadeStartRate = 0.4f;

        public string text => _text;
        public Color color => _color;
        public float fontSize => _fontSize;
        public float riseDistance => _riseDistance;
        public float duration => _duration;
        public float fadeStartRate => _fadeStartRate;
    }

    private Settings _settings;
    private TextMeshPro _textMesh;
    private Vector3 _startPosition;
    private float _elapsed;

    // positionに文字を出す
    public static CS_FloatingText Spawn(Vector3 position, Settings settings)
    {
        GameObject textObject = new GameObject($"FloatingText_{settings.text}");
        textObject.transform.position = position;

        TextMeshPro textMesh = textObject.AddComponent<TextMeshPro>();
        textMesh.text = settings.text;
        textMesh.color = settings.color;
        textMesh.fontSize = settings.fontSize;
        textMesh.fontStyle = FontStyles.Bold;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.enableWordWrapping = false;

        CS_FloatingText floatingText = textObject.AddComponent<CS_FloatingText>();
        floatingText._settings = settings;
        floatingText._textMesh = textMesh;
        floatingText._startPosition = position;
        floatingText.FaceCamera();
        return floatingText;
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _settings.duration);

        transform.position = _startPosition + Vector3.up * (_settings.riseDistance * t);
        FaceCamera();

        // 後半で薄くする
        float fade = _settings.fadeStartRate < 1f ? Mathf.InverseLerp(_settings.fadeStartRate, 1f, t) : 0f;
        Color color = _settings.color;
        color.a *= 1f - fade;
        _textMesh.color = color;

        if (t >= 1f) Destroy(gameObject);
    }

    // ビルボード(メインカメラと同じ向きにする)
    private void FaceCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null) transform.rotation = mainCamera.transform.rotation;
    }
}
