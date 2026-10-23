# NPC仕様(4人に満たない枠を埋める、人の代わりのプレイヤー)

このゲームは4人で遊ぶ前提なので、人が4人に満たない時(ソロなら3体、3人なら1体)は空き枠をNPCで埋めます。
NPCを調整・拡張する担当者(とそのClaude)向けの資料です。

- 仕様書: [Confluence「NPC(他プレイヤー)」](https://summervacationgamejam.atlassian.net/wiki/spaces/20272/pages/32145409) / [データ表「NPC(他プレイヤー)思考／行動指針データ」](https://summervacationgamejam.atlassian.net/wiki/spaces/20272/pages/21004355)
- Issue: #162(まとめ) / #164(空き枠にNPCを生成) / #165(判断の仕組み) / #166(3種類の性格) / #167(スコアに応じた強さの調整。クローズ済みのため未実装)
- 方針(Issueより): LLMや機械学習は使わず、ルールベースのゲームAI(ユーティリティAI)で作る

## 全体の仕組み

```
CS_PlayerSpawner(サーバー) ── 空き枠に、人と同じ Player.prefab を生成し MarkAsNpc() + 性格を渡す
        │
        ▼
CS_PlayerNpcBrain(サーバーだけで動く) ── 状況を見て行動を選び、ボタン・スティックを操作する
        │ 書き込む
        ▼
CS_PlayerNpcInput(移動・向き・各ボタン) ── CS_Player と各部品が、人の入力と同じように読む
        │
        ▼
CS_Player / CS_PlayerAttack / CS_PlayerTransformation / ...(人と全く同じ部品・数値・ルール)
```

- **NPCはプレイヤーと同じ部品をそのまま使う**(移動・攻撃・必殺技・アイテム・インタラクト・スコア・HUD・リザルト)。判断側は「ボタンを押す・スティックを倒す」だけを行う
- 基本パラメーター(HP・移動速度・攻撃力など)はプレイヤーと同じ(仕様書「基本パラメーターはプレイヤーと同様」)。性格で変えるのは、敵の位置の把握(索敵範囲)と頭の良さ(判断の間隔・反応の遅れ・行動の選び方)だけ
- NPCはサーバー(ホスト)が所有・操作する。他のクライアントからは他人のプレイヤーと同じに見える(位置は`NetworkTransform`で同期)

## ファイル

| ファイル | 役割 |
|---|---|
| [`CS_PlayerButton.cs`](../Assets/Programmer/Script/Entity/Player/CS_PlayerButton.cs) | ボタン1つ分。人の時は`InputAction`を読み、NPCの時は判断が押す・離す。各部品は`WasPressedThisFrame()` / `IsPressed()`で読むだけ |
| [`CS_PlayerNpcInput.cs`](../Assets/Programmer/Script/Entity/Player/CS_PlayerNpcInput.cs) | NPCの入力(移動・向き・各ボタン) |
| [`Npc/CS_PlayerNpcBrain.cs`](../Assets/Programmer/Script/Entity/Player/Npc/CS_PlayerNpcBrain.cs) | 判断。行動ごとに点数を付けて一番高いものを選び、入力として書き込む |
| [`Npc/CSO_NpcPersonality.cs`](../Assets/Programmer/Script/Entity/Player/Npc/CSO_NpcPersonality.cs) | 性格(強さ)のデータ(ScriptableObject) |
| [`Npc/CS_NpcNavigator.cs`](../Assets/Programmer/Script/Entity/Player/Npc/CS_NpcNavigator.cs) | NavMeshで経路を求め、次に進む方向を返す |
| [`Npc/CS_NpcWorld.cs`](../Assets/Programmer/Script/Entity/Player/Npc/CS_NpcWorld.cs) | 悪人・警察・アイテムの一覧(全NPCで共有。1秒に1回だけ取り直す) |
| `Assets/Programmer/Database/Npc/DB_NpcPersonality_Lv1〜3.asset` | 3種類の性格のデータ |

## 生成: `CS_PlayerSpawner`(`Field_Multi.prefab` / `Field_Solo1.prefab`)

- ゲーム開始時(シーンの`Start`)に、接続済みの人の分を生成した後、空いている番号(0〜3)をNPCで埋める
- 後から人が接続して枠が足りない時は、NPCを1体(番号の大きいもの)消して、その番号を人に譲る
- 人が切断して枠が空いたら、またNPCで埋める
- 性格はInspectorの`Npc Personalities`の順に割り当てる(1体目は[0]、2体目は[1]…)。現在は **Lv.3 → Lv.2 → Lv.1** の順(3人で遊ぶ時に入るNPCはLv.3のライバル)
- プレイヤー番号は空いている一番小さい番号。生成位置は番号と同じ番号のスポーン地点(仕様「決められたステージ隅の4地点」)
- オフラインのテストシーンでは、`Fill With Npc Offline`をオンにした時だけNPCを入れる(既定はオフ)
- `Fill With Npc`をオフにすると、NPCを入れない

## 判断: `CS_PlayerNpcBrain`

`Decision Interval`秒ごとに、次の行動の点数を出して一番高いものを選ぶ(点数 = 性格の重み × 近さ)。重みを0にした行動はしなくなる。

| 行動 | 内容 | 主な数値 |
|---|---|---|
| 悪人を倒す | 近づき、届く距離なら攻撃。範囲内に`Special Min Targets`人以上いれば必殺技 | `Villain Weight` |
| アイテムを拾う | 触れて拾う(スロットが埋まっていれば携帯型は狙わない。HPが低い時は回復などの即時型を優先) | `Item Weight` |
| イベントに参加 | ケアパッケージなどに近づき、通常攻撃ボタン(=インタラクト)を押し続ける | `Event Weight` |
| 他のプレイヤーを邪魔する | `Harass Range`以内のプレイヤーに近づいて攻撃・アイテム・必殺技(人のプレイヤーを優先) | `Harass Weight` |
| 警察から逃げる | 追跡中の警察が`Flee Range`以内にいたら、反対側へダッシュで逃げる | `Flee Weight` |
| うろつく | 何も無い時に、近くのランダムな場所へ歩く | `Wander Weight` |

- 行動を変えた時は`Reaction Delay`秒待ってから動き出す(反応の遅さ)
- 今の標的は点数を少し上乗せし(×1.2)、似た点数の標的の間で行ったり来たりしないようにしている
- HPが`Flee Hp Ratio`以下の時は、逃げる点数が2倍・悪人を狙う点数が半分になる
- プレイヤーは常に変身している(仕様変更、Issue #277)ため、変身・解除の判断は行わない(以前の`Transform Distance` / `Untransform Idle Time` / `Max Wanted Level`は削除した)
- アイテムは、逃げている時・標的が`Item Use Distance`以内の時に、`Item Use Chance`の確率で使う(爆弾は一番近い悪人・プレイヤーへ自動で飛ぶ)
- `Skips Late Crimes`がオンだと、向かっても犯罪の完遂に間に合わない悪人には向かわない(データ表 Lv.3)。見積もりは「距離 ÷ 移動速度」と「残り時間(悪人グループの`crimeProgress`から計算)」の比較
- 引っかかって進めない時はジャンプして経路を計算し直す。3回続いたら、その行動をあきらめて別の場所へうろつく

## 性格: `CSO_NpcPersonality`(データ表 Lv.1〜3)

データ表に具体的な数値がまだ無いため、**すべて仮の値**(遊んで要調整)。

| 項目 | Lv.3 ライバル | Lv.2 ぼーがい | Lv.1 雑魚 | データ表の記述 |
|---|---|---|---|---|
| 索敵範囲(m) | 36 | 30 | 24 | ミニマップ(UI仕様で約30m)より少し大きい / 同じ / 少し小さい |
| 判断の間隔(秒) | 0.3 | 0.5 | 1.0 | 頭の良さ |
| 反応の遅れ(秒) | 0.1 | 0.25 | 0.6 | 頭の良さ |
| 悪人 / アイテム / イベント / 邪魔 | 1 / 0.4 / 0.9 / 0.15 | 0.6 / 0.8 / 0.5 / 1.2 | 1 / 0 / 0.3 / 0 | Lv.3: 着実に1位・イベントにも積極的・有利なら邪魔 / Lv.2: 妨害優先 / Lv.1: アイテムは拾いに行かない |
| 間に合わない悪人を避ける | する | しない | しない | Lv.3「間に合わないであろう悪人には向かいません」 |
| 攻撃の間隔(秒) / 向きがずれる確率 | 0.3 / 0% | 0.4 / 10% | 0.7 / 40% | Lv.1「悪人にすら普通に時々負ける」 |
| 必殺技 | 2人以上の悪人に | プレイヤーのために取っておく | 2人以上の悪人に | Lv.2「必殺技をプレイヤーの為に取って置いて放ちます」 |
| アイテムを使う確率 | 60% | 70%(プレイヤーにも使う) | 5% | Lv.1「拾っても全然使わない」 |
| ダッシュで移動 | する | する | しない | |

## NPCを入れたことで変えたプレイヤー側のルール

- **「自分の画面のプレイヤーか」は`IsOwner`ではなく`CS_Player.isLocalPlayer`で判定する**。NPCはサーバーが所有するため、ホストではNPCも`IsOwner`がtrueになる(HP・必殺技ゲージ・アイテムスロットのUI、ミニマップの中心、頭上順位、カメラの揺れで対応済み)
- `CS_Player.isNpc`でNPCかどうかが全クライアントで分かる(リザルトの表示名などに使う想定。表示名は性格の`Display Name`)
- ボタン(`attackAction`など)の型は`InputAction`から`CS_PlayerButton`に変わった(メソッド名は同じなので、読む側のコードはそのまま)

## 未実装・今後の課題

- **スコアに応じた強さの調整(#167)**: クローズ済みのため入れていない。ライバルの「プレイヤーと同等のスコアを維持」、雑魚の「プレイヤーを最下位にさせない」は、今は性格の固定の数値だけで表している
- **リザルトでのNPCの表示**: `isNpc`と性格の`Display Name`はあるが、リザルト側(`CS_ResultData`)への受け渡しは未対応
- **警察から逃げる判定**: 警察が「誰を」追っているかは外から分からないため、「追跡中の警察が近くにいる」ことで判断している(他人を追っている警察からも逃げることがある)
- **ブリンクのオートカウンター(パリー)**: プレイヤー側に実装済み(`CS_PlayerParry`)。NPCは攻撃に合わせてブリンクボタンを押す判断はしていない(移動のダッシュがたまたま合えば成功する)
- **動作確認**: コンパイルは確認済み。プレイしての確認(特にNavMesh上の移動、ケアパッケージの開封)はまだ
