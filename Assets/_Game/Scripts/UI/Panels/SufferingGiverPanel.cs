using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SufferingGiverPanel : MonoBehaviour
{
    public static SufferingGiverPanel Instance { get; private set; }

    [Header("UI组件")]
    public GameObject panelRoot;
    public TextMeshProUGUI titleText;
    public Button[] traitButtons;
    public TextMeshProUGUI[] traitTexts;

    private Action<string> onTraitSelected;
    private List<string> currentTraits;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        panelRoot.SetActive(false);
    }

    public void Show(List<string> traits, Action<string> onSelected)
    {
        currentTraits = traits;
        onTraitSelected = onSelected;

        titleText.text = "选择给予特性";

        // 隐藏所有按钮
        for (int i = 0; i < traitButtons.Length; i++)
        {
            if (i < traits.Count)
            {
                traitButtons[i].gameObject.SetActive(true);
                traitTexts[i].text = traits[i];
                int index = i;
                traitButtons[i].onClick.RemoveAllListeners();
                traitButtons[i].onClick.AddListener(() => OnTraitClicked(index));
            }
            else
            {
                traitButtons[i].gameObject.SetActive(false);
            }
        }

        panelRoot.SetActive(true);
        panelRoot.transform.SetAsLastSibling();
    }

    void OnTraitClicked(int index)
    {
        if (index < currentTraits.Count)
        {
            string selected = currentTraits[index];
            panelRoot.SetActive(false);
            onTraitSelected?.Invoke(selected);
        }
    }

    public void Hide()
    {
        panelRoot.SetActive(false);
    }
}