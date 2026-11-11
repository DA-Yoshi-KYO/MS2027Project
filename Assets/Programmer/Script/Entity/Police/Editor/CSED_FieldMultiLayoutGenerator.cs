/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-11-09 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// マルチ用のフィールド(Field_Multi.prefab)の、悪人・警察・防犯装置・SAポイントを、フィールドの形から自動で配置し直すエディタ専用ツール
/// 方針
///   ・悪人の出現点は、3つのエリアごとに5つ置く。大通りのような広い道には置かず、狭い道(路地裏のような所。幅 _maxVillainStreetWidth 以下)に3つと、
///     低い屋上の歩ける場所(高さ4〜14m。屋上にSAのSAObjectを立てて、SAで飛んで行ける所だけ)に2つ置く。エリアごとに悪人の動きに特色を付ける
///     エリア0: 動き回る(長く往復・見回しは短い) / エリア1: 周囲を見渡す(短く往復・見回しが長い) / エリア2: その中間(高所から見下ろされる街)
///   ・悪人のいる場所の近くには、必ず警察陣営(警察のグループ・巡回ドローン・防犯レーザー)を置く。警察は地上だけ
///     警察のグループは、悪人の出現点ごとに1グループ(巡回ルートは、そのまわりの広い範囲(半径60〜120m)の道を一周する)と、
///     エリアごとに、グループの空いている所(他のグループの巡回ポイントから一番遠い所)を中心にしたグループを足す
///     ルートは、一部が他のグループと重なってよい(重なりが少ない向きを選ぶが、重なってもあきらめない)
///     ルートは、悪人の出現点のすぐそば(_routeNearRadius)と、地上のSAObjectの近く(_routeSnapDistance以内なら、SAObjectの柱の横)を通る
///     隣り合う巡回ポイントの間をまっすぐ結ぶと、建物などを貫通する所には、道なり(歩ける道のりの曲がり角)の中継ポイントを足して、
///     ルートの線が建物を貫通しないようにする
///     1グループは2〜3人(ルートが長ければ3人)。隊列は組まず、離れて、同じルートを、等間隔にずれたポイントから、それぞれ回る(離れて巡回)
///   ・ドローンとレーザーは、屋上などの高所に置く
///   ・SAポイントは、悪人の近くの屋上(悪人の上を取れる高さ)と、その屋上から少し高い屋上へ。街の地上には、道に沿ってSAポイントを並べる
///     SAで飛べる距離は、プレイヤーの飛ぶ速さ(25m/秒)×飛べる時間(1秒)で、約25mまで。そのため、飛んで着く先(SAObjectの頭の上)は、
///     足元の範囲の箱のすぐ近くに置く(箱は、SAObjectのまわりと、飛び立つ足元の高さを含む。箱の中のどこからでも、約22m以内で着く大きさにする)
///   ・SAObjectは、道のど真ん中には立てず、道の端(歩ける場所の縁。歩道・路肩)に立てる。端までが遠い場所(広場の真ん中など)には立てない
///     SAObject同士は、_saMinSpacing(m)以上離す(近すぎるものは置かない)
///   ・飛んで着く先には、ソロ用と同じように、信号・街灯に見立てた仮オブジェクト(SAObject)を立て、その頭の上にHidePointの足元を置く
///     SAObjectの構成は、ソロ用(Field.prefabのSAObjects)と同じ: SAObjects → SAObject → Cube(頭。2m角。MT_SAPoint) と Cube (1)(柱。太さ0.5m。Road.mat)
///     名前も同じ(SAObject、SAObject (1)、…。HidePointも HidePoint、HidePoint (1)、…)で、ソロ用と同じく、屋上・地上の分け方はせず、1つの親に並べる
/// 屋上は、上から下へ光線を撃って見つける(水平な面で、まわりも同じ高さのもの)。地上は、歩ける場所(NavMesh)から選ぶ
/// メニュー「Tools/Police/マルチのフィールドを配置し直す」で実行する(MainSceneを開いてから)。結果は Temp/FieldMultiLayoutReport.txt
/// 置き場所は、ソロ用(Field_Solo1)と同じように、種類ごとの親の下にまとめる
///   Police(警察のグループ。もとからある物と同じ親) / Villain/VillainSpawnPoints / SecurityLasers / SecurityLights / SecurityDrones / HidePoints / SAObjects
/// 前に生成した物(警察のグループ・悪人の出現点・防犯レーザー・ドローン・SAポイント)は消して、作り直す(ストリートライトは、SecurityLightsへ移して残す)
/// </summary>
public static class CSED_FieldMultiLayoutGenerator
{
    // ---- エリアごとの悪人の特色(巡回の端までの距離(m)・端で見回す時間(秒)) ----
    private static readonly float[] _trailDistance = { 25.0f, 5.0f, 12.0f };
    private static readonly float[] _trailLookTime = { 1.5f, 8.0f, 4.0f };

    // ---- 数 ----
    private const int _sitesPerArea = 5;
    private const float _villainLineClearance = 2.0f;     // SAObjectの柱を、悪人の往復ルートの線から離す距離(m)
    private const float _fixSearchRadius = 8.0f;          // 埋まったSAObjectを、ずらして探す範囲(m)
    private const float _fixRoadsideMax = 1.8f;           // ずらした先が、歩ける場所の縁から離れてよい距離の上限(m。これより遠いと、道のど真ん中)
    private const float _patrolEndMargin = 1.5f;          // 悪人の往復ルートの両端を、壁・柱・縁から離す距離(m)
    private const int _groundSitesPerArea = 2;           // エリアごとの悪人の出現点のうち、地上の狭い道に置く数(残りは、屋上などの高所。SAで倒しに行く)
    private const float _maxVillainStreetWidth = 16.0f;   // 地上の悪人の出現点を置ける、道の幅の上限(m。ビルの間のある程度広い所まで。これより広い道は、大通りとみなして置かない)
    private const int _roofSitesPerArea = 3;              // エリアごとの、登れるビルの屋上の悪人の出現点の数
    private const float _roofVillainMinHeight = 8.0f;     // 登れるビルの屋上の悪人の出現点の高さの下限(m。地上から)
    private const float _roofVillainMaxHeight = 120.0f;
    private const float _roofVillainNearSa = 30.0f;       // 登れる屋上のSAObjectから、悪人の出現点を離してよい距離の上限(m)
    private const float _roofVillainMinPatrol = 3.0f;     // 屋上の悪人の往復ルートの、片側の長さの下限(m)
    private const float _groupRadiusMargin = 1.5f;        // 悪人のグループの広がり(半径)の分、SAObjectの柱から離す距離(m)
    private const float _roofSiteMinEdge = 2.5f;          // 屋上の悪人の出現点は、屋上の縁からこの距離(m)以上内側に置く
    private const float _roofSiteMinHeight = 4.0f;        // 屋上の悪人の出現点の高さの範囲(m。SAで飛んで行ける高さ)
    private const float _roofSiteMaxHeight = 14.0f;
    private const int _extraSquadsPerArea = 2;   // エリアごとに足す、警察のグループの数(悪人の出現点ごとのグループとは別。空いている所に置く)
    private const int _droneStride = 3;          // ドローンを置く悪人の出現点の間隔(番号を、この数で割った余りが1の所に置く)
    private const int _sweepGroundPerArea = 3;          // エリアごとの、地上の通路を横切って往復するレーザーの数
    private const int _sweepRoofPerArea = 2;            // エリアごとの、屋上を横切って往復するレーザーの数
    private const float _sweepSpacing = 45.0f;          // 往復するレーザー同士の、往復の線を離す距離(m)
    private const float _sweepMinLength = 10.0f;        // 往復する距離の範囲(m。上限は、CS_SecurityLaserの往復の距離の上限50mに収める)
    private const float _sweepMaxLength = 40.0f;
    private static readonly float[] _sweepLengths = { 40.0f, 30.0f, 22.0f, 16.0f, 12.0f };   // 往復する距離の候補(長い順。m)
    private static readonly float[] _sweepStartShares = { 1.0f, 0.6f, 0.3f, 0.0f };      // 始点の位置の候補(通路の後ろの端から、点までの間の、どこまで戻るか)
    private const float _sweepHeight = 1.0f;            // レーザーの高さ(足元から。m。ジャンプで越える・かわす高さ)
    private const float _sweepBeamThickness = 0.3f;     // レーザーの太さ(m)
    private const float _sweepBeamExtra = 0.6f;         // レーザーの長さを、歩ける幅より足す長さ(m。壁際をすり抜けられないように)
    private const float _sweepGroundMinWidth = 3.0f;    // 地上の通路の、道幅の範囲(m)
    private const float _sweepGroundMaxWidth = 12.0f;
    private const float _sweepGroundWideWidth = 24.0f;   // 道幅が _sweepGroundMaxWidth までの通路が無いエリアで、選ぶ通路の道幅の上限(m)
    private const float _sweepWideFromVillain = 150.0f; // その時の、悪人の出現点からの距離の上限(m)
    private const float _sweepRoofMinWidth = 4.0f;      // 屋上の通路(屋上の幅)の範囲(m)
    private const float _sweepRoofMaxWidth = 40.0f;
    private const float _sweepMinFromVillain = 10.0f;   // 地上のレーザーを置く、悪人の出現点からの距離の範囲(m)
    private const float _sweepMaxFromVillain = 80.0f;
    private const float _sweepRoofMinFromVillain = 6.0f;    // 屋上のレーザーを置く、悪人の出現点からの距離の範囲(m)
    private const float _sweepRoofMaxFromVillain = 35.0f;
    private const int _lightsPerArea = 4;              // エリアごとのストリートライトの数(ソロ用は全体で12本)
    private const float _lightDistance = 100.0f;       // ストリートライトが往復する距離の上限(m。ソロ用と同じ)
    private const float _lightMinLine = 60.0f;         // ストリートライトを置く大通りの、まっすぐ続く長さの下限(m)
    private const float _lightMinWidth = 12.0f;        // ストリートライトを置く大通りの、道幅の下限(m。これより狭い道には置かない)
    private const float _lightMinSpacing = 60.0f;      // ストリートライトの往復する線同士を離す距離(m)
    private const float _lightHeightOffset = 0.25f;    // ストリートライトを、歩ける床から浮かせる高さ(m。ソロ用は床+0.25m)

    // ---- 距離(m) ----
    private const float _siteMinFromCenter = 45.0f;
    private const float _siteMaxFromCenter = 170.0f;
    private const float _siteMinFromStart = 35.0f;
    private const float _siteMinSpacing = 40.0f;
    private const float _perchMinDistance = 12.0f;
    private const float _perchMaxDistance = 50.0f;
    private const float _policeCoverDistance = 60.0f;   // 悪人の出現点から、警察陣営までの距離の上限(m)
    private const float _routeNearRadius = 8.0f;          // 巡回ルートの最初のポイントを、悪人の出現点から離す距離(m。悪人のすぐそばを通る)
    private const float _routeSnapDistance = 15.0f;       // 巡回ポイントの近く(この距離以内)に地上のSAObjectがあれば、その根元をポイントにする(SAObjectの並ぶ道を通る)
    private const float _minWaypointSpacing = 3.0f;      // 足す中継ポイントを、直前のポイントからこの距離(m)以上離す
    private const float _routeBesidePole = 1.8f;         // SAObjectに寄せた巡回ポイントを、柱の根元から離す距離(m。柱にぶつからない位置)
    private const float _longRouteLength = 500.0f;        // この長さ(m)以上の巡回ルートには3人、それより短いルートには2人置く
    private const int _shortRouteMembers = 2;
    private const int _longRouteMembers = 3;
    private const float _routeOverlapDistance = 30.0f;     // 他のグループの巡回ポイントが、この距離以内にあると、重なっているとみなす(m)
    private const float _detourRatio = 2.5f;              // 隣のポイントまでの道のりが、まっすぐの距離の何倍までなら、遠回りしすぎでないとみなすか
    private const float _relaxedDetourRatio = 4.0f;        // 広いルートが作れない街で、ゆるめに使う倍率
    private const float _minExtraRouteLength = 300.0f;  // 空いている所に足すグループの、巡回ルートの一周の長さの下限(m。これより短いルートは、意味が薄いので置かない)
    private const int _gapCandidateTries = 14;             // 空いている所のグループを置く時に、試す中心の候補の数
    private const int _routePoints = 10;                    // 巡回ルートのポイントの数(上限)
    private const float _roadSpacing = 22.0f;            // 地上のSAObjectを立てる間隔の目安(m。地上のどの点からも、この距離以内に1本ある。短いほど、ただ走る移動が減る)
    private const float _runSpeed = 5.0f;                // 街を走る速さ(m/秒。プレイヤーの moveSpeed の初期値)
    private const float _lightCoverageMin = 0.85f;      // ライトの円(往復する範囲)のうち、道(歩ける場所)の上にある割合の下限
    private const float _climbMinTargetHeight = 8.0f;   // 登れるようにするビルの屋上の高さの下限(m。地上から)
    private const float _climbMinRoofHeight = 4.0f;     // 登る時に足場にする屋上の高さの範囲(m)
    private const float _climbMaxRoofHeight = 120.0f;
    private const float _climbGrid = 3.0f;              // 登る足場の屋上を探す格子の間隔(m)
    private const float _climbMinStep = 6.0f;           // 1回で飛び移る水平距離の下限(m。上限は、足元の範囲の箱の広さ)
    private const float _climbMinGain = 0.0f;           // 1回で飛び移る高さの差の範囲(m)
    private const float _climbMaxGain = 19.0f;
    private const int _climbMaxSteps = 20;              // 登る段の数の上限
    private const float _ledgeMinHeight = 6.0f;         // 壁に足す足場(壁から張り出したSAObject)の、一番低い高さ(m。足元)
    private const float _ledgeStep = 6.0f;              // 壁に足す足場の候補を並べる、高さの間隔(m)
    private const int _ledgeEvery = 3;                  // 壁に足す足場の候補を、屋上の縁の格子の何点おきに置くか
    private const float _ledgeProbe = 7.0f;             // 屋上の縁の外を調べる距離(m。この先が低くなっていれば、本物の縁)
    private const float _ledgeDrop = 4.0f;              // 屋上の縁の外が、屋上よりこれ以上低ければ、本物の縁とみなす(m)
    private const float _ledgeArmWidth = 0.35f;         // 壁と足場の柱をつなぐ腕の太さ(m)
    private const int _climbTriesPerBuilding = 40;     // 1棟のビルで、登り道を作れる屋上の点を試す数の上限
    private const float _climbSameRoof = 1.0f;          // 隣り合う屋上の点の高さの差がこれ以内なら、同じビルの屋上とみなす(m)
    private const float _maxRunSeconds = 120.0f;         // ただ走る移動の長さの上限(秒。これを超える場所が無いようにする)
    private const float _maxFlightDistance = 22.0f;      // SAで飛んで着くまでの距離の上限(m)。プレイヤーの飛ぶ速さ(25m/秒)×飛べる時間(1秒)より、少し短くする
    private const float _maxReachHalf = 18.0f;           // 足元の範囲の箱の、水平方向の半分の大きさの上限(m)

    // ---- 高さ(m) ----
    private const float _groundHeight = 3.0f;
    private const float _perchMinAbove = 4.0f;
    private const float _perchMaxAbove = 14.0f;          // SAで飛べる屋上の高さの上限(悪人の足元から。飛べる距離に収まる高さ)
    private const float _chainMinGain = 4.0f;
    private const float _chainMaxGain = 12.0f;
    private const float _highMinAbove = 6.0f;            // ドローン・レーザーを置く屋上の高さ(悪人の足元から。こちらは高所なら、高いほどよい)
    private const float _highMaxAbove = 60.0f;
    private const float _droneAboveRoof = 4.0f;

    private const float _roofStartHeight = 250.0f;
    private const float _roofGrid = 2.5f;
    private const string _legacyContainerPrefix = "Layout_";     // 以前の版が作った入れ物(見つけたら消す)
    private const string _policeGroup = "Police";
    private const string _laserGroup = "SecurityLasers";
    private const string _lightGroup = "SecurityLights";
    private const string _handLaserGroup = "SecurityLaseres";    // 手で置いたレーザーの親(ソロ用と同じ綴り)。配置し直す時は消す
    private const string _handLightGroup = "ScurityLights";      // 手で置いたライトの親。配置し直す時は消す
    private const string _droneGroup = "SecurityDrones";
    private const string _hideGroup = "HidePoints";
    private const string _saObjectGroup = "SAObjects";         // SAポイントの足場にする、信号・街灯に見立てた仮オブジェクト(SAObject)の親
    private const float _roofPoleHeight = 4.0f;                // 屋上に立てるSAObjectの柱の高さ(m)
    private const float _roadPoleHeight = 8.0f;                // 地上に立てるSAObjectの柱の高さ(m。信号・街灯の高さ。頭の上までで10m)
    private const float _saHeadSize = 2.0f;                    // SAObjectの頭(箱)の大きさ(m)
    private const float _saPoleWidth = 0.5f;                   // SAObjectの柱の太さ(m)
    private const float _saMinSpacing = 14.0f;                // SAObject同士が、この距離(m)より近い物は置かない
    private const float _roadsideMaxShift = 20.0f;             // 道の端へ寄せる時に、動かしてよい距離の上限(m)。これより端が遠い場所(道のど真ん中)には置かない
    private const float _poleMargin = 0.3f;                    // 柱・頭・隠れる箱が、壁などからこの距離(m)以上あいていることを確かめる
    private const float _poleBottomLift = 0.3f;                // 柱の底を調べる時に、地面そのものに当たらないよう、足元から浮かせる高さ(m)
    private const string _generatedSquadPrefix = "PoliceSquad_A";   // このツールが置いた警察のグループの名前の先頭
    private const string _saHeadMaterialPath = "Assets/Programmer/Material/MT_SAPoint.mat";                         // SAObjectの頭(Cube)のマテリアル(ソロ用と同じ)
    private const string _saPoleMaterialPath = "Assets/Programmer/PlaceholderAsset/Field/Material/Road.mat";        // SAObjectの柱(Cube (1))のマテリアル(ソロ用と同じ)
    private const string _squadPrefabPath = "Assets/Programmer/Prefab/Field/FieldElement/Police/PoliceSquad.prefab";

    private class Site
    {
        public int area;
        public int index;
        public Vector3 position;
        public float yaw;
        public bool isRoof;      // 屋上の出現点か(地上の狭い道ではなく)
        public float patrolHalf; // 往復する巡回ルートの、片側の長さの上限(m。両端が壁や柱に当たらない長さ)
    }

    private static Transform _multi;
    private static StringBuilder _report;
    private static List<Vector3> _areaCenters;
    private static List<Vector3> _startPoints;
    private static List<Vector3> _ground;
    private static List<(Vector3 a, Vector3 b)> _villainLines;   // 悪人の往復ルート(両端の線)。SAObjectの柱をここに立てない
    private static bool _relaxSites;               // 悪人の出現点を選ぶ条件を、ゆるめているか(道の幅の上限・中心からの距離の上限)
    private static List<Vector3> _roofNavAll;       // 高さ _roofVillainMinHeight〜_roofVillainMaxHeight の屋上の、歩ける場所(NavMesh)の点
    private static List<Vector3> _climbRoofNodes = new List<Vector3>();   // 登れる屋上のSAObjectの位置(屋上の点)
    private static List<Vector3> _roofNav;          // 低い屋上(高さ _roofSiteMinHeight〜_roofSiteMaxHeight)の、歩ける場所(NavMesh)の点
    private static float _nearRadius = _routeNearRadius;   // 今作っている巡回ルートの、最初のポイントを悪人の出現点から離す距離(m)
    private static List<Vector3> _squadPoints;     // 置いた警察のグループ(もとからある物を含む)の巡回ポイントの位置(重なりを避けるのに使う)
    private static List<Vector3> _policeFaction;   // 置いた警察陣営(警察の巡回ポイント・ドローンの巡回ポイント・レーザー)の位置

    /// <summary>
    /// 配置し直すメソッド
    /// </summary>
    [MenuItem("Tools/Police/マルチのフィールドを配置し直す")]
    private static void Generate()
    {
        _report = new StringBuilder();
        _policeFaction = new List<Vector3>();
        _saObjectCount = 0;
        _saBases = new List<Vector3>();
        _villainLines = new List<(Vector3 a, Vector3 b)>();
        _skippedTooClose = 0;
        _skippedMidRoad = 0;
        _hidePointCount = 0;
        _squadPoints = new List<Vector3>();
        _multi = FindMulti();
        if (_multi == null)
        {
            Debug.LogError("CSED_FieldMultiLayoutGenerator: シーンに Field/Field_Multi が見つかりません(MainSceneを開いてください)");
            return;
        }

        // 前に生成した物のコライダーが、空き(柱を立てられるか)の判定に混ざらないよう、調べている間だけ無効にする
        List<GameObject> hidden = DisablePreviousLayout();
        try
        {
            CollectGeometry();
        }
        finally
        {
            RestoreLayout(hidden);
        }
        hidden = DisablePreviousLayout();
        string path = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(_multi.gameObject));
        if (!path.EndsWith("/Field_Multi.prefab"))
        {
            Debug.LogError($"CSED_FieldMultiLayoutGenerator: コピー先が Field_Multi.prefab ではありません({path})。中止しました");
            return;
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            LayoutAll(contents);
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally
        {
            RestoreLayout(hidden);
            PrefabUtility.UnloadPrefabContents(contents);
            string reportPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "FieldMultiLayoutReport.txt");
            File.WriteAllText(reportPath, _report.ToString(), new UTF8Encoding(false));
        }

        Debug.Log("CSED_FieldMultiLayoutGenerator: 配置し直しました");
    }

    /// <summary>
    /// マルチのフィールドの、防犯レーザーとストリートライトだけを、配置し直すメソッド(警察・SAObject・ハイドポイント・ドローンは、そのまま)
    /// ライト: 地上の大通り(長くまっすぐで、幅の広い道)を、ソロ用と同じように、大通りに沿って往復して照らす。エリアごとに _lightsPerArea 本
    /// レーザー: 通路を横切って、通路に沿って往復する棒。地上の通路と、屋上の悪人のそばに置く(悪人・SAObjectは、今ある物を使う)
    /// 結果は Temp/FieldLaserLightReport.txt
    /// </summary>
    [MenuItem("Tools/Police/マルチのレーザーとライトを配置する")]
    private static void PlaceLasersAndLights()
    {
        PlaceDevices(true, true);
    }

    /// <summary>
    /// マルチのフィールドの、ストリートライトだけを、配置し直すメソッド(レーザーを含む他の物は、そのまま。手で置いたライトは消す)
    /// </summary>
    [MenuItem("Tools/Police/マルチのライトだけを配置する")]
    private static void PlaceLightsOnly()
    {
        PlaceDevices(false, true);
    }

    private static void PlaceDevices(bool withLasers, bool withLights)
    {
        _report = new StringBuilder();
        _policeFaction = new List<Vector3>();
        _multi = FindMulti();
        if (_multi == null)
        {
            Debug.LogError("CSED_FieldMultiLayoutGenerator: シーンに Field/Field_Multi が見つかりません(MainSceneを開いてください)");
            return;
        }

        string path = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(_multi.gameObject));
        if (!path.EndsWith("/Field_Multi.prefab"))
        {
            Debug.LogError($"CSED_FieldMultiLayoutGenerator: コピー先が Field_Multi.prefab ではありません({path})。中止しました");
            return;
        }

        // 前に置いたレーザー・ライトのコライダーが、空きの判定に混ざらないよう、調べている間だけ無効にする
        List<GameObject> hidden = new List<GameObject>();
        List<string> groupNames = new List<string>();
        if (withLasers) groupNames.AddRange(new[] { _laserGroup, _handLaserGroup });
        if (withLights) groupNames.AddRange(new[] { _lightGroup, _handLightGroup });
        foreach (string groupName in groupNames)
        {
            Transform group = _multi.Find(groupName);
            if (group == null || !group.gameObject.activeSelf) continue;

            group.gameObject.SetActive(false);
            hidden.Add(group.gameObject);
        }
        Physics.SyncTransforms();

        GameObject contents = null;
        try
        {
            CollectGeometry();
            References refs = CollectReferences();
            contents = PrefabUtility.LoadPrefabContents(path);
            foreach (string groupName in groupNames)
            {
                Transform group = contents.transform.Find(groupName);
                if (group != null) Object.DestroyImmediate(group.gameObject);
            }

            if (withLights) PlaceLights(contents, refs);
            if (withLasers)
            {
                _saBases = CollectSceneSaBases();
                PlaceSweepLasers(contents, refs, CollectSceneSites());
            }
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally
        {
            RestoreLayout(hidden);
            if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
            string reportPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "FieldLaserLightReport.txt");
            File.WriteAllText(reportPath, _report.ToString(), new UTF8Encoding(false));
        }
        Debug.Log("CSED_FieldMultiLayoutGenerator: レーザーとライトを配置しました");
    }

    // 往復して照らすライトの1本分(置く場所・向き・往復する長さ)
    private class LightLine
    {
        public int area;
        public Vector3 origin;
        public float yaw;
        public float distance;
        public float score;
        public float diameter;     // 円の直径(m。道幅に合わせる)
        public float width;        // 線に沿った道幅の最小値(m)
    }

    /// <summary>
    /// ストリートライトを、地上の大通りに置くメソッド(エリアごとに、点数の高い大通りから、離れたものを選ぶ)
    /// </summary>
    private static void PlaceLights(GameObject contents, References refs)
    {
        Transform lights = FindOrCreateGroup(contents.transform, _lightGroup);
        List<LightLine> candidates = FindLightLines();
        List<LightLine> chosen = new List<LightLine>();
        int[] perArea = new int[_areaCenters.Count];
        foreach (float spacing in new[] { _lightMinSpacing, _lightMinSpacing * 0.6f })
        {
            foreach (LightLine line in candidates)
            {
                if (perArea[line.area] >= _lightsPerArea || IsLightLineNear(chosen, line, spacing) || LightRoadCoverage(line) < _lightCoverageMin) continue;

                chosen.Add(line);
                perArea[line.area]++;
                CreateLight(lights, refs, line, chosen.Count);
            }
        }
        _report.AppendLine($"ストリートライト(地上の大通り): {chosen.Count}本(エリアごと: {string.Join(", ", perArea)})");
        foreach (LightLine line in chosen) _report.AppendLine($"  エリア{line.area}: 始点 ({line.origin.x:F0}, {line.origin.y:F1}, {line.origin.z:F0}) 向き {line.yaw:F0}度 往復 {line.distance:F0}m 道幅 {line.width:F0}m 円の直径 {line.diameter:F0}m 点数 {line.score:F0}");
    }

    /// <summary>
    /// 大通りの候補(長くまっすぐで、幅が広く、建物や柱に当たらない線)を、点数の高い順に集めるメソッド
    /// 点数: 道幅の最小値 × 10 + 線の長さ × 0.5 - エリアの中心から線の真ん中までの距離 × 0.2(道幅が一番効く。一番広い大通りから選ばれる)
    /// </summary>
    private static List<LightLine> FindLightLines()
    {
        List<LightLine> lines = new List<LightLine>();
        for (int i = 0; i < _ground.Count; i += 2)
        {
            Vector3 point = _ground[i];
            int area = NearestArea(point);
            for (float yaw = 0.0f; yaw < 180.0f; yaw += 15.0f)
            {
                Vector3 direction = Quaternion.Euler(0.0f, yaw, 0.0f) * Vector3.forward;
                float forwardLength = ClearLength100(point, direction);
                float backLength = ClearLength100(point, -direction);
                if (forwardLength + backLength < _lightMinLine + 2.0f) continue;

                // 線の一方の端を始点にして、そこから大通りに沿って、最大 _lightDistance まで往復する
                Vector3 origin = point - direction * Mathf.Max(0.0f, backLength - 1.0f);
                float distance = Mathf.Min(_lightDistance, forwardLength + backLength - 2.0f);
                if (!NavMesh.SamplePosition(origin, out NavMeshHit hit, 0.6f, NavMesh.AllAreas)) continue;

                // 道のどこかではなく、道の真ん中を往復させる(円が道からはみ出さず、道を照らす)
                if (!TryCenterOnStreet(hit.position, direction, distance, out Vector3 centered, out float width) || width < _lightMinWidth) continue;

                Vector3 middle = centered + direction * (distance * 0.5f);
                float score = width * 10.0f + distance * 0.5f - FlatDistance(middle, _areaCenters[area]) * 0.2f;
                lines.Add(new LightLine { area = area, origin = centered, yaw = yaw, distance = distance, score = score, diameter = Mathf.Clamp(width - 1.0f, 8.0f, 20.0f), width = width });
            }
        }
        lines.Sort((a, b) => b.score.CompareTo(a.score));
        return lines;
    }

    /// <summary>
    /// 位置から向きへ、歩ける場所の中を、壁・柱に当たらずにまっすぐ進める長さを返すメソッド(最大 _lightDistance)
    /// </summary>
    private static float ClearLength100(Vector3 position, Vector3 direction)
    {
        float length = NavMesh.Raycast(position, position + direction * _lightDistance, out NavMeshHit hit, NavMesh.AllAreas) ? hit.distance : _lightDistance;
        for (float distance = 1.0f; distance <= length; distance += 1.0f)
        {
            if (Physics.CheckSphere(position + direction * distance + Vector3.up * 0.9f, 0.3f, ~0, QueryTriggerInteraction.Ignore)) return distance - 1.0f;
        }
        return length;
    }

    /// <summary>
    /// 線を、道の真ん中へ寄せるメソッド(5mおきに、線に直角な向きの歩ける長さを両側で測り、道の中心のずれの中央値だけ線を動かす)
    /// 道の中心が線に沿って大きくずれる(道が曲がっている・幅が変わる)・動かした線が壁や柱に当たる・歩ける場所から外れる場合は false
    /// </summary>
    /// <param name="origin">線の始点(歩ける場所)</param>
    /// <param name="direction">線の向き</param>
    /// <param name="distance">線の長さ</param>
    /// <param name="centered">道の真ん中に寄せた始点</param>
    /// <param name="width">線に沿った道幅の最小値</param>
    private static bool TryCenterOnStreet(Vector3 origin, Vector3 direction, float distance, out Vector3 centered, out float width)
    {
        const float maxCenterDeviation = 1.5f;     // 道の中心が、寄せた線からずれてよい最大(m)
        centered = origin;
        width = 0.0f;
        Vector3 side = Vector3.Cross(Vector3.up, direction);
        List<float> offsets = new List<float>();
        float minWidth = float.MaxValue;
        for (float t = 0.0f; t <= distance; t += 5.0f)
        {
            if (!NavMesh.SamplePosition(origin + direction * t, out NavMeshHit hit, 0.8f, NavMesh.AllAreas)) return false;

            float sideLength = Mathf.Min(WalkLength(hit.position, side), 20.0f);
            float backLength = Mathf.Min(WalkLength(hit.position, -side), 20.0f);
            offsets.Add((sideLength - backLength) * 0.5f);
            minWidth = Mathf.Min(minWidth, sideLength + backLength);
        }

        List<float> sorted = new List<float>(offsets);
        sorted.Sort();
        float median = sorted[sorted.Count / 2];
        foreach (float offset in offsets)
        {
            if (Mathf.Abs(offset - median) > maxCenterDeviation) return false;
        }

        if (!NavMesh.SamplePosition(origin + side * median, out NavMeshHit start, 0.8f, NavMesh.AllAreas)) return false;
        if (ClearLength100(start.position, direction) < distance - 0.5f) return false;

        centered = start.position;
        width = minWidth;
        return true;
    }

    /// <summary>
    /// 選んだ線のどれかと、近すぎる(線の上の点同士が spacing 未満)かを判定するメソッド
    /// </summary>
    private static bool IsLightLineNear(List<LightLine> chosen, LightLine line, float spacing)
    {
        foreach (LightLine other in chosen)
        {
            for (float a = 0.0f; a <= line.distance; a += 10.0f)
            {
                Vector3 pointA = line.origin + Quaternion.Euler(0.0f, line.yaw, 0.0f) * Vector3.forward * a;
                for (float b = 0.0f; b <= other.distance; b += 10.0f)
                {
                    Vector3 pointB = other.origin + Quaternion.Euler(0.0f, other.yaw, 0.0f) * Vector3.forward * b;
                    if (FlatDistance(pointA, pointB) < spacing) return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// ライトの円が、往復する間ずっと、道(歩ける場所)の上にどれだけ乗るか(0〜1)を返すメソッド(10mおきに、円の中を2mおきに調べる)
    /// 建物や壁の上を照らす割合が多いライトを、選ばないために使う
    /// </summary>
    private static float LightRoadCoverage(LightLine line)
    {
        Vector3 direction = Quaternion.Euler(0.0f, line.yaw, 0.0f) * Vector3.forward;
        float radius = line.diameter * 0.5f;
        int total = 0;
        int onRoad = 0;
        for (float t = 0.0f; t <= line.distance; t += 10.0f)
        {
            Vector3 center = line.origin + direction * t;
            for (float dx = -radius; dx <= radius; dx += 2.0f)
            {
                for (float dz = -radius; dz <= radius; dz += 2.0f)
                {
                    if (dx * dx + dz * dz > radius * radius) continue;

                    total++;
                    if (NavMesh.SamplePosition(center + new Vector3(dx, 0.0f, dz), out _, 0.6f, NavMesh.AllAreas)) onRoad++;
                }
            }
        }
        return total > 0 ? (float)onRoad / total : 0.0f;
    }

    private static void CreateLight(Transform container, References refs, LightLine line, int number)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(refs.lightPrefab, container);
        instance.name = $"SecurityLight_A{line.area}_{number:00}";
        CopyScriptValues(refs.lightTemplate.gameObject, instance);
        Quaternion rotation = Quaternion.Euler(0.0f, line.yaw, 0.0f);
        instance.transform.position = ToLocal(line.origin + Vector3.up * _lightHeightOffset);
        instance.transform.rotation = Quaternion.Inverse(_multi.rotation) * rotation;

        SerializedObject serialized = new SerializedObject(instance.GetComponent<CS_SecurityLight>());
        serialized.FindProperty("_moveDirection").vector3Value = Vector3.forward;
        serialized.FindProperty("_moveDistance").floatValue = Mathf.Clamp(line.distance, 0.0f, 100.0f);
        serialized.FindProperty("_diameter").floatValue = line.diameter;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// マルチのフィールドから、悪人の出現点・巡回ドローン・防犯レーザー・ストリートライトを削除するメソッド(警察・SAObject・ハイドポイントは残す)
    /// </summary>
    [MenuItem("Tools/Police/マルチの悪人・ドローン・レーザー・ライトを削除する")]
    private static void ClearDevices()
    {
        _multi = FindMulti();
        if (_multi == null)
        {
            Debug.LogError("CSED_FieldMultiLayoutGenerator: シーンに Field/Field_Multi が見つかりません(MainSceneを開いてください)");
            return;
        }

        string path = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(_multi.gameObject));
        if (!path.EndsWith("/Field_Multi.prefab")) return;

        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform root = contents.transform;
            foreach (string groupName in new[] { _laserGroup, _lightGroup, _droneGroup, _handLaserGroup, _handLightGroup })
            {
                Transform group = root.Find(groupName);
                if (group != null) Object.DestroyImmediate(group.gameObject);
            }

            Transform points = root.Find("Villain/VillainSpawnPoints");
            if (points != null)
            {
                for (int i = points.childCount - 1; i >= 0; i--) Object.DestroyImmediate(points.GetChild(i).gameObject);
            }
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
        Debug.Log("CSED_FieldMultiLayoutGenerator: 悪人・ドローン・レーザー・ライトを削除しました");
    }

    /// <summary>
    /// 建物などに埋まっているSAObjectを、埋まらない近くの場所へずらす(ハイドポイントも一緒に)か、
    /// ずらせない(ずらした先が道の真ん中・他のSAObjectに近すぎる・埋まったまま)なら、ハイドポイントごと削除するメソッド
    /// 埋まりは、コライダーとの重なりと、建物の中に入っているか(裏面への光線)で判定する。結果は Temp/SaObjectFixReport.txt
    /// </summary>
    [MenuItem("Tools/Police/埋まっているSAObjectをずらす・削除する")]
    private static void FixBuriedSaObjects()
    {
        _multi = FindMulti();
        Transform saGroup = _multi != null ? _multi.Find(_saObjectGroup) : null;
        Transform hideGroup = _multi != null ? _multi.Find(_hideGroup) : null;
        string path = _multi != null ? AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(_multi.gameObject)) : "";
        if (saGroup == null || hideGroup == null || !path.EndsWith("/Field_Multi.prefab"))
        {
            Debug.LogError("CSED_FieldMultiLayoutGenerator: Field_Multi のSAObjectsまたはHidePointsが見つかりません(MainSceneを開いてください)");
            return;
        }

        _villainLines = new List<(Vector3 a, Vector3 b)>();
        List<(string name, Vector3 from, Vector3 to)> moves = new List<(string, Vector3, Vector3)>();
        List<(string name, Vector3 from)> deletes = new List<(string, Vector3)>();
        List<Transform> all = new List<Transform>();
        foreach (Transform child in saGroup) all.Add(child);
        List<Vector3> bases = new List<Vector3>();
        foreach (Transform sa in all) bases.Add(sa.position);

        for (int i = 0; i < all.Count; i++)
        {
            Transform sa = all[i];
            if (CSED_FieldPlacementCheck.FindBuriedCollider(sa) == null) continue;

            Transform pole = sa.Find("Cube (1)");
            float poleHeight = pole != null ? pole.lossyScale.y : _roadPoleHeight;
            Vector3 origin = sa.position;
            sa.gameObject.SetActive(false);
            Physics.SyncTransforms();
            bool found = TryFindFreeSpot(origin, poleHeight, bases, i, out Vector3 spot);
            sa.gameObject.SetActive(true);
            if (found)
            {
                moves.Add((sa.name, origin, spot));
                bases[i] = spot;
            }
            else
            {
                deletes.Add((sa.name, origin));
                bases[i] = new Vector3(0.0f, -10000.0f, 0.0f);
            }
        }
        Physics.SyncTransforms();

        ApplySaFix(path, hideGroup, moves, deletes);

        StringBuilder report = new StringBuilder();
        report.AppendLine($"埋まっていたSAObject {moves.Count + deletes.Count}本: ずらした {moves.Count}本 / 削除した {deletes.Count}本");
        foreach ((string name, Vector3 from, Vector3 to) move in moves) report.AppendLine($"  ずらした {move.name}: {move.from} → {move.to}(動いた距離 {Vector3.Distance(move.from, move.to):F1}m)");
        foreach ((string name, Vector3 from) delete in deletes) report.AppendLine($"  削除した {delete.name}: {delete.from}");
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "SaObjectFixReport.txt"), report.ToString(), new UTF8Encoding(false));
        Debug.Log("CSED_FieldMultiLayoutGenerator: 埋まっているSAObjectを修正しました");
    }

    /// <summary>
    /// 埋まっているSAObjectの足元(origin)の近くで、柱・頭が埋まらず、道の端にあり、他のSAObjectから離れた場所を、近い順に探すメソッド
    /// </summary>
    private static bool TryFindFreeSpot(Vector3 origin, float poleHeight, List<Vector3> bases, int selfIndex, out Vector3 spot)
    {
        spot = origin;
        for (float radius = 1.0f; radius <= _fixSearchRadius; radius += 1.0f)
        {
            for (float angle = 0.0f; angle < 360.0f; angle += 30.0f)
            {
                Vector3 target = origin + Quaternion.Euler(0.0f, angle, 0.0f) * Vector3.forward * radius;
                if (!NavMesh.SamplePosition(target, out NavMeshHit hit, 0.6f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - origin.y) > 1.5f) continue;
                if (NavMesh.FindClosestEdge(hit.position, out NavMeshHit edge, NavMesh.AllAreas) && edge.distance > _fixRoadsideMax) continue;
                if (IsTooCloseToOtherSa(hit.position, bases, selfIndex) || !IsPoleFree(hit.position, poleHeight)) continue;

                spot = hit.position;
                return true;
            }
        }
        return false;
    }

    private static bool IsTooCloseToOtherSa(Vector3 position, List<Vector3> bases, int selfIndex)
    {
        for (int i = 0; i < bases.Count; i++)
        {
            if (i != selfIndex && Vector3.Distance(bases[i], position) < _saMinSpacing) return true;
        }
        return false;
    }

    /// <summary>
    /// ずらす・削除する結果を、プレハブ(Field_Multi.prefab)へ反映するメソッド(SAObjectと、同じ足元にあるハイドポイントを、同じ分だけ動かす・消す)
    /// </summary>
    private static void ApplySaFix(string path, Transform sceneHideGroup, List<(string name, Vector3 from, Vector3 to)> moves, List<(string name, Vector3 from)> deletes)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform saGroup = contents.transform.Find(_saObjectGroup);
            Transform hideGroup = contents.transform.Find(_hideGroup);
            foreach ((string name, Vector3 from, Vector3 to) move in moves)
            {
                Vector3 delta = _multi.InverseTransformVector(move.to - move.from);
                Transform sa = saGroup.Find(move.name);
                Transform hide = FindHidePointAt(hideGroup, move.from);
                if (sa != null) sa.localPosition += delta;
                if (hide != null) hide.localPosition += delta;
            }
            foreach ((string name, Vector3 from) delete in deletes)
            {
                Transform sa = saGroup.Find(delete.name);
                Transform hide = FindHidePointAt(hideGroup, delete.from);
                if (hide != null) Object.DestroyImmediate(hide.gameObject);
                if (sa != null) Object.DestroyImmediate(sa.gameObject);
            }
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static Transform FindHidePointAt(Transform hideGroup, Vector3 worldBase)
    {
        Vector3 local = _multi.InverseTransformPoint(worldBase);
        foreach (Transform hide in hideGroup)
        {
            Vector3 difference = hide.localPosition - local;
            if (new Vector2(difference.x, difference.z).magnitude < 0.05f && Mathf.Abs(difference.y) < 0.05f) return hide;
        }
        return null;
    }

    /// <summary>
    /// 前に生成した物(SAObject・防犯レーザー・ドローン・SAポイント)を、一時的に無効にするメソッド(シーン上の見た目だけ。保存はしない)
    /// </summary>
    /// <returns>無効にした物(あとで有効に戻す)</returns>
    private static List<GameObject> DisablePreviousLayout()
    {
        List<GameObject> disabled = new List<GameObject>();
        foreach (string groupName in new[] { _saObjectGroup, _hideGroup, _laserGroup, _droneGroup })
        {
            Transform group = _multi.Find(groupName);
            if (group == null || !group.gameObject.activeSelf) continue;

            group.gameObject.SetActive(false);
            disabled.Add(group.gameObject);
        }
        Physics.SyncTransforms();
        return disabled;
    }

    private static void RestoreLayout(List<GameObject> disabled)
    {
        foreach (GameObject group in disabled)
        {
            if (group != null) group.SetActive(true);
        }
    }

    /// <summary>
    /// すべてを配置するメソッド
    /// </summary>
    private static void LayoutAll(GameObject contents)
    {
        References refs = CollectReferences();
        CleanUp(contents);

        List<Site> sites = PlaceVillains(contents, refs);
        PlaceRoofSa(contents, refs, sites);     // SAObjectの位置を、警察のルートで使うので、警察より先に置く
        PlaceRoadSa(contents, refs);
        PlacePolice(contents, refs, sites);
        PlaceClimbSa(contents, refs);           // 屋上のSAObjectを足して、ビルを登れるようにする(ドローン・レーザーの空き判定に混ざるよう、先に置く)
        PlaceRoofVillains(contents, refs, sites);   // 登れるビルの屋上に、悪人を置く(警察のグループも、そのそばに置く)
        PlaceDrones(contents, refs, sites);
        EnsurePoliceCoverage(contents, refs, sites);
        ReportCoverage(sites);
        ReportRunTime(sites);
    }

    // =====================================================================
    // 準備
    // =====================================================================

    /// <summary>
    /// 配置に使う、元になるプレハブと設定のひな形(シーンにすでにある物)
    /// </summary>
    private class References
    {
        public GameObject villainPointPrefab;
        public GameObject squadPrefab;
        public CS_PoliceSquad squadTemplate;
        public GameObject dronePrefab;
        public CS_SecurityDroneSpawner droneTemplate;
        public GameObject laserPrefab;
        public CS_SecurityLaser laserTemplate;
        public GameObject lightPrefab;
        public CS_SecurityLight lightTemplate;
        public GameObject hidePrefab;
        public Material saHeadMaterial;     // SAObjectの頭のマテリアル
        public Material saPoleMaterial;     // SAObjectの柱のマテリアル
        public int saLayer;
    }

    private static Transform FindMulti()
    {
        foreach (Transform candidate in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate.name == "Field_Multi" && candidate.parent != null && candidate.parent.name == "Field") return candidate;
        }
        return null;
    }

    private static References CollectReferences()
    {
        References refs = new References();
        _report.AppendLine("元のプレハブ: " + string.Join(" / ", new[]
        {
            PrefabPath(FirstOf<CS_VillainSpawnPoint>()), PrefabPath(_multi.GetComponentInChildren<CS_PoliceSquad>(true)),
            PrefabPath(FirstOf<CS_SecurityDroneSpawner>()), PrefabPath(FirstOf<CS_SecurityLaser>()), PrefabPath(FirstOf<CS_HidePoint>()),
        }));
        CS_VillainSpawnPoint point = FirstOf<CS_VillainSpawnPoint>();
        refs.villainPointPrefab = Original(point);
        refs.squadTemplate = _multi.GetComponentInChildren<CS_PoliceSquad>(true);
        refs.squadPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(_squadPrefabPath);   // 既存のグループは、Field_Multiの中にそのまま置いてあるので、元のプレハブから引けない
        refs.droneTemplate = FirstOf<CS_SecurityDroneSpawner>();
        refs.dronePrefab = Original(refs.droneTemplate);
        refs.laserTemplate = FirstOf<CS_SecurityLaser>();
        refs.laserPrefab = Original(refs.laserTemplate);
        refs.lightTemplate = FirstOf<CS_SecurityLight>();
        refs.lightPrefab = Original(refs.lightTemplate);
        refs.hidePrefab = Original(FirstOf<CS_HidePoint>());
        CollectSaObjectLook(refs);
        return refs;
    }

    /// <summary>
    /// SAObjectのマテリアル(頭・柱)を読み込み、ソロ用のSAObject(SAObjects/SAObject/Cube (1))と同じレイヤーを調べるメソッド
    /// </summary>
    private static void CollectSaObjectLook(References refs)
    {
        refs.saHeadMaterial = AssetDatabase.LoadAssetAtPath<Material>(_saHeadMaterialPath);
        refs.saPoleMaterial = AssetDatabase.LoadAssetAtPath<Material>(_saPoleMaterialPath);

        foreach (Transform candidate in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate.name != "Cube (1)" || candidate.parent == null || !candidate.parent.name.StartsWith("SAObject") || candidate.IsChildOf(_multi)) continue;

            refs.saLayer = candidate.gameObject.layer;
            return;
        }
    }

    private static string PrefabPath(Component component)
    {
        GameObject original = Original(component);
        return original == null ? "(なし)" : AssetDatabase.GetAssetPath(original);
    }

    private static T FirstOf<T>() where T : Component
    {
        T inMulti = _multi.GetComponentInChildren<T>(true);
        return inMulti != null ? inMulti : Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    }

    private static GameObject Original(Component component)
    {
        return component == null ? null : PrefabUtility.GetCorrespondingObjectFromOriginalSource(component.gameObject);
    }

    /// <summary>
    /// エリアの中心・開始地点・歩ける地上の点を集めるメソッド
    /// </summary>
    private static void CollectGeometry()
    {
        _areaCenters = new List<Vector3>();
        _startPoints = new List<Vector3>();
        CS_PlayerSpawner spawner = _multi.GetComponentInChildren<CS_PlayerSpawner>(true);
        foreach (Transform root in spawner.areaSpawnRoots)
        {
            Vector3 center = Vector3.zero;
            foreach (Transform child in root)
            {
                center += child.position;
                _startPoints.Add(child.position);
            }
            _areaCenters.Add(root.childCount > 0 ? center / root.childCount : root.position);
        }

        _ground = new List<Vector3>();
        _roofNav = new List<Vector3>();
        _roofNavAll = new List<Vector3>();
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        HashSet<Vector2Int> seen = new HashSet<Vector2Int>();
        HashSet<Vector3Int> seenRoof = new HashSet<Vector3Int>();
        HashSet<Vector3Int> seenHigh = new HashSet<Vector3Int>();
        foreach (Vector3 vertex in triangulation.vertices)
        {
            if (vertex.y >= _roofVillainMinHeight && vertex.y <= _roofVillainMaxHeight && seenHigh.Add(new Vector3Int(Mathf.RoundToInt(vertex.x / 3.0f), Mathf.RoundToInt(vertex.y / 2.0f), Mathf.RoundToInt(vertex.z / 3.0f)))) _roofNavAll.Add(vertex);
            if (vertex.y >= _roofSiteMinHeight && vertex.y <= _roofSiteMaxHeight)
            {
                if (seenRoof.Add(new Vector3Int(Mathf.RoundToInt(vertex.x / 3.0f), Mathf.RoundToInt(vertex.y / 2.0f), Mathf.RoundToInt(vertex.z / 3.0f)))) _roofNav.Add(vertex);
                continue;
            }
            if (vertex.y >= _groundHeight || !seen.Add(new Vector2Int(Mathf.RoundToInt(vertex.x / 3.0f), Mathf.RoundToInt(vertex.z / 3.0f)))) continue;
            _ground.Add(vertex);
        }
        _ground.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.z.CompareTo(b.z));
        _report.AppendLine($"地上の歩ける点 {_ground.Count}個 / 低い屋上の歩ける点 {_roofNav.Count}個 / 高い屋上の歩ける点 {_roofNavAll.Count}個 / エリア {_areaCenters.Count}個");
    }

    /// <summary>
    /// 前に生成した物を消すメソッド
    /// </summary>
    private static void CleanUp(GameObject contents)
    {
        Transform root = contents.transform;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            string childName = root.GetChild(i).name;
            if (childName.StartsWith(_legacyContainerPrefix) || childName == _laserGroup || childName == _droneGroup || childName == _hideGroup || childName == _saObjectGroup)
                Object.DestroyImmediate(root.GetChild(i).gameObject);
        }

        DestroyChildrenStartingWith(root.Find(_policeGroup), _generatedSquadPrefix);
        MoveLightsToGroup(root);

        Transform points = root.Find("Villain/VillainSpawnPoints");
        if (points == null) return;

        for (int i = points.childCount - 1; i >= 0; i--) Object.DestroyImmediate(points.GetChild(i).gameObject);
    }

    private static void DestroyChildrenStartingWith(Transform parent, string prefix)
    {
        if (parent == null) return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            if (parent.GetChild(i).name.StartsWith(prefix)) Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }
    }

    /// <summary>
    /// 以前の版が「SecurityDevices」に入れていたストリートライトを、「SecurityLights」へ移して、「SecurityDevices」を消すメソッド
    /// </summary>
    private static void MoveLightsToGroup(Transform root)
    {
        Transform devices = root.Find("SecurityDevices");
        if (devices == null) return;

        Transform lights = FindOrCreateGroup(root, _lightGroup);
        for (int i = devices.childCount - 1; i >= 0; i--)
        {
            Transform child = devices.GetChild(i);
            if (child.name.StartsWith("SecurityLight")) child.SetParent(lights, true);
        }
        Object.DestroyImmediate(devices.gameObject);
    }

    /// <summary>
    /// 親の下にある、指定した名前の子を返すメソッド(無ければ作る)
    /// </summary>
    private static Transform FindOrCreateGroup(Transform parent, string groupName)
    {
        Transform found = parent.Find(groupName);
        if (found != null) return found;

        GameObject created = new GameObject(groupName);
        created.transform.SetParent(parent, false);
        return created.transform;
    }

    private static Vector3 ToLocal(Vector3 world)
    {
        return _multi.InverseTransformPoint(world);
    }

    private static int NearestArea(Vector3 position)
    {
        int nearest = 0;
        float best = float.MaxValue;
        for (int i = 0; i < _areaCenters.Count; i++)
        {
            Vector3 offset = _areaCenters[i] - position;
            offset.y = 0.0f;
            if (offset.sqrMagnitude >= best) continue;

            best = offset.sqrMagnitude;
            nearest = i;
        }
        return nearest;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0.0f;
        b.y = 0.0f;
        return Vector3.Distance(a, b);
    }

    // =====================================================================
    // 悪人
    // =====================================================================

    private static List<Site> PlaceVillains(GameObject contents, References refs)
    {
        List<Site> sites = new List<Site>();
        Transform parent = contents.transform.Find("Villain/VillainSpawnPoints");
        if (parent == null || refs.villainPointPrefab == null)
        {
            _report.AppendLine("悪人の出現点を置く場所(Villain/VillainSpawnPoints)か、元のプレハブが見つかりません");
            return sites;
        }

        for (int area = 0; area < _areaCenters.Count; area++)
        {
            for (int index = 0; index < _groundSitesPerArea; index++)
            {
                // 地上は、ビルの間の狭い道だけでは足りないエリアは、道の幅・中心からの距離を少しゆるめて、選び直す
                Site site = PickSite(area, index, sites);
                if (site == null)
                {
                    _relaxSites = true;
                    site = PickSite(area, index, sites);
                    _relaxSites = false;
                }
                if (site == null) continue;

                FitPatrolLine(site);
                RegisterVillainLine(site);
                sites.Add(site);
                CreateVillainPoint(parent, refs.villainPointPrefab, site);
            }
        }

        _report.AppendLine($"悪人の出現点 {sites.Count}個(エリアごと: {CountPerArea(sites)}) / うち屋上 {sites.FindAll(site => site.isRoof).Count}個、地上の狭い道 {sites.FindAll(site => !site.isRoof).Count}個");
        return sites;
    }

    private static string CountPerArea(List<Site> sites)
    {
        int[] counts = new int[_areaCenters.Count];
        foreach (Site site in sites) counts[site.area]++;
        return string.Join(", ", counts);
    }

    /// <summary>
    /// 悪人の出現点を1つ選ぶメソッド(エリアの中で、すでに選んだ点から一番離れた点)
    /// 最初の_groundSitesPerArea個は、地上の狭い道(大通りは避ける)。残りは、SAで飛んで行ける、低い屋上の歩ける場所
    /// 屋上の候補が無ければ、地上の狭い道から選ぶ
    /// </summary>
    private static Site PickSite(int area, int index, List<Site> chosen)
    {
        if (index >= _groundSitesPerArea)
        {
            Site roofSite = PickBestSite(area, index, chosen, _roofNav, true);
            if (roofSite != null) return roofSite;
        }
        return PickBestSite(area, index, chosen, _ground, false);
    }

    private static Site PickBestSite(int area, int index, List<Site> chosen, List<Vector3> candidates, bool isRoof)
    {
        Site best = null;
        float bestScore = -1.0f;
        foreach (Vector3 candidate in candidates)
        {
            if (NearestArea(candidate) != area) continue;

            float fromCenter = FlatDistance(candidate, _areaCenters[area]);
            if (fromCenter < _siteMinFromCenter || fromCenter > (_relaxSites ? _siteMaxFromCenter * 1.4f : _siteMaxFromCenter)) continue;
            if (IsNearAny(_startPoints, candidate, _siteMinFromStart)) continue;

            float spacing = MinDistanceToSites(chosen, candidate);
            if (spacing < _siteMinSpacing) continue;

            float score = Mathf.Min(spacing, 250.0f);
            if (score <= bestScore) continue;

            // 通りの向き・幅・(屋上なら)縁からの距離と、飛んで行けるか、は、点数が上がる候補だけ調べる(重い判定を減らす)
            float length = FindStreetYaw(candidate, out float yaw);
            if (length < 10.0f && !isRoof) continue;
            if (isRoof && length < 8.0f) continue;
            if (!IsNarrowStreet(candidate, yaw)) continue;

            Site site = new Site { area = area, index = index, position = candidate, yaw = yaw, isRoof = isRoof };
            if (isRoof && !IsRoofSiteReachable(site)) continue;

            bestScore = score;
            best = site;
        }
        return best;
    }

    /// <summary>
    /// 通りに直角な向きの、歩ける幅(両側の合計)が、_maxVillainStreetWidth以下か(大通りのような広い所でないか)を判定するメソッド
    /// 屋上の場合は、屋上の縁(歩ける場所の縁)から、_roofSiteMinEdge以上内側かも調べる
    /// </summary>
    private static bool IsNarrowStreet(Vector3 position, float yaw)
    {
        Vector3 along = Quaternion.Euler(0.0f, yaw, 0.0f) * Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, along);
        return WalkLength(position, side) + WalkLength(position, -side) <= (_relaxSites ? _maxVillainStreetWidth * 1.4f : _maxVillainStreetWidth);
    }

    private static bool IsNearAny(List<Vector3> points, Vector3 position, float distance)
    {
        foreach (Vector3 point in points)
        {
            if (FlatDistance(point, position) < distance) return true;
        }
        return false;
    }

    private static float MinDistanceToSites(List<Site> sites, Vector3 position)
    {
        float min = float.MaxValue;
        foreach (Site site in sites) min = Mathf.Min(min, FlatDistance(site.position, position));
        return min;
    }

    /// <summary>
    /// 歩ける道が、一番長く続く向き(通りの向き)を調べるメソッド
    /// </summary>
    /// <returns>その向きの、両側の長さの合計(m)</returns>
    private static float FindStreetYaw(Vector3 position, out float yaw)
    {
        float bestLength = 0.0f;
        yaw = 0.0f;
        for (float angle = 0.0f; angle < 180.0f; angle += 15.0f)
        {
            Vector3 direction = Quaternion.Euler(0.0f, angle, 0.0f) * Vector3.forward;
            float length = WalkLength(position, direction) + WalkLength(position, -direction);
            if (length <= bestLength) continue;

            bestLength = length;
            yaw = angle;
        }
        return bestLength;
    }

    /// <summary>
    /// 悪人の往復ルート(出現点から前後に同じ長さ)が、壁・柱・歩けない所に当たらないように、
    /// 出現点の位置を道の真ん中へずらし、通りの向きと、片側の長さ(patrolHalf)を決め直すメソッド
    /// 向きを15度ずつ試して、片側が一番長く取れる向きを選ぶ。ずらしても屋上の足場が作れなくなる時は、位置はずらさない
    /// </summary>
    private static void FitPatrolLine(Site site)
    {
        Vector3 bestCenter = site.position;
        float bestYaw = site.yaw;
        float bestHalf = 0.0f;
        for (float angle = 0.0f; angle < 180.0f; angle += 15.0f)
        {
            Vector3 direction = Quaternion.Euler(0.0f, angle, 0.0f) * Vector3.forward;
            float forwardLength = ClearLength(site.position, direction);
            float backLength = ClearLength(site.position, -direction);
            float shift = (forwardLength - backLength) * 0.5f;
            float half = (forwardLength + backLength) * 0.5f - _patrolEndMargin;
            if (half <= bestHalf) continue;

            Vector3 center = site.position + direction * shift;
            if (!NavMesh.SamplePosition(center, out NavMeshHit hit, 1.0f, NavMesh.AllAreas)) continue;

            bestHalf = half;
            bestYaw = angle;
            bestCenter = hit.position;
        }

        Site shifted = new Site { area = site.area, index = site.index, position = bestCenter, yaw = bestYaw, isRoof = site.isRoof };
        bool keepPosition = site.isRoof && !IsRoofSiteReachable(shifted);
        site.yaw = bestYaw;
        site.position = keepPosition ? site.position : bestCenter;
        site.patrolHalf = keepPosition ? Mathf.Max(1.5f, Mathf.Min(ClearLength(site.position, Quaternion.Euler(0.0f, bestYaw, 0.0f) * Vector3.forward), ClearLength(site.position, Quaternion.Euler(0.0f, bestYaw, 0.0f) * Vector3.back)) - _patrolEndMargin) : Mathf.Max(1.5f, bestHalf);
    }

    /// <summary>
    /// 悪人の往復ルートの線を記録するメソッド(あとで置くSAObjectの柱が、線の上や両端に立たないようにする)
    /// </summary>
    private static void RegisterVillainLine(Site site)
    {
        Vector3 direction = Quaternion.Euler(0.0f, site.yaw, 0.0f) * Vector3.forward;
        float half = Mathf.Min(_trailDistance[site.area % _trailDistance.Length], site.patrolHalf);
        _villainLines.Add((site.position + direction * half, site.position - direction * half));
    }

    private static bool IsOnVillainLine(Vector3 position)
    {
        if (_villainLines == null) return false;

        foreach ((Vector3 a, Vector3 b) line in _villainLines)
        {
            Vector3 d = line.b - line.a;
            d.y = 0.0f;
            Vector3 toPoint = position - line.a;
            toPoint.y = 0.0f;
            float t = d.sqrMagnitude < 0.0001f ? 0.0f : Mathf.Clamp01(Vector3.Dot(toPoint, d) / d.sqrMagnitude);
            if ((toPoint - d * t).magnitude < _villainLineClearance && Mathf.Abs(position.y - line.a.y) < 4.0f) return true;
        }
        return false;
    }

    /// <summary>
    /// 位置から向きへ、歩ける場所の中を、壁・柱(胸の高さのコライダー)に当たらずにまっすぐ進める長さを返すメソッド(最大40m)
    /// </summary>
    private static float ClearLength(Vector3 position, Vector3 direction)
    {
        float length = WalkLength(position, direction);
        for (float distance = 0.5f; distance <= length; distance += 0.5f)
        {
            if (Physics.CheckSphere(position + direction * distance + Vector3.up * 0.9f, 0.3f, ~0, QueryTriggerInteraction.Ignore)) return distance - 0.5f;
        }
        return length;
    }

    private static float WalkLength(Vector3 position, Vector3 direction)
    {
        const float maxLength = 40.0f;
        return NavMesh.Raycast(position, position + direction * maxLength, out NavMeshHit hit, NavMesh.AllAreas) ? hit.distance : maxLength;
    }

    private static void CreateVillainPoint(Transform parent, GameObject prefab, Site site)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.name = $"VillainSpawnPoint_A{site.area}_{site.index + 1:00}";
        instance.transform.position = ToLocal(site.position);
        instance.transform.rotation = Quaternion.Inverse(_multi.rotation) * Quaternion.Euler(0.0f, site.yaw, 0.0f);

        SerializedObject serialized = new SerializedObject(instance.GetComponent<CS_VillainSpawnPoint>());
        serialized.FindProperty("_fallbackPatrolDistance").floatValue = Mathf.Min(_trailDistance[site.area % _trailDistance.Length], site.patrolHalf);
        serialized.FindProperty("_lookAroundTime").floatValue = _trailLookTime[site.area % _trailLookTime.Length];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // =====================================================================
    // 悪人(屋上): SAで登れるビルの屋上
    // =====================================================================

    /// <summary>
    /// SAで登れるビルの屋上に、悪人の出現点を置くメソッド(ビルを登るSAObjectを立てた後に行う)
    /// エリアごとに _roofSitesPerArea 個。登れる屋上のSAObjectから _roofVillainNearSa 以内の、同じ高さの屋上の、歩ける場所から選ぶ
    /// 置いた出現点ごとに、そのそばの地上に警察のグループも置く。出現点が _sitesPerArea に足りないエリアは、ビルの間の地上で補う
    /// </summary>
    private static void PlaceRoofVillains(GameObject contents, References refs, List<Site> sites)
    {
        Transform parent = contents.transform.Find("Villain/VillainSpawnPoints");
        if (parent == null || refs.villainPointPrefab == null) return;

        Transform police = FindOrCreateGroup(contents.transform, _policeGroup);
        int roofCount = 0;
        int groundCount = 0;
        int squadCount = 0;
        List<string> heights = new List<string>();
        for (int area = 0; area < _areaCenters.Count; area++)
        {
            for (int k = 0; k < _roofSitesPerArea; k++)
            {
                Site site = PickRoofVillainSite(area, NextSiteIndex(sites, area), sites);
                if (site == null) break;

                AddVillainSite(parent, refs, sites, site);
                if (TryPlaceSquad(police, refs, GetGroundCenter(site), $"{_generatedSquadPrefix}{site.area}_{site.index + 1:00}")) squadCount++;
                roofCount++;
                heights.Add($"エリア{area}:{site.position.y:F0}m");
            }

            // 屋上に足りない分は、ビルの間の地上(ある程度広い所)で補う
            while (sites.FindAll(other => other.area == area).Count < _sitesPerArea)
            {
                _relaxSites = true;
                Site site = PickBestSite(area, NextSiteIndex(sites, area), sites, _ground, false);
                _relaxSites = false;
                if (site == null) break;

                FitPatrolLine(site);
                AddVillainSite(parent, refs, sites, site);
                if (TryPlaceSquad(police, refs, GetGroundCenter(site), $"{_generatedSquadPrefix}{site.area}_{site.index + 1:00}")) squadCount++;
                groundCount++;
            }
        }
        _report.AppendLine($"悪人の出現点(登れるビルの屋上): {roofCount}個({string.Join(", ", heights)}) / 足りない分をビルの間の地上で補った {groundCount}個 / 警察のグループ {squadCount}個");
    }

    private static void AddVillainSite(Transform parent, References refs, List<Site> sites, Site site)
    {
        RegisterVillainLine(site);
        sites.Add(site);
        CreateVillainPoint(parent, refs.villainPointPrefab, site);
    }

    private static int NextSiteIndex(List<Site> sites, int area)
    {
        int next = 0;
        foreach (Site site in sites)
        {
            if (site.area == area) next = Mathf.Max(next, site.index + 1);
        }
        return next;
    }

    /// <summary>
    /// 登れるビルの屋上から、悪人の出現点を1つ選ぶメソッド(エリアの中で、すでに選んだ点から離れていて、高い屋上を優先)
    /// 屋上の縁の外へ落ちないよう、縁から _roofSiteMinEdge 以上内側へ寄せ、往復ルートの線が、SAObjectの柱に当たらない所を選ぶ
    /// </summary>
    private static Site PickRoofVillainSite(int area, int index, List<Site> chosen)
    {
        Site best = null;
        float bestScore = -1.0f;
        foreach (Vector3 candidate in _roofNavAll)
        {
            if (NearestArea(candidate) != area || IsNearAny(_startPoints, candidate, _siteMinFromStart) || !IsNearClimbNode(candidate)) continue;

            float spacing = MinDistanceToSites(chosen, candidate);
            if (spacing < _siteMinSpacing) continue;

            float score = Mathf.Min(spacing, 250.0f) + Mathf.Min(candidate.y, 60.0f);
            if (score <= bestScore) continue;

            // 重い判定は、点数が上がる候補だけ行う
            if (!TryMoveInsideRoof(candidate, out Vector3 inside)) continue;

            Site site = new Site { area = area, index = index, position = inside, yaw = 0.0f, isRoof = false };
            FitPatrolLine(site);
            site.isRoof = true;
            if (site.patrolHalf < _roofVillainMinPatrol || IsVillainLineOnSaObject(site) || !IsNearClimbNode(site.position)) continue;

            bestScore = score;
            best = site;
        }
        return best;
    }

    /// <summary>
    /// 屋上の歩ける場所の点を、縁(歩ける場所の縁)から _roofSiteMinEdge 以上内側へ寄せるメソッド(寄せられなければ false)
    /// </summary>
    private static bool TryMoveInsideRoof(Vector3 position, out Vector3 inside)
    {
        inside = position;
        for (int i = 0; i < 4; i++)
        {
            if (!NavMesh.FindClosestEdge(inside, out NavMeshHit edge, NavMesh.AllAreas)) return false;
            if (edge.distance >= _roofSiteMinEdge) return true;

            Vector3 inward = FlatOf(inside - edge.position);
            if (inward.sqrMagnitude < 0.01f) inward = FlatOf(edge.normal);
            if (inward.sqrMagnitude < 0.01f) return false;

            Vector3 moved = inside + inward.normalized * (_roofSiteMinEdge + 0.5f - edge.distance);
            if (!NavMesh.SamplePosition(moved, out NavMeshHit hit, 1.0f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - position.y) > 1.5f) return false;

            inside = hit.position;
        }
        return NavMesh.FindClosestEdge(inside, out NavMeshHit last, NavMesh.AllAreas) && last.distance >= _roofSiteMinEdge * 0.8f;
    }

    /// <summary>
    /// 位置が、登れる屋上のSAObject(_climbRoofNodes)の近く(水平距離が _roofVillainNearSa 以内)で、同じ高さの屋上かを判定するメソッド
    /// </summary>
    private static bool IsNearClimbNode(Vector3 position)
    {
        foreach (Vector3 node in _climbRoofNodes)
        {
            if (Mathf.Abs(node.y - position.y) <= 1.5f && FlatDistance(node, position) <= _roofVillainNearSa) return true;
        }
        return false;
    }

    /// <summary>
    /// 悪人の往復ルートの線が、すでに立てたSAObjectの柱に近すぎる(線から _villainLineClearance + 柱の太さ以内)かを判定するメソッド
    /// </summary>
    private static bool IsVillainLineOnSaObject(Site site)
    {
        Vector3 direction = Quaternion.Euler(0.0f, site.yaw, 0.0f) * Vector3.forward;
        float half = Mathf.Min(_trailDistance[site.area % _trailDistance.Length], site.patrolHalf);
        Vector3 a = site.position + direction * half;
        Vector3 b = site.position - direction * half;
        foreach (Vector3 saBase in _saBases)
        {
            if (Mathf.Abs(saBase.y - site.position.y) > 4.0f) continue;

            Vector3 d = FlatOf(b - a);
            Vector3 toBase = FlatOf(saBase - a);
            float t = d.sqrMagnitude < 0.0001f ? 0.0f : Mathf.Clamp01(Vector3.Dot(toBase, d) / d.sqrMagnitude);
            if ((toBase - d * t).magnitude < _villainLineClearance + _saPoleWidth + _groupRadiusMargin) return true;
        }
        return false;
    }

    // =====================================================================
    // 警察(地上だけ)
    // =====================================================================

    private static void PlacePolice(GameObject contents, References refs, List<Site> sites)
    {
        Transform container = FindOrCreateGroup(contents.transform, _policeGroup);
        CollectExistingSquadPoints();
        ConvertExistingSquads(contents, refs);
        int placed = 0;

        // 悪人の出現点ごとに、1グループ(そのまわりを広く一周する)
        foreach (Site site in sites)
        {
            string squadName = $"{_generatedSquadPrefix}{site.area}_{site.index + 1:00}";
            if (TryPlaceSquad(container, refs, GetGroundCenter(site), squadName)) placed++;
            else _report.AppendLine($"  警察のグループを置けませんでした(巡回ルートが作れない): エリア{site.area} 出現点{site.index + 1}");
        }

        // エリアごとに、グループの空いている所(他のグループの巡回ポイントから一番遠い所)を中心にしたグループを足す
        int extra = 0;
        for (int area = 0; area < _areaCenters.Count; area++)
        {
            for (int k = 0; k < _extraSquadsPerArea; k++)
            {
                // 空いている所の候補を、遠い順に試し、一周が_minExtraRouteLength以上のルートが作れた所に置く
                foreach (Vector3 center in GetGapCandidates(area))
                {
                    if (!TryPlaceSquad(container, refs, center, $"{_generatedSquadPrefix}{area}_X{k + 1:00}", _minExtraRouteLength)) continue;

                    extra++;
                    break;
                }
            }
        }
        _report.AppendLine($"警察のグループ {placed + extra}個(悪人の出現点ごと {placed}個 + 空いている所 {extra}個。巡回ルートは、一部が重なってよい)");
    }

    /// <summary>
    /// 警察の巡回ルートの中心を取得するメソッド(地上の出現点はその位置、屋上の出現点は、そのそばの地上の歩ける点。警察は地上だけ歩くため)
    /// </summary>
    private static Vector3 GetGroundCenter(Site site)
    {
        return site.isRoof && TryFindNearestGround(site.position, out Vector3 ground) ? ground : site.position;
    }

    /// <summary>
    /// 中心のまわりを広く一周する巡回ルートを作り、警察のグループとして置くメソッド
    /// minLengthより短いルートしか作れない時は、置かない(minLengthが0なら、4ポイント以上の短いルートでも置く)
    /// </summary>
    private static bool TryPlaceSquad(Transform container, References refs, Vector3 center, string squadName, float minLength = 0.0f)
    {
        // 悪人のすぐそば(_routeNearRadius)から始まるルートが作れない場所では、少しずつ離した位置から始める
        List<Vector3> route = new List<Vector3>();
        foreach (float nearRadius in new[] { _routeNearRadius, 25.0f, 45.0f })
        {
            _nearRadius = nearRadius;
            route = BuildWideRoute(center, minLength);
            if (route.Count >= 4) break;
        }
        _nearRadius = _routeNearRadius;
        if (route.Count < 4) return false;

        CreateSquad(container, refs, route, squadName);
        _report.AppendLine($"  {squadName}: ポイント {route.Count}個 / 一周 {RouteLength(route):F0}m");
        return true;
    }

    /// <summary>
    /// できるだけ広い巡回ルートを作るメソッド(広い半径 → 広い半径で遠回りを許す → 中くらいの半径で遠回りを許す、の順に試す)
    /// 一周の長さがminLength以上になった時点で、そのルートを使う。どれも足りなければ、一番長かったルートを返す
    /// </summary>
    private static List<Vector3> BuildWideRoute(Vector3 center, float minLength)
    {
        List<Vector3> best = new List<Vector3>();
        float bestLength = 0.0f;
        (float[] radii, float ratio, int minPoints)[] stages =
        {
            (new[] { 100.0f, 80.0f, 120.0f, 60.0f }, _detourRatio, 5),
            (new[] { 100.0f, 80.0f, 120.0f }, _relaxedDetourRatio, 5),
            (new[] { 70.0f, 55.0f, 90.0f }, _relaxedDetourRatio, 5),
            (new[] { 50.0f, 35.0f, 70.0f }, _relaxedDetourRatio, 4),
        };

        foreach ((float[] radii, float ratio, int minPoints) stage in stages)
        {
            List<Vector3> route = FindBestRoute(center, stage.radii, stage.ratio, stage.minPoints);
            float length = route.Count >= stage.minPoints ? RouteLength(route) : 0.0f;
            if (length > bestLength)
            {
                best = route;
                bestLength = length;
            }
            if (bestLength >= minLength) break;
        }

        if (bestLength < minLength) return new List<Vector3>();

        _squadPoints.AddRange(best);
        return best;
    }

    /// <summary>
    /// エリアの中で、置いたグループの巡回ポイントから遠い順に、地上の歩ける点を並べるメソッド(開始地点のそばは避け、近い候補は省く)
    /// </summary>
    private static List<Vector3> GetGapCandidates(int area)
    {
        List<(float distance, Vector3 position)> scored = new List<(float, Vector3)>();
        foreach (Vector3 candidate in _ground)
        {
            if (NearestArea(candidate) != area || IsNearAny(_startPoints, candidate, _siteMinFromStart)) continue;

            float nearest = float.MaxValue;
            foreach (Vector3 point in _squadPoints) nearest = Mathf.Min(nearest, FlatDistance(candidate, point));
            scored.Add((nearest, candidate));
        }
        scored.Sort((a, b) => b.distance.CompareTo(a.distance));

        List<Vector3> result = new List<Vector3>();
        foreach ((float distance, Vector3 position) entry in scored)
        {
            if (!IsNearAny(result, entry.position, 30.0f)) result.Add(entry.position);
            if (result.Count >= _gapCandidateTries) break;
        }
        return result;
    }

    /// <summary>
    /// もとからあるグループ(このツールが置いた物以外)の、巡回ポイントの位置を集めるメソッド
    /// </summary>
    private static void CollectExistingSquadPoints()
    {
        foreach (CS_PoliceSquad squad in _multi.GetComponentsInChildren<CS_PoliceSquad>(true))
        {
            if (squad.name.StartsWith(_generatedSquadPrefix)) continue;

            foreach (Transform point in squad.GetRouteDrawPoints()) _squadPoints.Add(point.position);
        }
    }

    /// <summary>
    /// 半径と向きを変えて巡回ルートを作り、他のグループと重なりが少なく、長いものを選ぶメソッド
    /// </summary>
    private static List<Vector3> BuildBestRoute(Vector3 center, float[] radii, float detourRatio = _detourRatio, int minPoints = 5)
    {
        List<Vector3> best = FindBestRoute(center, radii, detourRatio, minPoints);
        _squadPoints.AddRange(best);
        return best;
    }

    /// <summary>
    /// 半径と向きを変えて巡回ルートを作り、他のグループと重なりが少なく、長いものを選ぶメソッド(選んだだけで、重なりの記録には入れない)
    /// </summary>
    private static List<Vector3> FindBestRoute(Vector3 center, float[] radii, float detourRatio, int minPoints)
    {
        List<Vector3> best = new List<Vector3>();
        int bestOverlap = int.MaxValue;
        float bestLength = 0.0f;
        foreach (float radius in radii)
        {
            for (float offset = 0.0f; offset < 72.0f; offset += 18.0f)
            {
                List<Vector3> route = BuildRoute(center, radius, offset, detourRatio);
                if (route.Count < minPoints) continue;

                int overlap = CountOverlap(route);
                float length = RouteLength(route);
                if (overlap > bestOverlap || (overlap == bestOverlap && length <= bestLength)) continue;

                best = route;
                bestOverlap = overlap;
                bestLength = length;
            }
        }

        return best;
    }

    private static int CountOverlap(List<Vector3> route)
    {
        int count = 0;
        foreach (Vector3 point in route)
        {
            if (IsNearAny(_squadPoints, point, _routeOverlapDistance)) count++;
        }
        return count;
    }

    private static float RouteLength(List<Vector3> route)
    {
        float length = 0.0f;
        for (int i = 0; i < route.Count; i++) length += Vector3.Distance(route[i], route[(i + 1) % route.Count]);
        return length;
    }

    /// <summary>
    /// 悪人の出現点のまわりを、広く一周する、地上の巡回ルートを作るメソッド
    /// 最初のポイントは悪人の近く(_routeNearRadius)、それ以降は、半径を交互に変えながら、まわりを一周する
    /// 次のポイントへは、歩いて行ける(遠回りしすぎない)ものだけをつなぎ、最後のポイントから最初へも歩いて戻れるようにする
    /// </summary>
    private static List<Vector3> BuildRoute(Vector3 center, float radius, float angleOffset, float detourRatio)
    {
        List<Vector3> route = new List<Vector3>();
        for (int i = 0; i < _routePoints; i++)
        {
            float pointRadius = i == 0 ? _nearRadius : radius * (i % 2 == 1 ? 1.0f : 0.65f);
            Vector3 direction = Quaternion.Euler(0.0f, angleOffset + i * (360.0f / _routePoints), 0.0f) * Vector3.forward;
            if (!NavMesh.SamplePosition(center + direction * pointRadius, out NavMeshHit hit, 8.0f, NavMesh.AllAreas) || hit.position.y >= _groundHeight) continue;

            // 最初のポイント(悪人のすぐそば)以外は、近くに地上のSAObjectがあれば、その柱の横に寄せる
            Vector3 point = i == 0 ? hit.position : SnapToSaObject(hit.position);
            if (route.Count > 0 && !IsReasonableWalk(route[route.Count - 1], point, detourRatio)) continue;

            route.Add(point);
        }

        // 最後のポイントから最初へ歩いて戻れない時は、戻れるところまで、最後のポイントを外す
        while (route.Count > 1 && !IsReasonableWalk(route[route.Count - 1], route[0], detourRatio)) route.RemoveAt(route.Count - 1);
        return route;
    }

    /// <summary>
    /// 近く(_routeSnapDistance以内)に地上のSAObjectがあれば、その根元の位置を返すメソッド(無ければ、そのままの位置)
    /// </summary>
    private static Vector3 SnapToSaObject(Vector3 position)
    {
        Vector3 nearestBase = Vector3.zero;
        float nearestDistance = _routeSnapDistance;
        bool found = false;
        foreach (Vector3 saBase in _saBases)
        {
            if (saBase.y >= _groundHeight) continue;

            float distance = FlatDistance(saBase, position);
            if (distance >= nearestDistance) continue;

            nearestDistance = distance;
            nearestBase = saBase;
            found = true;
        }
        if (!found) return position;

        // 柱そのものの位置ではなく、柱の横(道の内側。元の位置の側)へ寄せる(柱にぶつからない位置)
        Vector3 side = FlatOf(position - nearestBase);
        side = side.sqrMagnitude < 0.01f ? Vector3.right : side.normalized;
        Vector3 beside = nearestBase + side * _routeBesidePole;
        return NavMesh.SamplePosition(beside, out NavMeshHit hit, 1.0f, NavMesh.AllAreas) && hit.position.y < _groundHeight ? hit.position : position;
    }

    /// <summary>
    /// 2点の間を、歩いて行けて、遠回りしすぎないか(道のりが、まっすぐの距離の detourRatio 倍+10m以内か)を判定するメソッド
    /// </summary>
    private static bool IsReasonableWalk(Vector3 from, Vector3 to, float detourRatio)
    {
        NavMeshPath path = new NavMeshPath();
        if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return false;

        float length = 0.0f;
        for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        return length <= Vector3.Distance(from, to) * detourRatio + 10.0f;
    }

    private static void CreateSquad(Transform container, References refs, List<Vector3> ring, string squadName)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(refs.squadPrefab, container);
        instance.name = squadName;
        CopyScriptValues(refs.squadTemplate.gameObject, instance);
        ApplySpreadPatrol(instance.GetComponent<CS_PoliceSquad>(), refs.squadTemplate, RouteLength(ring));
        ring = FollowStreets(ring);
        instance.transform.position = ToLocal(ring[0]);
        _policeFaction.AddRange(ring);
        for (int i = 0; i < ring.Count; i++)
        {
            GameObject point = new GameObject($"RoadPatrolPoint_{i:000}");
            point.transform.SetParent(instance.transform, false);
            point.transform.position = ToLocal(ring[i]);
        }
    }

    /// <summary>
    /// 道のりの曲がり角が、地上のSAObjectの柱にめり込む近さ(_routeBesidePole未満)なら、柱の横へずらすメソッド
    /// </summary>
    private static Vector3 PushAwayFromPole(Vector3 position, Vector3 previous)
    {
        foreach (Vector3 saBase in _saBases)
        {
            if (saBase.y >= _groundHeight || FlatDistance(saBase, position) >= _routeBesidePole || Mathf.Abs(saBase.y - position.y) > 3.0f) continue;

            Vector3 side = FlatOf(position - saBase);
            side = side.sqrMagnitude < 0.01f ? Vector3.right : side.normalized;

            // 前のポイントからまっすぐ歩ける(建物を貫通しない)側へずらす。どちらも歩けなければ、そのまま
            foreach (float sign in new[] { 1.0f, -1.0f })
            {
                Vector3 beside = saBase + side * (sign * (_routeBesidePole + 0.3f));
                if (NavMesh.SamplePosition(beside, out NavMeshHit hit, 1.0f, NavMesh.AllAreas) && !NavMesh.Raycast(previous, hit.position, out _, NavMesh.AllAreas)) return hit.position;
            }
            return position;
        }
        return position;
    }

    /// <summary>
    /// 隣り合う巡回ポイントの間(最後から最初への間も含む)をまっすぐ結んで、建物などで歩けない所には、
    /// 歩ける道のりの曲がり角を、中継ポイントとして足すメソッド(ルートの線が、建物を貫通しないようにする)
    /// </summary>
    private static List<Vector3> FollowStreets(List<Vector3> route)
    {
        List<Vector3> result = new List<Vector3>();
        NavMeshPath path = new NavMeshPath();
        for (int i = 0; i < route.Count; i++)
        {
            Vector3 from = route[i];
            Vector3 to = route[(i + 1) % route.Count];
            result.Add(from);

            // まっすぐ歩ける間は、そのまま。歩けない所だけ、道のりの曲がり角を足す
            if (!NavMesh.Raycast(from, to, out _, NavMesh.AllAreas)) continue;
            if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;

            Vector3[] corners = path.corners;
            for (int k = 1; k < corners.Length - 1; k++)
            {
                Vector3 corner = PushAwayFromPole(corners[k], result[result.Count - 1]);
                if (FlatDistance(result[result.Count - 1], corner) >= _minWaypointSpacing) result.Add(corner);
            }
        }
        return result;
    }

    /// <summary>
    /// グループを、離れて巡回する2〜3人にするメソッド(ルートが長いほど人数を多くする。ステータスは、ひな形のグループの先頭と同じ)
    /// </summary>
    private static void ApplySpreadPatrol(CS_PoliceSquad squad, CS_PoliceSquad template, float routeLength)
    {
        SerializedProperty templateStatuses = new SerializedObject(template).FindProperty("_memberStatuses");
        if (templateStatuses == null || templateStatuses.arraySize == 0) return;

        Object status = templateStatuses.GetArrayElementAtIndex(0).objectReferenceValue;
        int members = routeLength >= _longRouteLength ? _longRouteMembers : _shortRouteMembers;

        SerializedObject serialized = new SerializedObject(squad);
        SerializedProperty statuses = serialized.FindProperty("_memberStatuses");
        statuses.arraySize = members;
        for (int i = 0; i < members; i++) statuses.GetArrayElementAtIndex(i).objectReferenceValue = status;
        serialized.FindProperty("_spreadPatrol").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// もとからあるグループ(このツールが置いた物以外)も、離れて巡回する2〜3人にするメソッド
    /// </summary>
    private static void ConvertExistingSquads(GameObject contents, References refs)
    {
        foreach (CS_PoliceSquad squad in contents.GetComponentsInChildren<CS_PoliceSquad>(true))
        {
            if (squad.name.StartsWith(_generatedSquadPrefix)) continue;

            List<Vector3> route = new List<Vector3>();
            foreach (Transform point in squad.GetRouteDrawPoints()) route.Add(point.position);
            ApplySpreadPatrol(squad, refs.squadTemplate, route.Count > 1 ? RouteLength(route) : 0.0f);
        }
    }

    // =====================================================================
    // ドローンとレーザー(高所)
    // =====================================================================

    private static void PlaceDrones(GameObject contents, References refs, List<Site> sites)
    {
        Transform drones = FindOrCreateGroup(contents.transform, _droneGroup);
        int droneCount = 0;
        foreach (Site site in sites)
        {
            if (site.index % _droneStride != 1) continue;

            List<Vector3> roofs = FindRoofs(site, _highMinAbove, _highMaxAbove, _perchMinDistance, _perchMaxDistance);
            if (roofs.Count > 0 && TryCreateDrone(drones, refs, roofs, $"SecurityDroneSpawner_A{site.area}_{site.index + 1:00}")) droneCount++;
        }
        _report.AppendLine($"巡回ドローン(屋上の上空): {droneCount}機");
    }

    /// <summary>
    /// 悪人の出現点の近く(_policeCoverDistance以内)に警察陣営がいない所へ、ドローンか警察のグループを足すメソッド
    /// </summary>
    private static void EnsurePoliceCoverage(GameObject contents, References refs, List<Site> sites)
    {
        Transform drones = FindOrCreateGroup(contents.transform, _droneGroup);
        Transform police = FindOrCreateGroup(contents.transform, _policeGroup);
        int added = 0;
        foreach (Site site in sites)
        {
            if (MinDistanceToPoliceFaction(site.position) <= _policeCoverDistance) continue;

            List<Vector3> roofs = FindRoofs(site, 6.0f, 80.0f, 12.0f, 70.0f);
            if (roofs.Count > 0 && TryCreateDrone(drones, refs, roofs, $"SecurityDroneSpawner_A{site.area}_{site.index + 1:00}_cover"))
            {
                added++;
                continue;
            }

            List<Vector3> ring = BuildBestRoute(GetGroundCenter(site), new[] { 60.0f, 40.0f });
            if (ring.Count < 5) continue;

            CreateSquad(police, refs, ring, $"{_generatedSquadPrefix}{site.area}_{site.index + 1:00}_cover");
            added++;
        }
        _report.AppendLine($"悪人の近くに警察陣営が足りなかった所へ足した数: {added}");
    }

    private static float MinDistanceToPoliceFaction(Vector3 position)
    {
        float min = float.MaxValue;
        foreach (Vector3 point in _policeFaction) min = Mathf.Min(min, FlatDistance(position, point));
        return min;
    }

    private static bool TryCreateDrone(Transform container, References refs, List<Vector3> roofs, string droneName)
    {
        Vector3 roof = roofs[roofs.Count / 2];
        foreach (float radius in new[] { 14.0f, 10.0f, 18.0f })
        {
            List<Vector3> ring = new List<Vector3>();
            for (int i = 0; i < 6; i++)
            {
                Vector3 point = roof + Quaternion.Euler(0.0f, i * 60.0f, 0.0f) * Vector3.forward * radius + Vector3.up * _droneAboveRoof;
                if (!Physics.CheckSphere(point, 1.0f, ~0, QueryTriggerInteraction.Ignore)) ring.Add(point);
            }
            if (ring.Count < 4) continue;

            CreateDrone(container, refs, ring, droneName);
            return true;
        }
        return false;
    }

    private static void CreateDrone(Transform container, References refs, List<Vector3> ring, string droneName)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(refs.dronePrefab, container);
        instance.name = droneName;
        CopyScriptValues(refs.droneTemplate.gameObject, instance);
        instance.transform.position = ToLocal(ring[0]);
        _policeFaction.AddRange(ring);
        for (int i = 0; i < ring.Count; i++)
        {
            Transform point = i < instance.transform.childCount ? instance.transform.GetChild(i) : NewChild(instance.transform, $"PatrolPoint_{i}");
            point.position = ToLocal(ring[i]);
        }
    }

    private static Transform NewChild(Transform parent, string childName)
    {
        GameObject created = new GameObject(childName);
        created.transform.SetParent(parent, false);
        return created.transform;
    }

    // =====================================================================
    // SAポイント: 屋上
    // =====================================================================

    private static void PlaceRoofSa(GameObject contents, References refs, List<Site> sites)
    {
        Transform hideGroup = FindOrCreateGroup(contents.transform, _hideGroup);
        Transform saGroup = FindOrCreateGroup(contents.transform, _saObjectGroup);
        int perchCount = 0;
        int chainCount = 0;
        foreach (Site site in sites)
        {
            // 屋上の出現点は、同じ屋上に足場(SAObject)を立てて、SAで飛んで行けるようにする
            if (site.isRoof)
            {
                if (TryFindRoofSitePerch(site, out Vector3 sitePerch, out float groundY))
                {
                    Vector3 siteTop = PlaceSaObject(saGroup, refs, sitePerch, _roofPoleHeight);
                    PlaceHidePoint(hideGroup, refs.hidePrefab, sitePerch, siteTop, groundY - 0.5f, true);
                    perchCount++;
                }
                continue;
            }

            List<Vector3> roofs = FindRoofs(site, _perchMinAbove, _perchMaxAbove, _perchMinDistance, _perchMaxDistance);
            if (roofs.Count == 0) continue;

            Vector3? low = PickPerch(roofs, site, 8.0f, null);
            Vector3? high = PickPerch(roofs, site, 13.0f, low);
            foreach (Vector3? perch in new[] { low, high })
            {
                if (!perch.HasValue || !TryPlacePerch(hideGroup, saGroup, refs, site, perch.Value, perchCount, out Vector3 top)) continue;

                perchCount++;
                if (perch == low && TryPlaceChain(hideGroup, saGroup, refs, site, perch.Value, top, chainCount)) chainCount++;
            }
        }
        _report.AppendLine($"SAポイント(屋上、SAObjectの上): 悪人の近くの屋上へ {perchCount}個 / さらに高い屋上へ {chainCount}個");
    }

    /// <summary>
    /// 屋上の悪人の出現点に、SAで飛んで行ける足場を立てられる場所があるかを判定するメソッド(立てるのは、PlaceRoofSaで行う)
    /// </summary>
    private static bool IsRoofSiteReachable(Site site)
    {
        return TryFindRoofSitePerch(site, out _, out _);
    }

    /// <summary>
    /// 屋上の悪人の出現点と同じ屋上(高さがほぼ同じ)で、悪人から少し離れた、屋上の内側の点を探すメソッド
    /// 屋上のSAObjectの頭の上へ、地上のどこから飛んでも、飛べる距離に収まる(足元の範囲の箱が、地上の歩ける場所に届く)ことが条件
    /// </summary>
    /// <param name="perch">足場(SAObject)を立てる屋上の点</param>
    /// <param name="groundY">足元の地上の高さ(屋上のそばの、地上の歩ける点の高さ)</param>
    private static bool TryFindRoofSitePerch(Site site, out Vector3 perch, out float groundY)
    {
        perch = Vector3.zero;
        groundY = 0.0f;
        const float radius = 28.0f;
        for (float dx = -radius; dx <= radius; dx += _roofGrid)
        {
            for (float dz = -radius; dz <= radius; dz += _roofGrid)
            {
                float distance = Mathf.Sqrt(dx * dx + dz * dz);
                if (distance < 6.0f || distance > radius) continue;

                if (!TryGetRoof(site.position.x + dx, site.position.z + dz, out Vector3 roof) || Mathf.Abs(roof.y - site.position.y) > 1.5f || !IsRoofInterior(roof)) continue;
                if (!IsPoleFree(roof, _roofPoleHeight) || !IsFarFromOtherSa(roof)) continue;

                // 屋上の近くの地上(水平に一番近い、地上の歩ける点)
                if (!TryFindNearestGround(roof, out Vector3 ground)) continue;

                float topY = roof.y + _roofPoleHeight + _saHeadSize;
                float feetY = ground.y - 0.5f;
                if (topY - feetY > _maxFlightDistance) continue;
                if (FlatDistance(ground, roof) > ReachHalf(topY - feetY) * 0.9f) continue;

                perch = roof;
                groundY = ground.y;
                return true;
            }
        }
        return false;
    }

    private static bool TryFindNearestGround(Vector3 position, out Vector3 ground)
    {
        ground = Vector3.zero;
        float nearest = float.MaxValue;
        foreach (Vector3 candidate in _ground)
        {
            float distance = FlatDistance(candidate, position);
            if (distance >= nearest) continue;

            nearest = distance;
            ground = candidate;
        }
        return nearest < float.MaxValue;
    }

    /// <summary>
    /// 屋上の候補から、狙いの高さに近い・別の方向の屋上を選ぶメソッド
    /// </summary>
    private static Vector3? PickPerch(List<Vector3> roofs, Site site, float targetAbove, Vector3? other)
    {
        Vector3? best = null;
        float bestScore = float.MaxValue;
        foreach (Vector3 roof in roofs)
        {
            if (other.HasValue && Vector3.Angle(FlatOf(roof - site.position), FlatOf(other.Value - site.position)) < 90.0f) continue;

            float score = Mathf.Abs(roof.y - site.position.y - targetAbove);
            if (score >= bestScore) continue;

            bestScore = score;
            best = roof;
        }
        return best;
    }

    private static Vector3 FlatOf(Vector3 vector)
    {
        vector.y = 0.0f;
        return vector;
    }

    /// <summary>
    /// 悪人の近くの屋上へ飛ぶSAポイントを置くメソッド
    /// 屋上にSAObjectを立て、その頭の上へ飛ぶ。足元の範囲の箱は、屋上のSAObjectのまわり(地上から頭の上まで)で、箱の中のどこからでも飛んで着ける大きさにする
    /// </summary>
    /// <param name="top">立てたSAObjectの頭の上の位置(飛んで着く位置)</param>
    private static bool TryPlacePerch(Transform hideGroup, Transform saGroup, References refs, Site site, Vector3 roof, int number, out Vector3 top)
    {
        top = roof;
        float topY = roof.y + _roofPoleHeight + _saHeadSize;
        float feetY = site.position.y - 0.5f;
        if (topY - feetY > _maxFlightDistance || !IsPoleFree(roof, _roofPoleHeight)) return false;
        if (!IsFarFromOtherSa(roof))
        {
            _skippedTooClose++;
            return false;
        }


        // 箱の水平方向の広さの中に、地上の歩ける場所が無ければ、飛び立てないので置かない
        float half = ReachHalf(topY - feetY);
        if (!NavMesh.SamplePosition(new Vector3(roof.x, site.position.y, roof.z), out NavMeshHit hit, half, NavMesh.AllAreas) || hit.position.y >= _groundHeight) return false;

        top = PlaceSaObject(saGroup, refs, roof, _roofPoleHeight);
        PlaceHidePoint(hideGroup, refs.hidePrefab, roof, top, feetY, true);
        return true;
    }

    /// <summary>
    /// 屋上のSAObjectの上から、少し高い屋上のSAObjectの上へ飛ぶSAポイントを置くメソッド
    /// </summary>
    private static bool TryPlaceChain(Transform hideGroup, Transform saGroup, References refs, Site site, Vector3 roof, Vector3 from, int number)
    {
        Site origin = new Site { area = site.area, position = roof };
        List<Vector3> higher = FindRoofs(origin, _chainMinGain, _chainMaxGain, 8.0f, 22.0f);
        higher.RemoveAll(candidate => !IsPoleFree(candidate, _roofPoleHeight) || !IsFarFromOtherSa(candidate));
        if (higher.Count == 0) return false;

        Vector3 target = higher[0];
        foreach (Vector3 candidate in higher)
        {
            if (candidate.y > target.y) target = candidate;
        }

        float feetY = from.y - 0.5f;
        if (target.y + _roofPoleHeight + _saHeadSize - feetY > _maxFlightDistance) return false;

        Vector3 top = PlaceSaObject(saGroup, refs, target, _roofPoleHeight);
        PlaceHidePoint(hideGroup, refs.hidePrefab, target, top, feetY, true);
        return true;
    }

    /// <summary>
    /// 足元の高さから頭の上までの高さの差に応じた、足元の範囲の箱の水平方向の半分の大きさを求めるメソッド
    /// 箱の中のどこからでも、_maxFlightDistance以内で着けるようにする(高い所へ飛ぶほど、水平方向は狭くなる)
    /// </summary>
    private static float ReachHalf(float heightDifference)
    {
        return Mathf.Clamp(Mathf.Sqrt(Mathf.Max(0.0f, _maxFlightDistance * _maxFlightDistance - heightDifference * heightDifference)), 4.0f, _maxReachHalf);
    }

    /// <summary>
    /// すでに立てたSAObjectから、_saMinSpacing以上離れているかを判定するメソッド
    /// </summary>
    private static bool IsFarFromOtherSa(Vector3 basePosition)
    {
        foreach (Vector3 other in _saBases)
        {
            if (Vector3.Distance(other, basePosition) < _saMinSpacing) return false;
        }
        return true;
    }

    /// <summary>
    /// 道の端(歩ける場所の縁)に寄せた、柱を立てられる位置を探すメソッド
    /// 道の端が、_roadsideMaxShiftより遠い(道のど真ん中)場合や、端の近くに立てる空きが無い場合、すでに立てた物に近すぎる場合は、置かない
    /// </summary>
    private static bool TryFindRoadsideBase(Vector3 node, float poleHeight, out Vector3 poleBase)
    {
        poleBase = node;
        if (!NavMesh.FindClosestEdge(node, out NavMeshHit edge, NavMesh.AllAreas) || edge.distance > _roadsideMaxShift)
        {
            _skippedMidRoad++;
            return false;
        }

        // 端から、道の内側(元の地点の方)へ、少しずつ離して、壁にめり込まない位置を探す
        Vector3 inward = FlatOf(node - edge.position);
        if (inward.sqrMagnitude < 0.01f) inward = FlatOf(edge.normal);
        inward.Normalize();
        foreach (float distance in new[] { 0.8f, 1.4f, 2.0f, 2.6f, 3.4f })
        {
            Vector3 candidate = edge.position + inward * distance;
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 0.6f, NavMesh.AllAreas) || hit.position.y >= _groundHeight) continue;
            if (!IsPoleFree(hit.position, poleHeight)) continue;

            if (!IsFarFromOtherSa(hit.position))
            {
                _skippedTooClose++;
                return false;
            }

            poleBase = hit.position;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 柱を立てられる地上の位置を探すメソッド(その地点にそのまま立てられなければ、近くの歩ける場所へずらして探す)
    /// </summary>
    private static bool TryFindPoleBase(Vector3 position, float poleHeight, out Vector3 poleBase)
    {
        poleBase = position;
        if (IsPoleFree(position, poleHeight)) return true;

        foreach (float distance in new[] { 1.5f, 3.0f, 4.5f })
        {
            for (int i = 0; i < 8; i++)
            {
                Vector3 candidate = position + Quaternion.Euler(0.0f, i * 45.0f, 0.0f) * Vector3.forward * distance;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.0f, NavMesh.AllAreas) || hit.position.y >= _groundHeight || !IsPoleFree(hit.position, poleHeight)) continue;

                poleBase = hit.position;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 柱を立てる場所(地面や屋上)の上に、SAObject(柱・頭の箱)と、頭の上に立つ時の隠れる箱が入る空きがあるかを判定するメソッド
    /// 壁・建物・看板などに、少しでも触れる(_poleMargin以内)なら、空きが無いとみなす(壁に埋まる物を置かないため)
    /// 地面そのものに当たらないよう、柱の底は、足元より少し上から調べる
    /// </summary>
    private static bool IsPoleFree(Vector3 basePosition, float poleHeight)
    {
        if (IsOnVillainLine(basePosition) || CSED_FieldPlacementCheck.IsPoleInsideSolid(basePosition, poleHeight, null)) return false;

        float half = _saHeadSize * 0.5f + _poleMargin;
        Vector3 poleCenter = basePosition + Vector3.up * (_poleBottomLift + (poleHeight - _poleBottomLift) * 0.5f);
        Vector3 poleHalf = new Vector3(_saPoleWidth * 0.5f + _poleMargin, (poleHeight - _poleBottomLift) * 0.5f, _saPoleWidth * 0.5f + _poleMargin);
        Vector3 headCenter = basePosition + Vector3.up * (poleHeight + _saHeadSize * 0.5f);
        Vector3 hideCenter = basePosition + Vector3.up * (poleHeight + _saHeadSize + 0.05f + 1.65f);
        Vector3 hideHalf = new Vector3(0.75f + _poleMargin, 1.65f + _poleMargin, 0.75f + _poleMargin);

        return !Physics.CheckBox(poleCenter, poleHalf, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
            && !Physics.CheckBox(headCenter, new Vector3(half, half, half), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
            && !Physics.CheckBox(hideCenter, hideHalf, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
    }

    /// <summary>
    /// 信号・街灯に見立てた仮オブジェクト(SAObject: 柱 + 頭の箱)を立てるメソッド(ソロ用のSAObjectと同じ作り)
    /// </summary>
    /// <returns>頭の箱の上面の位置(飛んで着く位置)</returns>
    private static Vector3 PlaceSaObject(Transform parent, References refs, Vector3 basePosition, float poleHeight)
    {
        _saBases.Add(basePosition);
        GameObject root = new GameObject(NextName("SAObject", ref _saObjectCount));
        root.transform.SetParent(parent, false);
        root.transform.position = ToLocal(basePosition);

        // ソロ用と同じ順番: 頭(Cube)、柱(Cube (1))
        CreateSaCube(root.transform, "Cube", basePosition + Vector3.up * (poleHeight + _saHeadSize * 0.5f), Vector3.one * _saHeadSize, refs.saHeadMaterial, refs.saLayer);
        CreateSaCube(root.transform, "Cube (1)", basePosition + Vector3.up * (poleHeight * 0.5f), new Vector3(_saPoleWidth, poleHeight, _saPoleWidth), refs.saPoleMaterial, refs.saLayer);
        return basePosition + Vector3.up * (poleHeight + _saHeadSize);
    }

    /// <summary>
    /// ソロ用と同じ名前の付け方(1つ目は「名前」、2つ目から「名前 (1)」「名前 (2)」…)で、次の名前を返すメソッド
    /// </summary>
    private static string NextName(string baseName, ref int count)
    {
        string result = count == 0 ? baseName : $"{baseName} ({count})";
        count++;
        return result;
    }

    private static void CreateSaCube(Transform parent, string cubeName, Vector3 center, Vector3 size, Material material, int layer)
    {
        GameObject cube = new GameObject(cubeName);
        cube.transform.SetParent(parent, false);
        cube.transform.position = ToLocal(center);
        cube.transform.localScale = size;
        cube.layer = layer;
        cube.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        MeshRenderer meshRenderer = cube.AddComponent<MeshRenderer>();
        if (material != null) meshRenderer.sharedMaterial = material;
        cube.AddComponent<BoxCollider>();
    }

    /// <summary>
    /// 中心のまわり(水平距離がminDistance〜maxDistance)で、中心より高さがminAbove〜maxAbove高い、屋上の内側の点を探すメソッド
    /// </summary>
    private static List<Vector3> FindRoofs(Site center, float minAbove, float maxAbove, float minDistance, float maxDistance)
    {
        List<Vector3> roofs = new List<Vector3>();
        for (float dx = -maxDistance; dx <= maxDistance; dx += _roofGrid)
        {
            for (float dz = -maxDistance; dz <= maxDistance; dz += _roofGrid)
            {
                float distance = Mathf.Sqrt(dx * dx + dz * dz);
                if (distance < minDistance || distance > maxDistance) continue;

                if (!TryGetRoof(center.position.x + dx, center.position.z + dz, out Vector3 roof) || !IsRoofInterior(roof)) continue;

                float above = roof.y - center.position.y;
                if (above >= minAbove && above <= maxAbove && IsStandFree(roof)) roofs.Add(roof);
            }
        }
        return roofs;
    }

    private static bool TryGetRoof(float x, float z, out Vector3 point)
    {
        point = Vector3.zero;
        if (!Physics.Raycast(new Vector3(x, _roofStartHeight, z), Vector3.down, out RaycastHit hit, _roofStartHeight + 5.0f, ~0, QueryTriggerInteraction.Ignore)) return false;
        if (hit.normal.y < 0.95f) return false;

        point = hit.point;
        return true;
    }

    /// <summary>
    /// 屋上の点の、まわり(2.5m四方)も同じ高さの水平な面か(屋上の端や、でこぼこでないか)を判定するメソッド
    /// </summary>
    private static bool IsRoofInterior(Vector3 roof)
    {
        foreach (Vector3 offset in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
        {
            Vector3 around = roof + offset * _roofGrid;
            if (!TryGetRoof(around.x, around.z, out Vector3 neighbor) || Mathf.Abs(neighbor.y - roof.y) > 0.25f) return false;
        }
        return true;
    }

    private static bool IsStandFree(Vector3 stand)
    {
        return !Physics.CheckCapsule(stand + Vector3.up * 0.5f, stand + Vector3.up * 2.0f, 0.4f, ~0, QueryTriggerInteraction.Ignore);
    }

    // =====================================================================
    // SAポイント: 地上(街のどこからも一定の距離以内に置く。結んで、走るだけの移動を減らす)
    // =====================================================================

    private static List<Vector3> _roadRoots = new List<Vector3>();
    private static int _saObjectCount;      // 立てたSAObjectの数(名前の連番に使う)
    private static List<Vector3> _saBases;  // 立てたSAObjectの柱の根元の位置(近すぎる物を避けるのに使う)
    private static int _skippedTooClose;     // 近すぎるので置かなかった数
    private static int _skippedMidRoad;      // 道の端が遠い(道のど真ん中)ので置かなかった数
    private static int _hidePointCount;     // 置いたHidePointの数(名前の連番に使う)

    private static void PlaceRoadSa(GameObject contents, References refs)
    {
        Transform hideGroup = FindOrCreateGroup(contents.transform, _hideGroup);
        Transform saGroup = FindOrCreateGroup(contents.transform, _saObjectGroup);
        _roadRoots = new List<Vector3>();
        int count = 0;
        foreach (Vector3 node in PickRoadNodes())
        {
            if (!TryFindRoadsideBase(node, _roadPoleHeight, out Vector3 poleBase)) continue;

            // 信号・街灯に見立てたSAObjectを、道の端に立て、そのすぐ近く(足元の範囲の箱の中)から、頭の上へ飛ぶ
            Vector3 top = PlaceSaObject(saGroup, refs, poleBase, _roadPoleHeight);
            PlaceHidePoint(hideGroup, refs.hidePrefab, poleBase, top, poleBase.y - 0.5f, false);
            _roadRoots.Add(poleBase);
            count++;
        }
        _report.AppendLine($"SAポイント(地上、SAObjectの上): SAObject {count}本 / SAポイント {count}個(間隔の目安 {_roadSpacing}m)");
        _report.AppendLine($"  置かなかった物: 道の端が遠い(道のど真ん中) {_skippedMidRoad}か所 / 他のSAObjectに近すぎる {_skippedTooClose}か所(屋上を含む)");
    }

    /// <summary>
    /// 街のどこからも_roadSpacing以内に地点があるように、地上の地点を選ぶメソッド
    /// </summary>
    private static List<Vector3> PickRoadNodes()
    {
        List<Vector3> nodes = new List<Vector3>();
        foreach (Vector3 candidate in _ground)
        {
            if (!IsNearAny(nodes, candidate, _roadSpacing)) nodes.Add(candidate);
        }
        return nodes;
    }

    // =====================================================================
    // SAポイントの置き方
    // =====================================================================

    /// <summary>
    /// SAポイントを1つ置くメソッド
    /// rootは、SAObjectの柱の根元(足元の範囲の箱は、この真上を中心にする)。standは、飛んで着く足元の位置(SAObjectの頭の上)
    /// feetLowYは、飛び立つ足元の一番低い高さ。箱は、その高さから頭の上までを含み、水平方向は、箱の中のどこからでも飛んで着ける広さにする
    /// </summary>
    private static void PlaceHidePoint(Transform container, GameObject prefab, Vector3 root, Vector3 stand, float feetLowY, bool keepLight)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container);
        instance.name = NextName("HidePoint", ref _hidePointCount);
        instance.transform.position = ToLocal(root);
        instance.transform.rotation = Quaternion.identity;

        Transform standPoint = instance.transform.Find("StandPoint");
        Transform hideBox = instance.transform.Find("HideBox");
        Transform reachBox = instance.transform.Find("SAReachBox");
        Transform pointLight = instance.transform.Find("Point Light");
        Vector3 localStand = ToLocal(stand + Vector3.up * 0.05f);
        standPoint.position = localStand;
        standPoint.rotation = Quaternion.LookRotation(FindWallDirection(stand));
        hideBox.position = localStand + Vector3.up * 1.65f;

        float heightDifference = stand.y - feetLowY;
        float half = ReachHalf(heightDifference);
        reachBox.position = ToLocal(new Vector3(root.x, (feetLowY + stand.y) * 0.5f, root.z));
        reachBox.localScale = new Vector3(half * 2.0f, heightDifference + 2.0f, half * 2.0f);
        if (pointLight == null) return;

        pointLight.position = localStand + Vector3.up * 0.35f;
        pointLight.gameObject.SetActive(keepLight);
    }

    /// <summary>
    /// 着いた時に向く向き(隠れる物の方)を、一番近い壁の方向から決めるメソッド
    /// </summary>
    private static Vector3 FindWallDirection(Vector3 stand)
    {
        Vector3 best = Vector3.forward;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < 8; i++)
        {
            Vector3 direction = Quaternion.Euler(0.0f, i * 45.0f, 0.0f) * Vector3.forward;
            if (!Physics.Raycast(stand + Vector3.up * 1.0f, direction, out RaycastHit hit, 8.0f, ~0, QueryTriggerInteraction.Ignore) || hit.distance >= bestDistance) continue;

            bestDistance = hit.distance;
            best = direction;
        }
        return best;
    }

    // =====================================================================
    // SAポイント: ビルを登る(屋上から屋上へ、SAで飛び移る)
    // =====================================================================

    /// <summary>
    /// ほとんどのビルの屋上へ、SAで飛び移りながら登っていけるように、屋上にSAObjectとハイドポイントを、階段のように足していくメソッド
    /// まず、地上(や地上のSAObject)から飛び立てる屋上を1段目にして、そこから、1回で飛べる高さ(_climbMinGain〜_climbMaxGain)ずつ高い屋上へ、段を広げていく(幅優先)。
    /// 屋上の点は、つながった同じ高さの面ごとに「ビル」としてまとめ、高さ _climbMinTargetHeight 以上のビルを、高い順に1棟ずつ調べる。
    /// すでに屋上にSAObjectがあるビル・他のビルの登り道で足場になったビルは、そのまま登れるとみなす。
    /// どちらでもなければ、登れる屋上の点を、段の少ない順に試して、1段目からその点までの屋上に、下から順にSAObjectを立てる
    /// </summary>
    private static void PlaceClimbSa(GameObject contents, References refs)
    {
        Transform hideGroup = FindOrCreateGroup(contents.transform, _hideGroup);
        Transform saGroup = FindOrCreateGroup(contents.transform, _saObjectGroup);
        _climbRoofNodes = new List<Vector3>();

        // 屋上の点を、ビルごとにまとめる(すでにSAObjectが立っているビルを見つけるため、近さで絞る前にまとめる)
        List<Vector3> allCells = CollectRoofCells();
        int[] buildingOfAll = ClusterRoofCells(allCells, out int buildingCount);
        float[] buildingTop = new float[buildingCount];
        bool[] hasSa = new bool[buildingCount];
        for (int i = 0; i < allCells.Count; i++)
        {
            int b = buildingOfAll[i];
            buildingTop[b] = Mathf.Max(buildingTop[b], allCells[i].y);
            if (hasSa[b]) continue;

            foreach (Vector3 saBase in _saBases)
            {
                if (FlatDistance(saBase, allCells[i]) < _climbGrid * 1.5f && Mathf.Abs(saBase.y - allCells[i].y) < 1.5f)
                {
                    hasSa[b] = true;
                    _climbRoofNodes.Add(saBase);
                    break;
                }
            }
        }

        List<Vector3> cells = new List<Vector3>();
        List<int> buildingOf = new List<int>();
        for (int i = 0; i < allCells.Count; i++)
        {
            if (!IsFarFromOtherSa(allCells[i]) || !IsPoleFree(allCells[i], _roofPoleHeight)) continue;

            cells.Add(allCells[i]);
            buildingOf.Add(buildingOfAll[i]);
        }

        // 屋上へ飛び移れる足場が無い高いビルのために、ビルの壁にも、張り出した足場の候補を並べる(屋上の点より後ろに足す)
        int roofCount = cells.Count;
        Dictionary<int, Vector3> walls = AddWallLedges(allCells, buildingOfAll, buildingTop, cells, buildingOf);

        int[] depth = new int[cells.Count];
        int[] parent = new int[cells.Count];
        int[] roadParent = new int[cells.Count];     // 地上のSAObjectの頭の上から飛ぶ時の、そのSAObjectの番号(_roadRoots の添字)。違えば -1
        Vector3[] grounds = new Vector3[cells.Count];
        Queue<int> queue = new Queue<int>();
        for (int i = 0; i < cells.Count; i++)
        {
            depth[i] = -1;
            parent[i] = -1;
            roadParent[i] = -1;
            if (!CanReachFromGround(cells[i], out grounds[i])) continue;

            depth[i] = 1;
            queue.Enqueue(i);
        }
        // 地上のSAObject(頭の上は、屋上のSAObjectより _roadPoleHeight - _roofPoleHeight 高い)の頭の上からも、屋上へ飛べる(地上 → 地上のSA → 屋上)
        for (int i = 0; i < cells.Count; i++)
        {
            if (depth[i] != -1) continue;

            for (int r = 0; r < _roadRoots.Count; r++)
            {
                Vector3 low = _roadRoots[r] + Vector3.up * (_roadPoleHeight - _roofPoleHeight);
                if (!CanHop(low, cells[i])) continue;

                depth[i] = 2;
                roadParent[i] = r;
                queue.Enqueue(i);
                break;
            }
        }
        int reachable = queue.Count;
        while (queue.Count > 0)
        {
            int low = queue.Dequeue();
            if (depth[low] >= _climbMaxSteps) continue;

            for (int high = 0; high < cells.Count; high++)
            {
                if (depth[high] != -1 || !CanHop(cells[low], cells[high])) continue;

                depth[high] = depth[low] + 1;
                parent[high] = low;
                queue.Enqueue(high);
                reachable++;
            }
        }

        // ビルごとの、登れる屋上の点
        List<int>[] members = new List<int>[buildingCount];
        for (int i = 0; i < roofCount; i++)
        {
            if (depth[i] < 1) continue;

            if (members[buildingOf[i]] == null) members[buildingOf[i]] = new List<int>();
            members[buildingOf[i]].Add(i);
        }

        // 高いビルから順に、登れるようにする
        List<int> order = new List<int>();
        for (int b = 0; b < buildingCount; b++)
        {
            if (buildingTop[b] >= _climbMinTargetHeight) order.Add(b);
        }
        order.Sort((a, b) => buildingTop[b].CompareTo(buildingTop[a]));

        HashSet<int> built = new HashSet<int>();     // もう立てた屋上の点(番号)
        int[] areaTotal = new int[_areaCenters.Count];
        int[] areaClimbable = new int[_areaCenters.Count];
        int alreadyHas = 0;
        int sharedOne = 0;
        int madeNew = 0;
        int blocked = 0;
        int unreachableLow = 0;
        int unreachableMid = 0;
        int unreachableHigh = 0;
        int steps = 0;
        float highest = 0.0f;
        foreach (int b in order)
        {
            int area = NearestArea(AverageOf(allCells, buildingOfAll, b));
            areaTotal[area]++;
            if (hasSa[b])
            {
                alreadyHas++;
                areaClimbable[area]++;
                continue;
            }

            List<int> candidates = members[b];
            if (candidates == null)
            {
                if (buildingTop[b] < 16.0f) unreachableLow++;
                else if (buildingTop[b] < 30.0f) unreachableMid++;
                else unreachableHigh++;
                continue;
            }

            if (candidates.Exists(index => built.Contains(index)))
            {
                sharedOne++;
                areaClimbable[area]++;
                continue;
            }

            // 段の少ない順に試す(同じ段数なら、ビルの真ん中に近い順)
            Vector3 middle = AverageOf(cells, buildingOf, b, roofCount);
            candidates.Sort((x, y) =>
            {
                int byDepth = depth[x].CompareTo(depth[y]);
                return byDepth != 0 ? byDepth : FlatDistance(cells[x], middle).CompareTo(FlatDistance(cells[y], middle));
            });
            bool made = false;
            for (int t = 0; t < candidates.Count && t < _climbTriesPerBuilding && !made; t++)
            {
                List<int> path = TryMakeClimbPath(candidates[t], parent, cells, built);
                if (path == null) continue;

                steps += BuildClimb(hideGroup, saGroup, refs, path, cells, parent, roadParent, grounds, built, walls);
                highest = Mathf.Max(highest, buildingTop[b]);
                made = true;
            }
            if (made)
            {
                madeNew++;
                areaClimbable[area]++;
            }
            else
            {
                blocked++;
            }
        }
        _report.AppendLine($"  ビルを登る: 高さ{_climbMinTargetHeight}m以上のビル {order.Count}棟(エリアごと: {string.Join(", ", areaTotal)}) / 登れる {alreadyHas + sharedOne + madeNew}棟(エリアごと: {string.Join(", ", areaClimbable)})");
        _report.AppendLine($"    内訳: 屋上にSAObjectがもともとある {alreadyHas}棟 / 他の登り道の足場になった {sharedOne}棟 / 新しく登り道を作った {madeNew}棟");
        _report.AppendLine($"    登れない: 他のSAObjectに近すぎて立てられない {blocked}棟 / 飛び移れる足場が無い(高さ16m未満 {unreachableLow}棟, 30m未満 {unreachableMid}棟, 30m以上 {unreachableHigh}棟)");
        _report.AppendLine($"SAポイント(ビルを登る): 足場にできる屋上 {roofCount}点 + 壁の足場の候補 {walls.Count}点(うち、地上から飛べる1段目 {CountDepth(depth, 1)}点、段を広げて登れる屋上 {reachable}点) / 足したSAObject {steps}本 / 一番高い屋上 {highest:F0}m");
    }

    /// <summary>
    /// 屋上の点から1段目まで(上から下へ)の、立てる屋上の列を作るメソッド
    /// すでに立てた屋上は使い回し、新しく立てる屋上が、他のSAObjectや、同じ列の別の屋上に近すぎれば、null を返す
    /// </summary>
    private static List<int> TryMakeClimbPath(int start, int[] parent, List<Vector3> cells, HashSet<int> built)
    {
        List<int> path = new List<int>();
        for (int node = start; node != -1; node = parent[node])
        {
            if (!built.Contains(node))
            {
                if (!IsFarFromOtherSa(cells[node])) return null;

                // 同じ列の中でも、つながっていない屋上同士が近すぎないようにする(横に飛び移る列で、行って戻るような並びを避ける)
                foreach (int other in path)
                {
                    if (!built.Contains(other) && Vector3.Distance(cells[node], cells[other]) < _saMinSpacing) return null;
                }
            }
            path.Add(node);
        }
        return path;
    }

    /// <summary>
    /// 屋上の点を、つながった同じ高さの面ごとに、ビル(の屋上)としてまとめるメソッド(隣り合う点の高さの差が _climbSameRoof 以内なら同じビル)
    /// </summary>
    /// <returns>点ごとの、ビルの番号</returns>
    private static int[] ClusterRoofCells(List<Vector3> cells, out int count)
    {
        Dictionary<long, int> lookup = new Dictionary<long, int>();
        for (int i = 0; i < cells.Count; i++) lookup[RoofGridKey(cells[i].x, cells[i].z, 0, 0)] = i;

        int[] result = new int[cells.Count];
        for (int i = 0; i < result.Length; i++) result[i] = -1;
        count = 0;
        Stack<int> stack = new Stack<int>();
        for (int i = 0; i < cells.Count; i++)
        {
            if (result[i] != -1) continue;

            result[i] = count;
            stack.Push(i);
            while (stack.Count > 0)
            {
                int current = stack.Pop();
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (!lookup.TryGetValue(RoofGridKey(cells[current].x, cells[current].z, dx, dz), out int next) || result[next] != -1) continue;
                        if (Mathf.Abs(cells[next].y - cells[current].y) > _climbSameRoof) continue;

                        result[next] = count;
                        stack.Push(next);
                    }
                }
            }
            count++;
        }
        return result;
    }

    private static long RoofGridKey(float x, float z, int dx, int dz)
    {
        return (long)(Mathf.RoundToInt((x + 400.0f) / _climbGrid) + dx) * 100000L + (Mathf.RoundToInt((z + 400.0f) / _climbGrid) + dz);
    }

    /// <summary>
    /// 同じビルの屋上の点の、真ん中の位置を求めるメソッド
    /// </summary>
    private static Vector3 AverageOf(List<Vector3> cells, IList<int> buildingOfCell, int building, int length = int.MaxValue)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;
        for (int i = 0; i < cells.Count && i < length; i++)
        {
            if (buildingOfCell[i] != building) continue;

            sum += cells[i];
            count++;
        }
        return count > 0 ? sum / count : Vector3.zero;
    }

    /// <summary>
    /// ビルの壁に、張り出した足場(壁から腕でつないだSAObject)の候補を、高さ _ledgeStep おきに並べるメソッド
    /// 屋上の縁(外が _ledgeDrop 以上低くなっている所)の格子点から外向きに、各高さで壁を探し、壁から離した位置に柱を立てられれば、候補にする
    /// </summary>
    /// <returns>候補の番号(cells の添字)と、壁の点</returns>
    private static Dictionary<int, Vector3> AddWallLedges(List<Vector3> allCells, int[] buildingOfAll, float[] buildingTop, List<Vector3> cells, List<int> buildingOf)
    {
        Dictionary<int, Vector3> walls = new Dictionary<int, Vector3>();
        HashSet<long> roofKeys = new HashSet<long>();
        foreach (Vector3 cell in allCells) roofKeys.Add(RoofGridKey(cell.x, cell.z, 0, 0));

        Vector3[] outward = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        float offset = _saHeadSize * 0.5f + _poleMargin + 0.3f;
        for (int i = 0; i < allCells.Count; i++)
        {
            Vector3 roof = allCells[i];
            int b = buildingOfAll[i];
            int gridX = Mathf.RoundToInt((roof.x + 400.0f) / _climbGrid);
            int gridZ = Mathf.RoundToInt((roof.z + 400.0f) / _climbGrid);
            if (buildingTop[b] < _climbMinTargetHeight || (gridX + gridZ) % _ledgeEvery != 0) continue;

            foreach (Vector3 direction in outward)
            {
                if (roofKeys.Contains(RoofGridKey(roof.x, roof.z, Mathf.RoundToInt(direction.x), Mathf.RoundToInt(direction.z)))) continue;

                // 本物の縁か(この先が、屋上より十分低くなっているか)
                Vector3 beyond = roof + direction * _ledgeProbe;
                if (Physics.Raycast(new Vector3(beyond.x, roof.y + 3.0f, beyond.z), Vector3.down, out RaycastHit below, roof.y + 10.0f, ~0, QueryTriggerInteraction.Ignore) && below.point.y > roof.y - _ledgeDrop) continue;

                for (float y = _ledgeMinHeight; y <= roof.y - 3.0f; y += _ledgeStep)
                {
                    Vector3 origin = new Vector3(roof.x + direction.x * (_ledgeProbe + 1.0f), y, roof.z + direction.z * (_ledgeProbe + 1.0f));
                    if (!Physics.Raycast(origin, -direction, out RaycastHit wall, _ledgeProbe + 1.5f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (wall.distance < 1.0f || Vector3.Dot(wall.normal, direction) < 0.9f || Mathf.Abs(wall.normal.y) > 0.25f) continue;

                    Vector3 pole = wall.point + wall.normal * offset;
                    pole.y = y;
                    if (!IsFarFromOtherSa(pole) || !IsPoleFree(pole, _roofPoleHeight)) continue;

                    walls[cells.Count] = wall.point;
                    cells.Add(pole);
                    buildingOf.Add(b);
                }
            }
        }
        return walls;
    }

    /// <summary>
    /// 壁に張り出した足場の柱と、壁をつなぐ腕(コライダー無しの細い箱)を、SAObjectに足すメソッド
    /// 壁から少しだけ(5cm)離して、壁に埋まっている扱いにならないようにする
    /// </summary>
    private static void AddWallArm(Transform saObject, Vector3 poleBase, Vector3 wallPoint, References refs)
    {
        Vector3 toWall = FlatOf(wallPoint - poleBase);
        float length = toWall.magnitude - 0.05f;
        if (length <= 0.1f) return;

        Vector3 direction = toWall.normalized;
        GameObject arm = new GameObject("Cube (2)");
        arm.transform.SetParent(saObject, false);
        arm.transform.position = ToLocal(poleBase + direction * (length * 0.5f) + Vector3.up * (_roofPoleHeight * 0.5f));
        arm.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        arm.transform.localScale = new Vector3(_ledgeArmWidth, _ledgeArmWidth, length);
        arm.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        MeshRenderer meshRenderer = arm.AddComponent<MeshRenderer>();
        if (refs.saPoleMaterial != null) meshRenderer.sharedMaterial = refs.saPoleMaterial;
    }

    private static int CountDepth(int[] depth, int value)
    {
        int count = 0;
        foreach (int item in depth)
        {
            if (item == value) count++;
        }
        return count;
    }

    /// <summary>
    /// 屋上の点(格子状。_climbGrid おき)を集めるメソッド(高さ _climbMinRoofHeight 以上の、屋上の内側だけ)
    /// </summary>
    private static List<Vector3> CollectRoofCells()
    {
        List<Vector3> cells = new List<Vector3>();
        for (float x = -400.0f; x <= 400.0f; x += _climbGrid)
        {
            for (float z = -400.0f; z <= 400.0f; z += _climbGrid)
            {
                if (TryGetRoof(x, z, out Vector3 roof) && roof.y >= _climbMinRoofHeight && roof.y <= _climbMaxRoofHeight && IsRoofInterior(roof)) cells.Add(roof);
            }
        }
        return cells;
    }

    /// <summary>
    /// 低い屋上から、高い屋上へ、1回のSAで飛び移れるかを判定するメソッド
    /// 高さの差が _climbMinGain〜_climbMaxGain、水平距離が _climbMinStep 以上で、足元の範囲の箱(ReachHalf)に収まり、SAObject同士が近すぎないこと
    /// </summary>
    private static bool CanHop(Vector3 low, Vector3 high)
    {
        float gain = high.y - low.y;
        if (gain < _climbMinGain || gain > _climbMaxGain) return false;

        float flat = FlatDistance(low, high);
        return flat >= _climbMinStep && flat <= ReachHalf(gain + 0.5f) * 0.9f && Vector3.Distance(low, high) >= _saMinSpacing;
    }

    /// <summary>
    /// 屋上のSAObjectの頭の上へ、地上のどこかから飛んで着けるかを判定するメソッド(足元の範囲の箱が、地上の歩ける場所に届くこと)
    /// </summary>
    private static bool CanReachFromGround(Vector3 roof, out Vector3 ground)
    {
        if (!TryFindNearestGround(roof, out ground) || ground.y >= _groundHeight) return false;

        float topY = roof.y + _roofPoleHeight + _saHeadSize;
        float feetY = ground.y - 0.5f;
        return topY - feetY <= _maxFlightDistance && FlatDistance(ground, roof) <= ReachHalf(topY - feetY) * 0.9f;
    }

    /// <summary>
    /// たどれた屋上の列に、下から順に、SAObjectとハイドポイントを立てるメソッド(一番下は、地上から。上は、1つ下のSAObjectの頭の上から飛ぶ)
    /// すでに立てた屋上は、立て直さない
    /// </summary>
    /// <param name="path">目標から1段目まで(上から下へ)の屋上の番号</param>
    /// <returns>新しく立てたSAObjectの数</returns>
    private static int BuildClimb(Transform hideGroup, Transform saGroup, References refs, List<int> path, List<Vector3> cells, int[] parent, int[] roadParent, Vector3[] grounds, HashSet<int> built, Dictionary<int, Vector3> walls)
    {
        int count = 0;
        for (int i = path.Count - 1; i >= 0; i--)
        {
            int node = path[i];
            if (!built.Add(node)) continue;

            float feetY;
            if (parent[node] != -1) feetY = cells[parent[node]].y + _roofPoleHeight + _saHeadSize - 0.5f;
            else if (roadParent[node] != -1) feetY = _roadRoots[roadParent[node]].y + _roadPoleHeight + _saHeadSize - 0.5f;
            else feetY = grounds[node].y - 0.5f;
            Vector3 top = PlaceSaObject(saGroup, refs, cells[node], _roofPoleHeight);
            if (walls.TryGetValue(node, out Vector3 wallPoint)) AddWallArm(saGroup.GetChild(saGroup.childCount - 1), cells[node], wallPoint, refs);
            else _climbRoofNodes.Add(cells[node]);
            PlaceHidePoint(hideGroup, refs.hidePrefab, cells[node], top, feetY, true);
            count++;
        }
        return count;
    }

    // =====================================================================
    // 防犯レーザー: 通路を横切って、通路に沿って往復する
    // =====================================================================

    // 往復するレーザー1本分(置く場所・往復する向きと長さ・レーザーの長さ)
    private class SweepLaser
    {
        public int area;
        public Vector3 start;        // 往復の始点(通路の真ん中。足元の高さ)
        public Vector3 direction;    // 始点から終点へ向かう向き(水平)
        public float distance;       // 始点から終点までの距離(m)
        public float length;         // レーザーの長さ(m。通路の端から端まで)
        public float score;
        public bool isRoof;
        public string place;         // 報告用の、置いた場所の説明
    }

    /// <summary>
    /// 防犯レーザーを置くメソッド。通路(壁と壁の間・屋上の端から端まで)を端から端まで横切る棒が、通路に沿って、始点から終点まで往復する
    /// 始点・終点は、通路が続く両端(壁・柱・SAObjectの柱・歩ける場所の終わり)に当たらない範囲に合わせる
    /// ・地上: 悪人の出現点の近く(_sweepMinFromVillain〜_sweepMaxFromVillain)の、道幅が _sweepGroundMinWidth〜_sweepGroundMaxWidth の通路を、エリアごとに _sweepGroundPerArea 本
    /// ・屋上: 登れるビルの屋上の悪人の出現点のそばの屋上を、エリアごとに _sweepRoofPerArea 本(SAで着いた後、悪人へ向かう途中を横切る)
    /// </summary>
    private static void PlaceSweepLasers(GameObject contents, References refs, List<Site> sites)
    {
        Transform lasers = FindOrCreateGroup(contents.transform, _laserGroup);
        List<SweepLaser> chosen = new List<SweepLaser>();
        // 道幅 _sweepGroundMaxWidth までの通路を優先し、無いエリアは、_sweepGroundWideWidth までの、広めの通路から選ぶ
        List<SweepLaser> groundCandidates = FindGroundSweeps(sites, _sweepGroundMaxWidth);
        groundCandidates.AddRange(FindGroundSweeps(sites, _sweepGroundWideWidth));
        for (int round = 0; round < _sweepGroundPerArea; round++)
        {
            for (int area = 0; area < _areaCenters.Count; area++)
            {
                SweepLaser best = groundCandidates.Find(candidate => candidate.area == area && !IsSweepNear(chosen, candidate));
                if (best != null) chosen.Add(best);
            }
        }

        for (int area = 0; area < _areaCenters.Count; area++)
        {
            List<Site> roofSites = sites.FindAll(site => site.isRoof && site.area == area);
            roofSites.Sort((a, b) => b.position.y.CompareTo(a.position.y));
            int made = 0;
            foreach (Site site in roofSites)
            {
                if (made >= _sweepRoofPerArea) break;

                SweepLaser best = FindRoofSweeps(site).Find(candidate => !IsSweepNear(chosen, candidate));
                if (best == null) continue;

                chosen.Add(best);
                made++;
            }
        }

        int[] groundPerArea = new int[_areaCenters.Count];
        int[] roofPerArea = new int[_areaCenters.Count];
        int number = 0;
        foreach (SweepLaser sweep in chosen)
        {
            number++;
            if (sweep.isRoof) roofPerArea[sweep.area]++;
            else groundPerArea[sweep.area]++;

            CreateSweepLaser(lasers, refs, sweep, $"SecurityLaser_A{sweep.area}_{number:00}");
            _report.AppendLine($"  レーザー{number}(エリア{sweep.area}・{sweep.place}): 始点 ({sweep.start.x:F0}, {sweep.start.y:F1}, {sweep.start.z:F0}) → 終点 ({(sweep.start + sweep.direction * sweep.distance).x:F0}, {sweep.start.y:F1}, {(sweep.start + sweep.direction * sweep.distance).z:F0}) / 往復 {sweep.distance:F0}m / レーザーの長さ {sweep.length:F1}m");
        }
        _report.AppendLine($"防犯レーザー(通路を横切って往復): {chosen.Count}本(地上 エリアごと: {string.Join(", ", groundPerArea)} / 屋上 エリアごと: {string.Join(", ", roofPerArea)})");
    }

    /// <summary>
    /// 選んだレーザーのどれかと、近すぎる(往復の線の上の点同士が _sweepSpacing 未満。屋上と地上は、高さが離れていれば別)かを判定するメソッド
    /// </summary>
    private static bool IsSweepNear(List<SweepLaser> chosen, SweepLaser sweep)
    {
        foreach (SweepLaser other in chosen)
        {
            if (Mathf.Abs(other.start.y - sweep.start.y) > 4.0f) continue;

            for (float a = 0.0f; a <= sweep.distance; a += 5.0f)
            {
                Vector3 pointA = sweep.start + sweep.direction * a;
                for (float b = 0.0f; b <= other.distance; b += 5.0f)
                {
                    if (FlatDistance(pointA, other.start + other.direction * b) < _sweepSpacing) return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// 地上の通路の、往復するレーザーの候補を、点数の高い順に集めるメソッド
    /// 点数: 往復の長さ + (悪人の出現点が近いほど高い) - 道幅 × 2(狭い通路ほど、すり抜けにくい)
    /// </summary>
    private static List<SweepLaser> FindGroundSweeps(List<Site> sites, float maxWidth)
    {
        List<SweepLaser> result = new List<SweepLaser>();
        HashSet<Vector2Int> seenStart = new HashSet<Vector2Int>();
        for (int i = 0; i < _ground.Count; i += 2)
        {
            Vector3 point = _ground[i];
            if (IsNearAny(_startPoints, point, _siteMinFromStart)) continue;

            float nearVillain = DistanceToNearestSite(sites, point, false);
            if (nearVillain < _sweepMinFromVillain || nearVillain > (maxWidth > _sweepGroundMaxWidth ? _sweepWideFromVillain : _sweepMaxFromVillain)) continue;
            if (FindStreetYaw(point, out float yaw) < _sweepMinLength + 2.0f) continue;
            if (!TryBuildSweep(point, yaw, maxWidth > _sweepGroundMaxWidth ? _sweepGroundMaxWidth : _sweepGroundMinWidth, maxWidth, out SweepLaser sweep)) continue;
            if (!seenStart.Add(new Vector2Int(Mathf.RoundToInt(sweep.start.x / 4.0f), Mathf.RoundToInt(sweep.start.z / 4.0f)))) continue;

            sweep.area = NearestArea(point);
            sweep.place = $"地上の通路 道幅{sweep.length - _sweepBeamExtra:F0}m";
            sweep.score = sweep.distance + (_sweepMaxFromVillain - nearVillain) * 0.5f - (sweep.length - _sweepBeamExtra) * 2.0f;
            result.Add(sweep);
        }
        result.Sort((a, b) => b.score.CompareTo(a.score));
        return result;
    }

    /// <summary>
    /// 屋上の悪人の出現点のそばの、同じ屋上の通路(屋上の端から端まで)の、往復するレーザーの候補を、点数の高い順に集めるメソッド
    /// 点数: 往復の長さ - 悪人の出現点からの距離 × 0.5
    /// </summary>
    private static List<SweepLaser> FindRoofSweeps(Site site)
    {
        List<SweepLaser> result = new List<SweepLaser>();
        HashSet<Vector2Int> seenStart = new HashSet<Vector2Int>();
        foreach (Vector3 point in _roofNavAll)
        {
            if (Mathf.Abs(point.y - site.position.y) > 1.5f) continue;

            float distance = FlatDistance(point, site.position);
            if (distance < _sweepRoofMinFromVillain || distance > _sweepRoofMaxFromVillain) continue;
            if (FindStreetYaw(point, out float yaw) < _sweepMinLength + 2.0f) continue;
            if (!TryBuildSweep(point, yaw, _sweepRoofMinWidth, _sweepRoofMaxWidth, out SweepLaser sweep)) continue;
            if (!seenStart.Add(new Vector2Int(Mathf.RoundToInt(sweep.start.x / 4.0f), Mathf.RoundToInt(sweep.start.z / 4.0f)))) continue;

            sweep.area = site.area;
            sweep.isRoof = true;
            sweep.place = $"屋上 高さ{site.position.y:F0}m";
            sweep.score = sweep.distance - distance * 0.5f;
            result.Add(sweep);
        }
        result.Sort((a, b) => b.score.CompareTo(a.score));
        return result;
    }

    private static float DistanceToNearestSite(List<Site> sites, Vector3 position, bool roof)
    {
        float min = float.MaxValue;
        foreach (Site site in sites)
        {
            if (site.isRoof == roof) min = Mathf.Min(min, FlatDistance(site.position, position));
        }
        return min;
    }

    /// <summary>
    /// 点を通る、向き(yaw)に続く通路に、往復するレーザーを作れるかを調べるメソッド
    /// 長い往復から順に(_sweepLengths)、通路が続く後ろの端から点までの間のいくつかの位置(_sweepStartShares)を始点にして、通路の真ん中に寄せられるかを試す。
    /// 道幅(線に直角な向きの、歩ける幅の最小値)が minWidth〜maxWidth であること。終点は、壁・柱・SAObjectの柱に当たる手前まで縮める
    /// </summary>
    private static bool TryBuildSweep(Vector3 point, float yaw, float minWidth, float maxWidth, out SweepLaser sweep)
    {
        sweep = null;
        Vector3 along = Quaternion.Euler(0.0f, yaw, 0.0f) * Vector3.forward;
        float forward = ClearLength100(point, along);
        float back = ClearLength100(point, -along);
        if (forward + back - 2.0f < _sweepMinLength) return false;

        foreach (float wanted in _sweepLengths)
        {
            foreach (float share in _sweepStartShares)
            {
                float behind = Mathf.Max(0.0f, (back - 1.0f) * share);
                float distance = Mathf.Min(wanted, forward + behind - 2.0f);
                if (distance < _sweepMinLength) continue;
                if (!NavMesh.SamplePosition(point - along * behind, out NavMeshHit hit, 0.8f, NavMesh.AllAreas)) continue;
                if (!TryCenterOnStreet(hit.position, along, distance, out Vector3 centered, out float width) || width < minWidth || width > maxWidth) continue;

                float length = width + _sweepBeamExtra;
                float free = SweepFreeLength(centered, along, length, distance);
                if (free < _sweepMinLength) continue;

                sweep = new SweepLaser { start = centered, direction = along, distance = free, length = length };
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 始点から向きへ、レーザー(長さlength・高さ _sweepHeight)を動かして、壁・柱・SAObjectの柱に当たらずに動ける距離を返すメソッド
    /// </summary>
    private static float SweepFreeLength(Vector3 start, Vector3 along, float length, float maxDistance)
    {
        Quaternion rotation = Quaternion.LookRotation(along);
        Vector3 side = rotation * Vector3.right;
        float free = maxDistance;

        // 立てたSAObject(柱と頭の箱)が、レーザーの高さにあり、通り道の上にあれば、その手前まで
        foreach (Vector3 saBase in _saBases)
        {
            if (saBase.y - start.y > _sweepHeight + 0.3f || saBase.y + _roadPoleHeight + _saHeadSize < start.y + _sweepHeight - 0.3f) continue;

            Vector3 offset = FlatOf(saBase - start);
            float ahead = Vector3.Dot(offset, along);
            if (ahead < -1.0f || ahead > maxDistance + 1.0f || Mathf.Abs(Vector3.Dot(offset, side)) > length * 0.5f + 1.0f) continue;

            free = Mathf.Min(free, ahead - 1.5f);
        }

        Vector3 half = new Vector3(length * 0.5f, _sweepBeamThickness * 0.5f + 0.1f, 0.5f);
        for (float t = 0.0f; t <= free; t += 1.0f)
        {
            if (Physics.CheckBox(start + along * t + Vector3.up * _sweepHeight, half, rotation, ~0, QueryTriggerInteraction.Ignore)) return Mathf.Max(0.0f, t - 1.5f);
        }
        return Mathf.Max(0.0f, free);
    }

    /// <summary>
    /// 道に沿う向きに直角な向きの、歩ける幅(両側の合計)を返すメソッド(centerShiftは、道の真ん中が、点から直角な向きにずれている量)
    /// </summary>
    private static float CrossWidth(Vector3 position, Vector3 along, out float centerShift)
    {
        Vector3 side = Vector3.Cross(Vector3.up, along);
        float left = WalkLength(position, side);
        float right = WalkLength(position, -side);
        centerShift = (left - right) * 0.5f;
        return left + right;
    }

    private static void CreateSweepLaser(Transform container, References refs, SweepLaser sweep, string laserName)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(refs.laserPrefab, container);
        instance.name = laserName;
        CopyScriptValues(refs.laserTemplate.gameObject, instance);
        Quaternion rotation = Quaternion.LookRotation(sweep.direction);
        instance.transform.position = ToLocal(sweep.start + Vector3.up * _sweepHeight);
        instance.transform.rotation = Quaternion.Inverse(_multi.rotation) * rotation;
        _policeFaction.Add(sweep.start + sweep.direction * (sweep.distance * 0.5f));

        SerializedObject serialized = new SerializedObject(instance.GetComponent<CS_SecurityLaser>());
        serialized.FindProperty("_beamLength").floatValue = sweep.length;
        serialized.FindProperty("_beamThickness").floatValue = _sweepBeamThickness;
        serialized.FindProperty("_moveDirection").vector3Value = Vector3.forward;
        serialized.FindProperty("_moveDistance").floatValue = Mathf.Clamp(sweep.distance, 0.0f, 50.0f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static float MinFlatDistance(List<Vector3> points, Vector3 position)
    {
        float min = float.MaxValue;
        foreach (Vector3 point in points) min = Mathf.Min(min, FlatDistance(point, position));
        return min;
    }

    /// <summary>
    /// シーンにあるマルチの悪人の出現点を、Site にして集めるメソッド(レーザーだけ置き直す時に使う)
    /// </summary>
    private static List<Site> CollectSceneSites()
    {
        List<Site> found = new List<Site>();
        foreach (CS_VillainSpawnPoint point in _multi.GetComponentsInChildren<CS_VillainSpawnPoint>(true))
        {
            Vector3 position = point.transform.position;
            found.Add(new Site { area = NearestArea(position), index = found.Count, position = position, yaw = point.transform.eulerAngles.y, isRoof = position.y >= _roofVillainMinHeight });
        }
        return found;
    }

    /// <summary>
    /// シーンにあるマルチのSAObjectの、柱の根元の位置を集めるメソッド(レーザーだけ置き直す時に使う)
    /// </summary>
    private static List<Vector3> CollectSceneSaBases()
    {
        List<Vector3> found = new List<Vector3>();
        Transform group = _multi.Find(_saObjectGroup);
        if (group == null) return found;

        foreach (Transform child in group) found.Add(child.position);
        return found;
    }

    // =====================================================================
    // 設定値の写し・確認
    // =====================================================================

    /// <summary>
    /// 元のゲームオブジェクトのスクリプトの設定値を、コピーへ写すメソッド(参照は、アセットだけ写す)
    /// </summary>
    private static void CopyScriptValues(GameObject source, GameObject copy)
    {
        MonoBehaviour[] sourceScripts = source.GetComponents<MonoBehaviour>();
        MonoBehaviour[] copyScripts = copy.GetComponents<MonoBehaviour>();
        for (int i = 0; i < sourceScripts.Length && i < copyScripts.Length; i++)
        {
            if (sourceScripts[i] == null || copyScripts[i] == null || sourceScripts[i].GetType() != copyScripts[i].GetType()) continue;

            SerializedObject from = new SerializedObject(sourceScripts[i]);
            SerializedObject to = new SerializedObject(copyScripts[i]);
            SerializedProperty property = from.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = property.propertyType != SerializedPropertyType.ObjectReference;
                if (property.name == "m_Script") continue;
                if (property.propertyType == SerializedPropertyType.ObjectReference
                    && property.objectReferenceValue != null && !EditorUtility.IsPersistent(property.objectReferenceValue)) continue;

                to.CopyFromSerializedProperty(property);
            }
            to.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>
    /// 街を走るだけの移動の長さを調べて書き出すメソッド
    /// 地上の歩ける点ごとに、道のり(NavMesh)で一番近い「SAポイント」と「敵(悪人・警察陣営)」までを走る時間にして、
    /// 中央値・95%・最大と、上限(_maxRunSeconds)を超える点の数を出す。道のりは、まっすぐの距離が近い順に5か所だけ調べる
    /// </summary>
    private static void ReportRunTime(List<Site> sites)
    {
        List<Vector3> saTargets = new List<Vector3>(_roadRoots);
        List<Vector3> enemyTargets = new List<Vector3>(_policeFaction);
        foreach (Site site in sites) enemyTargets.Add(site.position);

        List<float> saTimes = new List<float>();
        List<float> enemyTimes = new List<float>();
        NavMeshPath path = new NavMeshPath();
        for (int i = 0; i < _ground.Count; i += 2)
        {
            saTimes.Add(NearestRunSeconds(_ground[i], saTargets, path));
            enemyTimes.Add(NearestRunSeconds(_ground[i], enemyTargets, path));
        }
        _report.AppendLine($"走るだけの移動(速さ {_runSpeed}m/秒。地上の{saTimes.Count}点で調査):");
        AppendRunTime("  一番近いSAポイントまで", saTimes);
        AppendRunTime("  一番近い敵(悪人・警察陣営)まで", enemyTimes);
    }

    private static float NearestRunSeconds(Vector3 from, List<Vector3> targets, NavMeshPath path)
    {
        List<(float distance, Vector3 target)> near = new List<(float, Vector3)>();
        foreach (Vector3 target in targets) near.Add((FlatDistance(from, target), target));
        near.Sort((a, b) => a.distance.CompareTo(b.distance));

        float best = float.MaxValue;
        for (int i = 0; i < Mathf.Min(5, near.Count); i++)
        {
            float length = NavMesh.CalculatePath(from, near[i].target, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete ? PathLength(path) : near[i].distance * 1.5f;
            best = Mathf.Min(best, length);
        }
        return best / _runSpeed;
    }

    private static float PathLength(NavMeshPath path)
    {
        float length = 0.0f;
        for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        return length;
    }

    private static void AppendRunTime(string label, List<float> seconds)
    {
        seconds.Sort();
        int over = seconds.FindAll(value => value > _maxRunSeconds).Count;
        _report.AppendLine($"{label}: 中央値 {seconds[seconds.Count / 2]:F0}秒 / 95% {seconds[seconds.Count * 95 / 100]:F0}秒 / 最大 {seconds[seconds.Count - 1]:F0}秒 / {_maxRunSeconds:F0}秒を超える点 {over}個");
    }

    /// <summary>
    /// 配置の確認(悪人の近くの警察陣営・地上のSAポイントの届く範囲)を書き出すメソッド
    /// </summary>
    private static void ReportCoverage(List<Site> sites)
    {
        int covered = 0;
        int total = 0;
        float maxGap = 0.0f;
        foreach (Vector3 ground in _ground)
        {
            float nearest = float.MaxValue;
            foreach (Vector3 root in _roadRoots) nearest = Mathf.Min(nearest, FlatDistance(ground, root));
            total++;
            if (nearest <= _maxReachHalf) covered++;
            maxGap = Mathf.Max(maxGap, nearest);
        }
        _report.AppendLine($"地上のSAポイントの足元の範囲({_maxReachHalf}m 以内)に入る地上の点: {covered}/{total}({(total > 0 ? covered * 100 / total : 0)}%) / 一番遠い点まで {maxGap:F0}m");

        foreach (Site site in sites)
        {
            _report.AppendLine($"  悪人 エリア{site.area}-{site.index + 1}: ({site.position.x:F0}, {site.position.y:F0}, {site.position.z:F0}) 通りの向き {site.yaw:F0}度 往復 {_trailDistance[site.area % _trailDistance.Length]}m・見回し {_trailLookTime[site.area % _trailLookTime.Length]}秒 / 警察陣営まで {MinDistanceToPoliceFaction(site.position):F0}m");
        }
    }
}
