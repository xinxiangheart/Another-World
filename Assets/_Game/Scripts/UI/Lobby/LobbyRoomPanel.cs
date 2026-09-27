using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 「房间」面板的运行时状态机：房主 / 加入玩家两个槽、踢出、开始游戏、离开与房主转交。
/// </summary>
/// <remarks>2026-09-27 用户（第三次改房间）：「在房主自己视角里加入玩家头像左边做一个踢出的按钮（仅在有玩家在房间
/// 中显示），当房间内加入玩家后在双方头像中央靠右一点出现一个开始游戏（只有房主能点，客人视角里虽然出现但是字体
/// 颜色是灰色的，房主视角是白色悬停点击时变金色），点击后关闭房间界面（只是关闭信息都在），直接转到确认确认弹窗里
/// （不进入匹配池子），若此时玩家点拒绝不会和之前一样，而是两个玩家返回房间中，另外若房主点击右上角的叉会退出房间
/// 并将房主转交给客人，客人头像和名称会变到房主上面（权限也给客人），同时屏幕中央上方弹出提示：房主已离开，你已成
/// 为房主，客人点击叉就单纯离开房间了，同时弹出提示：玩家xxxx离开」。
///
/// **联机还没接进来**（真建房是 <see cref="CreateRoomPanel"/> 那套 Steam 大厅，走的是旧壳）。所以这里把「房间状态」
/// 划成一份本机状态机 + 四个给联机侧的接缝，行为与文案都按用户口径先定死：
///   • <see cref="SetGuest"/> / <see cref="ClearGuest"/>  —— 有人加入 / 离开时联机侧调（房主视角）。
///   • <see cref="OnGuestLeft"/>  —— 房主视角：客人走了 → 「玩家xxxx离开」。
///   • <see cref="OnHostLeft"/>   —— 客人视角：房主走了 → 自己接房主（头像 / 名字本就占着房主槽）+ 「房主已离开，你已成为房主」。
/// 真接进来时，把 <see cref="OnKickClicked"/> / <see cref="OnCloseClicked"/> 里那两句 Debug.Log 换成给对端的通知即可。
///
/// **Steam 接入（2026-09-27）**：面板第一次打开时 <see cref="LobbyRoomSession.BeginHosting"/> 去 Steam 建房 ——
/// 号先查重（先查后建：RequestLobbyList 不允许在已处于大厅时调用）再发布到大厅数据 <c>room_code</c>，
/// 建好后用真号覆盖右上角那个占位号；Steam 未登录 / 建不了房 → 房间号进灰态且开始游戏点不动。
/// 客人槽由大厅成员数据 <c>player_data</c> 填（名字 + 头像 + SteamID），人走了弹「玩家xxxx离开」。
///
/// 房主槽永远挂 <see cref="PlayerProfilePanel"/>（本机 Steam 头像 + 名字）—— 建房的人就是房主，所以「客人接房主」这条
/// 不用搬图像：把客人提升成房主时，房主槽显示的本来就是他自己。
/// </remarks>
public class LobbyRoomPanel : MonoBehaviour, ILobbySubPanelOpen
{
    public static LobbyRoomPanel Instance { get; private set; }

    [Header("壳（右上角的叉走它的 Close）")]
    public LobbySubPanel shell;

    [Header("房主槽")]
    public TMP_Text hostRoleText;

    [Header("加入玩家槽")]
    public RawImage guestWell;
    public TMP_Text guestNameText;
    public TMP_Text guestRoleText;

    [Header("踢出（房主视角 + 房里有人，两个条件都满足才显示）")]
    public GameObject kickGroup;

    [Header("开始游戏（房里有人才出现；只有房主可点，客人那条是灰的）")]
    public GameObject startGroup;
    public Button startButton;

    [Header("Steam 接入（2026-09-27）：真房间号 + 大厅状态机 —— 面板一打开就建房")]
    public LobbyRoomCodeTag codeTag;
    public LobbyRoomSession session;

    [Header("收尾：确认弹窗 / 双方确认后的战斗加载界面")]
    public MatchConfirmPanel confirmPanel;
    public BattleLoadingScreen battleLoading;

    [Header("文案")]
    public string hostRole = "房主";
    public string guestRole = "加入玩家";
    public string waitingText = "等待加入…";

    [Header("配色：空槽那句用钢色，有人之后用奶油")]
    public Color guestNameColor = new Color32(240, 232, 210, 236);    // 奶油 #F0E8D2
    public Color guestEmptyColor = new Color32(142, 162, 180, 205);   // 钢 #8EA2B4

    bool _established;      // 房间已经开着了（关面板不清状态 —— 用户：「只是关闭信息都在」）
    bool _steamReady = true;   // Steam 未登录 / 建不了房 → false：房间号进灰态 + 开始游戏点不动

    /// <summary>Steam 接入件（场景里挂在同一个物体上；没连就自己找一次）。</summary>
    public LobbyRoomSession Session
    {
        get
        {
            if (session == null) session = GetComponent<LobbyRoomSession>();
            return session;
        }
    }

    /// <summary>右上角那行房间号（没在 Inspector 里连就按名字找一次）。</summary>
    public LobbyRoomCodeTag CodeTag
    {
        get
        {
            if (codeTag == null)
            {
                Transform t = transform.Find("Text_RoomCode");
                if (t != null) codeTag = t.GetComponent<LobbyRoomCodeTag>();
            }
            return codeTag;
        }
    }
    bool _isHost;
    bool _hasGuest;
    string _guestName;
    Texture2D _guestAvatar;
    ulong _guestSteamID;

    public bool IsHost { get { return _isHost; } }
    public bool HasGuest { get { return _hasGuest; } }
    public string GuestName { get { return _guestName; } }
    public Texture2D GuestAvatar { get { return _guestAvatar; } }

    void Awake()
    {
        Instance = this;
        if (shell == null) shell = GetComponent<LobbySubPanel>();
    }

    /// <summary>面板每次打开：第一次开 = 建房（自己就是房主），之后开 = 沿用房间状态。</summary>
    void OnEnable()
    {
        if (!_established)
        {
            _established = true;
            _isHost = true;
            _hasGuest = false;
            _guestName = null;
            _guestAvatar = null;
            _guestSteamID = 0;
            _steamReady = true;
        }
        Refresh();
    }

    /// <summary>被用户打开（<see cref="LobbySubPanel.Open"/> 那条）→ 建房 / 回填真号。</summary>
    /// <remarks>不放 OnEnable：面板在场景里存成 active（编辑方便），运行时第一帧就被 closeOnStart 关掉 ——
    /// 挂 OnEnable 会在玩家没点过「房间」的时候就开一间 Steam 大厅。</remarks>
    public void OnSubPanelOpened()
    {
        if (Session != null) Session.BeginHosting(this);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // ===================== 联机侧的接缝 =====================

    /// <summary>有人加入房间（房主视角由联机侧调）。</summary>
    public void SetGuest(string name, Texture2D avatar, ulong steamID = 0)
    {
        _hasGuest = true;
        _guestName = name;
        _guestAvatar = avatar;
        _guestSteamID = steamID;
        Refresh();
    }

    /// <summary>加入的人走了 / 被踢了（只动本机界面，不弹提示 —— 提示走 OnGuestLeft）。</summary>
    public void ClearGuest()
    {
        _hasGuest = false;
        _guestName = null;
        _guestAvatar = null;
        _guestSteamID = 0;
        Refresh();
    }

    /// <summary>房主视角：加入的玩家自己退了 → 提示「玩家xxxx离开」。</summary>
    public void OnGuestLeft()
    {
        if (!_hasGuest) return;
        string who = _guestName;
        ClearGuest();
        Toast("玩家" + who + "离开");
    }

    /// <summary>客人视角：房主走了 → 自己接房主（头像 / 名字本就占着房主槽，这里只翻权限 + 收掉客人槽）。</summary>
    public void OnHostLeft()
    {
        if (_isHost) return;
        _isHost = true;
        ClearGuest();
        Toast("房主已离开，你已成为房主");
    }

    // ===================== 三个按钮 =====================

    /// <summary>踢出（只在房主视角 + 房里有人时出现）。</summary>
    public void OnKickClicked()
    {
        if (!_isHost || !_hasGuest) return;
        string who = _guestName;
        // 联机接进来后：这里给被踢的那位发一条 kicked（对面收 Kicked() 就自己走人）。
        if (Session != null) Session.PublishKick();      // 给大厅打 kicked=1（客人端读到就自己走）
        Debug.Log("[LobbyRoom] 房主把「" + who + "」踢出房间");
        ClearGuest();
        Toast("玩家" + who + "离开");
    }

    /// <summary>开始游戏（只有房主点得动）：关掉房间面板（信息都留着）→ 直接进确认弹窗，**不经过匹配池子**。</summary>
    public void OnStartGameClicked()
    {
        if (!_established || !_isHost || !_hasGuest) return;
        Debug.Log("[LobbyRoom] 房主点「开始游戏」→ 关房间面板（状态保留）→ 进确认弹窗");
        if (shell != null) shell.Close();
        if (confirmPanel != null) confirmPanel.OpenFromRoom(this);
    }

    /// <summary>右上角那个叉。房主走 = 把房主让给客人；客人走 = 单纯离开（提示留给对面）。</summary>
    public void OnCloseClicked()
    {
        if (_established)
        {
            if (_isHost)
            {
                if (_hasGuest) Debug.Log("[LobbyRoom] 房主离开 → 房主转交给「" + _guestName + "」（联机侧待接：发 promoted）");
                else Debug.Log("[LobbyRoom] 房主离开（房里没人，房间就地解散）");
            }
            else
            {
                // 联机接进来后：这里给房主发一条 guest_left（对面收 OnGuestLeft() 弹「玩家xxxx离开」）。
                Debug.Log("[LobbyRoom] 客人离开房间（联机侧待接：发 guest_left）");
            }
            if (_isHost && Session != null) Session.StopHosting();   // 房主走 = 房间解散（真大厅也 LeaveLobby）
            ResetRoom();
        }
        if (shell != null) shell.Close();
    }

    // ===================== 确认弹窗回来的两条 =====================

    /// <summary>确认弹窗里有人点了拒绝 / 超时 → 双方回房间（用户：「两个玩家返回房间中」，不进匹配池子）。</summary>
    public void ReturnToRoomAfterDecline()
    {
        Debug.Log("[LobbyRoom] 有人拒绝 / 超时 → 回房间");
        if (shell != null) shell.Open(null);
        Refresh();
    }

    /// <summary>双方都确认、金色 3 秒走完 → 进真正的战斗加载界面（与匹配那条路同一个界面）。</summary>
    public void OnBothConfirmed()
    {
        Debug.Log("[LobbyRoom] 双方已确认 → 进战斗加载界面");
        if (Session != null) Session.PublishStart();     // 发 start=1 + host_sid（客人侧按旧壳那条进 Game）
        if (battleLoading != null) battleLoading.Open();
    }

    // ===================== 界面同步 =====================

    /// <summary>把状态刷到三个件上：客人槽（头像 / 名字 / 空态）、踢出、开始游戏（含客人的灰态）。</summary>
    public void Refresh()
    {
        if (hostRoleText != null) hostRoleText.text = hostRole;
        if (guestRoleText != null) guestRoleText.text = guestRole;

        if (guestNameText != null)
        {
            guestNameText.text = _hasGuest && !string.IsNullOrEmpty(_guestName) ? _guestName : waitingText;
            guestNameText.color = _hasGuest ? guestNameColor : guestEmptyColor;
        }

        if (guestWell != null)
        {
            if (_hasGuest && _guestAvatar != null)
            {
                guestWell.texture = PlayerProfilePanel.CircleCrop(_guestAvatar);   // 头像也是方的，同样要裁圆
                guestWell.color = Color.white;
            }
            else
            {
                // ⚠ 空 RawImage（texture = null）默认画一块**纯白**方块，必须把 alpha 压掉。
                guestWell.texture = null;
                guestWell.color = new Color(1f, 1f, 1f, 0f);
            }
        }

        if (kickGroup != null) kickGroup.SetActive(_hasGuest && _isHost);     // 踢出：房主视角 + 房里有人
        if (startGroup != null) startGroup.SetActive(_hasGuest);              // 开始游戏：房里有人就出现（客人也看得见）
        // 客人那条只是「灰 + 点不动」—— 颜色由 Button 的 ColorTint 出（normal 奶油 / disabled 灰）。
        if (startButton != null) startButton.interactable = _hasGuest && _isHost && _steamReady;
    }

    /// <summary>建房成功：把 Steam 大厅里那串**真号**回填到右上角（显示 / 复制 / 提示都不变）。</summary>
    public void ApplyRealCode(string code)
    {
        _steamReady = true;
        if (CodeTag != null) CodeTag.SetCode(code);
        Refresh();
    }

    /// <summary>Steam 未登录 / 未连接 / 建房失败：房间号那行进灰态，开始游戏也点不动（沿用匹配 / 排位那条规矩）。</summary>
    public void ApplySteamOffline(string msg = null)
    {
        _steamReady = false;
        if (CodeTag != null)
        {
            if (string.IsNullOrEmpty(msg)) CodeTag.SetUnavailable();
            else CodeTag.SetUnavailable(msg);
        }
        Refresh();
    }

    void ResetRoom()
    {
        _established = false;
        _isHost = false;
        _hasGuest = false;
        _guestName = null;
        _guestAvatar = null;
        _guestSteamID = 0;
        Refresh();
    }

    /// <summary>屏幕中央上方那行一次性提示。</summary>
    static void Toast(string msg) { LobbyToast.Show(msg); }
}
