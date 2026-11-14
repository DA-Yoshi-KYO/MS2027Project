# 📋 概要
SA(ステルスアクション)で飛んで行ける場所(SAポイント)の作り方・置き方です。
SAポイントの共通部分は `CS_SAPoint`(基底クラス)にあり、種類ごとにこれを継承して作ります。

| 種類 | クラス | 内容 |
|:--|:--|:--|
| ハイドポイント | `CS_HidePoint` | 物陰・高所など。飛んで来た後、隠れられる箱(HideBox)の中にいる間は隠れている扱い |
| 信号機 | `CS_TrafficLightSAPoint` | 信号機の上など。飛ぶだけ(隠れている扱いにはならない) |

# ✍️ 置き方(共通)
プレハブをフィールドに置くだけです。置いた物の子に、次の空オブジェクトがあります。

| 子オブジェクト | 役割 |
|:--|:--|
| `StandPoint` | 着く位置と向き。着いた時の足元の位置(高さ込み)に置く |
| `SAReachBox` | SAできる範囲の箱。プレイヤーの足元がこの箱の中にある時だけSAでき、強調UIも出る。位置・回転・Scale(x: 幅, y: 高さ, z: 奥行 m)をそのまま箱にする |
| `HideBox`(ハイドポイントのみ) | 隠れられる箱 |

* SAできる範囲(SAReachBox)は、置いた物ごとに調整できます。シーンビューでSAポイントか子を選ぶと、箱が黄色の枠で見えます。
* 強調UIは、既存のハイドマーカーUI(`CS_PlayerHideMarkerUI`)をそのまま使います。種類ごとにUIは足しません。

# 🚦 信号機のプレハブ
`Assets/Programmer/Prefab/Field/FieldElement/TrafficLight/TrafficLight.prefab`
Cubeで作った仮の見た目です(柱 + 頭)。モデルができたら、`Pole`・`Head` を差し替えてください(`StandPoint` は頭の上、高さ8m)。

# 🆕 新しい種類を足す時
1. `CS_SAPoint` を継承したクラスを作る(`CS_TrafficLightSAPoint` が最小の例)。
2. 隠れられる箱を持たせたい時だけ、`hasHideBox`・`Contains`・`GetHideBoxPoint` を上書きする(`CS_HidePoint` を参照)。
3. プレハブを作り、`StandPoint` と `SAReachBox` を子に置いて、スクリプトに割り当てる。
`CS_PlayerHide`・ハイドマーカーUIは、`CS_SAPoint` の一覧を見ているため、変更は要りません。

# 🧪 テストシーン
`Assets/Programmer/Scene/Test/ObjectTest/SATest.unity`
プレイヤー・床・ビル代わりの箱・信号機3つ・ハイドポイント1つ・UICanvas を置いています(ネットワークなしで動かす想定)。
