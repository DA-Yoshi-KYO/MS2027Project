using UnityEngine;

/*
 * 悪人のHPを頭上のHPゲージ(VillainHpBarCanvas)に表示するクラス
 * CS_VillainHealthのHPをCS_UIVillainHpModelに渡し、ゲージをカメラの方へ向ける
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・使い方(ClaudeDocs/VillainHpUIの使い方.md 参照)
 *   VillainHpBarCanvas.prefab を悪人のプレハブの直下に置き、このクラスを悪人のルートに付ける
 *   → Bind(transform)したModelを、直下のHPバー(CS_UIVillainHpPresenter)が拾って表示する
 * ・HPは毎フレーム確認し、表示中の値から変わった時だけModelに渡す
 *   CS_VillainHealth.onHpChangedはオフライン時や最大HPの変更では呼ばれないため、イベントではなく値を見る
 *   → サーバー・クライアント・オフラインのどれでも同じように表示できる
 * ・Modelは整数でHPを持つので、小数のHPは切り上げて表示する(HPが残っているのに0と表示されないように)
 * ・HPバーはWorld SpaceのCanvasなので、悪人と一緒に回らないよう毎フレームカメラと同じ向きにする
 */
// ========================================

[RequireComponent(typeof(CS_VillainHealth))]
public class CS_VillainHpUI : MonoBehaviour
{
    [SerializeField]
    [Tooltip("カメラの方へ向けるHPバー(直下に置いたVillainHpBarCanvas)")]
    private Transform _hpBar;

    private CS_VillainHealth _health;
    private CS_UIVillainHpModel _hpModel;
    private int _shownHp = -1;      // 今Modelに渡しているHP(-1 = まだ渡していない)
    private int _shownMaxHp = -1;   // 今Modelに渡している最大HP(-1 = まだ渡していない)

    private void Awake()
    {
        _health = GetComponent<CS_VillainHealth>();

        // HPが決まるのはCS_VillainStats・CS_VillainHealthの初期化後なので、仮の値で作ってLateUpdateで合わせる
        _hpModel = new CS_UIVillainHpModel(1);
        _hpModel.Bind(transform);

        if (_hpBar == null)
        {
            Debug.LogWarning("CS_VillainHpUI: Hp Bar が未設定のため、HPバーをカメラの方へ向けません", this);
        }
    }

    private void LateUpdate()
    {
        UpdateHp();
        FaceCamera();
    }

    private void OnDestroy()
    {
        _hpModel?.Dispose();
    }

    // HPか最大HPが表示中の値から変わっていたら、Modelに渡す
    private void UpdateHp()
    {
        int maxHp = Mathf.CeilToInt(_health.maxHp);
        int currentHp = Mathf.CeilToInt(_health.currentHp);

        // 最大HPを先に渡す(現在HPが仮の最大HPで切り詰められないように)
        if (maxHp != _shownMaxHp)
        {
            _shownMaxHp = maxHp;
            _hpModel.SetMaxHp(maxHp);
        }

        if (currentHp == _shownHp) return;

        _shownHp = currentHp;
        _hpModel.SetHp(currentHp);
    }

    // HPバーをカメラと同じ向きにする(正面から読めるように)
    private void FaceCamera()
    {
        if (_hpBar == null) return;

        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        _hpBar.rotation = mainCamera.transform.rotation;
    }
}
