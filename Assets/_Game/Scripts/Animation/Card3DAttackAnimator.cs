using System;
using System.Collections;
using UnityEngine;

// ============================================================================
// Card3DAttackAnimator — 3D 卡牌攻击动画组件（挂到每个 3D 卡牌模型上）
// ============================================================================
//
// 攻击动画分两段（供 BattleAnimator 实现重叠窗口）：
//   ApproachAndHit(target, onHit)：飞向目标 → 击中（触发 onHit）
//   ReturnToOriginal()          ：返回原位 → 恢复漂浮
//
// 动画参数从 AnimationConfig 读取。攻击期间暂停 Card3DAnimator（漂浮/呼吸），
// 避免漂浮动画与攻击位移动画互相覆盖。
// ============================================================================

public class Card3DAttackAnimator : MonoBehaviour
{
    [Header("攻击动画参数（缺省从 AnimationConfig 读）")]
    public float lungeDuration = 0.12f;
    public float returnDuration = 0.15f;
    public float arcHeight = 0.5f;

    private Vector3 _originalPos;
    private Quaternion _originalRot;
    private Card3DAnimator _floatAnimator;

    void Awake()
    {
        _floatAnimator = GetComponent<Card3DAnimator>();
    }

    /// <summary>飞向目标 + 击中（阻塞到 onHit 触发）。</summary>
    public IEnumerator ApproachAndHit(GameObject target, Action onHit)
    {
        _originalPos = transform.position;
        _originalRot = transform.rotation;

        // 暂停漂浮动画，避免 position/localPosition 冲突
        if (_floatAnimator != null) _floatAnimator.enabled = false;

        // 目标位置：打随从飞向目标；打英雄（target==null）原地，只做小幅后坐
        Vector3 targetPos = target != null ? target.transform.position : _originalPos;

        yield return FlyTo(targetPos, lungeDuration, arcHeight);

        // 击中时刻：触发伤害数字 + 音效 + 扣血
        onHit?.Invoke();
    }

    /// <summary>返回原位（后台执行）。</summary>
    public IEnumerator ReturnToOriginal()
    {
        yield return FlyTo(_originalPos, returnDuration, 0f);

        transform.position = _originalPos;
        transform.rotation = _originalRot;

        // 恢复漂浮
        if (_floatAnimator != null) _floatAnimator.enabled = true;
    }

    /// <summary>从当前位置飞向目标位置（带可选弧线）。</summary>
    IEnumerator FlyTo(Vector3 target, float duration, float arc)
    {
        if (duration <= 0f) { transform.position = target; yield break; }

        Vector3 start = transform.position;
        Quaternion startRot = transform.rotation;
        // 面向目标方向
        Quaternion targetRot = target != start ? Quaternion.LookRotation(target - start) : startRot;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - (1f - t) * (1f - t); // ease-out quad

            Vector3 pos = Vector3.Lerp(start, target, eased);
            if (arc > 0f)
                pos.y += Mathf.Sin(t * Mathf.PI) * arc;

            transform.position = pos;
            transform.rotation = Quaternion.Slerp(startRot, targetRot, eased);

            yield return null;
        }

        transform.position = target;
    }
}
