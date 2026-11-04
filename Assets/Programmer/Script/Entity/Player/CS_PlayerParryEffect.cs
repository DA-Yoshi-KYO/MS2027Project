using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/*
 * パリー(CS_PlayerParry)に成功した時の演出を行うクラス
 * 自分のブリンクモーションと相手の攻撃モーションを少しの間遅くし、自分の画面の端を少し暗くする
 * (ゲームロジック側はこのクラスの存在を知らない。外してもパリーは動く)
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・仕様書「プレイヤー」のブリンク
 *   成功したことをわかりやすくするため、自身のブリンクモーションと敵の攻撃モーションの再生速度を
 *   0.3秒間ほど-80%ぐらい遅くする
 *   +αで、その0.3秒間画面端を少し(暗くなった認識はできる)だけ暗くし、また0.3秒かけて元の明るさに戻す
 * ・スロー: 自分と相手の子にあるAnimatorの再生速度を_slowDuration秒間だけ(1 - _slowRate)倍にし、元の速さに戻す
 *   全クライアントで行う(CS_PlayerParry.onParriedは全クライアントで発生する)
 *   ※ 見た目(Animatorの再生速度)だけを変える。悪人・警察の動き(ロジック)は変えない
 * ・画面端: HDRPのVignette(周辺減光)を、このクラスが作る全体用のVolumeで重ねる
 *   _slowDuration秒間は暗くしたまま(最初は_darkenInTimeで素早く暗くする)、その後_darkenOutTime秒かけて元に戻す
 *   自分の画面(CS_Player.isLocalPlayer)のパリーの時だけ行う
 * ・時間はTime.timeScaleの影響を受けない時間で数える
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
[RequireComponent(typeof(CS_PlayerParry))]
public class CS_PlayerParryEffect : MonoBehaviour
{
    [Header("スロー")]
    [SerializeField][Range(0f, 1f)] private float _slowRate = 0.8f;      // 再生速度を遅くする割合(0.8で-80%)
    [SerializeField][Min(0f)] private float _slowDuration = 0.3f;       // 遅くする時間(秒)

    [Header("画面端を暗くする")]
    [SerializeField] private bool _useVignette = true;
    [SerializeField][Range(0f, 1f)] private float _vignetteIntensity = 0.35f;   // 暗さ(HDRPのVignetteのIntensity)
    [SerializeField][Min(0f)] private float _darkenInTime = 0.05f;      // 暗くなるまでの時間(秒)
    [SerializeField][Min(0f)] private float _darkenOutTime = 0.3f;      // 元の明るさに戻るまでの時間(秒)

    private const float _vignettePriority = 100f;   // ほかのVolumeより優先する

    private CS_Player _player;
    private CS_PlayerParry _parry;

    private readonly Dictionary<Animator, float> _slowedAnimators = new Dictionary<Animator, float>();   // 遅くしているAnimatorと元の速さ
    private Coroutine _slowCoroutine;

    private Volume _vignetteVolume;
    private VolumeProfile _vignetteProfile;
    private Coroutine _vignetteCoroutine;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _parry = GetComponent<CS_PlayerParry>();
    }

    private void OnEnable()
    {
        _parry.onParried += HandleParried;
    }

    private void OnDisable()
    {
        _parry.onParried -= HandleParried;
        RestoreAnimators();
        if (_vignetteVolume != null) _vignetteVolume.weight = 0f;
    }

    private void OnDestroy()
    {
        if (_vignetteVolume != null) Destroy(_vignetteVolume.gameObject);
        if (_vignetteProfile != null) Destroy(_vignetteProfile);
    }

    private void HandleParried(GameObject attacker)
    {
        StartSlow(attacker);

        if (_useVignette && _player.isLocalPlayer)
        {
            StartVignette();
        }
    }

    // ========================================
    // スロー
    // ========================================

    private void StartSlow(GameObject attacker)
    {
        if (_slowRate <= 0f || _slowDuration <= 0f) return;

        // 前のスローが残っていれば、いったん元に戻してからかけ直す
        if (_slowCoroutine != null) StopCoroutine(_slowCoroutine);
        RestoreAnimators();

        SlowAnimators(gameObject);
        if (attacker != null) SlowAnimators(attacker);

        _slowCoroutine = StartCoroutine(SlowRoutine());
    }

    private void SlowAnimators(GameObject root)
    {
        foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
        {
            if (_slowedAnimators.ContainsKey(animator)) continue;

            _slowedAnimators.Add(animator, animator.speed);
            animator.speed *= 1f - _slowRate;
        }
    }

    private IEnumerator SlowRoutine()
    {
        yield return new WaitForSecondsRealtime(_slowDuration);
        RestoreAnimators();
        _slowCoroutine = null;
    }

    // 遅くしていたAnimatorを元の速さに戻す(消えたものは飛ばす)
    private void RestoreAnimators()
    {
        foreach (KeyValuePair<Animator, float> pair in _slowedAnimators)
        {
            if (pair.Key != null) pair.Key.speed = pair.Value;
        }
        _slowedAnimators.Clear();
    }

    // ========================================
    // 画面端を暗くする
    // ========================================

    private void StartVignette()
    {
        EnsureVignetteVolume();

        if (_vignetteCoroutine != null) StopCoroutine(_vignetteCoroutine);
        _vignetteCoroutine = StartCoroutine(VignetteRoutine());
    }

    // 暗くする → _slowDuration秒まで保つ → _darkenOutTime秒かけて戻す
    private IEnumerator VignetteRoutine()
    {
        float startWeight = _vignetteVolume.weight;
        float elapsed = 0f;
        while (elapsed < _slowDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = _darkenInTime <= 0f ? 1f : Mathf.Clamp01(elapsed / _darkenInTime);
            _vignetteVolume.weight = Mathf.Lerp(startWeight, 1f, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < _darkenOutTime)
        {
            elapsed += Time.unscaledDeltaTime;
            _vignetteVolume.weight = 1f - Mathf.Clamp01(elapsed / _darkenOutTime);
            yield return null;
        }

        _vignetteVolume.weight = 0f;
        _vignetteCoroutine = null;
    }

    // 画面端を暗くするための全体用Volumeを作る(最初の1回だけ)
    private void EnsureVignetteVolume()
    {
        if (_vignetteVolume != null) return;

        _vignetteProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        Vignette vignette = _vignetteProfile.Add<Vignette>(true);
        vignette.intensity.value = _vignetteIntensity;
        vignette.color.value = Color.black;

        GameObject volumeObject = new GameObject("ParryVignette");
        _vignetteVolume = volumeObject.AddComponent<Volume>();
        _vignetteVolume.isGlobal = true;
        _vignetteVolume.priority = _vignettePriority;
        _vignetteVolume.sharedProfile = _vignetteProfile;
        _vignetteVolume.weight = 0f;
    }
}
