using UnityEngine;

/*
 * グラップルのロープ表示(プレイヤーに付ける)
 * グラップルSAポイントへ飛んでいる間だけ、プレイヤーの手元と的の間にLineRendererで線を引く
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・CS_PlayerHideの飛び始め・飛び終わり(onFlyBegin・onFlyEnd)を購読して出し入れする。CS_PlayerHide側はこのクラスを知らない
 * ・グラップルSAポイント(CS_GrappleSAPoint)へ飛ぶ時だけ出す(信号機・ハイドポイントへ飛ぶ時は出さない)
 * ・線は仮の見た目(まっすぐな細い線)。LineRendererは自分の子に実行時に作るので、プレハブに用意しなくてよい
 * ・手元の位置は、プレイヤーの位置からの相対(_handOffset。プレイヤーの向き基準)で決める
 * ・飛ぶのは本人の手元だけなので、ロープも本人の画面にだけ出る(他のプレイヤーには見えない)
 */
// ========================================

[RequireComponent(typeof(CS_PlayerHide))]
public class CS_PlayerGrappleRope : MonoBehaviour
{
    [Header("ロープの見た目")]
    [SerializeField] private Vector3 _handOffset = new Vector3(0.3f, 0.2f, 0.3f);   // 手元の位置(プレイヤーの位置から、プレイヤーの向き基準。m)
    [SerializeField][Min(0.001f)] private float _width = 0.05f;                     // ロープの太さ(m)
    [SerializeField] private Color _color = new Color(0.9f, 0.85f, 0.6f);           // ロープの色
    [SerializeField] private Material _material;                                    // ロープの材質(空なら仮の材質を作る)

    private CS_PlayerHide _hide;
    private LineRenderer _line;
    private CS_GrappleSAPoint _target;      // 今飛んでいるグラップルポイント(無ければnull)

    private void Awake()
    {
        _hide = GetComponent<CS_PlayerHide>();
        CreateLine();
    }

    private void OnEnable()
    {
        _hide.onFlyBegin += OnFlyBegin;
        _hide.onFlyEnd += OnFlyEnd;
    }

    private void OnDisable()
    {
        _hide.onFlyBegin -= OnFlyBegin;
        _hide.onFlyEnd -= OnFlyEnd;
        Hide();
    }

    // プレイヤーの移動・飛ぶ処理が終わった後に位置を合わせる(ロープが遅れて見えないように)
    private void LateUpdate()
    {
        if (_target == null) return;

        _line.SetPosition(0, transform.TransformPoint(_handOffset));
        _line.SetPosition(1, _target.ropeEndPosition);
    }

    private void OnFlyBegin(CS_SAPoint point)
    {
        if (point is not CS_GrappleSAPoint grapple) return;

        _target = grapple;
        _line.enabled = true;
        LateUpdate();
    }

    private void OnFlyEnd(CS_SAPoint point)
    {
        Hide();
    }

    private void Hide()
    {
        _target = null;
        if (_line != null) _line.enabled = false;
    }

    // ロープ用のLineRendererを子に作る
    private void CreateLine()
    {
        GameObject lineObject = new GameObject("GrappleRope");
        lineObject.transform.SetParent(transform, false);

        _line = lineObject.AddComponent<LineRenderer>();
        _line.positionCount = 2;
        _line.useWorldSpace = true;
        _line.widthMultiplier = _width;
        _line.startColor = _color;
        _line.endColor = _color;
        _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _line.receiveShadows = false;
        _line.material = _material != null ? _material : CreateDefaultMaterial();
        _line.enabled = false;
    }

    // 仮の材質(単色)。使えるシェーダーが見つからなければnull(線はピンクになる)
    private Material CreateDefaultMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;

        Material material = new Material(shader);
        material.color = _color;
        return material;
    }
}
