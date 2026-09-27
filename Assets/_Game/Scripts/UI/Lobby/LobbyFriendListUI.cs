using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 好友侧边栏里的名单（挂在 Panel_Friends/Body/List 上）。
/// 数据来自 <see cref="FriendListService"/>（Steam 好友 ∩ 玩过本游戏 ∪ 手动加的游戏内好友）；
/// 行从场景里那份 inactive 的 RowTemplate 克隆。空表时显示「暂无好友」（沿用原来那句 Text_Empty）。
/// </summary>
public class LobbyFriendListUI : MonoBehaviour
{
    [Header("行模板（场景里 inactive 的那一行）")] public FriendRowUI rowTemplate;
    [Header("空表时那句「暂无好友」")] public TMP_Text emptyText;

    [Header("调试：塞三条假数据看版式（真名单要 Steam 起来、且好友里有本游戏玩家）")]
    public bool debugSampleRows;

    readonly List<FriendRowUI> _rows = new List<FriendRowUI>();
    float _rowHeight = 84f;
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

    /// <summary>按最新名单重排：不够就克隆、多了就藏。</summary>
    public void Rebuild()
    {
        IList<FriendEntry> entries = debugSampleRows
            ? Samples()
            : (FriendListService.Instance != null ? FriendListService.Instance.Entries : null);
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

        if (emptyText != null) emptyText.text = want == 0 ? "暂无好友" : "";
    }

    FriendRowUI CloneRow()
    {
        GameObject go = Instantiate(rowTemplate.gameObject, transform);
        go.name = "Row_" + _rows.Count.ToString("00");
        return go.GetComponent<FriendRowUI>();
    }

    /// <summary>三条假数据：真 Steam 头像 + 正在玩本游戏 / 游戏内好友 / 离线（只给调试看版式用）。</summary>
    IList<FriendEntry> Samples()
    {
        var l = new List<FriendEntry>();

        var a = new FriendEntry();
        a.SteamId = LobbyConfig.LocalSteamID;          // 借本机 SteamID 拿一张真头像
        a.steamFriend = true;
        a.playedOurGame = true;
        a.evidence = "sample";
        string myName = SteamDataManager.Instance != null ? SteamDataManager.Instance.localPlayerName : null;
        a.name = string.IsNullOrEmpty(myName) ? "样例玩家" : myName;
        a.Presence = FriendPresence.PlayingOurGame;
        l.Add(a);

        var b = new FriendEntry();
        b.playerId = "P00-20260927-9-8K3M7QX";
        b.name = "只有异界号的朋友";
        b.manual = true;
        b.Presence = FriendPresence.InGameOnly;
        l.Add(b);

        var c = new FriendEntry();
        c.name = "很久没上线的人";
        c.Presence = FriendPresence.Offline;
        l.Add(c);

        return l;
    }
}
