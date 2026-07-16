using UnityEngine;

public abstract class AuraBase
{
    public CardInstance source;
    public int hostSlotID = -1;

    public abstract bool IsActive();
    public virtual bool BlocksTrait(CardInstance target, string traitType) => false;
    public virtual bool IsTargetFullySilenced(CardInstance target) => false;

    /// <summary>攻击方伤害修正（Stage1/Stage3）。返回修改后的伤害值。</summary>
    public virtual int ModifyDamageOutgoing(int damage, DamageContext ctx) => damage;
    /// <summary>防守方伤害修正（Stage2/Stage4）。返回修改后的伤害值。</summary>
    public virtual int ModifyDamageIncoming(int damage, DamageContext ctx) => damage;

    protected int GetSlotOf(CardInstance ci)
    {
        BoardManager bm = GameObject.FindObjectOfType<BoardManager>();
        if (bm == null) return -1;
        for (int i = 0; i < 12; i++)
        {
            BoardSlot slot = bm.GetSlot(i);
            if (slot?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == ci) return i;
        }
        foreach (GameObject obj in bm.attachedModels)
        {
            Card3DInstance c3d = obj?.GetComponent<Card3DInstance>();
            if (c3d?.cardInstance == ci) return c3d.cardInstance.hostSlotID;
        }
        return -1;
    }
}
public class MistHiderAura : AuraBase
{
    private bool _isActive = true;

    public override bool IsActive()
    {
        bool newActive = source != null;
        if (newActive != _isActive)
        {
            _isActive = newActive;
            if (_isActive) ApplyHide();
            else RemoveHide();
        }
        return _isActive;
    }
    public void ApplyHide()
    {
        // Visual hiding happens ONLY on the opponent client via BoardSync header.
        // Server's own cards (6-11) must remain visible to the owner.
        // Mark dirty so SyncNow picks up IsMistHiderActive=true → sends "1|" header.
        BoardSyncManager.MarkDirty();
    }

    public void RemoveHide()
    {
        // Mark dirty so SyncNow sends "0|" header → opponent client un-hides.
        BoardSyncManager.MarkDirty();
    }
}
public class SageAura : AuraBase
{
    public override bool IsActive() => source != null;
}

public class FanaticShamanAura : AuraBase
{
    public override bool IsActive() => source != null;

    public override bool BlocksTrait(CardInstance target, string traitType)
    {
        if (target == source) return false;
        if (GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(source))
            return false;
        int targetSlot = GetSlotOf(target);
        if (targetSlot >= 6) return false;
        return traitType == "进场" || traitType == "抛置";
    }
}

public class JudgeAura : AuraBase
{
    public override bool IsActive() => source != null;

    public override bool BlocksTrait(CardInstance target, string traitType)
    {
        if (target == source) return false;
        if (GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(source))
            return false;
        int targetSlot = GetSlotOf(target);
        if (targetSlot >= 6) return false;
        return traitType == "退场";
    }
}

public class SuppressorAura : AuraBase
{
    public override bool IsActive() => source != null;
}

public class EnergyHackerAura : AuraBase
{
    public int mySlotID;

    public override bool IsActive() => source != null;

    public override bool IsTargetFullySilenced(CardInstance target)
    {
        if (target.templateID == "01335") return false;
        int currentSlot = mySlotID;
        if (source.isAttached) currentSlot = source.hostSlotID;
        if (currentSlot < 0) return false;
        int opponentSlot = currentSlot < 6 ? currentSlot + 6 : currentSlot - 6;
        int targetSlot = GetSlotOf(target);
        return targetSlot == opponentSlot;
    }
}

public class MerchantAura : AuraBase
{
    public override bool IsActive() => source != null;
}

public class EnergyReaperAura : AuraBase
{
    public override bool IsActive() => source != null;
}

/// <summary>猩红圣徒(01533)：对手召唤物进场后受到己方血歌前缀召唤物数量伤害。</summary>
public class ScarletSaintAura : AuraBase
{
    System.Action<CardInstance> _handler;

    public ScarletSaintAura()
    {
        _handler = OnAnyMinionEntered;
        var g = GlobalEventManager.Instance;
        if (g != null) g.OnMinionEntered += _handler;
    }

    public override bool IsActive() => source != null;

    void OnAnyMinionEntered(CardInstance entered)
    {
        if (source == null || entered == null) return;
        if (GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(source))
            return;

        int saintSlot = GetSlotOf(source);
        int enteredSlot = GetSlotOf(entered);
        // 同一半场 = 友方进场，不触发
        if ((saintSlot >= 6) == (enteredSlot >= 6)) return;

        // 数己方半场血歌前缀召唤物数量
        int bloodCount = 0;
        var bm = Object.FindObjectOfType<BoardManager>();
        if (bm != null)
        {
            int sideStart = saintSlot >= 6 ? 6 : 0;
            for (int i = sideStart; i < sideStart + 6; i++)
            {
                var ci = bm.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.prefixes != null && ci.prefixes.Contains("血歌"))
                    bloodCount++;
            }
        }

        if (bloodCount > 0)
        {
            entered.currentHealth -= bloodCount;
            var e3d = Find3DOf(entered);
            e3d?.UpdateValues();
            DamagePipeline.ShowFloaterAt(entered, bloodCount, FloaterType.Damage);
        }
    }

    Card3DInstance Find3DOf(CardInstance ci)
    {
        var bm = Object.FindObjectOfType<BoardManager>();
        if (bm == null) return null;
        for (int i = 0; i < 12; i++)
        {
            var c3d = bm.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>();
            if (c3d?.cardInstance == ci) return c3d;
        }
        return null;
    }
}