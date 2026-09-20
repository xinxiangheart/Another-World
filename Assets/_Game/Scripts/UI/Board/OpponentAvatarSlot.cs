using UnityEngine;

/// <summary>
/// 顶栏左侧常驻的「对方头像」显示：底色直接借用空白圆环（ring_empty），头像走圆环同款圆形遮罩。
/// 与阶段轮盘共用同一套数据源与判定（AI 对战 / 未捕获对方 SteamID → 无头像）；
/// 取不到头像时与阶段环的空状态一致 —— 只留空白圆环，不显示灰色占位头像。
/// Steam 头像是异步加载的：未就绪时保持空白环，缓存到位后自动换上，最多轮询 pollSeconds 秒。
/// </summary>
public class OpponentAvatarSlot : MonoBehaviour
{
    [Tooltip("承载头像的圆环（RingBackground / AvatarMask / AvatarImage）")]
    public RingSlot slot;
    [Tooltip("轮询对方头像的总时长（秒）；超时后保持当前显示，不再刷新")]
    public float pollSeconds = 10f;
    [Tooltip("轮询间隔（秒）")]
    public float pollInterval = 0.25f;

    float _elapsed;
    float _nextPoll;
    bool _settled;
    bool _requested;

    void Awake()
    {
        if (slot == null) slot = GetComponentInChildren<RingSlot>(true);
    }

    void OnEnable()
    {
        _elapsed = 0f;
        _nextPoll = 0f;
        _settled = false;
        _requested = false;
        Refresh();
    }

    void Update()
    {
        if (_settled) return;
        _elapsed += Time.unscaledDeltaTime;
        if (_elapsed >= pollSeconds) { _settled = true; return; }
        if (Time.unscaledTime < _nextPoll) return;
        _nextPoll = Time.unscaledTime + pollInterval;
        Refresh();
    }

    /// <summary>刷新一次：有真头像就换上并停轮询；没有（AI 对战 / 未捕获 SteamID / 尚未加载完成）就回到空白环。</summary>
    void Refresh()
    {
        if (slot == null) return;
        // AI 对战：AI 没有 SteamID，头像恒为空 —— 与阶段环判定一致，无需继续等
        if (SimpleAI.IsAIMatch) { slot.SetEmpty(); _settled = true; return; }
        ulong sid = LobbyConfig.RemoteSteamID;
        // SteamID 可能晚到（网络 SyncVar / 大厅捕获），先保持空白环继续等
        if (sid == 0) { slot.SetEmpty(); return; }
        Texture2D tex = SteamAvatarManager.PeekAvatar(sid);
        if (tex != null) { slot.SetAvatar(tex); _settled = true; return; }
        slot.SetEmpty();
        // 未缓存：只催一次加载（内部会请求 Steam 并自行轮询），之后静默等缓存命中
        if (!_requested) { _requested = true; SteamAvatarManager.GetAvatarTexture(sid); }
    }
}
