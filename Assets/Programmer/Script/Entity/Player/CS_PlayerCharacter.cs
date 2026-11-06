using System;
using Unity.Netcode;
using UnityEngine;

/*
 * プレイヤーが使っているプレイアブルキャラクターを持ち、キャラクターごとの必殺技の効果を効かせるクラス
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・キャラクターは一覧(CSO_PlayableCharacterRoster)の番号で決める。サーバー(とオフライン)は_serverIndexで持ち、スポーンした時に
 *   NetworkVariableへ書いて全員に同期する(スポーン前にNetworkVariableへは書けないため。CS_PlayerStatsがスポーン時にすぐ読むので、サーバーは_serverIndexを読む)
 *   選択画面はまだ無いので、今は全員が一覧の「テストで使うキャラクター」になる。選択画面を作る時はSetCharacterIndexを使う
 * ・キャラクターの基礎数値はCS_PlayerStatsが、必殺技の攻撃はCS_PlayerSpecialAttackが、ここのcurrentCharacterを見て使う
 *   (キャラクターで未設定の時は、それぞれPlayer.prefabに設定されている共通の値を使う)
 * ・必殺技の効果: 必殺技でゲージを消費した時(CS_PlayerSpecialAttack.onSpecialActivated、サーバー)から、キャラクターの効果時間だけ続く
 *   続いているかはeffectRemaining(全員で読める)で分かる。効果ごとに効かせる場所は次の通り
 *     正面からの奇襲   : CS_PlayerAmbushが、奇襲の「背後か上」の条件を見ない
 *     身体能力の強化   : ここ(サーバー)でCS_PlayerStats.SetSpecialBoostRateを掛け、終わったら1に戻す
 *     全員が見える     : CS_PlayerScanが、全ての悪人グループ・警察・巡回ドローンを見えている状態にする(使った本人の画面のみ)
 *     警察に見つからない: CS_PlayerTransformationが、変身中のタグを外す(警察の視界は変身中のタグのプレイヤーだけを見つける)
 * ・効果の残り時間は、サーバーの時刻での終わる時刻で持つ(オフラインではTime.timeAsDouble)。番号と同じく、サーバー(とオフライン)は
 *   _serverEffectEndTimeで持ち、スポーン中はNetworkVariable(_effectEndTime)にも書いて全員に同期する
 */
// ========================================

public class CS_PlayerCharacter : NetworkBehaviour
{
    [SerializeField] private CSO_PlayableCharacterRoster _roster;

    private readonly NetworkVariable<int> _characterIndex = new NetworkVariable<int>();
    private readonly NetworkVariable<double> _effectEndTime = new NetworkVariable<double>();
    private int _serverIndex;             // サーバー(とオフライン)でのキャラクターの番号
    private double _serverEffectEndTime;  // サーバー(とオフライン)での、必殺技の効果が終わる時刻

    private CS_PlayerStats _stats;
    private CS_PlayerSpecialAttack _specialAttack;
    private CS_PlayerTransformation _transformation;
    private bool _isBoosting;             // 身体能力の強化を掛けているか(サーバー、またはオフラインのみ)

    public CSO_PlayableCharacterRoster roster => _roster;
    public int characterIndex => !IsSpawned || IsServer ? _serverIndex : _characterIndex.Value;
    public CSO_PlayableCharacter currentCharacter => _roster != null ? _roster.Get(characterIndex) : null;
    public CSE_PlayerSpecialEffect currentEffect => currentCharacter != null ? currentCharacter.specialEffect : CSE_PlayerSpecialEffect.None;
    public float effectRemaining => Mathf.Max(0f, (float)((!IsSpawned || IsServer ? _serverEffectEndTime : _effectEndTime.Value) - GetNow()));   // 必殺技の効果の残り時間(秒)
    public bool isEffectActive => currentEffect != CSE_PlayerSpecialEffect.None && effectRemaining > 0f;

    public event Action<CSO_PlayableCharacter> onCharacterChanged;   // キャラクターが変わった時(全員)

    private void Awake()
    {
        _stats = GetComponent<CS_PlayerStats>();
        _specialAttack = GetComponent<CS_PlayerSpecialAttack>();
        _transformation = GetComponent<CS_PlayerTransformation>();

        // スポーン前に決めておく(CS_PlayerStatsがスポーン時にこのキャラクターの基礎数値を使うため)
        if (_roster != null) _serverIndex = _roster.testCharacterIndex;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer) _characterIndex.Value = _serverIndex;
        _characterIndex.OnValueChanged += HandleCharacterIndexChanged;
    }

    public override void OnNetworkDespawn()
    {
        _characterIndex.OnValueChanged -= HandleCharacterIndexChanged;
    }

    private void OnEnable()
    {
        if (_specialAttack != null) _specialAttack.onSpecialActivated += HandleSpecialActivated;
    }

    private void OnDisable()
    {
        if (_specialAttack != null) _specialAttack.onSpecialActivated -= HandleSpecialActivated;
    }

    private void Update()
    {
        // 警察に見つからない効果は、全員の手元でタグを切り替える(警察の判定はサーバーで行うが、見た目のずれを無くすため)
        if (_transformation != null) _transformation.SetHiddenFromPolice(IsEffectActive(CSE_PlayerSpecialEffect.HiddenFromPolice));

        UpdateBoost();
    }

    // この効果が今続いているか
    public bool IsEffectActive(CSE_PlayerSpecialEffect effect)
    {
        return currentEffect == effect && isEffectActive;
    }

    // キャラクターを変える(サーバー、またはオフラインのみ)。キャラクター選択の画面から、スポーン前に呼ぶ想定
    // スポーン後に変えた時は、基礎数値を新しいキャラクターの値に戻す
    public void SetCharacterIndex(int index)
    {
        if (IsSpawned && !IsServer) return;
        if (_roster == null || _roster.Get(index) == null || index == _serverIndex) return;

        int previous = _serverIndex;
        _serverIndex = index;
        SetEffectEndTime(0d);

        // スポーン中は、NetworkVariableの変更通知で全員(サーバーも)に知らせる
        if (IsSpawned) _characterIndex.Value = index;
        else HandleCharacterIndexChanged(previous, index);
        if (_stats != null) _stats.ResetToBase();
    }

    // 必殺技でゲージを消費した時、キャラクターの効果を始める(サーバー、またはオフラインで発生する)
    private void HandleSpecialActivated()
    {
        CSO_PlayableCharacter character = currentCharacter;
        if (character == null || character.specialEffect == CSE_PlayerSpecialEffect.None) return;

        SetEffectEndTime(GetNow() + character.effectDuration);
    }

    // 効果が終わる時刻を決める(サーバー、またはオフラインのみ)
    private void SetEffectEndTime(double endTime)
    {
        _serverEffectEndTime = endTime;
        if (IsSpawned) _effectEndTime.Value = endTime;
    }

    // 身体能力の強化を、効果が続いている間だけ掛ける(サーバー、またはオフラインのみ)
    private void UpdateBoost()
    {
        if (IsSpawned && !IsServer) return;
        if (_stats == null) return;

        bool shouldBoost = IsEffectActive(CSE_PlayerSpecialEffect.StatusBoost);
        if (shouldBoost == _isBoosting) return;

        _isBoosting = shouldBoost;
        _stats.SetSpecialBoostRate(shouldBoost ? currentCharacter.boostRate : 1f);
    }

    private void HandleCharacterIndexChanged(int previous, int current)
    {
        onCharacterChanged?.Invoke(currentCharacter);
    }

    // 今の時刻(スポーン中はサーバーの時刻、オフラインではTime.timeAsDouble)
    private double GetNow()
    {
        return IsSpawned ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
    }
}
