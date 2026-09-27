# UI Model の使い方まとめ

UI は MVP 構成（Model / Presenter / View）になっていますが、**ゲーム側のスクリプトが触るのは Model だけ** です。
Presenter / View は UI のプレハブに最初から付いているので、コードから触る必要はありません。

```
あなたのスクリプト ──new / Bind / Set〇〇──▶ Model ──(自動で通知)──▶ Presenter ──▶ View(画面)
```

| UI | Model クラス | 紐づけ方 | 置き場所（プレハブ） |
|---|---|---|---|
| プレイヤーHP | `CS_UIPlayerHpModel` | `Bind(プレイヤー番号)` | `UICanvas` の `HpGaugePlayer0〜3` |
| 必殺技ゲージ | `CS_UISpecialGaugeModel` | `Bind(プレイヤー番号)` | `UICanvas` の `SpecialGaugePlayer0〜3` |
| 敵HP | `CS_UIVillainHpModel` | `Bind(親のTransform)` | `VillainHpBarCanvas`（Villain の直下に置く） |
| アイテムスロット | `CS_UIItemSlotModel` | `Bind()`（番号なし） | `UICanvas` の `ItemSlot` |

プレハブはすべて `Assets/Programmer/Prefab/UI/` にあります。

---

## 共通ルール

1. **`new` で生成する**（Model は MonoBehaviour ではないので `AddComponent` しない）
2. **`Bind` で公開する** → 対応する UI が勝手に Model を見つけて表示を始める
3. **値の変更は `Set〇〇` / `Add〇〇` で行う** → UI が自動で更新される
4. **使い終わったら `Dispose()`**（`OnDestroy` などで）。多くの Model は `Dispose` 内で `Unbind` も行う
5. 値の読み取りは `〇〇.CurrentValue`（R3 の `ReadOnlyReactiveProperty`）
6. 同じ番号（同じ Transform）に別の Model を `Bind` すると、**後から Bind した方で上書き** されます

- 全 Model の基底クラスは `CS_BaseModel`（`IDisposable` を実装しているだけ）
- 動作サンプル：`Assets/Programmer/Script/UI/CS_TestUI.cs`

---

## ⚠ プレイヤー番号の割り当て（未実装・要対応）

プレイヤーHP・必殺技ゲージは `Bind(プレイヤー番号)` で紐づけますが、
**ゲーム側にはまだ「プレイヤー番号（0〜3）」を決める仕組みがありません。** UI を使う前にこれを作る必要があります。

### なぜ `[SerializeField]` では決められないのか

- プレイヤーは `CS_PlayerSpawner` が **同じプレハブから人数分 `Instantiate`** している
- プレハブに `[SerializeField] int _playerNumber` を置いても、**全員が同じ番号** になってしまう
- オンライン（Netcode）なので、番号は **サーバーが決めて全クライアントに同期** しないといけない
  （どのクライアントの画面にも、全員分のゲージを出すため）
- `OwnerClientId` は使えない。ホストが 0、クライアントが 1, 2…と振られるが、再接続のたびに増えていくので 0〜3 に収まらない

### やること

| # | 内容 | 対象 |
|---|---|---|
| 1 | プレイヤー番号を `NetworkVariable<int>` で持つ（書き込みはサーバーのみ、読み取りは全員） | `CS_Player` |
| 2 | サーバーが生成時に番号を割り当てる（`Spawn` / `SpawnAsPlayerObject` の前に値を入れる）。オフライン時は 0 | `CS_PlayerSpawner.Spawn()` |
| 3 | 切断で空いた番号を再利用するか決める（単純に `_spawnCount` を使うと、抜けて入り直した時に 4 以上になる） | `CS_PlayerSpawner` |
| 4 | ゲーム側の値を UI の Model に流すコンポーネントを作り、プレイヤーのプレハブに付ける | 新規（例：`CS_PlayerHudBinder`） |
| 5 | HP の型をそろえる。ゲーム側（`CS_PlayerHealth`）は `float`、`CS_UIPlayerHpModel` は `int` | 丸めるか Model を float にするか決める |

### 4. のイメージ

番号が同期された後（`OnNetworkSpawn`）で Model を作って Bind し、ゲーム側のイベントを受けて値を流します。
**全クライアントで、全プレイヤー分** これが動くので、どの画面にも全員のゲージが出ます。

```csharp
// ※ CS_Player.playerNumber は未実装（上の 1, 2 で作る想定）
public class CS_PlayerHudBinder : NetworkBehaviour
{
    private CS_PlayerHealth _health;
    private CS_PlayerSpecialGauge _gauge;

    private CS_UIPlayerHpModel _hpModel;
    private CS_UISpecialGaugeModel _gaugeModel;

    public override void OnNetworkSpawn()
    {
        _health = GetComponent<CS_PlayerHealth>();
        _gauge  = GetComponent<CS_PlayerSpecialGauge>();
        int number = GetComponent<CS_Player>().playerNumber;

        _hpModel = new CS_UIPlayerHpModel(Mathf.RoundToInt(_health.maxHp), Mathf.RoundToInt(_health.currentHp));
        _hpModel.Bind(number);

        _gaugeModel = new CS_UISpecialGaugeModel(_gauge.maxGauge, _gauge.currentGauge);
        _gaugeModel.Bind(number);

        _health.onHpChanged += HandleHpChanged;
        _gauge.onGaugeChanged += HandleGaugeChanged;
    }

    public override void OnNetworkDespawn()
    {
        _health.onHpChanged -= HandleHpChanged;
        _gauge.onGaugeChanged -= HandleGaugeChanged;
        _hpModel?.Dispose();
        _gaugeModel?.Dispose();
    }

    private void HandleHpChanged(float current, float max)
    {
        _hpModel.SetMaxHp(Mathf.RoundToInt(max));
        _hpModel.SetHp(Mathf.RoundToInt(current));
    }

    private void HandleGaugeChanged(float current, float max)
    {
        _gaugeModel.SetGauge(current);   // ※ CS_UISpecialGaugeModel は最大値を後から変えられない
    }
}
```

- 番号の `NetworkVariable` は **Spawn 前にサーバーが値を入れておけば**、各クライアントの `OnNetworkSpawn` の時点で読めます
- オフラインのテストシーン（NetworkManager が動いていない）では `OnNetworkSpawn` が呼ばれないので、
  `CS_PlayerHealth` と同じように `Start` でオフライン用の処理を別に書く必要があります
- 以下の各章のコード例に出てくる `_playerNumber` は、ここで割り当てた番号のことです

---

## 1. プレイヤーHP（`CS_UIPlayerHpModel`）

```csharp
// _playerNumber は「プレイヤー番号の割り当て」で決めた番号（SerializeField ではない）
private CS_UIPlayerHpModel _hpModel;

void Init(int playerNumber, int maxHp)
{
    _hpModel = new CS_UIPlayerHpModel(maxHp);        // 満タンで生成
    // new CS_UIPlayerHpModel(maxHp, 現在HP);         // 途中のHPから始める場合
    _hpModel.Bind(playerNumber);                     // 対応するゲージが表示される
}

public void TakeDamage(int damage)
{
    _hpModel.SetHp(_hpModel.currentHp.CurrentValue - damage);
}

void OnDestroy()
{
    _hpModel?.Dispose();                             // Unbind も自動 → ゲージは非表示
}
```

| メソッド / プロパティ | 説明 |
|---|---|
| `new CS_UIPlayerHpModel(maxHp)` | 満タンで生成（maxHp は最低1に補正） |
| `new CS_UIPlayerHpModel(maxHp, currentHp)` | 指定HPで生成 |
| `Bind(playerNumber)` | 何番のプレイヤーのHPとして公開するか（0〜3） |
| `Unbind()` | 公開をやめる（ゲージが非表示になる） |
| `SetHp(hp)` | HPを設定。0〜最大HPに自動で丸められる |
| `SetMaxHp(max)` | 最大HPを変更。現在HPが超えていたら切り詰める |
| `currentHp.CurrentValue` / `maxHp.CurrentValue` | 現在HP / 最大HP を読む |
| `Dispose()` | 後片付け（Unbind も行う） |

- ゲージは最初は非表示（CanvasGroup の alpha = 0）で、**Bind された時に表示** されます
- 2人プレイなら 0, 1 だけ Bind すれば、2, 3 のゲージは出ません

---

## 2. 必殺技ゲージ（`CS_UISpecialGaugeModel`）

プレイヤーHPとほぼ同じ使い方です。値は `float` です。

```csharp
private CS_UISpecialGaugeModel _gaugeModel;

void Awake()
{
    _gaugeModel = new CS_UISpecialGaugeModel(maxGauge: 300f, initGauge: 0f);
    _gaugeModel.Bind(_playerNumber);
}

void OnHitEnemy()
{
    _gaugeModel.AddGauge(10f);                             // 溜める
}

void UseSpecial()
{
    if (_gaugeModel.currentGauge.CurrentValue >= _gaugeModel.maxGauge)
        _gaugeModel.SetGauge(0f);                          // 使ったら空にする
}

void OnDestroy() => _gaugeModel?.Dispose();
```

| メソッド / プロパティ | 説明 |
|---|---|
| `new CS_UISpecialGaugeModel(maxGauge, initGauge)` | 生成（**引数は2つとも必須**） |
| `Bind(playerNumber)` / `Unbind()` | 公開 / 公開をやめる（ゲージの表示・非表示） |
| `SetGauge(value)` | ゲージ値を設定。0〜maxGauge に丸められる |
| `AddGauge(value)` | 加算（マイナスを渡せば減算） |
| `currentGauge.CurrentValue` | 現在値を読む |
| `maxGauge` | 最大値（普通のプロパティ。**後から変更はできない**） |
| `Dispose()` | 後片付け（Unbind も行う） |

- `maxGauge` に 0 を渡すと表示で 0 除算になるので、必ず正の値を渡してください

---

## 3. 敵HP（`CS_UIVillainHpModel`）

番号ではなく **Villain 自身の Transform** で紐づけます。複数の Villain を同時に出しても混ざりません。

**準備**：`VillainHpBarCanvas.prefab`（World Space Canvas）を **Villain のプレハブの直下** に置く。

```csharp
public class CS_Villain : MonoBehaviour
{
    [SerializeField] private int _maxHp = 300;
    private CS_UIVillainHpModel _hpModel;

    void Awake()
    {
        _hpModel = new CS_UIVillainHpModel(_maxHp);
        _hpModel.Bind(transform);   // 直下の HP バーが自動で拾う
    }

    public void TakeDamage(int damage)
    {
        _hpModel.SetHp(_hpModel.currentHp.CurrentValue - damage);
    }

    void OnDestroy() => _hpModel?.Dispose();
}
```

| メソッド / プロパティ | 説明 |
|---|---|
| `new CS_UIVillainHpModel(maxHp)` / `(maxHp, currentHp)` | 生成 |
| `Bind(transform)` | どの Villain の HP か公開する（**HPバーの直接の親** の Transform を渡す） |
| `Unbind()` | 公開をやめる（バーの更新が止まる。非表示にはならない） |
| `SetHp(hp)` / `SetMaxHp(max)` | プレイヤーHPと同じ |
| `currentHp.CurrentValue` / `maxHp.CurrentValue` | 読み取り |
| `Dispose()` | 後片付け（Unbind も行う） |

- Bind は `Awake` / `Start` どちらでも OK（HPバーとどちらが先でも紐づく）
- HPバーは Villain の子なので、Villain を Destroy すれば一緒に消えます

---

## 4. アイテムスロット（`CS_UIItemSlotModel`）

スロットは画面に1つだけの前提で、番号はありません。
**Bind は最初に1回だけ**。あとは `SetIcon` するだけで表示が変わり、`null`（`ClearIcon()`）で非表示になります。

アイコン画像はアイテムデータ（`CSO_ItemData.icon`）に入っているので、それを渡します。

```csharp
private CS_UIItemSlotModel _itemModel;

void Awake()
{
    _itemModel = new CS_UIItemSlotModel();   // 最初はアイコン無し = 非表示
    _itemModel.Bind();
}

void OnItemChanged(CSO_ItemDataCarriable item)   // 拾った / 使った
{
    _itemModel.SetIcon(item != null ? item.icon : null);   // null ならスロットは非表示
}

void OnDestroy() => _itemModel?.Dispose();
```

| メソッド / プロパティ | 説明 |
|---|---|
| `new CS_UIItemSlotModel()` / `(initialIcon)` | 生成（初期アイコン省略時は null = 非表示） |
| `Bind()` | このModelをスロットの表示元として公開する |
| `Unbind()` | 公開をやめる（スロットは非表示） |
| `SetIcon(sprite)` | アイコンを設定。**Bind 後でもすぐ表示に反映される**。null で非表示 |
| `ClearIcon()` | スロットを空にする（`SetIcon(null)` と同じ） |
| `icon.CurrentValue` | 現在のアイコンを読む |
| `Dispose()` | 後片付け（Unbind も行う） |

- `SetIcon` と `Bind` の順番はどちらが先でも OK
- ⚠ 自分のプレイヤーの分だけ表示してください（オンライン時は `IsOwner` のプレイヤーだけで Bind する）
- ⚠ `CS_PlayerItemSlot` の所持アイテムはまだネット同期されていないため、`onItemChanged` はサーバー（ホスト）とオフラインでしか呼ばれません。
  リモートのクライアントで表示するには、所持アイテムの同期が別途必要です

---

## 注意点（Bind のタイミング）

基本の流れは **「UI（UICanvas）はシーンに最初からある → Player / Enemy は後から生成して、その `Awake` で Bind」** です。
この流れでの各 UI の状況は次のとおりです。

| UI | 後から生成して Bind | 死亡 → Dispose → 再生成して Bind |
|---|---|---|
| プレイヤーHP / 必殺技ゲージ | ✅ 表示される | ✅ 消えて、また表示される |
| 敵HP | ✅（HPバーは Villain と一緒に生成される） | ✅ |
| アイテムスロット | ✅ | ✅ |

### 補足：UI より先に Bind した場合

シーンに最初から置いたオブジェクトで Bind する場合など、UI より先に Bind されると
**プレイヤーHP・必殺技ゲージは値は反映されるのにゲージが非表示（alpha 0）のまま** になります。
（`OnEnable` で Model を見つけた時に `SetVisible(true)` していないため）
後から生成する前提なら通常は起きませんが、テスト用に最初からシーンに置く場合は注意してください。
