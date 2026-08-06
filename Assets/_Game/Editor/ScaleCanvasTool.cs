using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

/// <summary>
/// 批量缩放当前打开场景中所有 RectTransform 的位置和尺寸。
/// 用于切换 Canvas Scaler Reference Resolution 后一次适配。
/// </summary>
public class ScaleCanvasTool : EditorWindow
{
    [MenuItem("Tools/缩放 UI 布局 (RectTransform)")]
    public static void ShowWindow()
    {
        GetWindow<ScaleCanvasTool>("缩放 UI");
    }

    private Vector2 scale = new Vector2(3f, 3f);

    void OnGUI()
    {
        GUILayout.Label("缩放所有 RectTransform 的坐标和尺寸", EditorStyles.boldLabel);
        GUILayout.Space(10);

        scale.x = EditorGUILayout.FloatField("X 倍率", scale.x);
        scale.y = EditorGUILayout.FloatField("Y 倍率", scale.y);

        GUILayout.Space(10);
        GUILayout.Label("常用倍率：640×360 → 1920×1080 填 3, 3", EditorStyles.helpBox);

        if (GUILayout.Button("执行缩放", GUILayout.Height(30)))
        {
            ScaleAll();
        }
    }

    void ScaleAll()
    {
        Undo.RecordObjects(
            GameObject.FindObjectsByType<RectTransform>(FindObjectsSortMode.None),
            "Scale Canvas Layout");

        int count = 0;
        foreach (var rt in GameObject.FindObjectsByType<RectTransform>(FindObjectsSortMode.None))
        {
            // 只处理 Canvas 下的 RectTransform（排除了非 UI 对象）
            if (rt.GetComponentInParent<Canvas>() == null) continue;

            Undo.RecordObject(rt, "Scale Canvas Layout");
            rt.anchoredPosition = new Vector2(
                rt.anchoredPosition.x * scale.x,
                rt.anchoredPosition.y * scale.y);
            rt.sizeDelta = new Vector2(
                rt.sizeDelta.x * scale.x,
                rt.sizeDelta.y * scale.y);

            count++;
        }

        EditorUtility.SetDirty(rt.gameObject.scene.GetRootGameObjects()[0]);
        Debug.Log($"[ScaleCanvas] 缩放完成：{count} 个 RectTransform ×({scale.x}, {scale.y})");
    }
}
