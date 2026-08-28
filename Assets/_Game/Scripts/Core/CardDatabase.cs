using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardDatabase : MonoBehaviour
{
    public static CardDatabase Instance { get; private set; }

    // 模板字典：模板ID -> CardData 模板
    private Dictionary<string, CardData> templateDict = new Dictionary<string, CardData>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        LoadTemplates("CardData");
        LoadTemplates("ChosenOneData");
        Debug.Log($"CardDatabase 加载完成，共 {templateDict.Count} 张模板");
    }

    void LoadTemplates(string folderName)
    {
        // 优先从 Preloader 缓存读取（Lobby场景已预加载），未命中回退到同步 LoadAll
        CardData[] templates = Preloader.Instance != null
            ? Preloader.Instance.GetAll<CardData>(folderName)
            : null;
        if (templates == null || templates.Length == 0)
            templates = Resources.LoadAll<CardData>(folderName);
        foreach (CardData data in templates)
        {
            if (!templateDict.ContainsKey(data.templateID))
                templateDict[data.templateID] = data;
        }
    }

    // 通过模板ID获取模板数据
    public CardData GetTemplate(string templateID)
    {
        if (string.IsNullOrEmpty(templateID)) return null; // 空 key 直接返回，避免 Dictionary.TryGetValue(null) 抛异常
        templateDict.TryGetValue(templateID, out CardData data);
        if (data == null)
            Debug.LogWarning($"CardDatabase：未找到模板 {templateID}");
        return data;
    }
}