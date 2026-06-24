using UnityEngine;
using UnityEngine.UI;

public class FakeEnemyPlayButton : MonoBehaviour
{
    public static FakeEnemyPlayButton Instance { get; private set; }

    // 标记：下一次打出视为对方打出（一次性）
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
        nextPlayAsEnemy = true;
        Debug.Log("[FakeEnemyPlay] 下一次打出将视为对方打出");
    }

    // ========== 对外接口 ==========

    /// <summary>
    /// 在卡牌打出时调用。返回 true 表示本次为对方打出。
    /// 同时触发己方反制牌检测。
    /// </summary>
    public static bool OnCardPlayed(CardData template)
    {
        bool playAsEnemy = nextPlayAsEnemy;
        nextPlayAsEnemy = false; // 一次性

        if (playAsEnemy && template != null)
        {
            // 反制牌不触发反制检测（反制牌是对面打出的才算）
            if ((template.spellType & SpellType.Counter) == 0)
            {
                Debug.Log($"[FakeEnemyPlay] 模拟对方打出: {template.cardName}");
                CounterManager.Instance?.CheckOnCardPlayed(template);
            }
        }

        return playAsEnemy;
    }

    /// <summary>
    /// 获取当前应该使用的槽位范围。
    /// playAsEnemy 为 true 时返回敌方槽位(0-5)，否则返回己方槽位(6-11)。
    /// 注意：这里用的是 nextPlayAsEnemy 而不是已消耗的标记，
    /// 因为在放置阶段标记还没被消耗（OnCardPlayed 还没调）。
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
    /// 己方单位面向敌方(180)，敌方单位面向己方(0)。
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
    /// 是否为敌方放置模式（在放置阶段读取，此时标记未被消耗）
    /// </summary>
    public static bool IsEnemyPlacement()
    {
        return nextPlayAsEnemy;
    }
}