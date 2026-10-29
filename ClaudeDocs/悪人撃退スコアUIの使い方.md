# 悪人撃退スコアUI の使い方（`CS_UIVillainScorePopupPresenter`）

悪人を倒した場所に「+150点」のような点数をふわっと出して消す、World Space のポップアップです（PR #364）。
UI側は MVP 構成になっていますが、**使う側はプレハブを生成して `Initialize()` を呼ぶだけ** です。
Model は Presenter が自分で作って、表示が終わったら一緒に片付けます。

```
倒した側のスクリプト ──Instantiate / Initialize(点数, 位置)──▶ Presenter ──(Modelを作って再生)──▶ View(点数テキスト・位置・透明度)
                                                                  └─ 表示が終わったら自分(ポップアップ)を Destroy
```

やることはこれだけです。

1. `VillainScorePopupCanvas.prefab` を Inspector で参照できるようにする
2. 悪人を倒したら、プレハブを `Instantiate` する
3. `Initialize(点数, 表示位置)` を呼ぶ → あとは勝手に出て、勝手に消える

> 2026-10-29: プレイヤー側で撃退時に出すようにつなぎました（Issue #368。`CS_PlayerScoring`）。倒した本人の画面にだけ出ます（他のプレイヤー・NPCが倒した分、0点(レイドボス)の時は出ません）。
> 下の「撃退時に出す（つなぎ方の例）」は、実際のつなぎ方とほぼ同じです。

---

## 準備（プレハブ側）

- プレハブ：`Assets/Programmer/Prefab/UI/InGame/Villain/VillainScorePopupCanvas.prefab`
- **World Space の Canvas**（縮尺 0.01）なので、シーンや他のプレハブに置いておく必要はありません。使うたびに `Instantiate` してください
- 子の `Text`（TextMeshPro）が `CS_UIVillainScorePopupView` の「スコア表示」に設定済みです
- Presenter / View / Canvas / CanvasGroup はプレハブに付いているので、追加の設定は不要です

## 見え方

| 段階 | 時間（既定） | 動き |
|---|---|---|
| 出現 | 0.2秒 | 透明 → 不透明になりながら 0.2 上に動く |
| 停止 | 0.5秒 | そのまま表示 |
| 消える | 0.3秒 | 不透明 → 透明になりながら さらに 0.3 上に動く |

- 表示は `+150点` の形です（マイナスなら `-20点`、0なら `0点`）
- 表示中は **常にカメラの方を向きます**
- 消え終わったら **ポップアップ自身を `Destroy`** します（呼んだ側で消す必要はありません）
- 時間は `Time.unscaledDeltaTime` で進むので、**`Time.timeScale = 0`（ポーズ）中も止まらずに進みます**

## コード例（いちばん簡単な使い方）

```csharp
using UnityEngine;

public class CS_ScorePopupSample : MonoBehaviour
{
    [SerializeField] private CS_UIVillainScorePopupPresenter _popupPrefab;   // VillainScorePopupCanvas.prefab をセット
    [SerializeField] private float _heightOffset = 2.0f;                     // 悪人の足元から少し上に出す

    // 悪人を倒したときに呼ぶ
    public void ShowScore(int point, Vector3 villainPosition)
    {
        Vector3 position = villainPosition + Vector3.up * _heightOffset;

        // ① 生成（位置は Initialize で入るので、ここは何でもOK）
        CS_UIVillainScorePopupPresenter popup = Instantiate(_popupPrefab, position, Quaternion.identity);

        // ② 点数と位置を渡す → 再生が始まり、終わったら自動で消える
        popup.Initialize(point, position);
        // カメラを指定したいとき: popup.Initialize(point, position, 自分のカメラ);
    }
}
```

- 何個同時に出しても OK です（1個ごとに Model と Presenter が別なので混ざりません）
- カメラを省略すると `Camera.main` を使います。プレイヤーごとにカメラが違う場合は、**そのプレイヤーのカメラを第3引数で渡してください**

## 撃退時に出す（つなぎ方の例）

点数は `CS_PlayerScoring.HandleDefeated()` で計算しています（暗躍ボーナスの倍率込み）。
ただし **スコアの計算はサーバーだけで動く** ので、そこで直接 `Instantiate` すると倒した本人の画面に出ません。
倒した本人（そのプレイヤーの Owner）へ RPC で「点数」と「位置」を送り、受け取った側で生成します。

```csharp
// CS_PlayerScoring に足すイメージ（未実装）
[SerializeField] private CS_UIVillainScorePopupPresenter _scorePopupPrefab;

private void HandleDefeated(IDamageable target)
{
    if (!CanScore()) return;

    if (target is CS_VillainHealth villain)
    {
        _bonus.AddDefeat();
        int point = Mathf.RoundToInt(GetDefeatScore(villain) * _bonus.scoreMultiplier);
        _holder.AddDefeatVillain(point);
        onVillainDefeated?.Invoke(villain);

        // ↓ 追加：倒した本人の画面に点数を出す
        Vector3 position = villain.transform.position + Vector3.up * 2.0f;
        if (IsSpawned) ShowScorePopupRpc(point, position);
        else ShowScorePopup(point, position);   // オフラインのテストシーン
        return;
    }
    // …
}

[Rpc(SendTo.Owner)]
private void ShowScorePopupRpc(int point, Vector3 position)
{
    ShowScorePopup(point, position);
}

private void ShowScorePopup(int point, Vector3 position)
{
    if (_scorePopupPrefab == null) return;

    CS_UIVillainScorePopupPresenter popup = Instantiate(_scorePopupPrefab, position, Quaternion.identity);
    popup.Initialize(point, position);
}
```

- NPC（AI プレイヤー）が倒した分も出したくない場合は、Owner かつ人が操作しているプレイヤーだけで出すように条件を足してください
- 実際につなぐときは、プレイヤー担当（`CS_PlayerScoring` の作成者）と相談してください

---

## Model を自分で持ちたいとき（補足）

基本は `Initialize()` で足ります。Model を外から作って渡したいときは `BindModel()` を使います。

```csharp
var popup = Instantiate(_popupPrefab);
var model = new CS_UIVillainScorePopupModel();

popup.BindModel(model);
model.Show(150, position);   // ここで再生が始まる
```

- 再生が終わると、Presenter が **Model も `Dispose()`** してからポップアップを消します。終わった後に同じ Model を使い回さないでください
- 再生中にもう一度 `Show()` すると、最初から再生し直します

---

## Inspector で調整できる値（`CS_UIVillainScorePopupPresenter`）

| 項目 | 既定値 | 説明 |
|---|---|---|
| `_fadeInDuration` | 0.2 | 出現にかける秒数 |
| `_holdDuration` | 0.5 | 止まって見せる秒数 |
| `_fadeOutDuration` | 0.3 | 消えるのにかける秒数 |
| `_fadeInMoveDistance` | 0.2 | 出現中に上に動く量（ワールド座標） |
| `_fadeOutMoveDistance` | 0.3 | 消える間に上に動く量（ワールド座標） |
| `_targetCamera` | なし | 向くカメラ。未設定なら `Camera.main`（`Initialize` の第3引数でも指定可） |
| `_lookAtCamera` | ON | カメラの方を向き続けるか |

- 文字の大きさや色は、子の `Text`（TextMeshPro）で変えてください（既定はフォントサイズ 50）

---

## API 早見表

| メソッド / プロパティ | 説明 |
|---|---|
| `Presenter.Initialize(score, worldPosition)` | 点数と位置を渡して再生する。Model は Presenter が作る |
| `Presenter.Initialize(score, worldPosition, camera)` | 向くカメラを指定して再生する |
| `Presenter.BindModel(model)` | 自分で作った Model を紐づける |
| `new CS_UIVillainScorePopupModel()` | Model を作る |
| `Model.Show(score, worldPosition)` | 点数と位置を設定して再生を要求する |
| `Model.currentScore` / `Model.currentWorldPosition` | 今の点数・位置を読む |
| `Model.Dispose()` | 後片付け（再生が終わると Presenter が呼ぶ） |

- **View のスクリプトは触らなくてOK**（プレハブに付いています）
- 関係するスクリプト
  - `Assets/Programmer/Script/UI/MVP(Model)/UI_VillainScorePopup/CS_UIVillainScorePopupModel.cs`
  - `Assets/Programmer/Script/UI/MVP(Presenter)/UI_VillainScorePopup/CS_UIVillainScorePopupPresenter.cs`
  - `Assets/Programmer/Script/UI/MVP(View)/UI_VillainScorePopup/CS_UIVillainScorePopupView.cs`
