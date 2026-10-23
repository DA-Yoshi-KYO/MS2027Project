/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-22 | 初回作成
 * ================================================ */

using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// AudioMixerのMaster・BGM・SE音量を管理する。
/// 0～1の音量をdBへ変換し、PlayerPrefsへ保存する。
/// </summary>
public class CS_SoundVolumeController : MonoBehaviour
{
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

    // =========================================================
    // Inspector
    // =========================================================

    [Header("AudioMixer")]
    [SerializeField] private AudioMixer _audioMixer;

    [Header("初期音量（0～1）")]
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

    // =========================================================
    // プロパティ公開（読み取り専用）
    // =========================================================

    public float masterVolume => _masterVolume;
    public float bgmVolume => _bgmVolume;
    public float seVolume => _seVolume;

    // =========================================================
    // 初期化
    // =========================================================

    private void Start()
    {
        LoadVolumes();
        ApplyAllVolumes();
    }

    // =========================================================
    // 音量変更API
    // =========================================================

    public void SetMasterVolume(float volume)
    {
        _masterVolume = Mathf.Clamp01(volume);

        ApplyVolume(
            MasterVolumeParameter,
            _masterVolume
        );

        SaveVolume(
            MasterVolumeKey,
            _masterVolume
        );
    }

    public void SetBgmVolume(float volume)
    {
        _bgmVolume = Mathf.Clamp01(volume);

        ApplyVolume(
            BgmVolumeParameter,
            _bgmVolume
        );

        SaveVolume(
            BgmVolumeKey,
            _bgmVolume
        );
    }

    public void SetSeVolume(float volume)
    {
        _seVolume = Mathf.Clamp01(volume);

        ApplyVolume(
            SeVolumeParameter,
            _seVolume
        );

        SaveVolume(
            SeVolumeKey,
            _seVolume
        );
    }

    // =========================================================
    // 読み込み・保存
    // =========================================================

    private void LoadVolumes()
    {
        _masterVolume = PlayerPrefs.GetFloat(
            MasterVolumeKey,
            _defaultMasterVolume
        );

        _bgmVolume = PlayerPrefs.GetFloat(
            BgmVolumeKey,
            _defaultBgmVolume
        );

        _seVolume = PlayerPrefs.GetFloat(
            SeVolumeKey,
            _defaultSeVolume
        );
    }

    private static void SaveVolume(
        string playerPrefsKey,
        float volume)
    {
        PlayerPrefs.SetFloat(
            playerPrefsKey,
            volume
        );

        PlayerPrefs.Save();
    }

    // =========================================================
    // AudioMixer反映
    // =========================================================

    private void ApplyAllVolumes()
    {
        ApplyVolume(
            MasterVolumeParameter,
            _masterVolume
        );

        ApplyVolume(
            BgmVolumeParameter,
            _bgmVolume
        );

        ApplyVolume(
            SeVolumeParameter,
            _seVolume
        );
    }

    private void ApplyVolume(
        string exposedParameter,
        float linearVolume)
    {
        if (_audioMixer == null)
        {
            Debug.LogError(
                "[CS_SoundVolumeController] AudioMixerが未設定です。",
                this
            );
            return;
        }

        float decibel = ConvertLinearVolumeToDecibel(
            linearVolume
        );

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
    /// 0～1の音量をAudioMixer用のdBへ変換する。
    /// 0は完全ミュートとして-80dBにする。
    /// </summary>
    private static float ConvertLinearVolumeToDecibel(
        float linearVolume)
    {
        if (linearVolume <= 0.0001f)
            return -80f;

        return Mathf.Log10(linearVolume) * 20f;
    }
}
