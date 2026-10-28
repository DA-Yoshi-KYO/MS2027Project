using Unity.Netcode;
using UnityEngine;

/*
 * 悪人グループ1つ分の犯罪完遂ゲージ
 * スポーン位置(CS_VillainSpawnPoint)に付け、そこにいるグループの犯罪の進行度をゲージに表示する
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・使い方はClaudeDocs/犯罪完遂ゲージの使い方.md参照
 * ・1つのスポーン位置には同時に1グループしかいないので、「スポーン位置 = グループ」とみなしてゲージを置く
 *   (CS_VillainGroupはGameObjectではなく、悪人は撃退で消えるが、スポーン位置は消えないため)
 * ・犯罪の進行度(CS_VillainGroup.crimeProgress)はサーバーにしか無いので、
 *   サーバー(CS_VillainSpawner)がSetProgressでNetworkVariableに書き込み、全クライアントがゲージに反映する
 * ・ゲージ(VillainCrimeGaugeCanvas)はスポーン位置の直下に置く
 *   ゲージのPresenterは「親のTransform」でModelを探すので、孫や別の場所に置くと紐づかない
 * ・ゲージは表示中、常にカメラの方を向く
 * ・路地裏(BackAlley)のScaleを変えても、ゲージの大きさ・高さが変わらないようにする
 *   起動時に、スポーン位置(このオブジェクト)のScaleを親のScaleの逆数にして、見た目のScaleを1に戻す
 *   (ゲージ側のScaleで打ち消すと、カメラの方へ回した時に親の縦横の拡大率の違いで形がゆがむため、親側で打ち消す)
 *   → スポーン位置は位置だけを使うので、Scaleを変えても悪人の出現には影響しない
 *   → エディタ上(実行前)では、ゲージは親のScaleの分だけ大きく表示される
 * ・シーンへの組み込みは Tools/Villain/犯罪完遂ゲージをスポーン位置に組み込む で行う
 *   (NetworkObject・このコンポーネント・ゲージをまとめて付ける)
 */
// ========================================

[RequireComponent(typeof(CS_VillainSpawnPoint))]
public class CS_VillainCrimeGauge : NetworkBehaviour
{
    [SerializeField]
    [Tooltip("スポーン位置の直下に置いたゲージ(VillainCrimeGaugeCanvas)。表示・非表示を切り替える")]
    private GameObject _gaugeCanvas;

    // 書き込みはサーバーのみ(NetworkVariableのデフォルト)
    private readonly NetworkVariable<float> _progress = new NetworkVariable<float>();
    private readonly NetworkVariable<bool> _isVisible = new NetworkVariable<bool>();

    private CS_UICrimeGaugeModel _model;

    private void Awake()
    {
        CancelParentScale();

        // 進行度(crimeProgress)は0〜1なので、最大値1で作る
        _model = new CS_UICrimeGaugeModel(1f);

        // 自分のTransformで公開すると、直下のゲージが拾って表示する
        _model.Bind(transform);

        Apply();
    }

    public override void OnNetworkSpawn()
    {
        _progress.OnValueChanged += HandleProgressChanged;
        _isVisible.OnValueChanged += HandleVisibleChanged;

        // 途中参加したクライアントも、今の値で表示する
        Apply();
    }

    public override void OnNetworkDespawn()
    {
        _progress.OnValueChanged -= HandleProgressChanged;
        _isVisible.OnValueChanged -= HandleVisibleChanged;
    }

    // 進行度(0〜1)と表示・非表示を設定する(サーバー、またはオフライン)
    public void SetProgress(float progress, bool isVisible)
    {
        if (IsSpawned && !IsServer) return;

        _progress.Value = progress;
        _isVisible.Value = isVisible;

        // オフラインではOnValueChangedが呼ばれないので、ここで直接反映する
        if (!IsSpawned) Apply();
    }

    // 自分のScaleを親のScaleの逆数にし、見た目のScale(lossyScale)を1にする(ゲージが路地裏のScaleで伸び縮みしないため)
    private void CancelParentScale()
    {
        Transform parent = transform.parent;
        if (parent == null) return;

        Vector3 parentScale = parent.lossyScale;
        if (parentScale.x == 0f || parentScale.y == 0f || parentScale.z == 0f) return;

        transform.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);
    }

    // ゲージ(World SpaceのCanvas)を常にカメラの方へ向ける(悪人のHPゲージと同じ)
    private void LateUpdate()
    {
        if (_gaugeCanvas == null || !_gaugeCanvas.activeSelf) return;

        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        _gaugeCanvas.transform.rotation = mainCamera.transform.rotation;
    }

    private void HandleProgressChanged(float previous, float current) => Apply();

    private void HandleVisibleChanged(bool previous, bool current) => Apply();

    // 値をゲージに反映する(全クライアント)
    private void Apply()
    {
        _model.SetValue(_progress.Value);
        if (_gaugeCanvas != null) _gaugeCanvas.SetActive(_isVisible.Value);
    }

    public override void OnDestroy()
    {
        // Bindも自動で外れる
        _model?.Dispose();
        base.OnDestroy();
    }
}
