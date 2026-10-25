using UnityEngine;
using UnityEngine.Rendering;

/*
 * ハイドポイントに張り付いて隠れている間、画面の端を少し暗くするクラス
 * CS_PlayerHideの状態を見て、CS_PlayerScreenEffectへ効果のオン・オフを伝えるだけ
 * (ゲームロジック側はこのクラスの存在を知らない。外してもハイドは動く)
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・かける条件は、見つからない状態(CS_PlayerHide.isHidden。サーバーが決めて同期する)の間
 *   飛んでいる間(まだ見つかる)はかけない
 * ・効果はVolume Profile(_hideEffect。既定はVO_HideScreenEffect)にまとめる
 *   画面の端を暗くする: Vignette(HDRPの標準)
 *   出し入れの速さ: CSV_ScreenEffectTimingのFade In Time / Fade Out Time
 * ・自分の画面のプレイヤーかどうかの判定はCS_PlayerScreenEffect側で行う
 */
// ========================================

[RequireComponent(typeof(CS_PlayerHide))]
[RequireComponent(typeof(CS_PlayerScreenEffect))]
public class CS_PlayerHideScreenEffect : MonoBehaviour
{
    [Header("かける効果")]
    [SerializeField] private VolumeProfile _hideEffect;   // 隠れている間にかける効果(画面の端を暗くする)

    private CS_PlayerHide _hide;
    private CS_PlayerScreenEffect _screenEffect;

    private void Awake()
    {
        _hide = GetComponent<CS_PlayerHide>();
        _screenEffect = GetComponent<CS_PlayerScreenEffect>();
    }

    private void LateUpdate()
    {
        _screenEffect.SetHeld(_hideEffect, _hide.isHidden);
    }

    private void OnDisable()
    {
        if (_screenEffect == null) return;
        _screenEffect.SetHeld(_hideEffect, false);
    }
}
