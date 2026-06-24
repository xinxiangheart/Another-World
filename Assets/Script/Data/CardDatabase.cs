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
        CardData[] templates = Resources.LoadAll<CardData>(folderName);
        foreach (CardData data in templates)
        {
            if (!templateDict.ContainsKey(data.templateID))
                templateDict[data.templateID] = data;
        }
    }

    // 通过模板ID获取模板数据
    public CardData GetTemplate(string templateID)
    {
        templateDict.TryGetValue(templateID, out CardData data);
        if (data == null)
            Debug.LogWarning($"CardDatabase：未找到模板 {templateID}");
        return data;
    }
}