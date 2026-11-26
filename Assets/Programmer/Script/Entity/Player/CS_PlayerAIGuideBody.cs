using UnityEngine;

/*
 * AIガイド(フクロウ型のアシストAI)の体を、自分のプレイヤーのそばに浮かべて付いてこさせるクラス
 * 仕様はConfluence「名称求(AIガイド)」: 常にプレイヤーの周りにおり、浮いている。画面の邪魔にならないように小さめ
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・自分のプレイヤー(人が操作している、isLocalPlayer)の画面にだけ出す。体は自分の手元で生成し、通信はしない
 *   (会話もCS_PlayerAIGuideが自分の画面だけに出しているので、それに合わせる)
 * ・体のプレハブは_bodyPrefab(OwlAssistAI.prefab。モデルと、羽ばたき(Fly_Loop)を流すAnimator)。大きさは_scale倍にする
 *   モデル(FBX)の正面はUnityの-Z向きだったので、プレハブの中でモデルをY軸で180度回して、+Z(プレハブの正面)を向かせている
 * ・付く位置: カメラの左右の向き(CS_Player.viewYaw)から見て、プレイヤーの右上の少し後ろ(_offset)
 *   カメラが回ってもいつも画面の同じあたり(プレイヤーの右上)に見え、プレイヤーと重ならない
 *   位置は_followTime秒遅れてなめらかに付いてくる。さらに_bobHeight(m)・_bobSpeed(回/秒)でふわふわ上下する
 *   プレイヤーから_teleportDistance(m)以上離れた時(復活・ワープなど)は、すぐそばへ移す
 * ・向き: プレイヤーの体が向いている向き(カメラの向きではない)へ、なめらかに回る。位置は上の通りカメラ基準(画面の右上)
 * ・タワーへ運ぶ間(CS_PlayerTowerCarry.isCarrying)は、プレイヤーの頭の上(_carryOffset)へ来て、運んでいるように見せる
 * ・倒れている間も付いてくる
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
public class CS_PlayerAIGuideBody : MonoBehaviour
{
    [Header("体")]
    [SerializeField] private GameObject _bodyPrefab;                         // AIガイドの体(OwlAssistAI.prefab。空なら出さない)
    [SerializeField][Min(0.01f)] private float _scale = 0.35f;              // 大きさ(倍。モデルは高さ約1m)

    [Header("付く位置")]
    [SerializeField] private Vector3 _offset = new Vector3(0.45f, 1.5f, 0f);    // カメラの向きから見た、プレイヤーの足元からの位置(右・上・前、m)
    [SerializeField][Min(0f)] private float _followTime = 0.25f;            // 付いてくる遅れ(秒。0でぴったり)
    [SerializeField][Min(1f)] private float _teleportDistance = 15f;        // これ以上離れたら、すぐそばへ移す(m)
    [SerializeField][Min(0f)] private float _bobHeight = 0.08f;             // ふわふわ上下する高さ(m)
    [SerializeField][Min(0f)] private float _bobSpeed = 0.8f;               // ふわふわ上下する速さ(回/秒)
    [SerializeField][Min(0f)] private float _turnSpeed = 360f;              // 向きを変える速さ(度/秒)

    [Header("タワーへ運ぶ間")]
    [SerializeField] private Vector3 _carryOffset = new Vector3(0f, 2.1f, 0f);   // プレイヤーの足元からの位置(m)

    private CS_Player _player;
    private CS_PlayerTowerCarry _towerCarry;
    private Transform _body;
    private Vector3 _velocity;
    private float _bobTime;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _towerCarry = GetComponent<CS_PlayerTowerCarry>();
    }

    private void OnDestroy()
    {
        if (_body != null) Destroy(_body.gameObject);
    }

    private void LateUpdate()
    {
        if (_bodyPrefab == null) return;

        if (!_player.isLocalPlayer)
        {
            if (_body != null) _body.gameObject.SetActive(false);
            return;
        }

        if (_body == null) CreateBody();
        else if (!_body.gameObject.activeSelf) _body.gameObject.SetActive(true);

        Follow(Time.deltaTime);
    }

    private void CreateBody()
    {
        _body = Instantiate(_bodyPrefab).transform;
        _body.name = "AIGuideBody";
        _body.localScale = Vector3.one * _scale;
        _body.SetPositionAndRotation(GetTargetPosition(), GetTargetRotation());
        _velocity = Vector3.zero;
    }

    // 付く位置へなめらかに動き、ふわふわ上下し、カメラと同じ向きへ回る
    private void Follow(float deltaTime)
    {
        Vector3 target = GetTargetPosition();
        Vector3 current = _body.position;
        if ((target - current).sqrMagnitude > _teleportDistance * _teleportDistance)
        {
            current = target;
            _velocity = Vector3.zero;
        }
        else if (_followTime > 0f)
        {
            bool isCarrying = _towerCarry != null && _towerCarry.isCarrying;   // 運ぶ間はプレイヤーが速く動くので、遅れを小さくする
            current = Vector3.SmoothDamp(current, target, ref _velocity, isCarrying ? _followTime * 0.2f : _followTime);
        }
        else
        {
            current = target;
        }

        _bobTime += deltaTime * _bobSpeed;
        Vector3 bob = Vector3.up * (Mathf.Sin(_bobTime * Mathf.PI * 2f) * _bobHeight);
        _body.position = current;
        _body.GetChild(0).localPosition = bob / Mathf.Max(0.01f, _scale);   // ふわふわは子(モデル)だけに付け、付いてくる動きと分ける

        _body.rotation = Quaternion.RotateTowards(_body.rotation, GetTargetRotation(), _turnSpeed * deltaTime);
    }

    private Vector3 GetTargetPosition()
    {
        Vector3 feet = transform.position;
        if (_towerCarry != null && _towerCarry.isCarrying) return feet + _carryOffset;

        return feet + Quaternion.Euler(0f, _player.viewYaw, 0f) * _offset;
    }

    // プレイヤーの体が向いている向き(カメラの向きではない)
    private Quaternion GetTargetRotation()
    {
        return Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
    }
}
