using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 切换 Canvas Scaler Reference Resolution 后，一键批量缩放所有 UI 元素的
/// 位置、尺寸、字号、边距、布局间距、格子大小等。
/// 支持 Ctrl+Z 撤销。
/// </summary>
public class ScaleCanvasTool : EditorWindow
{
    [MenuItem("Tools/缩放 UI 布局 (全面)")]
    public static void ShowWindow()
    {
        GetWindow<ScaleCanvasTool>("全面缩放 UI");
    }

    private float scaleX = 3f;
    private float scaleY = 3f;
    private float fontSizeScale = 3f;

    void OnGUI()
    {
        GUILayout.Label("缩放 Canvas 下所有 UI（位置/尺寸/字号/布局/格子）", EditorStyles.boldLabel);
        GUILayout.Space(10);

        GUILayout.Label("位置 & 尺寸倍率", EditorStyles.boldLabel);
        scaleX = EditorGUILayout.FloatField("  X 倍率", scaleX);
        scaleY = EditorGUILayout.FloatField("  Y 倍率", scaleY);
        GUILayout.Space(5);
        GUILayout.Label("字号倍率", EditorStyles.boldLabel);
        fontSizeScale = EditorGUILayout.FloatField("  字号 ×", fontSizeScale);

        GUILayout.Space(10);
        GUILayout.Label("常用: 640×360 → 1920×1080 全部填 3", EditorStyles.helpBox);
        GUILayout.Label("如需只缩字号不改位置: X/Y 填 1", EditorStyles.helpBox);

        if (GUILayout.Button("执行全面缩放", GUILayout.Height(35)))
        {
            if (EditorUtility.DisplayDialog("确认全面缩放",
                $"将对 Canvas 下所有 UI 执行：\n\n" +
                $"  位置 / 尺寸  ×({scaleX}, {scaleY})\n" +
                $"  字号 / 字体   ×{fontSizeScale}\n" +
                $"  布局间距       ×({scaleX}, {scaleY})\n" +
                $"  格子大小       ×({scaleX}, {scaleY})\n" +
                $"  外边距         ×({scaleX}, {scaleY})\n\n" +
                "此操作可撤销 (Ctrl+Z)。",
                "确定", "取消"))
            {
                ScaleAll();
            }
        }
    }

    void ScaleAll()
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        int rCount = 0, fCount = 0, lCount = 0, gCount = 0;

        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            foreach (var rt in canvas.GetComponentsInChildren<RectTransform>(true))
            {
                // ===== RectTransform =====
                Undo.RecordObject(rt, "Scale UI");
                rt.anchoredPosition = new Vector2(
                    rt.anchoredPosition.x * scaleX,
                    rt.anchoredPosition.y * scaleY);
                rt.sizeDelta = new Vector2(
                    rt.sizeDelta.x * scaleX,
                    rt.sizeDelta.y * scaleY);
                rCount++;

                // ===== TMP_Text =====
                var tmp = rt.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    Undo.RecordObject(tmp, "Scale Font");
                    tmp.fontSize *= fontSizeScale;
                    tmp.fontSizeMin *= fontSizeScale;
                    tmp.fontSizeMax *= fontSizeScale;
                    tmp.margin = new Vector4(
                        tmp.margin.x * scaleX,
                        tmp.margin.y * scaleY,
                        tmp.margin.z * scaleX,
                        tmp.margin.w * scaleY);

                    // SerializedObject for fontSizeBase (not exposed in API)
                    var so = new SerializedObject(tmp);
                    var baseProp = so.FindProperty("m_fontSizeBase");
                    if (baseProp != null && baseProp.floatValue > 0)
                    {
                        baseProp.floatValue *= fontSizeScale;
                        so.ApplyModifiedProperties();
                    }
                    fCount++;
                }

                // ===== Legacy Text =====
                var leg = rt.GetComponent<Text>();
                if (leg != null)
                {
                    Undo.RecordObject(leg, "Scale Font");
                    leg.fontSize = Mathf.RoundToInt(leg.fontSize * fontSizeScale);
                    fCount++;
                }

                // ===== LayoutElement =====
                var le = rt.GetComponent<LayoutElement>();
                if (le != null)
                {
                    Undo.RecordObject(le, "Scale Layout");
                    if (le.minWidth > 0) le.minWidth *= scaleX;
                    if (le.minHeight > 0) le.minHeight *= scaleY;
                    if (le.preferredWidth > 0) le.preferredWidth *= scaleX;
                    if (le.preferredHeight > 0) le.preferredHeight *= scaleY;
                    if (le.flexibleWidth > 0) le.flexibleWidth *= scaleX;
                    if (le.flexibleHeight > 0) le.flexibleHeight *= scaleY;
                    lCount++;
                }

                // ===== HorizontalLayoutGroup =====
                var hlg = rt.GetComponent<HorizontalLayoutGroup>();
                if (hlg != null)
                {
                    Undo.RecordObject(hlg, "Scale Layout");
                    hlg.spacing *= scaleX;
                    hlg.padding = new RectOffset(
                        Mathf.RoundToInt(hlg.padding.left * scaleX),
                        Mathf.RoundToInt(hlg.padding.right * scaleX),
                        Mathf.RoundToInt(hlg.padding.top * scaleY),
                        Mathf.RoundToInt(hlg.padding.bottom * scaleY));
                }

                // ===== VerticalLayoutGroup =====
                var vlg = rt.GetComponent<VerticalLayoutGroup>();
                if (vlg != null)
                {
                    Undo.RecordObject(vlg, "Scale Layout");
                    vlg.spacing *= scaleY;
                    vlg.padding = new RectOffset(
                        Mathf.RoundToInt(vlg.padding.left * scaleX),
                        Mathf.RoundToInt(vlg.padding.right * scaleX),
                        Mathf.RoundToInt(vlg.padding.top * scaleY),
                        Mathf.RoundToInt(vlg.padding.bottom * scaleY));
                }

                // ===== GridLayoutGroup =====
                var glg = rt.GetComponent<GridLayoutGroup>();
                if (glg != null)
                {
                    Undo.RecordObject(glg, "Scale Layout");
                    glg.cellSize = new Vector2(
                        glg.cellSize.x * scaleX,
                        glg.cellSize.y * scaleY);
                    glg.spacing = new Vector2(
                        glg.spacing.x * scaleX,
                        glg.spacing.y * scaleY);
                    glg.padding = new RectOffset(
                        Mathf.RoundToInt(glg.padding.left * scaleX),
                        Mathf.RoundToInt(glg.padding.right * scaleX),
                        Mathf.RoundToInt(glg.padding.top * scaleY),
                        Mathf.RoundToInt(glg.padding.bottom * scaleY));
                    gCount++;
                }

                // ===== ScrollRect (movement speed) =====
                var sr = rt.GetComponent<ScrollRect>();
                if (sr != null)
                {
                    Undo.RecordObject(sr, "Scale Scroll");
                    sr.scrollSensitivity *= scaleY;
                }
            }
        }

        Undo.CollapseUndoOperations(group);
        Undo.SetCurrentGroupName("全面缩放 UI");

        Debug.Log($"[全面缩放] RectTransform:{rCount} | 字号:{fCount} | LayoutElement:{lCount} | GridLayout:{gCount}");
    }
}
