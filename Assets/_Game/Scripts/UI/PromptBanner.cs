using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 指示选择 / 提示条 —— 画布顶部中央（贴着顶栏「阶段推进」条的下方）的一条承载文字的底带，
/// 纯运行时构建，不依赖场景摆放。位置、尺寸、时长都在 Inspector / 代码里可直接改。
///
/// 两种存在形式：
///   · 指示选择（Prompt）：从中央渐入，**停留到玩家做出选择**才渐出。
///     文字形如「选择「毒巫」特性1目标」/「选择「疗」作用目标」，由当前正在执行的特性实时推出。
///   · 提示（Toast）：从屏幕左边快速滑入 → 停住一段时间 → 从右边滑出（一条直线穿过去，不原路退回）。
///     用于"现在小心！"这类一次性提醒。
///
/// 鼠标交互：指针（或正拖着的牌）压到条上时整条淡到几乎透明；**全程不吃射线** ——
/// ① 所有 Graphic 的 raycastTarget 一律关掉，点击直接穿透到下方格子；
/// ② 悬停判定不走 EventSystem，改为每帧做矩形命中测试（RectTransformUtility），
///    所以哪怕它盖在格子上也绝不会抢掉任何一次点击。
///
/// 自动触发：
///   · 指示选择：跟随 SelectionManager.IsSelecting（进入选择 → 渐入；选择结束 → 渐出），
///     取不到"哪张卡的哪个特性"时不显示（不影响普通的拖牌落格）。
///   · 提示：己方生命值第一次降到 ≤ 阈值（默认 5）时滑入「现在小心！」；回到阈值以上后重新武装。
/// </summary>
[DisallowMultipleComponent]
public class PromptBanner : MonoBehaviour
{
    public static PromptBanner Instance { get; private set; }

    [Header("位置（锚点 = 画布顶部中央，y 为负 = 向下）")]
    public Vector2 anchoredPosition = new Vector2(0f, -70f);

    [Tooltip("底带尺寸（UI 单位，画布参考分辨率 1920×1080）")]
    public Vector2 plateSize = new Vector2(760f, 58f);

    [Header("鼠标压上去时的不透明度")]
    [Range(0f, 1f)] public float hoverAlpha = 0.12f;
    [Tooltip("淡入透明 / 淡回的速度（秒）")]
    public float hoverFadeTime = 0.12f;

    [Header("提示（滑入 / 停留 / 滑出，秒）")]
    public float slideInTime = 0.16f;
    public float slideHoldTime = 1.9f;
    public float slideOutTime = 0.16f;

    [Header("指示选择（渐入 / 渐出，秒）")]
    public float fadeInTime = 0.22f;
    [Tooltip("指示选择的淡出时长（选择一结束就迅速消失，不要拖）。滑入式提示用的是 slideOutTime，不受这里影响")]
    public float fadeOutTime = 0.07f;
    [Tooltip("渐入时的起始缩放（1 = 不缩放）")]
    public float fadeInStartScale = 0.96f;

    [Header("文案与配色")]
    [Tooltip("生命值第一次降到阈值以下时的提示文字")]
    public string lowHealthText = "现在小心！";
    public int lowHealthThreshold = 5;
    public Color lowHealthColor = new Color(1f, 0.62f, 0.54f, 1f);
    public Color promptColor = new Color(0.984f, 0.965f, 0.925f, 1f);
    public Color defaultHintColor = new Color(0.984f, 0.965f, 0.925f, 1f);

    // ── 运行时构建的引用 ──
    RectTransform _root;
    Image _plate;
    TextMeshProUGUI _text;
    CanvasGroup _group;
    bool _built;

    // ── 状态 ──
    enum Form { None, Prompt, Toast }
    Form _form = Form.None;
    Coroutine _routine;
    string _shownText;
    float _formAlpha;      // 该形式自身的透明度（不含悬停系数）
    float _hoverMul = 1f;  // 悬停系数（1 = 原样，hoverAlpha = 淡到几乎透明）
    bool _lowHealthFired;
    bool _lowHealthPending;
    int _prevHp = int.MinValue;
    int _lastLoggedHp = int.MinValue;

    // ══════════════════════════════════════════════════════════════════
    // 对外接口
    // ══════════════════════════════════════════════════════════════════

    /// <summary>场景加载后自动武装重试器：入场镜头期间画布可能是关的 / 还没就绪，
    /// 那时候创建会失败，靠它每帧重试，避免"一次创建失败就永远没有提示条"。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        PromptBannerBoot.Arm();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { PromptBannerBoot.Arm(); }

    /// <summary>创建提示条（幂等）。找不到主 Canvas 就返回 null，由重试器下一轮再试。</summary>
    public static PromptBanner Ensure()
    {
        if (Instance != null) return Instance;

        GameObject canvasGo = PlayRevealManager.FindMainCanvas();
        if (canvasGo == null)
        {
            Debug.LogWarning("[PromptBanner] 找不到主 Canvas，本轮跳过（重试器会继续试）");
            return null;
        }

        var go = new GameObject("PromptBanner", typeof(RectTransform), typeof(PromptBanner));
        go.transform.SetParent(canvasGo.transform, false);
        go.transform.SetAsLastSibling();
        Debug.Log($"[PromptBanner] 已创建，挂到 Canvas「{canvasGo.name}」下");
        return Instance;
    }

    /// <summary>滑入式提示（以后要加别的提示直接调这个即可，例如 PromptBanner.Hint("...")）。</summary>
    public static void Hint(string text)
    {
        PromptBanner b = Ensure();
        if (b != null) b.Toast(text, b.defaultHintColor);
    }

    // ══════════════════════════════════════════════════════════════════
    // 构建
    // ══════════════════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        Build();
    }

    void Build()
    {
        if (_built) return;
        _built = true;

        _root = (RectTransform)transform;
        _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 1f);
        _root.pivot = new Vector2(0.5f, 1f);
        _root.anchoredPosition = anchoredPosition;
        _root.sizeDelta = plateSize;
        _root.localScale = Vector3.one;

        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.interactable = false;
        _group.blocksRaycasts = false;   // 永远不吃射线

        // 底带：与顶栏「阶段推进」条同一张图（Resources/UI/PromptPlate）
        var plateGo = new GameObject("Plate", typeof(RectTransform));
        plateGo.transform.SetParent(_root, false);
        var pr = (RectTransform)plateGo.transform;
        pr.anchorMin = Vector2.zero;
        pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero;
        pr.offsetMax = Vector2.zero;
        _plate = plateGo.AddComponent<Image>();
        _plate.sprite = Resources.Load<Sprite>("UI/PromptPlate");
        _plate.type = Image.Type.Simple;
        _plate.color = _plate.sprite != null ? Color.white : new Color(0.07f, 0.10f, 0.16f, 1f);
        _plate.raycastTarget = false;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(_root, false);
        var tr = (RectTransform)textGo.transform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(30f, 3f);
        tr.offsetMax = new Vector2(-30f, -3f);
        _text = textGo.AddComponent<TextMeshProUGUI>();
        _text.alignment = TextAlignmentOptions.Center;
        _text.fontSize = 28f;
        _text.enableWordWrapping = false;
        _text.overflowMode = TextOverflowModes.Overflow;
        _text.raycastTarget = false;
        ApplyFont(_text);
        ApplyText(string.Empty, promptColor);
        Debug.Log($"[PromptBanner] 底图 = {(_plate.sprite != null ? _plate.sprite.name : "没加载到（用纯色兜底）")}");
    }

    // ══════════════════════════════════════════════════════════════════
    // 每帧
    // ══════════════════════════════════════════════════════════════════

    void Update()
    {
        SyncSelectionPrompt();
        WatchLowHealth();
        UpdateHover();
        if (_group != null) _group.alpha = _formAlpha * _hoverMul;

        // ── 临时自测键（确认过效果就删）──
        if (Input.GetKeyDown(KeyCode.F9)) Hint("这是一条提示");
        if (Input.GetKeyDown(KeyCode.F8))
        {
            if (_form == Form.Prompt) HidePrompt();
            else ShowPrompt("选择「毒巫」特性1目标");
        }
    }

    /// <summary>指示选择：进入选择 → 渐入；选择结束 → 渐出。
    /// 同一帧内换层（层叠选择）时只换字，不重播动画。取不到卡名时不显示。</summary>
    void SyncSelectionPrompt()
    {
        SelectionManager sm = SelectionManager.Instance;
        bool selecting = sm != null && sm.IsSelecting;

        // 滑入式提示正在播（含它抢占指示选择的那一次）时不要抢回来：让它播完，
        // 下一帧若选择仍未结束，会重新把指示选择渐入。
        if (_form == Form.Toast) return;

        if (selecting && !AiIsChoosing)
        {
            string want = DescribeSelection();
            if (string.IsNullOrEmpty(want)) want = "请选择目标";

            if (_form != Form.Prompt)
            {
                Debug.Log($"[PromptBanner] 指示选择 = {want}（targetType={BoardSlot.currentTargetType}, " +
                          $"source={SelectionManager.CurrentSourceName ?? "null"}, " +
                          $"dispatcher={EffectDispatcher.HasCurrentEffect}）");
                ShowPrompt(want);
            }
            else if (want != _shownText)
            {
                ApplyText(want, promptColor);
            }
        }
        else if (_form == Form.Prompt)
        {
            HidePrompt();
        }
    }

    /// <summary>AI 正在自动裁决这次选择（离线 AI 的 01503 领主等也会走同一条 BeginSelection）——
    /// 那时候不必给人类弹"请你选目标"，而且它一帧就被自动选掉，只会闪一下。</summary>
    static bool AiIsChoosing =>
        SimpleAI.IsAIEvaluating || SimpleAI.forceAutoSelect || BoardSlot.enterEffectActorIsAI;

    /// <summary>己方生命值第一次降到 ≤ 阈值 → 滑入提示；回到阈值以上后重新武装。
    /// 判定按**跨越**而不是按"采样到的值"：生命值可能一帧从 10 直接掉到 -2，
    /// 中间的 (0,5] 一次也没被观测到；旧写法还会被 hp ≤ 0 提前 return 掉，整段错过。</summary>
    void WatchLowHealth()
    {
        int hp = LocalHealth();
        if (hp == int.MaxValue) return;

        if (hp != _lastLoggedHp)
        {
            _lastLoggedHp = hp;
            Debug.Log($"[PromptBanner] 读到己方生命值 hp={hp}");
        }

        if (hp > lowHealthThreshold)
        {
            _lowHealthFired = false;
            _lowHealthPending = false;
            _prevHp = hp;
            return;
        }

        // 从阈值以上跨下来的那一次才算"第一次"；此后停留在低位不重复触发
        if (_prevHp == int.MinValue || _prevHp > lowHealthThreshold) _lowHealthPending = true;
        _prevHp = hp;

        if (_lowHealthFired || !_lowHealthPending) return;

        // Toast 现在会抢占指示选择，恒返回 true；这里仍按返回值收尾，留作后续再引入
        // 「不可抢占」类提示时的兜底（那时本次不成功则保留 pending，下一帧补播）。
        if (Toast(lowHealthText, lowHealthColor))
        {
            _lowHealthFired = true;
            _lowHealthPending = false;
        }
    }

    /// <summary>己方生命值的**权威**来源：对局里写的是 NetworkPlayer.Local.currentHealth
    /// （Player.Instance.healthText 显示的也是它，Player.currentHealth 没人写）。</summary>
    static int LocalHealth()
    {
        NetworkPlayer np = NetworkPlayer.Local;
        if (np != null) return np.currentHealth;
        Player p = Player.Instance;
        return p != null ? p.currentHealth : int.MaxValue;
    }

    /// <summary>悬停淡出：不用 EventSystem（那会吃掉射射线），改做矩形命中测试。</summary>
    void UpdateHover()
    {
        bool over = false;
        if (_form != Form.None && _root != null && _formAlpha > 0.01f)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            over = RectTransformUtility.RectangleContainsScreenPoint(_root, Input.mousePosition, cam);
        }

        float target = over ? hoverAlpha : 1f;
        _hoverMul = hoverFadeTime > 0f
            ? Mathf.MoveTowards(_hoverMul, target, Time.deltaTime / hoverFadeTime)
            : target;
    }

    // ══════════════════════════════════════════════════════════════════
    // 两种形式
    // ══════════════════════════════════════════════════════════════════

    void ShowPrompt(string text)
    {
        Debug.Log($"[PromptBanner] 指示选择：{text}");
        ApplyText(text, promptColor);
        _form = Form.Prompt;
        Restart(FadeInRoutine());
    }

    void HidePrompt()
    {
        if (_form != Form.Prompt) return;
        Restart(FadeOutRoutine());
    }

    /// <summary>滑入式提示。**会抢占正在展示的指示选择** —— 「现在小心！」这类警告不能被一个
    /// 迟迟不结束的选择无限期挡住（实测：生命值掉到 2 时玩家还停在选目标里，提示一直没弹出来）。
    /// 播完后 SyncSelectionPrompt 会把仍未结束的指示选择重新渐入。</summary>
    bool Toast(string text, Color color)
    {
        Debug.Log($"[PromptBanner] 提示：{text}" + (_form == Form.Prompt ? "（抢占指示选择）" : string.Empty));
        ApplyText(text, color);
        _form = Form.Toast;
        _root.anchoredPosition = anchoredPosition;   // 复位位置与缩放再滑入（抢占时状态可能被动过）
        _root.localScale = Vector3.one;
        Restart(SlideRoutine());
        return true;
    }

    void Restart(IEnumerator routine)
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(routine);
    }

    IEnumerator FadeInRoutine()
    {
        _root.anchoredPosition = anchoredPosition;
        float t = 0f;
        while (t < fadeInTime)
        {
            t += Time.deltaTime;
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / fadeInTime));
            _formAlpha = e;
            _root.localScale = Vector3.one * Mathf.Lerp(fadeInStartScale, 1f, e);
            yield return null;
        }
        _formAlpha = 1f;
        _root.localScale = Vector3.one;
        _routine = null;   // 停留：等 HidePrompt
    }

    IEnumerator FadeOutRoutine()
    {
        float from = _formAlpha;
        float t = 0f;
        while (t < fadeOutTime)
        {
            t += Time.deltaTime;
            // 先快后慢（ease-out）：选择一结束立刻掉下去，不要 SmoothStep 那种"先慢"的拖尾
            float x = Mathf.Clamp01(t / fadeOutTime);
            float e = 1f - Mathf.Pow(1f - x, 3f);
            _formAlpha = Mathf.Lerp(from, 0f, e);
            yield return null;
        }
        _formAlpha = 0f;
        _root.localScale = Vector3.one;
        _form = Form.None;
        _routine = null;
    }

    IEnumerator SlideRoutine()
    {
        float halfW = ParentHalfWidth();
        float travel = halfW + plateSize.x * 0.5f + 40f;
        float offX = -travel;      // 左：入场起点
        float offXRight = travel;  // 右：出场终点

        // ① 从屏幕左边快速滑入
        float t = 0f;
        while (t < slideInTime)
        {
            t += Time.deltaTime;
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / slideInTime));
            _root.anchoredPosition = new Vector2(Mathf.Lerp(offX, anchoredPosition.x, e), anchoredPosition.y);
            _formAlpha = e;
            yield return null;
        }
        _root.anchoredPosition = anchoredPosition;
        _formAlpha = 1f;

        // ② 停住
        yield return new WaitForSeconds(slideHoldTime);

        // ③ 向右滑出屏幕：一条直线穿过去，不再原路退回左侧。
        //    这一程**不淡出** —— 淡出会半路就化没，看不到"从右边出去"。
        t = 0f;
        while (t < slideOutTime)
        {
            t += Time.deltaTime;
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / slideOutTime));
            _root.anchoredPosition = new Vector2(Mathf.Lerp(anchoredPosition.x, offXRight, e), anchoredPosition.y);
            yield return null;
        }
        _root.anchoredPosition = anchoredPosition;
        _formAlpha = 0f;
        _form = Form.None;
        _routine = null;
    }

    float ParentHalfWidth()
    {
        var parent = _root != null ? _root.parent as RectTransform : null;
        return parent != null && parent.rect.width > 1f ? parent.rect.width * 0.5f : 960f;
    }

    // ══════════════════════════════════════════════════════════════════
    // 文字推导：选择「XX」特性N目标 / 选择「XX」作用目标
    // ══════════════════════════════════════════════════════════════════

    static string DescribeSelection()
    {
        // ① 首选：走 EffectDispatcher 的"迁移后特性"，能拿到最精确的哪张卡、哪条特性。
        if (EffectDispatcher.HasCurrentEffect)
        {
            string id = EffectDispatcher.CurrentEffectTemplateID;
            CardData card = (CardDatabase.Instance != null && !string.IsNullOrEmpty(id))
                ? CardDatabase.Instance.GetTemplate(id) : null;
            if (card != null)
            {
                string name = string.IsNullOrEmpty(card.cardName) ? id : card.cardName;
                Trigger trig = EffectDispatcher.CurrentEffectTrigger;
                // 法术：自身没有"第几条特性"，走「作用」
                if (trig == Trigger.Spell || card.cardType == CardType.Spell)
                    return "选择「" + name + "」作用目标";

                int index = SelectionManager.TraitIndexOf(card, SelectionManager.AttributeOf(trig));
                return index > 0
                    ? "选择「" + name + "」特性" + index + "目标"
                    : "选择「" + name + "」目标";
            }
        }

        // ② 兜底：发起方登记的来源（战斗阶段直连调用等，根本没经过 Dispatcher）。
        string src = SelectionManager.CurrentSourceName;
        if (!string.IsNullOrEmpty(src))
        {
            if (SelectionManager.CurrentSourceIsSpell)
                return "选择「" + src + "」作用目标";
            int idx = SelectionManager.CurrentSourceTraitIndex;
            return idx > 0
                ? "选择「" + src + "」特性" + idx + "目标"
                : "选择「" + src + "」目标";
        }

        // ③ 最后兜底：按目标类型给中性文案。**绝不返回 null** —— 一旦返回 null，
        //    "进入选择了却没提示"这类故障就复现了（PromptBanner 的整个存在意义就是这条提示）。
        return NeutralSelectionText(BoardSlot.currentTargetType);
    }

    /// <summary>推不出具体是哪张卡时，按本次选择的目标类型给的中性文案。</summary>
    static string NeutralSelectionText(TargetType t)
    {
        switch (t)
        {
            case TargetType.SingleEnemy: return "选择敌方目标";
            case TargetType.SingleAlly: return "选择己方目标";
            case TargetType.SingleAny: return "选择任意目标";
            case TargetType.AllEnemies: return "选择敌方全体";
            case TargetType.AllAllies: return "选择己方全体";
            case TargetType.AllMinions: return "选择全部召唤物";
            case TargetType.EnemyFrontRow:
            case TargetType.EnemyBackRow:
            case TargetType.EnemyAnyRow: return "选择敌方一整排";
            case TargetType.AllyFrontRow:
            case TargetType.AllyBackRow:
            case TargetType.AllyAnyRow: return "选择己方一整排";
            default: return "请选择目标";
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // 工具
    // ══════════════════════════════════════════════════════════════════

    void ApplyText(string text, Color color)
    {
        _shownText = text;
        if (_text == null) return;
        _text.text = text;
        _text.color = color;
    }

    /// <summary>取一个「认中文」的字体，与顶栏数值 / 择牌面板同源（Serif CJK + 同一份材质）。</summary>
    static void ApplyFont(TMP_Text t)
    {
        TMP_FontAsset font = null;
        Material mat = null;

        Player p = Player.Instance;
        TMP_Text hud = p != null ? p.energyText : null;
        if (hud == null && p != null) hud = p.healthText;
        if (hud != null && hud.font != null && hud.font.HasCharacter('择'))
        {
            font = hud.font;
            mat = hud.fontSharedMaterial;
        }

        if (font == null)
        {
            TMP_FontAsset[] all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].HasCharacter('择')) { font = all[i]; break; }
            }
        }

        if (font == null) font = TMP_Settings.defaultFontAsset;
        if (font != null) t.font = font;
        if (mat != null) t.fontSharedMaterial = mat;
        Debug.Log($"[PromptBanner] 文字字体 = {(font != null ? font.name : "null（会用 TMP 默认，中文可能不显示）")}");
    }
}

/// <summary>提示条的落地兜底：主 Canvas 迟到 / 入场期间被关时，反复重试创建（4Hz，成功后只做一次 null 判断）。
/// 跨场景常驻，所以重开局（场景重载）后也能把提示条重新建出来。</summary>
public class PromptBannerBoot : MonoBehaviour
{
    static PromptBannerBoot _boot;
    float _t;

    public static void Arm()
    {
        if (_boot != null) return;
        var go = new GameObject("PromptBannerBoot");
        DontDestroyOnLoad(go);
        _boot = go.AddComponent<PromptBannerBoot>();
        Debug.Log("[PromptBanner] 重试器已武装");
    }

    void Update()
    {
        if (PromptBanner.Instance != null) return;
        if (Player.Instance == null) return;   // 不在对局里（菜单等）不建

        _t += Time.unscaledDeltaTime;
        if (_t < 0.25f) return;
        _t = 0f;
        PromptBanner.Ensure();
    }
}
