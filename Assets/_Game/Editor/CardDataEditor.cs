using UnityEngine;
using UnityEditor;

/// <summary>
/// CardData 的 Inspector 定制：勾选 hasBuff / hasDebuff 才显示对应的描述文本输入框。
/// （Unity 原生 Inspector 不支持条件显示，这里用 CustomEditor 实现。）
/// 只影响编辑期显示，不影响运行时与打包。
/// </summary>
[CustomEditor(typeof(CardData))]
public class CardDataEditor : Editor
{
    SerializedProperty _hasBuff;
    SerializedProperty _buffText;
    SerializedProperty _hasDebuff;
    SerializedProperty _debuffText;

    void OnEnable()
    {
        _hasBuff   = serializedObject.FindProperty("hasBuff");
        _buffText  = serializedObject.FindProperty("buffText");
        _hasDebuff = serializedObject.FindProperty("hasDebuff");
        _debuffText = serializedObject.FindProperty("debuffText");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 其余字段按默认布局绘制（排除这四个，手动处理）
        DrawPropertiesExcluding(serializedObject, "hasBuff", "buffText", "hasDebuff", "debuffText");

        // Buff/Debuff 区（勾选才显示文本）
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Buff/Debuff 持续状态", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_hasBuff, new GUIContent("Has Buff", "是否有正面持续增益（护盾不算；仅持续型效果）"));
        if (_hasBuff.boolValue)
            EditorGUILayout.PropertyField(_buffText, new GUIContent("Buff Text", "正面增益描述文本"));
        EditorGUILayout.PropertyField(_hasDebuff, new GUIContent("Has Debuff", "是否有负面持续减益（护盾不算；仅持续型效果）"));
        if (_hasDebuff.boolValue)
            EditorGUILayout.PropertyField(_debuffText, new GUIContent("Debuff Text", "负面减益描述文本"));

        serializedObject.ApplyModifiedProperties();
    }
}
