# 警戒 UI の使い方（`CS_PoliceDetectionController`）

警察が自分に気付いているかを、画面中央の見えない楕円の上に「目アイコン」で知らせる UI です。
仕様の元：Confluence「UI」⑨警察に見られているか判定するUI

UI 側（Model / Presenter / View / Controller）は元浪さん、警察側からのつなぎ込み（`CS_PoliceDetectionSource`）は宇留野さん、プレイヤー側の登録（`CS_Player`）は秋野さんが実装済みです。

```
警察(サーバー) ──NetworkVariable──▶ 警察(各クライアント) ──RegisterPolice など──▶ Controller ──▶ Model ──▶ Presenter(毎フレーム方向計算) ──▶ View(目アイコン)
                                                                                         ▲
                                                     プレイヤー(ローカル) ──SetPlayerTransform─┘
```

---

## 仕様（表示ルール）

状態は **自分（ローカルプレイヤー）から見て** 3段階です。

| 状態 | 警察の状態（自分から見て） | 表示 |
|---|---|---|
| `Safe` | 巡回・待機中（自分に気付いていない） | 表示しない |
| `Tracked` | 巡回時の視界（20m）に入って怪しまれている ※ / 自分の信号を受けて駆け付け中 / 探索中（自分を見失った後・自分の信号の現場に着いた後） | 黄（5m以内なら赤） |
| `Spotted` | 自分を追跡中（発見視界（10m）で見つかった。仲間から共有された標的として追っている場合も含む） | 赤 ＋ 専用アニメーション |

※ 怪しまれている状態（警察の `Suspect`）は、まだ警察側から知らせていません（下の「警察側」を参照）。

- `Tracked` の色は **距離** で決まる（View の `_dangerDistance`、既定5m）。`Spotted` は距離に関係なく赤
- `Spotted` のアニメーションは、仕様の「UIアニメーション」がデザイン待ちのため未実装（View の `ApplySpottedAnimation` が TODO）
- 方向は **プレイヤーの向き基準**（正面＝楕円の上、真後ろ＝楕円の下）。Presenter が毎フレーム計算するので、位置を渡し続ける必要はない
- 近くにいる警察（角度差15°以内・距離差5m以内）は1つのアイコンにまとめて「×N」と表示する（Presenter の Inspector で変更可）。まとめた中に `Spotted` が1人でもいれば `Spotted` で表示する

---

## 仕組み（UI 側）

| クラス | ファイル | 役割 |
|---|---|---|
| `CS_PoliceDetectionController` | `GameController/CS_PoliceDetectionController.cs` | **外から触るのはこれだけ**。Model を内部で1つ持ち、Presenter に渡す。GameControllerManager.prefab に付いている |
| `CS_UIPoliceDetectionModel` | `UI/UI_PoliceDetection/CS_UIPoliceDetectionModel.cs` | 登録された警察を状態ごと保持（`Safe` も保持する） |
| `CS_PoliceDetectionData` / `CSE_PlayerDetectionState` | `UI/UI_PoliceDetection/CS_PoliceDetectionData.cs` | 警察1人分のデータと状態 |
| `CS_UIPoliceDetectionPresenter` | `UI/UI_PoliceDetection/CS_UIPoliceDetectionPresenter.cs` | `Safe` 以外の警察について毎フレーム角度・距離を計算し、まとめて View へ |
| `CS_UIPoliceDetectionView` | `UI/UI_PoliceDetection/CS_UIPoliceDetectionView.cs` | 楕円上へ配置・色・×N・`Spotted` のアニメーション。UICanvas.prefab に付いている |

※ 他の UI Model（HP・手配度など）と違い、**Model を `new` して `Bind` する形ではありません**。Model は Controller の中にあり、外からは Controller のメソッド経由でしか触れません。

```csharp
public enum CSE_PlayerDetectionState
{
    Safe = 0,    // 追跡されていない。UI非表示
    Tracked = 1, // 追跡・探索対象。5mより遠ければ黄色、5m以内なら赤
    Spotted = 2, // 警察に直接視認されている。赤＋専用アニメーション
}
```

### Controller の API

| メソッド | 説明 |
|---|---|
| `SetPlayerTransform(Transform)` | 方向の基準にするプレイヤーを登録 |
| `RegisterPolice(string id, Transform 警察, 状態 = Safe)` | 警察を登録（同じ id なら差し替え） |
| `SetPoliceState(string id, 状態)` | 登録済みの警察の状態を変える。`Safe` にすると非表示になるだけで、登録は残る（`SetPoliceState` で表示に戻せる） |
| `UnregisterPolice(string id)` | 登録解除（警察が消える時は必ず呼ぶ。呼ばないとデータが残り続ける） |

- 未登録の id に `SetPoliceState` しても何も起きないので、最初は `RegisterPolice` で登録する
- id はミニマップと同じく `$"Police_{NetworkObjectId}"` を推奨

---

## 警察側（宇留野さん担当）

警察AI（`CS_PoliceBrain`）はサーバーでしか動かないので、**状態を NetworkVariable で同期 → 各クライアントで Controller を呼ぶ** 形になっています。

### `CS_PoliceDetectionSource`（警察プレハブに付いている）

次の2つを NetworkVariable で持ちます（書き込みはサーバーのみ。オフラインのテストシーンではローカル変数で持つ）。

| 変数 | 内容 |
|---|---|
| `_foundPlayerNumber` | 追跡中の相手のプレイヤー番号。いなければ -1 |
| `_signalPlayerNumber` | 駆け付け中・探索中の相手のプレイヤー番号。いなければ -1 |

各クライアントは自分の番号（`CS_Player.localPlayer.playerNumber`）と比べて状態を決め、Controller を呼びます。

| 条件 | 状態 | Controller へ |
|---|---|---|
| `_foundPlayerNumber` が自分 | `Spotted` | `RegisterPolice` |
| `_signalPlayerNumber` が自分 | `Tracked` | `RegisterPolice` |
| どちらでもない | `Safe` | `UnregisterPolice` |

- 出現時（`OnNetworkSpawn`、オフラインなら `Start`）に登録の対象になり、消える時（`OnNetworkDespawn` / `OnDestroy`）に解除する
- ローカルプレイヤーが警察より後に出現する場合に備えて、`CS_Player.onLocalPlayerSet` でも判定し直す

### `CS_PoliceBrain.PublishDetection`（サーバー、判断のたびに呼ぶ）

| `CS_PoliceBrain` の状態 | 追跡中の相手 | 駆け付け・探索中の相手 |
|---|---|---|
| `Chase`（自分で見つけた・仲間から共有された、どちらも） | 標的の番号 ※ | 標的の番号 ※ |
| `Rush` | -1 | 信号の主の番号（分からなければ -1） |
| `Search` | -1 | **直前の相手の番号を残す**（Chase の標的 or Rush の主） |
| `Suspect` | -1 | **-1（未対応）** |
| `Patrol` / `Wait` | -1 | -1 |

※ 標的はプレイヤーとは限らない（悪人・陽動ホログラムもある）。`GetComponentInParent<CS_Player>()` が取れた時だけ番号を入れ、取れなければ -1。

**未対応：** 巡回時の視界に入って怪しまれている（`Suspect`）時も、UI では `Tracked`（黄）として出す仕様です。`Suspect` の時に、怪しんでいる相手のプレイヤー番号を「駆け付け・探索中の相手」として知らせる必要があります。

---

## プレイヤー側（秋野さん担当）

`CS_Player` で、自分の画面のプレイヤー（`isLocalPlayer`）が決まった時に次を行っています。

- `CS_Player.localPlayer` に自分を入れ、`onLocalPlayerSet` を発行する（警察側が判定し直すため）
- `CS_PoliceDetectionController.SetPlayerTransform` に、カメラの左右の向きを持つ基準（`_viewDirectionAnchor`）を渡す
  - 体はカメラと別の方向を向くため、プレイヤー本体ではなく専用の基準を渡している

---

## 表示の確認

デバッグモードはありません。警察を出したシーンで、実際に見つかったり信号を出したりして確認してください。
