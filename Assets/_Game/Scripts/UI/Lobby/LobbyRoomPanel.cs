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

    [Header("房主槽（客人视角：头像 / 名字这两件要换成对方）")]
    public TMP_Text hostRoleText;
    public RawImage hostWell;
    public TMP_Text hostNameText;

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

    [Header("「加入房间」右侧侧边栏（它自己滑动；这里只留一个引用）")]
    public LobbyJoinSidebar joinSidebar;

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
    Color _hostNameColor = new Color32(240, 232, 210, 236);   // 房主名字的常态色（Awake 记下场景里那个值 —— 客人视角借走后再还回来）
    string _hostName0 = "你自己";                               // 房主名字的原文案（本机名还没就绪时用它，别留着对方的名字）

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

    // ── 客人视角（进了别人的房）────────────────────────────────────────────
    string    _hostName;
    Texture2D _hostAvatar;
    ulong     _hostSteamID;
    bool      _guestMode;

    public bool GuestMode { get { return _guestMode; } }
    /// <summary>确认弹窗要的「对方」：房主视角 = 客人；客人视角 = 房主（2026-09-27）。</summary>
    public string OpponentName { get { return _guestMode ? _hostName : _guestName; } }
    public Texture2D OpponentAvatar { get { return _guestMode ? _hostAvatar : _guestAvatar; } }

    void Awake()
    {
        Instance = this;
        if (shell == null) shell = GetComponent<LobbySubPanel>();
        if (hostNameText != null)
        {
            _hostNameColor = hostNameText.color;                          // 还房主槽时用的常态色（客人视角会把它改成奶油）
            if (!string.IsNullOrEmpty(hostNameText.text)) _hostName0 = hostNameText.text;
        }
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
        if (!_guestMode && Session != null) Session.BeginHosting(this);   // 客人视角别再开自己的房
    }

    /// <summary>
    /// 客人视角：从「收到邀请」点同意进来 —— 只把面板打开，**不建自己的房**。
    /// </summary>
    /// <remarks>2026-09-27：这条入口与「加入房间」侧边栏不同 —— 那条是在房间面板里搜号，面板本来就是开的；
    /// 这条是从好友栏直接点进来，面板还是关的，而直接 <see cref="LobbySubPanel.Open"/> 会走
    /// <see cref="OnSubPanelOpened"/> ⇒ <c>BeginHosting</c> 先建一间自己的房，紧接着 JoinLobby 就会撞车
    /// （Steam 同时只能在一个大厅里）。所以先把 _guestMode 立起来（OnSubPanelOpened 看到它就不建房），
    /// 再开面板。真进大厅由 <see cref="LobbyRoomSession.JoinInviteAsGuest"/> 那条走，进来后
    /// <see cref="ApplyGuestLobby"/> 把两个槽换成对方的房。
    /// </remarks>
    public void OpenAsGuest()
    {
        _guestMode = true;                 // 让 OnSubPanelOpened 跳过建房
        _established = true;               // 让 OnEnable 不要再把 _isHost 翻回来
        _isHost = false;
        if (shell != null) shell.Open(null);
        Refresh();
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

    // ===================== 客人视角（进了别人的房） =====================

    /// <summary>真进了别人的房（<see cref="LobbyRoomSession.JoinFound"/> 里调）：房主槽换成对方、客人槽 = 自己。</summary>
    public void ApplyGuestLobby(string hostName, Texture2D hostAvatar, ulong hostId, string code)
    {
        _guestMode = true;
        _established = true;
        _isHost = false;
        _hostName = string.IsNullOrEmpty(hostName) ? "玩家" : hostName;
        _hostAvatar = hostAvatar;
        _hostSteamID = hostId;

        // 房主槽本来挂 PlayerProfilePanel（填本机资料），客人视角要把它换成对方 → 把那个组件关掉
        if (hostWell != null)
        {
            var view = hostWell.GetComponentInParent<PlayerProfilePanel>();
            if (view != null) view.enabled = false;
            hostWell.texture = _hostAvatar != null ? PlayerProfilePanel.CircleCrop(_hostAvatar) : PlayerProfilePanel.Placeholder();
            hostWell.color = Color.white;
        }
        if (hostNameText != null)
        {
            hostNameText.text = _hostName;
            hostNameText.color = guestNameColor;
        }

        // 客人槽放自己（自己是加入的那位）
        var sd = SteamDataManager.Instance;
        _hasGuest = true;
        _guestName = sd != null && !string.IsNullOrEmpty(sd.localPlayerName) ? sd.localPlayerName : "我";
        _guestAvatar = sd != null ? sd.localAvatar : null;
        _guestSteamID = sd != null ? sd.localSteamID.m_SteamID : 0;

        if (CodeTag != null && !string.IsNullOrEmpty(code)) CodeTag.SetCode(code);   // 屏幕上那行换成对方的号
        Refresh();
        Toast("已加入 " + _hostName + " 的房间");
    }

    /// <summary>客人视角：自己点了右上角的叉 —— 离开对方的房（提示留给房主那侧）。</summary>
    public void LeaveGuestRoom()
    {
        if (!_guestMode) return;
        if (Session != null) Session.LeaveGuestLobby();
        ResetRoom();
        ClearGuest();
    }

    /// <summary>客人视角：被房主踢了 → 回自己的房（重新查重开一间，号会变）。</summary>
    public void OnKickedByHost()
    {
        if (!_guestMode) return;
        Toast("你已被移出房间");
        ResetRoom();
        ClearGuest();
        if (Session != null) Session.RestartHosting();
    }

    /// <summary>客人视角：读到房主的 <c>confirm=1</c> → 关房间面板、进确认弹窗（对手 = 房主）。</summary>
    /// <remarks>2026-09-27：房主点「开始游戏」这一步的通知（原来一条都没有 —— 客人就卡在房间界面）。</remarks>
    public void OnRemoteConfirm()
    {
        if (!_guestMode) return;
        Debug.Log("[LobbyRoom] 房主点了「开始游戏」→ 客人侧进确认弹窗（对手 = " + _hostName + "）");
        if (shell != null) shell.Close();
        if (confirmPanel != null) confirmPanel.OpenFromRoom(this);
    }

    /// <summary>客人视角：读到房主的 <c>start=1</c>（双方已确认）→ 收掉确认弹窗、进战斗加载界面。</summary>
    /// <remarks>2026-09-27 二次修：这条原来做的是「进确认弹窗」（和 <c>confirm</c> 那条重复）——
    /// <c>start</c> 是「双方已确认」的语义，客人这时候人已经在确认弹窗里了，该做的是进加载界面。
    /// 兜底用：客人自己那 3 秒也会开加载界面（幂等，<c>OpenInternal</c> 第一行就是 <c>if (IsOpen) return;</c>）。</remarks>
    public void OnRemoteStart()
    {
        if (!_guestMode) return;
        Debug.Log("[LobbyRoom] 房主已确认开打 → 客人侧进战斗加载界面");
        if (confirmPanel != null && confirmPanel.IsOpen) confirmPanel.Hide();
        if (battleLoading != null) battleLoading.Open();
    }

    /// <summary>确认弹窗里自己点了「确认」（房间那条路）→ 把自己那格写给对面（房主写大厅数据 / 客人写成员数据）。</summary>
    /// <remarks>2026-09-27：房间这条路不经过 QuickMatchPanel（它那套 <c>host_ok</c> / <c>guest_ok</c> 的轮询只在匹配里跑），
    /// 所以「自己确认了」得从这里写到大厅里，否则对面永远不知道、两边各自 15 秒超时回房间。</remarks>
    public void NotifyLocalConfirmed() { if (Session != null) Session.PublishConfirmAccept(true); }

    /// <summary>确认弹窗里自己点了「拒绝」/ 15 秒超时（房间那条路）→ 把自己那格写「拒绝」，对面读到就一起回房间。</summary>
    public void NotifyLocalDeclined() { if (Session != null) Session.PublishConfirmAccept(false); }

    /// <summary>确认弹窗每帧替房间会话跑一拍 —— 房间壳（<c>LobbySubPanel</c>）关了之后
    /// <c>LobbyRoomSession.Update</c> 不再跑（两者同一个物体），对面那格的确认就读不回来（见 <see cref="LobbyRoomSession.PollDetached"/> 的注释）。</summary>
    public void PollDetached() { if (Session != null) Session.PollDetached(); }

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
    /// <remarks>2026-09-27 二次修（用户报「房主点击开始后客人不会进入确认界面而是仍卡在房间界面」）：
    /// 原来这里只关自己的面板、开自己的弹窗，**一个字都没发给客人** —— 客人侧永远读不到东西，就卡在房间界面。
    /// 现在先 <see cref="LobbyRoomSession.PublishConfirm"/> 打一个 <c>confirm=1</c>，客人侧读到就走
    /// <see cref="OnRemoteConfirm"/>（同一步：关房间面板 + 开确认弹窗）。
    /// ⚠ 不能拿 <c>start</c> 代发 —— 那是「双方已确认、真要开打」的语义（由 <see cref="OnBothConfirmed"/> 发），
    /// 提前发会让客人跳过确认直接进加载。</remarks>
    public void OnStartGameClicked()
    {
        if (!_established || !_isHost || !_hasGuest) return;
        Debug.Log("[LobbyRoom] 房主点「开始游戏」→ 关房间面板（状态保留）→ 进确认弹窗，并通知客人");
        if (Session != null) Session.PublishConfirm();          // 通知客人：也进确认弹窗（别卡在房间界面）
        if (shell != null) shell.Close();
        if (confirmPanel != null) confirmPanel.OpenFromRoom(this);
    }

    /// <summary>右上角那个叉。房主走 = 把房主让给客人；客人走 = 单纯离开（提示留给对面）。</summary>
    public void OnCloseClicked()
    {
        if (_guestMode) { LeaveGuestRoom(); if (shell != null) shell.Close(); return; }
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
        if (Session != null) Session.ResetConfirmWatch();   // 下一轮「开始游戏」还要能读到 confirm / 对面那格
        if (shell != null) shell.Open(null);
        Refresh();
    }

    /// <summary>双方都确认、金色 3 秒走完 → 进真正的战斗加载界面（与匹配那条路同一个界面）。</summary>
    /// <remarks>2026-09-27 二次修：配置那一步两侧都要填（房主 <c>PublishStart</c> / 客人 <c>FillGuestConfig</c>），
    /// 走 <see cref="LobbyRoomSession.ConfirmBattleEntry"/> 一把收 —— 否则客人那半区会读到上一局的对手。</remarks>
    public void OnBothConfirmed()
    {
        Debug.Log("[LobbyRoom] 双方已确认 → 进战斗加载界面");
        if (Session != null) Session.ConfirmBattleEntry();     // 房主：发 start=1 + 填 LobbyConfig；客人：填 LobbyConfig
        if (confirmPanel != null && confirmPanel.IsOpen) confirmPanel.Hide();
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

    /// <summary>房间状态整体回零（客人离开 / 房主解散 / 被踢）：字段清空 + **房主槽还给自己** + 房间号回到占位号。</summary>
    /// <remarks>2026-09-27 二次修（用户报「客人离开后点击房间可能直接显示之前房主的幻影房间（即使房主此时甚至是离线），
    /// 并且只显示房主头像不显示自己」）：原来这里只清字段 —— 客人视角借走的房主槽（<see cref="PlayerProfilePanel"/>
    /// 被 disabled、井里铺着对方的图 / 名字）没人还，面板再打开时房主槽还是上一位房主的残影，自己的头像反而看不见。
    /// 房间号那行同理：客人视角写的是**对方的号**，不清就跟着面板一起「复活」。</remarks>
    void ResetRoom()
    {
        _established = false;
        _guestMode = false;
        _hostName = null;
        _hostAvatar = null;
        _hostSteamID = 0;
        _isHost = false;
        _hasGuest = false;
        _guestName = null;
        _guestAvatar = null;
        _guestSteamID = 0;
        RestoreHostSlot();
        if (CodeTag != null) CodeTag.ResetPlaceholder();
        Refresh();
    }

    /// <summary>房主槽回到「我自己」的样子 —— 客人视角把它借给了对方，离开别人的房时必须还回来。</summary>
    /// <remarks>⚠ 不能只把 <c>PlayerProfilePanel.enabled</c> 拨回 true —— 它内部有「这张图已经铺过了」的短路缓存
    /// （<c>_applied == src &amp;&amp; texture != null</c>），头像井里现在是对方的图、texture 非空，短路会让残影留下来；
    /// 所以走 <see cref="PlayerProfilePanel.Reapply"/> 强制重铺一次。</remarks>
    void RestoreHostSlot()
    {
        if (hostWell != null)
        {
            var view = hostWell.GetComponentInParent<PlayerProfilePanel>();
            if (view != null) { view.enabled = true; view.Reapply(); }
            else
            {
                var sd = SteamDataManager.Instance;     // 组件不在（理论上不会）也要把井自己刷回来
                hostWell.texture = sd != null && sd.localAvatar != null
                    ? PlayerProfilePanel.CircleCrop(sd.localAvatar) : PlayerProfilePanel.Placeholder();
                hostWell.color = Color.white;
            }
        }
        if (hostNameText != null)
        {
            hostNameText.color = _hostNameColor;
            var sd = SteamDataManager.Instance;
            // 本机名还没就绪（Steam 未登录 / 名还没取到）时退回原文案 —— 总之不能留着上一位房主的名字
            hostNameText.text = sd != null && !string.IsNullOrEmpty(sd.localPlayerName) ? sd.localPlayerName : _hostName0;
        }
    }

    /// <summary>屏幕中央上方那行一次性提示。</summary>
    static void Toast(string msg) { LobbyToast.Show(msg); }
}
