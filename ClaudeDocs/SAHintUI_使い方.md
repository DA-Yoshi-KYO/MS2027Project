# SAヒントUI 使い方

プレイヤーがSAポイントに隠れている間、**近くにある他のSAポイントの方向**をプレイヤー周囲の楕円上に矢印で案内するUI。

## 1. 構成

| クラス | 役割 |
|---|---|
| `CSO_UISAHintData` | 調整値(ScriptableObject) |
| `CS_UISAHintModel` | 状態保持・SAポイント検索(UIを参照しない) |
| `CS_UISAHintPresenter` | Modelを購読し、矢印の位置・角度を計算してViewへ渡す |
| `CS_UISAHintView` | 矢印3つ分を管理する |
| `CS_UISAHintItemView` | 矢印1つ分。位置・回転・拡縮アニメーション |
| `CS_SAPoint` | 案内対象(`activePoints` / `standPosition` を使用) |

流れ:

```
ゲーム側 → Model.Bind()/StartHiding()
Model.OnBound → Presenter購読 → RefreshTargets()
Presenter.UpdateIndicators() → View.ShowIndicator() → ItemView.Show()
```

依存する既存クラス: `CS_BaseModel` / `CS_BasePresenter` / `CS_BaseView<T>` / `CS_UIAfterCameraUpdate` / `CS_UIWorldToCanvas`

## 2. セットアップ手順

### 2-1. データアセットを作る
1. Projectビューで右クリック → `Create > Game > UI > SA Hint Data`
2. 名前は `UI_SAHintData`(既定)。値は「4. 調整値」を参照。

### 2-2. UIを組む
Canvas配下に次の階層を作る。

```
Canvas
└─ SAHint            ← CS_UISAHintPresenter + CS_UISAHintView
   ├─ Arrow0         ← CS_UISAHintItemView
   │   └─ (Image)
   ├─ Arrow1         ← CS_UISAHintItemView
   └─ Arrow2         ← CS_UISAHintItemView
```

- `SAHint` に `CS_UISAHintPresenter` を付けると `CS_UISAHintView` が自動で要求される。
- **CS_UISAHintView**: `_items` に `CS_UISAHintItemView` を最大3個登録する。
- **CS_UISAHintPresenter**
  - `_canvas` / `_canvasRect`: 空なら親のCanvasから自動取得。
  - `_playerWorldOffset`: 楕円の中心にするプレイヤー位置の補正(既定: 頭の高さ `(0, 1.2, 0)`)。
  - `_pointWorldOffset`: 矢印が指すSAポイント位置の補正(既定: 0)。
- **CS_UISAHintItemView**
  - `_indicatorRoot`: 位置と回転を管理するルート(空なら自身)。
  - `_animationRoot`: 拡縮するルート(空なら `_indicatorRoot`)。
  - `_arrowImage`: 矢印画像(`raycastTarget` は自動でOFFになる)。
  - `_angleOffset`: 矢印画像が**右向きなら0、上向きなら-90**。

> 位置は `anchoredPosition` で設定されるため、矢印ルートのアンカー/ピボットは中央にしておくこと。Canvas全体の座標系と合わせる想定。

### 2-3. ゲーム側でModelを作って公開する
ローカルプレイヤーを所有するコードで行う。Presenter/Viewからは `Bind()` しない。

```csharp
// 生成と公開(プレイヤー生成時)
_saHintModel = new CS_UISAHintModel(
    player: playerTransform,
    playerCamera: playerCamera,
    data: saHintData);

_saHintModel.Bind();
```

```csharp
// SAポイントに隠れた時
_saHintModel.StartHiding(currentHidingPoint: point);

// SAポイントから出た時
_saHintModel.StopHiding();
```

```csharp
// 破棄時(Unbindも内部で行われる)
_saHintModel.Dispose();
```

- `StartHiding(point)` に現在隠れているSAポイントを渡すと、**そのポイント自身は案内対象から除外**される。
- `SetHiding(bool)` は既存コード接続用。`true` では現在地SAが不明で除外できないため、新規コードでは `StartHiding` / `StopHiding` を使う。
- Presenterより先にBindしても後でBindしても動く(`OnBound` と `TryGet` の両対応)。

## 3. 動作仕様

- 表示するのは**隠れている間と、SA到着後のみ**。`StopHint()`(`StopHiding()` も同じ)で対象がクリアされ、矢印は全て非表示になる。
- 検索対象は `CS_SAPoint.activePoints`。距離は `standPosition` で計算し、`searchDistance` 以内を**近い順に最大 `maximumDisplayCount` 個**表示する。
- 矢印は、プレイヤーの画面座標を中心とした楕円(`horizontalRadius` × `verticalRadius`)上に置かれ、SAポイント方向を向く。
- 画面変換に失敗した対象(カメラ背後など)や、プレイヤーと同一画面位置の対象はスキップされる。
- 矢印はUpdate内で `Time.unscaledDeltaTime` により拡縮する(ポーズ中も動く)。矢印ごとに位相がずれる。
- Presenterは毎フレーム(カメラ更新後)`RefreshTargets()` を呼ぶ。一覧が変化した時のみ `OnTargetsUpdated` が発火する。
- 案内するのは、`SAReachBox`が設定されている(`hasSAReachBox`)SAポイントだけ。箱が空でSAできないポイントは案内しない。
- プレイヤーから `minimumDistance` 未満の近すぎるSAポイントは案内しない。
- 遠くのSAポイントも案内する目的のため、`IsInSAReachBox`(足元が箱の中にあるか)では絞らない。
- 表示するのは、ハイドポイントの隠れられる箱の中にいる間と、SAで到着したSAポイント(信号機など種類を問わない)の上にいる間。ゲーム側は `StartHint(今いるSAポイント)` / `StopHint()` を呼ぶ(`StartHiding` / `StopHiding` も同じ動き)。
- SAで到着した後は、着く位置から1m(`CS_PlayerHide._arrivedHintLeaveDistance`)より離れる・ジャンプする・次のSAを始めると消える。

## 4. 調整値(`CSO_UISAHintData`)

| 項目 | 既定 | 説明 |
|---|---|---|
| `searchDistance` | 50 | SAポイントを検索する距離(m) |
| `minimumDistance` | 2 | これより近いSAポイントは案内しない距離(m。0〜`searchDistance`) |
| `maximumDisplayCount` | 3 | 同時表示する矢印数(1〜3) |
| `horizontalRadius` | 200 | 矢印楕円の横半径 |
| `verticalRadius` | 100 | 矢印楕円の縦半径 |
| `policeHorizontalRadius` | 700 | 警戒UI楕円の横半径 |
| `policeVerticalRadius` | 350 | 警戒UI楕円の縦半径 |
| `policeMargin` | 30 | 警戒UI楕円との最低間隔 |
| `minimumScale` | 0.9 | 拡縮の最小倍率 |
| `maximumScale` | 1.05 | 拡縮の最大倍率 |
| `scaleDuration` | 0.8 | 最小→最大にかかる時間(秒) |

- 実際に使われる楕円半径は `min(設定値, 警戒UI半径 − policeMargin)`。警戒UIと重ならないよう自動で内側に制限される。
- `OnValidate` で値が補正される(最大スケール ≥ 最小スケールなど)。

## 5. トラブルシュート

| 症状 | 確認すること |
|---|---|
| 何も表示されない | `Bind()` を呼んだか / `StartHiding()` を呼んだか / `data` が未設定でないか / Canvasが取得できているか |
| 矢印が3つ出ない | `maximumDisplayCount`、`View._items` の登録数、`searchDistance` 内のSAポイント数 |
| 自分のいるSAを指す | `StartHiding` に現在のSAポイントを渡しているか(`SetHiding(true)` は除外できない) |
| 矢印の向きがずれる | `CS_UISAHintItemView._angleOffset`(右向き=0、上向き=-90) |
| 矢印の位置がずれる | 矢印ルートのアンカー/ピボット、`_playerWorldOffset`、Canvasの設定 |
| 警戒UIと重なる | `policeHorizontalRadius` / `policeVerticalRadius` / `policeMargin` と実際の警戒UIの大きさを合わせる |
| 案内先が地面でなくずれて見える | `_pointWorldOffset` で矢印が指す高さを補正 |
| 再生を繰り返すと状態が残る | `Model` / `CS_SAPoint` は `RuntimeInitializeOnLoadMethod` で静的状態をリセット済み。独自のBindを追加した場合も `Dispose()` を忘れない |
