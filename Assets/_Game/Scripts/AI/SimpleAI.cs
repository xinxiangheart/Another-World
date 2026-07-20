using UnityEngine;

/// <summary>
/// [PLACEHOLDER] Simple greedy AI for offline/single-player mode.
/// Evaluates each card in hand → picks the best scoring play.
/// </summary>
public class SimpleAI : MonoBehaviour
{
    public enum Difficulty { Easy, Normal, Hard }

    public Difficulty difficulty = Difficulty.Normal;

    public void StartTurn() { /* TODO: 待实现 */ }
    public void EvaluateAndPlay() { /* TODO: 待实现 */ }
    private float ScoreCard(CardInstance ci) { /* TODO: 待实现 */ return 0f; }
}
