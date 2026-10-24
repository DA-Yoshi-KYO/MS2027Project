# ロックオン UI の使い方（`CS_UILockOnModel`）

UI側は MVP 構成になっていますが、**使う側が触るのは Model だけ** です。
Presenter / View は `UICanvas.prefab` に最初から付いているので、コードから触る必要はありません。

```
あなたのスクリプト ──new / Bind / SetTarget──▶ Model ──(自動で通知)──▶ Presenter ──▶ View(マーカー)
                                                                  └ 毎フレーム 対象の位置を画面上の位置に変換
```

やることは基本この4つだけです。

1. `SetLocalPlayerNumber(自分の番号)` で「画面に出すのは何番か」を登録する
2. `new` で Model を作る
3. `Bind(プレイヤー番号)` で「何番のプレイヤーのロックオンか」を公開する
4. ロックオンしたい相手が決まったら `SetTarget(Transform)`、外す時は `ClearTarget()` → 使い終わったら `Dispose()`

今は **奇襲できる悪人をロックオンする** 処理（`CS_PlayerLockOnUI`）が Player.prefab に付いているので、
奇襲のロックオンについては何もしなくても表示されます。

---

## 準備（シーン側）

- `Assets/Programmer/Prefab/UI/InGame/UICanvas.prefab` をシーンに置く
- 中の `LockOn` > `LockOnRoot` に Presenter / View が付いている。マーカーの画像は `LockOnMarker` > `LockOn`
- マーカーは **画面に1つだけ** です。表示されるのは `localPlayerNumber` と同じ番号で Bind された Model の対象だけ
- UICanvas は画面に重ねる Canvas（Screen Space）なので、マーカーは距離に関係なく同じ大きさで、壁に隠れることもありません

---

## 表示のしくみ（ここだけ注意）

Presenter は次の2つがそろった時に表示を始めます。

- `CS_UILockOnModel.SetLocalPlayerNumber(番号)` で自分の番号が登録されている
- その番号で `Bind(番号)` された Model があり、`SetTarget` で対象が設定されている

Presenter は **Bind の通知を受けた瞬間に「自分の番号か」を判定** します。
なので **`SetLocalPlayerNumber` は必ず `Bind` より先に呼んでください。**

- マーカーの位置は **対象の `Transform` の位置 + Presenter の `_worldOffset`（既定は 1m 上）**
  対象側に `LockOnPosition` などの空オブジェクトを作ってそれを渡す場合は、`_worldOffset` を 0 にしてください（二重にずれます）
- 対象が Destroy された時は、Presenter が自動で `ClearTarget()` します
- 対象がカメラの後ろにいる時は非表示になります
- 画面外にいる時は `_offscreenMode` で「隠す」か「画面の端に寄せる」かを選べます
- ロックオンした瞬間（非表示→表示になった時）に、マーカーが少し大きい状態から縮む演出が入ります

---

## 奇襲のロックオン（`CS_PlayerLockOnUI`）

Player.prefab に付いている `CS_PlayerLockOnUI` が、今奇襲できる悪人を自動でロックオンします。

- 自分の画面のプレイヤー（`CS_Player.isLocalPlayer`）だけが Model を作って Bind する（NPC・他のプレイヤーは何もしない）
- `_searchInterval` 秒ごと（既定 0.1 秒）に `CS_PlayerAmbush.FindAmbushTarget()` を呼び、一番近い「奇襲できる悪人」をロックオンする。いなければ外す
- 「奇襲できる」の条件は、攻撃した時の奇襲の判定（`CS_PlayerAmbush.CanAmbush`）と同じ
  1. 悪人がこちらに気づいていない（臨戦態勢で自分を狙っていない）
  2. 悪人との水平距離が `_range`（既定 3m）以内
  3. 背後（`_backAngle`）か上（`_aboveHeight`）を取っている
- 悪人を探すレイヤーは `CS_PlayerAmbush` の `_villainLayers`（既定は全部）

### 「気づかれていない」をクライアントで判定するしくみ

悪人の臨戦態勢・標的はサーバーでしか動いていないので、`CS_VillainCombat` が **今狙っているプレイヤーの NetworkObjectId を NetworkVariable で同期** しています。

- `CS_VillainCombat.IsTargeting(プレイヤーのroot)` で、そのプレイヤーを狙っているかが全クライアントで分かる
- サーバー・オフラインでは標的をそのまま見る（今までの奇襲の判定と同じ結果）
- クライアントでは同期を待つので、ほんの少し（1フレームほど）遅れることがあります

### 悪人の視野

悪人は犯罪中・巡回中、**正面の視野（`CS_VillainCombat._noticeAngle`、既定は左右 120 度）にいるプレイヤーにだけ気づきます。**
背後から臨戦態勢範囲に入っても気づかないので、背後を取れば奇襲（とロックオン）ができます。

- `_noticeAngle` は `CS_PlayerAmbush._backAngle`（既定 120 度）以下にしておく（大きくすると、背後を取っても気づかれて奇襲できない）
- 攻撃された時の反撃、追跡中の標的探し、陽動ホログラム・警察への気づき方は今まで通り（向きに関係なく気づく）

---

## コード例（奇襲以外でロックオンしたい時）

奇襲以外の理由でロックオンしたい場合も、Model の使い方は同じです。
ただしマーカーは画面に1つなので、**同じ番号に別の Model を Bind すると後から Bind した方で上書き** されます。
`CS_PlayerLockOnUI` と同時に使う場合は、新しく Model を作らず `CS_PlayerLockOnUI` に対象の決め方を足してください。

```csharp
// 自分のプレイヤーだけで呼ぶ
CS_UILockOnModel.SetLocalPlayerNumber(player.playerNumber);   // Bindより先に
_lockOnModel = new CS_UILockOnModel();
_lockOnModel.Bind(player.playerNumber);

// 対象が決まったら
_lockOnModel.SetTarget(enemy.transform);

// 外す時
_lockOnModel.ClearTarget();

// 使い終わったら(OnNetworkDespawn / OnDestroy)
_lockOnModel.Dispose();
```

---

## Inspector で変えられる値

**UICanvas > LockOn > LockOnRoot（`CS_UILockOnPresenter`）**

| 項目 | 内容 | 既定値 |
|---|---|---|
| `_canvasRect` / `_canvas` | 位置の変換に使う Canvas（未設定なら親から探す） | UICanvas |
| `_targetCamera` | 位置の計算に使うカメラ（未設定なら `Camera.main`） | 未設定 |
| `_worldOffset` | 対象の位置からのずらし幅 | (0, 1, 0) |
| `_offscreenMode` | 画面外の時に `Hide`（隠す）か `ClampToEdge`（端に寄せる）か | Hide |
| `_edgePadding` | 端に寄せる時の画面端からの余白（px） | 40 |

**UICanvas > LockOn > LockOnRoot（`CS_UILockOnView`）**

| 項目 | 内容 | 既定値 |
|---|---|---|
| `_markerRect` / `_markerImage` | マーカーの RectTransform と Image | LockOnMarker / LockOn |
| `_lockOnAnimDuration` | ロックオンした時に縮む演出の時間（秒）。0 で演出なし | 0.15 |
| `_lockOnStartScale` | 縮む演出の最初の大きさ（倍） | 1.4 |

マーカーの見た目（画像・色・大きさ）は `LockOnMarker` > `LockOn` の Image で変えます。

**Player.prefab（`CS_PlayerLockOnUI` / `CS_PlayerAmbush`）**

| 項目 | 内容 | 既定値 |
|---|---|---|
| `CS_PlayerLockOnUI._searchInterval` | 奇襲できる悪人を探し直す間隔（秒） | 0.1 |
| `CS_PlayerAmbush._range` / `_backAngle` / `_aboveHeight` | 奇襲の条件（ロックオンも同じ条件を使う） | 3m / 120度 / 1.5m |
| `CS_PlayerAmbush._villainLayers` | 悪人を探すレイヤー | 全部 |

---

## API 早見表

| メソッド / プロパティ | 説明 |
|---|---|
| `new CS_UILockOnModel()` | 生成。対象なしで始まる |
| `CS_UILockOnModel.SetLocalPlayerNumber(番号)` | 画面に出す番号（自分の番号）を登録する（static）。**Bind より先に呼ぶ** |
| `CS_UILockOnModel.localPlayerNumber` | 登録済みの自分の番号（未登録なら -1） |
| `CS_UILockOnModel.TryGet(番号, out model)` | その番号で Bind 済みの Model を取得する |
| `Bind(プレイヤー番号)` / `Unbind()` | 何番のプレイヤーのロックオンとして公開する / やめる（Unbind で対象も外れる） |
| `SetTarget(Transform)` | ロックオン対象を設定する（null なら外すのと同じ） |
| `ClearTarget()` | ロックオンを外す |
| `currentTarget` / `target` | 今の対象（`target` は購読できる ReadOnlyReactiveProperty） |
| `isLockedOn` | ロックオン中か |
| `Dispose()` | 後片付け。Unbind も自動で行う |
| `CS_PlayerAmbush.FindAmbushTarget()` | 今奇襲できる一番近い悪人（いなければ null。全クライアントで呼べる） |
| `CS_VillainCombat.IsTargeting(プレイヤーのroot)` | その悪人がそのプレイヤーに気づいて狙っているか（全クライアントで参照可） |

---

## まとめ

- 奇襲できる悪人のロックオンは **Player.prefab の `CS_PlayerLockOnUI` が自動でやる** ので、何もしなくてよい
- 自分で使う時は **`SetLocalPlayerNumber` → `new` → `Bind` → `SetTarget`** の順
- マーカーの位置・画面外の扱いは UICanvas の `LockOnRoot` の Presenter で調整する
- **Presenter / View のスクリプトは触らなくてOK**（UICanvas.prefab に付いています）
