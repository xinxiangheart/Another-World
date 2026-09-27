using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 「加入房间」右侧侧边栏（2026-09-27）：点右上角那个加入图标从屏幕右侧滑出，再点一次 / 点板以外滑回去。
/// </summary>
/// <remarks>
/// 用户 2026-09-27：「加入房间是一个右侧侧边栏（到顶，但不需要完全到底），范围到右上角的左边，不遮挡房间号，
/// ui 等，再次点击（或者点击范围外）滑动回去，先是显示在右边房间号和 ui 下面的加入房间四个字，然后金线分割
/// 一下，下面是一个输入框，再下面是输入后的预览，主要是展示搜索目标的头像/名称，其下面是人数 1/2 或者红色的
/// 2/2，然后数字右边是加入（有子背景）（根据是否满人为白色（可变金色）或者红色）」。
///
/// 几何（画布 1080 口径，场景侧在 LobbyUIBuilder.BuildRoomJoinSidebar）：板是浮层，
///   **板 = 右上横栏（Plate_TopBand / LobbyBandRight，538x95）那一块的背景** —— 与好友侧边栏
///   （LobbyFriendPanel：宽 = 左上头像板宽、齐屏幕左上角）同一套口径：
///     上沿 = 屏幕顶（0，与横栏同顶；横栏在 Layer_Hud_v1 ⇒ 永远压在板之上）、
///     左沿 = 横栏左沿（1920 - 538 = 1382）、右沿贴屏幕右沿 ⇒ 板宽 538、
///     下沿让开 160（用户：「不需要完全到底」）⇒ 板 538x920。
///   上面的「房间号 / 加入 / 叉」靠**层级**压在板之上，不靠躲；内容位置一个字没动 ——
///   场景侧那层 Content 仍钉在屏幕顶下来 126。
///   滑动行程由 <see cref="Travel"/> 现算（板宽 + 8），改几何不用回来改数。
///
/// 滑动与好友侧边栏（<see cref="LobbyFriendPanel"/>）同一套：progress 0..1 + MoveTowards + 快进慢出，只是朝右。
/// 搜索：输入满 6 位就 <see cref="LobbyRoomSession.SearchByCode"/>（那里面先 SuspendHosting —— RequestLobbyList
///   不允许在「已经处于某个大厅」时调用）；没搜到 / 关掉侧边栏就 ResumeHosting 用**同一个号**重建大厅，
///   所以屏幕上的房间号不会因为来搜一次就变。
/// 加入：<see cref="LobbyRoomSession.JoinFound"/>（真 JoinLobby + 写成员数据）→ 关上侧边栏，房间面板切客人视角。
/// 配色：满员 = 生命红 #B64848 且点不动；不满 = 白（悬停金）—— 两条都走 Button 的 ColorTint，不另写一套。
///
/// 根节点在场景里**存成 active**（方便在编辑器里看版式），运行时由 Start 的 closeOnStart 自己关掉 ——
/// 与 LobbyFriendPanel 同一套约定。
/// </remarks>
public class LobbyJoinSidebar : MonoBehaviour
{
    public static LobbyJoinSidebar Instance { get; private set; }

    [Header("滑动的那块板（留空 = 取第一个子物体）")]
    public RectTransform body;

    [Tooltip("板宽（画布 1080 口径）= 右上横栏的宽度（= 它的左沿距屏幕右沿）")]
    public float width = 538f;

    [Tooltip("滑入 / 滑出耗时（秒）—— 与好友侧边栏同档")]
    public float slideTime = 0.18f;

    [Header("输入框（满 6 位自动去搜）")]
    public TMP_InputField input;

    [Header("预览（搜到之前整组藏着）")]
    public GameObject previewGroup;
    public RawImage avatarWell;
    public TMP_Text nameText;
    public TMP_Text countText;

    [Header("加入（子背景 = LobbyChip_Kick 同款底）")]
    public Button joinButton;
    public TMP_Text joinLabel;

    [Header("提示行（搜索中 / 没找到 / 房间已满）")]
    public TMP_Text statusText;

    [Header("配色（与房间面板 / 确认弹窗同一套）")]
    public Color cream   = new Color32(240, 232, 210, 236);   // 奶油 #F0E8D2
    public Color steel   = new Color32(142, 162, 180, 205);   // 钢 #8EA2B4
    public Color fullRed = new Color32(182, 72, 72, 255);     // 生命 #B64848

    [Header("场景里默认可见（只在编辑器里看和调）")]
    public bool closeOnStart = true;

    float _progress, _target;
    float _restX;          // 场景里摆好的静止位（右沿缩进就在这个值里）
    bool _typing;          // 正在回写 input.text（免得 onValueChanged 自己咬自己）
    bool _joined;          // 已经进了别人的房 → 关板时不要再重建自己的大厅
    int  _searchSeq;       // 只有最后一次搜索的回调算数
    LobbyRoomSession.LobbySearchResult _found;

    /// <summary>开着（含正在开）= true。</summary>
    public bool IsOpen { get { return _target > 0f; } }

    /// <summary>搜到的那间房（没搜到 = null）。</summary>
    public LobbyRoomSession.LobbySearchResult Found { get { return _found; } }

    void Awake()
    {
        Instance = this;
        if (body == null && transform.childCount > 0) body = transform.GetChild(0) as RectTransform;
        if (body != null) _restX = body.anchoredPosition.x;   // 场景里摆好的静止位（含右沿那 32 缩进）
        _progress = 0f; _target = 0f;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        if (!closeOnStart) return;
        _progress = 0f; _target = 0f;
        Apply();
        gameObject.SetActive(false);
    }

    void Update()
    {
        if (Mathf.Approximately(_progress, _target)) return;
        float step = slideTime > 0.0001f ? Time.unscaledDeltaTime / slideTime : 1f;
        _progress = Mathf.MoveTowards(_progress, _target, step);
        Apply();
        if (_target <= 0f && _progress <= 0f) gameObject.SetActive(false);
    }

    /// <summary>整块被关掉（父级 SetActive(false)）时把滑动状态清干净 —— 否则「开着侧边栏直接关掉房间面板、
    /// 再打开」会看到它还是滑出态（Update 停了，_target 还停在上一次的值）。</summary>
    void OnDisable()
    {
        _progress = 0f;
        _target = 0f;
        Apply();
    }

    void Apply()
    {
        if (body == null) return;
        float e = 1f - Mathf.Pow(1f - _progress, 3f);   // 快进慢出：起手快、贴边稳
        Vector2 p = body.anchoredPosition;
        p.x = _restX + Travel() * (1f - e);              // 关着 = 整块停在屏幕右沿之外（行程里含右沿那点缩进）
        body.anchoredPosition = p;
    }

    /// <summary>滑动行程：板自身宽度 + 右沿缩进，再留 8 单位余量 —— 改几何不用回来改这个数。</summary>
    float Travel()
    {
        float need = body.rect.width + Mathf.Max(0f, -_restX) + 8f;
        return Mathf.Max(width, need);
    }

    public void Toggle() { if (IsOpen) Close(); else Open(); }

    public void Open()
    {
        gameObject.SetActive(true);
        // ⚠ 这里**不许**再 SetAsLastSibling：板在场景里的兄弟位次就是「压在房间面板内容之上、
        //   压在右上角那行（房间号 / 加入 / 叉）之下」（用户 2026-09-27：「上顶满的意思是像好友那样
        //   作为右上角和房间号 ui」）—— 抬到最上面会把那三个件盖掉。
        if (_progress <= 0f) Apply();
        _target = 1f;
        ResetForNewSearch();
    }

    /// <summary>收回去。没进别人的房就把搜号时让出去的大厅重建回来（号不变）。</summary>
    public void Close()
    {
        if (!gameObject.activeSelf) return;
        _target = 0f;
        if (!_joined && LobbyRoomSession.Instance != null) LobbyRoomSession.Instance.ResumeHosting();
    }

    /// <summary>把剪贴板里的东西当房间号**贴进输入框**（右键触发，见 <see cref="LobbyJoinInputPaste"/>）。</summary>
    /// <remarks>用户 2026-09-27：「加入一个在输入栏右键自动粘贴复制的房间号功能」。
    /// 清洗规则与手输完全同一条（<see cref="Clean"/>：只留 A-Z / 0-9 并转大写，再按 characterLimit 截断）——
    /// 所以「房间号：AB12CD」这种连前缀一起复制下来的串也能直接用。贴完走 <c>onValueChanged</c> ⇒
    /// <see cref="OnCodeChanged"/>：满 6 位自动去搜，和手打进去没有任何区别。
    /// **整段替换**、不是插到光标处 —— 6 位房间号井里替换才是想要的行为。
    /// 剪贴板拿不到 / 洗完是空的：只在下面那行提示里说一声，**不动输入框**（免得平白把已输的号清掉）。</remarks>
    public void PasteFromClipboard()
    {
        string raw = null;
        try { raw = GUIUtility.systemCopyBuffer; } catch { }   // 某些平台 / 无剪贴板权限时会抛

        string code = Clean(raw);
        if (code.Length == 0) { SetStatus("剪贴板里没有房间号"); return; }
        if (input == null) return;
        if (input.characterLimit > 0 && code.Length > input.characterLimit)
            code = code.Substring(0, input.characterLimit);

        input.text = code;                  // ⇒ onValueChanged ⇒ OnCodeChanged（再洗一遍，幂等）
        input.caretPosition = code.Length;  // = selectionAnchor/Focus 一起设：光标收到末尾
        input.ActivateInputField();         // 贴完把焦点留在这口井上，接着能直接改
    }

    // ===================== 输入 → 搜 =====================

    void ResetForNewSearch()
    {
        _joined = false;
        _found = null;
        _searchSeq++;
        if (input != null) { _typing = true; input.SetTextWithoutNotify(""); _typing = false; }
        ShowPreview(null);
        SetStatus("");
    }

    /// <summary>输入框每次变化（场景里那条持久监听接的）。只留大写字母 / 数字、最多 6 位；满 6 位就去搜。</summary>
    public void OnCodeChanged(string value)
    {
        if (_typing) return;
        string code = Clean(value);
        if (input != null && input.text != code) { _typing = true; input.SetTextWithoutNotify(code); _typing = false; }

        if (code.Length < 6)
        {
            _searchSeq++;                       // 让在飞的搜索回调作废
            _found = null;
            ShowPreview(null);
            SetStatus(code.Length == 0 ? "" : "输入 6 位房间号");
            return;
        }
        Search(code);
    }

    static string Clean(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new System.Text.StringBuilder(6);
        for (int i = 0; i < s.Length && sb.Length < 6; i++)
        {
            char c = char.ToUpperInvariant(s[i]);
            if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) sb.Append(c);
        }
        return sb.ToString();
    }

    void Search(string code)
    {
        _found = null;
        ShowPreview(null);
        var s = LobbyRoomSession.Instance;
        if (s == null) { SetStatus("房间服务没起来"); return; }

        _searchSeq++;
        int seq = _searchSeq;
        SetStatus("搜索中…");
        s.SearchByCode(code, r =>
        {
            if (seq != _searchSeq) return;      // 又改了号 / 关过板 → 这份结果不算
            ApplyResult(r);
        });
    }

    void ApplyResult(LobbyRoomSession.LobbySearchResult r)
    {
        if (r == null || !r.found)
        {
            _found = null;
            ShowPreview(null);
            SetStatus(r != null && !string.IsNullOrEmpty(r.message) ? r.message : "没找到这个房间号");
            return;
        }
        _found = r;
        ShowPreview(r);
        SetStatus(IsFull(r) ? "房间已满" : "");
    }

    static bool IsFull(LobbyRoomSession.LobbySearchResult r)
    {
        return r != null && r.found && r.members >= Mathf.Max(1, r.capacity);
    }

    void ShowPreview(LobbyRoomSession.LobbySearchResult r)
    {
        if (previewGroup != null) previewGroup.SetActive(r != null && r.found);
        if (r == null || !r.found) return;

        if (nameText != null)
        {
            nameText.text = string.IsNullOrEmpty(r.hostName) ? "玩家" : r.hostName;
            nameText.color = cream;
        }

        if (avatarWell != null)
        {
            Texture2D tex = r.hostId != 0 ? SteamAvatarManager.GetAvatarTexture(r.hostId) : null;
            avatarWell.texture = tex != null ? PlayerProfilePanel.CircleCrop(tex) : PlayerProfilePanel.Placeholder();
            avatarWell.color = Color.white;
        }

        bool full = IsFull(r);
        if (countText != null)
        {
            countText.text = r.members + "/" + Mathf.Max(1, r.capacity);
            countText.color = full ? fullRed : cream;
        }
        if (joinLabel != null) joinLabel.color = Color.white;   // 颜色让给 Button 的 ColorTint（白 → 悬停金 / 满员红）
        if (joinButton != null) joinButton.interactable = !full;
    }

    void SetStatus(string msg)
    {
        if (statusText == null) return;
        statusText.text = msg == null ? "" : msg;
        statusText.color = steel;
    }

    // ===================== 加入 =====================

    public void OnJoinClicked()
    {
        var s = LobbyRoomSession.Instance;
        if (s == null || _found == null || !_found.found) return;
        if (joinButton != null && !joinButton.interactable) return;

        string who = nameText != null && !string.IsNullOrEmpty(nameText.text) ? nameText.text : "玩家";
        var target = _found;
        if (joinButton != null) joinButton.interactable = false;
        SetStatus("正在加入…");

        s.JoinFound(target.lobby, (ok, msg) =>
        {
            if (!ok)
            {
                SetStatus(string.IsNullOrEmpty(msg) ? "加入失败，请重试" : msg);
                if (joinButton != null) joinButton.interactable = !IsFull(_found);
                return;
            }
            _joined = true;
            LobbyToast.Show("已加入 " + who + " 的房间");
            Close();
        });
    }
}
