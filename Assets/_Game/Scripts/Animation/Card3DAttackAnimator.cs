using System;
using System.Collections;
using UnityEngine;

// ============================================================================
// Card3DAttackAnimator — 3D 卡牌攻击动画组件（挂到每个 3D 卡牌模型上）
// ============================================================================
//
// 五阶段攻击动画（供 BattleAnimator 实现重叠窗口）：
//   ApproachAndHit(target, onHit)  = 蓄力 → 冲刺 → 击中（触发 onHit + 目标震动）
//   ReturnToOriginal()             = 停留 → 弹性返回 → 恢复漂浮
//
// 重叠窗口：ApproachAndHit 在"击中"后立即返回，下一个攻击随即启动；
// ReturnToOriginal 由 BattleAnimator 后台驱动（停留 + 返回不阻塞主流程）。
//
// 全部参数从 AnimationConfig 读取（攻击期间暂停 Card3DAnimator 漂浮/呼吸）。
// ============================================================================

public class Card3DAttackAnimator : MonoBehaviour
{
    private Vector3 _originalPos;
    private Quaternion _originalRot;
    private Card3DAnimator _floatAnimator;
    private AnimationConfig _cfg;

    void Awake()
    {
        _floatAnimator = GetComponent<Card3DAnimator>();
        _cfg = AnimationConfig.Load();
    }

    /// <summary>蓄力翘起 → 弧线冲刺下压 → 击中（阻塞到 onHit 触发）。</summary>
    public IEnumerator ApproachAndHit(GameObject target, Action onHit)
    {
        _originalPos = transform.position;
        _originalRot = transform.rotation;

        // 暂停漂浮动画，避免 position/localPosition 冲突
        if (_floatAnimator != null) _floatAnimator.enabled = false;

        float windup = _cfg != null ? _cfg.windupDuration : 0.15f;
        float lunge = _cfg != null ? _cfg.lungeDuration : 0.20f;
        float arc = _cfg != null ? _cfg.arcHeight : 0.8f;
        float pullback = _cfg != null ? _cfg.windupPullback : 0.15f;
        float pitch = _cfg != null ? _cfg.pitchAngle : 12f;
        float shakeStr = _cfg != null ? _cfg.targetShakeStrength : 0.08f;
        float shakeDur = _cfg != null ? _cfg.targetShakeDuration : 0.15f;
        float heightOffset = _cfg != null ? _cfg.attackTargetHeightOffset : 0.8f;

        // 判断攻击者半场：己方卡牌在屏幕下方（y<0，向上攻击），对方在上方（y>0，向下攻击）
        bool isAlly = _originalPos.y < 0f;

        // 卡牌半高（落点偏移量，让攻击卡牌停在目标边缘、不与目标中心重叠）
        float cardHalf = 0.8f;
        var mr = GetComponentInChildren<MeshRenderer>(); // 网格在 ModelRoot 子层级
        if (mr != null && mr.bounds.size.y > 0.01f)
            cardHalf = mr.bounds.size.y * 0.5f;

        Vector3 targetPos = target != null ? target.transform.position : _originalPos;

        // 落点：己方攻击落在目标下端，对方攻击落在目标上端（不再与目标重叠）
        Vector3 impactPos;
        if (target != null)
        {
            impactPos = isAlly ? targetPos + Vector3.down * cardHalf   // 己方→目标下端
                              : targetPos + Vector3.up * cardHalf;   // 对方→目标上端
        }
        else
        {
            // 空打（打英雄，无目标模型）：终点高度偏移用可配置参数，与有目标时的偏移量一致，
            // 沿攻击方向（己方向上/对方向下）位移，防止俯仰下压时原地穿模。
            impactPos = isAlly ? _originalPos + Vector3.up * heightOffset
                              : _originalPos + Vector3.down * heightOffset;
        }

        // ── 阶段1：蓄力（后拉+下沉，不再翘起）──
        Vector3 windupPos = _originalPos + Vector3.down * pullback;
        yield return AnimateTo(windupPos, _originalRot, windup);

        // ── 阶段2：冲刺（二次贝塞尔弧线 + 向目标方向倾斜）──
        Vector3 midPoint = Vector3.Lerp(_originalPos, impactPos, 0.5f) + Vector3.up * arc;
        // 己方（向上冲）：上半部分（顶部）朝目标翘起；对方（向下冲）：下半部分（底部）朝目标下压
        float pitchDir = isAlly ? -pitch : pitch;
        Quaternion lungeRot = _originalRot * Quaternion.Euler(pitchDir, 0, 0);
        // 冲刺后半段速度×1.5（前半段保持原速）→ 总体冲刺时间缩短到 5/6
        yield return AnimateBezier(_originalPos, midPoint, impactPos, lunge, lungeRot, 1.5f);

        // ── 阶段3：击中（单帧：音效 + 飘字 + 伤害应用 + 目标震动）──
        onHit?.Invoke();
        if (target != null)
            StartCoroutine(Shake(target.transform, shakeDur, shakeStr));
    }

    /// <summary>停留 → 弹性返回 → 恢复漂浮（后台执行）。</summary>
    public IEnumerator ReturnToOriginal()
    {
        float stay = _cfg != null ? _cfg.impactStay : 0.15f;
        float ret = _cfg != null ? _cfg.returnDuration : 0.30f;
        float overshoot = _cfg != null ? _cfg.returnOvershoot : 1.05f;

        // ── 阶段4：停留 ──
        yield return new WaitForSeconds(stay);

        // ── 阶段5：弹性返回（ease-out back 过冲）──
        yield return AnimateBack(_originalPos, _originalRot, ret, overshoot);

        transform.position = _originalPos;
        transform.rotation = _originalRot;

        // 恢复漂浮
        if (_floatAnimator != null) _floatAnimator.enabled = true;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 辅助方法
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>直线插值到目标位置 + 旋转（ease-out quad）。</summary>
    IEnumerator AnimateTo(Vector3 pos, Quaternion rot, float duration)
    {
        if (duration <= 0f) { transform.position = pos; transform.rotation = rot; yield break; }

        Vector3 start = transform.position;
        Quaternion startRot = transform.rotation;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - (1f - t) * (1f - t); // ease-out quad

            transform.position = Vector3.Lerp(start, pos, eased);
            transform.rotation = Quaternion.Slerp(startRot, rot, eased);
            yield return null;
        }
        transform.position = pos;
        transform.rotation = rot;
    }

    /// <summary>二次贝塞尔弧线飞行（a→b 控制点→c），线性旋转插值。
    /// secondHalfSpeed>1 时冲刺分前后两半：前半段原速、后半段速度×secondHalfSpeed，
    /// 总时长 = dur/2 + dur/(2×secondHalfSpeed)（×1.5 时= 5/6 dur）。</summary>
    IEnumerator AnimateBezier(Vector3 a, Vector3 b, Vector3 c, float duration, Quaternion rot, float secondHalfSpeed = 1f)
    {
        if (duration <= 0f) { transform.position = c; transform.rotation = rot; yield break; }

        Quaternion startRot = transform.rotation;
        float halfTime = duration * 0.5f; // 前半段原速走 dur/2
        float total = secondHalfSpeed > 0f ? halfTime + halfTime / secondHalfSpeed : duration;
        float elapsed = 0f;
        while (elapsed < total)
        {
            elapsed += Time.deltaTime;
            // 曲线参数 t：前半段 0→0.5 走原速；后半段 0.5→1.0 加速
            float t = elapsed <= halfTime
                ? (elapsed / halfTime) * 0.5f
                : 0.5f + ((elapsed - halfTime) / (halfTime / secondHalfSpeed)) * 0.5f;
            t = Mathf.Clamp01(t);

            // 二次贝塞尔：B(t) = (1-t)²a + 2(1-t)t·b + t²c
            float u = 1f - t;
            Vector3 p = u * u * a + 2f * u * t * b + t * t * c;

            transform.position = p;
            transform.rotation = Quaternion.Slerp(startRot, rot, t);
            yield return null;
        }
        transform.position = c;
        transform.rotation = rot;
    }

    /// <summary>弹性返回（ease-out back 过冲，LerpUnclamped 允许超过目标）。</summary>
    IEnumerator AnimateBack(Vector3 pos, Quaternion rot, float duration, float overshoot)
    {
        if (duration <= 0f) { transform.position = pos; transform.rotation = rot; yield break; }

        Vector3 start = transform.position;
        Quaternion startRot = transform.rotation;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutBack(t, overshoot);

            transform.position = Vector3.LerpUnclamped(start, pos, eased);
            transform.rotation = Quaternion.Slerp(startRot, rot, Mathf.Clamp01(eased));
            yield return null;
        }
        transform.position = pos;
        transform.rotation = rot;
    }

    /// <summary>目标受击震动（衰减抖动，结束恢复原位）。</summary>
    IEnumerator Shake(Transform t, float duration, float strength)
    {
        if (t == null) yield break;
        Vector3 basePos = t.position;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float d = strength * (1f - elapsed / duration); // 衰减
            t.position = basePos + new Vector3(
                UnityEngine.Random.Range(-1f, 1f) * d,
                UnityEngine.Random.Range(-1f, 1f) * d,
                0f);
            yield return null;
        }
        t.position = basePos;
    }

    /// <summary>ease-out back 缓动：到位后轻微过冲再回正。c1 为过冲系数。</summary>
    static float EaseOutBack(float t, float c1)
    {
        float c3 = c1 + 1f;
        float t1 = t - 1f;
        return 1f + c3 * t1 * t1 * t1 + c1 * t1 * t1;
    }
}
