/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-09-24 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>
/// 警察・悪人・プレイヤーの攻撃判定や視界を、Game画面に線で表示するデバッグ用クラス
/// </summary>
// ========================================
/*
 * メモ
 * ・シーンに1つ置き、Line Materialに MT_DebugRange を割り当てて使う
 * ・Toggle Key(初期値F3)で表示のON/OFFを切り替える
 * ・線はLineRendererを使い回して描く(毎フレーム、必要な本数だけ有効にする)
 * ・表示するもの
 *   警察  : 視界(扇形)、背後でも気付く距離、攻撃範囲。視界の色は行動状態で変わる
 *   悪人  : 攻撃判定、臨戦態勢範囲、反撃の索敵範囲、スポーン位置からの追跡限界
 *   プレイヤー : 各段の攻撃判定、必殺技の判定
 *   攻撃判定(球)は、攻撃中だけ明るい色の球(3方向の円)で表示する
 * ・判定は権威を持つマシン(サーバー/ホスト)で行われるため、
 *   クライアントでは他人の攻撃中表示や警察(ホストにしかいない)が出ないことがある
 */
// ========================================
public class CS_DebugRangeDrawer : MonoBehaviour
{
    // 円を何分割して描くか
    private const int _circleSegments = 48;

    // 地面の範囲を足元から少し浮かせる高さ(地面に埋もれないように)
    private const float _groundOffset = 0.05f;

    // HDRP/Unlitの色のプロパティ
    private static readonly int _unlitColorId = Shader.PropertyToID("_UnlitColor");

    // 使い回すLineRenderer
    private readonly List<LineRenderer> _lines = new List<LineRenderer>();

    // 今のフレームで使ったLineRendererの本数
    private int _usedLineCount = 0;

    // 線の色を変えるためのプロパティブロック
    private MaterialPropertyBlock _propertyBlock = null;

    // 円や扇形の頂点を作る際に使い回すバッファ
    private readonly List<Vector3> _points = new List<Vector3>();

    [Header("＝＝＝ 表示 ＝＝＝")]
    [SerializeField]
    [Tooltip("線のマテリアル(HDRP/Unlit)")]
    private Material _lineMaterial = null;

    [SerializeField, Min(0.001f)]
    [Tooltip("線の太さ")]
    private float _lineWidth = 0.06f;

    [SerializeField]
    [Tooltip("表示するか")]
    private bool _isVisible = true;

    [SerializeField]
    [Tooltip("表示のON/OFFを切り替えるキー")]
    private Key _toggleKey = Key.F3;

    [SerializeField]
    private bool _showPolice = true;

    [SerializeField]
    private bool _showVillain = true;

    [SerializeField]
    private bool _showPlayer = true;

    [Header("＝＝＝ 色(警察) ＝＝＝")]
    [SerializeField]
    [Tooltip("巡回中の視界")]
    private Color _policeViewPatrolColor = new Color(0.3f, 0.8f, 1.0f);

    [SerializeField]
    [Tooltip("駆け付け・探索中の視界")]
    private Color _policeViewAlertColor = new Color(1.0f, 0.8f, 0.2f);

    [SerializeField]
    [Tooltip("追跡中の視界")]
    private Color _policeViewChaseColor = new Color(1.0f, 0.3f, 0.3f);

    [SerializeField]
    [Tooltip("背後でも気付く距離")]
    private Color _policeNoticeColor = new Color(0.6f, 0.6f, 1.0f);

    [SerializeField]
    [Tooltip("攻撃範囲")]
    private Color _policeAttackColor = new Color(1.0f, 0.2f, 0.2f);

    [Header("＝＝＝ 色(悪人) ＝＝＝")]
    [SerializeField]
    [Tooltip("臨戦態勢範囲")]
    private Color _villainEngageColor = new Color(1.0f, 0.5f, 0.1f);

    [SerializeField]
    [Tooltip("反撃の索敵範囲")]
    private Color _villainCounterColor = new Color(0.7f, 0.3f, 1.0f);

    [SerializeField]
    [Tooltip("スポーン位置からの追跡限界")]
    private Color _villainLeashColor = new Color(0.5f, 0.5f, 0.5f);

    [Header("＝＝＝ 色(攻撃判定) ＝＝＝")]
    [SerializeField]
    [Tooltip("攻撃していない時の判定")]
    private Color _hitIdleColor = new Color(0.8f, 0.2f, 0.2f);

    [SerializeField]
    [Tooltip("攻撃中の判定")]
    private Color _hitActiveColor = new Color(1.0f, 1.0f, 0.2f);

    [SerializeField]
    [Tooltip("プレイヤーの必殺技の判定")]
    private Color _specialColor = new Color(1.0f, 0.4f, 1.0f);

    private void Awake()
    {
        _propertyBlock = new MaterialPropertyBlock();
    }

    private void LateUpdate()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[_toggleKey].wasPressedThisFrame) _isVisible = !_isVisible;

        _usedLineCount = 0;
        if (_isVisible && _lineMaterial != null)
        {
            if (_showPolice) DrawPolice();
            if (_showVillain) DrawVillains();
            if (_showPlayer) DrawPlayers();
        }

        // 今回使わなかった線は隠す
        for (int i = _usedLineCount; i < _lines.Count; i++)
        {
            if (_lines[i].enabled) _lines[i].enabled = false;
        }
    }

    /// <summary>
    /// 警察の視界と攻撃範囲を描画するメソッド
    /// </summary>
    private void DrawPolice()
    {
        foreach (CS_PoliceBrain brain in FindObjectsByType<CS_PoliceBrain>(FindObjectsSortMode.None))
        {
            Vector3 foot = GetFootPosition(brain);

            if (brain.TryGetComponent(out CS_PoliceVision vision) && vision.viewDistance > 0.0f)
            {
                DrawSector(foot, brain.transform.forward, vision.viewAngle, vision.viewDistance, GetPoliceViewColor(brain.state));
                DrawCircle(foot, vision.noticeDistance, _policeNoticeColor);
            }

            if (brain.TryGetComponent(out CS_PoliceAttack attack))
            {
                DrawCircle(foot, attack.attackRange, _policeAttackColor);
            }
        }
    }

    /// <summary>
    /// 悪人の攻撃判定と索敵範囲を描画するメソッド
    /// </summary>
    private void DrawVillains()
    {
        foreach (CS_VillainCombat combat in FindObjectsByType<CS_VillainCombat>(FindObjectsSortMode.None))
        {
            if (!combat.enabled) continue;

            Vector3 foot = GetFootPosition(combat);
            float footOffset = foot.y - combat.transform.position.y;

            if (combat.TryGetComponent(out CS_VillainStats stats)) DrawCircle(foot, stats.engageRange, _villainEngageColor);
            DrawCircle(foot, combat.counterSearchRange, _villainCounterColor);
            DrawCircle(combat.homePosition + Vector3.up * footOffset, combat.leashRange, _villainLeashColor);
            DrawHitSphere(combat.transform, combat.attackData, combat.isAttacking, _hitIdleColor);
        }
    }

    /// <summary>
    /// プレイヤーの攻撃判定を描画するメソッド
    /// </summary>
    private void DrawPlayers()
    {
        foreach (CS_PlayerAttack attack in FindObjectsByType<CS_PlayerAttack>(FindObjectsSortMode.None))
        {
            if (attack.attackSteps != null)
            {
                for (int i = 0; i < attack.attackSteps.Count; i++)
                {
                    DrawHitSphere(attack.transform, attack.attackSteps[i], attack.currentStep == i, _hitIdleColor);
                }
            }

            if (attack.TryGetComponent(out CS_PlayerSpecialAttack special))
            {
                DrawHitSphere(attack.transform, special.specialAttackData, special.isPerformingSpecial, _specialColor);
            }
        }
    }

    /// <summary>
    /// 警察の行動状態に応じた視界の色を取得するメソッド
    /// </summary>
    /// <param name="state">行動状態</param>
    /// <returns>視界の色</returns>
    private Color GetPoliceViewColor(CSE_PoliceMoveState state)
    {
        switch (state)
        {
            case CSE_PoliceMoveState.Chase: return _policeViewChaseColor;
            case CSE_PoliceMoveState.Rush:
            case CSE_PoliceMoveState.Search: return _policeViewAlertColor;
            default: return _policeViewPatrolColor;
        }
    }

    /// <summary>
    /// 足元の位置を取得するメソッド(コライダーの下端。無ければtransformの位置)
    /// </summary>
    /// <param name="owner">対象</param>
    /// <returns>足元の位置</returns>
    private Vector3 GetFootPosition(Component owner)
    {
        Vector3 position = owner.transform.position;
        if (owner.TryGetComponent(out Collider ownerCollider)) position.y = ownerCollider.bounds.min.y;
        position.y += _groundOffset;
        return position;
    }

    /// <summary>
    /// 攻撃判定の球を描画するメソッド(CS_AttackHitDetectorと同じ位置・大きさ)
    /// 攻撃していない時は水平の円のみ、攻撃中は3方向の円で球を表す
    /// </summary>
    /// <param name="origin">攻撃する側</param>
    /// <param name="data">攻撃データ</param>
    /// <param name="isActive">攻撃中か</param>
    /// <param name="idleColor">攻撃していない時の色</param>
    private void DrawHitSphere(Transform origin, CSO_AttackData data, bool isActive, Color idleColor)
    {
        if (data == null) return;

        Vector3 center = origin.position + origin.forward * data.hitRange;
        if (!isActive)
        {
            DrawCircle(center, data.hitRadius, idleColor);
            return;
        }

        DrawCircle(center, data.hitRadius, _hitActiveColor, Vector3.right, Vector3.forward);
        DrawCircle(center, data.hitRadius, _hitActiveColor, Vector3.right, Vector3.up);
        DrawCircle(center, data.hitRadius, _hitActiveColor, Vector3.forward, Vector3.up);
    }

    /// <summary>
    /// 水平の円を描画するメソッド
    /// </summary>
    private void DrawCircle(Vector3 center, float radius, Color color)
    {
        DrawCircle(center, radius, color, Vector3.right, Vector3.forward);
    }

    /// <summary>
    /// 2つの軸が張る平面上に円を描画するメソッド
    /// </summary>
    /// <param name="center">中心</param>
    /// <param name="radius">半径</param>
    /// <param name="color">色</param>
    /// <param name="axisA">平面の軸1</param>
    /// <param name="axisB">平面の軸2</param>
    private void DrawCircle(Vector3 center, float radius, Color color, Vector3 axisA, Vector3 axisB)
    {
        if (radius <= 0.0f) return;

        _points.Clear();
        for (int i = 0; i < _circleSegments; i++)
        {
            float angle = Mathf.PI * 2.0f * i / _circleSegments;
            _points.Add(center + (axisA * Mathf.Cos(angle) + axisB * Mathf.Sin(angle)) * radius);
        }
        DrawLine(_points, color, true);
    }

    /// <summary>
    /// 水平の扇形を描画するメソッド
    /// </summary>
    /// <param name="center">扇の要</param>
    /// <param name="forward">扇の中心の向き</param>
    /// <param name="angle">扇全体の角度(度)</param>
    /// <param name="radius">半径</param>
    /// <param name="color">色</param>
    private void DrawSector(Vector3 center, Vector3 forward, float angle, float radius, Color color)
    {
        if (radius <= 0.0f) return;
        if (angle >= 360.0f)
        {
            DrawCircle(center, radius, color);
            return;
        }

        forward.y = 0.0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        int segmentCount = Mathf.Max(2, Mathf.CeilToInt(_circleSegments * angle / 360.0f));
        _points.Clear();
        _points.Add(center);
        for (int i = 0; i <= segmentCount; i++)
        {
            float current = Mathf.Lerp(-angle * 0.5f, angle * 0.5f, (float)i / segmentCount);
            _points.Add(center + Quaternion.AngleAxis(current, Vector3.up) * forward * radius);
        }
        DrawLine(_points, color, true);
    }

    /// <summary>
    /// 頂点を結んだ線を描画するメソッド
    /// </summary>
    /// <param name="points">頂点</param>
    /// <param name="color">色</param>
    /// <param name="isLoop">最後の頂点と最初の頂点を結ぶか</param>
    private void DrawLine(List<Vector3> points, Color color, bool isLoop)
    {
        LineRenderer line = GetLine();
        line.loop = isLoop;
        line.widthMultiplier = _lineWidth;
        line.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++) line.SetPosition(i, points[i]);

        line.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor(_unlitColorId, color);
        line.SetPropertyBlock(_propertyBlock);
    }

    /// <summary>
    /// 使っていないLineRendererを取得するメソッド(足りなければ作る)
    /// </summary>
    /// <returns>LineRenderer</returns>
    private LineRenderer GetLine()
    {
        if (_usedLineCount >= _lines.Count)
        {
            GameObject lineObject = new GameObject("DebugRangeLine");
            lineObject.transform.SetParent(transform, false);

            LineRenderer created = lineObject.AddComponent<LineRenderer>();
            created.useWorldSpace = true;
            created.sharedMaterial = _lineMaterial;
            created.shadowCastingMode = ShadowCastingMode.Off;
            created.receiveShadows = false;
            created.numCornerVertices = 2;
            _lines.Add(created);
        }

        LineRenderer line = _lines[_usedLineCount++];
        if (!line.enabled) line.enabled = true;
        return line;
    }
}
