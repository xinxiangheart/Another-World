using UnityEngine;

public abstract class AuraBase
{
    public CardInstance source;
    public int hostSlotID = -1;

    public abstract bool IsActive();
    public virtual bool BlocksTrait(CardInstance target, string traitType) => false;
    public virtual bool IsTargetFullySilenced(CardInstance target) => false;

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