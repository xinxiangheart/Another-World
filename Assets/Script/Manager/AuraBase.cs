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
        BoardManager bm = GameObject.FindObjectOfType<BoardManager>();
        if (bm == null) return;

        for (int i = 6; i <= 11; i++)
        {
            BoardSlot s = bm.GetSlot(i);
            if (s?.currentCard3D != null)
            {
                CardDisplay3D display = s.currentCard3D.GetComponent<CardDisplay3D>();
                if (display != null) display.HideAllInfo();
            }
        }

        foreach (GameObject obj in bm.attachedModels)
        {
            if (obj == null) continue;
            CardDisplay3D display = obj.GetComponent<CardDisplay3D>();
            if (display != null) display.HideAllInfo();
        }
    }

    public void RemoveHide()
    {
        BoardManager bm = GameObject.FindObjectOfType<BoardManager>();
        if (bm == null) return;

        for (int i = 6; i <= 11; i++)
        {
            BoardSlot s = bm.GetSlot(i);
            if (s?.currentCard3D != null)
            {
                CardDisplay3D display = s.currentCard3D.GetComponent<CardDisplay3D>();
                if (display != null) display.ShowAllInfo();
            }
        }

        foreach (GameObject obj in bm.attachedModels)
        {
            if (obj == null) continue;
            CardDisplay3D display = obj.GetComponent<CardDisplay3D>();
            if (display != null) display.ShowAllInfo();
        }
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
        return traitType == "½ø³¡" || traitType == "Å×ÖÃ";
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
        return traitType == "ÍË³¡";
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