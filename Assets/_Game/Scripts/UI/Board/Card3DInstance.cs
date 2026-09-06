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
        CardDisplay3D display = GetComponent<CardDisplay3D>();
        if (display != null) display.Refresh();
        // 新 3D 卡图标（费用/类型/攻/血 + 三排）随同一触发点刷新；旧卡无此组件则跳过
        CardIcons3D icons = GetComponent<CardIcons3D>();
        if (icons != null) icons.Refresh();
    }

    /// <summary>召唤动画（缩放曲线：0 → 正常×1.25 → ×1.2 → ×1.0；前半 0.6s、后半 0.6s）。
    /// 只影响表现，不改任何逻辑判断；缩放作用在可见正面容器（UIComponents），文字/图标随卡面整体缩放。
    /// 用协程驱动，不阻塞其它逻辑。</summary>
    public void PlaySummonAnimation()
    {
        if (_summonAnimating) return;
        _summonRoutine = StartCoroutine(SummonAnimationRoutine());
    }

    /// <summary>立即完成召唤动画（攻击动画开始前调用）：停止缩放协程、复位正常缩放、恢复浮动。
    /// 防召唤动画未结束时攻击动画与其交互（缩放/浮动状态冲突）导致位置 bug。</summary>
    public void CompleteSummonAnimation()
    {
        if (!_summonAnimating) return;
        if (_summonRoutine != null) StopCoroutine(_summonRoutine);
        _summonRoutine = null;
        Transform target = GetScaleTarget();
        if (target != null) target.localScale = _summonBaseScale; // 复位到正常缩放
        Card3DAnimator fa = GetComponent<Card3DAnimator>();
        if (fa != null) fa.enabled = true; // 恢复浮动（攻击动画随后自行暂停）
        _summonAnimating = false;
    }

    Coroutine _summonRoutine;
    bool _summonAnimating;
    bool _bounceArmed; // 首刷后置 true；供 ElementBounceAllowed（抑制进场/召唤期弹跳）
    Vector3 _summonBaseScale = Vector3.one;

    /// <summary>生成入口统一调用：实例化后立即触发召唤动画（同步把可见正面容器缩到 0，避免先以完整尺寸闪现一帧）。</summary>
    public static void PlaySummonOn(GameObject model)
    {
        if (model != null)
            model.GetComponent<Card3DInstance>()?.PlaySummonAnimation();
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

    IEnumerator SummonAnimationRoutine()
    {
        Transform target = GetScaleTarget(); // 可见正面容器（UIComponents）；ModelRoot 是卡背，正面被 ShowFront 禁用不可见
        if (target == null) yield break;

        _summonAnimating = true;
        _summonRoutine = null; // 本协程运行中（引用由 PlaySummonAnimation 持有）
        // 动画期间暂停漂浮/呼吸（与攻击动画一致）：旧卡缩放整卡根时避免呼吸缩放覆盖动画
        Card3DAnimator floatAnim = GetComponent<Card3DAnimator>();
        if (floatAnim != null) floatAnim.enabled = false;

        Vector3 baseScale = target.localScale; // 正常缩放（新卡 UIComponents=1）
        _summonBaseScale = baseScale;          // 记录正常缩放（供攻击前强制完成时复位）
        target.localScale = Vector3.zero;      // 立即置 0，避免生成后以完整尺寸闪现

        // 阶段1（共 0.6s）：0 → 正常×1.25 → ×1.2
        const float growDur = 0.4f / 1.5f; // 0→1.25 放大阶段速度×1.5（0.4→≈0.267s）
        const float bounceDur = 0.2f;  // 1.25→1.2 回弹
        float t = 0f;
        while (t < growDur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / growDur);
            float eased = 1f - Mathf.Pow(1f - k, 3f);
            target.localScale = baseScale * (1.25f * eased);
            yield return null;
        }
        t = 0f;
        while (t < bounceDur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / bounceDur);
            target.localScale = baseScale * Mathf.Lerp(1.25f, 1.2f, k);
            yield return null;
        }

        // 阶段2（0.6s）：×1.2 → ×1.0 慢慢缩小到正常
        t = 0f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / 0.6f);
            float eased = 1f - Mathf.Pow(1f - k, 3f); // ease-out 慢收
            target.localScale = baseScale * Mathf.Lerp(1.2f, 1.0f, eased);
            yield return null;
        }
        target.localScale = baseScale; // 精确复位到正常

        if (floatAnim != null) floatAnim.enabled = true;
        _summonAnimating = false;
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
