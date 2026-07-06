using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GenericChoicePanel : MonoBehaviour
{
    public static GenericChoicePanel Instance { get; private set; }

    [Header("UI组件")]
    public GameObject panelRoot;
    public TextMeshProUGUI titleText;
    public Button[] optionButtons;
    public TextMeshProUGUI[] optionTexts;

    private Action<int> onSelected;
    private int selectedIndex = -1;
    private Color normalColor = Color.white;
    private Color selectedColor = Color.yellow;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        panelRoot.SetActive(false);
    }

    /// <summary>
    /// 显示通用选择面板
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="options">选项列表（最多3个）</param>
    /// <param name="onSelected">选中回调，参数为选项索引</param>
    public void Show(string title, List<string> options, Action<int> onSelected)
    {
        this.onSelected = onSelected;
        selectedIndex = -1;
        titleText.text = title;

        for (int i = 0; i < optionButtons.Length; i++)
        {
            if (i < options.Count)
            {
                optionButtons[i].gameObject.SetActive(true);
                optionTexts[i].text = options[i];
                optionButtons[i].GetComponent<Image>().color = normalColor;
                int index = i;
                optionButtons[i].onClick.RemoveAllListeners();
                optionButtons[i].onClick.AddListener(() => OnOptionClicked(index));
            }
            else
            {
                optionButtons[i].gameObject.SetActive(false);
            }
        }

        panelRoot.SetActive(true);
        panelRoot.transform.SetAsLastSibling();
    }

    void OnOptionClicked(int index)
    {
        // 取消旧选中
        if (selectedIndex >= 0)
            optionButtons[selectedIndex].GetComponent<Image>().color = normalColor;

        selectedIndex = index;
        optionButtons[index].GetComponent<Image>().color = selectedColor;

        // 选中后立即回调（也可以加确认按钮）
        onSelected?.Invoke(index);
        Hide();
    }

    public void Hide()
    {
        panelRoot.SetActive(false);
    }
}