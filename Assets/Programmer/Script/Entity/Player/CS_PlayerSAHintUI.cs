using UnityEngine;

/*
 * SAポイントに隠れている間、近くにある他のSAポイントの方向を矢印で案内するUI(SAヒントUI。CS_UISAHintModel)へ、
 * プレイヤーの隠れている状態を渡すクラス
 * UI側(Model/Presenter/View)はこのクラス・CS_Player・CS_PlayerHideの存在を一切知らない
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・UIの使い方はClaudeDocs/SAHintUI_使い方.md
 * ・「隠れている」は、体の中心がハイドポイントの隠れられる箱の中にある間(CS_PlayerHide.currentHidePoint。CS_PlayerHide.isHiddenと同じ条件)
 *   isHiddenはサーバーが決めて同期する値のため、クライアントだと表示が遅れる。ここでは自分の手元でその場で調べる
 *   隠れている間はStartHiding(今いるSAポイント)を呼び、そのSAポイント自身は案内しない。箱から出た時・倒れた時はStopHiding()
 *   信号機など、隠れられる箱を持たないSAポイントの上にいる時は出さない(隠れている扱いにならないため)
 * ・SAヒントは自分の画面に1つのため、自分(isLocalPlayer)のプレイヤーだけがModelを作ってBindする
 *   isLocalPlayerはスポーン後に決まるため、毎フレーム確かめて、自分のプレイヤーになった時にBindする
 * ・調整値(探す距離・矢印の数・楕円の大きさなど)はデータ(DB_SAHint。CSO_UISAHintData)で決める。矢印の見た目はUICanvas側(SAHint)
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでも単体で動く。通信はしない
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
[RequireComponent(typeof(CS_PlayerHide))]
public class CS_PlayerSAHintUI : MonoBehaviour
{
    [SerializeField] private CSO_UISAHintData _data;   // SAヒントUIの調整値(DB_SAHint)

    private CS_Player _player;
    private CS_PlayerHide _hide;
    private CS_PlayerHealth _health;
    private CS_UISAHintModel _model;
    private bool _hasWarnedNoData;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _hide = GetComponent<CS_PlayerHide>();
        _health = GetComponent<CS_PlayerHealth>();
    }

    private void OnDisable()
    {
        UnbindHintUI();
    }

    private void Update()
    {
        if (!_player.isLocalPlayer)
        {
            UnbindHintUI();   // NPC・他人のプレイヤーは出さない
            return;
        }

        if (_model == null && !BindHintUI()) return;

        CS_SAPoint point = IsDead() ? null : _hide.currentHidePoint;
        if (point != null) _model.StartHiding(point);   // 同じSAポイントのままなら、Model側で何もしない
        else _model.StopHiding();
    }

    // SAヒントUIのModelを作って公開する(カメラがまだ無ければ、次のフレームでやり直す)
    private bool BindHintUI()
    {
        if (_data == null)
        {
            if (!_hasWarnedNoData) Debug.LogWarning("CS_PlayerSAHintUI: SAヒントUIのデータ(DB_SAHint)が未設定です", this);
            _hasWarnedNoData = true;
            return false;
        }

        Camera playerCamera = _player.playerCamera;
        if (playerCamera == null) return false;

        _model = new CS_UISAHintModel(transform, playerCamera, _data);
        _model.Bind();
        return true;
    }

    private void UnbindHintUI()
    {
        if (_model == null) return;

        _model.Dispose();
        _model = null;
    }

    private bool IsDead()
    {
        return _health != null && _health.isDead;
    }
}
