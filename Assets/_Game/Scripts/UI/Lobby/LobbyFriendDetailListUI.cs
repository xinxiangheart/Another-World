using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「好友详情 → 好友列表」右侧那块里的名单（挂在大格子里的 ScrollRect Content 上）。
/// </summary>
/// <remarks>2026-09-27 用户：「好友列表……（上限50个好友），允许滑动」。
///
/// 数据与好友侧边栏同一份：<see cref="FriendListService.Entries"/>（Steam 好友 ∩ 玩过本游戏 ∪ 手动加的游戏内好友，
/// 已排序）。行从场景里那份 inactive 的 RowTemplate 克隆；超过一屏可以滚。
/// 上限 <see cref="maxFriends"/> = 50，右下角那行小字显示「实际 / 50」。
///
/// 与侧边栏那份（<see cref="LobbyFriendListUI"/>）只有三处不同：① 有上限；② 右下角有计数；③ 行距固定为一档间隙。
/// 同样的坑也照抄了：**改完 content 高度不能只写 verticalNormalizedPosition = 1**（ScrollRect 拿上一帧缓存的
/// content 边界换算，会停在中间），要再把 content 的 anchoredPosition 直接写 0。
/// </remarks>
public class LobbyFriendDetailListUI : MonoBehaviour
{
    [Header("行模板（场景里 inactive 的那一行）")] public FriendDetailRowUI rowTemplate;
    [Header("外层滚动框（Content 的祖父级那个）")] public ScrollRect scrollRect;
    [Header("右下角「实际 / 上限」那行小字")] public TMP_Text countText;
    [Header("空表时那句「暂无好友」")] public TMP_Text emptyText;

    [Header("上限（用户：上限 50 个好友）")] public int maxFriends = 50;
    [Tooltip("行与行之间那一点间隙（屏幕 px）")] public float rowGap = 12f;

    readonly List<FriendDetailRowUI> _rows = new List<FriendDetailRowUI>();
    float _rowHeight = 96f;
    int _shownCount = -1;
    bool _subscribed;

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
        if (on)
        {
            if (_subscribed || svc == null) return;
            svc.Refreshed += Rebuild;
            _subscribed = true;
        }
        else
        {
            if (!_subscribed) return;
            if (svc != null) svc.Refreshed -= Rebuild;
            _subscribed = false;
        }
    }

    /// <summary>按最新名单重排：不够就克隆、多了就藏、内容高度跟着行数走（ScrollRect 靠它算可滚范围）。</summary>
    public void Rebuild()
    {
        FriendListService svc = FriendListService.Instance;
        IList<FriendEntry> entries = svc != null ? svc.Entries : null;
        int total = entries != null ? entries.Count : 0;
        int want = Mathf.Min(total, Mathf.Max(1, maxFriends));

        while (_rows.Count < want) _rows.Add(CloneRow());

        float pitch = _rowHeight + rowGap;
        for (int i = 0; i < _rows.Count; i++)
        {
            FriendDetailRowUI row = _rows[i];
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

        // 右下角那行：实际 / 上限（用户举的例子就是「23/50」这种小字）
        if (countText != null) countText.text = want + "/" + Mathf.Max(1, maxFriends);
        if (emptyText != null) emptyText.text = want == 0 ? "暂无好友" : "";

        // 只有「行数变了」才回顶：20 秒重扫会把整份名单重排一遍，玩家正看到一半的位置不该被顶走
        if (want != _shownCount) { _shownCount = want; ScrollToTop(); }
    }

    /// <summary>
    /// 把名单拉回顶部。⚠ 不能只写 verticalNormalizedPosition = 1：ScrollRect 拿**上一帧缓存**的 content 边界换算，
    /// 刚改完高度时它会算出一个中间位置（侧边栏那边实测 14 行时停在 0.37）。所以先把 ScrollRect 的内部状态切回顶部，
    /// 再把 content 位置直接写 0 —— 下一帧 ScrollRect 用新高度钳制，0（顶）在新范围内一定合法。
    /// </summary>
    void ScrollToTop()
    {
        var crt = transform as RectTransform;
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
        if (crt != null) crt.anchoredPosition = new Vector2(crt.anchoredPosition.x, 0f);
    }

    FriendDetailRowUI CloneRow()
    {
        GameObject go = Instantiate(rowTemplate.gameObject, transform);
        go.name = "Row_" + _rows.Count.ToString("00");
        go.SetActive(true);
        return go.GetComponent<FriendDetailRowUI>();
    }
}