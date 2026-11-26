using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/*
 * フィールドを中心(AEGISタワー)から扇形に分けた、悪人のエリア分け(ソロ)
 * 位置がどの扇形に入るかで、その位置のエリア(CS_CrimeArea)を決める
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・置き方(Field_Solo1)
 *   Villain
 *   ├ CrimeAreaLayout   ← このコンポーネント。位置がエリア分けの中心になる
 *   │  ├ CrimeArea_生活区域      ← CS_CrimeArea(犯罪完遂ゲージ)
 *   │  ├ CrimeArea_エネルギー区域
 *   │  └ CrimeArea_インフラ区域
 *   └ VillainSpawnPoints  ← スポーン位置はここに置くだけ。どのエリアかは位置で自動で決まる
 * ・扇形(Sectors)
 *   エリアごとに、扇形の始まりの向き(startAngle)を持つ。向きは北(Z+)を0度として時計回り(東が90度)
 *   扇形は、始まりの向きから、次の扇形の始まりの向きまで(向きの小さい順に並べて考える。最後は最初へ戻る)
 *   初期値は仕様(#503の区画分けの図): 生活 243°〜0°(北西) / エネルギー 0°〜117°(東) / インフラ 117°〜243°(南)
 * ・スポーン位置のエリアは CS_VillainSpawnPoint.crimeArea が FindArea で求める
 *   エリア分けが無い(マルチ。Field_Solo1が無効)時はnullになり、グループごとの犯罪完遂のままになる
 * ・犯罪完遂ゲージUI: 有効な間、CS_CrimeAreaProgressFeedで各エリアの完遂率と、プレイヤーの今いるエリアをUIへ流す(プレイ中のみ)
 * ・表示: このオブジェクトかスポーン位置を選ぶと、Sceneビューの地面に扇形をエリアの色で描き、エリア名を出す
 *   スポーン位置もエリアの色の丸で描く。境目はこのオブジェクトを選んだ時につまんで回せる(CSED_CrimeAreaLayoutEditor)
 *   表示はオン・オフできる(showInEditor。巡回のツールバー・このオブジェクトのInspectorで切り替え。EditorPrefsで覚える)
 *   オフの間は、扇形・エリア名・スポーン位置の丸・境目の丸を描かない(エリア分けそのものは変わらない)
 *   → フィールドはオブジェクトが多いので、スポーン位置・エリア分けは毎回探さず、Hierarchyが変わった時だけ探し直す(FindForEditor / GetSpawnPointsForEditor)
 */
// ========================================

public class CS_CrimeAreaLayout : MonoBehaviour
{
    private const float _labelDistanceRate = 0.55f;   // エリア名を出す位置(扇形の半径に対する割合)
    private const float _spawnMarkRadius = 4f;        // スポーン位置の丸の半径(m)

    // 扇形1つ分(エリア1つ分)
    [Serializable]
    public class Sector
    {
        [SerializeField]
        [Tooltip("この扇形のエリア(犯罪完遂ゲージ)")]
        private CS_CrimeArea _area;

        [SerializeField, Range(0f, 360f)]
        [Tooltip("扇形の始まりの向き(度)。北(Z+)が0、時計回り(東が90)。次の扇形の始まりまでがこのエリア")]
        private float _startAngle;

        [SerializeField]
        [Tooltip("Sceneビューで描く色")]
        private Color _color = Color.white;

        public Sector(CS_CrimeArea area, float startAngle, Color color)
        {
            _area = area;
            _startAngle = Mathf.Repeat(startAngle, 360f);
            _color = color;
        }

        public CS_CrimeArea area => _area;
        public float startAngle
        {
            get => _startAngle;
            set => _startAngle = Mathf.Repeat(value, 360f);
        }
        public Color color => _color;
    }

    private static readonly List<CS_CrimeAreaLayout> _layouts = new List<CS_CrimeAreaLayout>();

    [SerializeField]
    private Sector[] _sectors = new Sector[0];

    [SerializeField, Min(1f)]
    [Tooltip("Sceneビューで扇形を描く半径(m)。フィールドの端まで届く大きさにする")]
    private float _displayRadius = 450f;

    private CS_CrimeAreaProgressFeed _progressFeed;   // 犯罪完遂ゲージUIへ値を流す(プレイ中のみ)

    public IReadOnlyList<Sector> sectors => _sectors;
    public float displayRadius => _displayRadius;

    private void OnEnable()
    {
        _layouts.Add(this);
        if (Application.isPlaying) _progressFeed = new CS_CrimeAreaProgressFeed(this);
    }

    private void OnDisable()
    {
        _layouts.Remove(this);
        _progressFeed?.Dispose();
        _progressFeed = null;
    }

    private void Update()
    {
        _progressFeed?.Tick();
    }

    // Domain Reloadを切っている場合に、前回の再生の一覧が残らないようにする
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _layouts.Clear();
    }

    // 有効なエリア分け(ソロ)で、位置のエリアを求める。エリア分けが無ければnull
    public static CS_CrimeArea FindArea(Vector3 position)
    {
        return _layouts.Count > 0 ? _layouts[0].GetArea(position) : null;
    }

    // 位置が入る扇形のエリア
    public CS_CrimeArea GetArea(Vector3 position)
    {
        Sector sector = GetSector(position);
        return sector != null ? sector.area : null;
    }

    // 位置が入る扇形(扇形が無ければnull)
    public Sector GetSector(Vector3 position)
    {
        if (_sectors.Length == 0) return null;

        float angle = GetAngle(position);
        Sector found = null;
        Sector last = null;
        foreach (Sector sector in _sectors)
        {
            if (last == null || sector.startAngle > last.startAngle) last = sector;
            if (sector.startAngle <= angle && (found == null || sector.startAngle > found.startAngle)) found = sector;
        }
        // 一番小さい始まりより手前の向きは、一番大きい始まりの扇形(北をまたぐ扇形)
        return found ?? last;
    }

    // 中心から見た位置の向き(度)。北(Z+)が0、時計回り
    public float GetAngle(Vector3 position)
    {
        Vector3 offset = position - transform.position;
        return Mathf.Repeat(Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg, 360f);
    }

    // 向き(度)の水平の向き
    public static Vector3 GetDirection(float angle)
    {
        return Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
    }

    // 扇形の終わりの向き(次の扇形の始まり)
    public float GetEndAngle(Sector sector)
    {
        float end = float.MaxValue;
        float first = float.MaxValue;
        foreach (Sector other in _sectors)
        {
            first = Mathf.Min(first, other.startAngle);
            if (other.startAngle > sector.startAngle) end = Mathf.Min(end, other.startAngle);
        }
        return end < float.MaxValue ? end : first + 360f;
    }

#if UNITY_EDITOR
    private static readonly List<CS_VillainSpawnPoint> _cachedSpawnPoints = new List<CS_VillainSpawnPoint>();
    private static CS_CrimeAreaLayout _cachedLayout;
    private static bool _isCacheDirty = true;
    private static GUIStyle _labelStyle;
    private const string _showInEditorKey = "CS_CrimeAreaLayout.ShowInEditor";

    // エディタ用: エリア分けをSceneビューに表示するか(オフにしても、エリア分けそのものは変わらない)
    public static bool showInEditor
    {
        get => EditorPrefs.GetBool(_showInEditorKey, true);
        set
        {
            EditorPrefs.SetBool(_showInEditorKey, value);
            SceneView.RepaintAll();
        }
    }

    // Hierarchyが変わったら、覚えておいたスポーン位置・エリア分けを探し直す
    [InitializeOnLoadMethod]
    private static void RegisterCacheReset()
    {
        EditorApplication.hierarchyChanged += () => _isCacheDirty = true;
        UnityEditor.SceneManagement.PrefabStage.prefabStageOpened += _ => _isCacheDirty = true;
        UnityEditor.SceneManagement.PrefabStage.prefabStageClosing += _ => _isCacheDirty = true;
    }

    private static void RefreshCache()
    {
        if (!_isCacheDirty) return;

        _isCacheDirty = false;
        _cachedSpawnPoints.Clear();
        _cachedSpawnPoints.AddRange(FindObjectsByType<CS_VillainSpawnPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        _cachedLayout = FindAnyObjectByType<CS_CrimeAreaLayout>();
    }

    // エディタ用: シーン(プレハブ編集中はプレハブ)のエリア分け。Hierarchyが変わるまでは覚えておいたものを返す
    public static CS_CrimeAreaLayout FindForEditor()
    {
        RefreshCache();
        return _cachedLayout;
    }

    // エディタ用: シーン(プレハブ編集中はプレハブ)のスポーン位置。Hierarchyが変わるまでは覚えておいたものを返す
    public static IReadOnlyList<CS_VillainSpawnPoint> GetSpawnPointsForEditor()
    {
        RefreshCache();
        return _cachedSpawnPoints;
    }

    private void OnDrawGizmosSelected()
    {
        DrawGizmos();
    }

    // 扇形・エリア名・スポーン位置をエリアの色で描く(スポーン位置を選んだ時にも、CS_VillainSpawnPointから呼ばれる)
    public void DrawGizmos()
    {
        if (!showInEditor) return;

        Vector3 center = transform.position;
        foreach (Sector sector in _sectors)
        {
            float start = sector.startAngle;
            float sweep = GetEndAngle(sector) - start;
            Color color = sector.color;

            Handles.color = new Color(color.r, color.g, color.b, 0.12f);
            Handles.DrawSolidArc(center, Vector3.up, GetDirection(start), sweep, _displayRadius);
            Handles.color = color;
            Handles.DrawAAPolyLine(4f, center, center + GetDirection(start) * _displayRadius);

            string name = sector.area != null ? sector.area.areaName : "(エリア未設定)";
            if (_labelStyle == null) _labelStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16 };
            _labelStyle.normal.textColor = color;
            Handles.Label(center + GetDirection(start + sweep * 0.5f) * (_displayRadius * _labelDistanceRate), name, _labelStyle);
        }

        foreach (CS_VillainSpawnPoint point in GetSpawnPointsForEditor())
        {
            if (point == null) continue;

            Sector sector = GetSector(point.transform.position);
            if (sector == null) continue;

            Handles.color = sector.color;
            Handles.DrawSolidDisc(point.transform.position, Vector3.up, _spawnMarkRadius);
        }
    }

    // 初期値(仕様の区画分け)。エリアは、名前が合うCS_CrimeAreaを子から探して入れる
    private void Reset()
    {
        _sectors = new[]
        {
            CreateSector("生活区域", 243f, new Color(0.35f, 0.9f, 0.45f)),
            CreateSector("エネルギー区域", 0f, new Color(0.95f, 0.25f, 0.25f)),
            CreateSector("インフラ区域", 117f, new Color(1f, 0.85f, 0.2f)),
        };
    }

    private Sector CreateSector(string areaName, float startAngle, Color color)
    {
        CS_CrimeArea found = null;
        foreach (CS_CrimeArea area in GetComponentsInChildren<CS_CrimeArea>())
        {
            if (area.areaName == areaName) found = area;
        }
        return new Sector(found, startAngle, color);
    }
#endif
}
