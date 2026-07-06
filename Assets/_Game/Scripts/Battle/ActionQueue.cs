using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [PLACEHOLDER] Action queue system — all game effects are queued and executed in order.
/// Supports AddToBottom (FIFO) and AddToTop (LIFO, "interrupt").
/// Replaces synchronous effect chains in BoardSlot/CardDrag/Card3DHover.
/// </summary>
public class ActionQueue : MonoBehaviour
{
    public static ActionQueue Instance { get; private set; }

    private Queue<IGameAction> _queue = new Queue<IGameAction>();
    private bool _executing;

    void Awake() { Instance = this; }

    public void AddToBottom(IGameAction action) { _queue.Enqueue(action); if (!_executing) ExecuteAll(); }
    public void AddToTop(IGameAction action) { } // TODO: stack-based insert

    void ExecuteAll()
    {
        _executing = true;
        while (_queue.Count > 0)
        {
            var act = _queue.Dequeue();
            act.Execute();
        }
        _executing = false;
    }
}

public interface IGameAction
{
    bool IsDone { get; }
    float Duration { get; }
    void Execute();
}
