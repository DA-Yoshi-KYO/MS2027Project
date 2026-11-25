using System.Collections.Generic;
using UnityEngine;

/*
 * プレイヤーの演出(ヒットストップ・殲滅スロー・必殺技のカットイン)で、ゲームの時間をゆっくりにするクラス
 * 複数の演出が同時に頼んでも、一番遅い速さを使い、全部終わったら元の速さ(1)に戻す
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・ソロだけで時間を変える(CS_GameModeStore.isSolo)。マルチではサーバー(ホスト)の時間を変えると全員の世界が変わるため、何もしない
 *   (マルチの演出は、カメラ・Animator・画面効果だけで行う)
 * ・Request(頼む側, 速さ, 秒数): 秒数はTime.timeScaleの影響を受けない時間で数える
 *   同じ頼む側がもう一度頼んだら、上書きする
 * ・他のクラスが直接Time.timeScaleを変えている間(スタート時のカメラの演出で0にしている間など)は、上書きしない
 *   (自分が最後に入れた値と違う時は、他のクラスが変えたとみなして触らない)
 * ・実行用のオブジェクトは最初に頼まれた時に作る(DontDestroyOnLoad)
 */
// ========================================

public class CS_PlayerTimeScale : MonoBehaviour
{
    private static CS_PlayerTimeScale _instance;

    private readonly Dictionary<object, (float scale, float endTime)> _requests = new Dictionary<object, (float scale, float endTime)>();
    private readonly List<object> _expired = new List<object>();
    private float _appliedScale = 1f;   // 自分が最後に入れた速さ

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
    }

    // ゲームの時間をduration秒(Time.timeScaleの影響を受けない時間)だけscale倍にする(ソロのみ)
    public static void Request(object requester, float scale, float duration)
    {
        if (!CS_GameModeStore.isSolo || requester == null || duration <= 0f) return;

        if (_instance == null)
        {
            GameObject runner = new GameObject("PlayerTimeScale");
            DontDestroyOnLoad(runner);
            _instance = runner.AddComponent<CS_PlayerTimeScale>();
        }

        _instance._requests[requester] = (Mathf.Clamp01(scale), Time.unscaledTime + duration);
        _instance.Apply();
    }

    // 頼んでいた分を取り消す
    public static void Cancel(object requester)
    {
        if (_instance == null || requester == null) return;
        if (_instance._requests.Remove(requester)) _instance.Apply();
    }

    private void Update()
    {
        if (_requests.Count == 0) return;

        _expired.Clear();
        foreach (KeyValuePair<object, (float scale, float endTime)> pair in _requests)
        {
            if (Time.unscaledTime >= pair.Value.endTime) _expired.Add(pair.Key);
        }
        foreach (object key in _expired) _requests.Remove(key);

        if (_expired.Count > 0) Apply();
    }

    private void OnDestroy()
    {
        _requests.Clear();
        Apply();
    }

    // 頼まれている中で一番遅い速さにする(無ければ1に戻す)
    private void Apply()
    {
        // 他のクラスがTime.timeScaleを変えている間は触らない
        if (!Mathf.Approximately(Time.timeScale, _appliedScale)) return;

        float scale = 1f;
        foreach ((float requested, float _) in _requests.Values) scale = Mathf.Min(scale, requested);

        _appliedScale = scale;
        Time.timeScale = scale;
    }
}
