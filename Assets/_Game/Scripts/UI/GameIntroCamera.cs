using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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
/// 入场两角的 3D 牌堆（右下＝己方 / 左上＝对方）也挂在这里：镜头飞行期间两摞牌从空中
/// 迅速落下堆好，最后一张正好落在 UI 开始浮现那一刻，UI 淡完再收起。见 GameIntroDeck。
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

    [Header("入场 3D 牌堆（右侧中央一摞，永远是卡背，默认留在场上）")]
    [Tooltip("3D 卡牌预制体（Card00_New_3D）。留空、或关掉下面那个开关，就不出这段")]
    public GameObject deckCardPrefab;
    [Tooltip("入场期间掉一摞 3D 卡背")]
    public bool showIntroDecks = true;
    [Tooltip("牌堆在终态画面里的视口坐标，0-1（0.5 = 竖直居中，越大越靠右）")]
    public Vector2 deckViewport = new Vector2(0.835f, 0.5f);
    [Tooltip("每摞几张")]
    public int deckCardCount = 10;
    [Tooltip("第一张落地的时刻（占整段位移的比例 0-1）")]
    [Range(0f, 1f)] public float deckFirstLandAt = 0.30f;
    [Tooltip("单张从生成到落地用多久（秒）")]
    public float deckFallDuration = 0.22f;
    [Tooltip("最后一张落地后停多久再收起")]
    public float deckHoldAfterLand = 0.2f;
    [Tooltip("收起时长（秒）。0 = 不收起：牌堆一直留在场上（默认）")]
    public float deckCollapseDuration = 0f;
    [Tooltip("牌堆的 z：与场上卡同面 = -5.7（比槽位 -5.6、棋盘面 -5.5 都更靠相机）")]
    public float deckPlaneZ = -5.7f;

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
    GameIntroDeck _decks;

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

        if (showIntroDecks) BuildDecks();

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

    void Update()
    {
        if (_decks == null) return;
        _decks.Tick(Time.deltaTime);
        if (_decks.Finished) { _decks.Dispose(); _decks = null; }   // 收起后整棵删掉，不残留
    }

    /// <summary>
    /// 入场牌堆：落点按「终态画面的视口坐标」算，所以换分辨率也在同一个地方、也还离槽位有距离；
    /// 最后一张的落地时刻 = revealAt 那一刻（UI 开始浮现）。默认不收起，一直留在场上。
    /// </summary>
    void BuildDecks()
    {
        if (deckCardPrefab == null) return;

        Camera cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        _decks = new GameIntroDeck
        {
            cardPrefab = deckCardPrefab,
            cam = cam,
            endCamPos = _endPos,
            endCamRot = _endRot,
            planeZ = deckPlaneZ,
            cardCount = deckCardCount,
            firstLandTime = moveDuration * deckFirstLandAt,
            lastLandTime = moveDuration * revealAt,
            fallDuration = deckFallDuration,
            holdAfterLand = deckHoldAfterLand,
            collapseDuration = deckCollapseDuration,
            viewport = deckViewport,
        };
        _decks.Build();
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

    /// <summary>
    /// 2D 界面的总画布（Game 场景里叫 CardCanvas）。
    ///
    /// 只认「本场景（Game）自己的」画布：过场层（SceneTransition）、设置面板（SettingsCanvas）、
    /// 匹配等待条（NetworkWaiting）都是挂在 DontDestroyOnLoad 上的全屏根 Overlay 画布，
    /// 切场景时它们还活着；一旦被挑中，入场淡入淡出的是它们，真正的 CardCanvas 反而没被藏起来。
    /// </summary>
    Canvas FindCardCanvas()
    {
        Scene scene = gameObject.scene;                 // 本组件就在 Game 场景里
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
            if (InScene(canvases[i], scene) && canvases[i].gameObject.name == "CardCanvas") return canvases[i];

        // 兜底一：与手牌同一个画布
        if (Player.Instance != null && Player.Instance.handArea != null)
        {
            Canvas c = Player.Instance.handArea.GetComponentInParent<Canvas>();
            if (InScene(c, scene)) return c;
        }
        // 兜底二：本场景里任意全屏叠加画布
        for (int i = 0; i < canvases.Length; i++)
            if (InScene(canvases[i], scene) && canvases[i].renderMode == RenderMode.ScreenSpaceOverlay) return canvases[i];
        return null;
    }

    /// <summary>画布存在、且与给定场景是同一个（跨场景的常驻层一律不算）。</summary>
    static bool InScene(Canvas c, Scene scene)
    {
        return c != null && c.gameObject.scene == scene;
    }
}
