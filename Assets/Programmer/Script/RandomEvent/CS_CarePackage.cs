using System;
using Unity.Netcode;
using UnityEngine;

/*
 * ランダムイベント「ケアパッケージ」の箱
 * プレイヤーが近くでボタンを長押しし続けると開封され、イベント側(CSO_RandomEventCarePackage)に開けた人を知らせる
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・IInteractableを実装し、プレイヤー側(CS_PlayerInteractor)から開封の開始・終了を受け取る
 * ・開封(サーバー、またはオフラインで管理)
 *   開けられるのは、先に開け始めた1人だけ(その人が開けている間、他のプレイヤーは開けられない)
 *   次のどれかで中断し、進み具合は0に戻る
 *     ボタンを離した / 範囲(interactRange)の外に出た / ダメージを受けた / 倒れた
 *   openTime 秒続けたら開封し、Setupで渡されたonOpenedを開けた人を付けて呼んでから、箱を消す
 * ・進み具合・開けている人・開封済みかはNetworkVariableで全員に同期する
 *   開けている間だけ、箱の上のゲージ(VillainCrimeGaugeCanvasを使い回し)に進み具合を表示する
 * ・プレハブはNetworkPrefabsList(DefaultNetworkPrefabs)に登録しておくこと
 */
// ========================================

public class CS_CarePackage : NetworkBehaviour, IInteractable
{
    private const ulong _noHolder = ulong.MaxValue;

    [SerializeField, Min(0f)]
    [Tooltip("開封できる範囲(m、水平方向)。開けている人がこの外に出ると中断する")]
    private float _interactRange = 2.5f;

    [SerializeField]
    [Tooltip("開封の進み具合を表示するゲージ(直下に置いたVillainCrimeGaugeCanvas)")]
    private GameObject _gaugeCanvas;

    // 書き込みはサーバーのみ(NetworkVariableのデフォルト)
    private readonly NetworkVariable<float> _progress = new NetworkVariable<float>();           // 開封の進み具合(0〜1)
    private readonly NetworkVariable<ulong> _holderId = new NetworkVariable<ulong>(_noHolder);  // 開けている人のOwnerClientId
    private readonly NetworkVariable<bool> _isOpened = new NetworkVariable<bool>();

    private float _openTime = 10f;
    private Action<GameObject> _onOpened;
    private GameObject _holder;               // 開けている人(サーバーのみ)
    private CS_PlayerHealth _holderHealth;
    private float _holderLastHp;              // ダメージを受けたかの判定に使う
    private float _elapsed;
    private CS_UICrimeGaugeModel _gaugeModel;

    public Vector3 interactPosition => transform.position;
    public float interactRange => _interactRange;

    // このマシンが開封を管理する権威を持つか(オフライン、またはサーバー)
    private bool hasAuthority => !IsSpawned || IsServer;

    private void Awake()
    {
        _gaugeModel = new CS_UICrimeGaugeModel(1f);
        _gaugeModel.Bind(transform);
    }

    private void OnEnable()
    {
        CS_InteractableRegistry.Register(this);
    }

    private void OnDisable()
    {
        CS_InteractableRegistry.Unregister(this);
    }

    public override void OnDestroy()
    {
        _gaugeModel?.Dispose();
        base.OnDestroy();
    }

    // 生成直後に呼ぶ(サーバー、またはオフライン)。開封にかかる時間と、開封された時の処理を渡す
    public void Setup(float openTime, Action<GameObject> onOpened)
    {
        _openTime = Mathf.Max(0f, openTime);
        _onOpened = onOpened;
    }

    // ---- IInteractable ----

    public bool CanInteract(GameObject interactor)
    {
        if (_isOpened.Value) return false;

        ulong holderId = _holderId.Value;
        return holderId == _noHolder || holderId == GetClientId(interactor);
    }

    public bool IsInteracting(GameObject interactor)
    {
        return !_isOpened.Value && _holderId.Value != _noHolder && _holderId.Value == GetClientId(interactor);
    }

    public void BeginInteract(GameObject interactor)
    {
        if (!hasAuthority || _isOpened.Value) return;
        if (_holder != null) return;   // 先に開け始めた人がいる

        CS_PlayerHealth health = interactor.GetComponent<CS_PlayerHealth>();
        if (health == null || health.isDead) return;
        if (!IsInRange(interactor)) return;

        _holder = interactor;
        _holderHealth = health;
        _holderLastHp = health.currentHp;
        _elapsed = 0f;
        _holderId.Value = GetClientId(interactor);
        _progress.Value = 0f;
    }

    public void EndInteract(GameObject interactor)
    {
        if (!hasAuthority || interactor != _holder) return;

        Interrupt();
    }

    // ---- 開封の進行 ----

    private void Update()
    {
        if (hasAuthority && _holder != null) UpdateOpening(Time.deltaTime);

        UpdateGauge();
    }

    private void UpdateOpening(float deltaTime)
    {
        if (ShouldInterrupt())
        {
            Interrupt();
            return;
        }

        _elapsed += deltaTime;
        _progress.Value = _openTime > 0f ? Mathf.Clamp01(_elapsed / _openTime) : 1f;
        if (_progress.Value >= 1f) Open();
    }

    // 開けている人が倒れた・ダメージを受けた・範囲の外に出た(Destroyされた場合も)
    private bool ShouldInterrupt()
    {
        if (_holder == null || _holderHealth == null || _holderHealth.isDead) return true;
        if (!IsInRange(_holder)) return true;

        bool isDamaged = _holderHealth.currentHp < _holderLastHp;
        _holderLastHp = _holderHealth.currentHp;
        return isDamaged;
    }

    private void Interrupt()
    {
        _holder = null;
        _holderHealth = null;
        _elapsed = 0f;
        _holderId.Value = _noHolder;
        _progress.Value = 0f;
    }

    // 開封して、開けた人をイベント側に知らせてから箱を消す
    private void Open()
    {
        GameObject opener = _holder;
        _isOpened.Value = true;
        _holder = null;
        _holderHealth = null;
        _holderId.Value = _noHolder;

        _onOpened?.Invoke(opener);

        if (IsSpawned)
        {
            NetworkObject.Despawn();
            return;
        }
        Destroy(gameObject);
    }

    // ---- 表示(全クライアント) ----

    // 誰かが開けている間だけ、ゲージに進み具合を出してカメラの方へ向ける
    private void UpdateGauge()
    {
        _gaugeModel.SetValue(_progress.Value);
        if (_gaugeCanvas == null) return;

        bool isVisible = !_isOpened.Value && _holderId.Value != _noHolder;
        if (_gaugeCanvas.activeSelf != isVisible) _gaugeCanvas.SetActive(isVisible);
        if (!isVisible) return;

        Camera mainCamera = Camera.main;
        if (mainCamera != null) _gaugeCanvas.transform.rotation = mainCamera.transform.rotation;
    }

    // ---- 補助 ----

    private bool IsInRange(GameObject interactor)
    {
        Vector3 offset = interactor.transform.position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= _interactRange * _interactRange;
    }

    // プレイヤーを区別する番号(オフラインでは全員0になるが、プレイヤーは1人なので問題ない)
    private static ulong GetClientId(GameObject interactor)
    {
        NetworkObject networkObject = interactor != null ? interactor.GetComponent<NetworkObject>() : null;
        return networkObject != null ? networkObject.OwnerClientId : 0;
    }
}
