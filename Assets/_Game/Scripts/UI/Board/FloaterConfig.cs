using UnityEngine;

// ============================================================================
// FloaterConfig — 浮动数字可视化配置 (ScriptableObject)
// ============================================================================
//
// 在 Project 窗口右键 → Create → Another World → Floater Config 创建 .asset，
// 拖到 Resources 目录下或 Inspector 的 DamageFloater.config 字段即可。
// 全部参数可在 Inspector 实时调整，无需改代码。
// ============================================================================

[CreateAssetMenu(menuName = "Another World/Floater Config", fileName = "FloaterConfig")]
public class FloaterConfig : ScriptableObject
{
    [Header("通用")]
    [Tooltip("弹出持续时间（秒）")]
    public float duration = 1.2f;
    [Tooltip("初速（世界单位/秒，方向见下面「弹出轨迹」的偏角）")]
    public float floatSpeed = 1.3f;
    [Tooltip("浮字在卡牌顶边之上的余量（世界单位）。卡高约 1.78，给 0.4 左右数字就贴在卡顶")]
    public float worldOffsetY = 0.42f;
    [Tooltip("渐隐开始比例（0=立即开始, 0.5=半程开始）")]
    public float fadeStart = 0.45f;

    [Header("弹出轨迹（随机）")]
    [Tooltip("弹出方向相对正上方的最大偏角（度）：0=纯向上，50≈左右各 50°")]
    public float angleSpread = 50f;
    [Tooltip("初速随机浮动（比例）：±该值")]
    public float speedJitter = 0.22f;
    [Tooltip("出生点随机偏移（世界单位）：横向 ±该值，纵向 ±60%")]
    public float spawnJitter = 0.24f;
    [Tooltip("抛线下坠（世界单位/秒²）：越大越像「扔出去」")]
    public float gravity = 2.2f;
    [Tooltip("横向速度收束（世界单位/秒²）：把随机横移慢慢拉回竖直")]
    public float horizontalDrift = 1.6f;
    [Tooltip("大小随机浮动（比例）：±该值")]
    public float sizeJitter = 0.1f;
    [Tooltip("随机倾斜（度）：±该值")]
    public float spinJitter = 6f;
    [Tooltip("弹入时长（秒）：从小冲过头再收回")]
    public float popTime = 0.16f;
    [Tooltip("弹入起始缩放（相对基准）")]
    public float popFrom = 0.55f;

    [Header("材质")]
    [Tooltip("字面相对类型色提亮的比例（0=纯类型色，0.4≈近白）")]
    public float fillBrighten = 0.22f;
    [Tooltip("勾上：描边取「类型色压深」（有颜色）；不勾则用下面的 outlineColor")]
    public bool useRimColor = true;
    [Tooltip("描边 = 类型色 × 该系数")]
    public float rimDarken = 0.3f;
    [Tooltip("字面加粗（FaceDilate）：顶栏数值用的是 0.3")]
    public float faceDilate = 0.28f;
    [Tooltip("描边柔和度（0=硬边）")]
    public float outlineSoftness = 0.06f;
    [Tooltip("勾上：字下垫一层柔投影，压在花哨卡面上也读得清")]
    public bool useShadow = true;
    [Tooltip("投影颜色")]
    public Color shadowColor = new Color(0f, 0f, 0f, 0.62f);
    [Tooltip("投影偏移（描边宽度单位）")]
    public float shadowOffset = 0.9f;

    [Header("字体")]
    [Tooltip("字体大小")]
    public float fontSize = 58f;
    [Tooltip("描边宽度")]
    public float outlineWidth = 0.22f;
    [Tooltip("描边颜色（仅在 useRimColor 关掉时使用）")]
    public Color outlineColor = new Color(0.05f, 0.04f, 0.04f, 1f);
    [Tooltip("粗体")]
    public bool bold = true;
    [Tooltip("弹窗宽度")]
    public float boxWidth = 160f;
    [Tooltip("弹窗高度")]
    public float boxHeight = 60f;

    [Header("伤害")]
    [Tooltip("颜色")]
    public Color damageColor = new Color(1f, 0.22f, 0.2f, 1f);
    [Tooltip("缩放倍数")]
    public float damageScale = 1.05f;

    [Header("治疗")]
    [Tooltip("颜色")]
    public Color healColor = new Color(0.2f, 1f, 0.3f, 1f);
    [Tooltip("缩放倍数")]
    public float healScale = 0.9f;

    [Header("抵挡")]
    [Tooltip("颜色")]
    public Color blockedColor = new Color(0.3f, 0.5f, 1f, 1f);
    [Tooltip("缩放倍数")]
    public float blockedScale = 1f;
    [Tooltip("文字")]
    public string blockedText = "抵挡!";

    [Header("增幅（+攻/+血 等）")]
    [Tooltip("颜色")]
    public Color buffColor = new Color(1f, 0.85f, 0.1f, 1f);
    [Tooltip("缩放倍数")]
    public float buffScale = 0.85f;

    [Header("减益（-攻/-血 等）")]
    [Tooltip("颜色")]
    public Color debuffColor = new Color(0.7f, 0.3f, 1f, 1f);
    [Tooltip("缩放倍数")]
    public float debuffScale = 0.85f;

    [Header("Canvas")]
    [Tooltip("Canvas 排序层级")]
    public int sortingOrder = 100;
    [Tooltip("Canvas 距相机距离")]
    public float planeDistance = 5f;
}
