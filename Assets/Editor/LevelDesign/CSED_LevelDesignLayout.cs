using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/*
 * レベルデザイン用のレイアウト(ソロ用/マルチ用)を展開するメニュー
 * Tools > レベルデザイン > ソロ用 / マルチ用
 *
 * 制作者：　吉田京志郎(Claude Codeで生成)
 */

// ========================================
/*
 * メモ
 * ・レイアウトは Layouts フォルダの .wlt ファイル(Unityのレイアウト保存と同じ形式)を読み込む
 *   ソロ用: Gameビュー + ステータス調整ウィンドウ
 *   マルチ用: Gameビュー + Play Mode Status + ステータス調整ウィンドウ(4分割)
 * ・窓の分割をコードで組むにはUnityの内部構造を触る必要があり壊れやすいので、
 *   一度だけ手で並べて「今の配置を保存」し、そのファイルをコミットして全員で使う
 * ・レイアウトファイルがまだ無い時は、必要なウィンドウだけ開いて保存の手順を案内する
 * ・レイアウトの読み込み・保存はUnityの内部API(WindowLayout)を使う。見つからない場合はエラーを出す
 */
// ========================================

public static class CSED_LevelDesignLayout
{
    private const string _layoutFolder = "Assets/Editor/LevelDesign/Layouts";
    private const string _soloLayoutPath = _layoutFolder + "/LD_Solo.wlt";
    private const string _multiLayoutPath = _layoutFolder + "/LD_Multi.wlt";
    private const string _playModeStatusMenu = "Window/Multiplayer/Play Mode Status";

    [MenuItem("Tools/レベルデザイン/ソロ用", false, 0)]
    private static void OpenSolo() => OpenLayout(CSE_LevelDesignMode.Solo);

    [MenuItem("Tools/レベルデザイン/マルチ用", false, 1)]
    private static void OpenMulti() => OpenLayout(CSE_LevelDesignMode.Multi);

    [MenuItem("Tools/レベルデザイン/今の配置を保存/ソロ用", false, 100)]
    private static void SaveSolo() => SaveLayout(CSE_LevelDesignMode.Solo);

    [MenuItem("Tools/レベルデザイン/今の配置を保存/マルチ用", false, 101)]
    private static void SaveMulti() => SaveLayout(CSE_LevelDesignMode.Multi);

    private static string GetLayoutPath(CSE_LevelDesignMode mode) => mode == CSE_LevelDesignMode.Solo ? _soloLayoutPath : _multiLayoutPath;

    private static void OpenLayout(CSE_LevelDesignMode mode)
    {
        string path = GetLayoutPath(mode);
        if (!File.Exists(path))
        {
            OpenWithoutLayout(mode);
            return;
        }

        if (!InvokeLayoutMethod(typeof(EditorUtility), "LoadWindowLayout", path) &&
            !InvokeLayoutMethod(GetWindowLayoutType(), "LoadWindowLayout", path))
        {
            Debug.LogError("レベルデザイン: レイアウトを読み込めませんでした: " + path);
            return;
        }

        // 読み込んだレイアウトの中のステータス調整ウィンドウにモードを設定する(無ければ開く)
        EditorApplication.delayCall += () =>
        {
            CSED_LevelDesignWindow[] windows = Resources.FindObjectsOfTypeAll<CSED_LevelDesignWindow>();
            if (windows.Length == 0) CSED_LevelDesignWindow.Open(mode);
            foreach (CSED_LevelDesignWindow window in windows) window.mode = mode;
        };
    }

    // レイアウトファイルがまだ無い時: 必要なウィンドウを開き、並べて保存するよう案内する
    private static void OpenWithoutLayout(CSE_LevelDesignMode mode)
    {
        Type inspectorType = typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow");
        CSED_LevelDesignWindow.Open(mode, inspectorType != null ? new[] { inspectorType } : new Type[0]);
        if (mode == CSE_LevelDesignMode.Multi && !EditorApplication.ExecuteMenuItem(_playModeStatusMenu))
        {
            Debug.LogWarning("レベルデザイン: Play Mode Statusを開けませんでした(Multiplayer Play Modeパッケージを確認してください)");
        }

        string layoutName = mode == CSE_LevelDesignMode.Solo ? "ソロ用" : "マルチ用";
        string windows = mode == CSE_LevelDesignMode.Solo
            ? "Gameビュー(左) と ステータス調整(右)"
            : "Gameビュー、Play Mode Status、ステータス調整(右)";
        EditorUtility.DisplayDialog("レベルデザイン",
            $"{layoutName}のレイアウトがまだ登録されていません。\n\n{windows} を並べてから、\n" +
            $"Tools > レベルデザイン > 今の配置を保存 > {layoutName} を押してください。\n" +
            "保存したファイルをコミットすると、全員が同じレイアウトを使えます。", "OK");
    }

    private static void SaveLayout(CSE_LevelDesignMode mode)
    {
        if (!EditorWindow.HasOpenInstances<CSED_LevelDesignWindow>())
        {
            EditorUtility.DisplayDialog("レベルデザイン", "ステータス調整ウィンドウを配置に入れてから保存してください。", "OK");
            return;
        }

        string path = GetLayoutPath(mode);
        Directory.CreateDirectory(_layoutFolder);
        if (!InvokeLayoutMethod(GetWindowLayoutType(), "SaveWindowLayout", Path.GetFullPath(path)))
        {
            Debug.LogError("レベルデザイン: レイアウトを保存できませんでした(Unityの内部APIが見つかりません)");
            return;
        }

        AssetDatabase.ImportAsset(path);
        Debug.Log("レベルデザイン: 今の配置を保存しました: " + path);
    }

    private static Type GetWindowLayoutType() => typeof(Editor).Assembly.GetType("UnityEditor.WindowLayout");

    // 「最初の引数がパス(string)」の静的メソッドを探して呼ぶ。Unityのバージョンで引数の数が違うため、残りは既定値で埋める
    private static bool InvokeLayoutMethod(Type type, string methodName, string path)
    {
        if (type == null) return false;

        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        MethodInfo method = type.GetMethods(flags)
            .Where(m => m.Name == methodName)
            .Where(m => m.GetParameters().Length > 0 && m.GetParameters()[0].ParameterType == typeof(string))
            .OrderBy(m => m.GetParameters().Length)
            .FirstOrDefault();
        if (method == null) return false;

        ParameterInfo[] parameters = method.GetParameters();
        object[] arguments = new object[parameters.Length];
        arguments[0] = path;
        for (int i = 1; i < parameters.Length; i++) arguments[i] = GetDefaultArgument(parameters[i]);

        object result = method.Invoke(null, arguments);
        return !(result is bool success) || success;
    }

    private static object GetDefaultArgument(ParameterInfo parameter)
    {
        if (parameter.HasDefaultValue) return parameter.DefaultValue;
        if (parameter.ParameterType != typeof(bool)) return parameter.ParameterType.IsValueType ? Activator.CreateInstance(parameter.ParameterType) : null;

        // メインウィンドウは残し、失敗はConsoleに出す。それ以外のフラグはオフ
        string name = parameter.Name.ToLowerInvariant();
        return name.Contains("keepmainwindow") || name.Contains("logserror");
    }
}
