using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/*
 * 警戒状態の間、画面の端に赤いビネットを出して拡縮させるクラス
 * CS_PlayerAlertの状態を見て、CS_PlayerScreenEffectへ効果のオン・オフを伝える
 * (ゲームロジック側はこのクラスの存在を知らない。外しても警戒状態は動く)
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・仕様はConfluence「プレイヤー」→「警戒状態時に起きる変化」①赤いビネットを拡縮させる(Issue #377)
 *   危機的な状況だとプレイヤーに伝えるため
 * ・かける条件は、警戒状態(発見中・捜索中のどちらでも。CS_PlayerAlert.isAlerted)の間
 *   警戒状態はサーバーが決めて同期するので、どのクライアントでも同じ条件で出る
 * ・効果のVolume Profileはこのクラスが実行時に作る(アセットは作らない)
 *   色・強さ・拡縮の速さ・出し入れの時間は、すべてこのコンポーネントのインスペクターで変える
 *   再生中にインスペクターで値を変えてもすぐ反映される(優先度だけは最初に効果を出した時の値)
 * ・拡縮: VignetteのIntensityを、最小〜最大の間でsin波のように往復させる
 *   Volumeのweightで揺らすと、下にある色(既定の黒)と混ざって赤が濁るため、weightは出し入れだけに使う
 * ・ハイド中の黒いビネット(VO_HideScreenEffect、優先度100)と重なった時は、優先度の高いこちらの赤が勝つ
 * ・自分の画面のプレイヤーかどうかの判定はCS_PlayerScreenEffect側で行う
 * ・時間はTime.timeScaleの影響を受けない時間で数える(CS_PlayerScreenEffectに合わせる)
 */
// ========================================

[RequireComponent(typeof(CS_PlayerAlert))]
[RequireComponent(typeof(CS_PlayerScreenEffect))]
public class CS_PlayerAlertScreenEffect : MonoBehaviour
{
    [Header("見た目")]
    [SerializeField] private Color _color = new Color(0.9f, 0f, 0f, 1f);           // ビネットの色
    [SerializeField][Range(0f, 1f)] private float _intensityMin = 0.3f;           // 縮んだ時の強さ(HDRPのVignetteのIntensity)
    [SerializeField][Range(0f, 1f)] private float _intensityMax = 0.5f;           // 広がった時の強さ
    [SerializeField][Range(0.01f, 1f)] private float _smoothness = 0.4f;          // 端のぼかし具合(HDRPのVignetteのSmoothness)

    [Header("拡縮")]
    [SerializeField][Min(0f)] private float _pulseSpeed = 1f;                     // 1秒に何回拡縮するか

    [Header("出し入れ")]
    [SerializeField][Min(0f)] private float _fadeInTime = 0.2f;                   // 出しきるまでの時間(秒)
    [SerializeField][Min(0f)] private float _fadeOutTime = 0.5f;                  // 消えきるまでの時間(秒)
    [SerializeField] private float _priority = 110f;                              // Volumeの優先度(ハイドの黒いビネットより上にする)

    private CS_PlayerAlert _alert;
    private CS_PlayerScreenEffect _screenEffect;

    private VolumeProfile _alertEffect;     // 実行時に作る効果(Vignette + CSV_ScreenEffectTiming)
    private Vignette _vignette;
    private CSV_ScreenEffectTiming _timing;
    private float _pulsePhase;              // 拡縮の進み具合(1で1往復)

    private void Awake()
    {
        _alert = GetComponent<CS_PlayerAlert>();
        _screenEffect = GetComponent<CS_PlayerScreenEffect>();
        CreateProfile();
    }

    private void LateUpdate()
    {
        bool isAlerted = _alert.isAlerted;

        UpdatePulse(isAlerted);
        ApplySettings();
        _screenEffect.SetHeld(_alertEffect, isAlerted);
    }

    private void OnDisable()
    {
        if (_screenEffect == null) return;
        _screenEffect.SetHeld(_alertEffect, false);
    }

    private void OnDestroy()
    {
        if (_alertEffect != null) Destroy(_alertEffect);
    }

    // 拡縮を進めて、今の強さをVignetteに入れる
    private void UpdatePulse(bool isAlerted)
    {
        // 警戒状態でない間は、次に出す時に縮んだ所から始まるように戻しておく
        if (!isAlerted)
        {
            _pulsePhase = 0f;
            return;
        }

        _pulsePhase = Mathf.Repeat(_pulsePhase + Time.unscaledDeltaTime * _pulseSpeed, 1f);
    }

    // インスペクターの値を効果に入れる(再生中に変えてもすぐ反映されるよう、毎フレーム入れる)
    private void ApplySettings()
    {
        // 0 → 1 → 0 と滑らかに往復する(位相0で最小、0.5で最大)
        float rate = 0.5f - 0.5f * Mathf.Cos(_pulsePhase * Mathf.PI * 2f);

        _vignette.color.value = _color;
        _vignette.intensity.value = Mathf.Lerp(_intensityMin, _intensityMax, rate);
        _vignette.smoothness.value = _smoothness;

        _timing.fadeInTime.value = _fadeInTime;
        _timing.fadeOutTime.value = _fadeOutTime;
    }

    // 赤いビネットの効果を作る
    private void CreateProfile()
    {
        _alertEffect = ScriptableObject.CreateInstance<VolumeProfile>();
        _alertEffect.name = "AlertScreenEffect";

        _vignette = _alertEffect.Add<Vignette>(true);

        // 優先度はCS_PlayerScreenEffectが最初にVolumeを作る時に読むので、ここで入れておく
        _timing = _alertEffect.Add<CSV_ScreenEffectTiming>(true);
        _timing.priority.value = _priority;

        ApplySettings();
    }

    private void OnValidate()
    {
        _intensityMax = Mathf.Max(_intensityMin, _intensityMax);
    }
}
