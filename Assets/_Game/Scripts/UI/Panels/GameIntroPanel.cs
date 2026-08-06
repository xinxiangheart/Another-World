using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 游戏介绍弹窗 — 只读滚动展示。
/// 内容在 Unity Editor 中直接作为 Content 的子对象预编写，运行时仅显示。
/// 每个子对象可以是：
///   - TMP_Text   → 文字段落（支持富文本：<b>粗体</b> <i>斜体</i> <color=red>颜色</color> <size=20>大小</size>）
///   - Image      → 图片
/// </summary>
public class GameIntroPanel : MonoBehaviour
{
    [Header("UI")]
    public GameObject panelRoot;
    public ScrollRect scrollRect;
    public Button closeButton;

    void Start()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    public void Open()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
            // 每次打开回到顶部
            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    public void Close()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }
}
