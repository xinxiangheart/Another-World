using UnityEngine;

// ============================================================================
// DamageFX — 伤害粒子动画静态入口（表现层，不参与伤害计算）
// ============================================================================
//
// 各伤害触发点调用 Request 请求粒子演出（替代直接 DamageFloater.Show）。
// 粒子起点/轨迹由 DamageAnimationManager 按来源类型计算：
//   Player/Spell → 屏幕中心下/上侧 直飞
//   Grid         → 格子中心 特殊轨迹
//   Self         → 自身中心 特殊轨迹
//   Attacker     → 攻击者槽位 直飞
//
// 兜底：管理器未初始化 / 目标世界坐标无效 → 直接 DamageFloater.Show（数字照弹）。
// 逻辑不阻塞：粒子是纯演出，伤害结算已由调用方先行完成。
// ============================================================================

public static class DamageFX
{
    /// <summary>请求一次伤害粒子演出。到达终点时弹伤害数字（DamageFloater.Show）。</summary>
    /// <param name="targetWorldPos">目标世界坐标（弹数字位置）</param>
    /// <param name="value">伤害数值</param>
    /// <param name="type">浮动数字类型</param>
    /// <param name="source">来源类型（决定起点/轨迹）</param>
    /// <param name="sourceSide">来源方 0=己方 1=对方（串行分组 + 屏幕上下侧）</param>
    /// <param name="sourceSlotID">格子来源槽位ID（Grid 用，其它 -1）</param>
    /// <param name="selfDamage">自伤标记（自身中心特殊轨迹）</param>
    public static void Request(Vector3 targetWorldPos, int value, FloaterType type,
        DamageFxSource source = DamageFxSource.Player, int sourceSide = 0,
        int sourceSlotID = -1, bool selfDamage = false)
    {
        // 兜底：目标无效或管理器未就绪 → 直接弹数字
        if (targetWorldPos == Vector3.zero)
        {
            DamageFloater.Show(targetWorldPos, value, type);
            return;
        }
        DamageAnimationManager.EnsureInstance();
        if (DamageAnimationManager.Instance == null)
        {
            DamageFloater.Show(targetWorldPos, value, type);
            return;
        }
        DamageAnimationManager.Instance.Enqueue(new DamageFxEvent
        {
            targetWorldPos = targetWorldPos,
            value = value,
            type = type,
            source = source,
            sourceSide = sourceSide,
            sourceSlotID = sourceSlotID,
            selfDamage = selfDamage,
        });
    }

    /// <summary>玩家/英雄世界坐标：己方=屏幕中心最下侧，敌方=屏幕中心最上侧（与卡牌同深度平面）。</summary>
    public static Vector3 GetPlayerWorldPos(bool enemy)
    {
        Camera cam = Camera.main;
        if (cam == null) return Vector3.zero;
        return cam.ViewportToWorldPoint(new Vector3(0.5f, enemy ? 1f : 0f, 9f));
    }
}
