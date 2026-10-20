using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/*
 * 自分の画面にかける画面効果(ポストプロセス)をまとめて管理するクラス
 * 演出クラス(CS_PlayerDashScreenEffectなど)からCSO_ScreenEffectDataを受け取り、Volumeのweightで出し入れする
 * (ゲームロジック側はこのクラスの存在を知らない。外しても動きは変わらない)
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・効果(CSO_ScreenEffectData)ごとに全体用のVolumeを1つ作り、このオブジェクトの子に置く(最初に使われた時だけ作る)
 *   複数の効果が同時に出ても、HDRPのVolumeの仕組みで重なる(同じ項目はpriorityの高い方が勝つ)
 * ・出し方
 *   SetHeld(data, true/false) : trueの間ずっとかける。fadeInTime / fadeOutTimeで出し入れする
 *   Play(data)                : 1回だけかける。playDuration秒かけてplayWeightCurveの強さで出す
 *   両方が同時に効いている時は強い方を使う
 * ・自分の画面のプレイヤー(CS_Player.isLocalPlayer)の時だけかける
 *   (他人のプレイヤー・NPCで呼ばれても何もしない。NPCはホストで動くため、ホストの画面にかからないようにする)
 * ・時間はTime.timeScaleの影響を受けない時間で数える(ヒットストップ中も出し入れが止まらないように)
 * ・見た目の演出用なので通信はしない
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
public class CS_PlayerScreenEffect : MonoBehaviour
{
    // 効果1つ分の再生状態
    private class Channel
    {
        public CSO_ScreenEffectData data;
        public Volume volume;
        public bool isHeld;             // SetHeldでかけ続けているか
        public float heldWeight;        // SetHeld側の強さ(0〜1)
        public float playElapsed = -1f; // Playしてからの経過時間(秒)。負なら再生していない
    }

    private CS_Player _player;
    private readonly Dictionary<CSO_ScreenEffectData, Channel> _channels = new Dictionary<CSO_ScreenEffectData, Channel>();

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
    }

    private void LateUpdate()
    {
        float deltaTime = Time.unscaledDeltaTime;

        foreach (Channel channel in _channels.Values)
        {
            UpdateWeight(channel, deltaTime);
        }
    }

    private void OnDisable()
    {
        // 外した時・無効にした時は全部消す
        foreach (Channel channel in _channels.Values)
        {
            channel.isHeld = false;
            channel.heldWeight = 0f;
            channel.playElapsed = -1f;
            ApplyWeight(channel, 0f);
        }
    }

    // trueの間ずっと効果をかける(falseでfadeOutTimeかけて消す)
    public void SetHeld(CSO_ScreenEffectData data, bool isHeld)
    {
        // まだ一度も使っていない効果を消す時は、Volumeを作らずに済ませる
        if (!isHeld && (data == null || !_channels.ContainsKey(data))) return;

        Channel channel = GetChannel(data);
        if (channel == null) return;

        channel.isHeld = isHeld;
    }

    // 効果を1回だけかける(再生中に呼ぶと最初からやり直す)
    public void Play(CSO_ScreenEffectData data)
    {
        Channel channel = GetChannel(data);
        if (channel == null) return;

        channel.playElapsed = 0f;
    }

    private void UpdateWeight(Channel channel, float deltaTime)
    {
        CSO_ScreenEffectData data = channel.data;

        // SetHeld側: 目標(1か0)へ一定の速さで近づける
        float target = channel.isHeld ? 1f : 0f;
        float fadeTime = channel.isHeld ? data.fadeInTime : data.fadeOutTime;
        channel.heldWeight = fadeTime > 0f
            ? Mathf.MoveTowards(channel.heldWeight, target, deltaTime / fadeTime)
            : target;

        // Play側: 経過の割合でカーブを読む
        float playWeight = 0f;
        if (channel.playElapsed >= 0f)
        {
            channel.playElapsed += deltaTime;
            float rate = channel.playElapsed / data.playDuration;
            if (rate >= 1f)
            {
                channel.playElapsed = -1f;
            }
            else
            {
                playWeight = Mathf.Clamp01(data.playWeightCurve.Evaluate(rate));
            }
        }

        ApplyWeight(channel, Mathf.Max(channel.heldWeight, playWeight));
    }

    // weightが0の時はVolumeごと止め、HDRPの計算対象から外す
    private static void ApplyWeight(Channel channel, float weight)
    {
        if (channel.volume == null) return;   // シーンを閉じる時など、先にVolumeが消えている場合

        channel.volume.weight = weight;
        channel.volume.enabled = weight > 0f;
    }

    // 効果に対応する再生状態を返す(なければVolumeを作る)。自分の画面のプレイヤーでない時はnull
    private Channel GetChannel(CSO_ScreenEffectData data)
    {
        if (data == null || data.profile == null) return null;
        if (!_player.isLocalPlayer) return null;

        if (_channels.TryGetValue(data, out Channel channel)) return channel;

        GameObject volumeObject = new GameObject($"ScreenEffect_{data.name}");
        volumeObject.transform.SetParent(transform, false);

        Volume volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = data.priority;
        volume.sharedProfile = data.profile;
        volume.weight = 0f;
        volume.enabled = false;

        channel = new Channel { data = data, volume = volume };
        _channels.Add(data, channel);
        return channel;
    }
}
