/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-11-16 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 配置ツールの、今選んでいる道具と、設定(置く物の大きさ・グラップルの値・警察の人数など)を持つクラス
/// 設定は、このPCのエディタ(EditorPrefs)に保存する
/// </summary>
public static class CSED_PlacementState
{
    /// <summary>
    /// 道具の種類
    /// </summary>
    public enum Tool
    {
        None,           // 何も置かない(Sceneビューは、ふつうの操作)
        SaObject,       // SAObject(柱 + 頭の箱)とハイドポイントを、セットで置く
        HidePoint,      // ハイドポイントだけを、クリックした面の上に置く
        TrafficLight,   // 信号機(飛ぶだけのSAポイント)を置く
        Grapple,        // グラップル(的)を置く
        Moving,         // パスに沿って動くSAポイントを置く(クリックで制御点を足す)
        Villain,        // 悪人の出現点を置く(巡回パターンは、巡回エディタで足す)
        Police,         // 警察のグループを置く(クリックで巡回ポイントを足す)
        Drone,          // 巡回ドローンを置く(クリックで巡回ポイントを足す)
        Laser,          // 防犯レーザーを置く(始点と終点の2クリック)
        Light,          // ストリートライトを置く(クリックするたびに、経路の点を足す)
        Delete,         // 置いた物を消す
    }

    /// <summary>
    /// 動くSAポイントにする、SAポイントの種類
    /// </summary>
    public enum MovingKind
    {
        TrafficLight,   // 信号機
        HidePoint,      // ハイドポイント
        Grapple,        // グラップル
    }

    private const string _prefix = "CSED_Placement_";

    public static Tool tool = Tool.None;

    // ---- SAObject・ハイドポイント ----
    public static bool autoPoleHeight
    {
        get => EditorPrefs.GetBool(_prefix + "AutoPole", true);
        set => EditorPrefs.SetBool(_prefix + "AutoPole", value);
    }

    public static float manualPoleHeight
    {
        get => EditorPrefs.GetFloat(_prefix + "ManualPole", 8.0f);
        set => EditorPrefs.SetFloat(_prefix + "ManualPole", value);
    }

    public static bool keepLight
    {
        get => EditorPrefs.GetBool(_prefix + "KeepLight", false);
        set => EditorPrefs.SetBool(_prefix + "KeepLight", value);
    }

    // 足元の範囲の箱(SAReachBox)を、置いた根元より何m下まで伸ばすか(低い所から飛び立てるようにする)
    public static float reachDown
    {
        get => EditorPrefs.GetFloat(_prefix + "ReachDown", 0.5f);
        set => EditorPrefs.SetFloat(_prefix + "ReachDown", value);
    }

    // ---- グラップル ----
    public static float pullSpeed
    {
        get => EditorPrefs.GetFloat(_prefix + "PullSpeed", 35.0f);
        set => EditorPrefs.SetFloat(_prefix + "PullSpeed", value);
    }

    public static float releaseDistanceRate
    {
        get => EditorPrefs.GetFloat(_prefix + "ReleaseRate", 0.5f);
        set => EditorPrefs.SetFloat(_prefix + "ReleaseRate", value);
    }

    public static float releaseDrag
    {
        get => EditorPrefs.GetFloat(_prefix + "ReleaseDrag", 0.5f);
        set => EditorPrefs.SetFloat(_prefix + "ReleaseDrag", value);
    }

    // グラップルを、カメラの手前側から飛べる向き(的の手前がカメラ側)に置くか
    public static bool grappleFaceCamera
    {
        get => EditorPrefs.GetBool(_prefix + "GrappleFaceCamera", true);
        set => EditorPrefs.SetBool(_prefix + "GrappleFaceCamera", value);
    }

    // ---- 動くSAポイント ----
    public static MovingKind movingKind
    {
        get => (MovingKind)EditorPrefs.GetInt(_prefix + "MovingKind", 0);
        set => EditorPrefs.SetInt(_prefix + "MovingKind", (int)value);
    }

    public static CSE_PathFollowMode movingMode
    {
        get => (CSE_PathFollowMode)EditorPrefs.GetInt(_prefix + "MovingMode", (int)CSE_PathFollowMode.PingPong);
        set => EditorPrefs.SetInt(_prefix + "MovingMode", (int)value);
    }

    public static float movingSpeed
    {
        get => EditorPrefs.GetFloat(_prefix + "MovingSpeed", 4.0f);
        set => EditorPrefs.SetFloat(_prefix + "MovingSpeed", value);
    }

    public static float movingWait
    {
        get => EditorPrefs.GetFloat(_prefix + "MovingWait", 0.0f);
        set => EditorPrefs.SetFloat(_prefix + "MovingWait", value);
    }

    // 制御点を、クリックした面から何m上に置くか
    public static float movingHeight
    {
        get => EditorPrefs.GetFloat(_prefix + "MovingHeight", 0.0f);
        set => EditorPrefs.SetFloat(_prefix + "MovingHeight", value);
    }

    // ---- 悪人 ----

    // 悪人の出現点を、地面から何m浮かせて置くか(犯罪完遂ゲージを見えるようにするため。巡回パターンの点は、そこから地面へ下りた位置に作る)
    public static float villainHeight
    {
        get => EditorPrefs.GetFloat(_prefix + "VillainHeight", 6.2f);
        set => EditorPrefs.SetFloat(_prefix + "VillainHeight", value);
    }

    // 悪人の出現点を置く犯罪完遂エリアを、区画分けの図から自動で決めるか(falseなら villainArea)
    public static bool villainAreaAuto
    {
        get => EditorPrefs.GetBool(_prefix + "VillainAreaAuto", true);
        set => EditorPrefs.SetBool(_prefix + "VillainAreaAuto", value);
    }

    public static int villainArea
    {
        get => EditorPrefs.GetInt(_prefix + "VillainArea", 1);
        set => EditorPrefs.SetInt(_prefix + "VillainArea", Mathf.Clamp(value, 1, 3));
    }

    // 悪人の道具を選んでいる間、悪人の分布(足りている所・過疎の所)を、Sceneビューに色で出すか
    public static bool villainOverlay
    {
        get => EditorPrefs.GetBool(_prefix + "VillainOverlay", true);
        set => EditorPrefs.SetBool(_prefix + "VillainOverlay", value);
    }

    // 悪人が足りている(色が緑)とみなす、悪人からの距離(m)。この距離の1.6倍より遠い所は、赤(過疎)
    public static float villainCoverRadius
    {
        get => EditorPrefs.GetFloat(_prefix + "VillainCoverRadius", 60.0f);
        set => EditorPrefs.SetFloat(_prefix + "VillainCoverRadius", Mathf.Clamp(value, 10.0f, 300.0f));
    }

    // 警察のグループ・巡回ドローンの、分布の表示(悪人と同じ)
    public static bool policeOverlay
    {
        get => EditorPrefs.GetBool(_prefix + "PoliceOverlay", true);
        set => EditorPrefs.SetBool(_prefix + "PoliceOverlay", value);
    }

    public static float policeCoverRadius
    {
        get => EditorPrefs.GetFloat(_prefix + "PoliceCoverRadius", 50.0f);
        set => EditorPrefs.SetFloat(_prefix + "PoliceCoverRadius", Mathf.Clamp(value, 10.0f, 300.0f));
    }

    public static bool droneOverlay
    {
        get => EditorPrefs.GetBool(_prefix + "DroneOverlay", true);
        set => EditorPrefs.SetBool(_prefix + "DroneOverlay", value);
    }

    public static float droneCoverRadius
    {
        get => EditorPrefs.GetFloat(_prefix + "DroneCoverRadius", 50.0f);
        set => EditorPrefs.SetFloat(_prefix + "DroneCoverRadius", Mathf.Clamp(value, 10.0f, 300.0f));
    }

    /// <summary>
    /// 種類ごとの、分布の表示のオン・オフと、足りている半径(m)
    /// </summary>
    public static bool GetOverlayEnabled(CSED_PlacementOverlay.Kind kind)
    {
        return kind == CSED_PlacementOverlay.Kind.Villain ? villainOverlay : (kind == CSED_PlacementOverlay.Kind.Police ? policeOverlay : droneOverlay);
    }

    public static void SetOverlayEnabled(CSED_PlacementOverlay.Kind kind, bool value)
    {
        if (kind == CSED_PlacementOverlay.Kind.Villain) villainOverlay = value;
        else if (kind == CSED_PlacementOverlay.Kind.Police) policeOverlay = value;
        else droneOverlay = value;
    }

    public static float GetCoverRadius(CSED_PlacementOverlay.Kind kind)
    {
        return kind == CSED_PlacementOverlay.Kind.Villain ? villainCoverRadius : (kind == CSED_PlacementOverlay.Kind.Police ? policeCoverRadius : droneCoverRadius);
    }

    public static void SetCoverRadius(CSED_PlacementOverlay.Kind kind, float value)
    {
        if (kind == CSED_PlacementOverlay.Kind.Villain) villainCoverRadius = value;
        else if (kind == CSED_PlacementOverlay.Kind.Police) policeCoverRadius = value;
        else droneCoverRadius = value;
    }

    // 悪人の影響範囲を、巡回グループのルート(巡回パターンのルートの線・往復の線)からの距離で測るか(falseなら、出現点からの半径)
    public static bool villainUseRoutes
    {
        get => EditorPrefs.GetBool(_prefix + "VillainUseRoutes", true);
        set => EditorPrefs.SetBool(_prefix + "VillainUseRoutes", value);
    }

    // 区画の境界線と、区画ごとの個数を、Sceneビューに出すか
    public static bool villainOverlayBorders
    {
        get => EditorPrefs.GetBool(_prefix + "VillainOverlayBorders", true);
        set => EditorPrefs.SetBool(_prefix + "VillainOverlayBorders", value);
    }

    // ---- 警察 ----

    // 警察のグループの人数(1〜3)
    public static int policeMembers
    {
        get => EditorPrefs.GetInt(_prefix + "PoliceMembers", 2);
        set => EditorPrefs.SetInt(_prefix + "PoliceMembers", Mathf.Clamp(value, 1, 3));
    }

    // 警察の巡回ポイントを、クリックした点の近くの、歩ける場所(NavMesh)へ合わせるか
    public static bool policeSnapNavMesh
    {
        get => EditorPrefs.GetBool(_prefix + "PoliceSnap", true);
        set => EditorPrefs.SetBool(_prefix + "PoliceSnap", value);
    }

    // ---- ドローン ----

    // ドローンの巡回ポイントを、クリックした面から何m上に置くか
    public static float droneHeight
    {
        get => EditorPrefs.GetFloat(_prefix + "DroneHeight", 4.0f);
        set => EditorPrefs.SetFloat(_prefix + "DroneHeight", value);
    }

    // ---- レーザー・ライト ----

    // レーザーの長さを、通路の幅から自動で決めるか(falseなら laserBeamLength)
    public static bool laserBeamAuto
    {
        get => EditorPrefs.GetBool(_prefix + "LaserAuto", true);
        set => EditorPrefs.SetBool(_prefix + "LaserAuto", value);
    }

    public static float laserBeamLength
    {
        get => EditorPrefs.GetFloat(_prefix + "LaserLength", 6.0f);
        set => EditorPrefs.SetFloat(_prefix + "LaserLength", value);
    }

    // ライトの円の直径を、道幅から自動で決めるか(falseなら lightDiameter)
    public static bool lightDiameterAuto
    {
        get => EditorPrefs.GetBool(_prefix + "LightAuto", true);
        set => EditorPrefs.SetBool(_prefix + "LightAuto", value);
    }

    // ライトを、経路の最後から最初へ戻って周回させるか(falseなら、端で折り返して往復する)
    public static bool lightLoop
    {
        get => EditorPrefs.GetBool(_prefix + "LightLoop", false);
        set => EditorPrefs.SetBool(_prefix + "LightLoop", value);
    }

    public static float lightDiameter
    {
        get => EditorPrefs.GetFloat(_prefix + "LightDiameter", 12.0f);
        set => EditorPrefs.SetFloat(_prefix + "LightDiameter", value);
    }

    // ---- 置く先の親(シーンの一番上から、/ で区切ったパス。無ければ、置く時に作る) ----

    // 置く先の親を決める種類(道具ごとに、別の親を設定できる)
    public enum GroupKind
    {
        SaObject,       // SAObject
        HidePoint,      // ハイドポイント
        Action,         // 信号機・グラップル・動くSAポイント
        VillainArea1,   // 敵(区画1。CrimeArea_01)
        VillainArea2,   // 敵(区画2。CrimeArea_02)
        VillainArea3,   // 敵(区画3。CrimeArea_03)
        Police,         // 警察のグループ
        Drone,          // 巡回ドローン
        Laser,          // 防犯レーザー
        Light,          // ストリートライト
    }

    private const string _fieldPath = "Field/Field_Solo1/";

    /// <summary>
    /// 種類ごとの、標準の親のパスを返すメソッド
    /// </summary>
    public static string GetDefaultParentPath(GroupKind kind)
    {
        switch (kind)
        {
            case GroupKind.SaObject: return _fieldPath + "SAObjects";
            case GroupKind.HidePoint: return _fieldPath + "HidePoints";
            case GroupKind.Action: return _fieldPath + "ActionObjects";
            case GroupKind.VillainArea1: return _fieldPath + "Villain/VillainSpawnPoints/CrimeArea_01";
            case GroupKind.VillainArea2: return _fieldPath + "Villain/VillainSpawnPoints/CrimeArea_02";
            case GroupKind.VillainArea3: return _fieldPath + "Villain/VillainSpawnPoints/CrimeArea_03";
            case GroupKind.Police: return _fieldPath + "Police";
            case GroupKind.Drone: return _fieldPath + "SecurityDrones";
            case GroupKind.Laser: return _fieldPath + "SecurityLasers";
            default: return _fieldPath + "SecurityLights";
        }
    }

    /// <summary>
    /// 敵を置く区画(1〜3)から、置く先の親の種類を返すメソッド
    /// </summary>
    public static GroupKind GetVillainKind(int area)
    {
        return (GroupKind)((int)GroupKind.VillainArea1 + Mathf.Clamp(area, 1, 3) - 1);
    }

    /// <summary>
    /// パスを整えるメソッド(各名前の前後の空白と、空の名前を取り除いて、/ でつなぐ)
    /// </summary>
    public static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;

        System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>();
        foreach (string part in path.Replace('\\', '/').Split('/'))
        {
            string trimmed = part.Trim();
            if (trimmed.Length > 0) parts.Add(trimmed);
        }
        return string.Join("/", parts);
    }

    /// <summary>
    /// 種類ごとの、置く先の親のパスを取得・設定するメソッド(空にすると、標準に戻る)
    /// </summary>
    public static string GetParentPath(GroupKind kind)
    {
        string path = NormalizePath(EditorPrefs.GetString(_prefix + "Parent" + kind, string.Empty));
        return path.Length > 0 ? path : GetDefaultParentPath(kind);
    }

    public static void SetParentPath(GroupKind kind, string path)
    {
        string normalized = NormalizePath(path);
        if (normalized.Length == 0 || normalized == GetDefaultParentPath(kind)) EditorPrefs.DeleteKey(_prefix + "Parent" + kind);
        else EditorPrefs.SetString(_prefix + "Parent" + kind, normalized);
    }

    // ---- 作業中の物 ----

    // 今、制御点を足している(まだ確定していない)動くSAポイント
    public static CS_PathFollower activeFollower;

    // 今、巡回ポイントを足している(まだ確定していない)警察のグループ・巡回ドローン
    public static CS_PoliceSquad activeSquad;
    public static CS_SecurityDroneSpawner activeDrone;

    // 今、経路の点を足している(まだ確定していない)ストリートライト
    public static CS_SecurityLight activeLight;

    // 2クリックで置く物(レーザー)の、1回目にクリックした点
    public static bool hasPendingStart;
    public static Vector3 pendingStart;

    /// <summary>
    /// 作業中の物(確定していない巡回ポイント・1回目のクリック)を、すべて外すメソッド
    /// </summary>
    public static void ClearWorking()
    {
        activeFollower = null;
        activeSquad = null;
        activeDrone = null;
        activeLight = null;
        hasPendingStart = false;
    }

    /// <summary>
    /// 作業中(巡回ポイントを足している・2クリック目を待っている)かを返すメソッド
    /// </summary>
    public static bool isWorking => activeFollower != null || activeSquad != null || activeDrone != null || activeLight != null || hasPendingStart;

    // ---- 置いた物の一覧(プレハブへ反映するために覚えておく。スクリプトを再読み込みすると、空になる) ----

    private static readonly List<GameObject> _created = new List<GameObject>();

    public static void RememberCreated(GameObject created)
    {
        _created.Add(created);
    }

    public static List<GameObject> GetCreated()
    {
        _created.RemoveAll(created => created == null);
        return new List<GameObject>(_created);
    }

    public static void ClearCreated()
    {
        _created.Clear();
    }
}
