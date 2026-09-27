using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「好友详情 → 添加好友」那一格的控制器（挂在那块大格子上）：输入框 + 右端放大镜 + 下方可滚的结果名单。
/// </summary>
/// <remarks>2026-09-27 用户：「添加好友，首先是一个长的输入框（框的最右边有个类似于放大镜的ui），输入框可以之间
/// 输入id或者昵称（加入右键粘贴id功能），然后会在下方列出所有相关玩家（一般是输入昵称时可以有多个玩家）
/// （也是能滑动的）」。
///
/// 分工：
///   搜索规则 / 加好友全在 <see cref="FriendAddSearch"/>（纯静态，不碰场景）；
///   本件只管三件事 —— **什么时候搜**（输入变化防抖 / 点放大镜 / 右键粘贴）、**把结果铺成行**、**空态与计数**。
///
/// 行是照 <see cref="LobbyFriendDetailListUI"/> 那一套克隆的：模板在场景里存 inactive，运行时克隆 + 复用；
/// 改完 content 高度不能只写 verticalNormalizedPosition = 1（ScrollRect 拿上一帧缓存的 content 边界换算，
/// 会停在中间），要再把 content 的 anchoredPosition 直接写 0 —— 同一个坑。
///
/// 与好友列表那份的差别只有一条：**每次搜完都回顶**。好友表那边 20 秒重扫一次、玩家可能正看一半，
/// 所以只在行数变了才回顶；这里是玩家自己敲出来的新结果，回顶才是对的。
/// </remarks>
public class FriendAddSearchUI : MonoBehaviour
{
    [Header("输入框（那口井）")] public TMP_InputField input;
    [Header("外层滚动框（Content 的祖父级那个）")] public ScrollRect scrollRect;
    [Header("结果容器（= ScrollRect 的 Content）")] public RectTransform resultContent;
    [Header("行模板（场景里 inactive 的那一行）")] public FriendAddRowUI rowTemplate;
    [Header("右下角那句「n 位相关玩家」")] public TMP_Text countText;
    [Header("列表正中那句（待输入 / 没找到）")] public TMP_Text emptyText;

    [Tooltip("输入停下多久才去搜（秒）—— 昵称是一路敲的，每敲一个字就搜会闪")]
    public float debounce = 0.25f;

    [Tooltip("行距（屏幕 px），与好友列表同一档")]
    public float rowGap = 12f;

    [Tooltip("最多列几行（与好友表上限同一个数）")]
    public int maxResults = 50;

    const string IdleHint = "输入异界号或昵称";
    const string NoResultHint = "没有找到相关玩家";

    readonly List<FriendAddRowUI> _rows = new List<FriendAddRowUI>();
    float _rowHeight = 96f;
    float _dueAt = -1f;          // < 0 = 没有待办的搜索

    void OnEnable()
    {
        if (!Application.isPlaying) return;      // 编辑态别往场景里克隆行（初始态由构建脚本摆好）

        if (rowTemplate != null)
        {
            var rt = rowTemplate.transform as RectTransform;
            if (rt != null && rt.sizeDelta.y > 1f) _rowHeight = rt.sizeDelta.y;
            rowTemplate.gameObject.SetActive(false);
        }
        RunSearch();
    }

    void OnDisable() { _dueAt = -1f; }

    void Update()
    {
        if (_dueAt < 0f) return;
        if (Time.unscaledTime < _dueAt) return;
        _dueAt = -1f;
        RunSearch();
    }

    // ===================== 什么时候搜 =====================

    /// <summary>输入框每次变化（场景里那条持久监听接的）—— 只排一个防抖闹钟。</summary>
    public void OnQueryChanged(string value)
    {
        if (!Application.isPlaying) return;
        _dueAt = Time.unscaledTime + Mathf.Max(0.05f, debounce);
    }

    /// <summary>点井右端那枚放大镜：立刻搜（不等防抖）。</summary>
    public void Submit()
    {
        _dueAt = -1f;
        RunSearch();
    }

    /// <summary>清空重来。</summary>
    public void Clear()
    {
        _dueAt = -1f;
        if (input != null) input.SetTextWithoutNotify("");
        RunSearch();
    }

    /// <summary>
    /// 右键 = 把剪贴板里的东西贴进井里（触发见 <see cref="FriendAddInputPaste"/>）。
    /// 剪贴板拿不到 / 是空的：只弹一句提示，**不动输入框**（免得把已打进去的字清掉）。
    /// </summary>
    /// <remarks>剪贴板里常常是「我的异界号：P00-xxxx」这种带前缀的一整句 —— 见 <see cref="ExtractPlayerId"/>：
    /// 能抠出异界号就用抠出来的那串，抠不出就当**昵称**原样贴（用户口径：「输入框可以之间输入 id 或者昵称」）。
    /// 整段替换、不是插到光标处；贴完立刻搜（不等那 0.25 秒）。</remarks>
    public void PasteFromClipboard()
    {
        string raw = null;
        try { raw = GUIUtility.systemCopyBuffer; } catch { }   // 某些平台 / 无剪贴板权限时会抛

        string text = raw == null ? "" : raw.Trim();
        if (text.Length == 0) { LobbyToast.Show("剪贴板里没有内容"); return; }
        if (input == null) return;

        string id = ExtractPlayerId(text);
        if (id != null) text = id;
        else if (input.characterLimit > 0 && text.Length > input.characterLimit)
            text = text.Substring(0, input.characterLimit);

        input.text = text;                  // ⇒ onValueChanged ⇒ OnQueryChanged（排一个闹钟，下面立刻取消掉）
        input.caretPosition = text.Length;  // = selectionAnchor/Focus 一起设：光标收到末尾
        input.ActivateInputField();         // 贴完把焦点留在这口井上，接着能直接改
        Submit();                           // 贴完就搜，不等那 0.25 秒
    }

    /// <summary>从一段文字里抠出一个合法的异界号（抠不出返回 null）。</summary>
    /// <remarks><see cref="PlayerId.Normalize"/> 只留 A-Z / 0-9，并把 I·L 当 1、O 当 0、Z 当 2 ——
    /// 所以「我的号：P00-2026…」这种带中文前缀 / 分隔符的串，归一化之后照样是那 20 个字符、校验位也照样对得上；
    /// 「一句话里夹着号，但前后还有别的东西」这种就用滑动窗兜一层（最多几十次，开销可以忽略）。</remarks>
    public static string ExtractPlayerId(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        if (PlayerId.IsWellFormed(raw)) return PlayerId.Pretty(raw);

        string n = PlayerId.Normalize(raw);
        int need = PlayerId.RawLength;
        for (int i = 0; i + need <= n.Length; i++)
        {
            string win = n.Substring(i, need);
            if (PlayerId.IsWellFormed(win)) return PlayerId.Pretty(win);
        }
        return null;
    }

    // ===================== 搜 + 铺行 =====================

    /// <summary>按输入框现在的内容搜一次、把结果铺出来。加完好友 / 切回这一格都会调它。</summary>
    public void RunSearch()
    {
        string q = input != null ? input.text : "";
        string message;
        List<FriendSearchHit> hits = FriendAddSearch.Run(q, out message);
        Rebuild(hits, q, message);
    }

    void Rebuild(List<FriendSearchHit> hits, string query, string message)
    {
        int want = Mathf.Min(hits.Count, Mathf.Max(1, maxResults));
        while (_rows.Count < want) _rows.Add(CloneRow());

        float pitch = _rowHeight + rowGap;
        for (int i = 0; i < _rows.Count; i++)
        {
            FriendAddRowUI row = _rows[i];
            if (row == null) continue;

            bool used = i < want;
            if (!used) { row.gameObject.SetActive(false); continue; }

            var rt = row.transform as RectTransform;
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(0f, -pitch * i);
                rt.SetSiblingIndex(i);
            }
            row.Bind(hits[i]);
        }

        if (resultContent != null)
        {
            float h = want > 0 ? pitch * want - rowGap : 0f;
            resultContent.sizeDelta = new Vector2(resultContent.sizeDelta.x, h);
        }

        string trimmed = query == null ? "" : query.Trim();
        bool searching = trimmed.Length > 0;

        if (emptyText != null)
        {
            if (!searching) emptyText.text = IdleHint;
            else if (want == 0) emptyText.text = string.IsNullOrEmpty(message) ? NoResultHint : message;
            else emptyText.text = "";
        }
        if (countText != null) countText.text = searching ? want + " 位相关玩家" : "";

        ScrollToTop();
    }

    /// <summary>
    /// 把名单拉回顶部。⚠ 不能只写 verticalNormalizedPosition = 1：ScrollRect 拿**上一帧缓存**的 content 边界换算，
    /// 刚改完高度时它会算出一个中间位置（好友侧边栏那边实测 14 行时停在 0.37）。所以先把 ScrollRect 的内部状态
    /// 切回顶部，再把 content 位置直接写 0 —— 下一帧 ScrollRect 用新高度钳制，0（顶）在新范围内一定合法。
    /// </summary>
    void ScrollToTop()
    {
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
        if (resultContent != null)
            resultContent.anchoredPosition = new Vector2(resultContent.anchoredPosition.x, 0f);
    }

    FriendAddRowUI CloneRow()
    {
        GameObject go = Instantiate(rowTemplate.gameObject, resultContent);
        go.name = "Row_" + _rows.Count.ToString("00");
        go.SetActive(true);
        return go.GetComponent<FriendAddRowUI>();
    }
}
