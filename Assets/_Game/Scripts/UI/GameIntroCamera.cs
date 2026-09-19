using System.Collections;
using UnityEngine;

/// <summary>
/// 战斗场景入场镜头（2026-09-19）：相机从贴近棋盘的一处起始位姿出发，
/// 一边平移到最终位置、一边远离棋盘；全程 2D UI（CardCanvas 整块）不显示，
/// 快到位时先让槽位预制体浮现、再把 UI 淡入，之后才放开局抽牌
/// （TurnManager.InitialDraw / NetworkPlayer.TargetReceiveInitialCards 会等这里的 Revealed）。
///
/// 起始位姿由场景里那个空物体 startPose 给出 —— 想改成什么角度，就先把 Scene 视角摆到那儿、
/// 选中它按 Ctrl+Shift+F（Align With View），不用手算数字。
/// 没挂 / 关掉 playOnStart 时整个入场不参与：UI 不隐藏，抽牌也不等。
///
/// 时间轴（秒，全部可在 Inspector 调）：
///   0                        相机停在起始位姿
///   0 → moveDuration         位姿插值到终态（一开始冲得快，越接近中段越慢，收尾轻轻贴上去）
///   进度到 revealAt           槽位开始浮现 + UI 开始淡入
///   UI 淡入结束 + drawDelay   Revealed = true → 开局抽牌开始
///
/// 槽位浮现不在这里做，只是替 BoardManager 喊一声（BoardManager.PlaySlotReveal），
/// 免得相机还在飞的时候槽位就自己浮出来了。
/// </summary>
[DisallowMultipleComponent]
public class GameIntroCamera : MonoBehaviour
{
    [Header("起始位姿（场景里摆好空物体，选中它 Ctrl+Shift+F 对齐当前视角）")]
    public Transform startPose;

    [Header("时间轴（秒）")]
    [Tooltip("从起始位姿走到终态的时长")]
    public float moveDuration = 2.0f;
    [Tooltip("速度曲线：1 = 匀速，越大越「一开始冲得快、到中段就慢下来、收尾轻贴」")]
    public float easePower = 2.4f;
    [Tooltip("进度到这里开始浮现槽位 + 淡入 UI（0-1）")]
    [Range(0f, 1f)] public float revealAt = 0.72f;
    [Tooltip("UI 淡入时长")]
    public float uiFadeIn = 0.6f;
    [Tooltip("UI 完全浮出后再等这么久才放开局抽牌")]
    public float drawDelay = 0.15f;

    [Header("开关")]
    public bool playOnStart = true;
    [Tooltip("入场期间隐藏全部 2D UI（CardCanvas 整块，含手牌与所有面板）")]
    public bool hideUiDuringIntro = true;

    public static GameIntroCamera Instance { get; private set; }

    /// <summary>场景里有没有在跑入场（BoardManager 据此决定槽位浮现要不要交给入场来喊）。</summary>
    public static bool Playing { get { return Instance != null && Instance._playing; } }

    /// <summary>UI 是否已经浮出来（＝开局抽牌可以开始）；没有入场时恒为 true。</summary>
    public static bool Revealed { get { return Instance == null || Instance._revealed; } }

    /// <summary>等 UI 浮出来；没有入场 / 入场没在跑就直接过。</summary>
    public static IEnumerator WaitRevealed()
    {
        GameIntroCamera intro = Instance;
        while (intro != null && !intro._revealed) yield return null;
    }

    bool _playing;
    bool _revealed;
    bool _uiFadeDone;
    Vector3 _startPos, _endPos;
    Quaternion _startRot, _endRot;
    CanvasGroup _uiGroup;

    void Awake()
    {
        Instance = this;

        _endPos = transform.position;
        _endRot = transform.rotation;

        _playing = playOnStart && isActiveAndEnabled && startPose != null;
        if (!_playing) { _revealed = true; return; }

        _startPos = startPose.position;
        _startRot = startPose.rotation;
        // 第一帧就摆到近景，否则会先闪一下终态再跳过去
        transform.SetPositionAndRotation(_startPos, _startRot);

        if (hideUiDuringIntro)
        {
            Canvas canvas = FindCardCanvas();
            if (canvas != null)
            {
                _uiGroup = canvas.GetComponent<CanvasGroup>();
                if (_uiGroup == null) _uiGroup = canvas.gameObject.AddComponent<CanvasGroup>();
                _uiGroup.alpha = 0f;
                _uiGroup.interactable = false;
                _uiGroup.blocksRaycasts = false;
            }
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (_playing) StartCoroutine(Play());
    }

    IEnumerator Play()
    {
        bool revealed = false;
        float t = 0f;
        while (t < moveDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / Mathf.Max(0.0001f, moveDuration));
            // ease-out 幂曲线：起步最快，越接近中段越慢，末尾轻轻贴上去（匀速 = 1）
            float e = 1f - Mathf.Pow(1f - p, Mathf.Max(1f, easePower));
            transform.SetPositionAndRotation(Vector3.LerpUnclamped(_startPos, _endPos, e),
                                             Quaternion.SlerpUnclamped(_startRot, _endRot, e));
            if (!revealed && p >= revealAt) { revealed = true; BeginReveal(); }
            yield return null;
        }

        transform.SetPositionAndRotation(_endPos, _endRot);
        if (!revealed) BeginReveal();

        while (!_uiFadeDone) yield return null;
        if (drawDelay > 0f) yield return new WaitForSeconds(drawDelay);
        _revealed = true;
    }

    /// <summary>快到位时：喊槽位浮现 + 把 UI 淡进来。</summary>
    void BeginReveal()
    {
        BoardManager board = FindObjectOfType<BoardManager>();
        if (board != null) board.PlaySlotReveal();

        if (_uiGroup != null) StartCoroutine(FadeUiIn());
        else _uiFadeDone = true;
    }

    IEnumerator FadeUiIn()
    {
        float dur = Mathf.Max(0.0001f, uiFadeIn);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            _uiGroup.alpha = Mathf.Clamp01(t / dur);
            yield return null;
        }
        _uiGroup.alpha = 1f;
        _uiGroup.interactable = true;
        _uiGroup.blocksRaycasts = true;
        _uiFadeDone = true;
    }

    /// <summary>2D 界面的总画布（Game 场景里叫 CardCanvas）。</summary>
    static Canvas FindCardCanvas()
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
            if (canvases[i] != null && canvases[i].gameObject.name == "CardCanvas") return canvases[i];

        // 兜底一：与手牌同一个画布
        if (Player.Instance != null && Player.Instance.handArea != null)
        {
            Canvas c = Player.Instance.handArea.GetComponentInParent<Canvas>();
            if (c != null) return c;
        }
        // 兜底二：任意全屏叠加画布
        for (int i = 0; i < canvases.Length; i++)
            if (canvases[i] != null && canvases[i].renderMode == RenderMode.ScreenSpaceOverlay) return canvases[i];
        return null;
    }
}
