/* ================================================
 * 名前入力の確定後に、指定された種別のアセットを保存してProject上で選択する。
 * ================================================
 * 制作者：吉本竜
 * ------------------------------------------------
 * 2026-09-20 | 初回作成
 * 2026-09-23 | 処理説明と日本語コメントを追加
 * ================================================ */
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2027.EditorTools
{
    /// <summary>
    /// 名前入力の確定後に、指定された種別のアセットを保存してProject上で選択する。
    /// </summary>
    internal sealed class CSED_TemplateNameAction : EndNameEditAction
    {
        // 名前入力を開始した側から受け取る作成条件と、複製元のオブジェクト。
        public string Kind, Prefix, Extension, TypeName;
        public UnityEngine.Object Source;

        /// <summary>
        /// 保存先・名前を検証し、種別に応じたアセットを作成する。失敗時はログとダイアログで理由を表示する。
        /// </summary>
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            try
            {
                string directory = Path.GetDirectoryName(pathName).Replace('\\', '/');
                if (directory != "Assets" && !directory.StartsWith("Assets/", StringComparison.Ordinal))
                    throw new InvalidOperationException("Assets内のフォルダーで作成してください。");
                // EditorWindowのコードが実行用ビルドに含まれないようEditorフォルダーへ保存する。
                if (Kind == "Editor" && !directory.Split('/').Contains("Editor"))
                {
                    if (!AssetDatabase.IsValidFolder(directory + "/Editor")) AssetDatabase.CreateFolder(directory, "Editor");
                    directory += "/Editor";
                }
                // CS_などの接頭辞を変更・削除していても、確定時に補完し直さない。
                string name = Path.GetFileNameWithoutExtension(pathName);
                string path = directory + "/" + name + Extension;
                for (int suffix = 1; File.Exists(path) || Directory.Exists(path); suffix++)
                    path = directory + "/" + name + suffix + Extension;
                // 重複回避で連番が付いた場合も、コード内のクラス名とファイル名を一致させる。
                name = Path.GetFileNameWithoutExtension(path);
                switch (Kind)
                {
                    case "Material":
                    case "Variant":
                        var shader = Shader.Find("HDRP/Lit");
                        if (Kind == "Variant" && !(Source is Material)) throw new InvalidOperationException("元のMaterialを選択してください。");
                        if (Kind == "Material" && shader == null) throw new InvalidOperationException("HDRP/Litが見つかりません。");
                        var material = Kind == "Variant" ? new Material((Material)Source) : new Material(shader);
                        // 単なる複製ではなく、選択Materialを親として持つVariantにする。
                        if (Kind == "Variant") material.parent = (Material)Source;
                        AssetDatabase.CreateAsset(material, path);
                        break;
                    case "Database":
                        var type = Type.GetType(TypeName, true);
                        var data = ScriptableObject.CreateInstance(type);
                        AssetDatabase.CreateAsset(data, path);
                        break;
                    case "Prefab":
                        // 元のシーンオブジェクトを変更せず、一時オブジェクトからPrefabを保存する。
                        var go = Source is GameObject original ? UnityEngine.Object.Instantiate(original) : new GameObject(name);
                        try { go.name = name; PrefabUtility.SaveAsPrefabAsset(go, path); }
                        finally { UnityEngine.Object.DestroyImmediate(go); }
                        break;
                    case "Scene":
                        // 作業中のSceneを置き換えずに追加作成し、保存後は閉じて元のSceneへ戻す。
                        var previous = SceneManager.GetActiveScene();
                        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                        try
                        {
                            if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Sceneを保存できませんでした。");
                        }
                        finally
                        {
                            EditorSceneManager.CloseScene(scene, true);
                            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                        }
                        break;
                    default:
                        File.WriteAllText(path, CSED_TemplateCreator.Header() + CSED_TemplateCreator.Body(Kind, name), new UTF8Encoding(true));
                        AssetDatabase.ImportAsset(path);
                        break;
                }
                ProjectWindowUtil.ShowCreatedAsset(AssetDatabase.LoadMainAssetAtPath(path));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("templateの作成に失敗しました", e.Message, "OK");
            }
        }
    }
}
