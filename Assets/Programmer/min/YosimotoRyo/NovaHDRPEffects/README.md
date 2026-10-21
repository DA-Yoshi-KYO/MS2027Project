# Nova HDRP エフェクトサンプル

`Prefabs` 内の5種類をシーンにドラッグして使えます。Particle Systemはループ再生します。

| Prefab | 内容 |
| --- | --- |
| 01_Flame | 炎、火の粉、HDRP Litの煙 |
| 02_ArcaneCircle | 回転する魔法陣、青い光の粒 |
| 03_Shockwave | 拡大する衝撃波、閃光、放射状の火花 |
| 04_Sparks | 重力で落ちる金色の火花 |
| 05_HeatDistortion | 熱による背景の歪み、熱源の発光 |

マテリアルは `Materials`、生成したテクスチャは `Textures` にあります。NovaのマテリアルInspectorで色調・透過・テクスチャなどを変更できます。色と発生量、速度、大きさは各Particle Systemで調整します。

歪みにはHDRP AssetとカメラのFrame SettingsでDistortionを有効にする必要があります。サンプル用カメラでは有効にしています。既存のカメラがあるシーンでは、その設定は変更しません。

`Tools > Nova > HDRP > Create Effect Samples` は現在のシーンにサンプルを配置します。同名のサンプルがある場合は追加せず選択します。編集中のシーンは切り替えません。

`Capture Effect Samples` はサンプルを0.65秒時点へ進め、メインカメラから `Preview.png` を生成します。

自作のプロシージャルテクスチャです。外部の画像素材は使用していません。
