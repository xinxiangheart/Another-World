using TMPro;
using UnityEngine;

/// <summary>
/// 顶栏（对方信息栏）右侧的「对方手牌数 / 回合数」。
/// · 对方手牌数 = NetworkPlayer.Remote.handCardCount（手牌张数，服务端同步；离线 AI 也是这只对象）
/// · 己方手牌数 = NetworkPlayer.Local.handCardCount（同一 SyncVar，本端自己那只对象）
/// · 回合数     = TurnManager.phaseCount（每进入一个新阶段 +1，与阶段轮盘同一数据源）
/// 左半边的对方生命值 / 能量不经过这里 —— EnemyPlayer 直接写自己的 TMP。
/// 己方的生命值 / 能量预留为后续独立显示（场景里挂在 StatIcons/SelfStats，暂关）。
/// </summary>
public class TopBarStats : MonoBehaviour
{
    [Tooltip("对方手牌数文本（NetworkPlayer.Remote.handCardCount）")]
    public TextMeshProUGUI handText;
    [Tooltip("己方手牌数文本（NetworkPlayer.Local.handCardCount）")]
    public TextMeshProUGUI selfHandText;
    [Tooltip("回合数文本（TurnManager.phaseCount）")]
    public TextMeshProUGUI turnText;

    int _lastHand = -1;
    int _lastSelfHand = -1;
    int _lastTurn = -1;

    void Update()
    {
        if (handText != null)
        {
            NetworkPlayer remote = NetworkPlayer.Remote;
            if (remote != null)
            {
                int n = remote.handCardCount;
                if (n != _lastHand)
                {
                    _lastHand = n;
                    handText.text = n.ToString();
                }
            }
        }

        if (selfHandText != null)
        {
            NetworkPlayer local = NetworkPlayer.Local;
            if (local != null)
            {
                int n = local.handCardCount;
                if (n != _lastSelfHand)
                {
                    _lastSelfHand = n;
                    selfHandText.text = n.ToString();
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
