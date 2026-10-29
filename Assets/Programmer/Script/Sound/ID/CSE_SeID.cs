/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-22 | 初回作成
 * ================================================ */

/// <summary>
/// SEを指定するためのID。
/// 数値帯によってInspector上のカテゴリを分ける。
/// </summary>
public enum CSE_SeId
{
    None = 0,

    // =========================================================
    // システム：100番台
    // =========================================================

    // UI操作
    SystemUIDecide = 100, // UIの決定音
    SystemUICancel,        // UIのキャンセル音
    SystemUICursorMove,    // UIのカーソル移動音

    // ゲーム進行
    SystemGameStart,       // ゲーム開始時の通知音
    SystemGameEnd,         // ゲーム終了時の通知音

    // =========================================================
    // プレイヤー：200番台
    // =========================================================

    // 移動
    PlayerFootstep = 200, // 足音
    PlayerJump,           // ジャンプ
    PlayerLanding,        // 着地
    PlayerDash,           // ダッシュ
    PlayerBlink,          // ブリンク回避

    // 通常攻撃
    PlayerNormalAttack1,  // 通常攻撃1段目
    PlayerNormalAttack2,  // 通常攻撃2段目
    PlayerNormalAttack3,  // 通常攻撃3段目
    PlayerNormalAttack4,  // 通常攻撃4段目
    PlayerNormalAttack5,  // 通常攻撃5段目
    PlayerAttackHit,      // 攻撃命中

    // 特殊攻撃・アクション
    PlayerSurpriseAttack,   // 奇襲
    PlayerParrySuccess,     // パリィ成功
    PlayerSpecialAttack,    // 必殺技
    PlayerSpecialGaugeFull, // 必殺技ゲージ満タン
    PlayerScan,              // スキャン

    // ハイド
    PlayerHideLeap,   // ハイドポイントへ飛びつき
    PlayerHideAttach, // ハイドポイントへ張り付き

    // アイテム
    PlayerItemPickup, // アイテム取得
    PlayerItemUse,    // アイテム使用

    // ダメージ・状態変化
    PlayerDamage,   // 被弾
    PlayerStunned,  // スタン
    PlayerStatusUp, // ステータスアップ

    // 暗躍ボーナス
    PlayerLimitBreakBonus, // 暗躍ボーナス付与・上昇

    // 出現・死亡
    PlayerSpawn,   // 初回スポーン
    PlayerDeath,   // 死亡
    PlayerRespawn, // リスポーン

    // =========================================================
    // 警察：300番台
    // =========================================================

    // 移動
    PolicePatrolFootstep = 300, // 巡回中の歩行音
    PoliceChaseFootstep,       // 追跡中の走行音

    // 発見・警戒
    PoliceSpotted,             // プレイヤーを発見した時の音
    PoliceAlert,               // 警戒状態へ移行した時の音

    // 戦闘
    PoliceAttackWarning,       // 攻撃前の溜め・予告
    PoliceAttack,              // 攻撃

    // 状態変化
    PoliceParriedStun,         // プレイヤーにパリィされ、スタンした時の音

    // 視界表示
    PoliceVisionVisualized,    // 警察の視界を見える状態にした時の音

    // =========================================================
    // 防犯・監視装置：400番台
    // =========================================================

    // 防犯レーザー
    SecurityLaserElectric = 400, // 防犯レーザー稼働中の電気音
    SecurityLaserContact,        // 防犯レーザーに接触した時の音

    // ストリートライト
    StreetLightDetected,         // ストリートライトに照らされた時の音

    // 巡回ドローン
    PatrolDronePatrol,           // 通常巡回
    PatrolDroneChase,            // 追跡中
    PatrolDroneSpotted,          // プレイヤー発見
    PatrolDroneTaserAttack,      // テーザー攻撃
    PatrolDroneBeamAttack,       // ビーム攻撃

    // =========================================================
    // 悪人：500番台
    // =========================================================

    // 出現
    VillainSpawn = 500,        // スポーン

    // 移動
    VillainPatrolMove,         // 巡回中の移動
    VillainAdaptiveMove,       // 状況に応じた臨機移動

    // 犯罪
    VillainCrimeStart,         // 犯罪を実行する時
    VillainCrimeComplete,      // 犯罪を完遂した時

    // 戦闘
    VillainAttackWarning,      // 攻撃前の溜め・予告
    VillainAttack,             // 攻撃
    VillainDamageKnockback,    // 被弾・ノックバック

    // 逃走・撃破
    VillainEscape,             // 逃走を開始・実行する時
    VillainDefeat,             // 撃破された時

    // =========================================================
    // レイドボス：600番台
    // =========================================================

    // 登場
    RaidBossSpawn = 600,  // レイドボス登場

    // 移動
    RaidBossMove,         // レイドボス移動

    // 攻撃
    RaidBossAttack1,      // 攻撃1
    RaidBossAttack2,      // 攻撃2
    RaidBossAttack3,      // 攻撃3

    // ダメージ
    RaidBossDamageVoice,  // 被弾時のボイス

    // =========================================================
    // アイテム：700番台
    // =========================================================

    ItemBomb = 700,       // 爆弾を使用・爆発させた時の音
    ItemHeal,             // 回復アイテムを使用した時の音
    ItemSmokeBomb,        // 煙幕を使用・発生させた時の音
    ItemDecoyHologram,    // 陽動ホログラムを使用・展開した時の音

    // =========================================================
    // イベント・通知：800番台
    // =========================================================

    // イベント
    EventCarePackage = 800, // ケアパッケージ出現・到着時
    EventAnnouncement,     // ランダムイベント発生の告知

    // ゲーム通知
    EventKillLog,          // キルログが表示された時
    EventTimeAnnouncement, // 残り時間などの時間告知
}
