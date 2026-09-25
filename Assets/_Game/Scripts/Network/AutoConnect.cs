using UnityEngine;
using Mirror;
using System.Net.Sockets;
using TMPro;
using Steamworks;
using UnityEngine.SceneManagement;

public class AutoConnect : MonoBehaviour
{
    private TurnManager _turnManager;
    private GameObject _waitingUI;
    private NetworkManager _nm;
    private float _startTime;
    private bool _returningToLobby;
    private bool _hostReadyShown;

    void Awake()
    {
        _nm = FindObjectOfType<NetworkManager>();
        _turnManager = FindObjectOfType<TurnManager>();
        CreateWaitingUI();
        if (!LobbyConfig.FromLobby) { HideUI(); return; }
        NetworkClient.OnConnectedEvent += OnConnected;
        NetworkClient.OnDisconnectedEvent += OnDisconnected;
        NetworkServer.OnDisconnectedEvent += OnServerDisconnected;
    }

    void Start()
    {
        if (!LobbyConfig.FromLobby)
        {
            // 离线单机模式：启动本地 Host（KCP），让 Mirror 创建 NetworkPlayer.Local。
            // 不注册 Steam 回调、不碰 FizzySteamworks，纯本地 KCP host。
            SetupKCP();
            _startTime = Time.time;
            Debug.LogWarning($"[AutoConnect-Offline] 离线模式启动本地 Host @{Time.time:F2}s");
            StartHostOffline();
            // 挂载离线 AI 创建器：等 Local 就绪后创建 AI 的 NetworkPlayer 并赋 Remote
            if (_nm != null && _nm.GetComponent<OfflineAIHost>() == null)
                _nm.gameObject.AddComponent<OfflineAIHost>();
            // 挂载 AI 决策组件
            if (_nm != null && _nm.GetComponent<SimpleAI>() == null)
                _nm.gameObject.AddComponent<SimpleAI>();
            return;
        }
        if (_turnManager != null) _turnManager.enabled = false;
        _startTime = Time.time;
        Debug.LogWarning($"[AutoConnect-Timing] Start — 场景加载完成, 网络连接开始 @{Time.time:F2}s");

        // Direct IP path — bypass Steam entirely for local/self-test
        if (LobbyConfig.IsDirectIP)
        {
            SetupKCP();
            if (LobbyConfig.IsHost)
            {
                SetText("正在创建本地房间...");
                _nm.StartHost();
            }
            else
            {
                SetText($"正在连接 {LobbyConfig.ServerIP} ...");
                _nm.networkAddress = LobbyConfig.ServerIP;
                _nm.StartClient();
            }
            return;
        }

        if (!SteamManager.Initialized)
        {
            SetText("Steam 未就绪\n请先启动 Steam 客户端\n或在输入框填写对方 IP");
            return;
        }

        if (LobbyConfig.IsHost)
        {
            Debug.LogWarning($"[AutoConnect-Timing] Host → 调用 CreateLobby @{Time.time - _startTime:F2}s");
            SetText("正在建立连接 (1/3)...");
            SetupFizzy();
            RegisterCallbacks();
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 2);
        }
        else
        {
            SetupFizzy();
            RegisterCallbacks();

            // 大厅阶段双方已经交换过 SteamID（QuickMatchPanel 写进 LobbyConfig）——
            // 直接连房主，不再依赖 Steam 大厅搜索：房主那间给 Mirror 用的临时大厅是他**进战斗场景后**
            // 才建的，靠 game=aw_<大厅ID> 过滤去搜要等 Steam 索引收录，收不到就会一直停在"正在搜索对手"。
            string hostSid = LobbyConfig.HostSteamID;
            if (string.IsNullOrEmpty(hostSid) && LobbyConfig.RemoteSteamID != 0)
                hostSid = LobbyConfig.RemoteSteamID.ToString();

            if (!string.IsNullOrEmpty(hostSid))
            {
                Debug.LogWarning($"[AutoConnect-Timing] Client → 直连房主 @{Time.time - _startTime:F2}s host={hostSid}（跳过大厅搜索）");
                StartCoroutine(DirectConnectRoutine(hostSid));
                return;
            }

            Debug.LogWarning($"[AutoConnect-Timing] Client → 没有房主 SteamID，回退搜索大厅 @{Time.time - _startTime:F2}s");
            SetText("正在搜索对手 (1/2)...");
            InvokeRepeating(nameof(SearchLobbies), 0f, 2f);
        }
    }

    /// <summary>离线单机启动本地 Host。上一次运行残留的实例（比如还开着的构建版 exe）
    /// 或另一个 Unity 编辑器可能还占着默认端口 7777 —— 原来直接 StartHost() 会抛
    /// SocketException，服务器起不来，游戏就卡在准备阶段。这里绑不上就往后换端口重试，
    /// 单机模式下服务端与本地客户端用的是同一个端口，换端口对玩法没有影响。</summary>
    void StartHostOffline()
    {
        const int maxTries = 8;
        for (int attempt = 0; attempt < maxTries; attempt++)
        {
            try
            {
                _nm.StartHost();
                if (attempt > 0)
                    Debug.LogWarning($"[AutoConnect-Offline] 默认端口被占用，已改用端口 {PortNow()} 启动本地 Host");
                return;
            }
            catch (SocketException e)
            {
                Debug.LogWarning($"[AutoConnect-Offline] 端口 {PortNow()} 绑定失败（{e.SocketErrorCode}）：{e.Message}");
                try { _nm.StopHost(); } catch { }
                if (!NextPort())
                {
                    Debug.LogError("[AutoConnect-Offline] 传输不支持换端口，无法启动本地 Host；" +
                                   "请确认没有第二个游戏实例（含构建版 exe）还在运行。");
                    return;
                }
            }
        }
        Debug.LogError($"[AutoConnect-Offline] 连试 {maxTries} 个端口都绑不上，放弃启动本地 Host；" +
                       "请确认没有第二个游戏实例（含构建版 exe）还在运行。");
    }

    int PortNow()
    {
        return Transport.active is PortTransport pt ? pt.Port : -1;
    }

    bool NextPort()
    {
        if (!(Transport.active is PortTransport pt)) return false;
        pt.Port = (ushort)(pt.Port + 1);
        return true;
    }

    void SetupFizzy()
    {
        // Remove KCP, keep FizzySteamworks
        var all = _nm.gameObject.GetComponents<Transport>();
        bool hasFizzy = false;
        foreach (var t in all)
        {
            if (t.GetType().Name.Contains("Fizzy") || t.GetType().Name.Contains("Steam")) { hasFizzy = true; continue; }
            DestroyImmediate(t);
        }
        if (!hasFizzy)
        {
            Debug.LogError("[AutoConnect] FizzySteamworks not found on NetworkManager! Add it in the Inspector.");
        }
    }

    void SetupKCP()
    {
        // Remove FizzySteamworks, keep KCP transport for direct IP
        var all = _nm.gameObject.GetComponents<Transport>();
        Transport kcp = null;
        foreach (var t in all)
        {
            if (t.GetType().Name.Contains("Kcp") || t.GetType().Name.Contains("KCP")) { kcp = t; continue; }
            DestroyImmediate(t);
        }
        if (kcp != null)
        {
            Transport.active = kcp;
        }
        else
        {
            Debug.LogError("[AutoConnect] KcpTransport not found on NetworkManager! Add it in the Inspector.");
        }
    }

    Callback<LobbyCreated_t> _lcb;
    Callback<LobbyMatchList_t> _llcb;
    Callback<LobbyEnter_t> _leb;
    void RegisterCallbacks()
    {
        _lcb?.Dispose(); _llcb?.Dispose(); _leb?.Dispose();
        _lcb = Callback<LobbyCreated_t>.Create(r =>
        {
            if (r.m_eResult != EResult.k_EResultOK) { SetText("创建房间失败"); return; }
            var lid = new CSteamID(r.m_ulSteamIDLobby);
            string matchKey = !string.IsNullOrEmpty(LobbyConfig.MatchKey) ? LobbyConfig.MatchKey : "anotherworld";
            SteamMatchmaking.SetLobbyData(lid, "game", matchKey);
            SteamMatchmaking.SetLobbyData(lid, "host_sid", SteamUser.GetSteamID().m_SteamID.ToString());
            Debug.Log($"[AutoConnect] Lobby {lid}, host SteamID64: {SteamUser.GetSteamID().m_SteamID}");
            Debug.LogWarning($"[AutoConnect-Timing] LobbyCreated 回调 @{Time.time - _startTime:F2}s — StartHost 即将执行");
            SetText("正在建立连接 (2/3)...");
            _nm.StartHost();
            // Mirror 的 host 模式**不会**触发 NetworkClient.OnConnectedEvent
            // （NetworkClient.ConnectHost() 直接置 connectState=Connected、走 HostMode.SetupConnections()，
            //   不经过 OnTransportConnected()），所以 OnConnected 里那句文案永远执行不到 ——
            // 不自己补一刀，黑幕上的字就会永远停在 (2/3)，看起来像"卡在建立连接"。
            ShowHostReady();
        });
        _llcb = Callback<LobbyMatchList_t>.Create(r =>
        {
            string matchKey = !string.IsNullOrEmpty(LobbyConfig.MatchKey) ? LobbyConfig.MatchKey : "anotherworld";
            float elapsed = Time.time - _startTime;
            Debug.Log($"[AutoConnect-Search] 搜到 {r.m_nLobbiesMatching} 个大厅 (game过滤={matchKey}, 已用时 {elapsed:F1}s)");

            if (r.m_nLobbiesMatching == 0)
            {
                // 30 秒仍搜不到 → 错误日志定位（Steam 索引延迟 / 距离过滤 / game 不匹配）
                if (elapsed > 30f)
                    Debug.LogError($"[AutoConnect-Search] 30 秒仍搜不到 Host 大厅（过滤 game={matchKey}）——检查 Steam 索引延迟、双方 game 字段是否一致、appid");
                return;
            }

            // 打印每个大厅详情，确认 game 字段匹配
            for (int i = 0; i < (int)r.m_nLobbiesMatching; i++)
            {
                CSteamID lid = SteamMatchmaking.GetLobbyByIndex(i);
                string g = SteamMatchmaking.GetLobbyData(lid, "game") ?? "";
                int m = SteamMatchmaking.GetNumLobbyMembers(lid);
                Debug.Log($"[AutoConnect-Search]   大厅[{i}] id={lid.m_SteamID} game={g} members={m}");
            }

            // 选 game 字段完全匹配的大厅（过滤已匹配，但遍历确认，避免取到非目标）
            CSteamID target = default;
            for (int i = 0; i < (int)r.m_nLobbiesMatching; i++)
            {
                CSteamID cand = SteamMatchmaking.GetLobbyByIndex(i);
                if ((SteamMatchmaking.GetLobbyData(cand, "game") ?? "") == matchKey)
                { target = cand; break; }
            }
            if (target.m_SteamID == 0)
                target = SteamMatchmaking.GetLobbyByIndex(0); // 兜底取第一个

            CancelInvoke(nameof(SearchLobbies));
            Debug.LogWarning($"[AutoConnect-Timing] LobbyMatchList 回调 @{elapsed:F2}s — 找到房间 {target.m_SteamID}, 正在 JoinLobby");
            SetText("找到对手, 正在加入...");
            SteamMatchmaking.JoinLobby(target);
        });
        _leb = Callback<LobbyEnter_t>.Create(r =>
        {
            if (LobbyConfig.IsHost) return;
            var lid = new CSteamID(r.m_ulSteamIDLobby);
            string hostSid = SteamMatchmaking.GetLobbyData(lid, "host_sid");
            if (string.IsNullOrEmpty(hostSid))
                hostSid = SteamMatchmaking.GetLobbyOwner(lid).m_SteamID.ToString();
            Debug.Log($"[AutoConnect] LobbyEnter — host SteamID64={hostSid}");
            Debug.LogWarning($"[AutoConnect-Timing] LobbyEnter 回调 @{Time.time - _startTime:F2}s — 准备 StartMirrorClient(1.5s延迟)");
            _nm.networkAddress = hostSid;
            SetText("正在连接 Steam P2P (2/2)...");
            Invoke(nameof(StartMirrorClient), 1.5f);
        });
    }

    void StartMirrorClient()
    {
        Debug.Log($"[AutoConnect] StartMirrorClient — transport={_nm.transport?.GetType().Name}");
        _nm.StartClient();
    }

    /// <summary>Guest 直连房主（SteamID 来自大厅阶段）。房主可能还没开始监听，所以分段重试；
    /// 每次失败先 StopClient 清掉 transport 里的旧 client（否则再 StartClient 会撞 "Client already running!"）。
    /// 全部失败再回退到"搜大厅"那条老路。</summary>
    System.Collections.IEnumerator DirectConnectRoutine(string hostSid)
    {
        const int maxTries = 6;
        for (int attempt = 1; attempt <= maxTries; attempt++)
        {
            if (NetworkClient.isConnected) yield break;
            if (attempt > 1) { try { _nm.StopClient(); } catch { } }

            SetText($"正在连接房主 (2/2)... {attempt}/{maxTries}");
            Debug.LogWarning($"[AutoConnect-Client] 直连房主 SteamID={hostSid}（第 {attempt}/{maxTries} 次）");
            _nm.networkAddress = hostSid;
            _nm.StartClient();

            float deadline = Time.time + 4f;
            while (Time.time < deadline && !NetworkClient.isConnected) yield return null;
            if (NetworkClient.isConnected)
            {
                Debug.LogWarning($"[AutoConnect-Client] 已连上房主 @{Time.time - _startTime:F2}s");
                yield break;
            }
        }

        Debug.LogError("[AutoConnect-Client] 直连房主失败，回退搜索大厅");
        SetText("正在搜索对手 (1/2)...");
        InvokeRepeating(nameof(SearchLobbies), 0f, 2f);
    }

    /// <summary>Host 就绪后的黑幕文案（服务端已监听、本地客户端已连上）。
    /// Mirror 在 host 模式下不给 OnConnected 回调，只能自己推；见 LobbyCreated 回调里的说明。</summary>
    void ShowHostReady()
    {
        if (!NetworkServer.active || !NetworkClient.isConnected) return;
        if (!_hostReadyShown)
        {
            _hostReadyShown = true;
            Debug.LogWarning($"[AutoConnect-Timing] Host 就绪 — 服务端已监听，等待对手接入 @{Time.time - _startTime:F2}s");
        }
        SetText("已连接, 等待对手加入...");
    }

    void SearchLobbies()
    {
        if (NetworkClient.isConnected || NetworkServer.active) { CancelInvoke(nameof(SearchLobbies)); return; }
        if (Time.time - _startTime > 60f) { CancelInvoke(nameof(SearchLobbies)); SetText("搜索超时"); return; }
        string matchKey = !string.IsNullOrEmpty(LobbyConfig.MatchKey) ? LobbyConfig.MatchKey : "anotherworld";
        SteamMatchmaking.AddRequestLobbyListStringFilter("game", matchKey, ELobbyComparison.k_ELobbyComparisonEqual);
        // 显式世界范围——默认只返回同数据中心的大厅，Host 在不同数据中心时 Client 搜不到（与 Lobby 场景同一根因）
        SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
        SteamMatchmaking.RequestLobbyList();
    }

    void CreateWaitingUI()
    {
        _waitingUI = new GameObject("NetworkWaiting"); DontDestroyOnLoad(_waitingUI);
        var c = _waitingUI.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 999;
        _waitingUI.AddComponent<UnityEngine.UI.CanvasScaler>(); _waitingUI.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        var p = new GameObject("Panel"); p.transform.SetParent(_waitingUI.transform, false);
        p.AddComponent<UnityEngine.UI.Image>().color = new Color(0,0,0,0.85f);
        var pr = p.GetComponent<RectTransform>(); pr.anchorMin=Vector2.zero; pr.anchorMax=Vector2.one; pr.offsetMin=Vector2.zero; pr.offsetMax=Vector2.zero;
        var t = new GameObject("Text"); t.transform.SetParent(_waitingUI.transform, false);
        var tmp = t.AddComponent<TextMeshProUGUI>(); tmp.fontSize=26; tmp.color=Color.white; tmp.alignment=TextAlignmentOptions.Center;
        var f = Resources.Load<TMP_FontAsset>("Fonts & Materials/NotoSansSC SDF"); if(f!=null) tmp.font=f;
        var tr = t.GetComponent<RectTransform>(); tr.anchorMin=new Vector2(0.05f,0.1f); tr.anchorMax=new Vector2(0.95f,0.9f); tr.offsetMin=Vector2.zero; tr.offsetMax=Vector2.zero;
    }
    void SetText(string m) { var t=_waitingUI?.GetComponentInChildren<TextMeshProUGUI>(); if(t!=null) t.text=m; }
    void HideUI() { if(_waitingUI!=null) _waitingUI.SetActive(false); }
    void ShowUI(string msg) { if(_waitingUI!=null) { _waitingUI.SetActive(true); SetText(msg); } }
    void OnConnected(){
        Debug.LogWarning($"[AutoConnect-Timing] OnConnected — 连接建立 @{Time.time - _startTime:F2}s");
        SetText(NetworkServer.active?"正在建立连接 (3/3)...":"已连接, 等待对手...");
    }
    void OnDisconnected()
    {
        // Only react if game was in progress, not during lobby/connecting phase
        if (_turnManager == null || !_turnManager.enabled) return;
        ReturnToLobby("连接断开");
    }
    void OnServerDisconnected(NetworkConnectionToClient conn)
    {
        // Only react if game was in progress and the remote player dropped
        if (_turnManager == null || !_turnManager.enabled) return;
        if (NetworkPlayer.Remote != null && conn.identity?.GetComponent<NetworkPlayer>() == NetworkPlayer.Remote)
            ReturnToLobby("对手已断开连接");
    }
    void ReturnToLobby(string reason)
    {
        if (_returningToLobby) return;
        _returningToLobby = true;
        Debug.Log($"[AutoConnect] ReturnToLobby: {reason}");
        ShowUI($"{reason}\n即将返回大厅...");
        StartCoroutine(DoReturnToLobby());
    }
    System.Collections.IEnumerator DoReturnToLobby()
    {
        yield return new WaitForSeconds(2f);
        if (NetworkServer.active) _nm.StopHost();
        else if (NetworkClient.isConnected) _nm.StopClient();
        // Destroy DontDestroyOnLoad objects from this session
        if (_waitingUI != null) { Destroy(_waitingUI); _waitingUI = null; }
        if (_nm != null) { Destroy(_nm.gameObject); _nm = null; }
        SceneManager.LoadScene("Lobby");
    }
    void OnDestroy(){ _lcb?.Dispose(); _llcb?.Dispose(); _leb?.Dispose(); NetworkClient.OnConnectedEvent-=OnConnected; NetworkClient.OnDisconnectedEvent-=OnDisconnected; NetworkServer.OnDisconnectedEvent-=OnServerDisconnected; }
    void Update(){
        if(_waitingUI==null||!_waitingUI.activeSelf)return;
        if(!_hostReadyShown) ShowHostReady();   // host 模式文本兜底（StartHost 抛异常时不会走到这里）
        if(_turnManager!=null&&_turnManager.enabled&&NetworkTurnSync.Instance!=null&&NetworkTurnSync.Instance.gameStarted){
            Debug.LogWarning($"[AutoConnect-Timing] 黑幕隐藏 — gameStarted=true @{Time.time - _startTime:F2}s 总耗时");
            _waitingUI.SetActive(false);
        }
    }
}
