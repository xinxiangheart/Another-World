using UnityEngine;

public class TimeWarpManager : MonoBehaviour
{
    public static TimeWarpManager Instance { get; private set; }
    public bool extraTurnPending;
    public bool inExtraTurn;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Activate()
    {
        TurnManager tm = FindObjectOfType<TurnManager>();
        if (tm.isMyTurnFirst)
            extraTurnPending = true; // 先手：本阶段敌方回合后
        else
            extraTurnPending = true; // 后手：下阶段敌方回合后（需要等一个完整阶段）
    }
}