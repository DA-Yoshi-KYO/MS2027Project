# テト：HDRPトゥーン表現と攻撃プレビュー

制作者：吉本竜 / 制作日：2026-09-25

## 使い方

1. `Scenes/Teto_EndfieldPreview.unity` を開く。
2. Unityを再生すると、テトが構えから右パンチを繰り返す。
3. Game画面にフォーカスしてSpaceキーを押すと、手動でも攻撃できる。

`CS_TetoAttackPreview` の Automatic を外すと手動のみ。Intervalで自動攻撃の間隔を変更できる。
攻撃中の連打は無視し、終わると待機へ戻る。攻撃判定・ダメージ処理は含まない見た目のプレビュー。

## ファイル

- `Shaders`：HDRP専用のトゥーンシェーダーと輪郭線。
- `EndfieldMaterials`：肌・顔・髪・目・衣装ごとの専用素材。元のMaterialsは保持する。
- `Animations`：ボーンを直接動かす待機と攻撃のAnimationClip。
- `Scripts`：照明・頭の方向をシェーダーへ渡す処理と攻撃再生。
- `Profiles`：確認用の固定露出・ACES・弱いBloom。
- `Prefabs/Teto_Endfield.prefab`：別シーンでも使える設定済みテト。
- `Editor/CSED_TetoEndfieldSetup.cs`：素材・クリップ・プレビューシーンの再生成。

別シーンではPrefabを配置し、CS_TetoToonLightingのKey LightへDirectional Lightを設定する。
露出が大きく異なるシーンでは材質のBrightnessまたはVolumeのExposureも調整する。

## 表現と前提

[参考記事](https://techartnomad.tistory.com/735)の考え方を、Unity 6.3 / HDRP 17.3向けに実装した。
色付きの三段階陰影、上方の補助光、GGX反射、髪の帯状反射、リムライト、肌の逆光補正、
HDRPの主光源シャドウ、細い輪郭線を組み合わせている。

記事のキャラクター専用SDF・材質マスク・ランプテクスチャはこのテトにはないため、
顔は頭の向きと法線による近似、材質は部位別パラメーターで調整する。
元作品の完全再現ではなく、このモデルに合わせた表現。複数光源・レイトレーシング・
モーションベクター用の専用パスは実装していない。確認シーンは残像を避けるためSMAAを使用する。

モーションはこのFBXのボーン名・ゼロ回転の基準姿勢に合わせた専用クリップ。
HumanoidのAvatarや外部のアニメーション素材には依存しない。

## 再生成

停止中に `Tools > Teto > Apply Endfield Look and Attack` を実行する。
現在のシーン内の `Teto_GameReady` に適用し、このフォルダの素材・クリップ・Prefab・確認用シーンを更新する。
再生成は調整値を既定値に戻すので、手作業で調整した素材やクリップを残す場合は先に複製する。
