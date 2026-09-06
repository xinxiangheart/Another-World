using UnityEditor;
using UnityEngine;

/// <summary>
/// 一键修复全部 CardData 的"表现层（预制体引用）"字段：
///   - 2D 召唤物(card2DPrefab)   → Card00_New_2D        （仅归档；运行时手牌仍走 Player.cardPrefab2D）
///   - 2D 法术(spell2DPrefab)    → SpellCard00_New_2D    （仅归档；运行时手牌仍走 Player.spellCardPrefab2D）
///   - 3D 召唤物(prefab3D)       → Card00_New_3D（旧 Card00_3D 一并换新）
///   - 3D 法术(spellPrefab3D)    → SpellCard00_3D（无新 3D 法术，保持）
/// 法术的 prefab3D 归空（该槽属召唤物场上模型）。
/// 菜单：Tools → 卡牌 → 修复CardData表现层预制体引用。跑完 Editor 自动保存。
/// </summary>
public static class CardDataPrefabMigrator
{
    const string Prefab2DSummon = "Assets/_Game/Prefabs/Cards/Summon/Card00_New_2D.prefab";
    const string Prefab2DSpell = "Assets/_Game/Prefabs/Cards/Spell/SpellCard00_New_2D.prefab";
    const string Prefab3DSummon = "Assets/_Game/Prefabs/Cards/Summon/Card00_New_3D.prefab";
    const string Prefab3DSpell = "Assets/_Game/Prefabs/Cards/Spell/SpellCard00_3D.prefab";

    [MenuItem("Tools/卡牌/修复CardData表现层预制体引用")]
    public static void FixAll()
    {
        GameObject p2dS = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab2DSummon);
        GameObject p2dSp = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab2DSpell);
        GameObject p3dS = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab3DSummon);
        GameObject p3dSp = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab3DSpell);
        if (p2dS == null) { Debug.LogError($"[CardData] 缺 2D 召唤物预制体: {Prefab2DSummon}"); return; }
        if (p2dSp == null) { Debug.LogError($"[CardData] 缺 2D 法术预制体: {Prefab2DSpell}"); return; }
        if (p3dS == null) { Debug.LogError($"[CardData] 缺 3D 召唤物预制体: {Prefab3DSummon}"); return; }
        if (p3dSp == null) { Debug.LogError($"[CardData] 缺 3D 法术预制体: {Prefab3DSpell}"); return; }

        int n = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:CardData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CardData cd = AssetDatabase.LoadAssetAtPath<CardData>(path);
            if (cd == null) continue;
            bool spell = cd.cardType == CardType.Spell;
            cd.card2DPrefab = spell ? null : p2dS;
            cd.spell2DPrefab = spell ? p2dSp : null;
            if (spell)
            {
                cd.prefab3D = null;            // 3D 召唤槽法术不用
                cd.spellPrefab3D = p3dSp;
            }
            else
            {
                cd.prefab3D = p3dS;            // 召唤全部换新 3D
            }
            EditorUtility.SetDirty(cd);
            n++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[CardData] 表现层预制体引用已修复: {n} 个资产");
    }
}
