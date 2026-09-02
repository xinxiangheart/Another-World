using UnityEngine;

// ============================================================================
// HoverTagConfig — 3D 召唤物悬停标签配置 (ScriptableObject)。
//
// 作用：TagLabel.prefab 位于 Assets/_Game/Prefabs/UI/Panels（不在 Resources 下，
// 无法 Resources.Load 按路径取），由本配置资产持有其引用。
// 运行时 Resources.Load<HoverTagConfig>("Config/HoverTagConfig") → tagLabelPrefab。
// 由 HoverTagPrefabBuilder（Tools/卡牌/生成悬停标签预制体）自动生成/更新。
// ============================================================================

[CreateAssetMenu(menuName = "Another World/Hover Tag Config", fileName = "HoverTagConfig")]
public class HoverTagConfig : ScriptableObject
{
    [Tooltip("悬停标签预制体（TagLabel.prefab，Prefabs/UI/Panels）")]
    public GameObject tagLabelPrefab;
}
