# 敵HP UI の使い方（`CS_UIVillainHpModel`）

UI側は MVP 構成になっていますが、**使う側が触るのは Model だけ** です。
Presenter / View はHPバーのプレハブに最初から付いているので、コードから触る必要はありません。

```
Villainのスクリプト ──new / Bind / SetHp──▶ Model ──(自動で通知)──▶ Presenter ──▶ View(ゲージ・数値)
                                                     ↑ 子のHPバーが「親のTransform」でModelを拾う
```

やることはこれだけです。

1. `VillainHpBarCanvas.prefab` を Villain のプレハブの **直下** に置く
2. `new` で Model を作り、`Bind(transform)` する
3. HPが変わったら `SetHp()` を呼ぶ → Villain が消えるときに `Dispose()`

---

## 準備（プレハブ側）

- `Assets/Programmer/Prefab/UI/VillainHpBarCanvas.prefab` を **Villain のプレハブの直下** に置く
- これは **World Space の Canvas** なので、頭上などの位置に配置してください
- 番号などの設定は不要です

## コード例

```csharp
using UnityEngine;

public class CS_Villain : MonoBehaviour
{
    [SerializeField] private int _maxHp = 300;

    private CS_UIVillainHpModel _hpModel;

    void Awake()
    {
        // ① Model生成（満タンで始める）
        _hpModel = new CS_UIVillainHpModel(_maxHp);
        // 途中のHPから始めたいとき: new CS_UIVillainHpModel(_maxHp, 現在HP);

        // ② 自分のTransformでBind → 直下のHPバーが勝手に拾って表示する
        _hpModel.Bind(transform);
    }

    public void TakeDamage(int damage)
    {
        // ③ HPを減らす → バーが自動で更新される
        _hpModel.SetHp(_hpModel.currentHp.CurrentValue - damage);
    }

    void OnDestroy()
    {
        // ④ 後片付け（Bindも自動で外れる。HPバーは子なので Villain と一緒に消える）
        _hpModel?.Dispose();
    }
}
```

- `Bind(transform)` を呼ぶスクリプトは、**HPバーの直接の親のオブジェクト** に付けてください
- Bind は `Awake` / `Start` のどちらで呼んでもOK（HPバーとどちらが先に準備されても紐づきます）
- Villain ごとに Transform が違うので、**複数の Villain を同時に出しても混ざりません**

---

## API 早見表

| メソッド / プロパティ | 説明 |
|---|---|
| `new CS_UIVillainHpModel(maxHp)` | 満タンで生成 |
| `new CS_UIVillainHpModel(maxHp, currentHp)` | 指定HPで生成 |
| `Bind(transform)` | どのVillainのHPか公開する。直下のHPバーが自動で紐づく |
| `Unbind()` | 公開をやめる（バーの更新が止まる） |
| `SetHp(hp)` | HPを設定。0〜最大HPに自動で丸められる |
| `SetMaxHp(max)` | 最大HPを変更。現在HPが超えていたら切り詰める |
| `currentHp.CurrentValue` | 現在HPを読む |
| `maxHp.CurrentValue` | 最大HPを読む |
| `Dispose()` | 後片付け。Unbind も自動で行う |

- ダメージ・回復は `SetHp(currentHp.CurrentValue ± 値)` の形で書きます（範囲チェックは Model 側でやってくれます）
- **Presenter / View のスクリプトは触らなくてOK**（プレハブに付いています）
- 動作サンプル：`Assets/Programmer/Script/UI/CS_TestUI.cs`（Pで敵HPバー生成、Spaceでダメージ）
