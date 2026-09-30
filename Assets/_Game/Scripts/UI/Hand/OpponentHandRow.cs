using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 对方手牌（只露卡背）—— 运行时挂在主画布上、排在 TopBar **之前**的一条「贴顶横排」。
/// 几张缩小的 2D 卡背叠成一道浅弧，张数跟着 NetworkPlayer.Remote.handCardCount 走；
/// 整排挂在屏幕顶上方、卡背下沿压在 bottomEdgeFromTop 这条线上 ——
/// **屏幕内不做任何裁剪**，上面那一大截是真走到屏幕外了（外加顶栏盖住的那部分）。
///
/// 飞入：对方每次**主动花能量抽牌**（点抽牌按钮 / AI TryDraw / 择牌择中）时，
/// NetworkPlayer 会把 activeDrawTick +1（SyncVar），本端收到就在这一排补一张卡背，
/// 并让一张缩小比例的 2D 卡背从右侧那摞 3D 牌堆所在的 2D 位置（视口 0.835, 0.5）飞过来。
/// 卡牌效果造成的抽牌不计数 —— 那种只让张数直接变一下，不播飞入。
///
/// 纯运行时构建，不依赖场景摆放（与 PromptBanner 同一套路）。位置 / 尺寸全在下面常量区。
/// </summary>
[DisallowMultipleComponent]
public class OpponentHandRow : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════════
    // 常量区（要调就改这里）
    // ══════════════════════════════════════════════════════════════════

    [Header("位置 / 大小")]
    [Tooltip("卡背**下沿**距屏幕顶的设计像素（1920×1080）：整排最低那张卡的底边落在这儿，整张卡往屏幕外走。\n顶栏下沿约 96；再往下那排格子（面板）上沿约 150 —— 想不压住格子就别超过 150，想露少点就调小。")]
    public float bottomEdgeFromTop = 132f;
    [Tooltip("整排缩放（1 = 用 2D 卡预制体原尺寸 250×439）")]
    public float rowScale = 0.62f;
    [Tooltip("整排最大宽度（排内单位，乘 rowScale 才是像素）")]
    public float designRowWidth = 1150f;

    [Header("弧形")]
    [Tooltip("沿水平轴镜像：对方手牌是**己方手牌上下翻**（两翼往上收、中间垂得最低、转角反向）。\n关掉就变成和己方一模一样地照搬。")]
    public bool mirrorArc = true;
    public float radius = 1500f;
    public float totalArcAngle = 30f;
    public float maxOverlapRatio = 0.65f;
    public float arcFlatStartCount = 5f;
    public float arcFlatEndCount = 20f;
    public float arcFlatMin = 0.6f;
    [Tooltip("排内位置渐变速度（越大越跟手）")]
    public float layoutSpeed = 14f;

    [Header("飞入")]
    [Tooltip("飞入那张卡背相对整排的缩放（比排里小一圈，落位时放大回排内尺寸）")]
    public float flyScale = 0.40f;
    [Tooltip("从 3D 牌堆飞到对方手牌那一排的时长（秒），越小越快")]
    public float flyDuration = 0.26f;
    [Tooltip("飞入起始旋转（度），落位转到排内角度")]
    public float flyTilt = -16f;
    [Tooltip("3D 牌堆在终态画面里的视口坐标（与 GameIntroCamera.deckViewport 保持一致）")]
    public Vector2 deckViewport = new Vector2(0.835f, 0.5f);

    // 2D 卡预制体（Card00_New_2D）原始尺寸；Player.Scale2DCard 会把实例放大 3 倍。
    const float CardPrefabWidth = 83.33f;
    const float CardPrefabHeight = 146.33f;
    const float CardDisplayScale = 3f;
    const float CardW = CardPrefabWidth * CardDisplayScale;
    const float CardH = CardW * (CardPrefabHeight / CardPrefabWidth);
    const int MaxCards = 20;
    const string BackSpritePath = "Cards/Back And Front/Back";
    const float FlyTokenLife = 1.5f;   // 收到「主动抽牌」信号后，等张数变上来的时限

    public static OpponentHandRow Instance { get; private set; }

    RectTransform _root;
    Sprite _backSprite;
    readonly List<RectTransform> _cards = new List<RectTransform>();
    readonly HashSet<RectTransform> _snapOnce = new HashSet<RectTransform>();

    int _flyTokens;
    float _flyTokenTime = -99f;
    int _debugBump;   // 临时自测键用（F10）

    // ══════════════════════════════════════════════════════════════════
    // 创建 / 收尾
    // ══════════════════════════════════════════════════════════════════

    /// <summary>场景加载后武装重试器 —— 入场期间画布可能还没就绪，靠它每帧重试。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OpponentHandRowBoot.Arm();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { OpponentHandRowBoot.Arm(); }

    /// <summary>创建对方手牌排（幂等）。找不到主 Canvas 就返回 null，由重试器下一轮再试。</summary>
    public static OpponentHandRow Ensure()
    {
        if (Instance != null) return Instance;

        GameObject canvasGo = PlayRevealManager.FindMainCanvas();
        if (canvasGo == null) return null;

        var go = new GameObject("OpponentHandRow", typeof(RectTransform), typeof(OpponentHandRow));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvasGo.transform, false);

        // 排在 TopBar 之前：只剩「顶栏挡住多少」这一层关系，别的 UI 不会压住它。
        Transform topBar = canvasGo.transform.Find("TopBar");
        if (topBar != null) rt.SetSiblingIndex(topBar.GetSiblingIndex());
        else rt.SetAsLastSibling();

        Debug.Log(string.Format("[OpponentHandRow] 已创建，挂到 Canvas「{0}」下", canvasGo.name));
        return Instance;
    }

    /// <summary>对方「主动花能量抽牌」时由 NetworkPlayer 的 SyncVar hook 调用。</summary>
    public static void NotifyActiveDraw()
    {
        OpponentHandRow row = Ensure();
        if (row != null) row.AddFlyToken();
    }

    void AddFlyToken()
    {
        _flyTokens++;
        _flyTokenTime = Time.time;
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        Build();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Build()
    {
        _root = (RectTransform)transform;
        _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 1f);
        _root.pivot = new Vector2(0.5f, 1f);
        _root.anchoredPosition = new Vector2(0f, -bottomEdgeFromTop);
        _root.sizeDelta = Vector2.zero;   // 零尺寸：局部原点 = 卡背下沿那条线
        _root.localScale = Vector3.one * rowScale;

        CanvasGroup cg = gameObject.AddComponent<CanvasGroup>();
        cg.interactable = false;
        cg.blocksRaycasts = false;   // 纯装饰，不吃射线

        // 不做屏幕内裁剪：卡背整张挂在排根下，上面那截靠屏幕顶 + 顶栏来挡（走到屏幕外才算看不见）。

        _backSprite = Resources.Load<Sprite>(BackSpritePath);
        if (_backSprite == null)
            Debug.LogWarning("[OpponentHandRow] 没加载到卡背图 Resources/" + BackSpritePath);
    }

    // ══════════════════════════════════════════════════════════════════
    // 每帧
    // ══════════════════════════════════════════════════════════════════

    void Update()
    {
        if (_root == null) return;

        // 过期信号：对方点了抽牌但没抽成（牌库空 / 不是他的回合）
        if (_flyTokens > 0 && Time.time - _flyTokenTime > FlyTokenLife) _flyTokens = 0;

        NetworkPlayer remote = NetworkPlayer.Remote;
        int target = remote != null ? Mathf.Clamp(remote.handCardCount, 0, MaxCards) : 0;
        target += _debugBump;

        Reconcile(target);
        Layout(Time.deltaTime);

        // ── 临时自测键（确认过效果就删）──
        // F10：假装对方主动抽一张（播飞入 + 张数 +1）；Shift+F10：复位
        if (Input.GetKeyDown(KeyCode.F10))
        {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                _debugBump = 0;
                _flyTokens = 0;
            }
            else
            {
                _debugBump++;
                AddFlyToken();
            }
        }
    }

    /// <summary>把实际的卡背张数对齐到 target（多了立刻删、少了补）。</summary>
    void Reconcile(int target)
    {
        while (_cards.Count < target)
        {
            RectTransform card = CreateCard();
            _cards.Add(card);

            bool fly = _flyTokens > 0;
            if (fly) _flyTokens--;

            if (fly)
            {
                SetAlpha(card, 0f);          // 本体先隐形，等飞过来的那张落位再显形
                StartCoroutine(FlyRoutine(card));
            }
        }

        while (_cards.Count > target)
        {
            int last = _cards.Count - 1;
            RectTransform card = _cards[last];
            _cards.RemoveAt(last);
            _snapOnce.Remove(card);
            if (card != null) Destroy(card.gameObject);
        }
    }

    /// <summary>浅弧排布（与 HandManager.RefreshLayout 同一套公式，只是坐标从「露出线」往下量）。</summary>
    void Layout(float dt)
    {
        int count = _cards.Count;
        if (count == 0) return;

        float arcFlat = Mathf.Lerp(1f, arcFlatMin, Mathf.InverseLerp(arcFlatStartCount, arcFlatEndCount, count));
        float overlap = Mathf.Lerp(0f, maxOverlapRatio, (count - 1) / 19f);
        float step = CardW * (1f - overlap);
        float totalW = step * (count - 1) + CardW;

        if (totalW > designRowWidth && count > 1)
        {
            step = (designRowWidth - CardW) / (count - 1);
            totalW = designRowWidth;
        }

        float startX = -totalW / 2f + CardW / 2f;
        float halfW = Mathf.Max(1f, designRowWidth / 2f);
        // 卡心在线的**上方半张**：卡的下沿正好压在这条线上，整张卡往上出屏幕。
        float centerY = CardH * 0.5f;
        // 镜像系数：己方手牌是「两翼往下坠」（arcY 为负），对方手牌上下翻过来 = 「两翼往上收」+ 转角反向。
        float arcSign = mirrorArc ? 1f : -1f;
        float k = dt > 0f ? 1f - Mathf.Exp(-layoutSpeed * dt) : 1f;

        for (int i = 0; i < count; i++)
        {
            RectTransform card = _cards[i];
            if (card == null) continue;

            float x = startX + i * step;
            float nx = x / halfW;
            float arcY = arcSign * Mathf.Abs(nx) * radius * 0.02f * arcFlat;
            float angle = arcSign * nx * totalArcAngle * 0.5f * arcFlat;

            Vector3 target = new Vector3(x, centerY + arcY, 0f);
            Quaternion rot = Quaternion.Euler(0f, 0f, angle);

            if (_snapOnce.Remove(card))
            {
                card.anchoredPosition = target;
                card.localRotation = rot;
            }
            else
            {
                card.anchoredPosition = Vector3.Lerp(card.anchoredPosition, target, k);
                card.localRotation = Quaternion.Slerp(card.localRotation, rot, k);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // 卡背 / 飞入
    // ══════════════════════════════════════════════════════════════════

    RectTransform CreateCard()
    {
        var go = new GameObject("OpponentCardBack", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(_root, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);   // 参照排根原点（= 卡背下沿那条线）
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(CardW, CardH);
        rt.localScale = Vector3.one;

        Image img = go.GetComponent<Image>();
        img.sprite = _backSprite;
        img.raycastTarget = false;
        img.color = Color.white;

        _snapOnce.Add(rt);
        return rt;
    }

    /// <summary>飞入的那张挂在排根上（**不进遮罩**），这样从牌堆一路飞到露出线都看得见。</summary>
    IEnumerator FlyRoutine(RectTransform card)
    {
        var go = new GameObject("OpponentCardBackFly", typeof(RectTransform), typeof(Image));
        var flyer = (RectTransform)go.transform;
        flyer.SetParent(_root, false);
        flyer.anchorMin = flyer.anchorMax = new Vector2(0.5f, 0.5f);
        flyer.pivot = new Vector2(0.5f, 0.5f);
        flyer.sizeDelta = new Vector2(CardW, CardH);
        flyer.SetAsLastSibling();

        Image img = go.GetComponent<Image>();
        img.sprite = _backSprite;
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, 0f);

        Vector3 from = DeckLocalPoint();
        Quaternion fromRot = Quaternion.Euler(0f, 0f, flyTilt);
        float fromScale = Mathf.Max(0.01f, flyScale / Mathf.Max(0.01f, rowScale));
        flyer.anchoredPosition = from;
        flyer.localRotation = fromRot;
        flyer.localScale = Vector3.one * fromScale;

        float t = 0f;
        float dur = Mathf.Max(0.01f, flyDuration);
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            float e = p * p * (3f - 2f * p);   // smoothstep：起步慢一点、快到位时收住

            Vector3 to = TargetLocal(card);
            Quaternion toRot = card != null ? card.localRotation : Quaternion.identity;
            flyer.anchoredPosition = Vector3.LerpUnclamped(from, to, e);
            flyer.localRotation = Quaternion.SlerpUnclamped(fromRot, toRot, e);
            flyer.localScale = Vector3.one * Mathf.LerpUnclamped(fromScale, 1f, e);
            img.color = new Color(1f, 1f, 1f, Mathf.Clamp01(p * 5f));
            yield return null;
        }

        if (card != null) SetAlpha(card, 1f);
        Destroy(go);
    }

    /// <summary>目标卡在「排根」局部坐标里的位置（卡直接挂在排根下，两套坐标一致）。</summary>
    Vector3 TargetLocal(RectTransform card)
    {
        if (card == null) return Vector3.zero;
        return new Vector3(card.anchoredPosition.x, card.anchoredPosition.y, 0f);
    }

    /// <summary>3D 牌堆在排根局部坐标里的位置（终态画面视口 → 屏幕像素 → 本排局部）。</summary>
    Vector3 DeckLocalPoint()
    {
        Vector2 screen = new Vector2(deckViewport.x * Screen.width, deckViewport.y * Screen.height);

        Canvas canvas = GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;

        Vector2 local;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, cam, out local))
            return new Vector3(local.x, local.y, 0f);
        return Vector3.zero;
    }

    static void SetAlpha(RectTransform rt, float a)
    {
        if (rt == null) return;
        Image img = rt.GetComponent<Image>();
        if (img != null) img.color = new Color(1f, 1f, 1f, a);
    }
}

/// <summary>对方手牌排的落地兜底：主 Canvas 迟到 / 入场期间被关时反复重试创建（4Hz）。</summary>
public class OpponentHandRowBoot : MonoBehaviour
{
    static OpponentHandRowBoot _boot;
    float _t;

    public static void Arm()
    {
        if (_boot != null) return;
        var go = new GameObject("OpponentHandRowBoot");
        DontDestroyOnLoad(go);
        _boot = go.AddComponent<OpponentHandRowBoot>();
    }

    void Update()
    {
        if (OpponentHandRow.Instance != null) return;
        if (Player.Instance == null) return;   // 不在对局里（菜单等）不建

        _t += Time.unscaledDeltaTime;
        if (_t < 0.25f) return;
        _t = 0f;
        OpponentHandRow.Ensure();
    }
}
