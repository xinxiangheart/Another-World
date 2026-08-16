using UnityEngine;

// ============================================================================
// AnimationConfig — 动画参数可视化配置 (ScriptableObject)
// ============================================================================
//
// 仿照 FloaterConfig 模式。在 Project 窗口右键 → Create → Another World → Animation Config
// 创建 .asset，放到 Resources/Config/ 下即可被 AnimationConfig.Load() 加载。
// 全部参数可在 Inspector 实时调整，无需改代码。
// ============================================================================

[CreateAssetMenu(menuName = "Another World/Animation Config", fileName = "AnimationConfig")]
public class AnimationConfig : ScriptableObject
{
    [Header("手牌抽入")]
    [Tooltip("单张牌飞行时长（秒）")]
    public float cardDrawDuration = 0.4f;
    [Tooltip("多张牌逐张延迟（秒）")]
    public float cardDrawStaggerDelay = 0.12f;
    [Tooltip("延迟让位触发点（0~1，飞行进度到该比例时才让现有手牌滑动让位）")]
    public float deferredLayoutTrigger = 0.5f;
    [Tooltip("弹性缓动过冲系数（ease-out back，1.2≈轻微过冲，1.7≈明显过冲）")]
    public float flyEaseOvershoot = 1.2f;
    [Tooltip("飞行初始 Z 轴旋转角（度，负=向左偏），到位归零")]
    public float flyZRotation = 8f;
    [Tooltip("飞行起始缩放（相对目标缩放的比例，0.95=95%→100%）")]
    public float flyScaleMin = 0.95f;
    [Tooltip("飞行时长随机化幅度（0.1=±10%）")]
    public float flyDurationRandomness = 0.1f;

    [Header("手牌悬停")]
    [Tooltip("悬停抬升高度")]
    public float hoverLiftHeight = 0.1f;
    [Tooltip("悬停缩放倍数")]
    public float hoverScale = 1.05f;
    [Tooltip("悬停动画时长（秒）")]
    public float hoverDuration = 0.15f;

    [Header("3D卡牌漂浮")]
    [Tooltip("漂浮振幅（世界单位）")]
    public float floatAmplitude = 0.02f;
    [Tooltip("漂浮频率")]
    public float floatFrequency = 0.5f;

    [Header("3D卡牌呼吸")]
    [Tooltip("呼吸最大缩放倍数（1.0 为基准）")]
    public float breatheMaxScale = 1.01f;
    [Tooltip("呼吸完整周期时长（秒）")]
    public float breatheDuration = 2f;

    [Header("攻击动画")]
    [Tooltip("蓄力时长（秒）")]
    public float windupDuration = 0.15f;
    [Tooltip("冲刺时长（秒）")]
    public float lungeDuration = 0.20f;
    [Tooltip("击中后停留时长（秒）")]
    public float impactStay = 0.15f;
    [Tooltip("返回时长（秒）")]
    public float returnDuration = 0.30f;
    [Tooltip("冲刺弧线高度（世界单位）")]
    public float arcHeight = 0.8f;
    [Tooltip("蓄力后拉/下沉幅度（世界单位）")]
    public float windupPullback = 0.15f;
    [Tooltip("目标受击震动强度（世界单位）")]
    public float targetShakeStrength = 0.08f;
    [Tooltip("目标受击震动时长（秒）")]
    public float targetShakeDuration = 0.15f;
    [Tooltip("返回弹性过冲倍数（1.05=轻微过冲）")]
    public float returnOvershoot = 1.05f;
    [Tooltip("攻击俯仰角（度）：蓄力 -pitchAngle 翘起，冲刺 +pitchAngle 下压")]
    public float pitchAngle = 12f;

    public static AnimationConfig Load()
        => Resources.Load<AnimationConfig>("Config/AnimationConfig");
}
