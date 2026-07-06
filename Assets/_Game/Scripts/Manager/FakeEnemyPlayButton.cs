using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class FakeEnemyPlayButton : MonoBehaviour
{
    public static FakeEnemyPlayButton Instance { get; private set; }

    // 标记：下一次打牌视为对方出牌（一次性）
    public static bool nextPlayAsEnemy = false;

    private Button button;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
    }

    void OnClick()
    {
        // Disabled in network mode
        if (NetworkServer.active || NetworkClient.isConnected) return;

        nextPlayAsEnemy = true;
        Debug.Log("[FakeEnemyPlay] 下一次打牌视为对方出牌");
    }

    // ========== 公共接口 ==========

    /// <summary>
    /// 在卡牌打出时调用。返回 true 表示该牌作为对方出牌。
    /// 同时触发对方的反制牌检测。
    /// </summary>
    public static bool OnCardPlayed(CardData template)
    {
        // Disabled in network mode - enemy plays come from real remote player
        if (NetworkServer.active || NetworkClient.isConnected) return false;

        bool playAsEnemy = nextPlayAsEnemy;
        nextPlayAsEnemy = false; // 一次性

        if (playAsEnemy && template != null)
        {
            // 非反制牌才触发反制牌检测（避免模拟对方的反制触发己方的不冻）
            if ((template.spellType & SpellType.Counter) == 0)
            {
                Debug.Log($"[FakeEnemyPlay] 模拟对方打牌: {template.cardName}");
                CounterManager.Instance?.CheckOnCardPlayed(template);
            }
        }

        return playAsEnemy;
    }

    /// <summary>
    /// 获取当前应使用的槽位范围。
    /// playAsEnemy 为 true 时返回敌方槽(0-5)，否则返回己方槽(6-11)。
    /// </summary>
    public static void GetSlotRange(out int min, out int max)
    {
        if (nextPlayAsEnemy)
        {
            min = 0;
            max = 5;
        }
        else
        {
            min = 6;
            max = 11;
        }
    }

    /// <summary>
    /// 获取替换模式下的槽位范围。
    /// </summary>
    public static void GetReplaceSlotRange(out int min, out int max)
    {
        if (nextPlayAsEnemy)
        {
            min = 0;
            max = 5;
        }
        else
        {
            min = 6;
            max = 11;
        }
    }

    /// <summary>
    /// 放置完成后调整3D模型朝向。
    /// 敌方槽位朝向己方(180)，己方槽位朝向敌方(0)。
    /// </summary>
    public static void AdjustModelRotation(GameObject model)
    {
        if (model == null) return;
        if (nextPlayAsEnemy)
        {
            model.transform.rotation = Quaternion.Euler(0, 0, 0);
        }
        else
        {
            model.transform.rotation = Quaternion.Euler(0, 180, 0);
        }
    }

    /// <summary>
    /// 是否为敌方出牌模式（在放置阶段读取，此时标志还未被消费）
    /// </summary>
    public static bool IsEnemyPlacement()
    {
        return nextPlayAsEnemy;
    }
}
