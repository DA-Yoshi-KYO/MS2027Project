# 暗躍ボーナス UI の使い方（`CS_UILimitBreakModel`）

暗躍ボーナスを画面に「LIMIT BREAK :20%」のように表示する UI です（仕様書「UI」ページの「LIMITBREAK(暗躍ボーナス)」）。
UI側は MVP 構成になっていますが、**使う側が触るのは Model だけ** です。
Presenter / View はUIのプレハブに最初から付いているので、コードから触る必要はありません。

```
あなたのスクリプト ──new / Bind / SetBonusPercent──▶ Model ──(自動で通知)──▶ Presenter ──▶ View(LIMIT BREAK :20%)
```

やることは基本この3つだけです。

1. `new` で Model を作る
2. `Bind()` で公開する
3. ボーナスが変わったら `SetBonusPercent()` を呼ぶ → 使い終わったら `Dispose()`

---

## 準備（シーン側）

- `Assets/Programmer/Prefab/UI/InGame/UICanvas.prefab` をシーンに置く
- 中の `LimitBreak` に Presenter / View が付いている（子の `DisplayRoot` の下にラベルと数値）
- 表示は **画面に1つだけ** です。自分（ローカルプレイヤー）の分だけ Bind してください

| クラス | 場所 | 役割 |
|---|---|---|
| `CS_UILimitBreakModel` | `Script/UI/MVP(Model)/UI_LimitBreak/` | 表示する%を持つ。使う側が触るのはここだけ |
| `CS_UILimitBreakPresenter` | `Script/UI/MVP(Presenter)/UI_LimitBreak/` | Bind された Model を購読して View に渡す |
| `CS_UILimitBreakView` | `Script/UI/MVP(View)/UI_LimitBreak/` | 表示・非表示とカウントアニメーション |

---

## Model でできること

| メソッド・プロパティ | 内容 |
|---|---|
| `Bind()` | この Model を画面に出す。すでに別の Model が Bind されていたら、そちらは外れる |
| `Unbind()` | 画面から外す（表示は0%に戻って消える） |
| `SetBonusPercent(int)` | 表示する%を設定する。0未満は0になる |
| `AddBonusPercent(int)` | 今の%に足す |
| `ClearBonus()` | 0%にする |
| `Dispose()` | Unbind して後片付けする。使い終わったら必ず呼ぶ |

---

## 表示のしくみ

- **0%の間は表示されません。** 1%以上になったら表示されます
- 値が変わると、**今の表示から新しい値まで1秒かけてカウント** します（View の `_countDuration`、既定1秒）
  - 0% → 20% なら、表示されてから 0 → 20 とカウントアップする
  - 途切れて 0% になった時は、1秒かけて 0 まで下がってから消える
- **Bind した瞬間はアニメーションしません。** その時の値がすぐに出ます（0なら非表示のまま）
- `Unbind()` / `Dispose()` すると、すぐに0%に戻って消えます
- カウントは `Time.unscaledDeltaTime` で進むので、ヒットストップなどで時間が止まっても進みます

---

## 渡す値（何の%か）

仕様書には「20%,40%と20%刻みで上がっていく」とだけ書かれています。
暗躍ボーナスで20%刻みなのは **スコアの倍率** なので、**スコア倍率の上乗せ分を%にしたもの** を渡します。

```
表示する% = (CS_PlayerStealthBonus.scoreMultiplier - 1) × 100
```

| 連続撃破数 | ボーナスの段階 | スコアの倍率 | 表示 |
|---|---|---|---|
| 0〜4人 | 0 | ×1.0 | 非表示 |
| 5人 | 1 | ×1.2 | 20% |
| 6人 | 2 | ×1.4 | 40% |
| … | … | … | … |
| 10人以上 | 6（上限） | ×2.2 | 120% |

- ステータスの倍率（0.1刻み）とは合わないので、こちらは使いません
- 小数の誤差を避けるため、`Mathf.RoundToInt` で丸めてから渡してください
- 倍率や人数はデータ表に合わせて `CS_PlayerStealthBonus` の Inspector で変わります。UI側に数字は持たせないでください

---

## コード例（プレイヤーへの組み込み）

ゲーム側の暗躍ボーナスは `CS_PlayerStealthBonus` がすでに持っています（`Player.prefab` に付いている）。

| ゲーム側 | 内容 |
|---|---|
| `streak` | 今の連続撃破数（NetworkVariable。全クライアントで読める） |
| `scoreMultiplier` | 今のスコアの倍率（1で無し） |
| `onStreakChanged(int 今の連続撃破数)` | 連続撃破数が変わった時。全クライアント・オフラインで発生する |

これを UI の Model に流すコンポーネントを、`CS_PlayerHideMarkerUI` と同じ形で作って `Player.prefab` に付けます（クラス名は仮）。

```csharp
using UnityEngine;

/*
 * プレイヤーの暗躍ボーナスを、暗躍ボーナスUI(CS_UILimitBreakModel)へ渡すクラス
 * CS_PlayerStealthBonusのスコア倍率を%にして、UI側のModelに渡すだけ
 * UI側(Model/Presenter/View)はこのクラス・CS_Playerの存在を一切知らない
 */

// ========================================
/*
 * メモ
 * ・暗躍ボーナスUIは自分の画面に1つだけのため、自分(isLocalPlayer)のプレイヤーだけがModelを作ってBindする
 *   isLocalPlayerはスポーン後に決まるため、毎フレーム確かめて、自分のプレイヤーになった時にBindする
 * ・CS_PlayerStealthBonusのロジックはこのクラスを知らない(外しても暗躍ボーナスは動く)
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでも単体で動く
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
[RequireComponent(typeof(CS_PlayerStealthBonus))]
public class CS_PlayerLimitBreakUI : MonoBehaviour
{
    private CS_Player _player;
    private CS_PlayerStealthBonus _stealthBonus;
    private CS_UILimitBreakModel _limitBreakModel;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _stealthBonus = GetComponent<CS_PlayerStealthBonus>();
    }

    private void OnEnable()
    {
        _stealthBonus.onStreakChanged += HandleStreakChanged;
    }

    private void OnDisable()
    {
        _stealthBonus.onStreakChanged -= HandleStreakChanged;
        UnbindLimitBreakUI();
    }

    private void LateUpdate()
    {
        if (!_player.isLocalPlayer)
        {
            UnbindLimitBreakUI();   // NPC・他人のプレイヤーは表示しない
            return;
        }

        if (_limitBreakModel == null) BindLimitBreakUI();
    }

    // 連続撃破数が変わったら、今の%をUIに渡す
    private void HandleStreakChanged(int streak)
    {
        ApplyBonusPercent();
    }

    // 暗躍ボーナスUIのModelを作って公開する
    private void BindLimitBreakUI()
    {
        _limitBreakModel = new CS_UILimitBreakModel();
        ApplyBonusPercent();            // Bind前に今の値を入れておく(Bind時はアニメーションせずにすぐ出る)
        _limitBreakModel.Bind();
    }

    private void UnbindLimitBreakUI()
    {
        if (_limitBreakModel == null) return;

        _limitBreakModel.Dispose();
        _limitBreakModel = null;
    }

    // スコアの倍率の上乗せ分を%にして渡す(×1.2なら20%)
    private void ApplyBonusPercent()
    {
        if (_limitBreakModel == null) return;

        int percent = Mathf.RoundToInt((_stealthBonus.scoreMultiplier - 1f) * 100f);
        _limitBreakModel.SetBonusPercent(percent);
    }
}
```

---

## 注意点

- **Bind できる Model は1つだけ** です。後から Bind した方で上書きされ、前の Model は外れます。
  NPC や他人のプレイヤーで Bind すると自分の表示が乗っ取られるので、必ず `isLocalPlayer` で絞ってください
  （`IsOwner` だと、ホストでは NPC も true になるので使わない）
- `onStreakChanged` は全クライアントで発生しますが、Model を持っているのは自分のプレイヤーだけなので、他人の分は何もしません
- 警察に見つかった・HPが0になった時は `CS_PlayerStealthBonus` が連続撃破数を0に戻すので、
  `onStreakChanged` から自動で 0% が渡り、1秒かけて下がってから消えます。UI側で特別な処理は要りません
- 表示位置・色・文字は仮です。デザインが決まり次第、`UICanvas.prefab` の `LimitBreak` を差し替えてください
