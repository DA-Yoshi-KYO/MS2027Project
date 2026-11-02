# 警戒状態の赤帯UI の使い方（`CS_UIAttentionAlertModel`）

警察や防犯装置に見つかって警戒状態になっている間、画面の上下に赤い帯を出して、左から右へループさせる UI です（PR #373）。
仕様の元：Confluence「UI」→「インゲーム内のリアクション・通知UI」→「警戒状態時のUI」

> 「警察や防犯装置の視界に引っ掛かり、警戒状態になってしまったときは赤幕を左から右へ動かしてください。警戒状態の間は常にループさせてほしいです。」

UI 側（Model / Presenter / View）と UICanvas への配置は PR #373 で実装済みです。
**ただし、2026-10-29 時点では Model を作って `Bind()` する人がいないため、ゲーム中に赤帯は一度も表示されません。**
このmdでは、プレイヤー側からのつなぎ込み方を説明します。

```
警察・防犯装置 ──NotifyDetected()──▶ CS_PlayerAlert(警戒状態。NetworkVariableで全員に同期)
                                          │ onAlertChanged
                                          ▼
                              CS_PlayerAttentionAlertUI(新規・つなぎ込み役。自分のプレイヤーだけ)
                                          │ new / Bind() / SetAlerted()
                                          ▼
                              CS_UIAttentionAlertModel ──▶ Presenter ──▶ View(上下の赤帯をUVスクロール)
```

---

## 仕組み（UI 側・実装済み）

| クラス | ファイル | 役割 |
|---|---|---|
| `CS_UIAttentionAlertModel` | `UI/UI_AttentionAlert/CS_UIAttentionAlertModel.cs` | 警戒中かどうか（`bool`）を持つ。`Bind()` すると Presenter に見つけてもらえる |
| `CS_UIAttentionAlertPresenter` | `UI/UI_AttentionAlert/CS_UIAttentionAlertPresenter.cs` | Bind された Model を購読して View に渡す。UICanvas.prefab の `AttentionAlert` に付いている |
| `CS_UIAttentionAlertView` | `UI/UI_AttentionAlert/CS_UIAttentionAlertView.cs` | 警戒中だけ `DisplayRoot` を表示し、`AlertBarUp` / `AlertBarDown` の UV を横にスクロールさせる |

### Model の API

| メンバー | 説明 |
|---|---|
| `Bind()` | この Model を「今表示する Model」として登録する。前に Bind されていた Model は自動で外れる |
| `Unbind()` | 登録を外す。赤帯は消える |
| `SetAlerted(bool)` | 警戒中かどうかを変える。`true` で赤帯が出てループ、`false` で消える |
| `Dispose()` | `Unbind()` してから片付ける。使い終わったら必ず呼ぶ |

- Presenter は `OnBound` / `OnUnbound` イベントで Model を見つけるので、**Presenter が先に有効になっていても、後から `Bind()` すれば表示されます**（順番は気にしなくてよい）
- `Bind()` の時点の値がすぐに反映されるので、**`SetAlerted()` → `Bind()` の順に呼ぶ** と、Bind 直後に一瞬違う表示になることがありません

---

## つなぎ込み（プレイヤー側・未実装）

`CS_PlayerScanMarkerUI`（スキャンの目印）と同じ形で、つなぎ込み役のクラスを1つ作ってプレイヤーに付けます。

### 守ること

- **自分の画面のプレイヤー（`isLocalPlayer`）だけが Model を作って `Bind()` する**
  `CS_PlayerAlert.onAlertChanged` は全クライアントで全プレイヤー分発生します。全員が Bind すると、他人の警戒状態で自分の画面に赤帯が出てしまいます
- `isLocalPlayer` はスポーン後に決まるので、毎フレーム確かめて、自分のプレイヤーになった時に Bind する
  （`IsOwner` ではなく `isLocalPlayer` を使う。NPC・オフラインのテストシーンにも対応するため。`CS_Player` のメモを参照）
- 警戒中かどうかは `CSE_PlayerAlertState` が `None` 以外か（`CS_PlayerAlert.isAlerted`）で決める。発見中・捜索中のどちらでも赤帯を出す
- 外れる時（無効化・破棄・自分のプレイヤーでなくなった時）は、イベントの購読を外して `Dispose()` する

### コード例（`Assets/Programmer/Script/Entity/Player/CS_PlayerAttentionAlertUI.cs`）

```csharp
using UnityEngine;

/*
 * 警戒状態(CS_PlayerAlert)を、警戒状態の赤帯UI(CS_UIAttentionAlertModel)へ渡すクラス
 * CS_PlayerAlertの状態が変わったら、警戒中かどうかをUI側のModelに渡すだけ
 * UI側(Model/Presenter/View)はこのクラス・CS_Playerの存在を一切知らない
 *
 * 制作者：　(担当者名)
 */

// ========================================
/*
 * メモ
 * ・赤帯は自分の画面にだけ出すため、自分(isLocalPlayer)のプレイヤーだけがModelを作ってBindする
 *   isLocalPlayerはスポーン後に決まるため、毎フレーム確かめて、自分のプレイヤーになった時にBindする
 * ・発見中・捜索中のどちらでも赤帯を出す(CS_PlayerAlert.isAlerted)
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでも単体で動く
 */
// ========================================

[RequireComponent(typeof(CS_PlayerAlert))]
[RequireComponent(typeof(CS_Player))]
public class CS_PlayerAttentionAlertUI : MonoBehaviour
{
    private CS_Player _player;
    private CS_PlayerAlert _alert;
    private CS_UIAttentionAlertModel _alertModel;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _alert = GetComponent<CS_PlayerAlert>();
    }

    private void OnDisable()
    {
        UnbindAlertUI();
    }

    private void LateUpdate()
    {
        if (!_player.isLocalPlayer)
        {
            UnbindAlertUI();    // NPC・他人のプレイヤーは赤帯を出さない
            return;
        }

        if (_alertModel == null) BindAlertUI();
    }

    // 赤帯UIのModelを作って公開する
    private void BindAlertUI()
    {
        _alertModel = new CS_UIAttentionAlertModel();
        _alertModel.SetAlerted(_alert.isAlerted);   // Bind前に今の状態を入れておく
        _alertModel.Bind();

        _alert.onAlertChanged += HandleAlertChanged;
    }

    private void UnbindAlertUI()
    {
        if (_alertModel == null) return;

        _alert.onAlertChanged -= HandleAlertChanged;
        _alertModel.Dispose();
        _alertModel = null;
    }

    // 警戒状態が変わったら、警戒中かどうかをModelに渡す
    private void HandleAlertChanged(CSE_PlayerAlertState state)
    {
        _alertModel?.SetAlerted(state != CSE_PlayerAlertState.None);
    }
}
```

### プレハブへの追加

1. `Assets/Programmer/Prefab/Entity/Player/Player.prefab` を開く
2. ルート（`CS_Player` と `CS_PlayerAlert` が付いている GameObject）に `CS_PlayerAttentionAlertUI` を追加する
3. Inspector で設定する項目はありません

UICanvas 側（`UICanvas.prefab` の `AttentionAlert`）は PR #373 で設定済みなので、触らなくて大丈夫です。

---

## Unity での確認

1. インゲームのシーン（ソロでよい）を再生する
2. 警察の視界に入る、または防犯装置（レーザーなど）に引っ掛かる
   → 画面の上下に赤帯が出て、左から右へループする
3. 見失われて捜索中になっても、赤帯は出たまま
4. 警戒状態が解けると（最後に見つかってから既定20秒、または警察に倒された時）赤帯が消える
5. マルチで確認できる場合は、**他のプレイヤーが見つかっても自分の画面に赤帯が出ない** ことを見る

### 見た目の調整（View の Inspector）

| 項目 | 既定 | 説明 |
|---|---|---|
| UVが横方向へ1周する秒数 | 2 | 小さいほど速く流れる |
| 横方向の移動量 | 1 | 1 で 1周 |
| 下画像の開始位置をずらす | 0 | 0.5 なら上下で半周ずらす |
| 移動方向 | ON | ON で左から右（仕様どおり）、OFF で右から左 |

---

## 「警戒状態!」「捜索中」の表示（`CS_UIPlayerAlertModel`）

仕様の元：Confluence「UI」②警戒状態。赤帯と同じく、`CS_PlayerAttentionAlertUI` がローカルプレイヤーの時に Model を作って Bind します。

| 状態（`CSE_PlayerAlertState`） | 表示 |
|---|---|
| `None` | 表示しない |
| `Detected`（見つかって追われている） | 「警戒状態!」（View の `_detectedRoot`） |
| `Searching`（見失われて捜索されている） | 「捜索中」（View の `_searchingRoot`） |

| クラス | ファイル | 役割 |
|---|---|---|
| `CS_UIPlayerAlertModel` | `UI/UI_PlayerAlert/` | 状態を持つ。`Bind()` で公開する（1つだけ） |
| `CS_UIPlayerAlertPresenter` | `UI/UI_PlayerAlert/` | Bind された Model を購読して View に渡す。UICanvas.prefab に付いている |
| `CS_UIPlayerAlertView` | `UI/UI_PlayerAlert/` | 状態に合わせて表示を切り替える |

```csharp
var model = new CS_UIPlayerAlertModel();
model.SetAlertState(alert.alertState);  // Bind前に今の状態を入れておく
model.Bind();
alert.onAlertChanged += model.SetAlertState;  // 実際はハンドラ経由で渡している
// 使い終わったら Dispose()（Unbind もされる）
```

Unity での確認：見つかった時に「警戒状態!」、見失われた時に「捜索中」が出て、警戒状態が解けたら消えること。
