using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LobbyRingNodes —— 大厅左侧星环的 12 颗点，2026-09-26。
///
/// 环上 12 颗点各自是一个**独立 RawImage**（贴图出成近白，颜色全靠 tint），这个组件管两件事：
///
///   ① 常亮（litCount）：前 N 颗钉死在满亮。
///   ② 自身闪烁（twinkleOn）：**每一颗自己**缓慢亮暗 —— 不是灯头在跑，是 12 颗各闪各的，
///      靠「相邻两颗错开一段相位」连成一道缓慢的微光波。亮度走**正弦**，所以亮度是连续变化的，
///      **不会亮一下停一会儿**；最暗那一档快熄灭（twinkleMin），最亮满亮（twinkleMax）。
///
/// 两个旋钮的直观含义（都按屏口径、顺时针自正上方偏右 10° 起：-80° / -50° / … / 250°）：
///   · twinklePeriod   = 一颗自己「灭到底再亮回来」要几秒 —— **越大越慢**；
///   · twinklePhaseStep = 相邻两颗错开多少个周期（1/12 = 一圈正好一个周期，此时整道波 6 秒绕环一圈；
///                        调大 = 相邻两点闪得隔得更开，同时波绕环的速度也变快）。
///
/// **动起来的前提**：进 Play；或勾上 previewInEditMode 在 Scene 视图里直接看（不进 Play）。
///
/// 口径与 Assets/_Game/Art/Sprites/Generated/lobby-bg-v2/README.md 一致：
/// 点显示 52px、辉光 112px，环心 (560, 540)、半径 350，12 颗每 30° 一颗。
/// 挂载：LobbyUI_v1/Bg_v2/Ring（由 Assets/_Game/Editor/LobbyBgV2Builder.cs 生成）。
/// </summary>
[ExecuteAlways]
public class LobbyRingNodes : MonoBehaviour
{
    [Tooltip("12 颗点，数组下标 = 槽位顺序（顺时针，自正上方偏右 10° 起）")]
    public RawImage[] nodes = new RawImage[0];

    [Tooltip("12 层辉光，与 nodes 一一对应，渲染在对应点之下")]
    public RawImage[] glows = new RawImage[0];

    [Tooltip("常亮前 N 颗（0..12）；0 = 全部参与闪烁")]
    [Range(0, 12)] public int litCount = 0;

    [Header("自身闪烁（每颗自己缓慢呼吸，连通无停顿）")]
    [Tooltip("开：每颗自己缓慢亮暗，靠相位差连成一道微光波。关：只有上面的常亮点，画面完全静止")]
    public bool twinkleOn = true;

    [Tooltip("一颗自己「灭下去再亮回来」要几秒 —— 越大越慢")]
    [Range(0.5f, 30f)] public float twinklePeriod = 6f;

    [Tooltip("相邻两颗错开多少个周期：1/12 = 一圈正好一个周期（此时整道波 6 秒绕环一圈）；调大 = 相邻两点闪得更开，但波绕环也更快")]
    [Range(0f, 0.5f)] public float twinklePhaseStep = 1f / 12f;

    [Tooltip("最暗那一档 —— 越接近 0 越接近熄灭（0 = 完全灭）")]
    [Range(0f, 1f)] public float twinkleMin = 0.06f;

    [Tooltip("最亮那一档")]
    [Range(0f, 1f)] public float twinkleMax = 1f;

    [Tooltip("勾上：不进 Play，Scene 视图里也一直闪（方便调参）。编辑态会持续改颜色，场景会一直被标成「已修改」，属正常")]
    public bool previewInEditMode = false;

    public Color dimTint = new Color(0.40f, 0.33f, 0.16f, 0.85f);
    public Color litTint = new Color(0.91f, 0.82f, 0.54f, 1.00f);
    public Color glowTint = new Color(0.91f, 0.82f, 0.54f, 0.62f);

    float _t;       // 闪烁的时间轴（秒），只累加

    void OnValidate() { _t = 0f; Apply(); }
    void OnEnable() { _t = 0f; Apply(); }

    /// <summary>按 litCount + 闪烁刷一遍颜色。纯改 color，不动位置 / 尺寸 / 贴图。</summary>
    public void Apply()
    {
        int n = nodes == null ? 0 : nodes.Length;
        if (n == 0) return;

        for (int i = 0; i < n; i++)
        {
            float t;
            if (i < litCount) t = 1f;                       // ① 常亮
            else if (!twinkleOn) t = 0f;                    // ② 不闪
            else
            {
                // 相位取负号：峰从 0 号位往 11 号位反向推进 = 顺着数组下标 = **顺时针**
                float u = _t / Mathf.Max(0.01f, twinklePeriod) - i * twinklePhaseStep;
                float w = 0.5f - 0.5f * Mathf.Cos(u * Mathf.PI * 2f);   // 0..1 连续正弦，没有停顿段
                t = Mathf.Lerp(twinkleMin, twinkleMax, w);
            }
            SetNode(i, Mathf.Clamp01(t));
        }
    }

    void Update()
    {
        if (!twinkleOn) return;
        if (!Application.isPlaying && !previewInEditMode) return;
        _t += Time.unscaledDeltaTime;       // 大厅即使暂停，闪烁照走
        Apply();
    }

    /// <summary>t = 0 暗态（dimTint、辉光全灭）→ t = 1 亮态（litTint + glowTint.a 的辉光）。</summary>
    void SetNode(int i, float t)
    {
        if (nodes[i] != null) nodes[i].color = Color.Lerp(dimTint, litTint, t);
        if (glows != null && i < glows.Length && glows[i] != null)
        {
            Color g = glowTint;
            g.a = glowTint.a * t;
            glows[i].color = g;
        }
    }
}