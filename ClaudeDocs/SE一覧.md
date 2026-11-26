# SE一覧(鳴るシチュエーション / DBへの登録名 / 今使用されているwav)

- 内容はPR #562(`吉田京志郎/SEの整理`)のマージ後の状態
- DBへの登録名 = `CSE_SeId` の名前(`DB_SeDataBase.asset` の `_id` の数値)
- wav = DBの `_clips` に入っている素材の名前
  - ブランチ上のDB(33b18f27時点)の `_clips` のguidと、wavの .meta のguidを照合した結果です
  - 照合済み: 100〜104、200〜203(すべて一致)。それ以外は、`Audio/SE` のwavが68個で、DB登録の68件と名前・数が1対1で合うことまでの確認で、guidの照合は未実施
  - wavの名前は、DBの登録名と同じ(例外: 200 は `PlayerFootStep`)
- 削除したもの: `PoliceVisionVisualized`(307)とレイドボス `RaidBoss*`(600〜605)。IDも素材も消去済み。他のIDの数字は変えていない

## システム(100番台)
| 鳴るシチュエーション | DBへの登録名 | 今使用されているwav |
|:--|:--|:--|
| タイトルメニューの決定・クリック、部屋接続画面のホスト/参加/ソロ、待機画面の開始 | SystemUIDecide (100) | SystemUIDecide |
| 待機画面の「戻る」 | SystemUICancel (101) | SystemUICancel |
| タイトルメニューで選択が変わった時 | SystemUICursorMove (102) | SystemUICursorMove |
| 待機画面でホストが開始を押した時 | SystemGameStart (103) | SystemGameStart |
| 時間切れ、ソロの終了時(各端末) | SystemGameEnd (104) | SystemGameEnd |

## プレイヤー(200番台)
| 鳴るシチュエーション | DBへの登録名 | 今使用されているwav |
|:--|:--|:--|
| 歩く・走る(約2.2m進むごと) | PlayerFootstep (200) | PlayerFootStep |
| ジャンプした時 | PlayerJump (201) | PlayerJump |
| 0.25秒以上空中にいて着地した時 | PlayerLanding (202) | PlayerLanding |
| スプリントを始めた時 | PlayerDash (203) | PlayerDash |
| Shiftを押した瞬間(ブリンク回避) | PlayerBlink (204) | PlayerBlink |
| 通常攻撃の1段目 | PlayerNormalAttack1 (205) | PlayerNormalAttack1 |
| 通常攻撃の2段目 | PlayerNormalAttack2 (206) | PlayerNormalAttack2 |
| 通常攻撃の3段目 | PlayerNormalAttack3 (207) | PlayerNormalAttack3 |
| 通常攻撃の4段目 | PlayerNormalAttack4 (208) | PlayerNormalAttack4 |
| 通常攻撃の5段目 | PlayerNormalAttack5 (209) | PlayerNormalAttack5 |
| 攻撃が相手に当たった時(位置で鳴る) | PlayerAttackHit (210) | PlayerAttackHit |
| 奇襲の当たりのタイミング | PlayerSurpriseAttack (211) | PlayerSurpriseAttack |
| パリィ成功 | PlayerParrySuccess (212) | PlayerParrySuccess |
| 必殺技の開始 | PlayerSpecialAttack (213) | PlayerSpecialAttack |
| 必殺技ゲージが満タンになった時(本人のみ) | PlayerSpecialGaugeFull (214) | PlayerSpecialGaugeFull |
| スキャンの開始 | PlayerScan (215) | PlayerScan |
| ハイドポイントへ飛びつき始めた時 | PlayerHideLeap (216) | PlayerHideLeap |
| 隠れた(張り付いた)時。228と同時に鳴る | PlayerHideAttach (217) | PlayerHideAttach |
| アイテムを持った時 | PlayerItemPickup (218) | PlayerItemPickup |
| アイテムを使った時 | PlayerItemUse (219) | PlayerItemUse |
| 調べる・操作を始めた時 | PlayerInteract (220) | PlayerInteract |
| 被弾してHPが減った時 | PlayerDamage (221) | PlayerDamage |
| スタンになった時 | PlayerStunned (222) | PlayerStunned |
| 能力が上がった時(1フレームに1回まで) | PlayerStatusUp (223) | PlayerStatusUp |
| 暗躍ボーナスの連続数が増えた時 | PlayerLimitBreakBonus (224) | PlayerLimitBreakBonus |
| 最初のスポーン(開始時) | PlayerSpawn (225) | PlayerSpawn |
| 死亡した時 | PlayerDeath (226) | PlayerDeath |
| 気絶から復活した時 | PlayerRespawn (227) | PlayerRespawn |
| 隠れて、ステルス状態に入った時 | PlayerStealthEnter (228) | PlayerStealthEnter |
| ステルス状態が解けた時 | PlayerStealthExit (229) | PlayerStealthExit |

## 警察(300番台)
| 鳴るシチュエーション | DBへの登録名 | 今使用されているwav |
|:--|:--|:--|
| 警察が歩いている時(約2.2m進むごと。4m/秒未満) | PolicePatrolFootstep (300) | PolicePatrolFootstep |
| 警察が走っている時(4m/秒以上。仮の値) | PoliceChaseFootstep (301) | PoliceChaseFootstep |
| 警察がプレイヤーを見つけて追跡を始めた時 | PoliceSpotted (302) | PoliceSpotted |
| 自分が警戒状態に入った時(本人のみ) | PoliceAlert (303) | PoliceAlert |
| 警察の攻撃の溜め始め | PoliceAttackWarning (304) | PoliceAttackWarning |
| 警察の攻撃が出た時 | PoliceAttack (305) | PoliceAttack |
| パリィされてスタンした時 | PoliceParriedStun (306) | PoliceParriedStun |

## 防犯・監視装置(400番台)
| 鳴るシチュエーション | DBへの登録名 | 今使用されているwav |
|:--|:--|:--|
| 防犯レーザーの稼働中(ループ音)。**未配線**(ループ再生の仕組みが無い) | SecurityLaserElectric (400) | SecurityLaserElectric |
| 防犯レーザーに触れた時 | SecurityLaserContact (401) | SecurityLaserContact |
| ストリートライトに照らされた時 | StreetLightDetected (402) | StreetLightDetected |
| ドローンが巡回の状態になった時 | PatrolDronePatrol (403) | PatrolDronePatrol |
| ドローンが追跡の状態になった時 | PatrolDroneChase (404) | PatrolDroneChase |
| ドローンがプレイヤーを発見した時 | PatrolDroneSpotted (405) | PatrolDroneSpotted |
| ドローンのテーザー攻撃 | PatrolDroneTaserAttack (406) | PatrolDroneTaserAttack |
| ドローンのビーム攻撃 | PatrolDroneBeamAttack (407) | PatrolDroneBeamAttack |

## 悪人(500番台)
| 鳴るシチュエーション | DBへの登録名 | 今使用されているwav |
|:--|:--|:--|
| 悪人が出現した時 | VillainSpawn (500) | VillainSpawn |
| 悪人が歩いている時(約2.2m進むごと) | VillainPatrolMove (501) | VillainPatrolMove |
| 悪人が走っている時(4m/秒以上。仮の値) | VillainAdaptiveMove (502) | VillainAdaptiveMove |
| 犯罪の動作を始めた時 | VillainCrimeStart (503) | VillainCrimeStart |
| グループが犯罪を完遂した時 | VillainCrimeComplete (504) | VillainCrimeComplete |
| 悪人の攻撃の溜め始め | VillainAttackWarning (505) | VillainAttackWarning |
| 悪人の攻撃が出た時 | VillainAttack (506) | VillainAttack |
| 悪人がノックバックした時 | VillainDamageKnockback (507) | VillainDamageKnockback |
| 悪人が逃走を始めた時 | VillainEscape (508) | VillainEscape |
| 悪人が撃破された時 | VillainDefeat (509) | VillainDefeat |

## アイテム(700番台)
| 鳴るシチュエーション | DBへの登録名 | 今使用されているwav |
|:--|:--|:--|
| 投げた爆弾が爆発した時 | ItemBomb (700) | ItemBomb |
| 回復アイテムの効果が出た時 | ItemHeal (701) | ItemHeal |
| 煙幕が張られた時 | ItemSmokeBomb (702) | ItemSmokeBomb |
| 陽動ホログラムを投げた時 | ItemDecoyHologram (703) | ItemDecoyHologram |

## イベント・通知(800番台)
| 鳴るシチュエーション | DBへの登録名 | 今使用されているwav |
|:--|:--|:--|
| ケアパッケージが出現した時 | EventCarePackage (800) | EventCarePackage |
| ランダムイベントの告知が出た時 | EventAnnouncement (801) | EventAnnouncement |
| キルログが出た時 | EventKillLog (802) | EventKillLog |
| 残り時間の告知が出た時 | EventTimeAnnouncement (803) | EventTimeAnnouncement |

## 鳴らしていないもの
- SecurityLaserElectric(400): ループ再生の仕組みが無いため
- ジングル(`WinJingle` など)、GameClear/GameOver のBGM: 呼び出し元が無く、DBにも登録が足りない
