using UnityEngine;

/// <summary>大厅里那张「预览卡」：Instantiate 卡面预制体 → 剥掉战斗件 → 按模板初始化 → 画一次卡面。</summary>
/// <remarks>2026-09-28：「卡牌总览」的网格与「卡牌详情」左半区那张放大卡走**同一条路** —— 抽出来免得两处各写一遍
/// （原来这段写在 <c>LobbyCardCollectionPanel.PlaceCard</c> 里）。
///
/// 剥掉的三个件都是战斗用的：<c>CardView</c>（压暗 / 选中态，图鉴不需要）、<c>CardDrag</c>（拖拽）、
/// <c>CardHover</c>（悬停自动弹战斗那份 Test1Panel 详情）。图鉴自己的悬停 / 点击由
/// <see cref="LobbyCardTile"/> 管。</remarks>
public static class LobbyCardPreview
{
    /// <summary>卡面预制体的**自然尺寸**（<c>Card00_New_2D.prefab</c> 根 sizeDelta = 83.33 x 146.33）。
    /// ⚠ 必须按自然尺寸铺：写成 64x84 会把卡框压扁（比例 0.76 vs 0.57），卡图与金线一起变形。</summary>
    public const float CardW = 83f;
    public const float CardH = 146f;

    /// <summary>建一张预览卡并摆成「锚左上 / 轴心居中」，位置留给调用方写（<c>anchoredPosition</c>）。
    /// 建不出来（模板没挂卡面预制体）返回 null。</summary>
    public static GameObject Create(CardData data, Transform parent, float scale)
    {
        if (data == null || parent == null) return null;

        GameObject prefab = data.cardType == CardType.Spell ? data.spell2DPrefab : data.card2DPrefab;
        if (prefab == null) return null;

        GameObject go = Object.Instantiate(prefab, parent);
        go.name = "CardFace_" + data.templateID;

        var view = go.GetComponent<CardView>(); if (view != null) view.enabled = false;
        var drag = go.GetComponent<CardDrag>(); if (drag != null) drag.enabled = false;
        var hover = go.GetComponent<CardHover>(); if (hover != null) hover.enabled = false;

        var inst = go.GetComponent<CardInstance>();
        if (inst == null) inst = go.AddComponent<CardInstance>();
        inst.InitFromTemplate(data, 0);

        // 优先用新的显示脚本；老预制体（只有基类 CardDisplay2D）才退到那条老路。
        // ⚠ 别写成 GetComponent<CardDisplay2D>() != null 就 Destroy —— 新脚本继承自它，
        //   那条会把新脚本自己删掉（GetComponent 会连派生类一起命中）。
        var dispNew = go.GetComponent<CardDisplay2DNew>();
        if (dispNew != null) dispNew.RefreshWithInstance(inst);
        else { var dispOld = go.GetComponent<CardDisplay2D>(); if (dispOld != null) dispOld.RefreshWithInstance(inst); }

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(CardW, CardH);
        rt.localScale = Vector3.one * scale;
        return go;
    }
}
