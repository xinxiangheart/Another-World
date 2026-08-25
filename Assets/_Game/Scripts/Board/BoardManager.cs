using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BoardManager : MonoBehaviour
{
    public static int attachGen = 0;
    public static System.Collections.Generic.HashSet<string> removedAttachIDs = new System.Collections.Generic.HashSet<string>();
    public static System.Collections.Generic.HashSet<string> removedAttachKeys = new System.Collections.Generic.HashSet<string>();

    /// <summary>记录并移除附着模型。用 (tid,hostSlot,order) 匹配跨端同步，用 instanceID 兜底。</summary>
    public static void RecordAndRemoveAttach(GameObject attach)
    {
        if (attach == null) return;
        var ci = attach.GetComponent<Card3DInstance>()?.cardInstance;
        if (ci != null)
        {
            string iid = ci.instanceID;
            if (!string.IsNullOrEmpty(iid)) removedAttachIDs.Add(iid);
            removedAttachKeys.Add($"{ci.templateID}|{ci.hostSlotID}|{ci.attachOrder}");
        }
        Object.Destroy(attach);
    }

    [Header("槽位预制体")]
    public GameObject slotPrefab;

    [Header("敌方前排")]
    public Vector2 enemyFrontRight = new Vector2(109, 45.5f);
    public Vector2 enemyFrontCenter = new Vector2(0, 45.5f);
    public Vector2 enemyFrontLeft = new Vector2(-109, 45.5f);

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
    private HandManager handManager;
    // 槽位 Z 坐标：相机 z=-16.22 看向 +Z，越负越靠近相机。
    // 由近到远：卡牌 -6（最近）→ 槽位 -5.6 → 棋盘贴图面 -5.5（最远）。
    private const float SLOT_Z = -5.6f;
    // 附着物列表（不占槽位，用于全局查找）
    public List<GameObject> attachedModels = new List<GameObject>();
    void Start()
    {
        handManager = FindObjectOfType<HandManager>();

        // World Space Canvas：槽位标记贴在棋盘表面（棋盘贴图面 z=-6，槽位略前 z=-5.9）
        GameObject slotCanvasObj = new GameObject("SlotCanvas");
        // 关键：World Space Canvas 必须独立在场景根，绝不能 SetParent 到 Screen Space 主 Canvas，
        // 否则会继承主 Canvas 的 scale（CanvasScaler 计算的 ~0.003），把槽位缩小约 333 倍。
        slotCanvasObj.transform.SetParent(null);

        RectTransform rt = slotCanvasObj.AddComponent<RectTransform>();
        rt.position = new Vector3(0f, 1f, SLOT_Z);
        // 关键：UI Canvas 必须正面朝向相机（默认 identity 朝向 +Z），不要旋转 180°。
        // 旋转 180° 会让 Canvas 背对相机，GraphicRaycaster 的射线检测因背面剔除而失败 → 悬停不变色。
        rt.rotation = Quaternion.identity;
        rt.sizeDelta = new Vector2(19.2f, 10.8f);      // 世界单位（独立 Canvas，1 UI 单位 = 1 世界单位）
        rt.localScale = Vector3.one;

        Canvas slotCanvas = slotCanvasObj.AddComponent<Canvas>();
        slotCanvas.renderMode = RenderMode.WorldSpace;
        slotCanvas.worldCamera = Camera.main;
        // 槽位排序低于主 Canvas(sortingOrder=5)：手牌等主 UI 渲染在槽位上面（不遮挡手牌）。
        // 原来嵌套 SlotCanvas 就是 sortingOrder=-1，沿用该值保证 raycast 优先级低于主 UI。
        slotCanvas.overrideSorting = true;
        slotCanvas.sortingOrder = 0;

        GraphicRaycaster raycaster = slotCanvasObj.AddComponent<GraphicRaycaster>();
        // 槽位 GraphicRaycaster.blockingObjects 默认即为 None（不阻挡 3D 卡牌）——
        // 选择模式下由 SelectionManager 的 3D 射线穿透路径（Physics.RaycastAll）负责点击选中槽位。
        // eventCamera 是只读属性，自动从 Canvas.worldCamera 解析（上方已设 Camera.main）

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

        // 槽位显示尺寸（世界单位）：与未迁移前一致。
        // 未迁移前 Slot_0.prefab sizeDelta=135×240，在 Screen Space(1080px=10世界单位) 下 = 1.25×2.22 世界单位。
        rt.sizeDelta = new Vector2(1.25f, 2.22f);

        // 世界坐标定位：与卡牌同用 GetSlotWorldPosition 的 X/Y（保证对齐），Z 贴在棋盘表面
        Vector3 worldPos = handManager != null ? handManager.GetSlotWorldPosition(slotID) : Vector3.zero;
        worldPos.z = SLOT_Z;
        rt.position = worldPos;

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

        if (cardA == null || cardB == null)
            Debug.LogWarning($"[SwapCards] {slotA}(card3D={(cardA != null)}) <-> {slotB}(card3D={(cardB != null)}) 有模型为空——可能选了空槽位，模型未移动");
        else
            Debug.Log($"[SwapCards] 移动模型: {slotA}->{posB}，{slotB}->{posA}");

        // Atomic reference swap — never exposes null to sync/network observers
        slotObjA.currentCard3D = cardB;
        slotObjA.hasCard = cardB != null;
        slotObjB.currentCard3D = cardA;
        slotObjB.hasCard = cardA != null;

        float now = Time.time;
        if (cardA != null)
        {
            cardA.transform.position = posB;
            // 漂浮动画基准位置重捕——否则动画每帧把模型拉回原槽位，视觉上不移动
            cardA.GetComponent<Card3DAnimator>()?.UpdateBaseLocalPos();
            var ci = cardA.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null) ci._placedAtTime = now;
        }
        if (cardB != null)
        {
            cardB.transform.position = posA;
            cardB.GetComponent<Card3DAnimator>()?.UpdateBaseLocalPos();
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