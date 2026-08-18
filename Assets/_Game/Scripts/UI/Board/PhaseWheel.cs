using System.Collections;
using UnityEngine;
using Steamworks;

/// <summary>
/// 阶段轮盘：5 个环按角度环形分布（MaskArea 框定可见 3 个），
/// 左环=上一阶段、中环=当前阶段、右环=下一阶段、两个隐藏位待命。
///
/// 内容刷新时机（关键）：
///   - 显示中的三个环（左/中/右）图案保持不变，旋转时带着内容一起转；
///   - 只有旋转进入隐藏位的环，在隐藏期间更新图案，为下次循环做准备；
///   - 玩家看不到换图过程。
/// 环内容：PhaseStart/MyTurn=先手头像，EnemyTurn=后手头像，BattlePhase=攻击图标，null=空白。
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
        // 首次：填充三个显示位（左/中/右），隐藏位留空
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
        if (_lastPhase == null) { _lastPhase = cur; UpdateWheelContents(PrevOf(cur), cur, NextOf(cur)); return; }
        if (_lastPhase.Value != cur)
        {
            _lastPhase = cur;
            RotateToPhase(PrevOf(cur), cur, NextOf(cur));
        }
    }

    static TurnManager.TurnPhase? PrevOf(TurnManager.TurnPhase p) => ORDER[(System.Array.IndexOf(ORDER, p) + 3) % 4];
    static TurnManager.TurnPhase? NextOf(TurnManager.TurnPhase p) => ORDER[(System.Array.IndexOf(ORDER, p) + 1) % 4];

    /// <summary>旋转一个环位 + 旋转结束后角色轮转（内容已在隐藏位预置，显示位保持）。</summary>
    public void RotateToPhase(TurnManager.TurnPhase? previous, TurnManager.TurnPhase current, TurnManager.TurnPhase? next)
    {
        if (_rotating || wheelContainer == null) { UpdateWheelContents(previous, current, next); return; }
        StartCoroutine(RotateRoutine(previous, current, next));
    }

    IEnumerator RotateRoutine(TurnManager.TurnPhase? previous, TurnManager.TurnPhase current, TurnManager.TurnPhase? next)
    {
        _rotating = true;

        // 旋转前：只更新两个隐藏环——H2 预置下一阶段（旋转后进右显示位）、H1 清空。
        // 显示中的三个环保持不动，玩家看不到换图。
        UpdateWheelContents(previous, current, next);

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

        // 旋转结束：角色轮转（环带内容转，显示位自然正确）
        RotateRoles();
        _rotating = false;
    }

    /// <summary>角色顺时针移动一位：H1←L, L←C, C←R, R←H2, H2←H1。</summary>
    void RotateRoles()
    {
        int h1 = _roleSlot[0], l = _roleSlot[1], c = _roleSlot[2], r = _roleSlot[3], h2 = _roleSlot[4];
        _roleSlot[0] = l; _roleSlot[1] = c; _roleSlot[2] = r; _roleSlot[3] = h2; _roleSlot[4] = h1;
    }

    /// <summary>只更新两个隐藏环的内容：H2 预置下一阶段，H1 清空。三个显示环保持不变（旋转期间不换图）。</summary>
    public void UpdateWheelContents(TurnManager.TurnPhase? previous, TurnManager.TurnPhase current, TurnManager.TurnPhase? next)
    {
        if (slots == null || slots.Length != 5) return;
        ApplyContent(_roleSlot[4], next); // H2（隐藏）预置下一阶段
        slots[_roleSlot[0]].SetEmpty();   // H1（隐藏）清空
    }

    void ApplyContent(int physIndex, TurnManager.TurnPhase? phase)
    {
        if (physIndex < 0 || physIndex >= slots.Length || slots[physIndex] == null) return;
        if (phase == null) { slots[physIndex].SetEmpty(); return; }
        switch (phase.Value)
        {
            case TurnManager.TurnPhase.BattlePhase:
                slots[physIndex].SetIcon(battleIcon);
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
