using UnityEditor;
using UnityEngine;

// CI(Generate Designer Meta Files ワークフロー)から batchmode で呼ばれる。
// ビルドは行わず、インポートによる .meta 生成だけが目的。
// 配置場所: Assets/Editor/CI/DesignerMetaGenerator.cs

public static class DesignerMetaGenerator
{
    public static void Run()
    {
        // プロジェクトを開いた時点でインポートは走るが、取りこぼしが無いよう明示的に更新する
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("[DesignerMetaGenerator] Assets/Designer の .meta 生成が完了しました");
    }
}
