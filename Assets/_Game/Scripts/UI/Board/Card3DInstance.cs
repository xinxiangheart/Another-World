using System.Collections;
using UnityEngine;

public class Card3DInstance : MonoBehaviour
{
    public CardInstance cardInstance;

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
        if (_summonAnimating) return;
        _summonAnimating = true;

        // 同步抢占根 Transform：不能等协程第一帧再关，否则 Card3DAnimator / 攻击动画
        // 可能在同帧写入 localPosition/localScale，把飞行起点或落点覆盖掉。
        Card3DAnimator floatAnim = GetComponent<Card3DAnimator>();
        if (floatAnim != null) floatAnim.SuspendForExternalAnimation();
        Card3DHover hover = GetComponent<Card3DHover>();
        if (hover != null) hover.enabled = false;

        _summonRoutine = StartCoroutine(SummonAnimationRoutine(fromWorld));
    }

    public bool IsSummonAnimating => _summonAnimating;

    /// <summary>攻击/进场等后续演出等待召唤结束。超时后仍强制收尾，防协程异常卡死。</summary>
    public IEnumerator WaitForSummonAnimation(float timeout = 2f)
    {
        float deadline = Time.unscaledTime + Mathf.Max(0.01f, timeout);
        while (_summonAnimating && Time.unscaledTime < deadline)
            yield return null;
        if (_summonAnimating) CompleteSummonAnimation();
    }

    /// <summary>立即完成召唤动画（攻击动画开始前调用）：停止协程、复位位置/旋转/缩放、恢复浮动。
    /// 防召唤动画未结束时攻击动画与其交互（缩放/浮动状态冲突）导致位置 bug。</summary>
    public void CompleteSummonAnimation()
    {
        if (!_summonAnimating) return;
        if (_summonRoutine != null) StopCoroutine(_summonRoutine);
        _summonRoutine = null;

        Transform target = GetScaleTarget();
        if (target != null) target.localScale = _summonBaseScale;
        transform.position = _summonBasePos;
        transform.rotation = _summonBaseRot;

        Card3DHover hover = GetComponent<Card3DHover>();
        if (hover != null) hover.enabled = true;

        Card3DAnimator floatAnim = GetComponent<Card3DAnimator>();
        if (floatAnim != null)
        {
            floatAnim.ResumeAtCurrentTransform();
        }
        _summonAnimating = false;
        _summonWatchdog = 0f;
    }

    Coroutine _summonRoutine;
    bool _summonAnimating;
    bool _bounceArmed; // 首刷后置 true；供 ElementBounceAllowed（抑制进场/召唤期弹跳）
    Vector3 _summonBaseScale = Vector3.one;
    Vector3 _summonBasePos;
    Quaternion _summonBaseRot = Quaternion.identity;
    float _summonWatchdog;

    void Update()
    {
        // 兜底：召唤协程若被暂停/打断（timeScale=0、对象临时失活等），
        // 超时后直接把卡复位到槽位，绝不让它停在半空/屏幕外。
        if (_summonAnimating && _summonWatchdog > 0f && Time.unscaledTime >= _summonWatchdog)
        {
            Debug.LogWarning($"[Summon] watchdog fired {name} pos={transform.position} -> {_summonBasePos}");
            CompleteSummonAnimation();
        }
    }

    /// <summary>生成入口统一调用：实例化后立即触发召唤动画（同步把可见正面容器缩到 0.86，
    /// 避免先以完整尺寸闪现一帧）。fromWorld 可传手牌位置；不传则按槽位推算。</summary>
    public static void PlaySummonOn(GameObject model, Vector3? fromWorld = null)
    {
        if (model != null)
            model.GetComponent<Card3DInstance>()?.PlaySummonAnimation(fromWorld);
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

    IEnumerator SummonAnimationRoutine(Vector3? fromOverride)
    {
        Transform target = GetScaleTarget(); // 可见正面容器（UIComponents）；ModelRoot 是卡背
        if (target == null) yield break;

        _summonRoutine = null;
        _summonWatchdog = Time.unscaledTime + 1.5f;

        // 动画期间暂停漂浮 / 悬停：漂浮每帧改 position，会与飞行插值打架
        Card3DAnimator floatAnim = GetComponent<Card3DAnimator>();
        if (floatAnim != null) floatAnim.SuspendForExternalAnimation();
        Card3DHover hover = GetComponent<Card3DHover>();
        if (hover != null) hover.enabled = false;

        Vector3 to = transform.position;
        Quaternion baseRot = transform.rotation;
        _summonBasePos = to;
        _summonBaseRot = baseRot;

        Vector3 baseScale = target.localScale;
        _summonBaseScale = baseScale;

        AnimationConfig cfg = AnimationConfig.Load();
        float launchDur = cfg != null ? cfg.summonLaunchDuration : 0.22f;
        float apexHeight = cfg != null ? cfg.summonApexHeight : 0.9f;
        float hangDur = cfg != null ? cfg.summonHangDuration : 0.08f;
        float dropDur = cfg != null ? cfg.summonDropDuration : 0.08f;
        float settleDur = cfg != null ? cfg.summonSettleDuration : 0.18f;
        float squash = cfg != null ? cfg.summonSquash : 0.14f;
        float shakeAmp = cfg != null ? cfg.summonShakeStrength : 0.10f;
        float shakeDur = cfg != null ? cfg.summonShakeDuration : 0.12f;

        Vector3 toCam = Camera.main != null ? -Camera.main.transform.forward : Vector3.back;
        // 己方在下、敌方在上（棋盘中心 y≈1）；敌方整套轨迹按此镜像
        float outward = to.y < 1f ? -1f : 1f;

        Vector3 from = fromOverride ?? ComputeSummonOrigin(to);
        // 正向：从所属玩家的一侧朝槽位直飞，横向与目标对齐（手牌只借它的高度/深度，不借横向偏位）
        from.x = to.x;
        // 0) 起手：略小、后仰，保持水平（正向飞入）
        transform.position = from;
        transform.rotation = baseRot * Quaternion.Euler(-14f, 0f, 0f);
        target.localScale = baseScale * 0.86f;

        // 1) 蓄力 60ms：再往后拉一点，压到 0.84
        Vector3 pullDir = from - to;
        pullDir.z = 0f;
        if (pullDir.sqrMagnitude < 0.0001f) pullDir = Vector3.up * outward;
        Vector3 antPos = from + pullDir.normalized * 0.10f;

        float t = 0f;
        const float anticipation = 0.06f;
        while (t < anticipation)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / anticipation);
            float e = 1f - Mathf.Pow(1f - k, 2f);
            transform.position = Vector3.Lerp(from, antPos, e);
            target.localScale = baseScale * Mathf.Lerp(0.86f, 0.84f, e);
            yield return null;
        }

        // 2) 飞出：二次贝塞尔到槽位上方；起步快、接近顶点慢
        Vector3 apex = to + Vector3.up * apexHeight + toCam * 0.35f;
        Vector3 ctrl = Vector3.Lerp(antPos, apex, 0.5f) + toCam * 0.25f;

        float distance = Vector3.Distance(antPos, apex);
        float flyDur = Mathf.Clamp(launchDur + distance * 0.01f, launchDur, launchDur + 0.08f);

        t = 0f;
        while (t < flyDur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / flyDur);
            float e = 1f - Mathf.Pow(1f - k, 3f); // ease-out cubic

            transform.position = Bezier2(antPos, ctrl, apex, e);
            transform.rotation = baseRot * Quaternion.Euler(Mathf.Lerp(-14f, 0f, e), 0f, 0f);
            target.localScale = baseScale * Mathf.Lerp(0.84f, 1.06f, e);
            yield return null;
        }

        // 3) 顶点悬停：给下落留出高度差
        t = 0f;
        while (t < hangDur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / hangDur);
            transform.position = apex + Vector3.up * (0.06f * Mathf.Sin(k * Mathf.PI));
            transform.rotation = baseRot;
            yield return null;
        }

        // 4) 砸下：ease-in，俯仰向前压
        t = 0f;
        while (t < dropDur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dropDur);
            float e = k * k;
            transform.position = Vector3.Lerp(apex, to, e);
            transform.rotation = baseRot * Quaternion.Euler(Mathf.Lerp(0f, 8f, k), 0f, 0f);
            target.localScale = baseScale * Mathf.Lerp(1.06f, 1f, k);
            yield return null;
        }

        // 5) 落地帧：精确到位 + 压扁 + 冲击环/闪光/相机震动（同一帧）
        transform.position = to;
        transform.rotation = baseRot;
        target.localScale = Vector3.Scale(baseScale, new Vector3(1.08f, 1f - squash, 1.08f));

        Vector3 fxPos = to + Vector3.down * 0.85f;
        CardSummonFX.PlayImpact(fxPos, 1f);
        CardSummonFX.PlaySlotFlash(FindSummonSlot(gameObject, to));
        CameraShake.Impulse(shakeAmp, shakeDur);
        // 局部停顿 35ms（不碰全局 Time.timeScale，避免影响联机/UI）
        float stopUntil = Time.time + 0.035f;
        while (Time.time < stopUntil) yield return null;

        // 6) 回弹：阻尼余弦，Y 先压后弹、XZ 先胀后收
        t = 0f;
        while (t < settleDur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / settleDur);
            float decay = Mathf.Exp(-7f * k);
            float osc = Mathf.Cos(k * Mathf.PI * 3f);

            float xz = 1f + 0.08f * decay * osc;
            float y = 1f - squash * decay * osc;
            target.localScale = Vector3.Scale(baseScale, new Vector3(xz, y, xz));
            yield return null;
        }

        // 结束：复位并交回漂浮 / 悬停
        target.localScale = baseScale;
        transform.position = to;
        transform.rotation = baseRot;

        if (hover != null) hover.enabled = true;
        if (floatAnim != null)
        {
            floatAnim.ResumeAtCurrentTransform();
        }
        _summonAnimating = false;
        _summonWatchdog = 0f;
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
