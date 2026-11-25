using System.Collections.Generic;
using TMPro;
using UnityEngine;

/*
 * プレイヤーをサポートするAIガイドの会話(テキスト)を出すクラス
 * 仕様はConfluence「名称求(AIガイド)」。スタート直後・エリアの攻略の前後は決まったセリフ、それ以外は状況に応じたセリフを出す
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・自分の画面のプレイヤー(CS_Player.isLocalPlayer)だけで動く。自分の画面だけのものなので通信はしない
 * ・キャラクターのモデルはまだ無いので、テキストだけ(会話ウィンドウはCS_PlayerAIGuideWindow。仮の見た目)
 * ・固定会話(仕様「固定会話」)
 *   スタート直後: ゲームが始まったら(スタート時のカメラの演出が終わってから)、_storyInterval秒(既定7秒)おきに①〜④を出す
 *     ①「まずは生活エリアの犯罪を防いで」はソロのエリアの順番の話なので、マルチでは出さない
 *   エリアの攻略の前後(ソロのみ。CS_SoloAreaProgressionのイベント)
 *     生活エリアを守れた → ⑤ / エネルギーエリアが始まった → ⑥ / エネルギーエリアを守れた → ⑦
 *     インフラエリアが始まった → ⑧ / インフラエリアを守れた → ⑨(タワーの復旧)
 *     守れなかったエリアでは⑤⑦⑨は出さない(次のエリアの⑥⑧は出す)
 * ・ランダム会話(仕様「ランダム会話」)
 *   悪人を_hintTime秒(既定20秒)見ていない・倒していない → そのエリアのヒント(エリアごとに定型文。_downtownHintなど。○○の部分は仮)
 *     スタート直後の説明(①〜④)を出し終えるまでは出さない(説明の途中に割り込まないように)
 *     見ている: 生きている悪人が_sightRange(m)以内で、カメラの画面の中にいる(壁で隠れているかは見ない)
 *     倒した: 自分から_nearRange(m)以内の悪人が倒れた(誰が倒したかは見ない。各マシンで同期済みのHPから分かるため)
 *   悪人のグループを倒し終わった → 褒める(グループの最後の1人が、自分から_nearRange以内で倒れた時)
 *   警察陣営に見られている(捜索中。CSE_PlayerAlertState.Searching) → 「見られてるよ！」
 *   警察陣営に追われている(発見中。CSE_PlayerAlertState.Detected) → 「バレちゃった！」
 * ・出す順番: 警察(急ぎ) > 固定会話 > その他。急ぎのセリフは、出している途中のセリフを止めてすぐに出す
 *   止められた固定会話は、あとでもう一度出す。その他のセリフは捨てる
 *   セリフとセリフの間は_gapTime秒あける。同じ種類のランダム会話は_randomCooldown秒は繰り返さない(警察は状態が変わるたびに出す)
 * ・時間はTime.timeScaleの影響を受けない時間で数える(ただし「悪人を見ていない時間」はゲームの時間で数える)
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
public class CS_PlayerAIGuide : MonoBehaviour
{
    private enum Priority
    {
        Chatter = 0,   // ヒント・褒める
        Story = 1,     // 固定会話
        Urgent = 2,    // 警察
    }

    private struct Line
    {
        public string text;
        public Priority priority;
    }

    [Header("会話ウィンドウ")]
    [SerializeField] private TMP_FontAsset _font;                     // 日本語の入ったフォント(BIZUDGothic-Bold SDF)
    [SerializeField] private string _speakerName = "AIガイド";        // ウィンドウに出す名前(仕様で名前が決まったら変える)
    [SerializeField][Min(0.5f)] private float _holdTime = 3.5f;       // 本文を出し終わってから消えるまでの時間(秒)
    [SerializeField][Min(0f)] private float _gapTime = 0.8f;         // セリフとセリフの間(秒)

    [Header("固定会話(スタート直後)")]
    [SerializeField][Min(0f)] private float _firstDelay = 1.5f;       // ゲームが始まってから①を出すまで(秒)
    [SerializeField][Min(1f)] private float _storyInterval = 7f;      // ①〜④の間隔(秒。仕様: 5〜10秒刻み)
    [SerializeField][TextArea] private string _startLine1 = "警備に見つからないように、まずは生活エリアの犯罪を防いで！";
    [SerializeField][TextArea] private string _startLine2 = "悪人を探し出すには、私のスキャン機能を活用してね！";
    [SerializeField][TextArea] private string _startLine3 = "ステルスアクションを使えば、バレずにかっこいい移動ができるね！";
    [SerializeField][TextArea] private string _startLine4 = "悪人の頭上や背中側から、ド派手に奇襲を決めちゃえ！";

    [Header("固定会話(エリアの攻略の前後。ソロのみ)")]
    [SerializeField][TextArea] private string _downtownClearedLine = "生活エリアの犯罪阻止を確認！凄い順調だね！あと2エリア！";
    [SerializeField][TextArea] private string _energyStartLine = "次はエネルギーエリアの犯罪を防ご～";
    [SerializeField][TextArea] private string _energyClearedLine = "エネルギーエリアの犯罪も確認！残るエリアは１つ！やっちゃえ！";
    [SerializeField][TextArea] private string _infraStartLine = "インフラエリアの攻略開始！君ならできる！";
    [SerializeField][TextArea] private string _infraClearedLine = "君のおかげでEAGISタワーが復旧したよ！お疲れ様！\n悪人を倒すことのできる君たちだから、守れた平和だね！";

    [Header("ランダム会話")]
    [SerializeField][Min(1f)] private float _hintTime = 20f;          // 悪人を見ていない・倒していない時間がこれを超えたらヒントを出す(秒)
    [SerializeField][Min(1f)] private float _sightRange = 40f;        // 悪人が見えているとみなす距離(m)
    [SerializeField][Min(1f)] private float _nearRange = 20f;         // 自分の近くで悪人が倒れたとみなす距離(m)
    [SerializeField][Min(0f)] private float _randomCooldown = 8f;     // 同じ種類のランダム会話を繰り返さない時間(秒)
    [SerializeField][TextArea] private string _downtownHint = "このエリアは路地裏に悪人が潜んでそうだよー";     // ○○の部分は仮
    [SerializeField][TextArea] private string _energyHint = "このエリアは発電所の物陰に悪人が潜んでそうだよー"; // ○○の部分は仮
    [SerializeField][TextArea] private string _infraHint = "このエリアは高い場所に悪人が潜んでそうだよー";       // ○○の部分は仮
    [SerializeField][TextArea] private string _groupDefeatedLine = "クールに決めたね！次々ー！";
    [SerializeField][TextArea] private string _searchedLine = "見られてるよ！落ち着いて距離を置いて！";
    [SerializeField][TextArea] private string _detectedLine = "バレちゃった！ステルスアクションで逃げてー！";

    private const float _scanInterval = 0.25f;     // 悪人を調べる間隔(秒)
    private const float _defeatMemoryTime = 2f;    // グループが倒し終わった時、最後に倒れた悪人を覚えておく時間(秒)

    private class GroupInfo
    {
        public int aliveCount;          // 今回数えた、生きているメンバーの数
        public int previousAliveCount;  // 前回数えた数
        public float lastNearDefeatTime = float.MinValue;   // 自分の近くでメンバーが倒れた時刻(実時間)
    }

    private CS_Player _player;
    private CS_PlayerAlert _alert;
    private CS_PlayerAIGuideWindow _window;

    private readonly List<Line> _queue = new List<Line>();
    private bool _hasCurrent;
    private Line _current;
    private float _gapRemaining;

    private bool _hasStarted;                  // ゲームが始まったか(スタート直後の固定会話を始めたか)
    private int _startLineIndex;               // 次に出すスタート直後のセリフ(0〜)
    private float _nextStartLineTime;          // それを出す時刻(実時間)

    private CS_SoloAreaProgression _progression;   // 購読しているソロのエリアの進行(マルチではnull)

    private float _nextScanTime;
    private float _lastActivityTime;           // 最後に悪人を見た・倒した時刻(ゲームの時間)
    private float _lastHintTime = float.MinValue;
    private float _lastPraiseTime = float.MinValue;
    private readonly Dictionary<int, GroupInfo> _groups = new Dictionary<int, GroupInfo>();
    private readonly HashSet<CS_VillainHealth> _aliveVillains = new HashSet<CS_VillainHealth>();   // 前回生きていた悪人
    private readonly HashSet<CS_VillainHealth> _aliveNow = new HashSet<CS_VillainHealth>();

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _alert = GetComponent<CS_PlayerAlert>();
    }

    private void OnEnable()
    {
        if (_alert != null) _alert.onAlertChanged += HandleAlertChanged;
    }

    private void OnDisable()
    {
        if (_alert != null) _alert.onAlertChanged -= HandleAlertChanged;
        SetProgression(null);
        if (_window != null) _window.Hide();
        _hasCurrent = false;
    }

    private void OnDestroy()
    {
        if (_window != null) Destroy(_window.gameObject);
    }

    private void Update()
    {
        if (!_player.isLocalPlayer) return;

        if (!_hasStarted)
        {
            // ゲームが始まるまで(スタート時のカメラの演出・マルチで全員を待つ間)は話さない
            if (!_player.isControlled || _player.isPlayingIntro || CS_PlayerIntroCamera.isMatchHeld) return;
            StartGuide();
        }

        if (_progression == null) SetProgression(CS_SoloAreaProgression.instance);

        UpdateStartLines();
        UpdateVillains();
        UpdateHint();
        UpdateWindow();
    }

    // ゲームが始まった時(一度だけ)
    private void StartGuide()
    {
        _hasStarted = true;
        _startLineIndex = CS_GameModeStore.isSolo ? 0 : 1;   // ①はソロのエリアの順番の話なので、マルチでは出さない
        _nextStartLineTime = Time.unscaledTime + _firstDelay;
        _lastActivityTime = Time.time;
        if (_window == null) _window = CS_PlayerAIGuideWindow.Create(_font, _speakerName);
    }

    // ========================================
    // 固定会話
    // ========================================

    private void UpdateStartLines()
    {
        if (_startLineIndex >= 4 || Time.unscaledTime < _nextStartLineTime) return;

        Enqueue(GetStartLine(_startLineIndex), Priority.Story);
        _startLineIndex++;
        _nextStartLineTime = Time.unscaledTime + _storyInterval;

        // スタート直後の説明を出し終えてから、ヒントまでの時間を数え始める
        if (_startLineIndex >= 4) _lastActivityTime = Time.time;
    }

    private string GetStartLine(int index)
    {
        switch (index)
        {
            case 0: return _startLine1;
            case 1: return _startLine2;
            case 2: return _startLine3;
            default: return _startLine4;
        }
    }

    private void SetProgression(CS_SoloAreaProgression progression)
    {
        if (_progression == progression) return;

        if (_progression != null)
        {
            _progression.onAreaCleared -= HandleAreaCleared;
            _progression.onAreaChanged -= HandleAreaChanged;
        }
        _progression = progression;
        if (_progression != null)
        {
            _progression.onAreaCleared += HandleAreaCleared;
            _progression.onAreaChanged += HandleAreaChanged;
        }
    }

    private void HandleAreaCleared(CS_CrimeArea area)
    {
        if (area == null) return;

        switch (area.areaType)
        {
            case CSE_CrimeAreaType.Downtown: Enqueue(_downtownClearedLine, Priority.Story); break;
            case CSE_CrimeAreaType.Energy: Enqueue(_energyClearedLine, Priority.Story); break;
            case CSE_CrimeAreaType.Infrastructure: Enqueue(_infraClearedLine, Priority.Story); break;
        }
    }

    private void HandleAreaChanged(CS_CrimeArea area)
    {
        _lastActivityTime = Time.time;   // 新しいエリアに入ったら、ヒントまでの時間を数え直す
        if (area == null) return;

        switch (area.areaType)
        {
            case CSE_CrimeAreaType.Energy: Enqueue(_energyStartLine, Priority.Story); break;
            case CSE_CrimeAreaType.Infrastructure: Enqueue(_infraStartLine, Priority.Story); break;
        }
    }

    // ========================================
    // ランダム会話
    // ========================================

    // 警察陣営に見られた・追われた時
    private void HandleAlertChanged(CSE_PlayerAlertState state)
    {
        if (!_hasStarted || !_player.isLocalPlayer) return;

        if (state == CSE_PlayerAlertState.Detected) Enqueue(_detectedLine, Priority.Urgent);
        else if (state == CSE_PlayerAlertState.Searching) Enqueue(_searchedLine, Priority.Urgent);
    }

    // 悪人を見た・近くで倒れたかを調べ、グループを倒し終わったら褒める
    private void UpdateVillains()
    {
        if (Time.unscaledTime < _nextScanTime) return;
        _nextScanTime = Time.unscaledTime + _scanInterval;

        Vector3 position = transform.position;
        Camera viewCamera = _player.playerCamera;
        float sqrSight = _sightRange * _sightRange;
        float sqrNear = _nearRange * _nearRange;

        foreach (GroupInfo group in _groups.Values)
        {
            group.previousAliveCount = group.aliveCount;
            group.aliveCount = 0;
        }

        _aliveNow.Clear();
        foreach (CS_VillainHealth villain in CS_NpcWorld.villains)
        {
            if (villain == null || !villain.isActiveAndEnabled) continue;

            int groupId = villain.TryGetComponent(out CS_VillainCrime crime) ? crime.groupId : -1;
            float sqrDistance = (villain.transform.position - position).sqrMagnitude;
            bool isAlive = !villain.isDefeated && villain.currentHp > 0f;

            if (isAlive)
            {
                _aliveNow.Add(villain);
                if (groupId >= 0) GetGroup(groupId).aliveCount++;
                if (sqrDistance <= sqrSight && IsOnScreen(viewCamera, villain.transform.position)) _lastActivityTime = Time.time;
                continue;
            }

            // 前回生きていた悪人が、自分の近くで倒れた
            if (_aliveVillains.Contains(villain) && sqrDistance <= sqrNear)
            {
                _lastActivityTime = Time.time;
                if (groupId >= 0) GetGroup(groupId).lastNearDefeatTime = Time.unscaledTime;
            }
        }

        _aliveVillains.Clear();
        _aliveVillains.UnionWith(_aliveNow);

        // グループの最後の1人が、自分の近くで倒れた
        foreach (GroupInfo group in _groups.Values)
        {
            if (group.previousAliveCount <= 0 || group.aliveCount > 0) continue;
            if (Time.unscaledTime - group.lastNearDefeatTime > _defeatMemoryTime) continue;
            if (Time.unscaledTime - _lastPraiseTime < _randomCooldown) continue;

            _lastPraiseTime = Time.unscaledTime;
            Enqueue(_groupDefeatedLine, Priority.Chatter);
        }
    }

    // 悪人をしばらく見ていない・倒していなければ、今のエリアのヒントを出す
    private void UpdateHint()
    {
        if (_startLineIndex < 4) return;   // スタート直後の説明の間はヒントを出さない
        if (Time.time - _lastActivityTime < _hintTime) return;
        if (Time.unscaledTime - _lastHintTime < _randomCooldown) return;
        if (_alert != null && _alert.isAlerted) return;   // 警察に追われている間は、逃げる方を優先する

        _lastActivityTime = Time.time;
        _lastHintTime = Time.unscaledTime;
        Enqueue(GetHint(), Priority.Chatter);
    }

    // 今のエリアのヒント(マルチなどエリアが分からない時は、生活エリアのもの)
    private string GetHint()
    {
        CS_CrimeArea area = _progression != null ? _progression.currentArea : null;
        CSE_CrimeAreaType type = area != null ? area.areaType : CSE_CrimeAreaType.Downtown;
        switch (type)
        {
            case CSE_CrimeAreaType.Energy: return _energyHint;
            case CSE_CrimeAreaType.Infrastructure: return _infraHint;
            default: return _downtownHint;
        }
    }

    private GroupInfo GetGroup(int groupId)
    {
        if (!_groups.TryGetValue(groupId, out GroupInfo group))
        {
            group = new GroupInfo();
            _groups.Add(groupId, group);
        }
        return group;
    }

    private static bool IsOnScreen(Camera viewCamera, Vector3 worldPosition)
    {
        if (viewCamera == null) return false;

        Vector3 viewport = viewCamera.WorldToViewportPoint(worldPosition);
        return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
    }

    // ========================================
    // 会話ウィンドウへ出す
    // ========================================

    private void Enqueue(string text, Priority priority)
    {
        if (string.IsNullOrEmpty(text)) return;

        Line line = new Line { text = text, priority = priority };

        // 急ぎのセリフは、出している途中のセリフを止めてすぐに出す(止めた固定会話は、あとでもう一度出す)
        if (priority == Priority.Urgent && _hasCurrent && _current.priority < Priority.Urgent)
        {
            if (_current.priority == Priority.Story) _queue.Insert(0, _current);
            _hasCurrent = false;
            _gapRemaining = 0f;
            ShowLine(line);
            return;
        }

        // 同じ急ぎのセリフが続けて溜まらないよう、待っている急ぎのセリフは新しいものに置き換える
        if (priority == Priority.Urgent) _queue.RemoveAll(queued => queued.priority == Priority.Urgent);

        _queue.Add(line);
    }

    private void UpdateWindow()
    {
        if (_window == null) return;

        if (_hasCurrent)
        {
            if (_window.isShowing) return;

            _hasCurrent = false;
            _gapRemaining = _gapTime;
        }

        if (_gapRemaining > 0f)
        {
            _gapRemaining -= Time.unscaledDeltaTime;
            return;
        }

        if (_queue.Count == 0) return;

        // 一番優先度の高いもの(同じなら先に入ったもの)を出す
        int best = 0;
        for (int i = 1; i < _queue.Count; i++)
        {
            if (_queue[i].priority > _queue[best].priority) best = i;
        }
        Line line = _queue[best];
        _queue.RemoveAt(best);
        ShowLine(line);
    }

    private void ShowLine(Line line)
    {
        _current = line;
        _hasCurrent = true;
        _window.Show(line.text, _holdTime);
    }
}
