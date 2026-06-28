using UnityEngine;
using TMPro;

/// <summary>
/// Canvas 上挂此脚本，拖入一个 TextMeshProUGUI。
/// 每帧读取 TurnManager.currentPhase + phaseCount 显示阶段名。
/// </summary>
public class TurnPhaseDisplay : MonoBehaviour
{
    public TextMeshProUGUI displayText;

    void Update()
    {
        TurnManager tm = TurnManager.Instance;
        if (tm == null)
        {
            if (displayText != null) displayText.text = "";
            return;
        }

        string phaseName = tm.currentPhase switch
        {
            TurnManager.TurnPhase.PhaseStart => "准备阶段",
            TurnManager.TurnPhase.MyTurn     => "己方回合",
            TurnManager.TurnPhase.EnemyTurn  => "对方回合",
            TurnManager.TurnPhase.BattlePhase => "攻击回合",
            _                                => ""
        };

        if (displayText != null)
            displayText.text = $"当前回合：{phaseName}（第{tm.phaseCount}阶段）";
    }
}
