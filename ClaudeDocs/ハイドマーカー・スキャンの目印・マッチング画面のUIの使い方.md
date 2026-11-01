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

スキャンで見えている悪人の頭上に、目印を出します。画面に直接重ねるので、建物の向こうにいても見えます。

| 役割 | クラス | 場所 |
|---|---|---|
| Model | `CS_UIScanMarkerModel` | `UI/UI_ScanMarker/` |
| Presenter | `CS_UIScanMarkerPresenter` | `UICanvas.prefab` の `ScanMarker` |
| View | `CS_UIScanMarkerView` | `UICanvas.prefab` の `ScanMarker`(複製元は子の `MarkerTemplate`) |
| Modelに値を渡す | `CS_PlayerScanMarkerUI` | `Player.prefab` |

- `CS_PlayerScanMarkerUI` は、毎フレーム `CS_PlayerScan.revealedVillains` の一覧を `SetTargets` に渡します。
- View は、`MarkerTemplate`(非表示)を必要な数だけ複製して使い回します。見た目を変えたいときは `MarkerTemplate` を差し替えてください(今は赤い菱形)。
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
