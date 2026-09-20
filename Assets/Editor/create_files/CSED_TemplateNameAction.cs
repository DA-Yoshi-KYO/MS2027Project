/* ================================================
 * 制作者：吉本竜
 * ------------------------------------------------
 * 2026-09-20 | 初回作成
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
    internal sealed class CSED_TemplateNameAction : EndNameEditAction
    {
        public string Kind, Prefix, Extension, TypeName;
        public UnityEngine.Object Source;

        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            try
            {
                string directory = Path.GetDirectoryName(pathName).Replace('\\', '/');
                if (directory != "Assets" && !directory.StartsWith("Assets/", StringComparison.Ordinal))
                    throw new InvalidOperationException("Assets内のフォルダーで作成してください。");
                if (Kind == "Editor" && !directory.Split('/').Contains("Editor"))
                {
                    if (!AssetDatabase.IsValidFolder(directory + "/Editor")) AssetDatabase.CreateFolder(directory, "Editor");
                    directory += "/Editor";
                }
                string name = CSED_TemplateCreator.NormalizeName(Path.GetFileNameWithoutExtension(pathName), Prefix);
                string path = directory + "/" + name + Extension;
                for (int suffix = 1; File.Exists(path) || Directory.Exists(path); suffix++)
                    path = directory + "/" + name + suffix + Extension;
                name = Path.GetFileNameWithoutExtension(path);
                switch (Kind)
                {
                    case "Material":
                    case "Variant":
                        var shader = Shader.Find("HDRP/Lit");
                        if (Kind == "Variant" && !(Source is Material)) throw new InvalidOperationException("元のMaterialを選択してください。");
                        if (Kind == "Material" && shader == null) throw new InvalidOperationException("HDRP/Litが見つかりません。");
                        var material = Kind == "Variant" ? new Material((Material)Source) : new Material(shader);
                        if (Kind == "Variant") material.parent = (Material)Source;
                        AssetDatabase.CreateAsset(material, path);
                        break;
                    case "Database":
                        var type = Type.GetType(TypeName, true);
                        var data = ScriptableObject.CreateInstance(type);
                        AssetDatabase.CreateAsset(data, path);
                        break;
                    case "Prefab":
                        var go = Source is GameObject original ? UnityEngine.Object.Instantiate(original) : new GameObject(name);
                        try { go.name = name; PrefabUtility.SaveAsPrefabAsset(go, path); }
                        finally { UnityEngine.Object.DestroyImmediate(go); }
                        break;
                    case "Scene":
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
