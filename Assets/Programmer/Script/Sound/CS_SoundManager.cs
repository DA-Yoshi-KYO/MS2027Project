/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-22 | 初回作成
 * ================================================ */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// SE・BGM・ジングルを管理するサウンドマネージャー。
/// </summary>
public class CS_SoundManager : MonoBehaviour
{
    private static CS_SoundManager _instance;
    public static CS_SoundManager instance => _instance;

    [Header("データベース")]
    [SerializeField] private CSO_SeDataBase _seDatabase;
    [SerializeField] private CSO_BgmDataBase _bgmDatabase;
    [SerializeField] private CSO_JingleDataBase _jingleDatabase;

    [Header("AudioMixer出力先")]
    [SerializeField] private AudioMixerGroup _seMixerGroup;
    [SerializeField] private AudioMixerGroup _bgmMixerGroup;
    [SerializeField] private AudioMixerGroup _jingleMixerGroup;

    [Header("SE設定")]
    [Min(1)]
    [SerializeField] private int _sePoolSize = 16;

    [Header("BGM設定")]
    [Min(0f)]
    [SerializeField] private float _defaultBgmFadeTime = 1f;

    private readonly List<SeSourceEntry> _seSourcePool = new();

    private AudioSource _bgmSourceA;
    private AudioSource _bgmSourceB;
    private AudioSource _currentBgmSource;
    private AudioSource _nextBgmSource;
    private Coroutine _bgmFadeCoroutine;
    private CSE_BgmId _currentBgmId = CSE_BgmId.None;

    private AudioSource _jingleSource;
    private Coroutine _jingleCoroutine;
    private Coroutine _duckCoroutine;
    private CSE_JingleId _currentJingleId = CSE_JingleId.None;
    private float _currentBgmDataVolume = 1f;

    public CSE_BgmId currentBgmId => _currentBgmId;
    public CSE_JingleId currentJingleId => _currentJingleId;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        CreateSeSourcePool();
        CreateBgmSources();
        CreateJingleSource();
    }

    private void Update()
    {
        foreach (SeSourceEntry entry in _seSourcePool)
        {
            if (!entry.audioSource.isPlaying &&
                entry.playingSeId != CSE_SeId.None)
            {
                ResetFinishedEntry(entry);
            }
        }
    }

    // =========================================================
    // AudioSource生成
    // =========================================================

    private void CreateSeSourcePool()
    {
        _seSourcePool.Clear();

        for (int i = 0; i < _sePoolSize; i++)
        {
            var sourceObject = new GameObject($"SeAudioSource_{i:00}");
            sourceObject.transform.SetParent(transform, false);

            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = _seMixerGroup;

            _seSourcePool.Add(new SeSourceEntry(source));
        }
    }

    private void CreateBgmSources()
    {
        _bgmSourceA = CreateMusicSource("BgmAudioSource_A", _bgmMixerGroup);
        _bgmSourceB = CreateMusicSource("BgmAudioSource_B", _bgmMixerGroup);
        _currentBgmSource = _bgmSourceA;
        _nextBgmSource = _bgmSourceB;
    }

    private void CreateJingleSource()
    {
        _jingleSource = CreateMusicSource(
            "JingleAudioSource",
            _jingleMixerGroup
        );
        _jingleSource.loop = false;
    }

    private AudioSource CreateMusicSource(
        string objectName,
        AudioMixerGroup mixerGroup)
    {
        var sourceObject = new GameObject(objectName);
        sourceObject.transform.SetParent(transform, false);

        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.pitch = 1f;
        source.outputAudioMixerGroup = mixerGroup;
        return source;
    }

    // =========================================================
    // SE
    // =========================================================

    public void PlaySe(CSE_SeId id)
    {
        if (!TryGetPlayableSeData(id, out CS_SeData data, out AudioClip clip))
            return;

        if (data.is3D)
        {
            Debug.LogWarning(
                $"[CS_SoundManager] 3D SEを位置なしで呼びました。2D再生します: {id}",
                this
            );
        }

        PlaySeInternal(id, data, clip, SePlayMode.TwoDimensional, Vector3.zero, null);
    }

    public void PlaySe(CSE_SeId id, Vector3 position)
    {
        if (!TryGetPlayableSeData(id, out CS_SeData data, out AudioClip clip))
            return;

        SePlayMode mode = data.is3D
            ? SePlayMode.ThreeDimensionalAtPosition
            : SePlayMode.TwoDimensional;

        PlaySeInternal(id, data, clip, mode, position, null);
    }

    public void PlaySe(CSE_SeId id, Transform followTransform)
    {
        if (followTransform == null)
        {
            Debug.LogWarning($"[CS_SoundManager] 追従先がnullです: {id}", this);
            return;
        }

        if (!TryGetPlayableSeData(id, out CS_SeData data, out AudioClip clip))
            return;

        SePlayMode mode = data.is3D
            ? SePlayMode.ThreeDimensionalFollow
            : SePlayMode.TwoDimensional;

        PlaySeInternal(id, data, clip, mode, followTransform.position, followTransform);
    }

    private bool TryGetPlayableSeData(
        CSE_SeId id,
        out CS_SeData data,
        out AudioClip clip)
    {
        data = null;
        clip = null;

        if (id == CSE_SeId.None)
            return false;

        if (_seDatabase == null)
        {
            Debug.LogError("[CS_SoundManager] SEデータベースが未設定です。", this);
            return false;
        }

        if (!_seDatabase.TryGetData(id, out data))
        {
            Debug.LogWarning($"[CS_SoundManager] SEが未登録です: {id}", this);
            return false;
        }

        clip = data.GetRandomClip();
        if (clip != null)
            return true;

        Debug.LogWarning($"[CS_SoundManager] AudioClipが未設定です: {id}", this);
        return false;
    }

    private void PlaySeInternal(
        CSE_SeId id,
        CS_SeData data,
        AudioClip clip,
        SePlayMode mode,
        Vector3 position,
        Transform followTransform)
    {
        EnforceConcurrentLimit(id, data.maxConcurrent);
        SeSourceEntry entry = GetAvailableEntry();
        if (entry == null)
            return;

        PrepareEntryTransform(entry, mode, position, followTransform);
        ConfigureSeAudioSource(entry.audioSource, data, clip, mode);
        entry.SetPlayingData(id, Time.unscaledTimeAsDouble);
        entry.audioSource.Play();
    }

    private void PrepareEntryTransform(
        SeSourceEntry entry,
        SePlayMode mode,
        Vector3 position,
        Transform followTransform)
    {
        Transform sourceTransform = entry.audioSource.transform;

        if (mode == SePlayMode.ThreeDimensionalFollow)
        {
            sourceTransform.SetParent(followTransform, false);
            sourceTransform.localPosition = Vector3.zero;
        }
        else if (mode == SePlayMode.ThreeDimensionalAtPosition)
        {
            sourceTransform.SetParent(transform, true);
            sourceTransform.position = position;
        }
        else
        {
            sourceTransform.SetParent(transform, false);
            sourceTransform.localPosition = Vector3.zero;
        }

        sourceTransform.localRotation = Quaternion.identity;
    }

    private static void ConfigureSeAudioSource(
        AudioSource source,
        CS_SeData data,
        AudioClip clip,
        SePlayMode mode)
    {
        source.Stop();
        source.clip = clip;
        source.volume = data.volume;
        source.pitch = data.GetRandomPitch();
        source.loop = false;

        bool is3D = mode != SePlayMode.TwoDimensional;
        source.spatialBlend = is3D ? 1f : 0f;

        if (!is3D)
            return;

        source.minDistance = data.minDistance;
        source.maxDistance = Mathf.Max(data.minDistance, data.maxDistance);
        source.rolloffMode = data.rolloff;
        source.dopplerLevel = 0f;
    }

    private void EnforceConcurrentLimit(CSE_SeId id, int maxConcurrent)
    {
        int count = 0;
        SeSourceEntry oldest = null;

        foreach (SeSourceEntry entry in _seSourcePool)
        {
            if (!entry.audioSource.isPlaying || entry.playingSeId != id)
                continue;

            count++;
            if (oldest == null || entry.startedTime < oldest.startedTime)
                oldest = entry;
        }

        if (count >= Mathf.Max(1, maxConcurrent))
            StopEntry(oldest);
    }

    private SeSourceEntry GetAvailableEntry()
    {
        foreach (SeSourceEntry entry in _seSourcePool)
        {
            if (!entry.audioSource.isPlaying)
            {
                ResetFinishedEntry(entry);
                return entry;
            }
        }

        SeSourceEntry oldest = null;
        foreach (SeSourceEntry entry in _seSourcePool)
        {
            if (oldest == null || entry.startedTime < oldest.startedTime)
                oldest = entry;
        }

        StopEntry(oldest);
        return oldest;
    }

    private void StopEntry(SeSourceEntry entry)
    {
        if (entry == null)
            return;

        entry.audioSource.Stop();
        ResetEntry(entry);
    }

    private void ResetFinishedEntry(SeSourceEntry entry)
    {
        if (!entry.audioSource.isPlaying)
            ResetEntry(entry);
    }

    private void ResetEntry(SeSourceEntry entry)
    {
        AudioSource source = entry.audioSource;
        source.clip = null;
        source.loop = false;
        source.spatialBlend = 0f;
        source.pitch = 1f;
        source.volume = 1f;
        source.outputAudioMixerGroup = _seMixerGroup;
        source.transform.SetParent(transform, false);
        source.transform.localPosition = Vector3.zero;
        source.transform.localRotation = Quaternion.identity;
        entry.ClearPlayingData();
    }

    public void StopAllSe()
    {
        foreach (SeSourceEntry entry in _seSourcePool)
            StopEntry(entry);
    }

    // =========================================================
    // BGM
    // =========================================================

    public void PlayBgm(CSE_BgmId id, float fadeTime = -1f)
    {
        if (id == CSE_BgmId.None)
        {
            StopBgm(fadeTime);
            return;
        }

        if (_bgmDatabase == null ||
            !_bgmDatabase.TryGetData(id, out CS_BgmData data) ||
            data.clip == null)
        {
            Debug.LogWarning($"[CS_SoundManager] BGMを再生できません: {id}", this);
            return;
        }

        if (_currentBgmId == id &&
            _currentBgmSource != null &&
            _currentBgmSource.isPlaying)
        {
            return;
        }

        StopCurrentBgmCoroutine();
        _bgmFadeCoroutine = StartCoroutine(
            CrossFadeBgm(id, data, GetFadeTime(fadeTime))
        );
    }

    private IEnumerator CrossFadeBgm(
        CSE_BgmId id,
        CS_BgmData data,
        float fadeTime)
    {
        AudioSource oldSource = _currentBgmSource;
        AudioSource newSource = _nextBgmSource;

        newSource.Stop();
        newSource.clip = data.clip;
        newSource.loop = data.loop;
        newSource.volume = 0f;
        newSource.outputAudioMixerGroup = _bgmMixerGroup;
        newSource.Play();

        float oldVolume = oldSource != null ? oldSource.volume : 0f;
        float targetVolume = data.volume;
        _currentBgmDataVolume = targetVolume;

        if (fadeTime > 0f)
        {
            float elapsed = 0f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.unscaledDeltaTime;
                float rate = Mathf.Clamp01(elapsed / fadeTime);

                if (oldSource != null)
                    oldSource.volume = Mathf.Lerp(oldVolume, 0f, rate);

                newSource.volume = Mathf.Lerp(0f, targetVolume, rate);
                yield return null;
            }
        }

        StopAndClearBgmSource(oldSource);
        newSource.volume = targetVolume;
        _currentBgmSource = newSource;
        _nextBgmSource = oldSource;
        _currentBgmId = id;
        _bgmFadeCoroutine = null;
    }

    public void StopBgm(float fadeTime = -1f)
    {
        StopCurrentBgmCoroutine();
        _bgmFadeCoroutine = StartCoroutine(
            FadeOutBgm(GetFadeTime(fadeTime))
        );
    }

    private IEnumerator FadeOutBgm(float fadeTime)
    {
        AudioSource source = _currentBgmSource;
        if (source == null || !source.isPlaying)
        {
            _currentBgmId = CSE_BgmId.None;
            _bgmFadeCoroutine = null;
            yield break;
        }

        float startVolume = source.volume;
        if (fadeTime > 0f)
        {
            float elapsed = 0f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(
                    startVolume,
                    0f,
                    Mathf.Clamp01(elapsed / fadeTime)
                );
                yield return null;
            }
        }

        StopAndClearBgmSource(source);
        _currentBgmId = CSE_BgmId.None;
        _currentBgmDataVolume = 1f;
        _bgmFadeCoroutine = null;
    }

    private float GetFadeTime(float requested)
    {
        return requested < 0f
            ? _defaultBgmFadeTime
            : Mathf.Max(0f, requested);
    }

    private void StopCurrentBgmCoroutine()
    {
        if (_bgmFadeCoroutine == null)
            return;

        StopCoroutine(_bgmFadeCoroutine);
        _bgmFadeCoroutine = null;
    }

    private static void StopAndClearBgmSource(AudioSource source)
    {
        if (source == null)
            return;

        source.Stop();
        source.clip = null;
        source.volume = 0f;
    }

    // =========================================================
    // ジングル
    // =========================================================

    /// <summary>
    /// ジングルを再生する。再生中のジングルがあれば置き換える。
    /// </summary>
    public void PlayJingle(CSE_JingleId id)
    {
        if (id == CSE_JingleId.None)
        {
            StopJingle();
            return;
        }

        if (_jingleDatabase == null ||
            !_jingleDatabase.TryGetData(id, out CS_JingleData data) ||
            data.clip == null)
        {
            Debug.LogWarning($"[CS_SoundManager] ジングルを再生できません: {id}", this);
            return;
        }

        StopJingleInternal(false);

        _currentJingleId = id;
        _jingleSource.clip = data.clip;
        _jingleSource.volume = data.volume;
        _jingleSource.loop = false;
        _jingleSource.outputAudioMixerGroup = _jingleMixerGroup;
        _jingleSource.Play();

        StartBgmDuck(data.bgmDuckVolume, data.duckFadeTime);
        _jingleCoroutine = StartCoroutine(
            WaitForJingleEnd(data.duckFadeTime)
        );
    }

    public void StopJingle()
    {
        StopJingleInternal(true);
    }

    private void StopJingleInternal(bool restoreBgm)
    {
        if (_jingleCoroutine != null)
        {
            StopCoroutine(_jingleCoroutine);
            _jingleCoroutine = null;
        }

        if (_jingleSource != null)
        {
            _jingleSource.Stop();
            _jingleSource.clip = null;
            _jingleSource.volume = 0f;
        }

        _currentJingleId = CSE_JingleId.None;

        if (restoreBgm)
            StartBgmDuck(1f, 0.3f);
    }

    private IEnumerator WaitForJingleEnd(float restoreFadeTime)
    {
        while (_jingleSource != null && _jingleSource.isPlaying)
            yield return null;

        if (_jingleSource != null)
        {
            _jingleSource.clip = null;
            _jingleSource.volume = 0f;
        }

        _currentJingleId = CSE_JingleId.None;
        _jingleCoroutine = null;
        StartBgmDuck(1f, restoreFadeTime);
    }

    private void StartBgmDuck(float volumeRate, float fadeTime)
    {
        if (_duckCoroutine != null)
            StopCoroutine(_duckCoroutine);

        _duckCoroutine = StartCoroutine(
            FadeBgmForDuck(Mathf.Clamp01(volumeRate), Mathf.Max(0f, fadeTime))
        );
    }

    private IEnumerator FadeBgmForDuck(float volumeRate, float fadeTime)
    {
        AudioSource source = _currentBgmSource;
        if (source == null || !source.isPlaying)
        {
            _duckCoroutine = null;
            yield break;
        }

        float startVolume = source.volume;
        float targetVolume = _currentBgmDataVolume * volumeRate;

        if (fadeTime > 0f)
        {
            float elapsed = 0f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(
                    startVolume,
                    targetVolume,
                    Mathf.Clamp01(elapsed / fadeTime)
                );
                yield return null;
            }
        }

        source.volume = targetVolume;
        _duckCoroutine = null;
    }

    // =========================================================
    // 終了処理
    // =========================================================

    private void OnDestroy()
    {
        if (_instance != this)
            return;

        StopCurrentBgmCoroutine();

        if (_duckCoroutine != null)
            StopCoroutine(_duckCoroutine);

        StopJingleInternal(false);
        StopAndClearBgmSource(_bgmSourceA);
        StopAndClearBgmSource(_bgmSourceB);
        StopAllSe();

        _seSourcePool.Clear();
        _instance = null;
    }

    private enum SePlayMode
    {
        TwoDimensional,
        ThreeDimensionalAtPosition,
        ThreeDimensionalFollow,
    }

    private class SeSourceEntry
    {
        private readonly AudioSource _audioSource;
        private CSE_SeId _playingSeId;
        private double _startedTime;

        public AudioSource audioSource => _audioSource;
        public CSE_SeId playingSeId => _playingSeId;
        public double startedTime => _startedTime;

        public SeSourceEntry(AudioSource audioSource)
        {
            _audioSource = audioSource;
            ClearPlayingData();
        }

        public void SetPlayingData(CSE_SeId id, double startedTime)
        {
            _playingSeId = id;
            _startedTime = startedTime;
        }

        public void ClearPlayingData()
        {
            _playingSeId = CSE_SeId.None;
            _startedTime = 0d;
        }
    }
}
