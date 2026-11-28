/* ================================================
 *
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-22 | 初回作成
 * 2026-11-28 | 全体の音量(開発用)・素材の底上げ・ジングル・設定画面用の操作を追加(中出峻輔)
 * ================================================ */

using System;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// AudioMixerのMaster・BGM・SE・ジングルの音量を管理する。
/// 0～1の音量をdBへ変換し、プレイヤーの設定はPlayerPrefsへ保存する。
/// </summary>
// ========================================
/*
 * メモ
 * ・最終的な音量(dB) = プレイヤーの設定(0～1) + 全体の音量(開発用の倍率) + 素材の底上げ(dB)
 *   ミキサーの上限(+20dB)を超える分は切る。どれかが0ならミュート(-80dB)
 *
 * ・全体の音量(Inspector「全体の音量」): 開発側で、BGM全体・SE全体・ジングル全体を一括で上げ下げする倍率(1 = データベースで調整したまま)
 *   再生中にInspectorで変えても、すぐ反映される。保存はしない(プレハブの値がそのまま使われる)
 * ・素材の底上げ(Inspector「素材の底上げ」): 音素材そのものが小さいBGM・ジングルを、音量1のままSEと同じくらいに聞こえるようにするdB
 * ・プレイヤーの設定: ゲーム内の設定画面から変える音量(マスター・BGM・SE。0～1)。PlayerPrefsに保存する
 *   初期値はInspector「プレイヤーの設定の初期値」。一度保存されると、そちらが優先される
 *   (初期値を変えて試したい時は、コンポーネントの右クリックメニュー「保存した設定を消す」)
 *   ジングルは音楽なので、BGMの設定に合わせる
 *
 * ・設定画面から使う時
 *   CS_SoundVolumeController.instance.GetVolume(CSE_SoundVolumeType.Bgm) / SetVolume(CSE_SoundVolumeType.Bgm, 0.5f)
 *   スライダーを動かしている間は SetVolume(type, value, false) で保存せず、閉じる時に Save() でまとめて保存してもよい
 *   UIのSliderにつなぐだけなら CS_SoundVolumeSlider を使う
 *   onVolumeChanged: プレイヤーの設定が変わった時に呼ばれる(表示の更新用)
 */
// ========================================
public class CS_SoundVolumeController : MonoBehaviour
{
    private static CS_SoundVolumeController _instance;
    public static CS_SoundVolumeController instance => _instance;

    // =========================================================
    // PlayerPrefsキー
    // =========================================================

    private const string MasterVolumeKey = "Sound.MasterVolume";
    private const string BgmVolumeKey = "Sound.BgmVolume";
    private const string SeVolumeKey = "Sound.SeVolume";

    // =========================================================
    // AudioMixer公開パラメータ名
    // =========================================================

    private const string MasterVolumeParameter = "MasterVolume";
    private const string BgmVolumeParameter = "BgmVolume";
    private const string SeVolumeParameter = "SeVolume";
    private const string JingleVolumeParameter = "JingleVolume";

    private const float MuteDecibel = -80f;
    private const float MaxDecibel = 20f;   // AudioMixerの音量の上限

    // =========================================================
    // Inspector
    // =========================================================

    [Header("AudioMixer")]
    [SerializeField] private AudioMixer _audioMixer;

    [Header("全体の音量（開発用の倍率。1 = データベースで調整したまま）")]
    [Range(0f, 2f)]
    [SerializeField] private float _bgmOverallVolume = 1f;      // BGM全体

    [Range(0f, 2f)]
    [SerializeField] private float _seOverallVolume = 1f;       // SE全体

    [Range(0f, 2f)]
    [SerializeField] private float _jingleOverallVolume = 1f;   // ジングル全体

    [Header("素材の底上げ（dB）")]
    [Tooltip("BGMの音素材が小さいため、音量1でもSEと同じくらいに聞こえるように足すdB")]
    [Range(0f, 20f)]
    [SerializeField] private float _bgmGainDb = 17f;

    [Tooltip("ジングルの音素材が小さいため、音量1でもSEと同じくらいに聞こえるように足すdB")]
    [Range(0f, 20f)]
    [SerializeField] private float _jingleGainDb = 18f;

    [Header("プレイヤーの設定の初期値（0～1）")]
    [Range(0f, 1f)]
    [SerializeField] private float _defaultMasterVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float _defaultBgmVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float _defaultSeVolume = 1f;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private float _masterVolume;
    private float _bgmVolume;
    private float _seVolume;
    private bool _isInitialized;

    // =========================================================
    // プロパティ公開（読み取り専用）
    // =========================================================

    public float masterVolume => _masterVolume;
    public float bgmVolume => _bgmVolume;
    public float seVolume => _seVolume;

    /// <summary>プレイヤーの設定(マスター・BGM・SE)が変わった時</summary>
    public event Action onVolumeChanged;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        if (_instance == null) _instance = this;
    }

    private void Start()
    {
        LoadVolumes();
        _isInitialized = true;
        ApplyAllVolumes();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // 再生中にInspectorで変えたら、すぐ反映する
    private void OnValidate()
    {
        if (Application.isPlaying && _isInitialized) ApplyAllVolumes();
    }

    // =========================================================
    // プレイヤーの設定（設定画面から使う）
    // =========================================================

    public float GetVolume(CSE_SoundVolumeType type)
    {
        switch (type)
        {
            case CSE_SoundVolumeType.Master: return _masterVolume;
            case CSE_SoundVolumeType.Bgm: return _bgmVolume;
            case CSE_SoundVolumeType.Se: return _seVolume;
            default: return 1f;
        }
    }

    /// <summary>
    /// プレイヤーの設定の音量(0～1)を変えて、すぐ反映する。save: PlayerPrefsに保存するか
    /// </summary>
    public void SetVolume(CSE_SoundVolumeType type, float volume, bool save = true)
    {
        volume = Mathf.Clamp01(volume);
        switch (type)
        {
            case CSE_SoundVolumeType.Master: _masterVolume = volume; break;
            case CSE_SoundVolumeType.Bgm: _bgmVolume = volume; break;
            case CSE_SoundVolumeType.Se: _seVolume = volume; break;
            default: return;
        }

        ApplyAllVolumes();
        if (save) Save();
        onVolumeChanged?.Invoke();
    }

    public void SetMasterVolume(float volume) => SetVolume(CSE_SoundVolumeType.Master, volume);
    public void SetBgmVolume(float volume) => SetVolume(CSE_SoundVolumeType.Bgm, volume);
    public void SetSeVolume(float volume) => SetVolume(CSE_SoundVolumeType.Se, volume);

    /// <summary>今のプレイヤーの設定をPlayerPrefsに保存する</summary>
    public void Save()
    {
        PlayerPrefs.SetFloat(MasterVolumeKey, _masterVolume);
        PlayerPrefs.SetFloat(BgmVolumeKey, _bgmVolume);
        PlayerPrefs.SetFloat(SeVolumeKey, _seVolume);
        PlayerPrefs.Save();
    }

    /// <summary>プレイヤーの設定を初期値に戻す(保存した設定も消す)</summary>
    [ContextMenu("保存した設定を消す（初期値に戻す）")]
    public void ResetToDefault()
    {
        PlayerPrefs.DeleteKey(MasterVolumeKey);
        PlayerPrefs.DeleteKey(BgmVolumeKey);
        PlayerPrefs.DeleteKey(SeVolumeKey);
        PlayerPrefs.Save();

        LoadVolumes();
        if (!Application.isPlaying) return;

        ApplyAllVolumes();
        onVolumeChanged?.Invoke();
    }

    // =========================================================
    // 読み込み
    // =========================================================

    private void LoadVolumes()
    {
        _masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, _defaultMasterVolume));
        _bgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, _defaultBgmVolume));
        _seVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SeVolumeKey, _defaultSeVolume));
    }

    // =========================================================
    // AudioMixer反映
    // =========================================================

    private void ApplyAllVolumes()
    {
        ApplyVolume(MasterVolumeParameter, _masterVolume, 1f, 0f);
        ApplyVolume(BgmVolumeParameter, _bgmVolume, _bgmOverallVolume, _bgmGainDb);
        ApplyVolume(SeVolumeParameter, _seVolume, _seOverallVolume, 0f);
        ApplyVolume(JingleVolumeParameter, _bgmVolume, _jingleOverallVolume, _jingleGainDb);   // ジングルはBGMの設定に合わせる
    }

    private void ApplyVolume(
        string exposedParameter,
        float userVolume,
        float overallVolume,
        float gainDb)
    {
        if (_audioMixer == null)
        {
            Debug.LogError(
                "[CS_SoundVolumeController] AudioMixerが未設定です。",
                this
            );
            return;
        }

        float decibel = CalculateDecibel(userVolume, overallVolume, gainDb);

        bool succeeded = _audioMixer.SetFloat(
            exposedParameter,
            decibel
        );

        if (!succeeded)
        {
            Debug.LogWarning(
                $"[CS_SoundVolumeController] " +
                $"公開パラメータが見つかりません: {exposedParameter}",
                this
            );
        }
    }

    /// <summary>
    /// プレイヤーの設定(0～1)・全体の音量(倍率)・底上げ(dB)から、AudioMixer用のdBを出す。
    /// どれかの音量が0ならミュート(-80dB)。上限は+20dB。
    /// </summary>
    private static float CalculateDecibel(float userVolume, float overallVolume, float gainDb)
    {
        if (userVolume <= 0.0001f || overallVolume <= 0.0001f)
            return MuteDecibel;

        float decibel = ConvertLinearVolumeToDecibel(userVolume)
            + ConvertLinearVolumeToDecibel(overallVolume)
            + gainDb;
        return Mathf.Clamp(decibel, MuteDecibel, MaxDecibel);
    }

    /// <summary>
    /// 0～1の音量をAudioMixer用のdBへ変換する。
    /// 0は完全ミュートとして-80dBにする。
    /// </summary>
    private static float ConvertLinearVolumeToDecibel(
        float linearVolume)
    {
        if (linearVolume <= 0.0001f)
            return MuteDecibel;

        return Mathf.Log10(linearVolume) * 20f;
    }
}
