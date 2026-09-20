using TMPro;
using UnityEngine;

/// <summary>
/// 顶栏资源条上的「卡牌数量 / 回合数」显示。
/// · 卡牌数量 = 本阶段剩余可抽牌数（由 DrawCardUI 持有，与抽牌按钮上的数字同源）
/// · 回合数   = TurnManager.phaseCount（每进入一个新阶段 +1，与阶段轮盘同一数据源）
/// 生命值 / 能量不经过这里 —— Player / EnemyPlayer / PlayerStatsUI 直接写各自的 TMP。
/// </summary>
public class TopBarStats : MonoBehaviour
{
    [Tooltip("卡牌数量文本（本阶段剩余抽牌数）")]
    public TextMeshProUGUI cardsText;
    [Tooltip("回合数文本（TurnManager.phaseCount）")]
    public TextMeshProUGUI turnText;

    DrawCardUI _drawUI;
    int _lastCards = -1;
    int _lastTurn = -1;

    void Update()
    {
        if (cardsText != null)
        {
            if (_drawUI == null) _drawUI = FindObjectOfType<DrawCardUI>();
            if (_drawUI != null)
            {
                int n = _drawUI.GetRemainingDraws();
                if (n != _lastCards)
                {
                    _lastCards = n;
                    cardsText.text = n.ToString();
                }
            }
        }

        if (turnText != null && TurnManager.Instance != null)
        {
            int t = TurnManager.Instance.phaseCount;
            if (t != _lastTurn)
            {
                _lastTurn = t;
                turnText.text = t.ToString();
            }
        }
    }
}
