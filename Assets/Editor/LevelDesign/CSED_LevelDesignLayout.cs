using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/*
 * レベルデザイン用のレイアウトを展開するメニュー
 * Tools > レベルデザイン > 開く
 *
 * 制作者：　吉田京志郎(Claude Codeで生成)
 */

// ========================================
/*
 * メモ
 * ・レイアウトは Layouts フォルダの .wlt ファイル(Unityのレイアウト保存と同じ形式)を読み込む
 *   Gameビュー + ステータス調整ウィンドウ
 * ・マルチ(Multiplayer Play Mode)の2〜4人目は別のUnityで起動し、このレイアウトに入れられないため、ソロ用・マルチ用の区別は設けない
 * ・窓の分割をコードで組むにはUnityの内部構造を触る必要があり壊れやすいので、
 *   一度だけ手で並べて「今の配置を保存」し、そのファイルをコミットして全員で使う
 * ・レイアウトファイルがまだ無い時は、必要なウィンドウだけ開き、ステータス調整ウィンドウの中で保存の手順を案内する
 * ・レイアウトの読み込み・保存はUnityの内部API(WindowLayout)を使う。見つからない場合はエラーを出す
 * ・開く前の配置は Library に退避しておき、「調整を終了」でその配置に戻す(個人の配置なのでコミットしない)
 */
// ========================================

public static class CSED_LevelDesignLayout
{
    private const string _layoutFolder = "Assets/Editor/LevelDesign/Layouts";
    private const string _layoutPath = _layoutFolder + "/LD_Layout.wlt";
    private const string _previousLayoutPath = "Library/LevelDesign/PreviousLayout.wlt";

    [MenuItem("Tools/レベルデザイン/開く", false, 0)]
    private static void OpenLayout()
    {
        SavePreviousLayout();

        string path = _layoutPath;
        if (!File.Exists(path))
        {
            OpenWithoutLayout();
            return;
        }

        if (!InvokeLayoutMethod(typeof(EditorUtility), "LoadWindowLayout", path) &&
            !InvokeLayoutMethod(GetWindowLayoutType(), "LoadWindowLayout", path))
        {
            Debug.LogError("レベルデザイン: レイアウトを読み込めませんでした: " + path);
            return;
        }

        // 読み込んだレイアウトにステータス調整ウィンドウが無ければ開く
        EditorApplication.delayCall += () =>
        {
            if (!EditorWindow.HasOpenInstances<CSED_LevelDesignWindow>()) CSED_LevelDesignWindow.Open();
        };
    }

    // レイアウトファイルがまだ無い時: 必要なウィンドウを開き、並べて保存するようウィンドウ内で案内する
    // (モーダルダイアログはUnityの処理を止めてしまうので使わない)
    private static void OpenWithoutLayout()
    {
        Type inspectorType = typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow");
        CSED_LevelDesignWindow window = CSED_LevelDesignWindow.Open(inspectorType != null ? new[] { inspectorType } : new Type[0]);
        window.layoutHint = "レイアウトがまだ登録されていません。\nGameビュー(左) と このウィンドウ(右) を並べてから、" +
            "Tools > レベルデザイン > 今の配置を保存 を押してください。保存したファイルをコミットすると、全員が同じレイアウトを使えます。";
    }

    // 調整用のレイアウトを開く前の配置を退避する
    // ステータス調整ウィンドウが開いている時は調整中(開き直しなど)なので、最初に退避した配置を残す
    private static void SavePreviousLayout()
    {
        if (EditorWindow.HasOpenInstances<CSED_LevelDesignWindow>()) return;

        Directory.CreateDirectory(Path.GetDirectoryName(_previousLayoutPath));
        if (!InvokeLayoutMethod(GetWindowLayoutType(), "SaveWindowLayout", Path.GetFullPath(_previousLayoutPath)))
        {
            Debug.LogWarning("レベルデザイン: 今の配置を退避できませんでした。「調整を終了」では元の配置に戻せません");
        }
    }

    // 調整を終了して、調整用のレイアウトを開く前の配置に戻す
    public static void EndAdjustment()
    {
        if (!EditorUtility.DisplayDialog("レベルデザイン", "調整を終了して、元のレイアウトに戻しますか？", "はい", "いいえ")) return;

        if (!File.Exists(_previousLayoutPath))
        {
            // 戻す配置が無い時は、ステータス調整ウィンドウだけ閉じる
            Debug.LogWarning("レベルデザイン: 元の配置が見つからないため、ステータス調整ウィンドウだけ閉じます");
            CloseLevelDesignWindows();
            return;
        }

        string path = Path.GetFullPath(_previousLayoutPath);
        if (!InvokeLayoutMethod(typeof(EditorUtility), "LoadWindowLayout", path) &&
            !InvokeLayoutMethod(GetWindowLayoutType(), "LoadWindowLayout", path))
        {
            Debug.LogError("レベルデザイン: 元のレイアウトを読み込めませんでした: " + _previousLayoutPath);
            return;
        }

        File.Delete(_previousLayoutPath);
        // 元の配置にステータス調整ウィンドウが入っていた場合も、調整は終わりなので閉じる
        EditorApplication.delayCall += CloseLevelDesignWindows;
    }

    private static void CloseLevelDesignWindows()
    {
        foreach (CSED_LevelDesignWindow window in Resources.FindObjectsOfTypeAll<CSED_LevelDesignWindow>()) window.Close();
    }

    [MenuItem("Tools/レベルデザイン/今の配置を保存", false, 100)]
    private static void SaveLayout()
    {
        if (!EditorWindow.HasOpenInstances<CSED_LevelDesignWindow>())
        {
            EditorUtility.DisplayDialog("レベルデザイン", "ステータス調整ウィンドウを配置に入れてから保存してください。", "OK");
            return;
        }

        string path = _layoutPath;
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
