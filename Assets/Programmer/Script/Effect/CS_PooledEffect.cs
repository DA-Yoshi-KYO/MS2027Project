using UnityEngine;
using UnityEngine.VFX;

/*
 * CS_EffectManagerがプールしているエフェクト1つ分の再生を管理するクラス
 * Prefabから作った時にCS_EffectManagerが付ける(Prefabに付けておく必要はない)
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・子に入っている ParticleSystem / VisualEffect / IEffectPlayable をまとめて再生・停止する
 * ・流れ
 *   Play → Lifetime秒たったら出すのをやめる(Stop) → 残っている粒が消えたら終わり(CS_EffectManagerへ返す)
 *   Lifetimeが0の時は、出すのをやめずに、粒が消えた時点で終わり(ループしないエフェクト用)
 *   粒がいつまでも消えない時のために、止めてから _maxFadeTime 秒で強制的に終わらせる
 * ・色の上書き(Override Color・呼ぶ側から渡された色)
 *   ParticleSystem: Start Color を置き換える。色を渡されなかった時はPrefabの元の色に戻す
 *   VisualEffect: 「Color」という名前のプロパティ(Vector4)があれば置き換える。色を渡されなかった時は元に戻す
 *   IEffectPlayable: Playの後にSetColorを呼ぶ(元の色に戻すのはIEffectPlayable側のPlayで行う)
 * ・時間はTime.timeScaleの影響を受ける(ゲームの速さに合わせて止まる・遅くなる)
 */
// ========================================

public class CS_PooledEffect : MonoBehaviour
{
    private const float _maxFadeTime = 5f;      // 止めてから粒が消えるのを待つ最大時間(秒)
    private const float _maxNoLifetime = 10f;   // Lifetimeが0の時に、再生してから強制的に終わらせるまでの時間(秒)
    private const float _aliveCheckDelay = 0.1f; // 再生直後は粒がまだ出ていないことがあるので、この秒数は終わったか確かめない

    private static readonly int _vfxColorId = Shader.PropertyToID("Color");

    private ParticleSystem[] _particles;
    private ParticleSystem.MinMaxGradient[] _defaultStartColors;
    private VisualEffect[] _visualEffects;
    private IEffectPlayable[] _playables;

    private CS_EffectData _data;
    private System.Action<CS_PooledEffect> _onFinished;

    private bool _isPlaying;
    private bool _isStopped;     // 出すのをやめたか
    private float _elapsed;      // 再生してからの時間
    private float _stopElapsed;  // 出すのをやめてからの時間
    private float _playStartTime;

    public CS_EffectData data => _data;
    public bool isPlaying => _isPlaying;
    public float playStartTime => _playStartTime;   // 一番古いものを探す時に使う

    // Prefabから作った直後に1回だけ呼ぶ
    public void Initialize(CS_EffectData data, System.Action<CS_PooledEffect> onFinished)
    {
        _data = data;
        _onFinished = onFinished;

        _particles = GetComponentsInChildren<ParticleSystem>(true);
        _visualEffects = GetComponentsInChildren<VisualEffect>(true);
        _playables = GetComponentsInChildren<IEffectPlayable>(true);

        _defaultStartColors = new ParticleSystem.MinMaxGradient[_particles.Length];
        for (int i = 0; i < _particles.Length; i++)
        {
            _defaultStartColors[i] = _particles[i].main.startColor;
        }

        // 親(このオブジェクト)の大きさが粒に効くようにする(Localのままだと効かない)
        if (!Mathf.Approximately(data.scale, 1f))
        {
            foreach (ParticleSystem particle in _particles)
            {
                ParticleSystem.MainModule main = particle.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
        }
    }

    // 最初から再生する(colorがnullなら元の色)
    public void Play(Color? color)
    {
        _isPlaying = true;
        _isStopped = false;
        _elapsed = 0f;
        _stopElapsed = 0f;
        _playStartTime = Time.time;

        ApplyParticleColor(color);
        foreach (ParticleSystem particle in _particles)
        {
            // Play On Awakeで勝手に始まっている分を消してから、最初から出し直す
            particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            particle.Play(false);
        }

        foreach (VisualEffect visualEffect in _visualEffects)
        {
            ApplyVfxColor(visualEffect, color);
            visualEffect.Reinit();   // 最初の状態に戻して、Initial Event(既定はOnPlay)を送る
        }

        foreach (IEffectPlayable playable in _playables)
        {
            playable.Play();
            if (color.HasValue) playable.SetColor(color.Value);
        }
    }

    // すぐに消して終わらせる(同時に出せる数を超えた時に、一番古いものを出し直すために使う)
    public void ForceFinish()
    {
        foreach (ParticleSystem particle in _particles)
        {
            particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        foreach (VisualEffect visualEffect in _visualEffects)
        {
            visualEffect.Stop();
        }
        foreach (IEffectPlayable playable in _playables)
        {
            playable.Stop();
        }

        Finish();
    }

    private void Update()
    {
        if (!_isPlaying) return;

        float deltaTime = Time.deltaTime;
        _elapsed += deltaTime;

        bool hasLifetime = _data.lifetime > 0f;
        if (hasLifetime && !_isStopped && _elapsed >= _data.lifetime) StopEmitting();
        if (_isStopped) _stopElapsed += deltaTime;

        // 出すのをやめた後(Lifetimeが0なら再生直後から)、粒が消えたら終わり
        bool canFinish = _isStopped || !hasLifetime;
        if (!canFinish || _elapsed < _aliveCheckDelay) return;

        bool isTimeout = _isStopped ? _stopElapsed >= _maxFadeTime : _elapsed >= _maxNoLifetime;
        if (!IsAlive() || isTimeout) Finish();
    }

    // 新しく出すのをやめる(出ている粒は自然に消えるまで残す)
    private void StopEmitting()
    {
        _isStopped = true;

        foreach (ParticleSystem particle in _particles)
        {
            particle.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
        foreach (VisualEffect visualEffect in _visualEffects)
        {
            visualEffect.Stop();
        }
        foreach (IEffectPlayable playable in _playables)
        {
            playable.Stop();
        }
    }

    // まだ見えている粒があるか
    private bool IsAlive()
    {
        foreach (ParticleSystem particle in _particles)
        {
            if (particle.IsAlive(false)) return true;
        }
        foreach (VisualEffect visualEffect in _visualEffects)
        {
            if (visualEffect.aliveParticleCount > 0) return true;
        }
        foreach (IEffectPlayable playable in _playables)
        {
            if (playable.isAlive) return true;
        }
        return false;
    }

    private void Finish()
    {
        if (!_isPlaying) return;

        _isPlaying = false;
        _onFinished?.Invoke(this);
    }

    private void ApplyParticleColor(Color? color)
    {
        for (int i = 0; i < _particles.Length; i++)
        {
            ParticleSystem.MainModule main = _particles[i].main;
            main.startColor = color.HasValue ? new ParticleSystem.MinMaxGradient(color.Value) : _defaultStartColors[i];
        }
    }

    private static void ApplyVfxColor(VisualEffect visualEffect, Color? color)
    {
        if (!visualEffect.HasVector4(_vfxColorId)) return;

        if (color.HasValue) visualEffect.SetVector4(_vfxColorId, color.Value);
        else visualEffect.ResetOverride(_vfxColorId);
    }
}
