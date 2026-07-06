using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [PLACEHOLDER] Step-by-step tutorial for first-time players.
/// Highlights UI elements, guides card play, explains energy/combat/prefixes.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static bool HasCompletedTutorial
    {
        get => PlayerPrefs.GetInt("tutorial_done", 0) == 1;
        set => PlayerPrefs.SetInt("tutorial_done", value ? 1 : 0);
    }

    private List<TutorialStep> _steps = new List<TutorialStep>();
    private int _currentStep;

    public void StartTutorial() { }
    public void NextStep() { }
    public void SkipAll() { HasCompletedTutorial = true; }
}
