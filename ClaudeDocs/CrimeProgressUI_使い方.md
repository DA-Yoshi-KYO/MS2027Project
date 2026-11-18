# 犯罪完遂ゲージUI 使い方

エリアごと(3エリア)の犯罪完遂率(0〜100%)をゲージで表示し、**現在地のゲージだけ大きく表示して数字(%)を出す**UI。

## 1. 構成

| クラス | 役割 |
|---|---|
| `CSE_CrimeAreaType` | エリアの種類(`Downtown` / `Energy` / `Infrastructure`) |
| `CS_UICrimeProgressModel` | エリアごとの完遂率と現在地を保持する(UIを参照しない) |
| `CS_UICrimeProgressPresenter` | Modelを購読し、変化をViewへ渡す |
| `CS_UICrimeProgressRootView` | 3エリア分のゲージをまとめて管理する |
| `CS_UICrimeProgressGaugeView` | エリア1か所分のゲージ。塗り・数字・拡縮を担当 |

流れ:

```
ゲーム側 → Model.Bind() / SetProgress() / SetCurrentArea()
Model.OnBound → Presenter購読 → 現在の状態を全て反映
Model.OnProgressChanged    → Presenter → RootView.SetProgress()      → GaugeView.SetProgress()
Model.OnCurrentAreaChanged → Presenter → RootView.SetCurrentArea()   → GaugeView.SetCurrentArea()
```

依存する既存クラス: `CS_BaseModel` / `CS_BasePresenter` / `CS_BaseView<T>`

## 2. セットアップ手順

### 2-1. UIを組む
Canvas配下に次の階層を作る。

```
Canvas
└─ CrimeProgress              ← CS_UICrimeProgressPresenter + CS_UICrimeProgressRootView
   ├─ AreaGauge_0             ← CS_UICrimeProgressGaugeView (Area Type: Downtown)
   │   ├─ GaugeBack           (Image)
   │   ├─ Gauge               (Image: Filled)
   │   ├─ Icon                (Image)
   │   └─ Value               (TextMeshProUGUI)
   ├─ AreaGauge_1             ← CS_UICrimeProgressGaugeView (Area Type: Energy)
   └─ AreaGauge_2             ← CS_UICrimeProgressGaugeView (Area Type: Infrastructure)
```

- `CrimeProgress` に `CS_UICrimeProgressPresenter` を付けると、`CS_UICrimeProgressRootView` が自動で要求される。
- **CS_UICrimeProgressRootView**: `_gaugeViews` に `AreaGauge_0〜2` を登録する。
- **CS_UICrimeProgressGaugeView**(各AreaGaugeに付ける)

| 項目 | 説明 |
|---|---|
| `_areaType` | 担当するエリア。3つで重複させない |
| `_animationRoot` | 拡縮する本体。空なら自身 |
| `_gaugeBackImage` | ゲージ背景 |
| `_gaugeImage` | 完遂ゲージ。**Image Typeを `Filled` にする**(`fillAmount` を操作するため) |
| `_iconImage` | エリアアイコン |
| `_valueText` | 完遂率の数字(例: `45%`) |
| `_inactiveScale` | 現在地でない時の倍率(既定 0.5) |
| `_activeScale` | 現在地の時の倍率(既定 1) |
| `_scaleDuration` | エリア移動時の拡縮時間(既定 0.25秒) |

### 2-2. 大きさの決め方
- AreaGaugeの**Inspector上のScaleが「現在地の基準サイズ」**になる(`Awake` で記憶される)。
- 現在地では `基準 × _activeScale`、それ以外では `基準 × _inactiveScale` で表示される。
- 基準サイズ100なら、現在地は100、それ以外は50になる。

### 2-3. 自動で変更される設定
`GaugeView` は `Awake` で次を自動設定する。Inspectorで変えても上書きされる。
- アンカー・ピボットを**左中央 `(0, 0.5)`** にする(左端を固定して右へ拡縮するため)。
- `anchoredPosition` は維持される。Pos X / Pos Y はInspectorで設定した値がそのまま使われる。
- 全Image・TextのRaycast Targetを**OFF**にする(入力を遮らない)。

### 2-4. ゲーム側でModelを作って公開する

```csharp
// 初期エリアを指定して生成
_crimeProgressModel = new CS_UICrimeProgressModel(CSE_CrimeAreaType.Downtown);

// 現在地が未確定で生成する場合
// _crimeProgressModel = new CS_UICrimeProgressModel();

_crimeProgressModel.Bind();
```

```csharp
// 完遂率を設定(0〜100に自動で丸められる)
_crimeProgressModel.SetProgress(CSE_CrimeAreaType.Energy, 40f);

// 完遂率を加算
_crimeProgressModel.AddProgress(CSE_CrimeAreaType.Energy, 5f);

// 現在地が変わった時
_crimeProgressModel.SetCurrentArea(CSE_CrimeAreaType.Infrastructure);
```

```csharp
// 破棄時(Unbindも内部で行われる)
_crimeProgressModel.Dispose();
```

- `Bind()` はModelを所有するゲーム側が呼ぶ。Presenter/Viewからは呼ばない。
- Presenterより先にBindしても後でBindしても動く(`OnBound` と `TryGet` の両対応)。
- Bind時点でModelの全エリアの完遂率と現在地がUIへ反映される。この初回反映では**拡縮アニメーションは再生されない**。

## 3. 動作仕様

- 完遂率は0〜100で保持する。範囲外の値は丸められ、値が変わらない場合はイベントが発火しない。
- 表示する数字は四捨五入した整数(例: `45%`)。
- **Value(数字)が表示されるのは現在地のゲージだけ**。それ以外は非表示。ただし非表示でも内容は最新値に更新される。
- 現在地が変わると、新しい現在地は拡大、前の現在地は縮小される(`Time.unscaledDeltaTime` を使うのでポーズ中も動く)。
- 同じエリアを `SetCurrentArea` しても何も起きない。
- 現在地が未確定(`hasCurrentArea == false`)の間は、全ゲージが小さい状態で数字も非表示。
- Modelは同時に1つだけ公開できる。別のModelを `Bind()` すると、前のModelは自動で `Unbind` される。
- UIが無効化されるとModel購読が解除され、全ゲージが「現在地なし」の状態に戻る。

## 4. エリアを増やす場合

1. `CSE_CrimeAreaType` に値を追加する。
2. シーンに `AreaGauge_N` を追加し、`_areaType` を設定する。
3. `CS_UICrimeProgressRootView._gaugeViews` に登録する。

> `Model` と `Presenter` は `Enum.GetValues` で全エリアを処理するので、enumへ追加するだけで対象になる。**ゲージを登録し忘れると、そのエリアの更新のたびに警告ログ**(`対応するゲージがありません`)が出る。

## 5. トラブルシュート

| 症状 | 確認すること |
|---|---|
| 何も更新されない | `Bind()` を呼んだか / Presenterが有効か |
| ゲージが塗られない | `_gaugeImage` の Image Type が `Filled` か |
| 警告「対応するゲージがありません」 | `_gaugeViews` の登録漏れ / `_areaType` の設定ミス・重複 |
| 数字が出ない | そのエリアが現在地か(`SetCurrentArea` 済みか)/ `_valueText` の設定 |
| 現在地が小さいまま | `hasCurrentArea` が false のまま(`SetCurrentArea` を呼んでいない) |
| 大きさがおかしい | AreaGaugeのInspector上のScale(基準サイズ)/ `_activeScale`・`_inactiveScale` |
| 位置がずれる | `Awake` でアンカー・ピボットが左中央に変更される。Pos X / Pos Y はその基準で設定する |
| 拡縮の途中で止まる | AreaGaugeを拡縮中に無効化すると、アニメーションが中断され途中の大きさで止まる |
| 再生を繰り返すと状態が残る | Modelの静的状態は `RuntimeInitializeOnLoadMethod` でリセット済み。独自にBindした場合も `Dispose()` を忘れない |
