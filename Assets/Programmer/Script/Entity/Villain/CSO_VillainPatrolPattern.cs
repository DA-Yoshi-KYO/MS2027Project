using System;
using System.Collections.Generic;
using UnityEngine;

/*
 * 悪人グループの巡回パターン(ScriptableObject)
 * 悪人(先頭)ごとのルート(ポイントの並び)と、ポイントでやること・回り方・速さ・隊列を持つ
 * スポーン位置に付けたCS_VillainPatrolRouteから使う(同じパターンを複数のスポーン位置で使い回せる)
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・作り方・編集はSceneビューの巡回エディタで行う(スポーン位置を選ぶとツールバーが出る。ClaudeDocs/巡回エディタの使い方.md)
 * ・ポイントの位置はスポーン位置からの相対(スポーン位置の向きで回す。Scaleは掛けない)
 *   → 同じパターンを別のスポーン位置で使うと、そのスポーン位置を基準に同じ形で回る
 * ・ルート(悪人1人分)
 *   ポイント1(スタート)から順に回る。回り方は 周回(最後の後にポイント1へ戻る) / 往復(ポイント1まで折り返す)
 *   速さは m/秒。startAngle はポイント1で湧いた時の向き(スポーン位置の向きからの角度、＋で右回り)
 *   ポイントに着いたら、やることを上から順に実行してから次へ向かう(何も無ければ止まらずに次へ)
 *     停止(秒) / 見回し(左右の角度・秒。秒数で左右を1往復する) / 向きを変える(角度。turnDuration秒かけて回る)
 * ・隊列
 *   ルートごとに、先頭に付いてくる人数・形・間隔を決める。付いてくる人は先頭の向きに合わせて並び、先頭と一緒に止まる・見回す
 * ・湧く人数と割り当て(CS_VillainSpawner)
 *   湧く人数は、このパターンの人数(先頭 + 付いてくる人)まで(それより多くは湧かない)
 *   先頭を上のルートから全員先に埋め、次に付いてくる人をルート1の隊列 → ルート2の隊列…の順に埋める(GetSlot)
 */
// ========================================

[CreateAssetMenu(fileName = "DB_VillainPatrol", menuName = "Villain/Villain Patrol Pattern")]
public class CSO_VillainPatrolPattern : ScriptableObject
{
    public const float turnDuration = 0.5f;   // 向きを変えるのにかかる時間(秒)
    public const float minSpeed = 0.5f;       // ルートの速さの下限(m/秒)

    // ルートの色(置いた順に使う。巡回エディタの表示用)
    public static readonly Color[] palette =
    {
        new Color32(0xd6, 0x45, 0x3b, 0xff), new Color32(0x2f, 0x6f, 0xd6, 0xff),
        new Color32(0x0f, 0x9a, 0x72, 0xff), new Color32(0x8a, 0x55, 0xd6, 0xff),
        new Color32(0xc4, 0x7a, 0x0e, 0xff), new Color32(0x1a, 0x8f, 0xa8, 0xff),
        new Color32(0xc4, 0x40, 0x7e, 0xff), new Color32(0x5f, 0x7d, 0x16, 0xff),
    };

    // ポイントでやること1つ分
    [Serializable]
    public class Action
    {
        [SerializeField] private CSE_VillainPatrolActionType _type;
        [SerializeField, Min(0f)] private float _seconds;   // 停止・見回しの時間(秒)
        [SerializeField] private float _angle;              // 見回し: 左右の角度(度) / 向きを変える: 回る角度(度、＋で右回り)

        public CSE_VillainPatrolActionType type => _type;
        public float seconds => _seconds;
        public float angle => _angle;

        // 実行にかかる時間(秒)
        public float duration => _type == CSE_VillainPatrolActionType.Turn ? turnDuration : Mathf.Max(0f, _seconds);

        public Action(CSE_VillainPatrolActionType type, float seconds, float angle)
        {
            _type = type;
            _seconds = seconds;
            _angle = angle;
        }

        // 種類ごとの初期値で作る(停止2秒 / 見回し左右60度を3秒 / 向きを変える90度)
        public static Action CreateDefault(CSE_VillainPatrolActionType type)
        {
            switch (type)
            {
                case CSE_VillainPatrolActionType.Look: return new Action(type, 3f, 60f);
                case CSE_VillainPatrolActionType.Turn: return new Action(type, 0f, 90f);
                default: return new Action(type, 2f, 0f);
            }
        }
    }

    // ルートのポイント1つ分
    [Serializable]
    public class Point
    {
        [SerializeField] private Vector3 _localPosition;   // スポーン位置からの相対位置
        [SerializeField] private List<Action> _actions = new List<Action>();

        public Vector3 localPosition
        {
            get => _localPosition;
            set => _localPosition = value;
        }
        public List<Action> actions => _actions;

        public Point(Vector3 localPosition)
        {
            _localPosition = localPosition;
        }
    }

    // 隊列(先頭に付いてくる人)
    [Serializable]
    public class Formation
    {
        [SerializeField, Min(0)] private int _followerCount;
        [SerializeField] private CSE_VillainFormationShape _shape = CSE_VillainFormationShape.Column;
        [SerializeField, Min(0.5f)] private float _spacing = 1.5f;   // 並ぶ間隔(m)

        public int followerCount => _followerCount;
        public CSE_VillainFormationShape shape => _shape;
        public float spacing => _spacing;
    }

    // 悪人(先頭)1人分のルート
    [Serializable]
    public class Route
    {
        [SerializeField] private Color _color = Color.red;
        [SerializeField] private CSE_VillainPatrolLoopMode _loopMode = CSE_VillainPatrolLoopMode.Loop;
        [SerializeField, Min(minSpeed)] private float _speed = 1.5f;   // 巡回の速さ(m/秒)
        [SerializeField] private float _startAngle;                      // 湧いた時の向き(スポーン位置の向きからの角度、＋で右回り)
        [SerializeField] private List<Point> _points = new List<Point>();
        [SerializeField] private Formation _formation = new Formation();

        public Color color => _color;
        public CSE_VillainPatrolLoopMode loopMode => _loopMode;
        public float speed => Mathf.Max(minSpeed, _speed);
        public float startAngle => _startAngle;
        public List<Point> points => _points;
        public Formation formation => _formation;

        public Route(Color color, Vector3 startLocalPosition)
        {
            _color = color;
            _points.Add(new Point(startLocalPosition));
        }

        // 1周で回るポイントの順番(ポイント1から。周回: 0,1,…,n-1 / 往復: 0,1,…,n-1,n-2,…,1)
        // 1周回ったら、また先頭から繰り返す
        public List<int> GetOrder()
        {
            List<int> order = new List<int>();
            int count = _points.Count;
            for (int i = 0; i < count; i++) order.Add(i);
            if (_loopMode == CSE_VillainPatrolLoopMode.PingPong)
            {
                for (int i = count - 2; i >= 1; i--) order.Add(i);
            }
            return order;
        }
    }

    [SerializeField] private List<Route> _routes = new List<Route>();

    public List<Route> routes => _routes;

    // このパターンで湧く人数の上限(先頭 + 付いてくる人)
    public int slotCount
    {
        get
        {
            int count = 0;
            foreach (Route route in _routes)
            {
                if (route.points.Count == 0) continue;
                count += 1 + route.formation.followerCount;
            }
            return count;
        }
    }

    // index番目(0始まり)に湧いた人の役割。先頭を全員先に、次に付いてくる人をルートの順に埋める
    // followerIndex: 隊列の何番目か(0始まり)。先頭なら-1。範囲外(slotCount以上)ならfalse
    public bool GetSlot(int index, out int routeIndex, out int followerIndex)
    {
        routeIndex = -1;
        followerIndex = -1;
        if (index < 0) return false;

        int remaining = index;
        for (int i = 0; i < _routes.Count; i++)
        {
            if (_routes[i].points.Count == 0) continue;
            if (remaining == 0)
            {
                routeIndex = i;
                return true;
            }
            remaining--;
        }

        for (int i = 0; i < _routes.Count; i++)
        {
            if (_routes[i].points.Count == 0) continue;
            int followers = _routes[i].formation.followerCount;
            if (remaining < followers)
            {
                routeIndex = i;
                followerIndex = remaining;
                return true;
            }
            remaining -= followers;
        }
        return false;
    }

    // 隊列のfollowerIndex番目(0始まり)の人が並ぶ位置(先頭から見た相対位置。x: 右 / z: 前)
    public static Vector3 GetFormationOffset(CSE_VillainFormationShape shape, int followerIndex, float spacing)
    {
        int number = followerIndex + 1;            // 先頭から何人目か
        int pair = (number + 1) / 2;               // 左右に並ぶ形で、何列目か(1, 1, 2, 2, …)
        float side = number % 2 == 1 ? 1f : -1f;   // 右(+)・左(-)の順に置く

        switch (shape)
        {
            case CSE_VillainFormationShape.Line:          return new Vector3(side * pair * spacing, 0f, 0f);
            case CSE_VillainFormationShape.DiagonalRight: return new Vector3(number * spacing, 0f, -number * spacing);
            case CSE_VillainFormationShape.DiagonalLeft:  return new Vector3(-number * spacing, 0f, -number * spacing);
            case CSE_VillainFormationShape.Wedge:         return new Vector3(side * pair * spacing, 0f, -pair * spacing);
            default:                                      return new Vector3(0f, 0f, -number * spacing);   // 縦一列
        }
    }
}
