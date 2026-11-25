using UnityEngine;

/*
 * ソロで、エリアが決着したら、AIガイドがプレイヤーを真ん中のタワー(AEGISタワー)の上へ運ぶクラス
 * 仕様はConfluence「ゲームループ」(エリアクリア後は一度タワー上にAIガイドの手で戻されます)・「名称求(AIガイド)」(役割・活躍シーン)。Issue #538
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・ソロだけで動く(エリアの進行CS_SoloAreaProgressionはソロのフィールドにしか無い)。自分のプレイヤー(isLocalPlayer)だけを運ぶ
 *   位置は自分の手元で動かし、NetworkTransform(Owner)が他の人へ伝える(ソロなので他の人はいないが、作りは移動と同じ)
 * ・運ぶ時: エリアが決着して(守れた・守れなかったのどちらも)、次のエリアがある時(CS_SoloAreaProgression.onAreaChanged)
 *   最後のエリアの後は、そのままリザルトへ進むので運ばない
 *   _carryOnlyWhenCleared がオンなら、守れたエリアの後だけ運ぶ
 * ・運ぶ先(タワーの上)
 *   _towerTopPointが設定されていればその位置。空なら、エリア分け(CS_CrimeAreaLayout)の中心(= AEGISタワー)の真上から
 *   下へ地面を調べ、当たった所(タワーの屋上)を運ぶ先にする。どちらも無ければ運ばない
 * ・運び方(仮): AIガイドの本体がまだ無いので、プレイヤーが弧を描いてタワーの上へ飛んでいく
 *   高さは、出発と到着の高い方より_arcHeight(m)上を通る。時間は距離 ÷ _carrySpeed を _minDuration〜_maxDuration に収める
 *   AIガイドの本体ができたら、運んでいる間にその本体をプレイヤーの上に付ける(Issue #538の依存)
 * ・運んでいる間(仕様に書かれていないので、決めた内容)
 *   操作できない(CS_Player.SetCarried。canActがfalseになるので、攻撃・スキャンなどもしない)
 *   無敵(CS_PlayerHealth.SetInvincible)。警察に見つからない(CS_PlayerCharacterが、変身中のタグを外す。isCarryingを見る)
 *   Rigidbodyはキネマティックにして、MovePositionで動かす(補間が効いてなめらかに動く)
 * ・着いたら、キネマティックを戻し、速度0で操作できるようにする。無敵もすぐ切る
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
public class CS_PlayerTowerCarry : MonoBehaviour
{
    [Header("運ぶ先")]
    [SerializeField] private Transform _towerTopPoint;                    // 運ぶ先(空ならタワーの中心の真上から屋上を探す)
    [SerializeField] private LayerMask _groundLayers = ~0;                // 屋上を探す時に地面とみなすレイヤー
    [SerializeField][Min(10f)] private float _searchHeight = 500f;        // 屋上を探す時、タワーの中心のどれだけ上から下へ調べるか(m)

    [Header("いつ運ぶか")]
    [SerializeField] private bool _carryOnlyWhenCleared = false;         // オンなら守れたエリアの後だけ運ぶ(オフなら守れなかった時も運ぶ)
    [SerializeField][Min(0f)] private float _startDelay = 1f;            // エリアが決着してから運び始めるまでの時間(秒。AIガイドのセリフを先に見せるため)

    [Header("運び方")]
    [SerializeField][Min(0f)] private float _arcHeight = 15f;            // 出発と到着の高い方より、どれだけ上を通るか(m)
    [SerializeField][Min(1f)] private float _carrySpeed = 40f;           // 運ぶ速さの目安(m/秒)
    [SerializeField][Min(0.1f)] private float _minDuration = 1.5f;       // 運ぶ時間の最短(秒)
    [SerializeField][Min(0.1f)] private float _maxDuration = 3f;         // 運ぶ時間の最長(秒)
    [SerializeField][Min(0f)] private float _landOffset = 0.1f;          // 屋上からどれだけ浮かせて降ろすか(m。地面にめり込まないように)

    private CS_Player _player;
    private CS_PlayerHealth _health;
    private CS_PlayerHide _hide;
    private Rigidbody _rigidbody;
    private Collider _collider;
    private CS_SoloAreaProgression _progression;

    private bool _isCarrying;
    private float _waitRemaining = -1f;     // 運び始めるまでの残り時間(待っていなければ負)
    private float _elapsed;
    private float _duration;
    private Vector3 _start;
    private Vector3 _control;
    private Vector3 _end;
    private bool _wasKinematic;
    private bool _lastAreaCleared;          // 直前に決着したエリアを守れたか(onAreaClearedはonAreaChangedより先に呼ばれる)

    public bool isCarrying => _isCarrying;   // 運んでいる間か(待っている間は含まない)

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _health = GetComponent<CS_PlayerHealth>();
        _hide = GetComponent<CS_PlayerHide>();
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();
    }

    private void OnDisable()
    {
        Unsubscribe();
        if (_isCarrying) FinishCarry();
    }

    private void Update()
    {
        // エリアの進行はフィールドと一緒に後から出てくることがあるので、見つかるまで探す
        if (_progression == null) Subscribe();

        if (_waitRemaining >= 0f)
        {
            _waitRemaining -= Time.deltaTime;
            if (_waitRemaining < 0f) StartCarry();
        }
    }

    private void FixedUpdate()
    {
        if (!_isCarrying) return;

        _elapsed += Time.fixedDeltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);
        _rigidbody.MovePosition(GetArcPoint(Mathf.SmoothStep(0f, 1f, t)));

        if (t >= 1f) FinishCarry();
    }

    private void Subscribe()
    {
        _progression = CS_SoloAreaProgression.instance;
        if (_progression != null) _progression.onAreaChanged += HandleAreaChanged;
        if (_progression != null) _progression.onAreaCleared += HandleAreaCleared;
    }

    private void Unsubscribe()
    {
        if (_progression == null) return;

        _progression.onAreaChanged -= HandleAreaChanged;
        _progression.onAreaCleared -= HandleAreaCleared;
        _progression = null;
    }

    private void HandleAreaCleared(CS_CrimeArea area)
    {
        _lastAreaCleared = true;
    }

    // エリアが決着して、次のエリアが始まった時(nextがnullなら全エリアが決着した)
    private void HandleAreaChanged(CS_CrimeArea next)
    {
        bool wasCleared = _lastAreaCleared;
        _lastAreaCleared = false;

        if (next == null) return;                              // 最後のエリアの後はリザルトへ進むので運ばない
        if (_carryOnlyWhenCleared && !wasCleared) return;
        if (!_player.isLocalPlayer || _isCarrying) return;

        _waitRemaining = _startDelay;
    }

    // ========================================
    // 運ぶ
    // ========================================

    private void StartCarry()
    {
        if (!TryGetTowerTop(out Vector3 towerTop)) return;
        if (_health != null && _health.isDead) return;   // 倒れている間は運ばない(復活はその場で行う)

        if (_hide != null) _hide.ExitHide();   // SAで飛んでいる途中ならやめる

        _start = _rigidbody.position;
        _end = towerTop + Vector3.up * (GetFootOffset() + _landOffset);
        float topY = Mathf.Max(_start.y, _end.y) + _arcHeight;
        _control = new Vector3((_start.x + _end.x) * 0.5f, topY, (_start.z + _end.z) * 0.5f);

        float distance = Vector3.Distance(_start, _control) + Vector3.Distance(_control, _end);
        _duration = Mathf.Clamp(distance / _carrySpeed, _minDuration, Mathf.Max(_minDuration, _maxDuration));
        _elapsed = 0f;

        _player.SetCarried(true);
        if (_health != null) _health.SetInvincible(this, true);
        _rigidbody.linearVelocity = Vector3.zero;
        _wasKinematic = _rigidbody.isKinematic;
        _rigidbody.isKinematic = true;
        _isCarrying = true;
    }

    private void FinishCarry()
    {
        _isCarrying = false;
        _rigidbody.isKinematic = _wasKinematic;
        if (!_rigidbody.isKinematic) _rigidbody.linearVelocity = Vector3.zero;
        if (_health != null) _health.SetInvincible(this, false);
        _player.SetCarried(false);
    }

    // 2次ベジェ曲線(出発 → 高い所 → タワーの上)の点
    private Vector3 GetArcPoint(float t)
    {
        float u = 1f - t;
        return u * u * _start + 2f * u * t * _control + t * t * _end;
    }

    // 運ぶ先(タワーの屋上)。_towerTopPointが無ければ、エリア分けの中心の真上から下へ調べる
    private bool TryGetTowerTop(out Vector3 position)
    {
        if (_towerTopPoint != null)
        {
            position = _towerTopPoint.position;
            return true;
        }

        CS_CrimeAreaLayout layout = FindAnyObjectByType<CS_CrimeAreaLayout>();
        if (layout == null)
        {
            position = Vector3.zero;
            return false;
        }

        Vector3 origin = layout.transform.position + Vector3.up * _searchHeight;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, _searchHeight * 2f, _groundLayers, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        position = layout.transform.position;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform)) continue;   // 自分の当たり判定は除く
            if (hit.distance >= nearest) continue;

            nearest = hit.distance;
            position = hit.point;
        }

        if (nearest == float.MaxValue) Debug.LogWarning("CS_PlayerTowerCarry: タワーの屋上が見つかりませんでした。Tower Top Pointを設定してください", this);
        return nearest != float.MaxValue;
    }

    // プレイヤーの位置(Rigidbodyの位置)から足元までの高さ
    private float GetFootOffset()
    {
        return _collider != null ? _rigidbody.position.y - _collider.bounds.min.y : 0f;
    }

    private void OnValidate()
    {
        _maxDuration = Mathf.Max(_minDuration, _maxDuration);
    }
}
