/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-09-25 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 警察のグループの警備エリア内で、プレイヤーが変身したことを検知するクラス
/// 一定間隔でエリア内のプレイヤーの変身状態を調べ、「前回は変身していなかった → 今回は変身している」
/// プレイヤーを見つけたら、その位置を返す(変身した状態でエリアに入ってきた場合は対象外)
/// </summary>
public class CS_PoliceTransformationWatcher
{
    // 変身状態を調べる間隔(秒)
    private const float _checkInterval = 0.2f;

    // 警備エリア内のコライダーを集める際に使い回すバッファ
    private readonly Collider[] _overlapBuffer = new Collider[32];

    // 前回・今回調べた時の、エリア内にいたプレイヤーの変身状態
    private Dictionary<CS_PlayerHealth, bool> _lastStates = new Dictionary<CS_PlayerHealth, bool>();
    private Dictionary<CS_PlayerHealth, bool> _currentStates = new Dictionary<CS_PlayerHealth, bool>();

    // 次に調べるまでの残り時間
    private float _checkTimer = 0.0f;

    /// <summary>
    /// 位置がエリアの内側にあるかを判定するメソッド
    /// </summary>
    /// <param name="area">エリアのコライダー</param>
    /// <param name="position">判定する位置</param>
    /// <returns>内側ならtrue</returns>
    public static bool IsInsideArea(Collider area, Vector3 position)
    {
        // ClosestPointは、位置がコライダーの内側ならその位置をそのまま返す
        return area.ClosestPoint(position) == position;
    }

    /// <summary>
    /// 一定間隔で警備エリア内を調べ、変身したばかりのプレイヤーの位置を集めるメソッド(毎フレーム呼ぶ)
    /// </summary>
    /// <param name="deltaTime">前のフレームからの経過時間</param>
    /// <param name="guardArea">警備エリア</param>
    /// <param name="transformedPositions">変身したばかりのプレイヤーの位置(調べていないフレームは空になる)</param>
    public void Update(float deltaTime, Collider guardArea, List<Vector3> transformedPositions)
    {
        transformedPositions.Clear();

        _checkTimer -= deltaTime;
        if (_checkTimer > 0.0f) return;
        _checkTimer = _checkInterval;

        CollectStates(guardArea, transformedPositions);

        // 今回の状態を、次回の比較に使う「前回の状態」にする
        (_lastStates, _currentStates) = (_currentStates, _lastStates);
    }

    /// <summary>
    /// 警備エリア内のプレイヤーの変身状態を記録し、変身したばかりのプレイヤーの位置を集めるメソッド
    /// </summary>
    /// <param name="guardArea">警備エリア</param>
    /// <param name="transformedPositions">変身したばかりのプレイヤーの位置</param>
    private void CollectStates(Collider guardArea, List<Vector3> transformedPositions)
    {
        _currentStates.Clear();

        // プレイヤーはEntityレイヤーのキャラクターの中から探す
        Bounds bounds = guardArea.bounds;
        int count = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, _overlapBuffer, Quaternion.identity,
            CS_PoliceLayers.entityLayers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            CS_PlayerHealth player = _overlapBuffer[i].GetComponentInParent<CS_PlayerHealth>();
            if (player == null || _currentStates.ContainsKey(player)) continue;

            // Boundsは箱なので、エリアの形によっては外にいることがある
            Vector3 position = player.transform.position;
            if (!IsInsideArea(guardArea, position)) continue;

            bool isTransformed = CS_PoliceVision.IsTransformed(player);
            _currentStates.Add(player, isTransformed);

            // 前回もエリア内にいて変身していなかったプレイヤーが、今回は変身している
            if (isTransformed && _lastStates.TryGetValue(player, out bool wasTransformed) && !wasTransformed)
            {
                transformedPositions.Add(position);
            }
        }
    }
}
