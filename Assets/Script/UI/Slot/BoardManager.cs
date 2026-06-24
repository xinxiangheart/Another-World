using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BoardManager : MonoBehaviour
{
    [Header("槽位预制体")]
    public GameObject slotPrefab;

    [Header("敌方前排")]
    public Vector2 enemyFrontRight = new Vector2(109, 146f);
    public Vector2 enemyFrontCenter = new Vector2(0, 146f);
    public Vector2 enemyFrontLeft = new Vector2(-109, 146f);

    [Header("敌方后排")]
    public Vector2 enemyBackRight = new Vector2(109, 129.3f);
    public Vector2 enemyBackCenter = new Vector2(0, 129.3f);
    public Vector2 enemyBackLeft = new Vector2(-109, 129.3f);

    [Header("己方前排")]
    public Vector2 myFrontRight = new Vector2(109, -46f);
    public Vector2 myFrontCenter = new Vector2(0, -46f);
    public Vector2 myFrontLeft = new Vector2(-109, -46f);

    [Header("己方后排")]
    public Vector2 myBackRight = new Vector2(109, -129.8f);
    public Vector2 myBackCenter = new Vector2(0, -129.8f);
    public Vector2 myBackLeft = new Vector2(-109, -129.8f);

    private BoardSlot[] allSlots = new BoardSlot[12];
    private Transform slotCanvasTransform;
    // 附着物列表（不占槽位，用于全局查找）
    public List<GameObject> attachedModels = new List<GameObject>();
    void Start()
    {
        Canvas parentCanvas = GetComponent<Canvas>();

        GameObject slotCanvasObj = new GameObject("SlotCanvas");
        slotCanvasObj.transform.SetParent(transform);
        slotCanvasObj.transform.SetAsFirstSibling(); // 排最前面 = 渲染最底层

        RectTransform rt = slotCanvasObj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.localPosition = Vector3.zero;
        rt.localScale = Vector3.one;

        Canvas slotCanvas = slotCanvasObj.AddComponent<Canvas>();
        slotCanvas.overrideSorting = true;
        slotCanvas.sortingOrder = -1;

        GraphicRaycaster raycaster = slotCanvasObj.AddComponent<GraphicRaycaster>();

        slotCanvasTransform = slotCanvasObj.transform;
        GenerateSlots();
    }


    void GenerateSlots()
    {
        // 敌方前排 0-2
        CreateSlot(0, enemyFrontRight, 6);
        CreateSlot(1, enemyFrontCenter, 7);
        CreateSlot(2, enemyFrontLeft, 8);

        // 敌方后排 3-5
        CreateSlot(3, enemyBackRight, 9);
        CreateSlot(4, enemyBackCenter, 10);
        CreateSlot(5, enemyBackLeft, 11);

        // 己方前排 6-8
        CreateSlot(6, myFrontRight, 0);
        CreateSlot(7, myFrontCenter, 1);
        CreateSlot(8, myFrontLeft, 2);

        // 己方后排 9-11
        CreateSlot(9, myBackRight, 3);
        CreateSlot(10, myBackCenter, 4);
        CreateSlot(11, myBackLeft, 5);
    }

    void CreateSlot(int slotID, Vector2 pos, int opponentID)
    {
        GameObject slotObj = Instantiate(slotPrefab, slotCanvasTransform);
        RectTransform rt = slotObj.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;

        BoardSlot slot = slotObj.GetComponent<BoardSlot>();
        slot.slotID = slotID;
        slot.opponentSlotID = opponentID;
        slot.currentCard3D = null;
        allSlots[slotID] = slot;
    }

    public BoardSlot GetSlot(int id)
    {
        if (id >= 0 && id < 12) return allSlots[id];
        return null;
    }
    public BoardSlot[] GetAllSlots()
    {
        return allSlots;
    }
    public static void SyncAttachedModels(BoardSlot slot)
    {
        if (slot == null) return;
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;

        Vector3 hostPos = FindObjectOfType<HandManager>().GetSlotWorldPosition(slot.slotID);

        List<GameObject> attached = new List<GameObject>();
        foreach (GameObject obj in bm.attachedModels)
        {
            if (obj == null || obj.transform == null) continue;
            CardInstance ci = obj.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.isAttached && ci.hostSlotID == slot.slotID)
                attached.Add(obj);
        }

        attached.RemoveAll(a => a == null || a.transform == null);

        attached.Sort((a, b) =>
        {
            int orderA = a.GetComponent<Card3DInstance>()?.cardInstance?.attachOrder ?? 0;
            int orderB = b.GetComponent<Card3DInstance>()?.cardInstance?.attachOrder ?? 0;
            return orderA.CompareTo(orderB);
        });

        for (int i = 0; i < attached.Count; i++)
        {
            if (attached[i] == null || attached[i].transform == null) continue;
            Vector3 newPos = new Vector3(hostPos.x - 0.5f - i * 0.5f, hostPos.y, hostPos.z + 0.1f + i * 0.1f);
            attached[i].transform.position = newPos;
        }
    }
}