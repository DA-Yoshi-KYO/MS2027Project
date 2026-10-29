using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/*
 * JSONに書いた設定から、ポストプロセス・空・霧のボリュームを作り、シーンのボリュームに当てるエディタ拡張(画づくり用)
 * Tools/Claude/Apply Volume Look から実行する。Unity MCPのexecute_menu_itemからも呼べる
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・設定ファイル: Assets/Programmer/Volume/LookSettings/VO_OrderZeroNight.json
 *   { "profile": "出力先のアセットのパス", "volume": "当てるシーンのボリュームの名前", "components": { "型名": { "項目名": 値 } } }
 *   型名はHDRPのVolumeComponentのクラス名(Exposure、Bloom、Fog、GradientSky など)。項目名はそのクラスのVolumeParameterのフィールド名
 *   値: 数値 / true・false / 色は[r, g, b, a] / Vector は[x, y, z, w] / enumは数値
 * ・実行するたびに、プロファイルの中身を作り直す(JSONに無い項目は既定値のまま)
 * ・JSONはプログラムを直さずに何度でも試せるよう、コードから分けている
 */
// ========================================

public static class CSED_ClaudeVolumeLook
{
    private const string _settingsPath = "Assets/Programmer/Volume/LookSettings/VO_OrderZeroNight.json";

    [MenuItem("Tools/Claude/Apply Volume Look")]
    public static void Apply()
    {
        if (!File.Exists(_settingsPath))
        {
            Debug.LogError("CSED_ClaudeVolumeLook: 設定ファイルがありません: " + _settingsPath);
            return;
        }

        Dictionary<string, object> root = MiniJson.Parse(File.ReadAllText(_settingsPath)) as Dictionary<string, object>;
        string profilePath = (string)root["profile"];
        string volumeName = (string)root["volume"];
        Dictionary<string, object> components = (Dictionary<string, object>)root["components"];

        VolumeProfile profile = LoadOrCreateProfile(profilePath);
        foreach (KeyValuePair<string, object> pair in components)
        {
            ApplyComponent(profile, pair.Key, (Dictionary<string, object>)pair.Value);
        }

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        AssignToVolume(volumeName, profile);
        Debug.Log("CSED_ClaudeVolumeLook: 当てました " + profilePath);
    }

    private static VolumeProfile LoadOrCreateProfile(string path)
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(profile, path);
        }

        // 中身を作り直す(前の設定が残らないように)
        foreach (VolumeComponent component in profile.components.ToArray())
        {
            profile.Remove(component.GetType());
            UnityEngine.Object.DestroyImmediate(component, true);
        }
        return profile;
    }

    private static void ApplyComponent(VolumeProfile profile, string typeName, Dictionary<string, object> values)
    {
        Type type = FindVolumeComponentType(typeName);
        if (type == null)
        {
            Debug.LogError("CSED_ClaudeVolumeLook: VolumeComponentが見つかりません: " + typeName);
            return;
        }

        VolumeComponent component = profile.Add(type, false);
        component.name = typeName;
        AssetDatabase.AddObjectToAsset(component, profile);

        foreach (KeyValuePair<string, object> pair in values)
        {
            FieldInfo field = type.GetField(pair.Key, BindingFlags.Public | BindingFlags.Instance);
            if (field == null || !(field.GetValue(component) is VolumeParameter parameter))
            {
                Debug.LogWarning($"CSED_ClaudeVolumeLook: {typeName}.{pair.Key} がありません");
                continue;
            }

            SetParameter(parameter, pair.Value, $"{typeName}.{pair.Key}");
        }
    }

    private static Type FindVolumeComponentType(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = assembly.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }

            foreach (Type type in types)
            {
                if (type.Name == name && typeof(VolumeComponent).IsAssignableFrom(type) && !type.IsAbstract) return type;
            }
        }
        return null;
    }

    // VolumeParameter<T>のTに合わせて値を変換し、Overrideする
    private static void SetParameter(VolumeParameter parameter, object value, string label)
    {
        Type valueType = GetParameterValueType(parameter.GetType());
        if (valueType == null)
        {
            Debug.LogWarning("CSED_ClaudeVolumeLook: 型が分かりません: " + label);
            return;
        }

        object converted;
        try
        {
            converted = Convert(value, valueType);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"CSED_ClaudeVolumeLook: {label} の値を変換できません: {e.Message}");
            return;
        }

        parameter.overrideState = true;
        parameter.GetType().GetProperty("value").SetValue(parameter, converted);
    }

    private static Type GetParameterValueType(Type parameterType)
    {
        for (Type type = parameterType; type != null; type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(VolumeParameter<>)) return type.GetGenericArguments()[0];
        }
        return null;
    }

    private static object Convert(object value, Type type)
    {
        if (type == typeof(float)) return System.Convert.ToSingle(value);
        if (type == typeof(int)) return System.Convert.ToInt32(value);
        if (type == typeof(bool)) return System.Convert.ToBoolean(value);
        if (type.IsEnum) return Enum.ToObject(type, System.Convert.ToInt32(value));

        List<object> list = value as List<object>;
        float At(int i, float fallback) => list != null && i < list.Count ? System.Convert.ToSingle(list[i]) : fallback;
        if (type == typeof(Color)) return new Color(At(0, 0f), At(1, 0f), At(2, 0f), At(3, 1f));
        if (type == typeof(Vector2)) return new Vector2(At(0, 0f), At(1, 0f));
        if (type == typeof(Vector3)) return new Vector3(At(0, 0f), At(1, 0f), At(2, 0f));
        if (type == typeof(Vector4)) return new Vector4(At(0, 0f), At(1, 0f), At(2, 0f), At(3, 0f));

        throw new NotSupportedException(type.Name);
    }

    private static void AssignToVolume(string volumeName, VolumeProfile profile)
    {
        foreach (Volume volume in UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (volume.gameObject.name != volumeName) continue;

            Undo.RecordObject(volume, "Apply Volume Look");
            volume.sharedProfile = profile;
            EditorUtility.SetDirty(volume);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(volume.gameObject.scene);
            return;
        }

        Debug.LogWarning("CSED_ClaudeVolumeLook: シーンにボリュームがありません: " + volumeName);
    }

    // 小さなJSONの読み取り(Dictionary / List / double / bool / string / null)
    private static class MiniJson
    {
        public static object Parse(string json)
        {
            int index = 0;
            return ParseValue(json, ref index);
        }

        private static object ParseValue(string s, ref int i)
        {
            SkipSpace(s, ref i);
            char c = s[i];
            if (c == '{') return ParseObject(s, ref i);
            if (c == '[') return ParseArray(s, ref i);
            if (c == '"') return ParseString(s, ref i);
            if (s.Substring(i).StartsWith("true")) { i += 4; return true; }
            if (s.Substring(i).StartsWith("false")) { i += 5; return false; }
            if (s.Substring(i).StartsWith("null")) { i += 4; return null; }

            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(start, i - start), System.Globalization.CultureInfo.InvariantCulture);
        }

        private static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            i++;
            while (true)
            {
                SkipSpace(s, ref i);
                if (s[i] == '}') { i++; return result; }
                string key = ParseString(s, ref i);
                SkipSpace(s, ref i);
                i++;   // :
                result[key] = ParseValue(s, ref i);
                SkipSpace(s, ref i);
                if (s[i] == ',') i++;
            }
        }

        private static List<object> ParseArray(string s, ref int i)
        {
            List<object> result = new List<object>();
            i++;
            while (true)
            {
                SkipSpace(s, ref i);
                if (s[i] == ']') { i++; return result; }
                result.Add(ParseValue(s, ref i));
                SkipSpace(s, ref i);
                if (s[i] == ',') i++;
            }
        }

        private static string ParseString(string s, ref int i)
        {
            i++;
            int start = i;
            while (s[i] != '"') i++;
            string result = s.Substring(start, i - start);
            i++;
            return result;
        }

        private static void SkipSpace(string s, ref int i)
        {
            while (i < s.Length && (char.IsWhiteSpace(s[i]) || s[i] == '/' && i + 1 < s.Length && s[i + 1] == '/'))
            {
                if (s[i] == '/') { while (i < s.Length && s[i] != '\n') i++; }
                else i++;
            }
        }
    }
}
