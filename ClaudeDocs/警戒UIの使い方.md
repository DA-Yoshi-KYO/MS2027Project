# 警戒 UI の使い方（`CS_PoliceDetectionController`）

警察が自分に気付いているかを、画面中央の見えない楕円の上に「目アイコン」で知らせる UI です。
仕様の元：Confluence「UI」⑨警察に見られているか判定するUI

UI 側（Model / Presenter / View / Controller）は実装済みです（作成：元浪）。
**警察・プレイヤー側からのつなぎ込みはまだ** なので、このmdでは「誰が何を呼ぶか」を担当ごとに書いています。

```
警察(サーバー) ──NetworkVariable──▶ 警察(各クライアント) ──RegisterPolice など──▶ Controller ──▶ Model ──▶ Presenter(毎フレーム方向計算) ──▶ View(目アイコン)
                                                                                         ▲
                                                     プレイヤー(ローカル) ──SetPlayerTransform─┘
```

---

## 仕様（表示ルール）

| 警察の状態（自分から見て） | 表示 |
|---|---|
| 巡回・待機中 | 表示しない |
| 自分の信号を受けて駆け付け中 | 黄（5m以内なら赤） |
| 自分を追跡中（仲間から共有された標的として追っている場合も含む） | 黄（5m以内なら赤）＋ 発見アニメーション |
| 探索中（自分を見失った後・自分の信号の現場に着いた後） | 黄（5m以内なら赤） |

- 色は **距離だけ** で決まる（View の `_dangerDistance`、既定5m）。発見アニメは状態（Found）だけで決まる
- 方向は **プレイヤーの向き基準**（正面＝楕円の上、真後ろ＝楕円の下）。Presenter が毎フレーム計算するので、位置を渡し続ける必要はない
- 近くにいる警察（角度差15°以内・距離差5m以内）は1つのアイコンにまとめて「×N」と表示する（Presenter の Inspector で変更可）

---

## 仕組み（UI 側・実装済み）

| クラス | ファイル | 役割 |
|---|---|---|
| `CS_PoliceDetectionController` | `GameController/CS_PoliceDetectionController.cs` | **外から触るのはこれだけ**。Model を内部で1つ持ち、Presenter に渡す。GameControllerManager.prefab に付いている |
| `CS_UIPoliceDetectionModel` | `UI/MVP(Model)/UI_PoliceDirection/CS_UIPoliceDetectionModel.cs` | 表示中の警察（Hidden 以外）だけを保持 |
| `CS_PoliceDetectionData` / `CSE_PoliceDetectionState` | `UI/CS_PoliceDetectionData.cs` | 警察1人分のデータと状態 |
| `CS_UIPoliceDetectionPresenter` | `UI/MVP(Presenter)/UI_PoliceDirection/CS_UIPoliceDetectionPresenter.cs` | 毎フレーム角度・距離を計算し、まとめて View へ |
| `CS_UIPoliceDetectionView` | `UI/MVP(View)/UI_PoliceDirection/CS_UIPoliceDetectionView.cs` | 楕円上へ配置・色・×N・発見アニメ。UICanvas.prefab に付いている |

※ 他の UI Model（HP・手配度など）と違い、**Model を `new` して `Bind` する形ではありません**。Model は Controller の中にあり、外からは Controller のメソッド経由でしか触れません。

```csharp
public enum CSE_PoliceDetectionState
{
    Hidden = 0,         // 表示しない（巡回・待機）
    SignalDetected = 1, // 駆け付け中・探索中
    Found = 2,          // 追跡中（発見アニメ）
}
```

### Controller の API

| メソッド | 説明 |
|---|---|
| `SetPlayerTransform(Transform)` | 方向の基準にするプレイヤーを登録。**プレイヤー担当が呼ぶ** |
| `RegisterPolice(string id, Transform 警察, 状態)` | 警察を登録（同じ id なら差し替え）。`Hidden` を渡すと登録解除になる |
| `SetPoliceState(string id, 状態)` | 登録済みの警察の状態を変える。`Hidden` にすると登録解除される |
| `UnregisterPolice(string id)` | 登録解除 |

- **一度 `Hidden` にした警察は Model から消えるので、`SetPoliceState` では復帰しません。** 表示に戻す時は必ず `RegisterPolice` を呼び直してください
- 迷ったら「Hidden 以外 → `RegisterPolice`、Hidden → `UnregisterPolice`」だけ使えば安全です
- id はミニマップと同じく `$"Police_{NetworkObjectId}"` を推奨

---

## 警察担当（宇留野さん向け）

警察AI（`CS_PoliceBrain`）はサーバーでしか動かないので、**状態を NetworkVariable で同期 → 各クライアントで Controller を呼ぶ** 形にします。

### 1. 同期用の NetworkBehaviour を追加（仮名 `CS_PoliceDetectionSource`）

警察プレハブに付けて、次の2つを NetworkVariable で持ちます（書き込みはサーバーのみ）。

| 変数 | 型 | 内容 |
|---|---|---|
| `foundPlayerNumber` | `int` | 追跡中の相手のプレイヤー番号。いなければ -1 |
| `signalPlayerNumber` | `int` | 駆け付け中・探索中の相手のプレイヤー番号。いなければ -1 |

### 2. サーバーで状態が変わった時に書く

| `CS_PoliceBrain` の状態 | `foundPlayerNumber` | `signalPlayerNumber` |
|---|---|---|
| `Chase`（自分で見つけた・仲間から共有された、どちらも） | 標的の番号 ※ | 標的の番号 ※ |
| `Rush` | -1 | 信号の主の番号（分からなければ -1） |
| `Search` | -1 | **直前の相手の番号を残す**（Chase の標的 or Rush の主） |
| `Patrol` / `Wait` | -1 | -1 |

※ 標的はプレイヤーとは限らない（悪人・陽動ホログラムもある）。`target.GetComponentInParent<CS_Player>()` が取れた時だけ番号を入れ、取れなければ -1。

### 3. 駆け付けの「信号の主」を通す

今は駆け付けの指示に位置しか渡っていない所があるので、主（`CS_PlayerHealth`）を通す必要があります。

| 駆け付けの発生元 | 今の状態 | 必要な変更 |
|---|---|---|
| 変身中の定期信号 `CS_PoliceSquad.NotifyIncident(位置, 主)` → 警備グループ | `ReceiveIncident(位置)` で主が落ちる | `ReceiveIncident` → `RequestRush` に主を渡す |
| 同上 → 増援 `CS_PoliceWantedManager.ReceiveSignal` | 主は分かる | `RequestRush` に主を渡す |
| 警備エリア内で変身完了（`CS_PoliceTransformationWatcher`） | 位置しか返さない | 位置と一緒にプレイヤーも返す |
| 増援の出現 `RequestPursuit(owner)` | 主は分かる | そのまま番号に使う |
| 位置だけの `NotifyIncident(位置)`（その他の事件） | 主なし | -1 のまま（UIには出ない） |

### 4. 各クライアントで Controller を呼ぶ

`CS_PoliceMiniMapIcon` と同じ形（`OnNetworkSpawn` で登録、`OnNetworkDespawn` / `OnDestroy` で解除、Controller は `FindAnyObjectByType` で探す）。

```csharp
// 2つの NetworkVariable の OnValueChanged と、OnNetworkSpawn から呼ぶ
private void Refresh()
{
    if (_controller == null || CS_Player.localPlayer == null) return;
    int me = CS_Player.localPlayer.playerNumber;

    CSE_PoliceDetectionState state =
          _foundPlayerNumber.Value  == me ? CSE_PoliceDetectionState.Found
        : _signalPlayerNumber.Value == me ? CSE_PoliceDetectionState.SignalDetected
        : CSE_PoliceDetectionState.Hidden;

    if (state == CSE_PoliceDetectionState.Hidden) _controller.UnregisterPolice(_id);
    else _controller.RegisterPolice(_id, transform, state);
}
```

- ローカルプレイヤーが警察より後に出現する場合があるので、ローカルプレイヤーが決まった時にも `Refresh()` し直せるようにしておく（`CS_Player.localPlayer` 側のイベント等）

---

## プレイヤー担当（秋野さん向け）

### 1. 方向の基準を登録する

自分の画面のプレイヤー（`CS_Player.isLocalPlayer` が true）が出現した時に呼びます。

```csharp
FindAnyObjectByType<CS_PoliceDetectionController>()?.SetPlayerTransform(transform);
```

- `IsOwner` ではなく `isLocalPlayer` で判定（NPC はホストで IsOwner が true になるため）

### 2. 自分の番号を警察側から参照できるようにする

`CS_Player` に static の参照を追加し、`isLocalPlayer` の時にセットします（案）。

```csharp
public static CS_Player localPlayer { get; private set; }
public static event System.Action<CS_Player> onLocalPlayerSet; // 警察側が後から Refresh するため
```

### 3. 信号を出す処理

`CS_PlayerTransformation` が変身中に `CS_PoliceSquad.NotifyIncident(位置, 自分の CS_PlayerHealth)` を出しているので、**追加作業なし**。

---

## デバッグモード（警察・プレイヤーなしで表示確認）

Controller の `_enableDebugMode` をオンにすると、仮の警察が出てプレイヤー（`_debugPlayerTransform`、未設定なら Controller 自身）の周りを回ります。

| キー | 内容 |
|---|---|
| 1 / 2 / 3 | 前 / 右 / 後ろに警察を追加（SignalDetected） |
| 4 | 全削除 |
| 5 | 近い2体を出して「×2」を確認 |
| 6 | Found の警察を出す（発見アニメ確認用） |
| 7 | 4m の警察を出して赤を確認 |
| Space | 回転の停止・再開 |
