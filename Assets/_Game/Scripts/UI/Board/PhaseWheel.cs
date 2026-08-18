using System.Collections;
using UnityEngine;
using Steamworks;

/// <summary>
/// 阶段轮盘：5 个环按角度环形分布（MaskArea 框定可见 3 个），
/// 左环=上一阶段、中环=当前阶段、右环=下一阶段、两个隐藏位待命。
///
/// 隐藏环更新机制（关键，避免闪烁/占错位）：
///   - 3 个显示环（左/中/右）：图片绝不允许清除或更改；
///   - 2 个隐藏环（H1/H2）：唯一允许更改图片的环；
///   - 旋转前：预置"即将转进显示区"的隐藏环（当前 H2 位）= 下一阶段；
///   - 旋转动画期间：不更新任何环的图片；
///   - 旋转后：角色轮转，清空"刚转出显示区"的隐藏环（当前 H1 位）；
///   - 显示环图片永不更新（只在 Start 首次填充）。
/// 环内容：PhaseStart/MyTurn=先手头像，EnemyTurn=后手头像，BattlePhase=整环攻击图片，null=空白。
/// </summary>
public class PhaseWheel : MonoBehaviour
{
    public static PhaseWheel Instance { get; private set; }

    [Header("引用")]
    public RectTransform wheelContainer;   // 旋转容器
    public RingSlot[] slots;               // 5 个环，物理 index [0=Hidden1, 1=Left, 2=Center, 3=Right, 4=Hidden2]（按角度）

    [Header("配置")]
    [Tooltip("旋转动画时长（秒）")]
    public float rotateDuration = 0.4f;
    [Tooltip("攻击回合图标（两剑交叉）")]
    public Sprite battleIcon;

    static readonly TurnManager.TurnPhase[] ORDER = { TurnManager.TurnPhase.PhaseStart, TurnManager.TurnPhase.MyTurn, TurnManager.TurnPhase.EnemyTurn, TurnManager.TurnPhase.BattlePhase };

    /// <summary>角色 → 物理环 index。[H1, L, C, R, H2]。旋转 -60° 后角色顺时针移动一位。</summary>
    int[] _roleSlot = { 0, 1, 2, 3, 4 };
    bool _rotating;
    TurnManager.TurnPhase? _lastPhase;

    void Awake()
    {
        Instance = this;
        if (slots == null || slots.Length != 5)
            Debug.LogError("[PhaseWheel] 需要 5 个 RingSlot 引用");
    }

    void Start()
    {
        var tm = TurnManager.Instance;
        if (tm == null) return;
        _lastPhase = tm.currentPhase;
        // 首次：填充三个显示位（左/中/右），隐藏位留空。
        // 此后显示环内容永不更新，只在旋转进出隐藏位时变化。
        ApplyContent(_roleSlot[1], PrevOf(_lastPhase.Value));
        ApplyContent(_roleSlot[2], _lastPhase.Value);
        ApplyContent(_roleSlot[3], NextOf(_lastPhase.Value));
        slots[_roleSlot[0]].SetEmpty();
        slots[_roleSlot[4]].SetEmpty();
    }

    void Update()
    {
        var tm = TurnManager.Instance;
        if (tm == null || _rotating) return;
        TurnManager.TurnPhase cur = tm.currentPhase;
        if (_lastPhase == null) { _lastPhase = cur; return; }
        if (_lastPhase.Value != cur)
        {
            _lastPhase = cur;
            RotateToPhase(PrevOf(cur), cur, NextOf(cur));
        }
    }

    static TurnManager.TurnPhase? PrevOf(TurnManager.TurnPhase p) => ORDER[(System.Array.IndexOf(ORDER, p) + 3) % 4];
    static TurnManager.TurnPhase? NextOf(TurnManager.TurnPhase p) => ORDER[(System.Array.IndexOf(ORDER, p) + 1) % 4];

    /// <summary>旋转一个环位：预置即将转进的隐藏环 → 旋转 → 角色轮转 → 清空刚转出的隐藏环。</summary>
    public void RotateToPhase(TurnManager.TurnPhase? previous, TurnManager.TurnPhase current, TurnManager.TurnPhase? next)
    {
        if (_rotating || wheelContainer == null) { UpdateWheelContents(previous, current, next); return; }
        StartCoroutine(RotateRoutine(previous, current, next));
    }

    IEnumerator RotateRoutine(TurnManager.TurnPhase? previous, TurnManager.TurnPhase current, TurnManager.TurnPhase? next)
    {
        _rotating = true;

        // 1. 旋转前：只预置"即将转进显示区"的隐藏环（当前 H2 位）= 下一阶段。
        //    玩家看不到（H2 在遮罩外），旋转进入显示区时已带着正确图片。
        ApplyContent(_roleSlot[4], next);

        // 2. 旋转动画：期间不更新任何环的图片（避免闪烁/占错位）。
        Quaternion startRot = wheelContainer.localRotation;
        Quaternion endRot = startRot * Quaternion.Euler(0f, 0f, -60f); // 顺时针 60°：右环(240°)滑入中环(180°)
        float t = 0f;
        while (t < rotateDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / rotateDuration);
            float e = 1f - (1f - p) * (1f - p); // ease-out
            wheelContainer.localRotation = Quaternion.Slerp(startRot, endRot, e);
            yield return null;
        }
        wheelContainer.localRotation = startRot;

        // 3. 旋转后：角色轮转（环带内容转，显示位自然正确）
        RotateRoles();

        // 4. 旋转后：清空"刚转出显示区"的隐藏环（当前 H1 位 = 原左环）。
        //    只清空隐藏位环，显示位三个环绝不动。
        slots[_roleSlot[0]].SetEmpty();

        _rotating = false;
    }

    /// <summary>角色顺时针移动一位：H1←L, L←C, C←R, R←H2, H2←H1。</summary>
    void RotateRoles()
    {
        int h1 = _roleSlot[0], l = _roleSlot[1], c = _roleSlot[2], r = _roleSlot[3], h2 = _roleSlot[4];
        _roleSlot[0] = l; _roleSlot[1] = c; _roleSlot[2] = r; _roleSlot[3] = h2; _roleSlot[4] = h1;
    }

    /// <summary>预置即将转进显示区的隐藏环（当前 H2 位）= 下一阶段。显示环永不在此更新。</summary>
    public void UpdateWheelContents(TurnManager.TurnPhase? previous, TurnManager.TurnPhase current, TurnManager.TurnPhase? next)
    {
        if (slots == null || slots.Length != 5) return;
        ApplyContent(_roleSlot[4], next); // H2（隐藏）预置下一阶段
    }

    void ApplyContent(int physIndex, TurnManager.TurnPhase? phase)
    {
        if (physIndex < 0 || physIndex >= slots.Length || slots[physIndex] == null) return;
        if (phase == null) { slots[physIndex].SetEmpty(); return; }
        switch (phase.Value)
        {
            case TurnManager.TurnPhase.BattlePhase:
                slots[physIndex].SetBattle(battleIcon); // 整个环替换为攻击图片，不显示头像
                break;
            case TurnManager.TurnPhase.PhaseStart:
            case TurnManager.TurnPhase.MyTurn:
                slots[physIndex].SetAvatar(FirstPlayerAvatar());
                break;
            case TurnManager.TurnPhase.EnemyTurn:
                slots[physIndex].SetAvatar(SecondPlayerAvatar());
                break;
        }
    }

    // ============ 头像获取 ============

    /// <summary>先手玩家头像：isMyTurnFirst=true → 己方；false → 对方。</summary>
    Texture2D FirstPlayerAvatar()
    {
        bool myFirst = TurnManager.Instance != null && TurnManager.Instance.isMyTurnFirst;
        return myFirst ? MyAvatar() : OppAvatar();
    }

    /// <summary>后手玩家头像：与先手相反。</summary>
    Texture2D SecondPlayerAvatar()
    {
        bool myFirst = TurnManager.Instance != null && TurnManager.Instance.isMyTurnFirst;
        return myFirst ? OppAvatar() : MyAvatar();
    }

    Texture2D MyAvatar() => SteamDataManager.Instance != null ? SteamDataManager.Instance.localAvatar : null;

    Texture2D OppAvatar()
    {
        // 对手头像：己方是 Client 时对手=Host（用 HostSteamID）；己方是 Host 时对手无 SteamID → null
        if (LobbyConfig.IsHost || string.IsNullOrEmpty(LobbyConfig.HostSteamID)) return null;
        if (!ulong.TryParse(LobbyConfig.HostSteamID, out ulong sid)) return null;
        return RingSlot.LoadAvatarFromSteamID(new CSteamID(sid));
    }
}
