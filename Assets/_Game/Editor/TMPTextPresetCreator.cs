using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 一键生成 4 类共享 TMP 文字材质预设（卡面 / UI / 描述 / 备用）。
/// 底 = NotoSerifCJKsc-Regular SDF（中文正文），拉丁缺字走字体 Fallback 的 Liberation。
/// 用法：菜单 Tools/卡牌/生成 TMP 四类文字预设，输出到 Assets/_Game/Materials/TMP/。
/// 只生成资产，不改任何预制体；之后按需把 TMP 组件换成对应 fontSharedMaterial。
/// </summary>
public static class TMPTextPresetCreator
{
    const string FontRel = "Assets/_Game/Fonts/NotoSerifCJKsc-Regular SDF.asset";
    const string OutDir = "Assets/_Game/Materials/TMP";

    [MenuItem("Tools/卡牌/生成 TMP 四类文字预设")]
    public static void CreatePresets()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontRel);
        if (font == null)
        {
            Debug.LogError($"[TMPPreset] 找不到字体 {FontRel}");
            return;
        }
        Directory.CreateDirectory(OutDir); // Ensure folder exists before CreateAsset

        CreateOne(font, "TMP_CardFace_Regular", ConfigureCardFace);
        CreateOne(font, "TMP_UI_Regular",       ConfigureUI);
        CreateOne(font, "TMP_Desc_Regular",     ConfigureDesc);
        CreateOne(font, "TMP_Reserved_Regular", ConfigureReserved);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TMPPreset] 已生成 4 个 TMP 文字预设 → " + OutDir);
    }

    static void CreateOne(TMP_FontAsset font, string name, System.Action<Material> cfg)
    {
        // 以字体自带的 SDF 材质为模板复制（保留图集/着色器/字重预设），生成独立共享材质
        var src = font.material;
        if (src == null)
        {
            Debug.LogError($"[TMPPreset] {font.name} 无 material");
            return;
        }
        var mat = new Material(src);
        cfg?.Invoke(mat);

        string path = $"{OutDir}/{name}.mat";
        AssetDatabase.CreateAsset(mat, path);
        AssetDatabase.SaveAssets();
        Debug.Log($"[TMPPreset] 生成 {path}");
    }

    // ---- 各类差异化（均先读默认值再改，缺属性跳过）----

    /// <summary>卡面文字：高对比、细深色描边，抗卡图压底。</summary>
    static void ConfigureCardFace(Material m)
    {
        SetF(m, "_FaceDilate", 0f);
        SetC(m, "_FaceColor", new Color(1f, 1f, 1f, 1f));
        // 描边（细、深），确保 shader 开启 Outline 位（_ShaderFlags bit0）
        SetC(m, "_OutlineColor", new Color(0.05f, 0.05f, 0.08f, 1f));
        SetF(m, "_OutlineWidth", 0.16f);
        SetF(m, "_OutlineSoftness", 0f);
        OrFlag(m, 1); // Outline
    }

    /// <summary>UI 文字：干净清晰，无描边/无投影。</summary>
    static void ConfigureUI(Material m)
    {
        SetC(m, "_FaceColor", new Color(1f, 1f, 1f, 1f));
        SetF(m, "_OutlineWidth", 0f);
        SetF(m, "_UnderlayOffsetX", 0f);
        SetF(m, "_UnderlayOffsetY", 0f);
        SetF(m, "_UnderlaySoftness", 0f);
        ClearFlags(m);
    }

    /// <summary>描述文字：正文可读，关发光，留轻微淡投影（UI 详情可用）。</summary>
    static void ConfigureDesc(Material m)
    {
        SetC(m, "_FaceColor", new Color(1f, 1f, 1f, 1f));
        SetF(m, "_OutlineWidth", 0f);
        SetC(m, "_UnderlayColor", new Color(0f, 0f, 0f, 0.45f));
        SetF(m, "_UnderlayOffsetX", 0f);
        SetF(m, "_UnderlayOffsetY", 1.2f);
        SetF(m, "_UnderlaySoftness", 0.35f);
        ClearFlags(m);
        OrFlag(m, 2); // Underlay
    }

    /// <summary>备用：保持字体默认底座，仅作为未来扩展的干净模板。</summary>
    static void ConfigureReserved(Material m)
    {
        ClearFlags(m);
    }

    static void SetF(Material m, string p, float v)
    {
        if (m.HasProperty(p)) m.SetFloat(p, v);
    }
    static void SetC(Material m, string p, Color c)
    {
        if (m.HasProperty(p)) m.SetColor(p, c);
    }
    /// <summary>按位打开 shader 效果（Outline=1, Underlay=2）。</summary>
    static void OrFlag(Material m, int bit)
    {
        const string flag = "_ShaderFlags";
        if (!m.HasProperty(flag)) return;
        int v = Mathf.RoundToInt(m.GetFloat(flag));
        v |= bit;
        m.SetFloat(flag, v);
    }
    static void ClearFlags(Material m)
    {
        const string flag = "_ShaderFlags";
        if (m.HasProperty(flag)) m.SetFloat(flag, 0f);
    }
}
