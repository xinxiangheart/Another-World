using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 回合字幕条带：灰色条带背景 + 两种文字模式，纯协程实现（不依赖 DOTween）。
///
/// 场景结构（位置全部在 Scene 中手动摆放，代码不写死坐标）：
///   SubtitleCanvas（Canvas + CanvasScaler）
///     └─ SubtitleBand（Image 灰色条带，覆盖全屏 X，Y/高度手动调；挂本组件）
///          ├─ Mode1Text   （单行：对方回合/攻击回合；其位置即公共锚点）
///          ├─ Mode2Top    （己方回合，场景中摆好停留位）
///          └─ Mode2Bottom （第X阶段，场景中摆好停留位）
///
/// 公共锚点 = Mode1Text 的 anchoredPosition。模式一文字中心在此；
/// 模式二两行文字从该锚点出发，各自移动到停留位置。
///
/// 模式一（后手回合/攻击回合）：单行，淡入 + 微缩(1.05→1.0) → 停留 → 淡出。
/// 模式二（仅先手玩家回合）：两行（"XX回合" + "第X阶段"），淡入 + 从锚点平滑移到停留位 → 停留 → 淡出。
/// 阶段数只在先手玩家的回合显示；后手玩家回合走模式一（单行回合名）。
/// 条带淡入淡出与文字完全同频。
///
/// 自动触发（挂在 TurnManager 阶段变化上，先手判断用 tm.isMyTurnFirst）：
///   先手回合（MyTurn/EnemyTurn）→ 模式二（回合名 + 第X阶段）
///   后手回合（MyTurn/EnemyTurn）→ 模式一（回合名）
///   BattlePhase → 模式一（"攻击回合"）
/// 也可手动调用 PlayMode1 / PlayMode2。
/// </summary>
public class SubtitleBand : MonoBehaviour
{
    [Header("引用（Mode1Text 位置 = 公共锚点）")]
    public TextMeshProUGUI mode1Text;    // 单行：后手回合/攻击回合（模式一）
    public TextMeshProUGUI mode2Top;     // "XX回合"（先手回合，模式二）
    public TextMeshProUGUI mode2Bottom;  // 第X阶段（先手回合）
    public Image bandImage;              // 灰色条带（默认取自身组件）

    [Header("时长（秒）")]
    [Tooltip("淡入时长")]
    public float fadeInDuration = 0.3f;
    [Tooltip("停留时长")]
    public float holdDuration = 1.0f;
    [Tooltip("退场时长")]
    public float fadeOutDuration = 0.4f;

    [Header("模式一：单行缩放")]
    [Tooltip("入场起始缩放")]
    public float mode1StartScale = 1.05f;
    [Tooltip("入场结束缩放（停留/退场保持此值）")]
    public float mode1EndScale = 1.0f;

    [Header("模式二：两行移动")]
    [Tooltip("锚点到停留位置的距离（上/下各此距离，UI 单位）。0 时用场景中摆放的 Mode2Top/Bottom 位置")]
    public float mode2MoveDistance = 40f;

    [Header("条带")]
    [Tooltip("条带颜色（默认灰，alpha 0.6；运行时淡入淡出的是 alpha）")]
    public Color bandColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);

    // ---- 运行时状态 ----
    TurnManager.TurnPhase? _lastPhase;
    Coroutine _routine;
    bool _inited;
    Vector2 _anchor;               // 公共锚点 = Mode1Text 的 anchoredPosition
    Vector2 _topRest, _bottomRest; // 模式二停留位置（场景摆放）
    float _mode1FullAlpha = 1f;    // 各文字淡入的"满 alpha"（= Inspector 里设的基准 alpha，默认 1）
    float _mode2TopFullAlpha = 1f;
    float _mode2BottomFullAlpha = 1f;

    void Awake()
    {
        if (bandImage == null) bandImage = GetComponent<Image>();
    }

    void Start()
    {
        InitPositions();
    }

    void Update()
    {
        // 自动触发：跟随 TurnManager 阶段变化（PhaseStart 与行动阶段合并，不单独触发）
        var tm = TurnManager.Instance;
        if (tm == null) return;
        if (_lastPhase == null) { _lastPhase = tm.currentPhase; return; }
        if (_lastPhase.Value == tm.currentPhase) return;
        _lastPhase = tm.currentPhase;
        OnPhaseChanged(tm.currentPhase);
    }

    void OnPhaseChanged(TurnManager.TurnPhase phase)
    {
        var tm = TurnManager.Instance;
        bool firstMine = tm != null && tm.isMyTurnFirst;
        int phaseNum = tm != null ? tm.phaseCount : 0;
        switch (phase)
        {
            case TurnManager.TurnPhase.MyTurn:
                if (firstMine) PlayMode2("己方回合", phaseNum); // 先手+己方 → 模式二（己方回合 第X阶段）
                else PlayMode1("己方回合");                     // 后手+己方 → 模式一（己方回合）
                break;
            case TurnManager.TurnPhase.EnemyTurn:
                if (!firstMine) PlayMode2("对方回合", phaseNum); // 先手+对方 → 模式二（对方回合 第X阶段）
                else PlayMode1("对方回合");                      // 后手+对方 → 模式一（对方回合）
                break;
            case TurnManager.TurnPhase.BattlePhase:
                PlayMode1("攻击回合");
                break;
        }
    }

    // ================= 公共 API =================

    /// <summary>模式一：单行。淡入 + 微缩 → 停留 → 淡出。</summary>
    public void PlayMode1(string text)
    {
        if (mode1Text == null) return;
        InitPositions();
        mode1Text.text = text;
        PlayRoutine(Mode1Routine());
    }

    /// <summary>模式二（仅先手玩家回合）：两行。"XX回合" + "第X阶段"。</summary>
    public void PlayMode2(string topText, int phaseNumber)
    {
        if (mode2Top == null || mode2Bottom == null) return;
        InitPositions();
        mode2Top.text = topText;
        mode2Bottom.text = phaseNumber > 0 ? $"第{phaseNumber}阶段" : "";
        PlayRoutine(Mode2Routine());
    }

    void PlayRoutine(IEnumerator routine)
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(routine);
    }

    /// <summary>首帧/懒初始化：记录锚点与停留位，初始全隐藏。</summary>
    void InitPositions()
    {
        if (_inited) return;
        _inited = true;
        if (mode1Text != null)
        {
            _anchor = mode1Text.rectTransform.anchoredPosition;
            _mode1FullAlpha = mode1Text.color.a;
        }
        if (mode2Top != null)
        {
            _topRest = mode2Top.rectTransform.anchoredPosition;
            _mode2TopFullAlpha = mode2Top.color.a;
        }
        if (mode2Bottom != null)
        {
            _bottomRest = mode2Bottom.rectTransform.anchoredPosition;
            _mode2BottomFullAlpha = mode2Bottom.color.a;
        }
        SetTextAlpha(mode1Text, 0f);
        SetTextAlpha(mode2Top, 0f);
        SetTextAlpha(mode2Bottom, 0f);
        SetBandAlpha(0f);
    }

    // ================= 模式一：单行 =================

    IEnumerator Mode1Routine()
    {
        // 隐藏模式二，模式一就位（锚点 + 起始缩放）
        SetTextAlpha(mode2Top, 0f);
        SetTextAlpha(mode2Bottom, 0f);
        mode1Text.rectTransform.localScale = Vector3.one * mode1StartScale;
        SetTextAlpha(mode1Text, 0f);

        // ① 淡入 + 微缩（1.05 → 1.0），条带同步淡入
        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / fadeInDuration);
            float e = Mathf.SmoothStep(0f, 1f, p);
            SetTextAlpha(mode1Text, e * _mode1FullAlpha);
            mode1Text.rectTransform.localScale = Vector3.Lerp(
                Vector3.one * mode1StartScale, Vector3.one * mode1EndScale, e);
            SetBandAlpha(Mathf.Lerp(0f, bandColor.a, e));
            yield return null;
        }
        mode1Text.rectTransform.localScale = Vector3.one * mode1EndScale;
        SetTextAlpha(mode1Text, _mode1FullAlpha);
        SetBandAlpha(bandColor.a);

        // ② 停留
        yield return new WaitForSeconds(holdDuration);

        // ③ 淡出（缩放保持 1.0，仅淡 alpha；条带同步淡出）
        t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / fadeOutDuration);
            float e = Mathf.SmoothStep(0f, 1f, p);
            SetTextAlpha(mode1Text, _mode1FullAlpha * (1f - e));
            SetBandAlpha(Mathf.Lerp(bandColor.a, 0f, e));
            yield return null;
        }
        SetTextAlpha(mode1Text, 0f);
        SetBandAlpha(0f);
    }

    // ================= 模式二：两行 =================

    IEnumerator Mode2Routine()
    {
        // 隐藏模式一
        SetTextAlpha(mode1Text, 0f);
        mode1Text.rectTransform.localScale = Vector3.one;

        // 停留位：mode2MoveDistance>0 时用 锚点±距离，否则用场景摆放的位置
        Vector2 topRest = mode2MoveDistance > 0f ? _anchor + new Vector2(0f, mode2MoveDistance) : _topRest;
        Vector2 bottomRest = mode2MoveDistance > 0f ? _anchor - new Vector2(0f, mode2MoveDistance) : _bottomRest;

        // 两行从公共锚点出发
        mode2Top.rectTransform.anchoredPosition = _anchor;
        mode2Bottom.rectTransform.anchoredPosition = _anchor;
        SetTextAlpha(mode2Top, 0f);
        SetTextAlpha(mode2Bottom, 0f);

        // ① 淡入 + 从锚点平滑移动到停留位，条带同步淡入
        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / fadeInDuration);
            float e = Mathf.SmoothStep(0f, 1f, p);
            SetTextAlpha(mode2Top, e * _mode2TopFullAlpha);
            SetTextAlpha(mode2Bottom, e * _mode2BottomFullAlpha);
            mode2Top.rectTransform.anchoredPosition = Vector2.Lerp(_anchor, topRest, e);
            mode2Bottom.rectTransform.anchoredPosition = Vector2.Lerp(_anchor, bottomRest, e);
            SetBandAlpha(Mathf.Lerp(0f, bandColor.a, e));
            yield return null;
        }
        mode2Top.rectTransform.anchoredPosition = topRest;
        mode2Bottom.rectTransform.anchoredPosition = bottomRest;
        SetTextAlpha(mode2Top, _mode2TopFullAlpha);
        SetTextAlpha(mode2Bottom, _mode2BottomFullAlpha);
        SetBandAlpha(bandColor.a);

        // ② 停留
        yield return new WaitForSeconds(holdDuration);

        // ③ 淡出（位置保持，仅淡 alpha；条带同步淡出）
        t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / fadeOutDuration);
            float e = Mathf.SmoothStep(0f, 1f, p);
            SetTextAlpha(mode2Top, _mode2TopFullAlpha * (1f - e));
            SetTextAlpha(mode2Bottom, _mode2BottomFullAlpha * (1f - e));
            SetBandAlpha(Mathf.Lerp(bandColor.a, 0f, e));
            yield return null;
        }
        SetTextAlpha(mode2Top, 0f);
        SetTextAlpha(mode2Bottom, 0f);
        SetBandAlpha(0f);
    }

    // ================= 工具 =================

    void SetTextAlpha(TextMeshProUGUI t, float a)
    {
        if (t == null) return;
        var c = t.color; c.a = Mathf.Clamp01(a); t.color = c;
    }

    void SetBandAlpha(float a)
    {
        if (bandImage == null) return;
        var c = bandColor; c.a = Mathf.Clamp01(a); bandImage.color = c;
    }
}
