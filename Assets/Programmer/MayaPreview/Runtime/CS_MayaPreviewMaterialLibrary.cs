using System;
using System.Collections.Generic;
using System.IO;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/*
+=====================================
 ファイル名 : CS_MayaPreviewMaterialLibrary
 概要       : Mayaのマテリアル名ごとに、PreviewHost内のMaterialを管理する
              ・初めて見る名前 → HDRP/Lit で自動生成（Mayaのテクスチャ・色を反映）
              ・シェーダーと値は PreviewHost の Inspector で変更する
              ・変更はマテリアル名ごとのJSONに保存（保存先はMayaから SESSION で受け取る）
                → 形やUVを直しても、別のシーンでも、同じ名前なら同じ設定で表示
              ・テクスチャはファイル更新日時が変わったら読み直す（Substance再書き出し対応）
 作者       : ヨシモト リョウ
 履歴       : 2026/10/10 新規作成 / 自動HDRP/Lit + Inspector編集 + 保存 に変更
=====================================+
*/

public class CS_MayaPreviewMaterialLibrary : IDisposable
{
    // =========================================================
    // 受信データ（Maya → Unity）: Mayaのマテリアルから自動で取れる物だけ
    // =========================================================

    [Serializable]
    public class TextureRef
    {
        // "@base" = ベースカラー、"@normal" = ノーマル
        public string prop;
        public string path;
    }

    [Serializable]
    public class MaterialDef
    {
        public string type;
        public string name;
        public float[] baseColor;
        public TextureRef[] textures;
    }


    // =========================================================
    // 保存データ（PreviewHostで変更した物）
    // =========================================================

    [Serializable]
    private class FloatValue { public string prop; public float value; }

    [Serializable]
    private class VectorValue { public string prop; public Vector4 value; }

    [Serializable]
    private class TextureValue { public string prop; public string path; }

    [Serializable]
    private class SavedMaterial
    {
        public int version = 1;
        public string name;
        public string shader;
        public FloatValue[] floats;
        public VectorValue[] colors;
        public VectorValue[] vectors;
        public TextureValue[] textures;
    }


    // =========================================================
    // 内部
    // =========================================================

    public const string DefaultShader = "HDRP/Lit";
    private const float SaveDelay = 0.8f;

    private class ShaderInfo
    {
        public Shader shader;
        public Material defaults;
        public string baseProp;
        public string normalProp;
    }

    private class Record
    {
        public string name;
        public Material material;
        public string shader;                 // null = DefaultShader
        public readonly Dictionary<string, float> floats = new Dictionary<string, float>();
        public readonly Dictionary<string, Color> colors = new Dictionary<string, Color>();
        public readonly Dictionary<string, Vector4> vectors = new Dictionary<string, Vector4>();
        public readonly Dictionary<string, string> textures = new Dictionary<string, string>();
        public float[] mayaColor;
        public string mayaBase;
        public string mayaNormal;
        public bool dirty;
    }

    private class CachedTexture
    {
        public Texture2D texture;
        public DateTime stamp;
    }

    private readonly Dictionary<string, ShaderInfo> _shaders =
        new Dictionary<string, ShaderInfo>(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _shaderNames = new List<string>();
    private readonly Dictionary<string, Record> _records =
        new Dictionary<string, Record>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Material, Record> _byMaterial = new Dictionary<Material, Record>();
    private readonly Dictionary<string, CachedTexture> _textures =
        new Dictionary<string, CachedTexture>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _warned = new HashSet<string>();
    private string _folder;
    private float _saveAt = -1f;

    // 現在のライブラリ（Inspectorから使う）
    public static CS_MayaPreviewMaterialLibrary Current { get; private set; }

    // Materialが作り直された（シェーダー変更・リセット・保存先変更）。引数 = マテリアル名
    public event Action<string> MaterialReplaced;

    public IReadOnlyList<string> ShaderNames => _shaderNames;
    public string Folder => _folder;


    // =========================================================
    // 構築
    // =========================================================

    public CS_MayaPreviewMaterialLibrary(CS_MayaPreviewMaterialCatalog catalog)
    {
        AddShader(Shader.Find(DefaultShader), null);

        if (catalog != null)
        {
            foreach (var entry in catalog.entries)
            {
                if (entry != null && entry.material != null)
                {
                    AddShader(entry.material.shader, entry.material);
                }
            }
        }

        _shaderNames.Sort((a, b) => a == DefaultShader ? -1 : b == DefaultShader ? 1 : string.CompareOrdinal(a, b));
        Current = this;
    }

    private void AddShader(Shader shader, Material defaults)
    {
        if (shader == null || _shaders.ContainsKey(shader.name))
        {
            return;
        }

        var info = new ShaderInfo { shader = shader, defaults = defaults };
        int count = shader.GetPropertyCount();

        for (int i = 0; i < count; i++)
        {
            if (shader.GetPropertyType(i) != ShaderPropertyType.Texture) continue;
            ShaderPropertyFlags flags = shader.GetPropertyFlags(i);
            if (info.baseProp == null && (flags & ShaderPropertyFlags.MainTexture) != 0) info.baseProp = shader.GetPropertyName(i);
            if (info.normalProp == null && (flags & ShaderPropertyFlags.Normal) != 0) info.normalProp = shader.GetPropertyName(i);
        }

        info.baseProp = info.baseProp ?? FirstExisting(shader, "_BaseMap", "_BaseColorMap", "_MainTex");
        info.normalProp = info.normalProp ?? FirstExisting(shader, "_NormalMap", "_BumpMap");
        _shaders.Add(shader.name, info);
        _shaderNames.Add(shader.name);
    }

    private static string FirstExisting(Shader shader, params string[] names)
    {
        foreach (string name in names)
        {
            if (shader.FindPropertyIndex(name) >= 0) return name;
        }
        return null;
    }


    // =========================================================
    // 取得
    // =========================================================

    public Material GetOrCreate(string materialName)
    {
        if (string.IsNullOrEmpty(materialName))
        {
            return null;
        }

        if (!_records.TryGetValue(materialName, out Record record))
        {
            record = new Record { name = materialName };
            _records.Add(materialName, record);
            LoadSaved(record);
            Rebuild(record, false);
        }

        return record.material;
    }

    public bool Owns(Material material, out string materialName)
    {
        if (material != null && _byMaterial.TryGetValue(material, out Record record))
        {
            materialName = record.name;
            return true;
        }

        materialName = null;
        return false;
    }

    public bool IsOverridden(string materialName, string prop)
    {
        return _records.TryGetValue(materialName, out Record r) &&
            (r.floats.ContainsKey(prop) || r.colors.ContainsKey(prop) || r.vectors.ContainsKey(prop) || r.textures.ContainsKey(prop));
    }

    // Inspectorに出すテクスチャパス（変更した物 → Mayaの物 → なし）
    public string TexturePath(string materialName, string prop)
    {
        if (!_records.TryGetValue(materialName, out Record r)) return "";
        if (r.textures.TryGetValue(prop, out string path)) return path;
        ShaderInfo info = InfoOf(r);
        if (prop == info.baseProp && !string.IsNullOrEmpty(r.mayaBase)) return r.mayaBase;
        if (prop == info.normalProp && !string.IsNullOrEmpty(r.mayaNormal)) return r.mayaNormal;
        return "";
    }


    // =========================================================
    // Mayaからの更新（色・ベースマップ・ノーマル）。Materialは作り直さない
    // =========================================================

    public void SetMayaData(MaterialDef def)
    {
        if (def == null || string.IsNullOrEmpty(def.name))
        {
            return;
        }

        GetOrCreate(def.name);
        Record record = _records[def.name];
        record.mayaColor = def.baseColor;
        record.mayaBase = null;
        record.mayaNormal = null;

        if (def.textures != null)
        {
            foreach (TextureRef reference in def.textures)
            {
                if (reference == null || string.IsNullOrEmpty(reference.path)) continue;
                if (reference.prop == "@base") record.mayaBase = reference.path;
                else if (reference.prop == "@normal") record.mayaNormal = reference.path;
            }
        }

        ApplyMaya(record, record.material, InfoOf(record));
        Validate(record.material);
    }


    // =========================================================
    // Inspectorからの変更（記録して保存）
    // =========================================================

    public void SetShader(string materialName, string shaderName)
    {
        if (!_records.TryGetValue(materialName, out Record record)) return;
        record.shader = string.Equals(shaderName, DefaultShader, StringComparison.OrdinalIgnoreCase) ? null : shaderName;
        Rebuild(record, true);
        MarkDirty(record);
    }

    public void SetFloat(string materialName, string prop, float value)
    {
        if (!_records.TryGetValue(materialName, out Record record)) return;
        record.floats[prop] = value;
        SetNumber(record.material, prop, value);
        Validate(record.material);
        MarkDirty(record);
    }

    public void SetColor(string materialName, string prop, Color value)
    {
        if (!_records.TryGetValue(materialName, out Record record)) return;
        record.colors[prop] = value;
        record.material.SetColor(prop, value);
        Validate(record.material);
        MarkDirty(record);
    }

    public void SetVector(string materialName, string prop, Vector4 value)
    {
        if (!_records.TryGetValue(materialName, out Record record)) return;
        record.vectors[prop] = value;
        record.material.SetVector(prop, value);
        Validate(record.material);
        MarkDirty(record);
    }

    // path が空 = 変更を取り消す（Mayaのテクスチャ / なし に戻る）
    public void SetTexture(string materialName, string prop, string path)
    {
        if (!_records.TryGetValue(materialName, out Record record)) return;

        if (string.IsNullOrEmpty(path))
        {
            record.textures.Remove(prop);
            record.material.SetTexture(prop, null);
            ApplyMaya(record, record.material, InfoOf(record));
        }
        else
        {
            Texture2D texture = LoadTexture(path, IsLinear(prop, InfoOf(record)));
            if (texture == null) return;
            record.textures[prop] = path;
            record.material.SetTexture(prop, texture);
        }

        Validate(record.material);
        MarkDirty(record);
    }

    // =========================================================
    // Undo / 書き出し用: シェーダーと変更値をJSONで取り出す / 戻す
    // =========================================================

    public string Snapshot(string materialName)
    {
        if (!_records.TryGetValue(materialName, out Record record)) return "";
        var saved = new SavedMaterial
        {
            name = record.name,
            shader = record.shader ?? DefaultShader,
            floats = ToArray(record.floats, (k, v) => new FloatValue { prop = k, value = v }),
            colors = ToArray(record.colors, (k, v) => new VectorValue { prop = k, value = v }),
            vectors = ToArray(record.vectors, (k, v) => new VectorValue { prop = k, value = v }),
            textures = ToArray(record.textures, (k, v) => new TextureValue { prop = k, path = v })
        };
        return JsonUtility.ToJson(saved);
    }

    public void Restore(string materialName, string json)
    {
        if (string.IsNullOrEmpty(materialName)) return;
        GetOrCreate(materialName);
        if (!_records.TryGetValue(materialName, out Record record)) return;
        record.shader = null;
        record.floats.Clear(); record.colors.Clear(); record.vectors.Clear(); record.textures.Clear();
        if (!string.IsNullOrEmpty(json))
        {
            var saved = JsonUtility.FromJson<SavedMaterial>(json);
            if (saved != null)
            {
                record.shader = string.IsNullOrEmpty(saved.shader) || saved.shader == DefaultShader ? null : saved.shader;
                if (saved.floats != null) foreach (var v in saved.floats) record.floats[v.prop] = v.value;
                if (saved.colors != null) foreach (var v in saved.colors) record.colors[v.prop] = v.value;
                if (saved.vectors != null) foreach (var v in saved.vectors) record.vectors[v.prop] = v.value;
                if (saved.textures != null) foreach (var v in saved.textures) record.textures[v.prop] = v.path;
            }
        }
        Rebuild(record, true);
        MarkDirty(record);
    }

    public IEnumerable<string> MaterialNames => _records.Keys;

    public Material MaterialOf(string materialName) =>
        _records.TryGetValue(materialName, out Record record) ? record.material : null;

    // テクスチャのファイルパス（変更した物 → Mayaの物）。無ければ ""
    public string SourcePath(string materialName, string prop) => TexturePath(materialName, prop);

    public bool IsNormalProp(string materialName, string prop) =>
        _records.TryGetValue(materialName, out Record record) && InfoOf(record)?.normalProp == prop;

    public bool IsLinearProp(string materialName, string prop) =>
        _records.TryGetValue(materialName, out Record record) && IsLinear(prop, InfoOf(record));


    // 変更を全て捨てて HDRP/Lit + Maya の状態に戻す
    public void ResetMaterial(string materialName)
    {
        if (!_records.TryGetValue(materialName, out Record record)) return;
        record.shader = null;
        record.floats.Clear(); record.colors.Clear(); record.vectors.Clear(); record.textures.Clear();
        Rebuild(record, true);
        MarkDirty(record);
    }


    // =========================================================
    // 保存先（Mayaのプロジェクトごと）
    // =========================================================

    public void SetFolder(string folder)
    {
        folder = string.IsNullOrEmpty(folder) ? null : folder.Replace('\\', '/');
        if (string.Equals(folder, _folder, StringComparison.OrdinalIgnoreCase)) return;

        SaveDirty();
        _folder = folder;

        foreach (Record record in new List<Record>(_records.Values))
        {
            record.shader = null;
            record.floats.Clear(); record.colors.Clear(); record.vectors.Clear(); record.textures.Clear();
            LoadSaved(record);
            Rebuild(record, true);
        }

        Debug.Log("Maya Preview Material : 保存先 " + (_folder ?? "(なし)"));
    }

    // 毎フレーム呼ぶ。変更から少し待ってまとめて保存
    public void Tick(float time)
    {
        if (_saveAt >= 0f && time >= _saveAt)
        {
            SaveDirty();
        }
    }

    private void MarkDirty(Record record)
    {
        record.dirty = true;
        _saveAt = Time.unscaledTime + SaveDelay;
    }

    public void SaveDirty()
    {
        _saveAt = -1f;
        if (_folder == null) return;

        foreach (Record record in _records.Values)
        {
            if (!record.dirty) continue;
            record.dirty = false;
            string path = PathOf(record.name);

            try
            {
                bool empty = record.shader == null && record.floats.Count == 0 && record.colors.Count == 0 &&
                    record.vectors.Count == 0 && record.textures.Count == 0;
                if (empty)
                {
                    if (File.Exists(path)) File.Delete(path);
                    continue;
                }

                var saved = new SavedMaterial
                {
                    name = record.name,
                    shader = record.shader ?? DefaultShader,
                    floats = ToArray(record.floats, (k, v) => new FloatValue { prop = k, value = v }),
                    colors = ToArray(record.colors, (k, v) => new VectorValue { prop = k, value = v }),
                    vectors = ToArray(record.vectors, (k, v) => new VectorValue { prop = k, value = v }),
                    textures = ToArray(record.textures, (k, v) => new TextureValue { prop = k, path = v })
                };
                Directory.CreateDirectory(_folder);
                File.WriteAllText(path, JsonUtility.ToJson(saved, true));
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Maya Preview Material : 保存失敗 " + path + "\n" + exception.Message);
            }
        }
    }

    private void LoadSaved(Record record)
    {
        if (_folder == null) return;
        string path = PathOf(record.name);
        if (!File.Exists(path)) return;

        try
        {
            var saved = JsonUtility.FromJson<SavedMaterial>(File.ReadAllText(path));
            if (saved == null) return;
            record.shader = string.IsNullOrEmpty(saved.shader) || saved.shader == DefaultShader ? null : saved.shader;
            if (saved.floats != null) foreach (var v in saved.floats) record.floats[v.prop] = v.value;
            if (saved.colors != null) foreach (var v in saved.colors) record.colors[v.prop] = v.value;
            if (saved.vectors != null) foreach (var v in saved.vectors) record.vectors[v.prop] = v.value;
            if (saved.textures != null) foreach (var v in saved.textures) record.textures[v.prop] = v.path;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Maya Preview Material : 読み込み失敗 " + path + "\n" + exception.Message);
        }
    }

    private string PathOf(string materialName)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) materialName = materialName.Replace(c, '_');
        return _folder + "/" + materialName + ".json";
    }

    private static T[] ToArray<TValue, T>(Dictionary<string, TValue> source, Func<string, TValue, T> make)
    {
        var result = new T[source.Count];
        int i = 0;
        foreach (var pair in source) result[i++] = make(pair.Key, pair.Value);
        return result;
    }


    // =========================================================
    // Material生成・反映
    // =========================================================

    private ShaderInfo InfoOf(Record record)
    {
        if (record.shader != null && _shaders.TryGetValue(record.shader, out ShaderInfo info)) return info;
        if (record.shader != null) Warn("shader:" + record.shader, "Maya Preview Material : シェーダー \"" + record.shader + "\" がビルドにありません。" + DefaultShader + " を使います。");
        return _shaders.TryGetValue(DefaultShader, out info) ? info : null;
    }

    private void Rebuild(Record record, bool notify)
    {
        ShaderInfo info = InfoOf(record);
        if (info == null) return;

        Material material = info.defaults != null ? new Material(info.defaults) : new Material(info.shader);
        material.name = record.name;

        // テンプレート元のテクスチャは持ち込まない
        int count = info.shader.GetPropertyCount();
        for (int i = 0; i < count; i++)
        {
            if (info.shader.GetPropertyType(i) == ShaderPropertyType.Texture &&
                (info.shader.GetPropertyFlags(i) & ShaderPropertyFlags.HideInInspector) == 0)
            {
                material.SetTexture(info.shader.GetPropertyName(i), null);
            }
        }

        ApplyMaya(record, material, info);

        foreach (var pair in record.floats) if (material.HasProperty(pair.Key)) SetNumber(material, pair.Key, pair.Value);
        foreach (var pair in record.colors) if (material.HasProperty(pair.Key)) material.SetColor(pair.Key, pair.Value);
        foreach (var pair in record.vectors) if (material.HasProperty(pair.Key)) material.SetVector(pair.Key, pair.Value);
        foreach (var pair in record.textures)
        {
            if (!material.HasProperty(pair.Key)) continue;
            Texture2D texture = LoadTexture(pair.Value, IsLinear(pair.Key, info));
            if (texture != null) material.SetTexture(pair.Key, texture);
        }

        Validate(material);

        Material previous = record.material;
        if (previous != null) _byMaterial.Remove(previous);
        record.material = material;
        _byMaterial[material] = record;

        if (notify) MaterialReplaced?.Invoke(record.name);
        if (previous != null) UnityEngine.Object.Destroy(previous);
    }

    // Mayaの色・テクスチャ（Inspectorで変更したプロパティは上書きしない）
    private void ApplyMaya(Record record, Material material, ShaderInfo info)
    {
        if (material == null || info == null) return;

        if (record.mayaColor != null && record.mayaColor.Length >= 3)
        {
            Color color = new Color(record.mayaColor[0], record.mayaColor[1], record.mayaColor[2],
                record.mayaColor.Length > 3 ? record.mayaColor[3] : 1f);
            string colorProp = material.HasProperty("_BaseColor") ? "_BaseColor" : material.HasProperty("_Color") ? "_Color" : null;
            if (colorProp != null && !record.colors.ContainsKey(colorProp)) material.SetColor(colorProp, color);
        }

        SetMayaTexture(record, material, info.baseProp, record.mayaBase, false);
        SetMayaTexture(record, material, info.normalProp, record.mayaNormal, true);
    }

    private void SetMayaTexture(Record record, Material material, string prop, string path, bool linear)
    {
        if (string.IsNullOrEmpty(prop) || !material.HasProperty(prop) || record.textures.ContainsKey(prop)) return;
        material.SetTexture(prop, string.IsNullOrEmpty(path) ? null : LoadTexture(path, linear));
    }

    private static void SetNumber(Material material, string prop, float value)
    {
        Shader shader = material.shader;
        int index = shader.FindPropertyIndex(prop);
        if (index >= 0 && shader.GetPropertyType(index) == ShaderPropertyType.Int) material.SetInteger(prop, Mathf.RoundToInt(value));
        else material.SetFloat(prop, value);
    }

    private void Validate(Material material)
    {
        if (material == null) return;
        try
        {
            // HDRPのキーワード（_NORMALMAP 等）をプロパティに合わせる
            HDMaterial.ValidateMaterial(material);
        }
        catch (Exception exception)
        {
            Warn("validate:" + material.shader.name, "Maya Preview Material : ValidateMaterial 失敗\n" + exception.Message);
        }
    }

    private static bool IsLinear(string prop, ShaderInfo info)
    {
        if (info != null && prop == info.normalProp) return true;
        if (info != null && prop == info.baseProp) return false;
        string lower = prop.ToLowerInvariant();
        return !(lower.Contains("color") || lower.Contains("albedo") || lower.Contains("emiss") || lower.Contains("base"));
    }


    // =========================================================
    // テクスチャ読み込み（PNG / JPG）
    // =========================================================

    private Texture2D LoadTexture(string path, bool linear)
    {
        if (!File.Exists(path))
        {
            Warn("missing:" + path, "Maya Preview Material : テクスチャが見つかりません " + path);
            return null;
        }

        string key = path + (linear ? "|linear" : "|srgb");
        DateTime stamp = File.GetLastWriteTimeUtc(path);

        if (_textures.TryGetValue(key, out CachedTexture cached) && cached.texture != null && cached.stamp == stamp)
        {
            return cached.texture;
        }

        // 同じオブジェクトを読み直すので、使っているMaterialも自動で更新される
        Texture2D texture = cached != null && cached.texture != null
            ? cached.texture
            : new Texture2D(2, 2, TextureFormat.RGBA32, true, linear);

        try
        {
            if (!texture.LoadImage(File.ReadAllBytes(path), false))
            {
                Warn("format:" + path, "Maya Preview Material : PNG / JPG 以外は読み込めません " + path);
                return null;
            }
        }
        catch (IOException exception)
        {
            Debug.LogWarning("Maya Preview Material : 読み込み失敗 " + path + "\n" + exception.Message);
            return cached?.texture;
        }

        texture.name = Path.GetFileName(path);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Trilinear;
        texture.anisoLevel = 4;
        _textures[key] = new CachedTexture { texture = texture, stamp = stamp };
        return texture;
    }

    private void Warn(string key, string message)
    {
        if (_warned.Add(key)) Debug.LogWarning(message);
    }


    // =========================================================
    // 破棄
    // =========================================================

    public void Dispose()
    {
        SaveDirty();
        foreach (Record record in _records.Values)
        {
            if (record.material != null) UnityEngine.Object.Destroy(record.material);
        }
        foreach (CachedTexture cached in _textures.Values)
        {
            if (cached.texture != null) UnityEngine.Object.Destroy(cached.texture);
        }
        _records.Clear();
        _byMaterial.Clear();
        _textures.Clear();
        if (Current == this) Current = null;
    }
}
