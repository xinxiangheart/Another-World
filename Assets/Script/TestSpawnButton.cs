using UnityEngine;
using UnityEngine.UI;

public class TestSpawnButton : MonoBehaviour
{
    private Button button;

    void Start()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(SpawnTestMinion);
    }

    void SpawnTestMinion()
    {
        CardData data = CardDatabase.Instance?.GetTemplate("00000");
        if (data?.prefab3D == null) return;

        BoardManager bm = FindObjectOfType<BoardManager>();
        BoardSlot slot = bm?.GetSlot(1);
        if (slot == null || slot.hasCard) return;

        HandManager hm = FindObjectOfType<HandManager>();
        Vector3 pos = hm.GetSlotWorldPosition(slot.slotID);
        GameObject model = Instantiate(data.prefab3D, pos, Quaternion.Euler(0, 180, 0));

        Card3DInstance inst = model.GetComponent<Card3DInstance>();
        if (inst != null)
        {
            inst.cardInstance = model.AddComponent<CardInstance>();
            inst.cardInstance.currentAttack = data.baseAttack;
            inst.cardInstance.baseAttack = data.baseAttack;
            inst.cardInstance.currentHealth = data.baseHealth;
            inst.cardInstance.currentMaxHealth = data.baseHealth;
            inst.cardInstance.currentCost = data.baseCost;
            inst.cardInstance.currentTier = data.baseTier;
            inst.cardInstance.templateID = data.templateID;
            inst.cardInstance.prefixes = data.prefix;
            inst.UpdateValues();
        }

        slot.SetCard(model);
        Debug.Log($"测试召唤物 {data.cardName} 生成在敌方槽位 1");
    }
}