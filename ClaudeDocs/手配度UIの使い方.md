# 手配度 UI の使い方（`CS_UIWantedLevelModel`）

UI側は MVP 構成になっていますが、**使う側が触るのは Model だけ** です。
Presenter / View はUIのプレハブに最初から付いているので、コードから触る必要はありません。

```
あなたのスクリプト ──new / SetLevel──▶ Model ──(自動で通知)──▶ Presenter ──▶ View(星アイコン)
```

やることは基本この4つだけです。

1. `SetLocalPlayerNumber(自分の番号)` で「画面に出すのは何番か」を登録する
2. `new` で Model を作る
3. `Bind(プレイヤー番号)` で「何番のプレイヤーの手配度か」を公開する
4. 手配度が変わったら `SetLevel()` を呼ぶ → 使い終わったら `Dispose()`

---

## 準備（シーン側）

- `Assets/Programmer/Prefab/UI/InGame/UICanvas.prefab` をシーンに置く
- 中の `WantedLevelRoot` > `WantedLevel` に Presenter / View が付いている
- 手配度の表示は **画面に1つだけ** です。表示されるのは `localPlayerNumber` と同じ番号で Bind された Model だけ
  （HP UI のように「0〜3番のゲージが並ぶ」形ではありません）
- 表示は星アイコンで、最大手配度の数だけ並び、手配度の分だけ **左から色付き星** になります（0なら全部空星）

---

## 表示のしくみ（ここだけ注意）

Presenter は次の2つがそろった時に表示を始めます。

- `CS_UIWantedLevelModel.SetLocalPlayerNumber(番号)` で自分の番号が登録されている
- その番号で `Bind(番号)` された Model がある

Presenter は **Bind の通知を受けた瞬間に「自分の番号か」を判定** します。
なので **`SetLocalPlayerNumber` は必ず `Bind` より先に呼んでください。**
順番が逆だと、Bind の通知を見逃して表示されません。

- 他のプレイヤーの番号で Bind しても画面には出ません（全員分 Bind しても問題なし）
- 同じ番号に別の Model を Bind すると、後から Bind した方で上書きされます
- `Dispose()` / `Unbind()` で自分の番号の Model が外れると、星の表示は最後の状態のまま止まります

---

## プレイヤー番号の決まり方

`Bind` / `SetLocalPlayerNumber` に渡す番号は、ゲーム側では `CS_Player.playerNumber`（0〜3）を使います。

- `CS_PlayerSpawner.Spawn()` が、プレイヤーを生成した直後・Spawn する前に `AssignPlayerNumber(_spawnCount % 4)` で割り当てる
- `CS_Player` 側は `NetworkVariable<int>`（書き込みはサーバーのみ）なので、Spawn 時に全クライアントへ同期される
  → **`OnNetworkSpawn` の時点で、どのクライアントからも正しい番号が読める**
- オフラインのテストシーン（NetworkManager が動いていない）でプレイヤーを直接置いた場合は、既定値の 0 になる

> ※ 現在は `CS_PlayerSpawner` が「空いている一番小さい番号」を割り当てるようになったため、下の番号の重なりは起きません（NPC 対応時に変更）。
>
> ⚠ **番号が重なることがあります（HP UI なども同じ）。**
> 番号は「生成した順番を 4 で割った余り」なので、誰かが抜けて別の人が入ると、まだ使われている番号と重なる場合があります。
> 例：0〜3番の4人がいて1番が抜ける → 次に入った人は 5人目なので `4 % 4 = 0` 番になり、0番の人と重なる
> 同じ番号に Bind すると **後から Bind した方で上書き** されるので、0番の人の画面に新しく入った人の手配度が出てしまいます。
> 空いた番号を使い回す仕組みは `CS_PlayerSpawner` 側の対応が必要です（UI 側では防げません）。

---

## コード例（プレイヤーへの組み込み）

**→ 組み込み済み（秋野）**：`Assets/Programmer/Script/Entity/Player/CS_PlayerWantedLevelUI.cs` を `Player.prefab` に付けています。下の例とほぼ同じですが、自分かどうかは `IsOwner` ではなく `CS_Player.isLocalPlayer` で判定しています（NPC はホストで `IsOwner` が true になるため）。

ゲーム側の手配度は `CS_PlayerWantedLevel` がすでに持っています。

| ゲーム側 | 内容 |
|---|---|
| `level` | 今の手配度（NetworkVariable。全クライアントで読める） |
| `maxLevel` | 手配度の上限（Inspector の `_maxLevel`、既定 5） |
| `onLevelChanged(int 今の手配度)` | 手配度が変わった時。全クライアント・オフラインで発生する |

これを UI の Model に流すコンポーネントを、`CS_PlayerHpUI` と同じ形で作って Player.prefab に付けます（クラス名は仮）。

```csharp
using Unity.Netcode;
using UnityEngine;

// プレイヤーの手配度を手配度UI(CS_UIWantedLevelModel)へ反映するクラス(仮)
[RequireComponent(typeof(CS_Player))]
[RequireComponent(typeof(CS_PlayerWantedLevel))]
public class CS_PlayerWantedLevelUI : NetworkBehaviour
{
    private CS_Player _player;
    private CS_PlayerWantedLevel _wantedLevel;
    private CS_UIWantedLevelModel _wantedModel;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _wantedLevel = GetComponent<CS_PlayerWantedLevel>();
    }

    // オフライン(NetworkManagerが動いていない)のテストシーン用
    private void Start()
    {
        if (IsSpawned) return;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return;

        BindWantedLevelUI(true);
    }

    public override void OnNetworkSpawn()
    {
        BindWantedLevelUI(IsOwner);
    }

    public override void OnNetworkDespawn()
    {
        UnbindWantedLevelUI();
    }

    public override void OnDestroy()
    {
        UnbindWantedLevelUI();
        base.OnDestroy();
    }

    private void BindWantedLevelUI(bool isLocalPlayer)
    {
        // 自分の番号を先に登録しておく(Bindの通知を受けた時点でPresenterが自分の分だと判断できるように)
        if (isLocalPlayer)
        {
            CS_UIWantedLevelModel.SetLocalPlayerNumber(_player.playerNumber);
        }

        _wantedModel = new CS_UIWantedLevelModel(_wantedLevel.maxLevel);
        _wantedModel.SetLevel(_wantedLevel.level); // 途中参加などで、すでに手配度がある場合に合わせる
        _wantedModel.Bind(_player.playerNumber);

        _wantedLevel.onLevelChanged += HandleLevelChanged;
    }

    private void UnbindWantedLevelUI()
    {
        if (_wantedModel == null) return;

        _wantedLevel.onLevelChanged -= HandleLevelChanged;
        _wantedModel.Dispose();
        _wantedModel = null;
    }

    private void HandleLevelChanged(int level)
    {
        _wantedModel.SetLevel(level);
    }
}
```

- 手配度UIは画面に1つなので、`CS_PlayerItemSlotUI` のように **自分(Owner)だけ Bind する** 形でも動きます。
  上の例は HP UI とそろえて全員分 Bind しています（表示されるのは自分の番号だけなので問題なし）
- 増減の判断は `CS_PlayerWantedLevel` 側（サーバー）がやっているので、UI 側は **表示専用**。`SetLevel` で値を写すだけにしてください
- `CS_UIWantedLevelModel` は初期値 0 で始まるので、生成直後に `SetLevel(現在値)` で合わせておきます

---

## 最大手配度と星の数

- View（`CS_UIWantedLevelView`）は起動時に Inspector の `_defaultMaxLevel`（今は 5）の数だけ星を作ります
- Model を Bind すると、Model の `maxLevel` の数で星を作り直します（数が同じなら作り直さない）
- なので **ゲーム側の `_maxLevel` を変えた場合も、Model を `new CS_UIWantedLevelModel(maxLevel)` で作っていれば星の数は自動で追従** します
- 今は `CS_PlayerWantedLevel._maxLevel`・View の `_defaultMaxLevel` どちらも 5 です。最初の1フレームのチラつきを避けたいなら、両方の値をそろえておくと安心です
- 最大手配度は最低 1 に丸められます（0 以下を渡しても 1）

---

## API 早見表

| メソッド / プロパティ | 説明 |
|---|---|
| `new CS_UIWantedLevelModel(maxLevel)` | 最大手配度を指定して生成。手配度は 0 から始まる |
| `CS_UIWantedLevelModel.SetLocalPlayerNumber(番号)` | 画面に出す番号（自分の番号）を登録する（static）。**Bind より先に呼ぶ** |
| `CS_UIWantedLevelModel.localPlayerNumber` | 登録済みの自分の番号を読む（未登録なら -1） |
| `CS_UIWantedLevelModel.TryGet(番号, out model)` | その番号で Bind 済みの Model を取得する |
| `Bind(プレイヤー番号)` | 何番のプレイヤーの手配度として公開するか |
| `Unbind()` | 公開をやめる |
| `SetLevel(level)` | 手配度を設定。0〜maxLevel に自動で丸められる |
| `AddLevel(value)` | 手配度を加算（マイナスで減算）。範囲は同じく自動で丸められる |
| `SetMaxLevel(max)` | 最大手配度を変更。今の手配度が超えていたら切り詰める。星の数も作り直される |
| `currentLevel.CurrentValue` | 今の手配度を読む |
| `maxLevel.CurrentValue` | 最大手配度を読む |
| `Dispose()` | 後片付け。Unbind も自動で行う |

---

## まとめ

- **`SetLocalPlayerNumber` → `new` → `Bind` → `SetLevel`** の順で使う
- `SetLocalPlayerNumber` を Bind より後に呼ぶと表示されないので注意
- ゲーム側の値は `CS_PlayerWantedLevel.onLevelChanged` で受け取って `SetLevel` で写すだけ
- **Presenter / View のスクリプトは触らなくてOK**（UICanvas.prefab に付いています）
