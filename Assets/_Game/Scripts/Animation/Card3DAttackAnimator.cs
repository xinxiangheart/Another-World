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
        float pitch = _cfg != null ? _cfg.pitchAngle : 4f;
        float shakeStr = _cfg != null ? _cfg.targetShakeStrength : 0.08f;
        float shakeDur = _cfg != null ? _cfg.targetShakeDuration : 0.15f;

        // 目标位置：打随从飞向目标；打英雄（target==null）原地，只做小幅动作
        Vector3 targetPos = target != null ? target.transform.position : _originalPos;

        // ── 阶段1：蓄力（后拉+下沉 + 俯仰翘起）──
        Vector3 windupPos = _originalPos + Vector3.down * pullback;
        Quaternion windupRot = _originalRot * Quaternion.Euler(-pitch, 0, 0);
        yield return AnimateTo(windupPos, windupRot, windup);

        // ── 阶段2：冲刺（二次贝塞尔弧线 + 俯仰下压）──
        Vector3 midPoint = Vector3.Lerp(_originalPos, targetPos, 0.5f) + Vector3.up * arc;
        Quaternion lungeRot = _originalRot * Quaternion.Euler(pitch, 0, 0);
        yield return AnimateBezier(_originalPos, midPoint, targetPos, lunge, lungeRot);

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

    /// <summary>二次贝塞尔弧线飞行（a→b 控制点→c），线性旋转插值。</summary>
    IEnumerator AnimateBezier(Vector3 a, Vector3 b, Vector3 c, float duration, Quaternion rot)
    {
        if (duration <= 0f) { transform.position = c; transform.rotation = rot; yield break; }

        Quaternion startRot = transform.rotation;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

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
