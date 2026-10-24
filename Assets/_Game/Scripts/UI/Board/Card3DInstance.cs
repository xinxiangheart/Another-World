using System.Collections;
using TMPro;
using UnityEngine;

public class Card3DInstance : MonoBehaviour
{
    internal const string SummonTracePrefix = "[SUMMON-TRACE]";
    public CardInstance cardInstance;

    internal string SummonTraceLabel => $"{SummonTracePrefix}[{name}#{GetInstanceID()}]";
    internal bool SummonTickerRegistered => _summonTickerRegistered;
    internal float SummonElapsedForTrace => _summonElapsed;

    /// <summary>本端（玩家半场）是否有召唤演出正在播放。手牌区的“恢复显示”要等它结束，
    /// 否则 Screen Space Overlay 的手牌会在飞行途中重新盖住卡。</summary>
    public static bool AnyOwnSummonAnimating => s_ownSummonVisuals > 0;

    /// <summary>诊断召唤丢失：每次只在关键节点输出，带激活态/可见渲染器/视口坐标。</summary>
    void TraceSummon(string phase, string extra = null)
    {
        if (!Application.isPlaying) return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        int enabledRenderers = 0;
        int visibleRenderers = 0;
        float minAlpha = 1f;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            enabledRenderers++;
            if (r.isVisible) visibleRenderers++;
        }
        SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < sprites.Length; i++)
        {
            SpriteRenderer sr = sprites[i];
            if (sr == null || !sr.gameObject.activeInHierarchy) continue;
            if (sr.color.a < minAlpha) minAlpha = sr.color.a;
        }
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text t = texts[i];
            if (t == null || !t.gameObject.activeInHierarchy) continue;
            if (t.alpha < minAlpha) minAlpha = t.alpha;
        }

        Transform ui = transform.Find("UIComponents");
        bool uiActive = ui == null || ui.gameObject.activeSelf;

        Camera cam = Camera.main;
        Vector3 viewport = cam != null ? cam.WorldToViewportPoint(transform.position) : Vector3.zero;
        string extraText = string.IsNullOrEmpty(extra) ? "" : " " + extra;

        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, this,
            "{0} {1} f={2} ut={3:F3} anim={4} ticker={5} elapsed={6:F3} activeSelf={7} activeHierarchy={8} compEnabled={9} tid={10} iid={11} renderers={12}/{13} visible={14} minAlpha={15:F2} ui={16} vp=({17:F2},{18:F2},{19:F2}) pos={20}{21}",
            SummonTraceLabel, phase, Time.frameCount, Time.unscaledTime, _summonAnimating, _summonTickerRegistered,
            _summonElapsed, gameObject.activeSelf, gameObject.activeInHierarchy, enabled,
            cardInstance != null ? cardInstance.templateID : "-",
            cardInstance != null ? cardInstance.instanceID : "-",
            enabledRenderers, renderers.Length, visibleRenderers, minAlpha, uiActive,
            viewport.x, viewport.y, viewport.z, transform.position, extraText);
    }

    void Awake()
    {
        // 每个 3D 卡牌实例自动挂漂浮/呼吸动画组件（无需逐个在实例化点手动添加）
        if (GetComponent<Card3DAnimator>() == null)
            gameObject.AddComponent<Card3DAnimator>();
        // 攻击动画组件（飞向/击中/返回）
        if (GetComponent<Card3DAttackAnimator>() == null)
            gameObject.AddComponent<Card3DAttackAnimator>();
        // 数值/图标弹跳（纯表现）——自动挂，无元素触发时自然空转
        if (GetComponent<CardFaceBounceFX>() == null)
            gameObject.AddComponent<CardFaceBounceFX>();
    }

    /// <summary>元素弹跳是否允许：已过首次刷新(armed)且不在召唤动画中。</summary>
    public bool ElementBounceAllowed => _bounceArmed && !_summonAnimating;

    public void UpdateValues()
    {
        // 首刷即武装：进场那次 UpdateValues 本身不弹（无前值/召唤期），此后数值真变化才弹
        _bounceArmed = true;
        // 晚赋值(cardInstance 在实例化后才 CopyFrom)后让悬停重读实例——否则 Start 已过、详情/悬停拿不到卡
        Card3DHover hover = GetComponent<Card3DHover>();
        if (hover != null) hover.RefreshCardData();
        CardDisplay3D display = GetComponent<CardDisplay3D>();
        if (display != null) display.Refresh();
        // 新 3D 卡图标（费用/类型/攻/血 + 三排）随同一触发点刷新；旧卡无此组件则跳过
        CardIcons3D icons = GetComponent<CardIcons3D>();
        if (icons != null) icons.Refresh();
    }

    /// <summary>召唤动画：从手牌 / 侧翼锚点飞出 → 顶点悬停 → 砸落 → 压扁回弹。
    /// 只影响表现，不改任何逻辑判断。fromWorld 为空时按槽位位置推算己方/敌方锚点。</summary>
    public void PlaySummonAnimation(Vector3? fromWorld = null)
    {
        if (this == null) return;
        TraceSummon("CALL", $"from={(fromWorld.HasValue ? fromWorld.Value.ToString("F2") : "auto")}");

        // 同帧两条生成/同步链路重复调用时只算一次；仍在正常推进的演出也不重播。
        bool startedThisFrame = _summonAnimating && _summonStartFrame == Time.frameCount;
        bool tickingNormally = _summonAnimating && _summonTickerRegistered
            && Time.unscaledTime - _summonLastTickTime < 0.35f;
        if (startedThisFrame || tickingNormally)
        {
            TraceSummon("SKIP_REENTRANT", $"startedThisFrame={startedThisFrame} tickingNormally={tickingNormally}");
            return;
        }

        // 域重载 / 父节点失活后再次进入：序列化进度仍在，直接接回推进器，
        // 不从头重播，更不能因为残留的 _summonAnimating 把新演出永久挡掉。
        if (_summonAnimating && _summonElapsed > 0f)
        {
            TraceSummon("RESUME_EXISTING", "domain-reload-or-reenable");
            PrepareVisualForSummon();
            CaptureSummonVisual();
            BeginSummonOverlay();
            SetSummonVisualCounted(true);
            EnforceSummonVisual();
            LockSummonPresentation();
            SummonAnimationTicker.Register(this);
            ApplySummonPose();
            TraceSummon("RESUME_EXISTING_RETURN");
            return;
        }

        PrepareVisualForSummon();
        Transform scaleTarget = GetScaleTarget();
        if (scaleTarget == null)
        {
            Debug.LogWarning($"[Summon] 缺少卡面容器 {name}");
            TraceSummon("ABORT_NO_SCALE_TARGET");
            RestoreSummonVisual();
            EndSummonOverlay();
            SetSummonVisualCounted(false);
            return;
        }

        _summonAnimating = true;
        _summonImpactTriggered = false;
        _summonRetireAfterComplete = false;
        _summonElapsed = 0f;
        _summonTraceStage = -1;
        _summonProgressFrame = Time.frameCount;
        _summonStartFrame = Time.frameCount;
        _summonBasePos = transform.position;
        _summonBaseRot = transform.rotation;
        _summonBaseScale = scaleTarget.localScale;
        _summonTo = transform.position;
        CaptureSummonVisual();
        BeginSummonOverlay();
        SetSummonVisualCounted(true);
        EnforceSummonVisual();
        Camera cam = Camera.main;
        _summonToCam = cam != null ? -cam.transform.forward : Vector3.back;
        Vector3 from = fromWorld ?? ComputeSummonOrigin(_summonTo);
        // 正向：从所属玩家的一侧朝槽位直飞，横向与目标对齐。
        from.x = _summonTo.x;
        _summonFrom = from;
        TraceSummon("START", $"from={_summonFrom.ToString("F2")} to={_summonTo.ToString("F2")} baseScale={_summonBaseScale.ToString("F3")}");

        // 同步抢占根 Transform：不能等协程第一帧再关，否则 Card3DAnimator / 攻击动画
        // 可能在同帧写入 localPosition/localScale，把飞行起点或落点覆盖掉。
        LockSummonPresentation();
        ApplySummonPose();
        // 最后一个动作再注册：上面的 ApplySummonPose 若当场完成（例如容器缺失）不会留下空目标。
        if (_summonAnimating)
        {
            SummonAnimationTicker.Register(this);
            TraceSummon("REGISTERED");
        }
    }

    public bool IsSummonAnimating => _summonAnimating;

    /// <summary>攻击/进场等后续演出等待召唤结束。
    /// 默认连续 3 秒没有任何进度才强制收尾：长帧只会让演出变慢，不会被误判成卡死；
    /// 真正的异常停滞也不会把攻击、展示层或手牌恢复永久卡住。</summary>
    public IEnumerator WaitForSummonAnimation(float timeout = 3f, bool forceCompleteOnTimeout = true)
    {
        float stallLimit = timeout > 0f ? timeout : 3f;
        float lastElapsed = _summonElapsed;
        float lastProgressTime = Time.unscaledTime;

        while (this != null && _summonAnimating)
        {
            yield return null;
            if (this == null || !_summonAnimating) break;

            if (_summonElapsed > lastElapsed + 0.0001f)
            {
                lastElapsed = _summonElapsed;
                lastProgressTime = Time.unscaledTime;
            }
            else if (Time.unscaledTime - lastProgressTime >= stallLimit)
            {
                if (forceCompleteOnTimeout) CompleteSummonAnimation();
                break;
            }
        }
    }

    /// <summary>立即完成召唤动画（仅调用方明确需要掐掉演出时使用）：复位位置/旋转/缩放、恢复浮动。</summary>
    public void CompleteSummonAnimation()
    {
        if (this == null || !_summonAnimating) return;

        TraceSummon("COMPLETE", $"retireAfter={_summonRetireAfterComplete}");

        _summonAnimating = false;
        _summonImpactTriggered = false;
        _summonElapsed = 0f;
        _summonStartFrame = -1;
        SummonAnimationTicker.Unregister(this);
        RestoreSummonVisual();
        EndSummonOverlay();
        SetSummonVisualCounted(false);

        RestoreSummonPresentation(
            GetScaleTarget(), _summonBasePos, _summonBaseRot, _summonBaseScale,
            GetComponent<Card3DHover>(), GetComponent<Card3DAnimator>());
        ApplyPendingHiddenState();
        RetireAfterSummonIfRequested();
    }

    /// <summary>同步层收到“移除这个模型”但模型还在飞入时调用：等演出自然结束后再销毁，
    /// 避免过期 RPC/上报把刚打出的召唤动画直接抹掉。调用方不要再立即 Destroy。</summary>
    public void RequestDestroyAfterSummon()
    {
        if (this == null) return;
        TraceSummon("REQUEST_RETIRE_AFTER_SUMMON");
        _summonRetireAfterComplete = true;
        Card3DHover hover = GetComponent<Card3DHover>();
        if (hover != null) hover.enabled = false;
    }

    // 召唤期间收到的“翻面/隐藏”请求先压住：飞行中的正面容器不能被 ShowBack 关掉。
    // 演出结束后再由 CompleteSummonAnimation 统一套用，保证终态仍是同步来的那一面。
    bool _hasPendingHiddenState;
    bool _pendingHidden;
    bool _pendingHiddenIsAttachment;

    public void DeferHiddenUntilSummonComplete(bool hidden, bool isAttachment)
    {
        _hasPendingHiddenState = true;
        _pendingHidden = hidden;
        _pendingHiddenIsAttachment = isAttachment;
    }

    void ApplyPendingHiddenState()
    {
        if (!_hasPendingHiddenState) return;
        _hasPendingHiddenState = false;
        Card3DHover.SetHidden(gameObject, _pendingHidden, _pendingHiddenIsAttachment);
    }

    void RestoreSummonPresentation(
        Transform target, Vector3 basePos, Quaternion baseRot, Vector3 baseScale,
        Card3DHover hover, Card3DAnimator floatAnim)
    {
        if (target != null) target.localScale = baseScale;
        transform.position = basePos;
        transform.rotation = baseRot;

        if (hover != null) hover.enabled = true;
        if (floatAnim != null)
            floatAnim.ResumeAtCurrentTransform();
    }

    // 演出进度必须序列化：域重载会销毁协程与普通运行时字段，只有进度能带过重载。
    // 推进器登记与“本帧是否已推进”属于运行时状态，故意不序列化；重载后由 OnEnable 重新接回。
    [SerializeField, HideInInspector] bool _summonAnimating;
    [SerializeField, HideInInspector] bool _summonImpactTriggered;
    [SerializeField, HideInInspector] bool _summonRetireAfterComplete;
    [SerializeField, HideInInspector] float _summonElapsed;
    [SerializeField, HideInInspector] Vector3 _summonBasePos;
    [SerializeField, HideInInspector] Vector3 _summonBaseScale = Vector3.one;
    [SerializeField, HideInInspector] Quaternion _summonBaseRot = Quaternion.identity;
    [SerializeField, HideInInspector] Vector3 _summonFrom;
    [SerializeField, HideInInspector] Vector3 _summonTo;
    [SerializeField, HideInInspector] Vector3 _summonToCam = Vector3.back;
    bool _bounceArmed; // 首刷后置 true；供 ElementBounceAllowed（抑制进场/召唤期弹跳）
    int _summonProgressFrame;
    [System.NonSerialized] bool _summonTickerRegistered;
    [System.NonSerialized] float _summonLastTickTime;
    [System.NonSerialized] int _summonStartFrame = -1;
    [System.NonSerialized] int _summonTraceStage = -1;

    // ── 召唤期“硬可见”快照 ──────────────────────────────────────────────
    // 任何路径（翻面、压暗、展示层、状态刷新、运行时重建）只要把卡面任意一层
    // 关掉或写成透明，玩家看到的就是“槽位凭空出现”。演出期间每帧按快照恢复，
    // 并把 SortingOrder 抬到最上层；演出结束再把 SortingOrder 原样写回。
    struct SummonSpriteVisual
    {
        public SpriteRenderer renderer;
        public bool enabled;
        public Color color;
        public Sprite sprite;
        public int sortingLayerID;
        public int sortingOrder;
    }

    struct SummonTextVisual
    {
        public TMP_Text text;
        public bool enabled;
        public float alpha;
    }

    struct SummonRendererVisual
    {
        public Renderer renderer;
        public bool enabled;
    }

    struct SummonGroupVisual
    {
        public CanvasGroup group;
        public float alpha;
    }

    struct SummonChildVisual
    {
        public GameObject gameObject;
        public bool activeSelf;
    }

    const int SummonTopSortingOrder = 30000;

    SummonSpriteVisual[] _summonSpriteVisuals;
    SummonTextVisual[] _summonTextVisuals;
    SummonRendererVisual[] _summonRendererVisuals;
    SummonGroupVisual[] _summonGroupVisuals;
    SummonChildVisual[] _summonChildVisuals;
    [System.NonSerialized] bool _summonVisualCaptured;
    [System.NonSerialized] bool _summonVisualCounted;
    [System.NonSerialized] bool _summonCountsForHand;
    [System.NonSerialized] bool _summonOverlayActive;
    static int s_ownSummonVisuals;

    bool BeginSummonOverlay()
    {
        if (_summonOverlayActive) return true;
        _summonOverlayActive = SummonOverlayRenderer.Begin(this);
        return _summonOverlayActive;
    }

    void EndSummonOverlay()
    {
        if (!_summonOverlayActive) return;
        _summonOverlayActive = false;
        SummonOverlayRenderer.End(this);
    }

    /// <summary>推进器在每个 LateUpdate 末尾（所有普通脚本之后）再压一次可见态，
    /// 防止同帧后续逻辑把卡面关掉或写透明。</summary>
    internal void EnforceSummonVisibleNow()
    {
        if (!_summonAnimating) return;
        EnsureSummonVisible();
    }

    internal void MarkSummonTickerRegistered(bool registered)
    {
        _summonTickerRegistered = registered;
    }

    /// <summary>召唤动画的单帧推进上限：长帧最多推进 0.1s，避免整段演出被一帧跳完。</summary>
    static float SummonDeltaTime => Mathf.Min(Time.unscaledDeltaTime, 0.1f);

    void OnEnable()
    {
        // 域重载 / SetActive 往返后，序列化进度还在、推进器的静态引用已经没了。
        // 先重新登记常驻推进器，再套用当前帧姿态；即使父节点稍后才激活也不会停住。
        if (!Application.isPlaying || !_summonAnimating) return;
        TraceSummon("ON_ENABLE_RESUME");
        CaptureSummonVisual();
        BeginSummonOverlay();
        SetSummonVisualCounted(true);
        EnforceSummonVisual();
        SummonAnimationTicker.Register(this);
        LockSummonPresentation();
        ApplySummonPose();
    }

    void OnDisable()
    {
        if (_summonAnimating) TraceSummon("ON_DISABLE_MID_SUMMON");
    }

    void Update()
    {
        if (!_summonAnimating) return;
        if (_summonTickerRegistered) return;

        // 兜底：常驻推进器意外缺失时就地接管。Register 成功后本帧由推进器统一跑，
        // 避免一次帧里推进两次导致演出加速。
        SummonAnimationTicker.Register(this);
        if (!_summonTickerRegistered) TickSummon(SummonDeltaTime);
    }

    internal void TickSummon(float dt)
    {
        if (!_summonAnimating) return;
        EnsureSummonVisible();

        bool firstTick = _summonLastTickTime <= 0f;
        _summonLastTickTime = Time.unscaledTime;
        _summonProgressFrame = Time.frameCount;
        if (dt <= 0f) return;

        _summonElapsed += Mathf.Min(dt, 0.1f);
        if (firstTick) TraceSummon("FIRST_TICK", $"dt={dt:F4}");
        ApplySummonPose();
    }

    void OnDestroy()
    {
        if (_summonAnimating) TraceSummon("ON_DESTROY_MID_SUMMON");
        EndSummonOverlay();
        SetSummonVisualCounted(false);
        SummonAnimationTicker.Unregister(this);
    }

    /// <summary>生成入口统一调用：实例化后立即触发召唤动画（同步把可见正面容器缩到 0.86，
    /// 避免先以完整尺寸闪现一帧）。fromWorld 可传手牌位置；不传则按槽位推算。</summary>
    public static void PlaySummonOn(GameObject model, Vector3? fromWorld = null)
    {
        if (model == null)
        {
            Debug.LogWarning($"{SummonTracePrefix} REQUEST model=null caller={DescribeSummonCaller()}");
            return;
        }

        string caller = DescribeSummonCaller();
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, model,
            "{0}[{1}#{2}] REQUEST caller={3} activeSelf={4} activeHierarchy={5} from={6}",
            SummonTracePrefix, model.name, model.GetInstanceID(), caller,
            model.activeSelf, model.activeInHierarchy,
            fromWorld.HasValue ? fromWorld.Value.ToString("F2") : "auto");

        if (!model.activeSelf) model.SetActive(true);
        Card3DInstance inst = model.GetComponent<Card3DInstance>();
        if (inst == null)
        {
            Debug.LogWarning($"{SummonTracePrefix}[{model.name}#{model.GetInstanceID()}] ABORT_NO_CARD3DINSTANCE caller={caller}");
            return;
        }
        if (!inst.enabled) inst.enabled = true;
        inst.TraceSummon("ACCEPT", $"caller={caller}");
        inst.PrepareVisualForSummon();
        inst.PlaySummonAnimation(fromWorld);
        inst.TraceSummon("ACCEPT_RETURN", "PlaySummonOn finished");
    }

    static string DescribeSummonCaller()
    {
        try
        {
            var trace = new System.Diagnostics.StackTrace(2, true);
            for (int i = 0; i < trace.FrameCount; i++)
            {
                var frame = trace.GetFrame(i);
                var method = frame?.GetMethod();
                if (method == null || method.DeclaringType == typeof(Card3DInstance)) continue;
                string file = frame.GetFileName();
                if (!string.IsNullOrEmpty(file)) file = System.IO.Path.GetFileName(file);
                return $"{method.DeclaringType?.Name}.{method.Name} @ {file}:{frame.GetFileLineNumber()}";
            }
        }
        catch { }
        return "unknown";
    }

    /// <summary>生成入口在挂好 CardInstance / UpdateValues 后调用：确保正面容器可见，
    /// 不会在「UIComponents 仍关着」或卡背残留的状态下播放飞入。</summary>
    public void PrepareVisualForSummon()
    {
        CardDisplay3D display = GetComponent<CardDisplay3D>();
        if (display != null) display.ShowFront();
        Transform ui = transform.Find("UIComponents");
        if (ui != null && !ui.gameObject.activeSelf) ui.gameObject.SetActive(true);
        CaptureSummonVisual();
        EnforceSummonVisual();
    }

    /// <summary>附着滑入动画：从下一个附着牌位置滑到自己理论位置（约0.5s，仅表现，不影响附着逻辑/位置结算）。</summary>
    public void PlayAttachSlideIn(Vector3 from, Vector3 to, float duration = 0.5f)
    {
        StartCoroutine(AttachSlideRoutine(from, to, duration));
    }

    IEnumerator AttachSlideRoutine(Vector3 from, Vector3 to, float duration)
    {
        // 暂停漂浮：漂浮每帧改 localPosition，与滑入的世界位置插值冲突
        Card3DAnimator floatAnim = GetComponent<Card3DAnimator>();
        if (floatAnim != null) floatAnim.enabled = false;

        transform.position = from; // 立即定位到起点，避免在目标位置闪现一帧
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float eased = 1f - (1f - k) * (1f - k); // ease-out 滑入
            transform.position = Vector3.Lerp(from, to, eased);
            yield return null;
        }
        transform.position = to; // 精确到位（与 GetAttachWorldPos 理论位置一致）

        // 恢复漂浮：先重捕基准位置（滑入后基准已变），否则漂浮会拉回起点
        if (floatAnim != null) { floatAnim.UpdateBaseLocalPos(); floatAnim.enabled = true; }
    }

    void LockSummonPresentation()
    {
        Card3DAnimator floatAnim = GetComponent<Card3DAnimator>();
        if (floatAnim != null) floatAnim.SuspendForExternalAnimation();
        Card3DHover hover = GetComponent<Card3DHover>();
        if (hover != null) hover.enabled = false;
        _summonProgressFrame = Time.frameCount;
    }

    struct SummonCurve
    {
        public Vector3 antPos;
        public Vector3 apex;
        public Vector3 ctrl;
        public float anticipation;
        public float flyDur;
        public float hangDur;
        public float dropDur;
        public float impactHold;
        public float settleDur;
        public float squash;
        public float shakeAmp;
        public float shakeDur;

        public float ImpactAt => anticipation + flyDur + hangDur + dropDur;
        public float Total => ImpactAt + impactHold + settleDur;
    }

    SummonCurve BuildSummonCurve()
    {
        AnimationConfig cfg = AnimationConfig.Load();
        SummonCurve c = new SummonCurve
        {
            anticipation = 0.06f,
            flyDur = cfg != null ? Mathf.Max(0.0001f, cfg.summonLaunchDuration) : 0.22f,
            hangDur = cfg != null ? Mathf.Max(0f, cfg.summonHangDuration) : 0.08f,
            dropDur = cfg != null ? Mathf.Max(0.0001f, cfg.summonDropDuration) : 0.08f,
            impactHold = 0.035f,
            settleDur = cfg != null ? Mathf.Max(0.0001f, cfg.summonSettleDuration) : 0.18f,
            squash = cfg != null ? cfg.summonSquash : 0.14f,
            shakeAmp = cfg != null ? cfg.summonShakeStrength : 0.10f,
            shakeDur = cfg != null ? cfg.summonShakeDuration : 0.12f
        };

        // 顶点高度与飞行时长按当前起落点解析计算；所有输入都已序列化，域重载后同一条曲线。
        float apexHeight = cfg != null ? cfg.summonApexHeight : 0.9f;
        float outward = _summonTo.y < 1f ? -1f : 1f;
        Vector3 pullDir = _summonFrom - _summonTo;
        pullDir.z = 0f;
        if (pullDir.sqrMagnitude < 0.0001f) pullDir = Vector3.up * outward;

        c.antPos = _summonFrom + pullDir.normalized * 0.10f;
        c.apex = _summonTo + Vector3.up * apexHeight + _summonToCam * 0.35f;
        c.ctrl = Vector3.Lerp(c.antPos, c.apex, 0.5f) + _summonToCam * 0.25f;
        float distance = Vector3.Distance(c.antPos, c.apex);
        c.flyDur = Mathf.Clamp(c.flyDur + distance * 0.01f, c.flyDur, c.flyDur + 0.08f);
        return c;
    }

    void ApplySummonPose()
    {
        EnsureSummonFrontVisible();
        Transform target = GetScaleTarget();
        if (target == null)
        {
            CompleteSummonAnimation();
            return;
        }

        SummonCurve c = BuildSummonCurve();
        float t = Mathf.Max(0f, _summonElapsed);
        Vector3 baseScale = _summonBaseScale;

        int stage = t < c.anticipation ? 1
            : t < c.anticipation + c.flyDur ? 2
            : t < c.anticipation + c.flyDur + c.hangDur ? 3
            : t < c.ImpactAt ? 4
            : t < c.ImpactAt + c.impactHold ? 5 : 6;
        if (stage != _summonTraceStage)
        {
            _summonTraceStage = stage;
            TraceSummon("STAGE", $"stage={stage} t={t:F3} impactAt={c.ImpactAt:F3} total={c.Total:F3}");
        }

        if (t < c.anticipation)
        {
            // 1) 蓄力：从起手点再后拉一点，压到 0.84。
            float k = Mathf.Clamp01(t / c.anticipation);
            float e = 1f - Mathf.Pow(1f - k, 2f);
            transform.position = Vector3.Lerp(_summonFrom, c.antPos, e);
            transform.rotation = _summonBaseRot * Quaternion.Euler(-14f, 0f, 0f);
            target.localScale = baseScale * Mathf.Lerp(0.86f, 0.84f, e);
        }
        else if (t < c.anticipation + c.flyDur)
        {
            // 2) 飞出：二次贝塞尔，起步快、接近顶点慢。
            float k = Mathf.Clamp01((t - c.anticipation) / c.flyDur);
            float e = 1f - Mathf.Pow(1f - k, 3f);
            transform.position = Bezier2(c.antPos, c.ctrl, c.apex, e);
            transform.rotation = _summonBaseRot * Quaternion.Euler(Mathf.Lerp(-14f, 0f, e), 0f, 0f);
            target.localScale = baseScale * Mathf.Lerp(0.84f, 1.06f, e);
        }
        else if (t < c.anticipation + c.flyDur + c.hangDur)
        {
            // 3) 顶点悬停：给下落留出高度差。
            float k = Mathf.Clamp01((t - c.anticipation - c.flyDur) / c.hangDur);
            transform.position = c.apex + Vector3.up * (0.06f * Mathf.Sin(k * Mathf.PI));
            transform.rotation = _summonBaseRot;
            target.localScale = baseScale * 1.06f;
        }
        else if (t < c.ImpactAt)
        {
            // 4) 砸下：ease-in，俯仰向前压。
            float k = Mathf.Clamp01((t - c.anticipation - c.flyDur - c.hangDur) / c.dropDur);
            float e = k * k;
            transform.position = Vector3.Lerp(c.apex, _summonTo, e);
            transform.rotation = _summonBaseRot * Quaternion.Euler(Mathf.Lerp(0f, 8f, k), 0f, 0f);
            target.localScale = baseScale * Mathf.Lerp(1.06f, 1f, k);
        }
        else if (t < c.ImpactAt + c.impactHold)
        {
            // 5) 落地硬停：精确到位 + 压扁。
            transform.position = _summonTo;
            transform.rotation = _summonBaseRot;
            target.localScale = Vector3.Scale(baseScale, new Vector3(1.08f, 1f - c.squash, 1.08f));
        }
        else
        {
            // 6) 回弹：阻尼余弦，Y 先压后弹、XZ 先胀后收。
            float k = Mathf.Clamp01((t - c.ImpactAt - c.impactHold) / c.settleDur);
            float decay = Mathf.Exp(-7f * k);
            float osc = Mathf.Cos(k * Mathf.PI * 3f);
            float xz = 1f + 0.08f * decay * osc;
            float y = 1f - c.squash * decay * osc;
            transform.position = _summonTo;
            transform.rotation = _summonBaseRot;
            target.localScale = Vector3.Scale(baseScale, new Vector3(xz, y, xz));
        }

        ClampSummonPositionToViewport();

        if (!_summonImpactTriggered && t >= c.ImpactAt)
        {
            _summonImpactTriggered = true;
            PlaySummonImpact(c);
        }

        if (t >= c.Total)
        {
            transform.position = _summonTo;
            transform.rotation = _summonBaseRot;
            target.localScale = baseScale;
            CompleteSummonAnimation();
        }
    }

    /// <summary>飞行途中把卡心夹进视口安全区。起手点被夹过，但贝塞尔控制点/顶点仍可能
    /// 把卡带到屏幕外（尤其是宽屏 + 透视相机）；任何一帧都不允许整张卡在视口外。</summary>
    void ClampSummonPositionToViewport()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 vp = cam.WorldToViewportPoint(transform.position);
        if (vp.z <= 0f) return;
        float x = Mathf.Clamp(vp.x, 0.07f, 0.93f);
        float y = Mathf.Clamp(vp.y, 0.10f, 0.93f);
        if (Mathf.Approximately(x, vp.x) && Mathf.Approximately(y, vp.y)) return;

        vp.x = x;
        vp.y = y;
        transform.position = cam.ViewportToWorldPoint(vp);
    }

    /// <summary>演出开始前抓一份“可见状态”快照：每层渲染器开关 / 颜色 alpha / TMP alpha /
    /// CanvasGroup alpha / SortingOrder。之后每帧按快照恢复，任何外部逻辑都藏不掉这张卡。</summary>
    void CaptureSummonVisual()
    {
        if (_summonVisualCaptured) return;

        SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>(true);
        _summonSpriteVisuals = new SummonSpriteVisual[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            SpriteRenderer sr = sprites[i];
            _summonSpriteVisuals[i] = new SummonSpriteVisual
            {
                renderer = sr,
                enabled = sr != null && sr.enabled,
                color = sr != null ? sr.color : Color.white,
                sprite = sr != null ? sr.sprite : null,
                sortingLayerID = sr != null ? sr.sortingLayerID : 0,
                sortingOrder = sr != null ? sr.sortingOrder : 0,
            };
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        _summonRendererVisuals = new SummonRendererVisual[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            _summonRendererVisuals[i] = new SummonRendererVisual
            {
                renderer = renderers[i],
                enabled = renderers[i] != null && renderers[i].enabled,
            };
        }

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        _summonTextVisuals = new SummonTextVisual[texts.Length];
        for (int i = 0; i < texts.Length; i++)
        {
            _summonTextVisuals[i] = new SummonTextVisual
            {
                text = texts[i],
                enabled = texts[i] != null && texts[i].enabled,
                alpha = texts[i] != null ? texts[i].alpha : 1f,
            };
        }

        CanvasGroup[] groups = GetComponentsInChildren<CanvasGroup>(true);
        _summonGroupVisuals = new SummonGroupVisual[groups.Length];
        for (int i = 0; i < groups.Length; i++)
        {
            _summonGroupVisuals[i] = new SummonGroupVisual
            {
                group = groups[i],
                alpha = groups[i] != null ? groups[i].alpha : 1f,
            };
        }

        Transform[] children = GetComponentsInChildren<Transform>(true);
        _summonChildVisuals = new SummonChildVisual[children.Length];
        for (int i = 0; i < children.Length; i++)
        {
            _summonChildVisuals[i] = new SummonChildVisual
            {
                gameObject = children[i] != null ? children[i].gameObject : null,
                activeSelf = children[i] != null && children[i].gameObject.activeSelf,
            };
        }

        _summonVisualCaptured = true;
    }

    /// <summary>把快照状态重新按到卡面上：渲染器启用、alpha 不低于起手值、正面容器激活、
    /// SortingOrder 抬到最上层。只负责“看得见”，不改任何逻辑/隐藏终态。</summary>
    void EnforceSummonVisual()
    {
        if (!_summonVisualCaptured) return;
        if (cardInstance != null && cardInstance.isDead) return;

        if (!gameObject.activeSelf) gameObject.SetActive(true);

        // 正面容器 / 卡图 / 文字对象被 ShowBack / HideAllInfo 关掉时按起手态恢复；
        // 起手就关闭的层（ModelRoot 卡背网格、无卡图的 CardArt）保持关闭，避免重影。
        if (_summonChildVisuals != null)
        {
            for (int i = 0; i < _summonChildVisuals.Length; i++)
            {
                GameObject go = _summonChildVisuals[i].gameObject;
                if (go != null && go.activeSelf != _summonChildVisuals[i].activeSelf)
                    go.SetActive(_summonChildVisuals[i].activeSelf);
            }
        }

        if (_summonRendererVisuals != null)
        {
            for (int i = 0; i < _summonRendererVisuals.Length; i++)
            {
                Renderer r = _summonRendererVisuals[i].renderer;
                if (r == null) continue;
                if (r.enabled != _summonRendererVisuals[i].enabled)
                    r.enabled = _summonRendererVisuals[i].enabled;
                if (r.enabled) r.forceRenderingOff = false;
            }
        }

        if (_summonSpriteVisuals != null)
        {
            for (int i = 0; i < _summonSpriteVisuals.Length; i++)
            {
                SpriteRenderer sr = _summonSpriteVisuals[i].renderer;
                if (sr == null) continue;
                if (sr.enabled != _summonSpriteVisuals[i].enabled)
                    sr.enabled = _summonSpriteVisuals[i].enabled;
                if (!sr.enabled) continue;

                if (sr.sprite == null && _summonSpriteVisuals[i].sprite != null)
                    sr.sprite = _summonSpriteVisuals[i].sprite;
                sr.sortingLayerID = _summonSpriteVisuals[i].sortingLayerID;
                if (sr.sortingOrder < SummonTopSortingOrder) sr.sortingOrder = SummonTopSortingOrder;

                // 只把被外部写低的 alpha 拉回起手值；起手就是透明的层（无卡图等）不动。
                Color captured = _summonSpriteVisuals[i].color;
                if (captured.a > 0.01f && sr.color.a + 0.01f < captured.a)
                {
                    Color c = sr.color;
                    c.a = captured.a;
                    sr.color = c;
                }
            }
        }

        if (_summonTextVisuals != null)
        {
            for (int i = 0; i < _summonTextVisuals.Length; i++)
            {
                TMP_Text t = _summonTextVisuals[i].text;
                if (t == null) continue;
                if (t.enabled != _summonTextVisuals[i].enabled)
                    t.enabled = _summonTextVisuals[i].enabled;
                float captured = _summonTextVisuals[i].alpha;
                if (captured > 0.01f && t.alpha + 0.01f < captured) t.alpha = captured;
            }
        }

        if (_summonGroupVisuals != null)
        {
            for (int i = 0; i < _summonGroupVisuals.Length; i++)
            {
                CanvasGroup g = _summonGroupVisuals[i].group;
                if (g == null) continue;
                float captured = _summonGroupVisuals[i].alpha;
                if (captured > 0.01f && g.alpha + 0.01f < captured) g.alpha = captured;
            }
        }
    }

    /// <summary>演出结束时只写回 SortingOrder / Renderer.enabled —— alpha 恢复到起手值本身
    /// 就是终态，不需要再动（避免覆盖演出期间正常发生的颜色刷新）。</summary>
    void RestoreSummonVisual()
    {
        if (!_summonVisualCaptured) return;

        if (_summonSpriteVisuals != null)
        {
            for (int i = 0; i < _summonSpriteVisuals.Length; i++)
            {
                SpriteRenderer sr = _summonSpriteVisuals[i].renderer;
                if (sr != null) sr.sortingOrder = _summonSpriteVisuals[i].sortingOrder;
            }
        }
        if (_summonRendererVisuals != null)
        {
            for (int i = 0; i < _summonRendererVisuals.Length; i++)
            {
                Renderer r = _summonRendererVisuals[i].renderer;
                if (r == null) continue;
                // 起手被关闭的渲染器（无卡图层等）恢复关闭；起手开启的保持开启。
                if (!_summonRendererVisuals[i].enabled) r.enabled = false;
            }
        }

        _summonVisualCaptured = false;
        _summonSpriteVisuals = null;
        _summonTextVisuals = null;
        _summonRendererVisuals = null;
        _summonGroupVisuals = null;
        _summonChildVisuals = null;
    }

    void SetSummonVisualCounted(bool counted)
    {
        if (_summonVisualCounted == counted) return;
        _summonVisualCounted = counted;
        if (counted)
        {
            _summonCountsForHand = transform.position.y < 1f; // 己方半场（前排 -0.02 / 后排 -1.86）；敌方在 2.04 以上
            if (_summonCountsForHand) s_ownSummonVisuals++;
        }
        else
        {
            if (_summonCountsForHand) s_ownSummonVisuals = Mathf.Max(0, s_ownSummonVisuals - 1);
            _summonCountsForHand = false;
        }
    }

    /// <summary>召唤演出期间无论谁调了 ShowBack / SetActive(false)，下一帧都恢复正面容器。
    /// 只做表现兜底，不改隐藏态本身；真正要隐藏的终态由 DeferHiddenUntilSummonComplete 延后套用。</summary>
    void EnsureSummonFrontVisible()
    {
        if (!_summonAnimating) return;
        Transform ui = transform.Find("UIComponents");
        if (ui == null || ui.gameObject.activeSelf) return;

        Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, this,
            "{0} ENSURE_FRONT_REACTIVATE elapsed={1:F3} activeSelf={2} activeHierarchy={3}",
            SummonTraceLabel, _summonElapsed, ui.gameObject.activeSelf, ui.gameObject.activeInHierarchy);
        ui.gameObject.SetActive(true);
    }

    void EnsureSummonVisible()
    {
        if (!_summonAnimating) return;
        if (cardInstance != null && cardInstance.isDead) return;

        if (!gameObject.activeSelf)
        {
            Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, this,
                "{0} ENSURE_ROOT_REACTIVATE elapsed={1:F3} parent={2}",
                SummonTraceLabel, _summonElapsed, transform.parent != null ? transform.parent.name : "null");
            gameObject.SetActive(true);
        }
        EnsureSummonFrontVisible();
        // 通道若因相机/RT 未就绪在第一帧失败，之后每帧重试；失败时卡牌仍留在主相机，
        // 不会因为“改过 Layer 却没人渲染”而消失。
        if (!_summonOverlayActive) BeginSummonOverlay();
        EnforceSummonVisual();
    }

    void PlaySummonImpact(SummonCurve c)
    {
        Vector3 fxPos = _summonTo + Vector3.down * 0.85f;
        CardSummonFX.PlayImpact(fxPos, 1f);
        CardSummonFX.PlaySlotFlash(FindSummonSlot(gameObject, _summonTo));
        CameraShake.Impulse(c.shakeAmp, c.shakeDur);
    }

    void RetireAfterSummonIfRequested()
    {
        if (!_summonRetireAfterComplete) return;
        _summonRetireAfterComplete = false;

        // 只清“仍指向自己”的槽位；期间若已经换了新卡，不能误清新模型。
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm != null)
        {
            BoardSlot[] slots = bm.GetAllSlots();
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].currentCard3D == gameObject)
                    slots[i].SetCard(null);
            }
        }
        Destroy(gameObject);
    }

    /// <summary>没有手牌来源时（Token / 效果生成 / 敌方）按槽位推算起手点：
    /// 己方从下方、敌方从上方，并沿该玩家方向「正向」对准槽位飞入（不再横向扇开）。</summary>
    static Vector3 ComputeSummonOrigin(Vector3 to)
    {
        Camera cam = Camera.main;
        Vector3 toCam = cam != null ? -cam.transform.forward : Vector3.back;
        float outward = to.y < 1f ? -1f : 1f;
        // 越靠后排，起手离棋盘越远；横向保持在槽位正后方
        float row = Mathf.Clamp01(Mathf.Abs(to.y - 1f) / 2.2f);
        float y = to.y + outward * (2.0f + 0.5f * row);
        Vector3 from = new Vector3(to.x, y, to.z) + toCam * 0.6f;

        // 旧动画只缩放槽位上的卡，从不把卡挪出屏幕；新动画会写到玩家一侧，
        // 后排/手牌区的起手点可能整张卡都在相机视口外，表现为「看不到起手，
        // 只在槽位上突然出现」。按视口夹住起手点（保持与槽位同列）。
        // 注意：夹在视口内不等于不被 UI 盖住。本端手牌 / 对方手牌排都是
        // Screen Space Overlay，正常情况下落位入口会先隐藏本端手牌；若某条
        // 路径漏了隐藏，就把起手点抬到手牌排之外，至少保证动画能被看到。
        if (cam != null)
        {
            Vector3 vp = cam.WorldToViewportPoint(from);
            // 只关心本端手牌是否还挡着下半屏；场景里另有一套对方手牌管理器。
            HandManager hm = Player.Instance != null && Player.Instance.myHandManager != null
                ? Player.Instance.myHandManager
                : Object.FindObjectOfType<HandManager>();
            bool ownHandVisible = hm == null || !hm.IsHandHidden;
            bool opponentRowVisible = OpponentHandRow.Instance != null;
            float slotY = cam.WorldToViewportPoint(to).y;
            // 起手点必须仍在槽位下方（己方）/上方（敌方），不能为了躲手牌越过槽位。
            float minY = outward < 0f
                ? (ownHandVisible ? Mathf.Min(0.30f, slotY - 0.04f) : 0.10f)
                : -0.10f;
            float maxY = outward < 0f ? 1.05f : (opponentRowVisible ? 0.72f : 0.95f);
            float clampedY = Mathf.Clamp(vp.y, minY, maxY);
            if (!Mathf.Approximately(clampedY, vp.y))
            {
                vp.y = clampedY;
                from = cam.ViewportToWorldPoint(vp);
                from.x = to.x;
            }
        }
        return from;
    }

    /// <summary>落到哪个槽位就闪哪一格：先按 currentCard3D 认领，再按平面距离兜底。
    /// 只用于表现，找不到时安静跳过。</summary>
    static BoardSlot FindSummonSlot(GameObject card, Vector3 worldPos)
    {
        BoardManager bm = Object.FindObjectOfType<BoardManager>();
        if (bm == null) return null;

        BoardSlot[] slots = bm.GetAllSlots();
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] != null && slots[i].currentCard3D == card) return slots[i];

        BoardSlot best = null;
        float bestDist = 1.1f;
        for (int i = 0; i < slots.Length; i++)
        {
            BoardSlot s = slots[i];
            if (s == null) continue;
            Vector3 p = s.transform.position;
            float d = new Vector2(p.x - worldPos.x, p.y - worldPos.y).magnitude;
            if (d < bestDist) { bestDist = d; best = s; }
        }
        return best;
    }

    static Vector3 Bezier2(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    /// <summary>取缩放动画的目标层：可见正面容器 UIComponents（新卡正面视觉=UIComponents 卡面精灵+文字+图标，
    /// ModelRoot 模型盒是卡背，正面被 CardDisplay3D.ShowFront 禁用）；旧卡无 UIComponents → 整卡根。</summary>
    Transform GetScaleTarget()
    {
        Transform t = transform.Find("UIComponents");
        if (t != null) return t;
        return transform;
    }
}
