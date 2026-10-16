Teto GameReady Unity HDRP v5

目的
- 元VRMをポリゴン削減せずFBX化
- Skeleton / Skin Weight / BindPose / BlendShapeをFBXへ保持
- 元VRM内の35画像を個別PNGとしてそのまま外出し
- Unity 6.x HDRPでHDRP/Lit Materialを自動生成・Remap
- RigはGeneric。Humanoid化は自動では行わない

v5の重要修正
- v4でSkin ClusterのTransformを誤ってmesh bind(identity)として書いていたため、Unityで首・腕・胴体などがボーン方向へ伸びていた。
- v5ではFBXの一般的なSkin Cluster構造に合わせ、
    Transform              = boneWorld^-1 * meshWorld
    TransformLink          = boneWorld
    TransformAssociateModel= skeletonRootWorld
  とした。
- このVRMではmeshWorldとskeletonRootWorldがidentityなので、Transformは元VRMのinverseBindMatrixそのものになる。
- 全使用ボーンについて TransformLink * Transform = identity になることを数値検証済み。

元モデル統計
- Mesh: 3 (Face / Body / Hair)
- Vertices: 4069 / 9378 / 16167
- Triangles: 7128 + 15401 + 20436 = 42965
- Skeleton joints: 233
- Skinで実際に使用されるjoint: 129
- Materials: 21
- BlendShapes: 57
- PNG images: 35

Unityへの導入
1. Assets内に以前の Teto_GameReady_UnityHDRP フォルダがある場合は削除する。
2. この Teto_GameReady_UnityHDRP フォルダだけをAssets以下へコピーする。
3. UnityのImportが終わるまで待つ。
4. Tools > Teto GameReady > Rebuild HDRP Materials を実行する。
5. Consoleに以下のような表示が出ることを確認する。
   Teto_GameReady: Import OK / SkinnedMeshRenderer=3 / BlendShapes=57 / Transforms=...
6. Models/Teto_GameReady.fbx をSceneまたはHierarchyへドラッグする。

フォルダ
- Models/Teto_GameReady.fbx : FBX本体
- Textures/*.png             : 元VRMから無変換で取り出したPNG
- MaterialMap.json           : MaterialとTextureの対応
- Editor/TetoGameReadyImporter.cs : HDRP/Lit Material自動生成・自動Remap
- Validation.json            : 変換後FBXの構造検証結果

Validationの主な確認項目
- 42965 triangles
- 233 joints
- 57 BlendShapes
- Object ID重複なし
- Connection参照切れなし
- FBX括弧構造正常
- Hips存在
- BindPose存在
- Cluster Transform / TransformLink / TransformAssociateModel存在
- bind matrix residual = 0.0

注意
- 今回は前回までの自作Bind行列の誤りを修正した版。
- Unity Editorそのものはこの実行環境では起動できないため、最終レンダリング確認だけはUnity側で行う必要がある。
- Humanoid Avatar化は別工程。まずGenericでSkinned Meshが正しく見えることを確認する。
