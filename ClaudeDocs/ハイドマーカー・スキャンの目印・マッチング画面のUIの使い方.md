# ハイドマーカー・スキャンの目印・マッチング画面の UI の使い方

この4つのUIは、MVPになっていなかったものを直したものです。

- ハイドマーカー(前は `CS_PlayerHideMarker`)
- スキャンの目印(前は `CS_PlayerScanEffect` の中)
- ルーム接続の画面(前は `CS_RoomConnectionUI`)
- マッチング待ちの画面(前は `CS_MatchingWaitingUI`)

**仮の見た目でも、UIはMVPで作ります。** 見た目を差し替えるときにロジックへ手を入れなくて済むようにするためです。

```
ゲーム側のスクリプト ──new / Bind / Set〇〇──▶ Model ──(自動で通知)──▶ Presenter ──▶ View(画面)
```

---

## 1. ハイドマーカー(`CS_UIHideMarkerModel`)

今ダッシュボタンを押したら飛べるハイドポイント(SA)の上に、目印を出します。

| 役割 | クラス | 場所 |
|---|---|---|
| Model | `CS_UIHideMarkerModel` | `UI/UI_HideMarker/` |
| Presenter | `CS_UIHideMarkerPresenter` | `UICanvas.prefab` の `HideMarker` |
| View | `CS_UIHideMarkerView` | `UICanvas.prefab` の `HideMarker`(目印の画像は子の `Marker`) |
| Modelに値を渡す | `CS_PlayerHideMarkerUI` | `Player.prefab` |

- `CS_PlayerHideMarkerUI` は、自分のプレイヤーのときだけ Model を `new` して `Bind()` します。そのあと毎フレーム、`CS_PlayerHide.currentCandidate` の立つ位置(`CS_HidePoint.standTransform`)を `SetTarget` に渡します。
- 目印を出す高さは Presenter の `_worldOffset` で変えます(既定は立つ位置の1.6m上)。
- 見た目は `Marker` の Image で変えます(今は水色の四角)。
- カメラの後ろや画面の外にあるときは出しません。

## 2. スキャンの目印(`CS_UIScanMarkerModel`)

スキャンで見えている悪人グループ・警察・巡回ドローンの頭上に、目印を出します(Issue #403・#404)。画面に直接重ねるので、建物の向こうにいても見えます。
仕様の元：Confluence「UI」スキャンUI、「プレイヤー」スキャン。

| 役割 | クラス | 場所 |
|---|---|---|
| Model | `CS_UIScanMarkerModel` | `UI/UI_ScanMarker/` |
| 表示内容1つ分 | `CS_UIScanMarkerData`(種類・位置・距離・犯罪完遂度) / `CSE_UIScanTargetType` | `UI/UI_ScanMarker/` |
| Presenter | `CS_UIScanMarkerPresenter` | `UICanvas.prefab` の `ScanMarker` |
| View | `CS_UIScanMarkerView` | `UICanvas.prefab` の `ScanMarker`(複製元は子の `MarkerTemplate`) |
| 目印1つ分のView | `CS_UIScanMarkerItemView` | 複製した目印に付く(`MarkerTemplate` に付いていなければ実行時に付ける) |
| Modelに値を渡す | `CS_PlayerScanMarkerUI` | `Player.prefab` |

### 表示ルール

| 種類 | 目印の数 | 出す情報 |
|---|---|---|
| 悪人 | **グループごとに1つ**(位置は生きているメンバーの中心) | 種類(アイコンの色)・距離・犯罪完遂度(下から溜まる赤い板) |
| 警察 | 1人ずつ | 種類・距離 |
| 巡回ドローン | 1機ずつ | 種類・距離 |

- 距離は自分のプレイヤーから相手までで、`12m` のように整数で出します。
- グループの誰か1人に波が届くと、グループ全員が見えるようになります(`CS_PlayerScan`)。

### 流れ

```
サーバー: CS_VillainGroup ──groupId・完遂度──▶ CS_VillainCrime(NetworkVariable)
自分のクライアント: CS_PlayerScan(見えている相手を種類ごとに持つ)
  └─ CS_PlayerScanMarkerUI ──SetTargets(CS_UIScanMarkerDataの一覧)──▶ Model ──▶ Presenter(座標変換) ──▶ View ──▶ ItemView
```

- `CS_PlayerScan` で見えている相手は、`revealedVillainGroups`(グループ番号・メンバー・中心・完遂度)、`revealedPolice`、`revealedDrones` で取れます。
- 悪人グループと犯罪の進行度はサーバーにしか無いので、グループが `CS_VillainCrime` の `groupId` / `crimeProgress`(NetworkVariable)へ書き込んでいます。完遂度は1%以上変わった時と、完遂した時だけ送ります。
- グループに属さない悪人(シーンに直接置いたもの)は、1人で1グループとして出します(完遂度は0)。

### 見た目を変える

画像は仕様書(Confluence「UI」スキャンUI)の物で、`Assets/Programmer/PlaceholderAsset/UI/Scan/` に置いています。`ScanMarker` の View(`CS_UIScanMarkerView`)の Inspector に設定済みです。

| 種類 | 画像 | 出し方 |
|---|---|---|
| 悪人グループ | `Icon_Villain_BackGroundEmpty`(灰色の三角) | 背景 |
| | `Icon_Villain_BackGroundFull`(赤い三角) | 背景に重ね、完遂度の分だけ下から溜まる(Image の Filled・縦・下から) |
| | `Icon_Villain`(白いマーク) | 一番上に重ねる |
| 警察 | `Icon_Police` | 1枚 |
| 巡回ドローン | `Icon_Drone` | 1枚 |

- 目印の大きさは `MarkerTemplate` の大きさ(今は 60×52)で決まります。画像は縦横比を保って収まります。
- 赤い三角・悪人のマーク・距離の文字は、`MarkerTemplate` に何も設定していなければ実行時に作ります(プレハブを触らなくても表示されます)。
  悪人のマークの位置・大きさ、距離の文字の大きさ・位置は、`CS_UIScanMarkerItemView` の「実行時に作る時の設定」で変えられます。
  自分で作った物を使いたい時は、`MarkerTemplate` に `CS_UIScanMarkerItemView` を付けて、各 Image・TextMeshPro を設定してください。
- 目印を出す高さは Presenter の `_worldOffset` で変えます(既定は足元の2.2m上)。
- 波の輪はUIではなくワールドに出すエフェクトなので、今まで通り `CS_PlayerScanEffect` が出します。

## 3. ルーム接続の画面(`CS_UIRoomConnectionModel`)

`RoomConnection` シーンの `RoomConnectionUI` オブジェクトに、次の3つが付いています。

| 役割 | クラス | やること |
|---|---|---|
| Controller | `CS_RoomConnectionController` | Model を `new` して持つ。ホスト・参加・ソロの処理と、状態を Model に書く |
| Presenter | `CS_UIRoomConnectionPresenter` | Model を View に反映する。ボタンが押されたことを `onHostRequested` / `onJoinRequested`(部屋番号付き) / `onSoloRequested` で Controller に知らせる |
| View | `CS_UIRoomConnectionView` | ボタン・入力欄・状態表示を持つ。押されたことを Observable で出すだけ |

## 4. マッチング待ちの画面(`CS_UIMatchingWaitingModel`)

`WaitingforaMatch` シーンの `MatchingWaitingUI` オブジェクトに、次の3つが付いています。

| 役割 | クラス | やること |
|---|---|---|
| Controller | `CS_MatchingWaitingController` | Model を `new` して持つ。入室の受付、開始、戻るの処理と、参加人数・開始ボタンを出すかを Model に書く |
| Presenter | `CS_UIMatchingWaitingPresenter` | Model を View に反映する。ボタンが押されたことを `onStartRequested` / `onBackRequested` で Controller に知らせる |
| View | `CS_UIMatchingWaitingView` | 参加人数の表示とボタンを持つ。押されたことを Observable で出すだけ |

- ボタンの入力は「View → Presenter → Controller」と流れ、表示は「Controller → Model → Presenter → View」と流れます。ボタンのある画面を新しく作るときも、同じ形にしてください。
