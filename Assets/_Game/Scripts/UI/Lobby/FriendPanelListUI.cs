using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「好友详情 → 申请列表 / 黑名单」右侧那块里的名单（挂在大格子里的 ScrollRect Content 上）。
/// </summary>
/// <remarks>2026-09-27 用户：「均在右下角类似限制50」。
///
/// 与「好友列表」那份（<see cref="LobbyFriendDetailListUI"/>）是**同一套滚动与排版**，差别只有三处：
///   ① 数据源：申请 = <see cref="FriendRequestStore"/>，黑名单 = <see cref="FriendBlock.List"/>；
///   ② 行是 <see cref="FriendPanelRowUI"/>（右端动作不一样）；
///   ③ 计数写「n/50」（和好友列表同一口径）。
/// 滚动那几个坑照抄（**改完 content 高度不能只写 verticalNormalizedPosition = 1**，要再把 content 的
/// anchoredPosition 直接写 0 —— ScrollRect 拿上一帧缓存的 content 边界换算，会停在中间）。
///
/// 两份都**订阅**各自的数据源：黑名单跟 <see cref="FriendListService.Refreshed"/>（拉黑 / 取消拉黑都写好友表那个文件），
/// 申请跟 <see cref="FriendRequestStore.Changed"/>。申请那份以前是不订阅的（当时没有任何外部来源，切进来重读一遍就够），
/// 从「拉黑顺手清申请」这一版起必须订阅 —— 否则玩家正看着申请列表时进来的新申请（以后是后端收包）不会显示。
/// </remarks>
public class FriendPanelListUI : MonoBehaviour
{
    [Header("这是哪一格（决定数据源与右端动作）")] public FriendPanelMode mode;

    [Header("行模板（场景里 inactive 的那一行）")] public FriendPanelRowUI rowTemplate;
    [Header("外层滚动框（Content 的祖父级那个）")] public ScrollRect scrollRect;
    [Header("右下角「实际 / 上限」那行小字")] public TMP_Text countText;
    [Header("空表时那句（暂无好友申请 / 黑名单是空的）")] public TMP_Text emptyText;

    [Header("上限（用户：类似限制 50）")] public int maxRows = FriendRequestStore.MaxRequests;
    [Tooltip("行与行之间那一点间隙（屏幕 px）")] public float rowGap = 12f;

    readonly List<FriendPanelRowUI> _rows = new List<FriendPanelRowUI>();
    float _rowHeight = 96f;
    int _shownCount = -1;
    bool _subscribed;        // 黑名单那份：跟 FriendListService.Refreshed
    bool _reqSubscribed;     // 申请那份：跟 FriendRequestStore.Changed

    void OnEnable()
    {
        if (!Application.isPlaying) return;      // 编辑态别往场景里克隆行（初始态由构建脚本摆好）

        if (rowTemplate != null)
        {
            var rt = rowTemplate.transform as RectTransform;
            if (rt != null && rt.sizeDelta.y > 1f) _rowHeight = rt.sizeDelta.y;
            rowTemplate.gameObject.SetActive(false);
        }
        Subscribe(true);
        Rebuild();
        ScrollToTop();
    }

    void OnDisable() { Subscribe(false); }

    void Subscribe(bool on)
    {
        FriendListService svc = FriendListService.Instance;

        // 黑名单那份：跟着好友表刷新（拉黑 / 取消拉黑都写好友表那个文件）
        if (on && !_subscribed && mode == FriendPanelMode.Block && svc != null)
        { svc.Refreshed += Rebuild; _subscribed = true; }
        if (!on && _subscribed)
        { if (svc != null) svc.Refreshed -= Rebuild; _subscribed = false; }

        // 申请那份：跟着**申请文件**刷新。以前它只在切进这一格时重建一次（当时没有任何外部来源），
        // 现在外面有两处会改它 —— 拉黑顺手清申请、以及以后**后端收包**。
        // 不订阅的话，「玩家正看着申请列表，这时进来一条新申请」是**不会显示**的。
        if (on && !_reqSubscribed && mode == FriendPanelMode.Request)
        { FriendRequestStore.Changed += Rebuild; _reqSubscribed = true; }
        if (!on && _reqSubscribed)
        { FriendRequestStore.Changed -= Rebuild; _reqSubscribed = false; }
    }

    /// <summary>按最新名单重排：不够就克隆、多了就藏、内容高度跟着行数走（ScrollRect 靠它算可滚范围）。</summary>
    public void Rebuild()
    {
        IList<FriendEntry> entries = mode == FriendPanelMode.Block ? FriendBlock.List() : FriendRequestStore.Load();
        int total = entries != null ? entries.Count : 0;
        int want = Mathf.Min(total, Mathf.Max(1, maxRows));

        while (_rows.Count < want) _rows.Add(CloneRow());

        float pitch = _rowHeight + rowGap;
        for (int i = 0; i < _rows.Count; i++)
        {
            FriendPanelRowUI row = _rows[i];
            if (row == null) continue;

            bool used = i < want;
            if (!used) { row.gameObject.SetActive(false); continue; }

            var rt = row.transform as RectTransform;
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(0f, -pitch * i);
                rt.SetSiblingIndex(i);
            }
            row.Bind(entries[i]);
        }

        var crt = transform as RectTransform;
        if (crt != null)
        {
            float h = want > 0 ? pitch * want - rowGap : 0f;
            crt.sizeDelta = new Vector2(crt.sizeDelta.x, h);
        }

        if (countText != null) countText.text = want + "/" + Mathf.Max(1, maxRows);
        if (emptyText != null) emptyText.text = want == 0 ? EmptyLabel() : "";

        // 只有「行数变了」才回顶：别把玩家正看到一半的位置顶走
        if (want != _shownCount) { _shownCount = want; ScrollToTop(); }
    }

    string EmptyLabel()
    {
        return mode == FriendPanelMode.Block ? "黑名单是空的" : "暂无好友申请";
    }

    /// <summary>
    /// 把名单拉回顶部。⚠ 不能只写 verticalNormalizedPosition = 1：ScrollRect 拿**上一帧缓存**的 content 边界换算，
    /// 刚改完高度时它会算出一个中间位置。所以先把 ScrollRect 的内部状态切回顶部，再把 content 位置直接写 0。
    /// </summary>
    void ScrollToTop()
    {
        var crt = transform as RectTransform;
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
        if (crt != null) crt.anchoredPosition = new Vector2(crt.anchoredPosition.x, 0f);
    }

    FriendPanelRowUI CloneRow()
    {
        GameObject go = Instantiate(rowTemplate.gameObject, transform);
        go.name = "Row_" + _rows.Count.ToString("00");
        go.SetActive(true);
        return go.GetComponent<FriendPanelRowUI>();
    }
}
