using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 分层卡框（v7 组件分离）—— 卡身 / 名牌 / 画窗 / 数值条 / 费用边带 五层各自独立。
///
/// 组件分离后「费用档」只活在边带这一层：换边带贴图（Frame_Edge_0..5），
/// 或勾 useTint 用白色边带（Frame_Edge_Tint）乘 edgeColors[费用]。
/// 其余四层是静态图，可单独替换 / 单独挪位 / 单独关掉，不用重出整框。
///
/// 挂在卡牌根节点，2D(Image) 与 3D(SpriteRenderer) 共用一套写法。
/// 没挂本组件的预制体不受影响：显示脚本仍走原来的「整框贴图」路径。
/// </summary>
public class CardFrameLayers : MonoBehaviour
{
    [Header("2D 层（Image，整框铺满卡面）")]
    public Image body;          // 卡身（含原画透明洞）
    public Image namePlate;     // 名牌底板
    public Image artWindow;     // 画窗金线（中间透明）
    public Image statPlate;     // 数值条 / 说明条底板

    [Header("2D 费用边带（费用档只改这一层）")]
    public Image edge;

    [Header("3D 层（SpriteRenderer，同卡面叠不同 z）")]
    public SpriteRenderer bodySR;
    public SpriteRenderer namePlateSR;
    public SpriteRenderer artWindowSR;
    public SpriteRenderer statPlateSR;
    public SpriteRenderer edgeSR;

    [Header("费用边带 · 换贴图模式")]
    [Tooltip("6 张边带贴图（index=费用 0-5）；留空则按 edgePath 路径加载")]
    public Sprite[] edgeSprites;
    [Tooltip("相对 Assets/_Game/Resources/，{0}=费用")]
    public string edgePath = "Cards/Frame/Frame_Edge_{0}";

    [Header("费用边带 · 乘色模式（可选，勾 useTint 生效）")]
    [Tooltip("白色边带乘以 edgeColors[费用] —— 可运行时实时变色（减费 3→2 当场跳色）")]
    public bool useTint;
    public Sprite edgeTintSprite;
    [Tooltip("0-5 费边带颜色（乘色模式）")]
    public Color[] edgeColors =
    {
        new Color32(0x92, 0x9A, 0xA4, 0xFF),   // 0
        new Color32(0xE2, 0xDF, 0xD4, 0xFF),   // 1
        new Color32(0x56, 0xB0, 0x68, 0xFF),   // 2
        new Color32(0x56, 0x8C, 0xD6, 0xFF),   // 3
        new Color32(0x98, 0x6C, 0xD0, 0xFF),   // 4
        new Color32(0xE2, 0xBA, 0x56, 0xFF),   // 5
    };

    int _tier = -1;

    /// <summary>按费用档刷新边带（cost 夹到 0-5）。费用由模板 baseCost 决定，与 currentCost 无关。</summary>
    public void ApplyTier(int cost)
    {
        int c = Mathf.Clamp(cost, 0, 5);
        if (c == _tier) return;

        if (useTint)
        {
            Sprite tint = edgeTintSprite != null ? edgeTintSprite : LoadSprite("Cards/Frame/Frame_Edge_Tint");
            if (tint != null) SetEdgeSprite(tint);
            Color col = edgeColors != null && c < edgeColors.Length ? edgeColors[c] : Color.white;
            if (edge != null) edge.color = col;
            if (edgeSR != null) edgeSR.color = col;
        }
        else
        {
            Sprite s = edgeSprites != null && c < edgeSprites.Length ? edgeSprites[c] : null;
            if (s == null) s = LoadSprite(string.Format(edgePath, c));
            if (s != null) SetEdgeSprite(s);
        }

        _tier = c;
    }

    /// <summary>强制下次 ApplyTier 重刷（换模板 / 换图后用）。</summary>
    public void Invalidate() { _tier = -1; }

    void SetEdgeSprite(Sprite s)
    {
        if (edge != null) { edge.sprite = s; edge.enabled = true; }
        if (edgeSR != null) { edgeSR.sprite = s; edgeSR.enabled = true; }
    }

    static Sprite LoadSprite(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        Sprite s = Resources.Load<Sprite>(path);
#if UNITY_EDITOR
        if (s == null)
            s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Resources/" + path + ".png");
#endif
        return s;
    }
}
