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
    public float duration = 1.5f;
    [Tooltip("每秒向上飘移的世界单位")]
    public float floatSpeed = 1.2f;
    [Tooltip("模型上方偏移量（世界单位）")]
    public float worldOffsetY = 2.5f;
    [Tooltip("渐隐开始比例（0=立即开始, 0.5=半程开始）")]
    public float fadeStart = 0f;

    [Header("字体")]
    [Tooltip("字体大小")]
    public float fontSize = 36f;
    [Tooltip("描边宽度")]
    public float outlineWidth = 0.25f;
    [Tooltip("描边颜色")]
    public Color outlineColor = new Color(0, 0, 0, 0.7f);
    [Tooltip("粗体")]
    public bool bold = true;
    [Tooltip("弹窗宽度")]
    public float boxWidth = 120f;
    [Tooltip("弹窗高度")]
    public float boxHeight = 40f;

    [Header("伤害")]
    [Tooltip("颜色")]
    public Color damageColor = new Color(1f, 0.2f, 0.2f, 1f);
    [Tooltip("缩放倍数")]
    public float damageScale = 1.1f;

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

    [Header("Canvas")]
    [Tooltip("Canvas 排序层级")]
    public int sortingOrder = 100;
    [Tooltip("Canvas 距相机距离")]
    public float planeDistance = 5f;
}
