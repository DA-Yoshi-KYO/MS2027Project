/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-17 | 初回作成
 * ================================================ */

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// エリアごとの犯罪完遂率と現在地を保持するModel。
///
/// ・画面に1つだけ存在する
/// ・Bind()でPresenterへ公開する
/// ・完遂率は0～100で保持する
/// ・完遂率と現在地の変化を通知する
/// </summary>
public sealed class CS_UICrimeProgressModel :
    CS_BaseModel
{
    // =========================================================
    // 公開中のModel
    // =========================================================

    private static CS_UICrimeProgressModel _current;

    public static event Action<
        CS_UICrimeProgressModel
    > OnBound;

    public static event Action OnUnbound;

    /// <summary>
    /// 現在公開されているModelを取得する。
    /// </summary>
    public static bool TryGet(
        out CS_UICrimeProgressModel model)
    {
        model = _current;

        return model != null;
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _current = null;
        OnBound = null;
        OnUnbound = null;
    }

    // =========================================================
    // 状態
    // =========================================================

    private readonly Dictionary<
        CSE_CrimeAreaType,
        float
    > _progressValues =
        new Dictionary<CSE_CrimeAreaType, float>();

    private CSE_CrimeAreaType _currentArea;
    private bool _hasCurrentArea;

    // =========================================================
    // イベント
    // =========================================================

    public event Action<
        CSE_CrimeAreaType,
        float
    > OnProgressChanged;

    public event Action<
        CSE_CrimeAreaType
    > OnCurrentAreaChanged;

    // =========================================================
    // 公開プロパティ
    // =========================================================

    public CSE_CrimeAreaType currentArea =>
        _currentArea;

    public bool hasCurrentArea =>
        _hasCurrentArea;

    // =========================================================
    // コンストラクター
    // =========================================================

    /// <summary>
    /// 初期エリアを指定して生成する。
    /// </summary>
    public CS_UICrimeProgressModel(
        CSE_CrimeAreaType initialArea)
    {
        InitializeProgressValues();

        _currentArea = initialArea;
        _hasCurrentArea = true;
    }

    /// <summary>
    /// 現在地が未確定の状態で生成する。
    /// </summary>
    public CS_UICrimeProgressModel()
    {
        InitializeProgressValues();

        _hasCurrentArea = false;
    }

    // =========================================================
    // 初期化
    // =========================================================

    private void InitializeProgressValues()
    {
        Array areaValues =
            Enum.GetValues(
                typeof(CSE_CrimeAreaType)
            );

        foreach (
            CSE_CrimeAreaType areaType
            in areaValues)
        {
            _progressValues[areaType] = 0f;
        }
    }

    // =========================================================
    // Bind / Unbind
    // =========================================================

    public void Bind()
    {
        if (_current == this)
        {
            return;
        }

        if (_current != null)
        {
            _current.Unbind();
        }

        _current = this;

        OnBound?.Invoke(this);
    }

    public void Unbind()
    {
        if (_current != this)
        {
            return;
        }

        _current = null;

        OnUnbound?.Invoke();
    }

    // =========================================================
    // 完遂率
    // =========================================================

    /// <summary>
    /// 指定エリアの完遂率を取得する。
    /// </summary>
    public float GetProgress(
        CSE_CrimeAreaType areaType)
    {
        if (_progressValues.TryGetValue(
                areaType,
                out float progress))
        {
            return progress;
        }

        return 0f;
    }

    /// <summary>
    /// 指定エリアの完遂率を設定する。
    /// </summary>
    public void SetProgress(
        CSE_CrimeAreaType areaType,
        float progress)
    {
        float clampedProgress =
            Mathf.Clamp(
                progress,
                0f,
                100f
            );

        float previousProgress =
            GetProgress(areaType);

        if (Mathf.Approximately(
                previousProgress,
                clampedProgress))
        {
            return;
        }

        _progressValues[areaType] =
            clampedProgress;

        OnProgressChanged?.Invoke(
            areaType,
            clampedProgress
        );
    }

    /// <summary>
    /// 指定エリアの完遂率を加算する。
    /// </summary>
    public void AddProgress(
        CSE_CrimeAreaType areaType,
        float amount)
    {
        float newProgress =
            GetProgress(areaType) +
            amount;

        SetProgress(
            areaType,
            newProgress
        );
    }

    // =========================================================
    // 現在地
    // =========================================================

    /// <summary>
    /// プレイヤーが現在いるエリアを変更する。
    /// </summary>
    public void SetCurrentArea(
        CSE_CrimeAreaType areaType)
    {
        if (_hasCurrentArea &&
            _currentArea == areaType)
        {
            return;
        }

        _currentArea = areaType;
        _hasCurrentArea = true;

        OnCurrentAreaChanged?.Invoke(
            areaType
        );
    }

    // =========================================================
    // Dispose
    // =========================================================

    public override void Dispose()
    {
        Unbind();

        OnProgressChanged = null;
        OnCurrentAreaChanged = null;

        _progressValues.Clear();

        _hasCurrentArea = false;
    }
}