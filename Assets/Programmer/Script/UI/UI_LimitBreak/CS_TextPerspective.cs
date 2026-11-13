/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-13 | 初回作成
 * ================================================ */
using TMPro;
using UnityEngine;

/// <summary>
/// TextMeshPro の頂点を台形に変形し、遠近感（左が大きく右が小さい）を付ける。
/// TextMeshPro - Text (UI) と同じ GameObject にアタッチして使う。Canvas は 2D(Screen Space)のままでよい。
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(TMP_Text))]
public class CS_TmpPerspective : MonoBehaviour
{
    [Header("遠近設定（1 = 元の高さ）")]
    [SerializeField, Range(0.1f, 1f)] private float _leftScale = 1f;    // 左端の高さ倍率
    [SerializeField, Range(0.1f, 1f)] private float _rightScale = 0.5f; // 右端の高さ倍率

    private TMP_Text _text;

    // 公開用（式形式メンバー + バッキングフィールド）
    public float leftScale => _leftScale;
    public float rightScale => _rightScale;

    /// <summary>
    /// 実行中に倍率を変えたい時に呼ぶ。
    /// </summary>
    public void SetScale(float leftScale, float rightScale)
    {
        _leftScale = Mathf.Clamp(leftScale, 0.1f, 1f);
        _rightScale = Mathf.Clamp(rightScale, 0.1f, 1f);

        RefreshText();
    }

    private void OnEnable()
    {
        _text = GetComponent<TMP_Text>();

        // テキストのメッシュが再生成されるたびに通知を受け取る（毎フレーム処理はしない）
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        RefreshText();
    }

    private void OnDisable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);

        // 無効化したら元の形に戻す
        RefreshText();
    }

    private void OnValidate()
    {
        // Inspectorで値を変えた時に反映する
        if (isActiveAndEnabled) RefreshText();
    }

    /// <summary>
    /// メッシュを再生成する。再生成後に OnTextChanged が呼ばれて台形が適用される。
    /// </summary>
    private void RefreshText()
    {
        if (_text == null) return;

        _text.ForceMeshUpdate();
    }

    private void OnTextChanged(Object changedObject)
    {
        if (_text == null || changedObject != _text) return;

        ApplyPerspective();
    }

    /// <summary>
    /// X位置に応じて、縦方向を中心線に向かって縮める。
    /// </summary>
    private void ApplyPerspective()
    {
        TMP_TextInfo textInfo = _text.textInfo;
        if (textInfo == null || textInfo.meshInfo == null) return;

        // 全メッシュの頂点範囲を求める
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            Vector3[] vertices = textInfo.meshInfo[i].vertices;
            int vertexCount = textInfo.meshInfo[i].vertexCount;

            for (int j = 0; j < vertexCount; j++)
            {
                Vector3 position = vertices[j];
                if (position.x < minX) minX = position.x;
                if (position.x > maxX) maxX = position.x;
                if (position.y < minY) minY = position.y;
                if (position.y > maxY) maxY = position.y;
            }
        }

        float width = maxX - minX;
        if (width <= 0f) return;

        float centerY = (minY + maxY) * 0.5f;

        // 頂点を変形する
        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            Vector3[] vertices = textInfo.meshInfo[i].vertices;
            int vertexCount = textInfo.meshInfo[i].vertexCount;

            for (int j = 0; j < vertexCount; j++)
            {
                Vector3 position = vertices[j];

                float t = (position.x - minX) / width;   // 0(左端)〜1(右端)
                float scale = Mathf.Lerp(_leftScale, _rightScale, t);

                position.y = centerY + (position.y - centerY) * scale;
                vertices[j] = position;
            }
        }

        // 変形した頂点をメッシュに反映する
        _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    }
}
