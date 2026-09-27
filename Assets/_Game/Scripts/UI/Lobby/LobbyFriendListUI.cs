using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 好友侧边栏里的名单（挂在 Panel_Friends/Body/List/Viewport/Content 上）。
/// 数据来自 <see cref="FriendListService"/>（Steam 好友 ∩ 玩过本游戏 ∪ 手动加的游戏内好友）。
/// 行从场景里那份 inactive 的 RowTemplate 克隆；容器自带 ScrollRect，超过一屏可以滚（2026-09-27 用户：「加滚动」）。
/// 空表时显示「暂无好友」（沿用原来那句 Text_Empty）。
/// </summary>
public class LobbyFriendListUI : MonoBehaviour
{
    [Header("行模板（场景里 inactive 的那一行）")] public FriendRowUI rowTemplate;
    [Header("空表时那句「暂无好友」")] public TMP_Text emptyText;
    [Tooltip("外层滚动框（Content 的祖父级那个 List）—— 重排后用它把位置拉回顶部")] public ScrollRect scrollRect;

    readonly List<FriendRowUI> _rows = new List<FriendRowUI>();
    float _rowHeight = 84f;
    int _shownCount = -1;
    bool _subscribed;

    void OnEnable()
    {
        if (rowTemplate != null)
        {
            var rt = rowTemplate.transform as RectTransform;
            if (rt != null && rt.sizeDelta.y > 1f) _rowHeight = rt.sizeDelta.y;
            rowTemplate.Bind(null);          // 模板自己永远藏着
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
        IList<FriendEntry> entries = FriendListService.Instance != null ? FriendListService.Instance.Entries : null;
        int want = entries != null ? entries.Count : 0;

        while (_rows.Count < want) _rows.Add(CloneRow());

        for (int i = 0; i < _rows.Count; i++)
        {
            FriendRowUI row = _rows[i];
            if (row == null) continue;

            bool used = i < want;
            if (!used) { row.gameObject.SetActive(false); continue; }

            var rt = row.transform as RectTransform;
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(0f, -_rowHeight * i);
                rt.SetSiblingIndex(i);
            }
            row.Bind(entries[i]);
        }

        var crt = transform as RectTransform;
        if (crt != null) crt.sizeDelta = new Vector2(crt.sizeDelta.x, _rowHeight * want);

        if (emptyText != null) emptyText.text = want == 0 ? "暂无好友" : "";

        // 只有「行数变了」才回顶：20 秒重扫会把整份名单重排一遍，但玩家正看到一半的位置不该被顶走
        if (want != _shownCount) { _shownCount = want; ScrollToTop(); }
    }

    /// <summary>
    /// 把名单拉回顶部。
    /// ⚠ 不能只写 <c>verticalNormalizedPosition = 1</c>：ScrollRect 拿**上一帧缓存**的 content 边界换算，
    /// 刚改完高度（上面那行）时它会算出一个中间位置 —— 2026-09-27 实测 14 行时停在 0.37 处。
    /// 所以先让 ScrollRect 把内部状态切回顶部，再把 content 位置**直接**写 0；
    /// 下一帧 ScrollRect 用新高度做钳制，而 0（顶）在新范围内一定合法，不会被弹走。
    /// </summary>
    void ScrollToTop()
    {
        var crt = transform as RectTransform;
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
        if (crt != null) crt.anchoredPosition = new Vector2(crt.anchoredPosition.x, 0f);
    }

    FriendRowUI CloneRow()
    {
        GameObject go = Instantiate(rowTemplate.gameObject, transform);
        go.name = "Row_" + _rows.Count.ToString("00");
        return go.GetComponent<FriendRowUI>();
    }
}
