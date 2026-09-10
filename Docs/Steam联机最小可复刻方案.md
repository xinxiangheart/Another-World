# Steam 联机最小可复刻方案

> 抽取自《Another World》的 Steam 大厅 + Mirror/FizzySteamworks P2P 联机实现。
> 目标：另一个 Unity 项目照着本文就能搭出同构的「大厅 → 房间 → P2P → 对局 → 断线回大厅」链路。
>
> 技术栈：Unity 2022.3 / Mirror / FizzySteamworks（Steamworks.NET）/ Steam Matchmaking。

---

## 0. 一句话架构

**Steam Matchmaking 只做「撮合 + 交换名片」，真正的游戏数据走 Mirror over FizzySteamworks（Steam P2P 通道）。**
大厅里交换的关键信息只有两样：**对手的 SteamID64**（作为 P2P 目标地址）和 **MatchKey**（防止多组玩家串线）。

---

## 1. 架构图（文字版）

```
┌──────────────────────────── Lobby 场景（无 NetworkManager） ────────────────────────────┐
│                                                                                        │
│  SteamManager(常驻)          SteamDataManager(常驻)        LobbyManager(按钮入口)        │
│   SteamAPI.Init/RunCallbacks  昵称/头像/存档              创建房间/加入房间/快速匹配/AI │
│                                                                                        │
│   ┌─ CreateRoomPanel ─────┐  ┌─ JoinRoomPanel ─────┐  ┌─ QuickMatchPanel ─────────┐   │
│   │ 房主：CreateLobby(2人) │  │ 输入6位房间号 →      │  │ 先搜(0.5s×10) 再自建；    │   │
│   │ 写 SetLobbyData:       │  │  AddRequestLobbyList │  │ 自建后启动后台搜索(3s/次)，│   │
│   │  game/room_code/       │  │  StringFilter(       │  │ 搜到别的单人厅就放弃自己   │   │
│   │  host_data             │  │   "room_code",code)  │  │ 去加入（防双自建互撞）     │   │
│   │ 轮询读 guest memberdata│  │ → JoinLobby → 写      │  │ host_ok/guest_ok 双确认   │   │
│   │ 踢人/开局(SetLobbyData)│  │  SetLobbyMemberData  │  │ 15s 倒计时自动重排        │   │
│   └────────────────────────┘  └──────────────────────┘  └───────────────────────────┘   │
│                    │                       │                        │                  │
│                    └───────────── LobbyConfig(静态跨场景状态) ───────┘                  │
│                       FromLobby / IsHost / MatchKey / CurrentLobbyID / HostSteamID      │
│                              / RemoteSteamID / IsDirectIP / IsAI                       │
│                                        │                                               │
│                              JoinGamePanel（双方头像+3s倒计时+Preloader 预加载）          │
└────────────────────────────────────────┼───────────────────────────────────────────────┘
                                         │ SceneManager.LoadSceneAsync("Game")
┌────────────────────────────────────────▼───────────────────────────────────────────────┐
│  Game 场景                                                                             │
│   NetworkManager(常驻, dontDestroyOnLoad)  ← 同物体挂 FizzySteamworks + KcpTransport   │
│        │                                        AutoConnect 运行时二选一销毁             │
│        │                                                                               │
│   AutoConnect.Start():                                                                 │
│     离线/AI → SetupKCP() + StartHost() + OfflineAIHost 造 AI 的 NetworkPlayer           │
│     DirectIP → SetupKCP() + StartHost/StartClient(IP)                                   │
│     FromLobby+Host  → SetupFizzy() → CreateLobby(2) ──LobbyCreated──→ StartHost()        │
│     FromLobby+Guest → SetupFizzy() → 每2s RequestLobbyList(game=MatchKey)               │
│                          ──LobbyMatchList──→ JoinLobby ──LobbyEnter──→                  │
│                          networkAddress = host_sid(SteamID64) → 延迟1.5s → StartClient() │
│                                        │                                               │
│   NetworkTurnSync(NetworkBehaviour, SyncVar)                                           │
│     双端 NetworkPlayer 就绪 → gameStarted=true → 服务端 StartGameForClient() 发初始牌   │
│     首手权 isHostFirst/isMyTurnFirst（服务端随机，客户端取反）                            │
│     阶段推进走 BroadcastTurnPhase(TargetRpc)，不进 SyncVar                              │
│                                        │                                               │
│   NetworkPlayer(Local/Remote) + BoardSyncManager(12槽快照) + RegistrySyncManager(五区)   │
│   AutoConnect: OnDisconnected/OnServerDisconnectedEvent → 2s → StopHost/Client → 回 Lobby│
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. 必需组件清单

### 2.1 外部包
| 包 | 用途 |
|---|---|
| Steamworks.NET（`com.rlabrecque.steamworks.net`） | Steam API 绑定 |
| Mirror | 网络框架 |
| FizzySteamworks（Mirror 的 Transport 扩展） | 用 Steam P2P 当 Mirror 的传输层 |
| TextMeshPro | 面板文字（可选） |

### 2.2 脚本（按职责）

| 类 | 挂载 | 职责 |
|---|---|---|
| `SteamManager` | 常驻空物体 | `SteamAPI.Init()` / `RunCallbacks()`（每帧）/ `Shutdown()`。Direct IP 模式可跳过初始化 |
| `SteamAppIDBootstrapper` | 静态（`RuntimeInitializeOnLoadMethod`） | 打包后往 exe 目录写 `steam_appid.txt`（Editor 里 Steamworks.NET 自己处理） |
| `SteamDataManager` | 常驻 | 昵称/头像/本地存档；填 `LobbyConfig.LocalSteamID` |
| `SteamAvatarManager` | 静态 + 轮询器 | SteamID → Texture2D 头像缓存；未就绪时 `RequestUserInformation` + 轮询降级 |
| `LobbyConfig` | 静态 | **跨场景唯一状态载体**（见 §3.6） |
| `LobbyManager` | Lobby 场景 | 按钮入口 + 面板互斥（打开一个面板先关掉其它） |
| `CreateRoomPanel` | Lobby 场景 | 建房（房主侧）+ 房间号展示 + 读客人数据 + 踢人/开局 |
| `JoinRoomPanel` | Lobby 场景 | 房间号搜索 + 加入 + 展示房主信息 |
| `QuickMatchPanel` | Lobby 场景 | 一键匹配（先搜后自建 + 后台搜索 + 双确认 + 倒计时） |
| `JoinGamePanel` | Lobby 场景 | 过渡面板（双方头像/名字 + 3s 倒计时 + 预加载进度） |
| `Preloader` | 常驻 | 提前异步预加载 Game 场景重资源 + `LoadSceneAsync` |
| `AutoConnect` | Game 场景 | **连接编排器**：选 Transport → 建房/搜房 → StartHost/StartClient → 断线回大厅 |
| `NetworkTurnSync` | Game 场景（带 NetworkIdentity） | `gameStarted` 门闩、首手权 SyncVar、回合请求 Command |
| `NetworkPlayer` | Player 预制体 | 每玩家的权威状态（血量/能量/手牌）+ 业务 RPC |
| `OfflineAIHost` | Game 场景（运行时 AddComponent） | 离线模式造一个 server-only 的第二个 NetworkPlayer 当 AI |

### 2.3 场景/预制体
- **Lobby 场景**：`SteamManager`、`SteamDataManager`、`LobbyManager` 三个面板、`JoinGamePanel`。**不放 NetworkManager**。
- **Game 场景**：一个 `NetworkManager` 物体（`dontDestroyOnLoad:1`，`autoCreatePlayer:1`，`playerPrefab` = 玩家预制体），同一物体上**同时挂** `FizzySteamworks` 和 `KcpTransport`（运行时二选一），再挂 `AutoConnect`、`NetworkTurnSync`。
- 玩家预制体：带 `NetworkIdentity` + `NetworkPlayer`。

---

## 3. 核心流程（伪代码）

### 3.1 初始化

```
// 静态引导，任何场景加载前
SteamAppIDBootstrapper.EnsureSteamAppId()   // 打包后写 steam_appid.txt

SteamManager.Awake():
    if LobbyConfig.IsDirectIP: return        // 直连模式不碰 Steam
    SteamAPI.RestartAppIfNecessary(AppId_t.Invalid)   // 没从 Steam 启动就重启
    SteamAPI.Init()  → m_bInitialized
SteamManager.Update(): SteamAPI.RunCallbacks()        // ★ 必须每帧，否则所有 Callback<T> 不触发
```

### 3.2 建房（房主，Lobby 场景）

```
CreateRoomPanel.OpenAsHost():
    if !SteamManager.Initialized: return
    if !SteamUser.BLoggedOn(): 报"Steam 未登录/未连接"，return    // ★ 否则 CreateLobby 静默失败
    _roomCode = Random(100000..999999)
    _lcb = Callback<LobbyCreated_t>.Create(cb =>          // ★ 注册前先 Dispose 旧的
        if cb.m_eResult != k_EResultOK: 报错 + return
        _lobbyID = cb.m_ulSteamIDLobby
        SetLobbyData(_lobbyID, "game", "<项目唯一key>")   // 搜索过滤用
        SetLobbyData(_lobbyID, "room_code", _roomCode)    // 房间号（自己写的约定字段）
        SetLobbyData(_lobbyID, "host_data", 我的JSON)      // 昵称/战绩/steamID
    )
    CreateLobby(k_ELobbyTypePublic, 2)                    // 2 = 上限两人

// 每 0.5s（房主轮询客人）
RequestLobbyData(_lobbyID)                                // ★ 刷新本地缓存，否则读不到 memberdata
for member in GetLobbyMemberByIndex(lobby, 0..count-1):
    if member == 我: continue
    guestJson = GetLobbyMemberData(lobby, member, "player_data")
    if 非空: 显示客人信息; 打开"开始游戏"按钮; _hasGuest = true

// 房主操作
Kick:  SetLobbyData(lobby, "kicked", "1")                 // 客人轮询到就自己退
Start: SetLobbyData(lobby, "start", "1")
       SetLobbyData(lobby, "host_sid", 我的SteamID64)      // ★ 客人用它当 P2P 地址
       LobbyConfig.{FromLobby=true, IsHost=true, MatchKey=$"aw_{lobbyID}", HostSteamID, CurrentLobbyID}
       加载 Game 场景
```

### 3.3 房间号加入（客人，Lobby 场景）

```
JoinRoomPanel.OnInputChanged(code):                       // 输入满 6 位自动搜
    _lobbyListCB?.Dispose()                               // ★ 先释放旧回调
    _lobbyListCB = Callback<LobbyMatchList_t>.Create(OnLobbyList)
    AddRequestLobbyListStringFilter("room_code", code, k_ELobbyComparisonEqual)
    AddRequestLobbyListDistanceFilter(k_ELobbyDistanceFilterWorldwide)   // ★ 否则跨数据中心搜不到
    RequestLobbyList()
    StartCoroutine(5s 超时 → 提示"未找到该房间")

OnLobbyList(cb):
    if cb.m_nLobbiesMatching == 0: 超时提示; return
    lid = GetLobbyByIndex(0)
    if GetNumLobbyMembers(lid) >= 2: 提示"房间已满"; return
    展示房主信息（GetLobbyData(lid,"host_data")）; 显示"加入"按钮

Join():
    _enterCB = Callback<LobbyEnter_t>.Create(cb =>
        SetLobbyMemberData(lobby, "player_data", 我的JSON)   // ★ 客人只能写 memberdata
        RequestLobbyData(lobby)                             // ★ 立刻刷新
        CreateRoomPanel.OpenAsGuest(lobby, roomCode)
    )
    JoinLobby(lid)
    StartCoroutine(10s 超时 → "加入失败")

// 客人轮询（CreateRoomPanel.Update，0.5s）
SetLobbyMemberData(lobby,"player_data",我的JSON)             // 反复写，防丢
RequestLobbyData(lobby)
hostJson = GetLobbyData(lobby,"host_data")                  // 读房主信息
if GetLobbyData(lobby,"kicked") == "1": 退房
if GetNumLobbyMembers(lobby) < 2:       退房                 // 房主跑了
if GetLobbyData(lobby,"start") == "1":  记 LobbyConfig(IsHost=false) → 加载 Game 场景
```

### 3.4 快速匹配（QuickMatchPanel，最复杂）

```
Open():
    if !SteamUser.BLoggedOn(): 报错，return
    state = Searching; RegisterCallbacks()      // LobbyMatchList / LobbyCreated / LobbyEnter / LobbyDataUpdate
    StartCoroutine(SearchRoutine())

SearchRoutine():                                // 先搜 0.5s × 10 次
    for i in 0..9:
        AddRequestLobbyListStringFilter("game","<快速匹配key>",Equal)
        AddRequestLobbyListDistanceFilter(Worldwide)
        AddRequestLobbyListResultCountFilter(1)
        RequestLobbyList()
        yield 0.5s
        if _joining || _lobbyID != 0: break
    if 没搜到: _iAmHost = true; CreateLobby(Public, 2)

OnLobbyList(cb):                                // 前台搜索回调
    if _iAmHost: return                         // ★ 已自建 → 忽略（后台搜索接管）
    _joining = true; 停 SearchRoutine; _iAmHost = false
    _lobbyID = GetLobbyByIndex(0); JoinLobby(_lobbyID)

OnLobbyCreated(cb):                             // 自建成功
    if !_iAmHost: LeaveLobby(cb.lobby); return  // ★ 已加入别人的 → 销毁自己这个废弃大厅
    if cb.m_eResult != OK: 报错 + ResetState
    _lobbyID = cb.lobby
    SetLobbyData("game","<快速匹配key>"); WriteMyData("host_data")
    StartBackgroundSearch()                     // ★ 关键：自建后仍持续搜别人

OnLobbyEnter(cb):                               // CreateLobby 自己也会触发 LobbyEnter
    if _iAmHost:
        if GetNumLobbyMembers(lobby) >= 2:      // 真的有客人了
            StopBackgroundSearch(); StartCoroutine(PollGuestData())
        // members==1：刚建完，保持后台搜索（★ 不要在这里停，否则双方自建后互搜不到）
    else:
        StopBackgroundSearch()
        SetLobbyMemberData("player_data", 我的JSON)   // 客人写
        RetryWriteGuestData()                          // 0.8s × 4 重写
        RefreshOpponent()

BackgroundSearchRoutine():                      // 每 3s
    if 自己大厅 members >= 2: 停止（有客人了）
    RequestLobbyList(game=<快速匹配key>, Worldwide, ResultCount=3)

OnBgLobbyList(cb):                              // 后台搜索回调（独立 Callback 句柄）
    for each found lobby:
        if found == 自己: continue
        if found 已满(>=2) 或 host_ok==1 或 start==1: continue
        ★ 发现另一个单人等待的大厅 → 放弃自己、加入对方
        StopBackgroundSearch(); LeaveLobby(自己); _iAmHost=false; JoinLobby(found); return

// 双方确认（Update 轮询）
host_ok  = GetLobbyData(lobby,"host_ok")                    // 房主写 lobbydata
guest_ok = _iAmHost ? ReadMemberDataKey("guest_ok")         // 客人写 memberdata
           : (_iAccepted ? "1" : "")
oppOk = _iAmHost ? guest_ok : host_ok
if oppOk == "0": 对方拒绝 → 重排
if host_ok=="1" && guest_ok=="1":
    LobbyConfig.{FromLobby=true, IsHost=_iAmHost, MatchKey=$"aw_{lobbyID}"}
    CaptureRemoteSteamID()                                   // 进游戏前最终捕获对手 SteamID
    JoinGamePanel.Open()
// 15s 倒计时内没双确认 → LeaveLobby + 重新搜索
```

### 3.5 P2P 连接建立（Game 场景，AutoConnect）

```
AutoConnect.Start():
  A) 离线/AI（FromLobby == false）
       SetupKCP()                       // 销毁 Fizzy，保留 KcpTransport，Transport.active = kcp
       NetworkManager.StartHost()
       AddComponent<OfflineAIHost>()    // 造 server-only 的第二个 NetworkPlayer → NetworkPlayer.Remote
       AddComponent<SimpleAI>()

  B) Direct IP（IsDirectIP == true）
       SetupKCP()
       IsHost ? StartHost() : (networkAddress = ServerIP; StartClient())

  C) 在线（FromLobby == true）
       SetupFizzy()                     // 销毁 KCP，保留 FizzySteamworks
       RegisterCallbacks()

       【房主】
         CreateLobby(Public, 2) ──LobbyCreated──→
             SetLobbyData(lobby, "game", MatchKey)      // MatchKey = "aw_{Lobby场景大厅ID}"
             SetLobbyData(lobby, "host_sid", 我的SteamID64)
             NetworkManager.StartHost()

       【客人】
         InvokeRepeating(SearchLobbies, 0, 2s)          // 每 2s 搜一次
         SearchLobbies():
             AddRequestLobbyListStringFilter("game", MatchKey, Equal)
             AddRequestLobbyListDistanceFilter(Worldwide)
             RequestLobbyList()
         ──LobbyMatchList──→ 挑 game == MatchKey 的大厅（兜底取第 0 个）→ JoinLobby
         ──LobbyEnter──→
             hostSid = GetLobbyData(lobby,"host_sid") ?? GetLobbyOwner(lobby)
             NetworkManager.networkAddress = hostSid     // ★ SteamID64 字符串
             Invoke(StartMirrorClient, 1.5f)             // ★ 延迟让 Steam relay/主机 transport 就绪
         StartMirrorClient(): NetworkManager.StartClient()

SetupFizzy() / SetupKCP():
    foreach Transport in NetworkManager.GetComponents<Transport>():
        保留目标类型，其余 DestroyImmediate
    Transport.active = 保留的那个                 // ★ 必须在 StartHost/StartClient 之前
```

**FizzySteamworks 侧**：`ClientConnect(address)` 把 address 当 `UInt64.Parse` → `CSteamID` → `SteamNetworkingSockets.ConnectP2P()`；`ServerStart()` 用 `SteamUser.GetSteamID()` 当自己的身份；`ServerUri()` 返回 `steam://<SteamID64>`。所以 **Mirror 的 `networkAddress` 就是对方的 SteamID64 十进制字符串**。

### 3.6 场景过渡与双端同步

```
LobbyConfig（静态，跨场景唯一载体）
    FromLobby   : 是否在线对局（false → 离线 Host + AI）
    IsHost      : 本端是否房主
    MatchKey    : "aw_{Lobby场景大厅ID}"，Game 场景搜索的过滤键 + 防串线
    CurrentLobbyID / HostSteamID / RemoteSteamID
    IsDirectIP / ServerIP / IsAI

JoinGamePanel.Open():
    显示双方头像/昵称（来自 SteamDataManager + QuickMatchPanel 缓存）
    Preloader.StartPreload()                 // 提前异步加载 CardData/字体/常用预制体
    3s 倒计时 → 等预加载完成（超时 10s 兜底）→ Preloader.LoadGameScene()
        LoadSceneAsync("Game") + allowSceneActivation=false
        → 等预加载 & progress>=0.9 → allowSceneActivation=true

Game 场景启动后：
    AutoConnect 建连（见 §3.5）
    NetworkManager.autoCreatePlayer=1 → 每端自动 Spawn 自己的 NetworkPlayer
    NetworkPlayer.OnStartServer/OnStartClient 里把 Local/Remote 引用接好
    NetworkTurnSync.Update():
        if NetworkServer.active && !gameStarted && Local != null && Remote != null:
            TurnManager.enabled = true
            TurnManager.StartGameForClient()      // 服务端发初始手牌（TargetRpc 到各端）
            gameStarted = true                    // SyncVar → 客户端 OnGameStartedChanged
    首手权：服务端 OnStartServer 随机 isHostFirst；客户端 isMyTurnFirst = !isHostFirst
    阶段推进：服务端 BroadcastTurnPhase(TargetRpc)，不走 SyncVar（各端视角不同）
    板面同步：BoardSyncManager.MarkDirty() → LateUpdate 打包 12 槽快照 → RpcSyncBoard
    手牌/能量：NetworkPlayer 的 SyncVar + 定向 TargetRpc
```

### 3.7 断线 / 取消 / 超时

| 场景 | 处理 |
|---|---|
| 客人中途退出 | 房主轮询 `GetNumLobbyMembers < 2` → 复位房间 UI，等新客人 |
| 房主退出 | 客人轮询 `members < 2` → 自己退房回大厅 |
| 房主踢人 | `SetLobbyData("kicked","1")` → 客人轮询到 → 退房 |
| 匹配中取消 | `LeaveLobby()` + `Dispose()` 所有 `Callback<T>` 句柄 |
| 搜索超时 | JoinRoom 5s / AutoConnect 30s（打错误日志）/ 60s（停止并提示） |
| 加入超时 | `LobbyEnter_t` 10s 没回调 → 提示"加入失败" |
| 确认超时 | QuickMatch 15s 倒计时 → 重新匹配 |
| P2P 连接超时 | FizzySteamworks `Timeout`（默认 25s）→ 触发 `OnClientDisconnected` |
| 对局中断线 | `NetworkClient.OnDisconnectedEvent` / `NetworkServer.OnDisconnectedEvent` → 2s 提示 → `StopHost/StopClient` → `Destroy(NetworkManager)` → 回 Lobby 场景 |

```csharp
// AutoConnect 的断线统一入口
void OnDisconnected()            { if (对局已开始) ReturnToLobby("连接断开"); }
void OnServerDisconnected(conn)  { if (conn.identity 是 Remote) ReturnToLobby("对手已断开连接"); }
ReturnToLobby(reason):
    2s 黑幕提示 → StopHost/StopClient → 销毁常驻物体 → SceneManager.LoadScene("Lobby")
```

---

## 4. 常见坑（血泪清单）

### 4.1 权限：房主 vs 客人
- **只有大厅 owner 能 `SetLobbyData`**（房主）。客人调用会静默失败 → 客人必须用 `SetLobbyMemberData`。
- 因此约定：**房主的公开信息写 lobby data（`host_data` / `host_sid` / `start` / `kicked` / `host_ok`），客人的信息写 member data（`player_data` / `guest_ok`）**。
- 读取方向相反：房主遍历 `GetLobbyMemberByIndex` 读客人；客人直接 `GetLobbyData` 读房主。
- 房主读 member data **必须先 `RequestLobbyData`**，否则读的是过期/空缓存（见 4.3）。

### 4.2 回调冲突（最坑）
- `Callback<T>.Create()` 返回的句柄**必须 `Dispose()`**。不释放 → 多个面板/多次搜索会同时收到同一事件，重复处理（典型：打开 JoinRoomPanel 又开 QuickMatchPanel，`LobbyMatchList_t` 被两边都消费）。
- **面板互斥**：`LobbyManager` 打开一个面板前先关掉其它面板（关 = 面板隐藏 + 释放回调）。
- **前台/后台搜索必须用两个独立句柄**：QuickMatch 自建大厅后启动后台搜索，此时要 `Dispose()` 掉前台的 `LobbyMatchList_t` 句柄，否则每次后台 `RequestLobbyList` 都会同时触发前台回调，把 `_iAmHost` 清成 false 并 `JoinLobby(自己的大厅)` → 永远匹配不到。
- `CreateLobby` 成功后 Steam **也会触发 `LobbyEnter_t`**（进入自己的大厅，members==1）。房主分支里不要因为收到 `LobbyEnter` 就停止后台搜索。

### 4.3 索引/缓存延迟
- `GetLobbyMemberData` 读的是**本地缓存**。刚 `SetLobbyMemberData` 或对方刚写，**必须 `RequestLobbyData(lobby)`** 再读，否则一直是空。
- 大厅列表有**索引延迟**（几秒）。刚 `CreateLobby` 完立刻 `RequestLobbyList` 可能搜不到 → 必须**重试轮询**（0.5s × 10 / 每 2s / 每 3s）。
- 默认 `ELobbyDistanceFilter` 只返回**同数据中心**的大厅，双方挂不同加速器/地区会互搜不到 → **显式 `AddRequestLobbyListDistanceFilter(k_ELobbyDistanceFilterWorldwide)`**。
- 搜索结果可能包含自己的大厅 → 用 `found == 自己lobbyID` 过滤。

### 4.4 双人同时自建（匹配死锁）
- A 和 B 同时点匹配，都搜不到对方 → 都自建 → 各自等对方加入 → 永远匹配不上。
- 解法：**自建成功后启动"后台搜索"**，一旦搜到另一个"单人等待中"的大厅，就**放弃自己的大厅去加入对方**。判定对方可用：`members < 2 && host_ok != "1" && start != "1"`。

### 4.5 Steam 未登录/未连接
- `SteamManager.Initialized == true` 只代表 `SteamAPI_Init()` 成功，**不代表已连上 Steam 后端**。
- 无网/被墙/加速器未生效时 `CreateLobby` / `RequestLobbyList` 会以 `k_EResultNoConnection` **静默失败**（回调不触发或返回错误），面板会永久卡在"匹配中"。
- 做法：调用前先 `if (!SteamUser.BLoggedOn()) { 明确报错 + return; }`；`LobbyCreated` 里检查 `cb.m_eResult != k_EResultOK` 并显示失败原因。

### 4.6 跨场景状态
- 场景切换会清空场景对象 → 用**静态类**（`LobbyConfig`）传状态；不要靠 `FindObjectOfType` 跨场景。
- `MatchKey = "aw_{大厅ID}"` 是**防串线关键**：多个房间同时进 Game 场景时，客户端只搜自己那组的 key，避免连到陌生人的主机。
- Game 场景会**再建一个大厅**（不是复用 Lobby 场景那个）：它只是"握手大厅"，作用是把房主 SteamID 交给客户端。若两处 `game` 字段不一致（例如一处写死 `"anotherworld"`、另一处写 `"anotherworld_quick"`），搜不到。

### 4.7 Transport 与地址
- `Transport.active` 必须在 `StartHost/StartClient` **之前**设好；两个 Transport 同时挂在 NetworkManager 上时，运行时销毁不需要的那个（`DestroyImmediate`）。
- `networkAddress` 对 FizzySteamworks 而言是**对方 SteamID64 的十进制字符串**，不是 IP。
- 客户端拿到 `host_sid` 后**延迟 1.5s** 再 `StartClient()`：给 Steam relay 初始化 / 主机 transport 就绪留时间。
- 打包后必须有 `steam_appid.txt`（或用正式 AppID），否则 `SteamAPI.Init()` 失败。

### 4.8 其它
- `SteamManager.Update()` 里的 `SteamAPI.RunCallbacks()` 是所有 `Callback<T>` 的生命线，漏了就没回调。
- 房间号只是**自己约定的 6 位随机字符串**（存在 lobby data 的 `room_code` 里），用字符串过滤搜出来——Steam 没有"按房间号加入"的原生 API。
- 头像：`GetLargeFriendAvatar` 返回 -1 表示未加载，需要 `RequestUserInformation` + 后续轮询；Steam 头像像素是上下颠倒的，要翻转 Y。

---

## 5. 关键代码结构速查

### 5.1 类职责表
| 层 | 类 | 负责 |
|---|---|---|
| Steam 底座 | `SteamManager` | Init/RunCallbacks/Shutdown，单例常驻 |
| 玩家资料 | `SteamDataManager` / `SteamAvatarManager` | 昵称、SteamID、头像、本地存档 |
| 撮合 | `CreateRoomPanel` / `JoinRoomPanel` / `QuickMatchPanel` | 三种入口的大厅生命周期 |
| 状态载体 | `LobbyConfig` | 跨场景传 FromLobby/IsHost/MatchKey/SteamID |
| 过渡 | `JoinGamePanel` / `Preloader` | 倒计时 + 预加载 + 异步场景激活 |
| 连接编排 | `AutoConnect` | 选 Transport、建房/搜房、StartHost/Client、断线回大厅 |
| 对局同步 | `NetworkTurnSync` / `NetworkPlayer` / `BoardSyncManager` | 开局门闩、玩家状态、板面快照 |
| 离线 | `OfflineAIHost` | 造 server-only 的 AI NetworkPlayer |

### 5.2 RPC 用法约定
| 类型 | 用途 | 例子 |
|---|---|---|
| `[SyncVar]` | 少量、双端一致的权威状态 | `isHostFirst` / `gameStarted` / 血量/能量 |
| `[Command]` | 客户端 → 服务器 | `CmdRequestEndTurn()` / `CmdPlayCard(...)` |
| `[TargetRpc]` | 服务器 → 指定客户端（定向） | 发初始手牌 `TargetReceiveCard(conn, ...)` / 广播阶段 `BroadcastTurnPhase` |
| `[ClientRpc]` | 服务器 → 所有客户端 | `RpcSyncBoard(...)`（板面快照） |
| Steam `Callback<T>` | Steam 事件 | `LobbyCreated_t` / `LobbyMatchList_t` / `LobbyEnter_t` / `LobbyDataUpdate_t` |

**注意**：`NetworkTurnSync` 是**场景对象**（不是 player 对象），`isLocalPlayer` 恒为 false，判断"我是谁"要用 `isServer`；首手权客户端要取反（`isMyTurnFirst = isServer ? sv : !sv`）。

---

## 6. 最小可复刻 Checklist

1. [ ] 导入 Steamworks.NET + Mirror + FizzySteamworks；`steam_appid.txt` 就位。
2. [ ] 建 `SteamManager`（常驻，每帧 `RunCallbacks`）、`SteamDataManager`（昵称/头像/存档）。
3. [ ] 建 `LobbyConfig` 静态类（FromLobby / IsHost / MatchKey / HostSteamID / RemoteSteamID）。
4. [ ] Lobby 场景：三个面板 + `LobbyManager` 互斥；每个面板的 `Callback<T>` 在 `Close/OnDestroy` 里 `Dispose`。
5. [ ] 约定 lobby data key：`game`(项目key) / `room_code` / `host_data` / `host_sid` / `start` / `kicked` / `host_ok`；member data key：`player_data` / `guest_ok`。
6. [ ] 搜索一律加 `Worldwide` 距离过滤 + 重试轮询。
7. [ ] QuickMatch 自建后启动后台搜索 + 前台回调 Dispose（防双自建死锁 + 回调串台）。
8. [ ] Game 场景：NetworkManager（dontDestroyOnLoad + autoCreatePlayer）同物体挂 Fizzy + KCP，`AutoConnect` 运行时二选一。
9. [ ] `AutoConnect`：房主 `CreateLobby` → `StartHost`；客人搜 `game == MatchKey` → `JoinLobby` → `networkAddress = host_sid` → 延迟 1.5s → `StartClient`。
10. [ ] `NetworkTurnSync` 用 `gameStarted` 门闩 + 双方 NetworkPlayer 就绪后开局。
11. [ ] 断线：`OnDisconnected` / `OnServerDisconnectedEvent` → 提示 → Stop → 回 Lobby。
12. [ ] 所有 Steam 调用前 `SteamUser.BLoggedOn()` 预检；所有回调检查 `m_eResult`；所有等待加超时。
