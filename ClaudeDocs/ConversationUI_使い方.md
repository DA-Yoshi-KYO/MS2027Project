# 会話ウィンドウUI 使い方

`Say("文", 秒数)` を呼ぶと、会話ウィンドウが**フェードインして文を表示し、指定秒数後に自動でフェードアウトして消える**UI。固定会話・ランダム会話など、いろいろな呼び出し元から同じ入口で使える。

> このUIは **R3**(`using R3;`)を使っている。プロジェクトにR3が入っている必要がある。

## 1. 構成

| クラス | 役割 |
|---|---|
| `CSO_UIConversationSettings` | 表示時間・フェード時間の設定(ScriptableObject) |
| `CS_UIConversationModel` | 今の文と表示状態を保持する。`Say()` が外からの入口 |
| `CS_UIConversationPresenter` | Modelを購読してViewを動かす。表示時間のタイマーも持つ |
| `CS_UIConversationView` | 文字の表示と、CanvasGroupのalphaによるフェード |

流れ:

```
Model.Say("文", 3秒)
 → (自動) View.SetText / View.SetVisible(true)   … 文字が変わってフェードイン
 → (自動) Presenterが3秒のタイマーを開始
 → 3秒後 Model.Hide()
 → (自動) View.SetVisible(false)                  … フェードアウト
```

依存する既存クラス: `CS_BaseModel` / `CS_BasePresenter` / `CS_BaseView<T>`

## 2. セットアップ手順

### 2-1. 設定アセットを作る
1. Projectビューで右クリック → `Create > AiGuide > UI Conversation Settings`
2. `DB_UIConversationSettings` ができる。値は「5. 調整値」を参照。

### 2-2. UIを組む
Canvas配下に、背景Imageを持つウィンドウを作る。

```
Canvas
└─ ConversationWindow        ← Image + CanvasGroup + CS_UIConversationView + CS_UIConversationPresenter
   └─ MessageText            (TextMeshPro)
```

- **ViewとPresenterは同じGameObjectに付ける**(PresenterがGetComponentでViewを取得する)。
- `CS_UIConversationView` を付けると `CanvasGroup` が自動で追加される。
- **CS_UIConversationView**: `_label` に子の `MessageText`(`TMP_Text`)を設定する。
- **CS_UIConversationPresenter**: `_settings` に `DB_UIConversationSettings` を設定する。**未設定だとエラーになる**(フェード時間・表示時間を設定から読むため)。

> Viewは起動時に自動で `alpha = 0`(透明)、操作を受け付けない、クリックを素通し(`blocksRaycasts = false`)に設定する。ゲーム操作の邪魔にならない。

### 2-3. Modelを作って公開する
会話ウィンドウは画面に1つなので、Modelも**1つだけ**作って `Bind()` する。使う側(会話を管理するクラスなど)が `new` して、使い終わったら `Dispose()` する。

```csharp
private CS_UIConversationModel _conversationModel;

private void Start()
{
    _conversationModel = new CS_UIConversationModel();
    _conversationModel.Bind();
}

private void OnDestroy()
{
    _conversationModel?.Dispose();   // Unbindも内部で行われる
}
```

- Presenterより先にBindしても後でBindしても動く(`OnBound` と `TryGet` の両対応)。

## 3. 使い方

### 3-1. 文を表示する

```csharp
// 秒数を指定する
_conversationModel.Say("こんにちは", 3f);

// 秒数を省略する(設定アセットの既定秒数。初期値4秒)
_conversationModel.Say("こんにちは");
```

### 3-2. すぐ消す

```csharp
_conversationModel.Hide();   // フェードアウトして非表示。文は残る
```

### 3-3. Modelを自分で作らない側から呼ぶ
固定会話・ランダム会話など、Modelを所有していないクラスは `TryGet` で公開中のModelを取得する。

```csharp
if (CS_UIConversationModel.TryGet(out var model))
{
    model.Say("あっちに行ってみよう", 2.5f);
}
```

Modelが未公開(`Bind()` 前)の時は `TryGet` が `false` を返す。その場合 `Say` は呼べず、何も表示されない。

## 4. 動作仕様

- **表示時間の決まり方**
  - 秒数が0以下(省略時も含む) → 設定アセットの `defaultDuration`
  - 秒数が `minDuration` より短い → `minDuration` まで引き上げる
- **表示中に `Say()` を呼ぶと**、文が新しい文に差し替わり、**表示時間もやり直し**になる。前の文のタイマーで新しい文が途中で消えることはない。
- **空の文**(空文字・空白のみ)は無視され、警告ログが出る。
- `Hide()` や時間切れで消えても、**文はModelに残る**(フェードアウト中に文字が消えないようにするため)。
- フェードは `Time.unscaledDeltaTime` を使うので、ポーズ中(`timeScale = 0`)でも動く。表示時間のタイマーはR3の `Observable.Timer` で、時間の進み方はR3のTimeProvider設定に従う。
- **別のModelを `Bind()` すると置き換わる**(警告ログが出る)。
- **`Unbind()` / `Dispose()` でModelが外れると、表示中ならフェードアウトして消える。**
- Presenterが購読を始めた時点でModelがすでに表示中だった場合は、**指定秒数ではなく既定秒数**で消すタイマーが始まる。Presenterの生成前に `Say("文", 10f)` を呼んだ場合などは、10秒ではなく既定秒数で消える。
- Presenterが無効化(`OnDisable`)されるとModelの購読は解除される。再び有効化されると、Bind済みのModelを自動で再購読する。

## 5. 調整値(`CSO_UIConversationSettings`)

| 項目 | 既定 | 説明 |
|---|---|---|
| `defaultDuration` | 4 | `Say()` で秒数を指定しなかった時の表示時間(秒) |
| `minDuration` | 0.5 | 表示時間の下限(秒)。極端に短い指定で一瞬で消えるのを防ぐ |
| `fadeInSeconds` | 0.15 | 出てくる時のフェード時間(秒)。0なら即表示 |
| `fadeOutSeconds` | 0.25 | 消える時のフェード時間(秒)。0なら即消える |

## 6. トラブルシュート

| 症状 | 確認すること |
|---|---|
| 何も表示されない | `Bind()` を呼んだか / `TryGet` が `true` を返しているか / ViewとPresenterが同じGameObjectにあるか |
| Presenterでエラーが出る | `_settings` に `DB_UIConversationSettings` を設定したか |
| 文字が出ない | Viewの `_label` を設定したか(未設定だと `SetText` でエラーになる) |
| 警告「空の文が渡されたため無視します」 | `Say` に空文字・空白だけを渡していないか |
| 警告「既に Bind 済みの Model があるため置き換えます」 | Modelを複数作って `Bind()` していないか。会話ウィンドウ用のModelは1つだけにする |
| 指定した秒数より早く消える | `minDuration` や、`Say` の連続呼び出し(後の `Say` で時間がやり直される)を確認 |
| 指定した秒数にならない | Presenterの購読前に `Say` した場合は既定秒数になる(4章) |
| 一瞬で消える・出ない | `fadeInSeconds` / `fadeOutSeconds` / `defaultDuration` の値 |
| UIがクリックを邪魔する | ViewがCanvasGroupの `blocksRaycasts = false` に設定する。他のコードで書き換えていないか確認 |
| 再生を繰り返すと状態が残る | Modelの静的状態は `RuntimeInitializeOnLoadMethod` でリセット済み。独自にBindした場合も `Dispose()` を忘れない |
