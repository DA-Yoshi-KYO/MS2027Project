# プレイヤーHP UI の使い方（`CS_UIPlayerHpModel`）

UI側は MVP 構成になっていますが、**使う側が触るのは Model だけ** です。
Presenter / View はUIのプレハブに最初から付いているので、コードから触る必要はありません。

```
あなたのスクリプト ──new / SetHp──▶ Model ──(自動で通知)──▶ Presenter ──▶ View(ゲージ・数値)
```

やることは基本この3つだけです。

1. `new` で Model を作る
2. `Bind(プレイヤー番号)` で「何番のプレイヤーのHPか」を公開する
3. HPが変わったら `SetHp()` を呼ぶ → 使い終わったら `Dispose()`

---

## 準備（シーン側）

- `Assets/Programmer/Prefab/UI/UICanvas.prefab` をシーンに置く
- 中に `HpGaugePlayer0` 〜 `HpGaugePlayer3` があり、それぞれ `_playerNumber` が 0〜3 に設定済み
- **Bindされていない番号のゲージは自動で非表示** になります（2人プレイなら 0,1 だけBindすればOK）

## コード例

```csharp
using UnityEngine;

public class CS_Player : MonoBehaviour
{
    [SerializeField] private int _playerNumber = 0; // 0〜3
    [SerializeField] private int _maxHp = 300;

    private CS_UIPlayerHpModel _hpModel;

    void Awake()
    {
        // ① 生成（満タンで始める）
        _hpModel = new CS_UIPlayerHpModel(_maxHp);
        // 途中のHPから始めたいとき: new CS_UIPlayerHpModel(_maxHp, 現在HP);

        // ② 何番のプレイヤーのHPか公開 → 対応するゲージが勝手に表示される
        _hpModel.Bind(_playerNumber);
    }

    public void TakeDamage(int damage)
    {
        // ③ HPを変更するとゲージと数値が自動で更新される
        _hpModel.SetHp(_hpModel.currentHp.CurrentValue - damage);
    }

    void OnDestroy()
    {
        // ④ 後片付け（Bindも自動で外れる → ゲージは非表示になる）
        _hpModel?.Dispose();
    }
}
```

> ⚠ **Bind は `Awake` で行ってください。**
> 現状の Presenter は、ゲージが有効になった瞬間(`OnEnable`)に Model が見つからないと自分を非表示にし、
> その後に Bind されても再表示されないことがあります。
> `Start` 以降で Bind するとゲージが出ない可能性があるので、`Awake` で Bind しておくのが安全です。

---

## API 早見表

| メソッド / プロパティ | 説明 |
|---|---|
| `new CS_UIPlayerHpModel(maxHp)` | 満タンで生成 |
| `new CS_UIPlayerHpModel(maxHp, currentHp)` | 指定HPで生成 |
| `Bind(プレイヤー番号)` | 何番のプレイヤーのHPとして公開するか（0〜3）。ゲージが自動で紐づく |
| `Unbind()` | 公開をやめる（ゲージが非表示になる） |
| `SetHp(hp)` | HPを設定。0〜最大HPに自動で丸められる |
| `SetMaxHp(max)` | 最大HPを変更。現在HPが超えていたら切り詰める |
| `currentHp.CurrentValue` | 現在HPを読む |
| `maxHp.CurrentValue` | 最大HPを読む |
| `Dispose()` | 後片付け。Unbind も自動で行う |

- ダメージ・回復は `SetHp(currentHp.CurrentValue ± 値)` の形で書きます（範囲チェックは Model 側でやってくれます）
- **Presenter / View のスクリプトは触らなくてOK**（プレハブに付いています）
- 動作サンプル：`Assets/Programmer/Script/UI/CS_TestUI.cs`（↑↓キーでHP増減）
