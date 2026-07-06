using System.Collections.Generic;
using UnityEngine;

public class DamageSourceMarker : MonoBehaviour
{
    // 本阶段所有对该卡牌造成伤害的来源
    public List<GameObject> damageSources = new List<GameObject>();

    // 本阶段累计伤害
    public int totalDamageThisPhase = 0;

    // 记录一个伤害来源
    public void RegisterDamage(GameObject source, int amount)
    {
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
        // 记录敌方来源
        if (source3D?.cardInstance != null)
        {
            int sourceSlot = GetSlotOf(source3D.cardInstance);
            if (sourceSlot >= 0 && sourceSlot <= 5) // 敌方
            {
                if (!targetInst.enemyDamageSourceIDs.Contains(sourceInstanceID))
                    targetInst.enemyDamageSourceIDs.Add(sourceInstanceID);
            }
        }
    }
    // 检查是否死于敌方召唤物的伤害（用于触发反击）
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