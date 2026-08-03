using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BoardManager : MonoBehaviour
{
    public static int attachGen = 0;
    public static System.Collections.Generic.HashSet<string> removedAttachIDs = new System.Collections.Generic.HashSet<string>();

    /// <summary>记录并移除附着模型（权威删除）。过期同步不会重建此实例。</summary>
    public static void RecordAndRemoveAttach(GameObject attach)
    {
        if (attach == null) return;
        string iid = attach.GetComponent<Card3DInstance>()?.cardInstance?.instanceID;
        if (!string.IsNullOrEmpty(iid)) removedAttachIDs.Add(iid);
        Object.Destroy(attach);
    }

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
    /// <summary>返回槽位对面半场的起止索引</summary>
    public static void GetEnemySideRange(int slotID, out int start, out int end)
    {
        start = (slotID >= 6) ? 0 : 6;
        end = start + 5;
    }
    /// <summary>返回槽位所在半场的起止索引 (0-5 或 6-11)</summary>
    public static void GetSideRange(int slotID, out int start, out int end)
    {
        start = (slotID >= 6) ? 6 : 0;
        end = start + 5;
    }
    /// <summary>返回 CardInstance 所在半场的起止索引</summary>
    public static bool GetSideRangeOf(CardInstance ci, out int start, out int end)
    {
        start = end = -1;
        if (ci == null) return false;
        var bm = FindObjectOfType<BoardManager>();
        if (bm == null) return false;
        for (int i = 0; i < 12; i++)
        {
            if (bm.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == ci)
            { GetSideRange(i, out start, out end); return true; }
        }
        return false;
    }

    /// <summary>返回指定槽位所属的玩家（true=己方/6-11半场, false=对方/0-5半场）。
    /// 在服务端和客户端语义一致：6-11永远是"本地玩家"的半场。</summary>
    public static bool IsAllySide(int slotID) => slotID >= 6;

    /// <summary>返回指定槽位的所有者 NetworkPlayer（6-11→Local, 0-5→Remote）。
    /// 非联网模式下 slotID 无效时返回 Local。</summary>
    public static NetworkPlayer GetOwnerPlayer(int slotID)
    {
        if (slotID < 0 || slotID >= 12) return NetworkPlayer.Local;
        return IsAllySide(slotID) ? NetworkPlayer.Local : NetworkPlayer.Remote;
    }

    /// <summary>返回指定槽位的对手 NetworkPlayer（6-11→Remote, 0-5→Local）。</summary>
    public static NetworkPlayer GetOpponentPlayer(int slotID)
    {
        if (slotID < 0 || slotID >= 12) return NetworkPlayer.Remote;
        return IsAllySide(slotID) ? NetworkPlayer.Remote : NetworkPlayer.Local;
    }

    /// <summary>遍历对方半场的所有槽位，对每个有卡槽位执行 action。
    /// 自动根据 slotID 判断对方是 0-5 还是 6-11。</summary>
    public static void ForEachEnemySlot(int mySlotID, System.Action<BoardSlot> action)
    {
        var bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;
        GetEnemySideRange(mySlotID, out int start, out int end);
        for (int i = start; i <= end; i++)
        {
            BoardSlot s = bm.GetSlot(i);
            if (s?.currentCard3D != null) action(s);
        }
    }

    /// <summary>遍历己方半场的所有槽位，对每个有卡槽位执行 action。</summary>
    public static void ForEachAllySlot(int mySlotID, System.Action<BoardSlot> action)
    {
        var bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;
        GetSideRange(mySlotID, out int start, out int end);
        for (int i = start; i <= end; i++)
        {
            BoardSlot s = bm.GetSlot(i);
            if (s?.currentCard3D != null) action(s);
        }
    }

    /// <summary>对方半场是否有任何召唤物。</summary>
    public static bool HasEnemyMinion(int mySlotID)
    {
        var bm = FindObjectOfType<BoardManager>();
        if (bm == null) return false;
        GetEnemySideRange(mySlotID, out int start, out int end);
        for (int i = start; i <= end; i++)
            if (bm.GetSlot(i)?.currentCard3D != null) return true;
        return false;
    }

    /// <summary>己方半场是否有除 excludeSlotID 外的其他召唤物。</summary>
    public static bool HasAllyMinionExcept(int mySlotID, int excludeSlotID = -1)
    {
        var bm = FindObjectOfType<BoardManager>();
        if (bm == null) return false;
        GetSideRange(mySlotID, out int start, out int end);
        for (int i = start; i <= end; i++)
            if (i != excludeSlotID && bm.GetSlot(i)?.currentCard3D != null) return true;
        return false;
    }

    /// <summary>
    /// Atomically swap the card GameObjects between two board slots.
    /// No intermediate null state — safe to call during sync or network processing.
    /// Handles: currentCard3D/hasCard swap, transform reposition, _placedAtTime refresh,
    /// and attached model hostSlotID re-targeting.
    /// Both slots must be on the same half (both 0-5 or both 6-11).
    /// </summary>
    public static void SwapCards(int slotA, int slotB)
    {
        if (slotA == slotB) return;
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;
        BoardSlot slotObjA = bm.GetSlot(slotA);
        BoardSlot slotObjB = bm.GetSlot(slotB);
        if (slotObjA == null || slotObjB == null) return;

        GameObject cardA = slotObjA.currentCard3D;
        GameObject cardB = slotObjB.currentCard3D;

        Vector3 posA = FindObjectOfType<HandManager>().GetSlotWorldPosition(slotA);
        Vector3 posB = FindObjectOfType<HandManager>().GetSlotWorldPosition(slotB);

        // Atomic reference swap — never exposes null to sync/network observers
        slotObjA.currentCard3D = cardB;
        slotObjA.hasCard = cardB != null;
        slotObjB.currentCard3D = cardA;
        slotObjB.hasCard = cardA != null;

        float now = Time.time;
        if (cardA != null)
        {
            cardA.transform.position = posB;
            var ci = cardA.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null) ci._placedAtTime = now;
        }
        if (cardB != null)
        {
            cardB.transform.position = posA;
            var ci = cardB.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null) ci._placedAtTime = now;
        }

        // Swap attached model hostSlotIDs
        for (int i = bm.attachedModels.Count - 1; i >= 0; i--)
        {
            var am = bm.attachedModels[i];
            if (am == null) { bm.attachedModels.RemoveAt(i); continue; }
            var aci = am.GetComponent<Card3DInstance>()?.cardInstance;
            if (aci != null && aci.isAttached)
            {
                if (aci.hostSlotID == slotA) aci.hostSlotID = slotB;
                else if (aci.hostSlotID == slotB) aci.hostSlotID = slotA;
            }
        }

        SyncAttachedModels(slotObjA);
        SyncAttachedModels(slotObjB);
    }

    public static void SyncAttachedModels(BoardSlot slot)
    {
        if (slot == null) return;
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;

        Vector3 hostPos = FindObjectOfType<HandManager>().GetSlotWorldPosition(slot.slotID);

        // 优先取实卡 3D 世界坐标做宿主参考（与 PlaceAttachedCard 同级）
        if (slot.currentCard3D != null)
            hostPos = slot.currentCard3D.transform.position;

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