using UnityEngine;
using UnityEditor;

/// <summary>
/// CardData 的 Inspector 定制：
/// ① 特性条目结构化编辑（特性数量驱动 N 行，每行 text + 8 属性勾选，勾选"赋予"=给予型不显示不编号）
/// ② 检测旧 traits 字符串 → 一键迁移到结构化 traitEntries
/// ③ 勾选 hasBuff / hasDebuff 才显示对应描述文本
/// 只影响编辑期显示，不影响运行时与打包。
/// </summary>
[CustomEditor(typeof(CardData))]
public class CardDataEditor : Editor
{
    SerializedProperty _hasBuff;
    SerializedProperty _buffText;
    SerializedProperty _hasDebuff;
    SerializedProperty _debuffText;
    SerializedProperty _traitEntries;

    void OnEnable()
    {
        _hasBuff   = serializedObject.FindProperty("hasBuff");
        _buffText  = serializedObject.FindProperty("buffText");
        _hasDebuff = serializedObject.FindProperty("hasDebuff");
        _debuffText = serializedObject.FindProperty("debuffText");
        _traitEntries = serializedObject.FindProperty("traitEntries");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 其余字段按默认布局绘制（排除特性/旧字符串/BuffDebuff 四字段，手动处理）
        DrawPropertiesExcluding(serializedObject,
            "traitEntries", "traits", "traitProperties",
            "hasBuff", "buffText", "hasDebuff", "debuffText");

        DrawTraitSection();
        DrawBuffDebuffSection();

        serializedObject.ApplyModifiedProperties();
    }

    // ================= 特性条目 =================

    void DrawTraitSection()
    {
        var data = (CardData)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("特性条目", EditorStyles.boldLabel);

        // 旧 traits 一键迁移（traitEntries 空 且 旧字符串有内容）
        if ((data.traitEntries == null || data.traitEntries.Count == 0)
            && !string.IsNullOrEmpty(data.traits) && data.traits != "无")
        {
            EditorGUILayout.HelpBox("检测到旧 traits 字符串，可一键迁移为结构化条目。", MessageType.Info);
            if (GUILayout.Button("从 traits 字符串迁移"))
            {
                data.traitEntries = data.GetTraitEntryList();
                EditorUtility.SetDirty(data);
                serializedObject.Update();
            }
            // 只读显示旧字符串，便于核对
            EditorGUILayout.LabelField("旧 traits 字符串（只读）：", EditorStyles.miniLabel);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextArea(data.traits);
            EditorGUI.EndDisabledGroup();
        }

        // 特性数量（= list.arraySize），改动自动增删行
        int count = _traitEntries.arraySize;
        int newCount = EditorGUILayout.IntField("特性数量", count);
        if (newCount < 0) newCount = 0;
        if (newCount != count)
        {
            _traitEntries.arraySize = newCount;
            for (int i = count; i < newCount; i++)
            {
                var el = _traitEntries.GetArrayElementAtIndex(i);
                var textProp = el.FindPropertyRelative("text");
                if (textProp != null && string.IsNullOrEmpty(textProp.stringValue))
                    textProp.stringValue = "新特性";
            }
        }

        for (int i = 0; i < _traitEntries.arraySize; i++)
            DrawTraitEntry(_traitEntries.GetArrayElementAtIndex(i), i + 1);
    }

    void DrawTraitEntry(SerializedProperty entry, int index)
    {
        EditorGUILayout.BeginVertical("box");
        var textProp = entry.FindPropertyRelative("text");
        EditorGUILayout.PropertyField(textProp, new GUIContent($"特性 {index}", "特性文本（勾选\"赋予\"则自身详情面板不显示、不参与编号）"));

        EditorGUILayout.BeginHorizontal();
        DrawToggle(entry, "isEnter", "进场");
        DrawToggle(entry, "isFirstStrike", "先手");
        DrawToggle(entry, "isRevenge", "反击");
        DrawToggle(entry, "isDeath", "退场");
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        DrawToggle(entry, "isActiveExit", "主动退场");
        DrawToggle(entry, "isDiscard", "抛置");
        DrawToggle(entry, "isAttach", "附着");
        DrawToggle(entry, "isGrant", "赋予");
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    void DrawToggle(SerializedProperty entry, string field, string label)
    {
        var prop = entry.FindPropertyRelative(field);
        if (prop == null) return;
        bool v = prop.boolValue;
        bool nv = EditorGUILayout.ToggleLeft(label, v, GUILayout.Width(80));
        if (nv != v) prop.boolValue = nv;
    }

    // ================= Buff/Debuff =================

    void DrawBuffDebuffSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Buff/Debuff 持续状态", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_hasBuff, new GUIContent("Has Buff", "是否有正面持续增益（护盾不算；仅持续型效果）"));
        if (_hasBuff.boolValue)
            EditorGUILayout.PropertyField(_buffText, new GUIContent("Buff Text", "正面增益描述文本"));
        EditorGUILayout.PropertyField(_hasDebuff, new GUIContent("Has Debuff", "是否有负面持续减益（护盾不算；仅持续型效果）"));
        if (_hasDebuff.boolValue)
            EditorGUILayout.PropertyField(_debuffText, new GUIContent("Debuff Text", "负面减益描述文本"));
    }
}
