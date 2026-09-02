using UnityEngine;

/// <summary>
/// 卡牌快照 proto —— 不依赖 MonoBehaviour，可序列化/反序列化。
/// 兼容现有 16 字段 pipe 格式（保持向后兼容），同时提供命名访问。
/// </summary>
[System.Serializable]
public struct CardStateProto
{
    // ═══════════════════ 标识 ═══════════════════
    public string instanceID;
    public string templateID;
    public CardZone zone;          // 当前所在区
    public int slotID;             // 板面槽位号（不在板面=-1）
    public int ownerIndex;         // 0=己方, 1=对方
    public int hostSlotID;         // 附着宿主槽位（非附着=-1）
    public int attachOrder;

    // ═══════════════════ 属性（16 字段 pipe 格式）═══════════════════
    public int currentHealth, currentAttack, currentMaxHealth;
    public int baseAttack, baseHealth, baseMaxHealth;
    public int currentCost, currentTier, baseTier;
    public bool hasShield, shieldIsPermanent, shieldEndAtBattleStart, shieldEndAtBattleEnd;
    public bool silenced, isAttached, poisoned;
    public string prefixes;        // 空格分隔
    public string grantedTraits;   // ";;" 分隔
    public int totalDamageTaken;
    public bool hasBuff;
    public string buffText;
    public bool hasDebuff;
    public string debuffText;
    public string lastGivenPrefix; // 卡名变色：最后一次赋予的新前缀（规则1）
    public string activeStatuses;  // ";;" 分隔的目标侧状态来源记录（description~…）；与 grantedTraits 同待遇

    // ═══════════════════ 槽位标记 ═══════════════════
    public bool slotBlocked, slotPrison, slotPlague, slotSpotlight;
    public int plagueRound, spotlightTier, slotTempAtk;

    // ═══════════════════ 序列化 ═══════════════════

    /// <summary>输出为现 21 字段 pipe 格式（向后兼容 BoardSyncManager.Tid()）。</summary>
    public string SerializeCard()
    {
        return string.Join("|",
            templateID ?? "",
            currentHealth, currentAttack, currentMaxHealth,
            baseAttack, baseHealth, baseMaxHealth,
            currentCost, currentTier, baseTier,
            hasShield ? (1+(shieldIsPermanent?2:0)+(shieldEndAtBattleStart?4:0)+(shieldEndAtBattleEnd?8:0)).ToString() : "0",
            silenced ? "1" : "0",
            isAttached ? "1" : "0",
            poisoned ? "1" : "0",
            prefixes ?? "",
            grantedTraits ?? "",
            totalDamageTaken,
            hasBuff ? "1" : "0",
            buffText ?? "",
            hasDebuff ? "1" : "0",
            debuffText ?? "",
            lastGivenPrefix ?? "",
            activeStatuses ?? "");
    }

    /// <summary>从 pipe 格式反序列化（不设 instanceID/zone/slotID——由调用方补充）。</summary>
    public static CardStateProto DeserializeCard(string raw)
    {
        var s = new CardStateProto();
        if (string.IsNullOrEmpty(raw)) return s;
        var p = raw.Split('|');
        if (p.Length > 0)  s.templateID = p[0];
        if (p.Length > 1)  int.TryParse(p[1], out s.currentHealth);
        if (p.Length > 2)  int.TryParse(p[2], out s.currentAttack);
        if (p.Length > 3)  int.TryParse(p[3], out s.currentMaxHealth);
        if (p.Length > 4)  int.TryParse(p[4], out s.baseAttack);
        if (p.Length > 5)  int.TryParse(p[5], out s.baseHealth);
        if (p.Length > 6)  int.TryParse(p[6], out s.baseMaxHealth);
        if (p.Length > 7)  int.TryParse(p[7], out s.currentCost);
        if (p.Length > 8)  int.TryParse(p[8], out s.currentTier);
        if (p.Length > 9)  int.TryParse(p[9], out s.baseTier);
        if (p.Length > 10 && int.TryParse(p[10], out int shieldEnc) && shieldEnc > 0)
        { s.hasShield = true; s.shieldIsPermanent = (shieldEnc & 2) != 0; s.shieldEndAtBattleStart = (shieldEnc & 4) != 0; s.shieldEndAtBattleEnd = (shieldEnc & 8) != 0; }
        if (p.Length > 11) s.silenced = p[11] == "1";
        if (p.Length > 12) s.isAttached = p[12] == "1";
        if (p.Length > 13) s.poisoned = p[13] == "1";
        if (p.Length > 14) s.prefixes = p[14];
        if (p.Length > 15) s.grantedTraits = p[15];
        if (p.Length > 16) int.TryParse(p[16], out s.totalDamageTaken);
        // Buff/Debuff 持续状态（18-21th 字段，向后兼容——旧数据缺省为 false/空）
        if (p.Length > 17) s.hasBuff = p[17] == "1";
        if (p.Length > 18) s.buffText = p[18];
        if (p.Length > 19) s.hasDebuff = p[19] == "1";
        if (p.Length > 20) s.debuffText = p[20];
        if (p.Length > 21) s.lastGivenPrefix = p[21];
        if (p.Length > 22) s.activeStatuses = p[22];
        return s;
    }

    /// <summary>从 CardInstance MonoBehaviour 快照字段。</summary>
    public static CardStateProto FromCardInstance(CardInstance ci, CardZone zone, int slotID, int ownerIndex)
    {
        if (ci == null) return new CardStateProto { zone = zone, slotID = slotID, ownerIndex = ownerIndex };
        return new CardStateProto
        {
            instanceID = ci.instanceID ?? "",
            templateID = ci.templateID ?? "",
            zone = zone,
            slotID = slotID,
            ownerIndex = ownerIndex,
            hostSlotID = ci.hostSlotID,
            attachOrder = ci.attachOrder,
            currentHealth = ci.currentHealth,
            currentAttack = ci.currentAttack,
            currentMaxHealth = ci.currentMaxHealth,
            baseAttack = ci.baseAttack,
            baseHealth = ci.baseHealth,
            baseMaxHealth = ci.baseMaxHealth,
            currentCost = ci.currentCost,
            currentTier = ci.currentTier,
            baseTier = ci.baseTier,
            hasShield = ci.hasShield,
            shieldIsPermanent = ci.shieldIsPermanent,
            shieldEndAtBattleStart = ci.shieldEndAtBattleStart,
            shieldEndAtBattleEnd = ci.shieldEndAtBattleEnd,
            silenced = ci.silencedThisPhase,
            isAttached = ci.isAttached,
            poisoned = ci.poisoned,
            prefixes = ci.prefixes ?? "",
            grantedTraits = ci.SerializeGrantedTraits(),
            totalDamageTaken = ci.totalDamageTaken,
            hasBuff = ci.hasBuff,
            buffText = ci.buffText ?? "",
            hasDebuff = ci.hasDebuff,
            debuffText = ci.debuffText ?? "",
            lastGivenPrefix = ci.lastGivenPrefix ?? "",
            activeStatuses = ci.SerializeActiveStatuses(),
        };
    }

    public bool IsEmpty => string.IsNullOrEmpty(templateID);
    public bool IsValid => !string.IsNullOrEmpty(instanceID) && !string.IsNullOrEmpty(templateID);
}
