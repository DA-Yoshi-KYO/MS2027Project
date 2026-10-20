using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/*
 * 自分の画面にかける画面効果(ポストプロセス)をまとめて管理するクラス
 * 演出クラス(CS_PlayerDashScreenEffectなど)からVolume Profileを受け取り、Volumeのweightで出し入れする
 * (ゲームロジック側はこのクラスの存在を知らない。外しても動きは変わらない)
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・効果(Volume Profile)ごとに全体用のVolumeを1つ作り、このオブジェクトの子に置く(最初に使われた時だけ作る)
 *   複数の効果が同時に出ても、HDRPのVolumeの仕組みで重なる(同じ項目はpriorityの高い方が勝つ)
 * ・出し入れの時間はProfileに入れたCSV_ScreenEffectTimingから読む(入っていなければ既定値)
 *   時間は毎フレーム読むので、再生中にInspectorで値を変えてもすぐ反映される(Priorityだけは最初にVolumeを作った時の値)
 * ・出し方
 *   SetHeld(profile, true/false) : trueの間ずっとかける。fadeInTime / fadeOutTimeで出し入れする
 *   Play(profile)                : 1回だけかける。playDuration秒かけてplayWeightCurveの強さで出す
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
        public CSV_ScreenEffectTiming timing;
        public Volume volume;
        public bool isHeld;             // SetHeldでかけ続けているか
        public float heldWeight;        // SetHeld側の強さ(0〜1)
        public float playElapsed = -1f; // Playしてからの経過時間(秒)。負なら再生していない
    }

    // ProfileにCSV_ScreenEffectTimingが入っていない時に使う既定値(全効果で共有する)
    private static CSV_ScreenEffectTiming _defaultTiming;

    private CS_Player _player;
    private readonly Dictionary<VolumeProfile, Channel> _channels = new Dictionary<VolumeProfile, Channel>();

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
    public void SetHeld(VolumeProfile profile, bool isHeld)
    {
        // まだ一度も使っていない効果を消す時は、Volumeを作らずに済ませる
        if (!isHeld && (profile == null || !_channels.ContainsKey(profile))) return;

        Channel channel = GetChannel(profile);
        if (channel == null) return;

        channel.isHeld = isHeld;
    }

    // 効果を1回だけかける(再生中に呼ぶと最初からやり直す)
    public void Play(VolumeProfile profile)
    {
        Channel channel = GetChannel(profile);
        if (channel == null) return;

        channel.playElapsed = 0f;
    }

    private void UpdateWeight(Channel channel, float deltaTime)
    {
        CSV_ScreenEffectTiming timing = channel.timing;

        // SetHeld側: 目標(1か0)へ一定の速さで近づける
        float target = channel.isHeld ? 1f : 0f;
        float fadeTime = channel.isHeld ? timing.fadeInTime.value : timing.fadeOutTime.value;
        channel.heldWeight = fadeTime > 0f
            ? Mathf.MoveTowards(channel.heldWeight, target, deltaTime / fadeTime)
            : target;

        // Play側: 経過の割合でカーブを読む
        float playWeight = 0f;
        if (channel.playElapsed >= 0f)
        {
            channel.playElapsed += deltaTime;
            float rate = channel.playElapsed / timing.playDuration.value;
            if (rate >= 1f)
            {
                channel.playElapsed = -1f;
            }
            else
            {
                playWeight = Mathf.Clamp01(timing.playWeightCurve.value.Evaluate(rate));
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
    private Channel GetChannel(VolumeProfile profile)
    {
        if (profile == null) return null;
        if (!_player.isLocalPlayer) return null;

        if (_channels.TryGetValue(profile, out Channel channel)) return channel;

        CSV_ScreenEffectTiming timing = GetTiming(profile);

        GameObject volumeObject = new GameObject($"ScreenEffect_{profile.name}");
        volumeObject.transform.SetParent(transform, false);

        Volume volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = timing.priority.value;
        volume.sharedProfile = profile;
        volume.weight = 0f;
        volume.enabled = false;

        channel = new Channel { timing = timing, volume = volume };
        _channels.Add(profile, channel);
        return channel;
    }

    // Profileの出し入れの時間を返す(入っていなければ既定値)
    private static CSV_ScreenEffectTiming GetTiming(VolumeProfile profile)
    {
        if (profile.TryGet(out CSV_ScreenEffectTiming timing)) return timing;

        if (_defaultTiming == null) _defaultTiming = ScriptableObject.CreateInstance<CSV_ScreenEffectTiming>();
        return _defaultTiming;
    }
}
