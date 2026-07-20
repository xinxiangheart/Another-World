using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 服务端权威卡牌注册中心 —— 维护双方全量状态，支持增量推送与通用查询。
/// 与 BoardSyncManager 同模式：普通 MonoBehaviour，RPC 通过 NetworkPlayer.Local 路由。
/// 不替换/不修改任何现有代码路径。
/// </summary>
public class RegistrySyncManager : MonoBehaviour
{
    public static RegistrySyncManager Instance { get; private set; }

    // ═══════════════════ 服务端权威数据 ═══════════════════
    PlayerStateRegistry _localReg = new PlayerStateRegistry();
    PlayerStateRegistry _remoteReg = new PlayerStateRegistry();

    // ═══════════════════ 客户端镜像 ═══════════════════
    PlayerStateRegistry _mirror = new PlayerStateRegistry();

    // ═══════════════════ 脏标记 ═══════════════════
    HashSet<string> _dirty = new HashSet<string>();
    bool _statsDirty;
    float _nextSync;
    const float SYNC_MS = 0.05f;

    /// <summary>
    /// 自举：若场景/预制体中不存在则自动创建，保证 Instance 永远非 null。
    /// 在 Awake 之前执行（RuntimeInitializeLoadType.BeforeSceneLoad）。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EnsureExists()
    {
        if (Instance != null) return;
        var go = new GameObject("RegistrySyncManager");
        DontDestroyOnLoad(go);
        go.AddComponent<RegistrySyncManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (CardZoneManager.Instance != null)
            CardZoneManager.Instance.OnCardZoneChanged += OnZoneChanged;
        Debug.Log("[RegSync] 启动");
    }

    void LateUpdate()
    {
        if (!NetworkServer.active) return;
        if (Time.time < _nextSync) return;
        _nextSync = Time.time + SYNC_MS;
        if (_dirty.Count == 0 && !_statsDirty) return;
        Flush();
    }

    void OnDestroy()
    {
        if (CardZoneManager.Instance != null)
            CardZoneManager.Instance.OnCardZoneChanged -= OnZoneChanged;
    }

    // ═══════════════════ Public API ═══════════════════

    public void MarkDirty(string iid) { if (!string.IsNullOrEmpty(iid)) _dirty.Add(iid); }
    public void MarkStatsDirty() { _statsDirty = true; }

    public void UpdateCard(CardInstance ci, int ownerIndex, CardZone zone, int slotID)
    {
        if (ci == null || !NetworkServer.active) return;
        var reg = ownerIndex == 0 ? _localReg : _remoteReg;
        reg.Upsert(CardStateProto.FromCardInstance(ci, zone, slotID, ownerIndex));
        MarkDirty(ci.instanceID);
    }

    public void Remove(string iid, int ownerIndex)
    {
        if (string.IsNullOrEmpty(iid) || !NetworkServer.active) return;
        (ownerIndex == 0 ? _localReg : _remoteReg).Remove(iid);
        MarkDirty(iid);
    }

    public List<CardStateProto> GetOpponentHand()
    {
        if (NetworkServer.active) { SnapshotStats(); return _remoteReg.GetHandCards(); }
        return _mirror.GetHandCards();
    }

    public List<CardStateProto> GetOpponentBoard()
    {
        if (NetworkServer.active) return _remoteReg.GetBoardCards();
        return _mirror.GetBoardCards();
    }

    public PlayerStateRegistry GetRegistry(int i) => i == 0 ? _localReg : _remoteReg;

    // ═══════════════════ 增量推送 ═══════════════════

    void SnapshotStats()
    {
        var lp = NetworkPlayer.Local;
        var rp = NetworkPlayer.Remote;
        if (lp != null) { _localReg.currentHealth = lp.currentHealth; _localReg.currentEnergy = lp.currentEnergy; }
        if (rp != null)
        {
            if (_remoteReg.currentHealth != rp.currentHealth || _remoteReg.currentEnergy != rp.currentEnergy)
                _statsDirty = true;
            _remoteReg.currentHealth = rp.currentHealth;
            _remoteReg.currentEnergy = rp.currentEnergy;
        }
    }

    void Flush()
    {
        SnapshotStats();
        if (NetworkPlayer.Remote == null) return;

        string payload = PackDelta(_localReg, _remoteReg);
        if (!string.IsNullOrEmpty(payload))
            NetworkPlayer.Local.RpcSyncRegistry(NetworkPlayer.Remote.connectionToClient, payload);

        _dirty.Clear();
        _statsDirty = false;
    }

    string PackDelta(PlayerStateRegistry target, PlayerStateRegistry other)
    {
        var czm = CardZoneManager.Instance;
        string stats = _statsDirty
            ? $"S|{target.currentHealth}|{target.currentEnergy}|{czm?.DeckCount ?? 0}|{czm?.GraveyardCount ?? 0}"
            : "";

        var changed = new List<string>();
        var removed = new List<string>();
        foreach (var iid in _dirty)
        {
            var c = target.GetCard(iid);
            if (c.HasValue)
                changed.Add($"C|{iid}|{(int)c.Value.zone}|{c.Value.slotID}|{c.Value.SerializeCard()}");
            else if (!other.GetCard(iid).HasValue)
                removed.Add($"R|{iid}");
        }

        if (stats == "" && changed.Count == 0 && removed.Count == 0) return "";
        // 分隔符 ~|~ 和 ~||~ 互不包含，避免 changed 为空时与 section 分隔符合并
        return (stats == "" ? "-" : stats) + "~||~" +
               string.Join("~|~", changed) + "~||~" +
               string.Join("~|~", removed);
    }

    void OnZoneChanged(string iid, CardZone from, CardZone to)
    {
        // 在 _localReg 和 _remoteReg 中查找该卡并更新 zone
        var card = _localReg.GetCard(iid);
        if (card.HasValue)
        {
            var u = card.Value; u.zone = to;
            _localReg.Upsert(u);
        }
        else
        {
            card = _remoteReg.GetCard(iid);
            if (card.HasValue)
            {
                var u = card.Value; u.zone = to;
                _remoteReg.Upsert(u);
            }
        }
        MarkDirty(iid);
    }

    // ═══════════════════ 客户端：应用增量 ═══════════════════

    public void ApplyDelta(string payload)
    {
        if (string.IsNullOrEmpty(payload)) return;
        string[] sections = payload.Split(new[] { "~||~" }, System.StringSplitOptions.None);
        if (sections.Length < 3) return;

        string statsPart = sections[0];
        string changedPart = sections[1];
        string removedPart = sections[2];

        if (statsPart != "-" && statsPart.StartsWith("S|"))
        {
            var sp = statsPart.Split('|');
            int v;
            if (sp.Length > 1 && int.TryParse(sp[1], out v)) _mirror.currentHealth = v;
            if (sp.Length > 2 && int.TryParse(sp[2], out v)) _mirror.currentEnergy = v;
        }

        if (changedPart.Length > 0)
            foreach (var entry in changedPart.Split(new[] { "~|~" }, System.StringSplitOptions.None))
            {
                if (!entry.StartsWith("C|")) continue;
                var parts = entry.Split('|');
                if (parts.Length < 5) continue;
                var card = CardStateProto.DeserializeCard(parts[4]);
                card.instanceID = parts[1];
                int z; int.TryParse(parts[2], out z); card.zone = (CardZone)z;
                int s; int.TryParse(parts[3], out s); card.slotID = s;
                card.ownerIndex = 1;
                _mirror.Upsert(card);
            }

        if (removedPart.Length > 0)
            foreach (var entry in removedPart.Split(new[] { "~|~" }, System.StringSplitOptions.None))
            {
                if (!entry.StartsWith("R|")) continue;
                _mirror.Remove(entry.Substring(2));
            }
    }
}
