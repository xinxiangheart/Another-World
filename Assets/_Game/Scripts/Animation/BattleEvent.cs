using System;
using System.Collections;
using UnityEngine;

// ============================================================================
// BattleEvent — 战斗动画事件基类（逻辑与动画解耦）
// ============================================================================
//
// 战斗的逻辑结算（伤害值、目标、死亡标记）先全部完成，
// 动画只是"演出"已经算好的结果，动画不改变任何逻辑结果。
//
// Play()   ：播放"接近 + 击中"（阻塞到 onImpact 触发）。
// Return() ：播放"返回原位"（由 BattleAnimator 后台驱动，用于重叠窗口）。
// ============================================================================

public abstract class BattleEvent
{
    /// <summary>格子顺序（决定播放顺序，先手方在前）。</summary>
    public int slotIndex;

    /// <summary>是否先手事件（第一阶段普通攻击为 false，后续接入先手动画用）。</summary>
    public bool isFirstStrike;

    /// <summary>播放"飞向目标 + 击中"（阻塞到 onImpact 触发）。</summary>
    public abstract IEnumerator Play();

    /// <summary>播放"返回原位"（后台执行，不阻塞主流程）。</summary>
    public abstract IEnumerator Return();
}

/// <summary>攻击事件：攻击者飞向目标，击中时触发 onImpact（扣血+数字+音效）。</summary>
public class AttackEvent : BattleEvent
{
    /// <summary>攻击者 3D 模型（可能为 null，则跳过动画直接触发 onImpact）。</summary>
    public GameObject attackerModel;

    /// <summary>被攻击者 3D 模型（null = 打英雄，只做原地动作不飞向）。</summary>
    public GameObject defenderModel;

    /// <summary>已算好的伤害值（动画中不重算）。</summary>
    public int damage;

    /// <summary>是否打英雄（打空位）。打英雄伤害走 FinalDamage 净差，此处仅演出。</summary>
    public bool isHeroAttack;

    /// <summary>溅射/附带伤害：跳过飞向动画，直接结算伤害（onImpact）。溅射动画后续单独做。</summary>
    public bool skipAnimation;

    /// <summary>击中回调（扣血 + 弹伤害数字 + 播放音效）。</summary>
    public Action onImpact;

    public override IEnumerator Play()
    {
        var anim = !skipAnimation && attackerModel != null ? attackerModel.GetComponent<Card3DAttackAnimator>() : null;
        if (anim == null)
        {
            // 无动画组件（模型未生成）或溅射伤害（skipAnimation）→ 直接触发 onImpact
            onImpact?.Invoke();
            yield break;
        }

        // 打英雄：defenderModel 为 null，原地挥砍动作；打随从：飞向目标
        yield return anim.ApproachAndHit(defenderModel, onImpact);
    }

    public override IEnumerator Return()
    {
        var anim = !skipAnimation && attackerModel != null ? attackerModel.GetComponent<Card3DAttackAnimator>() : null;
        if (anim != null)
            yield return anim.ReturnToOriginal();
    }
}
