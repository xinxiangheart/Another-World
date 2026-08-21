using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// 一键生成回合字幕条带结构，作为 CardCanvas 的子组件：
///   CardCanvas（Game 场景现有）
///     └─ SubtitleBand（Image 灰带，全屏 X，Y/高度可调，挂 SubtitleBand 组件）
///          ├─ Mode1Text   （位置 = 公共锚点）
///          ├─ Mode2Top    （己方回合）
///          └─ Mode2Bottom （第X阶段）
/// 菜单：Tools → 字幕条带 → 生成到当前场景
/// 生成后请在 Scene/Inspector 中手动调整条带 Y/高度、Mode1Text(=锚点)、Mode2 停留位。
/// </summary>
public static class SubtitleBandSetup
{
    static TMP_FontAsset _font;

    [MenuItem("Tools/字幕条带/生成到当前场景")]
    public static void CreateSubtitleBand()
    {
        var existing = Object.FindObjectOfType<SubtitleBand>(true);
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            Debug.Log($"[SubtitleBand] 场景中已存在 {existing.name}，已选中，无需重复生成");
            return;
        }

        // 找 CardCanvas（子组件挂到这里）
        Canvas cardCanvas = FindCardCanvas();
        if (cardCanvas == null)
        {
            Debug.LogError("[SubtitleBand] 当前场景找不到名为 CardCanvas 的 Canvas，无法生成。请打开 Game.unity");
            return;
        }

        _font = FindChineseFont();

        // ── 条带：全屏 X，Y=画布中心（可在 Inspector 调 anchor/height）──
        var bandGO = new GameObject("SubtitleBand", typeof(RectTransform), typeof(Image), typeof(SubtitleBand));
        bandGO.transform.SetParent(cardCanvas.transform, false);
        bandGO.transform.SetAsLastSibling(); // 置于 CardCanvas 子级最上层显示
        var bandRT = bandGO.GetComponent<RectTransform>();
        bandRT.anchorMin = new Vector2(0f, 0.5f);
        bandRT.anchorMax = new Vector2(1f, 0.5f);
        bandRT.pivot = new Vector2(0.5f, 0.5f);
        bandRT.sizeDelta = new Vector2(0f, 90f);
        var bandImage = bandGO.GetComponent<Image>();
        bandImage.color = new Color(0.5f, 0.5f, 0.5f, 0.6f); // 与 SubtitleBand.bandColor 默认一致

        var comp = bandGO.GetComponent<SubtitleBand>();
        comp.mode1Text = CreateText(bandGO.transform, "Mode1Text", "对方回合", new Vector2(0, 0), 36);
        comp.mode2Top = CreateText(bandGO.transform, "Mode2Top", "己方回合", new Vector2(0, 35), 36);
        comp.mode2Bottom = CreateText(bandGO.transform, "Mode2Bottom", "第1阶段", new Vector2(0, -35), 30);

        Selection.activeGameObject = bandGO;
        EditorSceneManager.MarkSceneDirty(bandGO.scene);
        Debug.Log("[SubtitleBand] 已生成到 CardCanvas 下。请在 Scene 中调整：条带 Y/高度、Mode1Text(=公共锚点)、Mode2 停留位");
    }

    /// <summary>在 CardCanvas 下查找名为 CardCanvas 的 Canvas（忽略大小写）。</summary>
    static Canvas FindCardCanvas()
    {
        foreach (var c in Object.FindObjectsOfType<Canvas>(true))
        {
            if (c.name.Equals("CardCanvas", System.StringComparison.OrdinalIgnoreCase))
                return c;
            // 兜底：父物体名为 CardCanvas 的（有的 Canvas 在子物体上）
            if (c.transform.parent != null &&
                c.transform.parent.name.Equals("CardCanvas", System.StringComparison.OrdinalIgnoreCase))
                return c;
        }
        return null;
    }

    static TextMeshProUGUI CreateText(Transform parent, string name, string content, Vector2 anchoredPos, float fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.font = _font;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(500f, 64f);
        rt.anchoredPosition = anchoredPos;
        return text;
    }

    /// <summary>复用场景中已有 TMP 文字使用的字体（保证中文正常显示），兜底 TMP 默认字体。</summary>
    static TMP_FontAsset FindChineseFont()
    {
        var anyText = Object.FindObjectOfType<TextMeshProUGUI>(true);
        if (anyText != null && anyText.font != null) return anyText.font;
        return TMP_Settings.defaultFontAsset;
    }
}
