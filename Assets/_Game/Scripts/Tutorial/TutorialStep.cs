using System;

/// <summary>
/// [PLACEHOLDER] Single tutorial step — highlight target, show text, wait for action.
/// </summary>
[Serializable]
public class TutorialStep
{
    public string instructionText;
    public string targetObjectName;   // UI element to highlight
    public TutorialAction expectedAction;
    public bool skippable = true;
}

public enum TutorialAction
{
    None,
    DragCardToBoard,
    ClickEndTurn,
    ClickDrawCard,
    PlayCounterSpell,
    DiscardCard,
    ViewDetailPanel
}
