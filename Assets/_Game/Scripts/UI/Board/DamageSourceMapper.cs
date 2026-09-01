using System.Collections.Generic;
using UnityEngine;

public class DamageSourceMarker : MonoBehaviour
{
    // 本阶段所有对该卡牌造成伤害的来源
    public List<GameObject> damageSources = new List<GameObject>();

    // 本阶段累计伤害
    public int totalDamageThisPhase = 0;

    // 效果级溯源：本阶段最近一次伤害的来源明细（可为空——兼容非效果伤害）
    public int lastTraitIndex = -1;   // 特性序号（第N条可见特性），非特性=-1
    public string lastTraitText;      // 特性文本（"先手：对对方前排1伤"）
    public string lastEffectText;     // 法术效果描述（CardData.effect）

    // 记录一个伤害来源（traitIndex/traitText 特性级溯源；effectText 法术级溯源，均可空）
    public void RegisterDamage(GameObject source, int amount, int traitIndex = -1, string traitText = null, string effectText = null)
    {
        lastTraitIndex = traitIndex;
        lastTraitText = traitText;
        lastEffectText = effectText;
        if (source == null) return;

        Card3DInstance source3D = source.GetComponent<Card3DInstance>();
        if (source3D?.cardInstance == null) return;

        string sourceInstanceID = source3D.cardInstance.instanceID;

        if (!damageSources.Contains(source))
            damageSources.Add(source);

        CardInstance targetInst = GetComponent<Card3DInstance>()?.cardInstance;
        if (targetInst != null && !targetInst.damageSourceInstanceIDs.Contains(sourceInstanceID))
        {
            targetInst.damageSourceInstanceIDs.Add(sourceInstanceID);
        }

        totalDamageThisPhase += amount;

        // 记录敌方来源：对比攻击方和防守方的半场，不同半场=敌方
        if (source3D?.cardInstance != null && targetInst != null)
        {
            int sourceSlot = GetSlotOf(source3D.cardInstance);
            int targetSlot = GetSlotOf(targetInst);
            bool isEnemy = (sourceSlot >= 6) != (targetSlot >= 6); // 不同半场=敌方
            if (isEnemy && !targetInst.enemyDamageSourceIDs.Contains(sourceInstanceID))
                targetInst.enemyDamageSourceIDs.Add(sourceInstanceID);
        }
    }
    // 检查是否死于敌方召唤物的伤害（用于触发反击）
    [System.Obsolete]
    public bool DiedFromMinionDamage()
    {
        if (totalDamageThisPhase <= 0) return false;
        foreach (GameObject source in damageSources)
        {
            if (source != null && source.GetComponent<Card3DInstance>() != null)
                return true;
        }
        return false;
    }

    // 获取所有敌方召唤物伤害来源
    public List<GameObject> GetMinionDamageSources()
    {
        List<GameObject> minionSources = new List<GameObject>();
        foreach (GameObject source in damageSources)
        {
            if (source != null && source.GetComponent<Card3DInstance>() != null)
                minionSources.Add(source);
        }
        return minionSources;
    }

    // 阶段结束时清空
    public void ClearPhase()
    {
        damageSources.Clear();
        totalDamageThisPhase = 0;
    }
    int GetSlotOf(CardInstance ci)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        for (int i = 0; i < 12; i++)
        {
            if (bm?.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == ci)
                return i;
        }
        return -1;
    }
}