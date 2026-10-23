/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-23 | 初回作成
 * ================================================ */

using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 【仮モデル。今後削除する】警察の仮モデルの色付けと、警戒状態の時に頭の上のパトランプを光らせるクラス
/// ・仮モデル(警察のプレハブの子オブジェクト「TestModel」)の各部分に、色を付ける
/// ・警戒状態(追跡・駆け付け・探索中)の間は、頭の上のパトランプが、赤と青で交互に点灯する。警戒状態でなければ消灯
/// ・警戒状態かの判断はサーバー(またはオフライン)だけが分かる(CS_PoliceBrainがサーバーでしか動かないため)
///   サーバーがNetworkVariableで全員へ同期し、各クライアントが自分の画面でパトランプを点灯させる
/// ・元のカプセルの見た目は消す(当たり判定のコライダーは、そのまま使う)
/// 削除する時は、このフォルダ(TestModel)と、警察のプレハブのTestModelの子オブジェクト・このコンポーネントを消す
/// </summary>
public class CS_PoliceTestModel : NetworkBehaviour
{
    /// <summary>
    /// 仮モデルの1つの部分と、その色
    /// </summary>
    [Serializable]
    private class Part
    {
        [SerializeField] private Renderer _renderer = null;
        [SerializeField] private Color _color = Color.white;

        public Renderer renderer => _renderer;
        public Color color => _color;
    }

    // マテリアルの色のプロパティ(HDRP/Litの基本色と、HDRP/Unlitの色)
    private static readonly int _baseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int _unlitColorId = Shader.PropertyToID("_UnlitColor");

    // 警戒状態か(書き込みはサーバーのみ。パトランプを全員の画面で点灯させるために同期する)
    private readonly NetworkVariable<bool> _isAlert = new NetworkVariable<bool>();

    private CS_PoliceBrain _brain = null;
    private MaterialPropertyBlock _propertyBlock = null;

    [Header("＝＝＝ 仮モデル ＝＝＝")]
    [SerializeField]
    [Tooltip("色を付ける仮モデルの部分(体・頭・帽子など)")]
    private Part[] _parts = new Part[0];

    [SerializeField]
    [Tooltip("非表示にする元の見た目(カプセル)")]
    private Renderer _hiddenRenderer = null;

    [Header("＝＝＝ パトランプ ＝＝＝")]
    [SerializeField]
    [Tooltip("赤いランプ")]
    private Renderer _redLamp = null;

    [SerializeField]
    [Tooltip("青いランプ")]
    private Renderer _blueLamp = null;

    [SerializeField]
    [ColorUsage(false, true)]
    [Tooltip("赤いランプの色(明るさも含む)")]
    private Color _redColor = new Color(3.0f, 0.1f, 0.1f);

    [SerializeField]
    [ColorUsage(false, true)]
    [Tooltip("青いランプの色(明るさも含む)")]
    private Color _blueColor = new Color(0.1f, 0.4f, 3.0f);

    [SerializeField, Min(0.05f)]
    [Tooltip("赤と青が入れ替わる間隔(秒)")]
    private float _blinkInterval = 0.25f;

    private void Awake()
    {
        _brain = GetComponent<CS_PoliceBrain>();
        _propertyBlock = new MaterialPropertyBlock();

        if (_hiddenRenderer != null) _hiddenRenderer.enabled = false;
        SetLamps(false, false);
    }

    private void Update()
    {
        // 警戒状態かを、サーバー(またはオフライン)が調べる。クライアントは同期された値を使う
        bool isAlert = IsAlertNow();
        if (IsSpawned && IsServer) _isAlert.Value = isAlert;
        if (IsSpawned) isAlert = _isAlert.Value;

        UpdateLamps(isAlert);
    }

    // デバッグ表示(CS_PoliceChargeDebugVisual)が見た目の色を消す・変えるので、それが終わっている部分だけ、色を付け直す
    private void LateUpdate()
    {
        foreach (Part part in _parts)
        {
            if (part.renderer != null && !part.renderer.HasPropertyBlock()) SetColor(part.renderer, _baseColorId, part.color);
        }
        RefreshLampColor(_redLamp, _redColor);
        RefreshLampColor(_blueLamp, _blueColor);
    }

    /// <summary>
    /// 今、警戒状態かを調べるメソッド(追跡・駆け付け・探索中を、警戒状態とする。サーバー、またはオフラインでだけ正しい)
    /// </summary>
    private bool IsAlertNow()
    {
        if (_brain == null || CS_PoliceSquad.IsNetworkClientOnly()) return false;

        CSE_PoliceMoveState state = _brain.state;
        return state == CSE_PoliceMoveState.Chase || state == CSE_PoliceMoveState.Rush || state == CSE_PoliceMoveState.Search;
    }

    /// <summary>
    /// パトランプを点灯させるメソッド(警戒状態の間は、赤と青が交互に点灯する。警戒状態でなければ消灯)
    /// </summary>
    private void UpdateLamps(bool isAlert)
    {
        if (!isAlert)
        {
            SetLamps(false, false);
            return;
        }

        bool isRedPhase = Mathf.FloorToInt(Time.time / _blinkInterval) % 2 == 0;
        SetLamps(isRedPhase, !isRedPhase);
    }

    private void SetLamps(bool isRedOn, bool isBlueOn)
    {
        if (_redLamp != null) _redLamp.enabled = isRedOn;
        if (_blueLamp != null) _blueLamp.enabled = isBlueOn;
    }

    /// <summary>
    /// ランプの色を、見た目の色が消されていたら付け直すメソッド
    /// </summary>
    private void RefreshLampColor(Renderer lamp, Color color)
    {
        if (lamp != null && !lamp.HasPropertyBlock()) SetColor(lamp, _unlitColorId, color);
    }

    /// <summary>
    /// 見た目の色を、MaterialPropertyBlockで上書きするメソッド(マテリアル自体は書き換えない)
    /// </summary>
    private void SetColor(Renderer target, int propertyId, Color color)
    {
        target.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor(propertyId, color);
        target.SetPropertyBlock(_propertyBlock);
    }
}
