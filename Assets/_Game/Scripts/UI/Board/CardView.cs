using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

[RequireComponent(typeof(Image), typeof(CanvasGroup))]
public class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [HideInInspector] public RectTransform rectTransform;
    [HideInInspector] public Vector3 targetPos;
    [HideInInspector] public Quaternion targetRotation = Quaternion.identity;
    [HideInInspector] public HandManager handManager;

    public static bool IsAnyCardDragging = false;
    public System.Action<CardInstance> OnCardClicked;
    private CanvasGroup canvasGroup;
    private Vector3 originalScale;
    private int originalSibling;

    /// <summary>悬停上浮高度 / 放大倍率（悬停期间保持，不缩回）。</summary>
    const float HOVER_RAISE = 30f;
    const float HOVER_SCALE = 1.15f;
    bool _hovered;
    /// <summary>当前是否被悬停（HandManager 布局让位用）。</summary>
    public bool IsHovered => _hovered;

    // ── 整手"非己方回合"压暗态（HandManager 统一驱动）─────────────
    // _dimScale<1 整手缩小（悬停放大仍在此倍率基础上 ×HOVER_SCALE）；_dimOffsetY<0 整手下移，部分移出视野。
    float _dimScale = 1f;
    float _dimOffsetY = 0f;

    // ── 压暗视觉：对卡面本身"去饱和 + 压亮度"，不是盖一层灰 ──────────
    // 旧做法是往卡面盖一张半透明灰图。alpha 混合 out = lerp(卡面, 灰, a) 同时干了两件坏事：
    //   ① 把黑位抬到灰（灰×a）→ 卡面暗部整片发灰，读起来像"蒙了张阴影纸"；
    //   ② 把对比度整体乘 (1-a) → 明度阶梯被压平，缩到卡面尺寸时结构糊掉。
    // 现在改成：
    //   ① 卡面每个 Graphic 换上 CardDimSprite 材质（见该 shader），逐帧插值 _DimAmount：
    //      先去饱和（只降彩度、明度阶梯不动）再乘亮度（黑仍是黑）最后叠一点冷调环境色；
    //   ② TMP 文字换不得材质（SDF），改用乘法压顶点色 —— 参数直接读材质的 _Dim* 属性，
    //      保证文字与卡面是同一套灰度，不会"卡面灰了、文字还亮着"。
    const string DimMaterialPath = "Cards/Materials/CardDim";
    [Tooltip("压暗材质（留空 → 从 Resources/" + DimMaterialPath + " 加载）")]
    public Material dimMaterialOverride;
    [Tooltip("压暗淡入淡出速度（1/秒，指数逼近；缩放/位移仍是 0.2s 缓动）")]
    public float dimFadeSpeed = 20f;

    static Material _dimSharedTemplate;      // 模板材质（Resources 里那张，只读不写）
    static bool _dimMaterialMissingWarned;
    Material _dimMat;                        // 本卡实例：淡入淡出期间只改 _DimAmount
    bool _dimOn;                             // 目标态（HandManager 驱动）
    float _dimAmount;                        // 当前视觉强度 0-1（含 dimStrength 缩放后的实际值）
    float _dimTargetAmount;                  // 压暗目标强度（HandManager 的 dimStrength，非 1 时整体更浅）
    bool _dimRescanNeeded = true;            // 卡面 Graphic 集合需要重扫（图标排/卡图是运行时生成的）
    readonly List<Graphic> _dimBufferAll = new List<Graphic>();
    readonly List<Graphic> _dimGraphics = new List<Graphic>();
    readonly List<TMP_Text> _dimTexts = new List<TMP_Text>();
    readonly Dictionary<TMP_Text, Color> _textBaseColor = new Dictionary<TMP_Text, Color>();
    readonly Dictionary<TMP_Text, Color> _textAppliedColor = new Dictionary<TMP_Text, Color>();

    static readonly int DimAmountId = Shader.PropertyToID("_DimAmount");
    static readonly int DimSaturationId = Shader.PropertyToID("_DimSaturation");
    static readonly int DimBrightnessId = Shader.PropertyToID("_DimBrightness");
    static readonly int DimTintId = Shader.PropertyToID("_DimTint");
    static readonly int DimLiftId = Shader.PropertyToID("_DimLift");

    // TMP 的 color 走 Color32（1/255 量化），读回来必然有台阶误差 → 判重留容差
    const float ColorEps = 0.005f;
    const float DefaultDimSaturation = 0.12f;
    const float DefaultDimBrightness = 0.84f;
    const float DefaultDimLift = 0.04f;
    static readonly Color DefaultDimTint = new Color(0.78f, 0.82f, 0.92f, 1f);

    /// <summary>抽牌入场动画进行中（飞行中的牌不参与 RefreshLayout 的 snap，也不被 Update 的 lerp 覆盖）。</summary>
    [HideInInspector] public bool IsFlying = false;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        originalScale = transform.localScale;
    }

    void OnDestroy()
    {
        if (_dimMat == null) return;
        // 材质实例是本卡私有的，销毁卡牌时必须一起销毁，否则手牌一进一出就漏一个 Material
        if (Application.isPlaying) Destroy(_dimMat);
        else DestroyImmediate(_dimMat);
        _dimMat = null;
    }

    /// <summary>重新记录当前的 localScale 作为基准（Scale2DCard 调用后）</summary>
    public void RefreshOriginalScale()
    {
        originalScale = transform.localScale;
    }

    // ── 整手压暗目标位/倍率 ──────────────────────────────────────
    Vector3 DesiredPos()
    {
        Vector3 p = new Vector3(targetPos.x, targetPos.y + _dimOffsetY, 0);
        if (_hovered) p.y += HOVER_RAISE; // 悬停上浮加在压暗后的休息位之上
        return p;
    }
    Vector3 DesiredScale() => originalScale * _dimScale * (_hovered ? HOVER_SCALE : 1f);

    /// <summary>整手压暗/还原（HandManager 统一驱动）。dim=true：倍率 scale(<1 缩小)+下移 offsetY+去饱和压暗
    /// （强度 amount 0-1，卡面与文字一起按它缩放）；false：还原到 1.0×/原位/原色。
    /// 悬停期间的压暗卡仍放大，但保持缩小基数与压暗强度。</summary>
    public void SetGroupDim(bool dim, float scale, float offsetY, float amount = 1f)
    {
        _dimScale = dim ? scale : 1f;
        _dimOffsetY = dim ? offsetY : 0f;
        _dimOn = dim;
        _dimTargetAmount = dim ? Mathf.Clamp01(amount) : 0f;
        _dimRescanNeeded = true; // 重扫卡面：让新入卡/新生成的图标排一并纳入压暗

        // 未激活（隐藏/待飞入）与飞行中的牌没有 Update 驱动淡入，直接落到目标强度
        if (!gameObject.activeInHierarchy || IsFlying)
        {
            _dimAmount = _dimTargetAmount;
            ApplyDimVisual(_dimAmount);
        }

        if (!gameObject.activeInHierarchy)
        {
            // 未激活（隐藏/待飞入的牌也在 handCards 列表）：不启协程，直接落缩放；
            // 重新激活后 CardView.Update 会自动向 DesiredPos(含偏移) 对齐位置。
            rectTransform.localScale = DesiredScale();
            handManager?.MarkBoundsDirty();
            return;
        }
        if (IsFlying) return; // 飞行中不抢动画，落位后由 FlyIn 以 originalScale*_dimScale 对齐
        StopAllCoroutines();
        StartCoroutine(SmoothTo(DesiredPos(), _hovered ? Quaternion.identity : targetRotation, DesiredScale(), 0.2f));
        handManager?.MarkBoundsDirty();
    }

    void Update()
    {
        StepDimFade(Time.deltaTime); // 压暗强度逐帧逼近目标（指数逼近，和位移/缩放同一套 lerp 语言，且不受 StopAllCoroutines 影响）
        if (!IsAnyCardDragging && !IsFlying)
        {
            // 悬停时保持"放大+上浮"状态（不向基础位置回拉）；移开后回到 targetPos/targetRotation（均含整手压暗偏移）
            Vector3 desiredPos = DesiredPos();
            Quaternion desiredRot = _hovered ? Quaternion.identity : targetRotation;
            rectTransform.localPosition = Vector3.Lerp(rectTransform.localPosition, desiredPos, Time.deltaTime * 15f);
            rectTransform.localRotation = Quaternion.Slerp(rectTransform.localRotation, desiredRot, Time.deltaTime * 15f);
            handManager?.MarkBoundsDirty();
        }
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (IsAnyCardDragging) return;
        _hovered = true;
        originalSibling = transform.GetSiblingIndex();
        transform.SetAsLastSibling();
        StopAllCoroutines();
        StartCoroutine(SmoothTo(DesiredPos(), Quaternion.identity, DesiredScale(), 0.12f));
        handManager?.RefreshLayout(false); // 触发相邻卡牌让位
        handManager?.MarkBoundsDirty();
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (IsAnyCardDragging) return;
        _hovered = false;
        transform.SetSiblingIndex(originalSibling);
        StopAllCoroutines();
        StartCoroutine(SmoothTo(DesiredPos(), targetRotation, DesiredScale(), 0.15f));
        handManager?.RefreshLayout(false); // 触发相邻卡牌归位
        handManager?.MarkBoundsDirty();
    }

    System.Collections.IEnumerator SmoothTo(Vector3 pos, Quaternion rot, Vector3 scale, float dur)
    {
        Vector3 sp = rectTransform.localPosition;
        Quaternion sr = rectTransform.localRotation;
        Vector3 ss = rectTransform.localScale;
        float t = 0;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            p = p < 0.5f ? 2 * p * p : -1 + (4 - 2 * p) * p;
            rectTransform.localPosition = Vector3.Lerp(sp, pos, p);
            rectTransform.localRotation = Quaternion.Slerp(sr, rot, p);
            rectTransform.localScale = Vector3.Lerp(ss, scale, p);
            handManager?.MarkBoundsDirty();
            yield return null;
        }
        rectTransform.localPosition = pos;
        rectTransform.localRotation = rot;
        rectTransform.localScale = scale;
        handManager?.MarkBoundsDirty();
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (!IsAnyCardDragging && OnCardClicked != null)
        {
            CardInstance ci = GetComponent<CardInstance>();
            OnCardClicked?.Invoke(ci);
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 压暗视觉：去饱和 + 压亮度（对卡面本身处理，不是盖一层灰）
    // ══════════════════════════════════════════════════════════════

    /// <summary>压暗强度逐帧逼近目标值。刻意不用协程：OnPointerEnter/Exit 会 StopAllCoroutines()，
    /// 协程版淡入会在悬停那一刻被打断卡在半灰。</summary>
    void StepDimFade(float dt)
    {
        float target = _dimOn ? _dimTargetAmount : 0f;
        if (_dimAmount == target) return;
        _dimAmount = Mathf.Lerp(_dimAmount, target, Mathf.Clamp01(dt * Mathf.Max(0.01f, dimFadeSpeed)));
        if (Mathf.Abs(_dimAmount - target) < 0.004f) _dimAmount = target;
        ApplyDimVisual(_dimAmount);
    }

    /// <summary>卡面刷新后调用（CardDisplay2DNew/Spell.Refresh 末尾）：换卡图、重建图标排会引入新的
    /// Graphic，需要按当前强度一并铺上，否则新出现的图标会"半卡亮着"。未压暗时不需要做任何事。</summary>
    public void RefreshDimVisuals()
    {
        if (_dimAmount <= 0f) return;
        _dimRescanNeeded = true;
        ApplyDimVisual(_dimAmount);
    }

    /// <summary>把当前压暗强度铺到卡面上。amount=0 → 还原材质，保证"没压暗的卡"与原始状态逐像素一致。</summary>
    void ApplyDimVisual(float amount)
    {
        if (amount <= 0f) { RestoreDimVisuals(); return; }

        if (_dimRescanNeeded) { ScanDimVisuals(); _dimRescanNeeded = false; }

        Material mat = GetDimMaterial();
        if (mat == null) return;

        mat.SetFloat(DimAmountId, amount);
        for (int i = 0; i < _dimGraphics.Count; i++)
        {
            Graphic g = _dimGraphics[i];
            if (g != null) g.material = mat; // 已指向同一材质时 setter 直接 return，不会反复重建 Canvas
        }

        // 文字：参数直接读材质，保证与卡面同一套灰度
        float saturation = mat.GetFloat(DimSaturationId);
        float brightness = mat.GetFloat(DimBrightnessId);
        Color tint = mat.GetColor(DimTintId);
        float lift = mat.GetFloat(DimLiftId);

        for (int i = 0; i < _dimTexts.Count; i++)
        {
            TMP_Text t = _dimTexts[i];
            if (t == null) continue;

            // 基色判定：外部（CardDisplay2DNew.Refresh 写 Get*Color()）刚改过 → 以新值作基色。
            // 不这么做的话，刷新后会把"已经压暗过的值"当成基色再压一次，越压越黑。
            Color applied;
            if (!_textAppliedColor.TryGetValue(t, out applied) || !ApproxSame(t.color, applied))
                _textBaseColor[t] = t.color;

            Color baseColor = _textBaseColor[t];
            t.color = Color.Lerp(baseColor, DimColor(baseColor, saturation, brightness, tint, lift), amount);
            _textAppliedColor[t] = t.color;
        }
    }

    /// <summary>还原卡面：材质交回 Canvas 默认（UI/Default），文字回到基色。</summary>
    void RestoreDimVisuals()
    {
        for (int i = 0; i < _dimGraphics.Count; i++)
        {
            Graphic g = _dimGraphics[i];
            if (g != null) g.material = null;
        }

        for (int i = 0; i < _dimTexts.Count; i++)
        {
            TMP_Text t = _dimTexts[i];
            if (t == null) continue;
            Color baseColor;
            Color applied;
            // 只有"当前值仍是我们写进去的那一份"才还原，免得把外部刚写的新颜色顶掉
            if (_textBaseColor.TryGetValue(t, out baseColor)
                && _textAppliedColor.TryGetValue(t, out applied)
                && ApproxSame(t.color, applied))
                t.color = baseColor;
        }
    }

    /// <summary>重扫卡面 Graphic（卡图/图标排都是运行时生成的，所以不能只在 Awake 扫一次）：
    /// 默认材质的 Graphic → 纳入压暗材质；TMP 文字 → 单独记，走顶点色。</summary>
    void ScanDimVisuals()
    {
        _dimBufferAll.Clear();
        _dimGraphics.Clear();
        _dimTexts.Clear();
        GetComponentsInChildren(true, _dimBufferAll);

        for (int i = 0; i < _dimBufferAll.Count; i++)
        {
            Graphic g = _dimBufferAll[i];
            if (g == null) continue;

            // TMP 文字是 SDF 着色器，换材质会直接糊掉 → 先滤出来走 color 乘法（TMP_Text 本身也是 Graphic）
            TMP_Text t = g as TMP_Text;
            if (t != null) { _dimTexts.Add(t); continue; }

            // 自带自定义材质的 Graphic（特效/描边一类）不碰，免得把它的着色器换成压暗材质
            Material cur = g.material;
            if (cur != null && cur.shader != null && cur.shader.name != "UI/Default") continue;

            _dimGraphics.Add(g);
        }
    }

    /// <summary>取本卡的压暗材质实例（懒创建，模板来自 Resources 或 Inspector 指定）。</summary>
    Material GetDimMaterial()
    {
        if (_dimMat != null) return _dimMat;

        Material template = dimMaterialOverride;
        if (template == null)
        {
            if (_dimSharedTemplate == null) _dimSharedTemplate = Resources.Load<Material>(DimMaterialPath);
            template = _dimSharedTemplate;
        }
        if (template == null)
        {
            // 兜底：材质资产缺失时直接按着色器名找（同名 shader 在 Resources 里，构建不会剥离）
            Shader shader = Shader.Find("AnotherWorld/CardDimSprite");
            if (shader == null)
            {
                if (!_dimMaterialMissingWarned)
                {
                    _dimMaterialMissingWarned = true;
                    Debug.LogWarning($"[CardView] 找不到压暗材质 Resources/{DimMaterialPath}，也找不到 AnotherWorld/CardDimSprite —— 非己方回合手牌将不做压暗。");
                }
                return null;
            }
            template = CreateDimFallbackMaterial(shader);
        }

        // 每卡一份实例：淡入淡出只改自己的 _DimAmount，互不干扰
        _dimMat = new Material(template);
        _dimMat.name = template.name + " (CardView 实例)";
        return _dimMat;
    }

    static Material CreateDimFallbackMaterial(Shader shader)
    {
        Material m = new Material(shader);
        m.SetFloat(DimSaturationId, DefaultDimSaturation);
        m.SetFloat(DimBrightnessId, DefaultDimBrightness);
        m.SetColor(DimTintId, DefaultDimTint);
        m.SetFloat(DimLiftId, DefaultDimLift);
        _dimSharedTemplate = m;
        return m;
    }

    /// <summary>与 CardDimSprite 同一套算式（去饱和 → 压亮度 → 叠环境色），供 TMP 文字使用。
    /// 项目是 Linear 色彩空间而 UI 顶点色会转线性，故先把颜色转线性再算，保证文字与卡面灰度一致。
    /// 注意 _DimTint 是材质属性，不经 gamma 转换，所以这里同样用原值，不做 .linear。</summary>
    static Color DimColor(Color c, float saturation, float brightness, Color tint, float lift)
    {
        bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
        Color src = linear ? c.linear : c;
        float lum = 0.299f * src.r + 0.587f * src.g + 0.114f * src.b;
        float r = Mathf.Min(Mathf.Lerp(lum, src.r, saturation) * brightness + tint.r * lift, 1f);
        float g = Mathf.Min(Mathf.Lerp(lum, src.g, saturation) * brightness + tint.g * lift, 1f);
        float b = Mathf.Min(Mathf.Lerp(lum, src.b, saturation) * brightness + tint.b * lift, 1f);
        Color dimmed = new Color(r, g, b, c.a);
        return linear ? dimmed.gamma : dimmed;
    }

    static bool ApproxSame(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) <= ColorEps
            && Mathf.Abs(a.g - b.g) <= ColorEps
            && Mathf.Abs(a.b - b.b) <= ColorEps
            && Mathf.Abs(a.a - b.a) <= ColorEps;
    }

    /// <summary>返回卡牌在屏幕空间的包围矩形（含缩放/位移/旋转）。</summary>
    public Rect GetWorldRect()
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        // corners: [0]=左下, [1]=左上, [2]=右上, [3]=右下
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }

    /// <summary>设置卡牌透明度。</summary>
    public void SetAlpha(float a)
    {
        canvasGroup.alpha = a;
    }

    /// <summary>从屏幕右侧外水平飞入到目标位置的入场动画（全程可见，弹性落位）。</summary>
    /// <param name="startWorldPos">起点世界坐标（屏幕右边界外；Y/Z 会被初始目标覆盖以保持严格水平）</param>
    /// <param name="targetWorldPos">初始目标世界坐标（RefreshLayout 计算出的 targetPos 转世界，飞行中会每帧重新读取最新值）</param>
    /// <param name="targetRotation">初始目标旋转（飞行中会每帧重新读取最新值）</param>
    /// <param name="duration">动画时长</param>
    /// <param name="cfg">动画配置（弹性/旋转/缩放参数）</param>
    /// <param name="layoutTrigger">延迟让位触发点（0~1），飞行进度到该比例时回调 onLayoutTrigger</param>
    /// <param name="onLayoutTrigger">延迟让位回调（触发现有手牌滑动让位）</param>
    public System.Collections.IEnumerator FlyInFromDeck(Vector3 startWorldPos, Vector3 targetWorldPos, Quaternion targetRotation,
                                                          float duration, AnimationConfig cfg,
                                                          float layoutTrigger, System.Action onLayoutTrigger)
    {
        float overshoot = cfg != null ? cfg.flyEaseOvershoot : 1.2f;
        float zRot = cfg != null ? cfg.flyZRotation : 8f;
        float scaleMin = cfg != null ? cfg.flyScaleMin : 0.95f;

        Vector3 targetScale = originalScale * _dimScale; // Scale2DCard 已 ×3；含起飞时压暗基数
        Vector3 FlyScale() => originalScale * _dimScale; // 实时倍率：压暗若在飞行中翻转，落位用最新值，不错拍成"小卡卡住"
        Quaternion startRotation = Quaternion.Euler(0f, 0f, -zRot);

        IsFlying = true;

        // 全程可见：显式激活 + alpha=1，飞行中不再有任何隐藏操作
        gameObject.SetActive(true);
        canvasGroup.alpha = 1f;

        // 起点：X 取屏幕外，Y/Z 锁定为初始目标值 → 纯水平飞入，无上下跳动、无深度位移
        Vector3 startPos = new Vector3(startWorldPos.x, targetWorldPos.y, targetWorldPos.z);

        transform.position = startPos;
        transform.rotation = startRotation;
        transform.localScale = targetScale * scaleMin;

        float elapsed = 0f;
        bool layoutTriggered = false;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutBack(t, overshoot); // 弹性缓动：到位后轻微过冲再回正

            // 飞到 trigger 进度时触发一次延迟让位
            if (!layoutTriggered && t >= layoutTrigger)
            {
                layoutTriggered = true;
                onLayoutTrigger?.Invoke();
            }

            // 每帧重新读取最新 targetPos/targetRotation（RefreshLayout 可能已因新牌加入而更新目标），
            // 从当前位置向最新目标插值 → 目标变化时无缝转向，不锁定终点、无停顿。
            Vector3 currentTargetWorld = transform.parent.TransformPoint(targetPos);

            transform.position = Vector3.Lerp(transform.position, currentTargetWorld, eased);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, eased);
            transform.localScale = Vector3.Lerp(transform.localScale, FlyScale(), eased);

            yield return null;
        }

        // 精确落位到最终 targetPos（飞行结束，交回 Update 的 lerp 也指向同一目标）
        Vector3 finalTargetWorld = transform.parent.TransformPoint(targetPos);
        transform.position = finalTargetWorld;
        transform.rotation = targetRotation;
        transform.localScale = FlyScale();
        canvasGroup.alpha = 1f;
        IsFlying = false;
        handManager?.MarkBoundsDirty();
    }

    /// <summary>ease-out back 缓动：到位后轻微过冲再回正。c1 为过冲系数（1.7≈标准，越小越轻）。</summary>
    static float EaseOutBack(float t, float c1)
    {
        float c3 = c1 + 1f;
        float t1 = t - 1f;
        return 1f + c3 * t1 * t1 * t1 + c1 * t1 * t1;
    }
}
