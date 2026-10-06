# NPC仕様(実装済み機能の一覧)

人のプレイヤーが4人に満たない時に、空き枠を埋めるNPCの実装内容です。
NPCのタスクは出たばかりで**仕様が変わる可能性が高い**ため、変更しやすい作りにしています(下の「仕様が変わったときの直し方」を参照)。

- 関連Issue: #162(まとめ) / #164(空き枠にNPCを生成) / #165(判断の仕組み) / #166(3種類の性格) / #167(スコアに応じた強さの調整)
- 仕様書: [NPC(他プレイヤー)](https://summervacationgamejam.atlassian.net/wiki/spaces/20272/pages/32145409)
- 方針: LLMや機械学習は使わず、ルールベースのゲームAI(ユーティリティAI)

## 仕組み

NPCは**人のプレイヤーと同じプレハブ・同じ処理**で動きます。違うのは「入力の出どころ」だけです。

```
人  : キーボード/コントローラー → CS_PlayerInputActions ─┐
                                                          ├→ IPlayerInputSource → CS_Player(移動)・攻撃・必殺技・変身・アイテム
NPC : 状況を見て行動を選ぶ    → CS_NpcBrain           ─┘
```

- `CS_Player`と各操作のコンポーネントは、ボタンを直接読まずに`CS_Player.input`(`IPlayerInputSource`)を読む
- そのため、NPCも人と同じ能力(移動・攻撃・変身・必殺技・アイテム)・同じ制限(クールタイム・変身中しか攻撃できない等)で動く
- 移動: NPCは「次に向かう方向」へ視点(yaw)を向けて前進の入力を出す。経路はNavMeshで求める(NavMeshAgentは使わない)

## ファイル構成

| ファイル | 役割 | Issue |
|---|---|---|
| `Entity/Player/CS_IPlayerInputSource.cs` | 入力の出どころのインターフェース | #165 |
| `Entity/Player/CS_PlayerInputActions.cs` | 人の入力(キーボード・コントローラー) | #165 |
| `Entity/Npc/CS_NpcSpawner.cs` | 空き枠(プレイヤー番号)にNPCを生成する | #164 |
| `Entity/Npc/CS_NpcBrain.cs` | 判断の中心。行動を選び、入力として渡す | #165 |
| `Entity/Npc/CS_NpcAction.cs` | 行動の基底クラス | #165 |
| `Entity/Npc/CS_NpcAction〇〇.cs` | 行動1つずつ(下の表) | #165 |
| `Entity/Npc/CS_NpcSensor.cs` | 周り(悪人・アイテム・警察・プレイヤー)の把握 | #165 |
| `Entity/Npc/CS_NpcNavigator.cs` | NavMeshでの経路探索 | #165 |
| `Entity/Npc/CSO_NpcPersonality.cs` | 性格ごとのデータ(ScriptableObject) | #166 |
| `Entity/Npc/CS_NpcDifficulty.cs` | スコア差を見て強さを上げ下げする | #167 |
| `Database/Npc/DB_NpcRival / DB_NpcWeak / DB_NpcHarasser.asset` | 3種類の性格のデータ(数値は仮) | #166 |

(パスは`Assets/Programmer/Script/`、`Assets/Programmer/`からの相対)

## 生成: `CS_NpcSpawner`(#164)

- サーバー(またはオフライン)だけが生成する。NPCはサーバーが所有し、サーバーが動かす。クライアントは位置の同期を受けるだけ
- 人のプレイヤー(`CS_PlayerSpawner`が生成)を`Start Delay`秒待ってから、使われていないプレイヤー番号(0〜3)にNPCを生成する
- **どの枠にどの性格を入れるか**は`Slot Personalities`(要素番号 = プレイヤー番号)で決める。空の枠には入れない
- NPCにもプレイヤー番号を割り当てるので、HP UI・スコア・リザルトでは人と同じように扱われる
- **NPCかどうかは`CS_Player.isNpc`**、性格は`CS_NpcBrain.personality.displayName`で判別できる
- 後から人が接続した場合: その人に割り当てられた番号と同じ番号のNPCを消して、枠を譲る
- 生成位置は`Spawn Points`の子を番号順に使う(`CS_PlayerSpawner`のオブジェクトを指定すれば、人と同じ位置を使える)

### シーンへの設定(MainScene・テストシーンそれぞれに必要)

1. 空のGameObjectを作り、`CS_NpcSpawner`を付ける
2. `Player Prefab`: 人と同じ`Player.prefab`
3. `Spawn Points`: `CS_PlayerSpawner`が付いているオブジェクト
4. `Slot Personalities`: 4要素。例えばソロなら0番は人が使うので、1〜3番に`DB_NpcRival` / `DB_NpcWeak` / `DB_NpcHarasser`
5. シーンにNavMeshがベイクされていること(悪人・警察と同じもの)

## 判断: `CS_NpcBrain`(#165)

- 一定間隔(判断の間隔。性格と強さで変わる)で、全行動の「やる価値」を比べて一番高い行動を選ぶ(ユーティリティAI)
  - 今の行動は少しだけ価値を上乗せして、行動がころころ変わらないようにしている
- 選んだ行動は毎フレーム、移動先・向き・ボタンを指示する
- **反応の遅さ**: 行動を切り替えてから`Reaction Delay`秒はボタンを押さない
- **迷い**: 判断のたびに、強さに応じた確率でその間立ち止まる
- アイテムは、持っていて近くに悪人がいれば、どの行動中でも使う
- Inspectorの`Debug Action`で、今選んでいる行動を確認できる(実行中のNPCを選択する)

### 行動の一覧

| 行動 | クラス | やる価値が高くなる時 | 中身 |
|---|---|---|---|
| 悪人を倒す | `CS_NpcActionHuntVillain` | 近くに悪人がいる | 近づきながら変身(警察が近い間は変身しない)→攻撃。範囲に悪人が多ければ必殺技 |
| アイテムを拾う | `CS_NpcActionSeekItem` | スロットが空で、近くにアイテムがある | アイテムまで移動する(触れると自動で拾う) |
| 妨害する | `CS_NpcActionHarass` | 近くに他のプレイヤーがいる | 一番近い他のプレイヤーの周りを回る |
| 警察から逃げる | `CS_NpcActionFleePolice` | 変身中で、近くに警察がいる | 変身を解いて、警察と反対へ離れる |
| うろつく | `CS_NpcActionWander` | (常に一定。他にやることが無い時) | 近くのランダムな点へ歩く |

## 性格: `CSO_NpcPersonality`(#166)

右クリック → Create → Npc → Npc Personality で作れる。基本パラメーター(HP・移動速度など)はプレイヤーと同じで、ここでは「頭の良さ」と「行動の好み」だけを持つ。

| 項目 | 内容 |
|---|---|
| Display Name | 表示名 |
| Reaction Delay | 反応の遅さ(秒) |
| 〇〇 Sight Range | 悪人・アイテム・他プレイヤー・警察の位置が分かる範囲(m)。今は距離だけで判定(壁越しでも分かる) |
| 〇〇 Weight | 行動の選びやすさ(0にするとその行動をしない) |
| Attack Range / Police Avoid Range / Special 〇〇 | 戦闘の判断に使う距離など |
| Difficulty Mode 以下 | 強さの調整(下の#167) |
| Think Interval など(x, y) | 強さの段階ごとの値。x = 一番弱い段階、y = 一番強い段階。その間は補間する |

### 3種類の性格(数値は仮)

| | ライバル(`DB_NpcRival`) | 雑魚(`DB_NpcWeak`) | 妨害(`DB_NpcHarasser`) |
|---|---|---|---|
| 狙い | 人と同じくらいのスコアを保つ | 人を絶対に最下位にさせない | スコアを狙わず邪魔する |
| 悪人を倒す | 1.2 | 0.8 | 0(しない) |
| アイテム | 0.4 | 0.3 | 1.0 |
| 妨害 | 0 | 0 | 1.0 |
| 見える範囲(悪人) | 35m | 15m | 10m |
| 反応の遅さ | 0.15秒 | 0.5秒 | 0.3秒 |
| 強さの調整 | Rival | Weak | None |

## 強さの調整: `CS_NpcDifficulty`(#167)

- `Adjust Interval`秒ごとに、自分と人のプレイヤー(NPCを除く)のスコアを比べて、強さの段階(0〜`Level Count`-1)を上げ下げする
  - **ライバル**: 人の中で一番高いスコアと比べ、`Score Threshold`以上負けていたら1段階強く、勝っていたら1段階弱く
  - **雑魚**: 人の中で一番低いスコアから`Weak Score Margin`以上下を保つ。上回りそうな間は悪人を狙わず、一番弱い段階にする
  - **調整なし**: 何もしない
- 強さで変わるもの: 判断の間隔、攻撃ボタンを押す間隔、迷って立ち止まる確率、悪人を狙う積極性
- 人のプレイヤーがいない時(NPCだけのテストなど)は調整しない

## ネットワーク

- 頭脳(`CS_NpcBrain`)はサーバーにだけ付く。NPCの操作(攻撃の判定など)はすべてサーバーで行われる
- NPCはサーバーが所有するので、`IsOwner`だけで「自分のプレイヤー」を判定するとホストでNPCが自分扱いになる
  - ミニマップの中心(`CS_PlayerMiniMapIcon`)は`IsOwner && !isNpc`で判定するよう修正済み
  - 攻撃時のカメラの揺れ(`CS_PlayerCameraShake`)はNPCでは起こさない(ホストの画面が揺れるため)
  - 新しく「自分のプレイヤーだけ」の処理を作るときは、`CS_Player.isLocalHuman`を使う

## 仕様が変わったときの直し方

| 変えたいこと | 直す場所 |
|---|---|
| 数値(性格・強さ) | `Database/Npc/DB_Npc〇〇.asset`をInspectorで直す(コード不要) |
| 性格を増やす | 性格のアセットを新しく作り、`CS_NpcSpawner`の`Slot Personalities`に入れる |
| 行動を増やす・変える | `CS_NpcAction`を継承したクラスを作り、`CS_NpcBrain.CreateActions()`の一覧に加える(他の行動には影響しない) |
| 行動を選ぶ重みを増やす | `CSO_NpcPersonality`に重みを足し、その行動の`Evaluate`で掛ける |
| 強さの比べ方(「上手さ」をスコア以外でも見る等) | `CS_NpcDifficulty.Evaluate` |
| 見え方(壁越しは見えない等) | `CS_NpcSensor` |

## 未実装・確認事項

- **データ表にNPCのデータが無い**(#166) → プランナーに確認。今の数値は仮
- **「上手さ」をスコア以外に何で見るか**(#167: 被弾数・撃破ペースなど) → プランナーに確認。今はスコアだけ
- **手配度**(#158)が未実装なので、変身・解除の判断は「警察が近いか」だけで行っている。手配度ができたら判断に加える
- 妨害は「他のプレイヤーの周りを回る」だけ(攻撃や妨害アイテムの使用は未実装)
- ジャンプ・ダッシュは使わない(段差や障害物で詰まった時の対処も未実装)
- リザルトでNPCを判別する表示(名前など)は、リザルト側(`CS_ResultData`)に項目が無いため未対応
