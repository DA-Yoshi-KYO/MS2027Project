using UnityEngine;

/*
 * プレイアブルキャラクター1人分の設定(ScriptableObject)
 * 基礎数値・必殺技(攻撃と、使った後に続く効果)をキャラクターごとに持つ
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・プランナーは「Tools → プレイヤー調整」のウィンドウから編集する(Inspectorで直接編集してもよい)
 * ・4キャラ並べた一覧はCSO_PlayableCharacterRosterが持つ。プレイヤーへの反映はCS_PlayerCharacterが行う
 * ・基礎数値(_stats)・必殺技の攻撃(_specialAttack)が未設定の時は、Player.prefabに設定されている共通の値を使う
 * ・必殺技の効果(仕様書「プレイヤー」の必殺技。効果の種類はCSE_PlayerSpecialEffect)
 *   必殺技が当たる瞬間(ゲージを消費した時)から_effectDuration秒続く
 *   _boostRateは「身体能力の強化」の時だけ使う
 */
// ========================================

[CreateAssetMenu(fileName = "DB_PlayableCharacter", menuName = "Player/Playable Character")]
public class CSO_PlayableCharacter : ScriptableObject
{
    [Header("キャラクター")]
    [SerializeField] private string _displayName = "キャラクター";     // 表示名
    [SerializeField] private Sprite _icon;                             // アイコン(UI用。未設定でもよい)
    [SerializeField] private Color _themeColor = Color.white;          // キャラクターの色(UI用)

    [Header("基礎数値")]
    [SerializeField] private CSO_PlayerStats _stats;                   // 未設定ならPlayer.prefabの共通の値

    [Header("必殺技")]
    [SerializeField] private string _specialName = "必殺技";           // 必殺技の名前
    [SerializeField] private CSO_AttackData _specialAttack;            // 必殺技の攻撃。未設定ならPlayer.prefabの共通の攻撃
    [SerializeField] private CSE_PlayerSpecialEffect _specialEffect;   // 必殺技を使った後に続く効果
    [SerializeField][Min(0f)] private float _effectDuration = 60f;     // 効果が続く時間(秒)
    [SerializeField][Min(0f)] private float _boostRate = 1.5f;         // 身体能力の強化の倍率(1で変化なし)

    [Header("メモ")]
    [SerializeField][TextArea(2, 5)] private string _memo;             // プランナー用のメモ(ゲームには使わない)

    public string displayName => _displayName;
    public Sprite icon => _icon;
    public Color themeColor => _themeColor;
    public CSO_PlayerStats stats => _stats;
    public string specialName => _specialName;
    public CSO_AttackData specialAttack => _specialAttack;
    public CSE_PlayerSpecialEffect specialEffect => _specialEffect;
    public float effectDuration => _effectDuration;
    public float boostRate => _boostRate;
}
