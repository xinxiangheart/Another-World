using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏介绍弹窗 — 只读滚动展示。
/// </summary>
public class GameIntroPanel : MonoBehaviour
{
    [Header("UI")]
    public GameObject panelRoot;
    public ScrollRect scrollRect;
    public Button closeButton;

    private CanvasGroup _canvasGroup;

    void Start()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (panelRoot != null)
        {
            _canvasGroup = panelRoot.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = panelRoot.AddComponent<CanvasGroup>();

            // 全屏透明遮罩：拦截射线 + 遮挡下层按钮
            var mask = panelRoot.GetComponent<Image>();
            if (mask == null)
            {
                mask = panelRoot.AddComponent<Image>();
                mask.color = new Color(0, 0, 0, 0);
                mask.raycastTarget = true;
            }

            panelRoot.SetActive(false);
        }
    }

    public void Open()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }
            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    public void Close()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }
}
