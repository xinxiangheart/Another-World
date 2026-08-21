using System.Collections;
using UnityEngine;
using Steamworks;

/// <summary>
/// 阶段轮盘：5 个环按角度环形分布（MaskArea 框定可见 3 个）。
///
/// PhaseStart 与随后行动阶段（MyTurn/EnemyTurn）合二为一：PhaseStart 不触发旋转，
/// 初始直接显示第一行动阶段，每轮只旋转 3 次（首行动→次行动→Battle→下轮首行动）。
/// 内容模型（同一数据源 tm.currentPhase + tm.isMyTurnFirst）：
///   - Prev      ：刚过去的单元（L 位）
///   - Cur       ：当前单元（C 位）
///   - Next      ：下一单元（R 位）
///   - NextNext  ：下下单元（隐藏位预载，旋转后进 R 位显示"切换后的下一单元"）
///
/// 完整循环（严格"预载→旋转→清空→再预载"）：
///   阶段切换时：
///     ① 预载：隐藏环(H2) = NextNext（= NextOf(Next)，旋转后进 R 位显示新 Next）
///     ② 旋转：所有环带图案滚动一位
///     ③ 结果：原 NextNext → R 位（新 Next）；原 Next → C 位（新 Cur）；
///               原 Cur → L 位（新 Prev）；原 Prev → H1 位（转出显示区）
///     ④ 清空：H1（原 Prev）清空图案，为再下一轮准备（下次旋转前作为 H2 被预载）
///   显示环（L/C/R）图案永不更新——只靠物理环带内容移动。
///
/// 先后手交换（关键）：
///   先手每轮互换一次——TurnManager.EndCurrentTurn 在设 BattlePhase 前调 SwapFirstPlayer()
///   翻转 isMyTurnFirst。因此轮内各阶段生效期间 isMyTurnFirst 不变，且预载任何未来阶段
///   （Next/NextNext）时 tm.isMyTurnFirst 已反映该阶段所在轮的先后手，直接读取即可
///   （见 IsFirstMineForPhase）。
///
/// L 位是显示环（上一阶段）：只在 Start 初始态为空白，之后随旋转自然显示上一阶段，永不清空。
///
/// 旋转校正：旋转动画（0.4s）期间阶段又变化时（回合边界 Battle→PhaseStart→MyTurn/EnemyTurn
///   常在旋转窗口内连跳，但 PhaseStart 被合并不旋转），旋转结束后按最新阶段 + 最新先手
///   补转一次，避免五环内容滞后。
///
/// 头像显示（行动者视角，AI 对战与联机一致）：
///   - MyTurn（己方行动）→ 己方头像；EnemyTurn（对方行动）→ 对方头像（AI 无头像 → 空白环）。
///   - PhaseStart（准备阶段）→ 本回合先手头像（按 isMyTurnFirst）。
/// 头像不可用（AI 先手/AI 回合/未加载）→ 空白环（绝不 SetAvatar(null) 残留）。
/// </summary>
public class PhaseWheel : MonoBehaviour
{
    public static PhaseWheel Instance { get; private set; }

    [Header("引用")]
    public RectTransform wheelContainer;   // 旋转容器（本实现不旋转，保持 0）
    public RingSlot[] slots;               // 5 个环，物理 index [0=Hidden1, 1=Left, 2=Center, 3=Right, 4=Hidden2]

    [Header("配置")]
    [Tooltip("旋转动画时长（秒）")]
    public float rotateDuration = 0.4f;
    [Tooltip("攻击回合图标（两剑交叉）")]
    public Sprite battleIcon;

    static readonly TurnManager.TurnPhase[] ORDER = { TurnManager.TurnPhase.PhaseStart, TurnManager.TurnPhase.MyTurn, TurnManager.TurnPhase.EnemyTurn, TurnManager.TurnPhase.BattlePhase };
    /// <summary>角色 → 世界角度（度）。H1=300(左上), L=240(左下), C=180(正下), R=120(右下), H2=60(右上)。
    /// Left 在左、Right 在右；next 从右侧(120°)滑入中央(180°)，顺时针视觉。</summary>
    static readonly float[] ROLE_ANGLE = { 300f, 240f, 180f, 120f, 60f };

    /// <summary>角色 → 物理环 index。[H1, L, C, R, H2]。</summary>
    int[] _roleSlot = { 0, 1, 2, 3, 4 };
    bool _rotating;
    TurnManager.TurnPhase? _lastPhase;
    float _radius;
    /// <summary>物理环 → 内容描述（跟随物理环，旋转时内容不变）。</summary>
    string[] _slotDesc = new string[5];

    void LogWheel(string tag)
    {
        if (slots == null || slots.Length != 5) return;
        bool mf = TurnManager.Instance != null && TurnManager.Instance.isMyTurnFirst;
        int round = TurnManager.Instance != null ? TurnManager.Instance.phaseCount : 0;
        Debug.Log($"[PhaseWheel] {tag} (myFirst={mf}, round={round}) → H1={_slotDesc[_roleSlot[0]]}, " +
                  $"L={_slotDesc[_roleSlot[1]]}, C={_slotDesc[_roleSlot[2]]}, R={_slotDesc[_roleSlot[3]]}, H2={_slotDesc[_roleSlot[4]]}");
    }

    string AvatarDesc(bool firstMine, Texture2D avatar)
    {
        if (avatar == null) return SimpleAI.IsAIMatch ? "AI空白" : "空白";
        return firstMine ? "玩家1" : "玩家2";
    }

    void Awake()
    {
        Instance = this;
        if (slots == null || slots.Length != 5) { Debug.LogError("[PhaseWheel] 需要 5 个 RingSlot 引用"); return; }
        var rt0 = slots[0] != null ? slots[0].GetComponent<RectTransform>() : null;
        _radius = rt0 != null ? rt0.anchoredPosition.magnitude : 125f;
    }

    void Start()
    {
        var tm = TurnManager.Instance;
        if (tm == null) return;
        bool myFirst = tm.isMyTurnFirst;
        // PhaseStart 与随后行动阶段合二为一：初始直接显示第一行动阶段（MyTurn/EnemyTurn），
        // 不把 PhaseStart 当独立节点。_lastPhase 记为 initial，使随后 PhaseStart→首行动 不触发旋转。
        TurnManager.TurnPhase initial = tm.currentPhase;
        if (initial == TurnManager.TurnPhase.PhaseStart)
            initial = myFirst ? TurnManager.TurnPhase.MyTurn : TurnManager.TurnPhase.EnemyTurn;
        _lastPhase = initial;
        // 游戏开始第一回合：L（上一阶段）= 空白（尚无上一阶段）；C=第一行动阶段；R=下一单元；隐藏位留空。
        ApplyContent(_roleSlot[1], null, false); // L = 空白
        ApplyContent(_roleSlot[2], initial, false);                       // C = Cur
        ApplyContent(_roleSlot[3], NextOfPhase(initial, myFirst), false); // R = Next
        slots[_roleSlot[0]].SetEmpty();
        slots[_roleSlot[4]].SetEmpty();
        LogWheel("[Start] 初始五环");
    }

    void Update()
    {
        var tm = TurnManager.Instance;
        if (tm == null) return;
        TurnManager.TurnPhase cur = tm.currentPhase;
        if (_lastPhase == null) { _lastPhase = cur; return; }
        if (_lastPhase.Value == cur) return;

        // PhaseStart 与随后行动阶段（MyTurn/EnemyTurn）合二为一：PhaseStart 不触发旋转，
        // 也不推进 _lastPhase——Battle→PhaseStart 直接过渡到下一轮首行动阶段时只旋转一次
        // （该旋转在首行动阶段（PhaseStart→MyTurn/EnemyTurn）那一刻触发，_lastPhase 仍是 Battle）。
        if (cur == TurnManager.TurnPhase.PhaseStart) return;

        LogWheel($"[Update] 阶段变化 {_lastPhase.Value} → {cur}，切换前五环");
        // 上一旋转单元（PhaseStart 已被跳过，故 _lastPhase 必为真实单元，可作旋转的 prev）。
        TurnManager.TurnPhase prev = _lastPhase.Value;
        // 立即记录最新阶段（无论是否旋转中）——防止旋转结束误判阶段又变，触发第二次旋转（连转两次）。
        _lastPhase = cur;
        if (_rotating) return; // 旋转动画播放期间忽略新的旋转请求
        bool myFirst = tm.isMyTurnFirst;
        RotateToPhase(prev, cur, NextOfPhase(cur, myFirst));
    }

    /// <summary>下一单元（考虑先手方：MyTurn/EnemyTurn 顺序因先手而异）。
    /// 旋转节点：MyTurn → EnemyTurn → Battle → 下一轮首行动（MyTurn/EnemyTurn），PhaseStart 已合并。
    /// 玩家先手轮：MyTurn→EnemyTurn→Battle→MyTurn(下轮)；AI 先手轮：EnemyTurn→MyTurn→Battle→EnemyTurn(下轮)。</summary>
    static TurnManager.TurnPhase NextOfPhase(TurnManager.TurnPhase p, bool myFirst)
    {
        switch (p)
        {
            case TurnManager.TurnPhase.PhaseStart: return myFirst ? TurnManager.TurnPhase.MyTurn : TurnManager.TurnPhase.EnemyTurn;
            case TurnManager.TurnPhase.MyTurn:     return myFirst ? TurnManager.TurnPhase.EnemyTurn : TurnManager.TurnPhase.BattlePhase;
            case TurnManager.TurnPhase.EnemyTurn:  return myFirst ? TurnManager.TurnPhase.BattlePhase : TurnManager.TurnPhase.MyTurn;
            // Battle 的下一单元 = 下一轮首行动阶段（PhaseStart 已合并），按翻转后的先手取 MyTurn/EnemyTurn
            case TurnManager.TurnPhase.BattlePhase: return myFirst ? TurnManager.TurnPhase.MyTurn : TurnManager.TurnPhase.EnemyTurn;
        }
        return TurnManager.TurnPhase.PhaseStart;
    }

    /// <summary>旋转一个环位：预载 NextNext → 物理环角度动画 → 角色轮转 → 清空刚转出的隐藏环。
    /// L 位是显示环（上一阶段），永不清空——只在 Start 初始态为空白。</summary>
    public void RotateToPhase(TurnManager.TurnPhase? previous, TurnManager.TurnPhase current, TurnManager.TurnPhase? next)
    {
        if (_rotating || slots == null || slots.Length != 5) { UpdateWheelContents(previous, current, next); return; }
        StartCoroutine(RotateRoutine(previous, current, next));
    }

    IEnumerator RotateRoutine(TurnManager.TurnPhase? previous, TurnManager.TurnPhase current, TurnManager.TurnPhase? next)
    {
        _rotating = true;
        LogWheel($"[旋转] {DescribePhase(previous)} → {DescribePhase(current)} → {DescribePhase(next)}");

        // ① 预载：隐藏环(H2) = next（切换后的下一阶段 = 切换前的 NextNext 下下阶段）。
        //    旋转后 next 进 R 位显示"新的下一阶段"。
        //    注意：先手翻转发生在 EndCurrentTurn 设 BattlePhase 之前，预载任何未来阶段时
        //    tm.isMyTurnFirst 已反映该阶段所在轮的先后手，直接读取即可（见 IsFirstMineForPhase）。
        ApplyContent(_roleSlot[4], next, true);
        LogWheel($"[预载] 预载 H2={DescribePhase(next)}，五环");

        // ② 旋转动画：物理环从当前角色位 → 新角色位（逆时针移一位，带内容移动）。
        //    显示环（L/C/R）图案在动画期间不变。
        float[] startA = new float[5], endA = new float[5];
        for (int role = 0; role < 5; role++)
        {
            startA[role] = ROLE_ANGLE[role];
            endA[role] = ROLE_ANGLE[(role + 4) % 5]; // 逆时针移一位：R→C, C→L, L→H1, H1→H2, H2→R
        }

        float t = 0f;
        while (t < rotateDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / rotateDuration);
            float e = 1f - (1f - p) * (1f - p); // ease-out
            for (int role = 0; role < 5; role++)
                SetPhysAngle(_roleSlot[role], Mathf.LerpAngle(startA[role], endA[role], e));
            yield return null;
        }
        for (int role = 0; role < 5; role++)
            SetPhysAngle(_roleSlot[role], endA[role]);

        // ③ 角色轮转：H1←L, L←C, C←R, R←H2, H2←H1
        RotateRoles();

        // ④ 清空"刚转出显示区"的隐藏环（当前 H1 位 = 原 Prev 环）。
        //    显示环（L/C/R）已带正确内容到位，图案永不更新；L 位（上一阶段）永不清空。
        slots[_roleSlot[0]].SetEmpty();
        _slotDesc[_roleSlot[0]] = "空白";

        LogWheel("[旋转后] 五环");
        // 下次旋转前，这个空隐藏环作为 H2 位被预载（① 预载已覆盖），循环闭合。
        _rotating = false;

        // ⑤ 旋转校正：旋转动画期间阶段又变化（回合边界 Battle→PhaseStart→MyTurn/EnemyTurn
        //    常在 0.4s 旋转窗口内连跳，PhaseStart 被合并跳过），本次旋转是按旧阶段预载的，
        //    补转一次落到最新行动阶段。补转时用最新 isMyTurnFirst 计算 prev/next，
        //    确保跨轮预载拿到翻转后的先后手。
        var tm = TurnManager.Instance;
        if (tm != null && tm.currentPhase != current && tm.currentPhase != TurnManager.TurnPhase.PhaseStart)
        {
            LogWheel($"[旋转校正] 旋转期间阶段 {current} → {tm.currentPhase}，补转校正");
            bool myFirst = tm.isMyTurnFirst;
            // prev = 本次旋转的 current（刚显示的真实单元）
            RotateToPhase(current, tm.currentPhase, NextOfPhase(tm.currentPhase, myFirst));
        }
    }

    static string DescribePhase(TurnManager.TurnPhase? phase)
    {
        if (phase == null) return "空白";
        switch (phase.Value)
        {
            case TurnManager.TurnPhase.BattlePhase: return "攻击图标";
            case TurnManager.TurnPhase.PhaseStart: return "准备阶段";
            case TurnManager.TurnPhase.MyTurn: return "己方回合";
            case TurnManager.TurnPhase.EnemyTurn: return "对方回合";
            default: return "未知";
        }
    }

    /// <summary>角色顺时针移动一位：H1←L, L←C, C←R, R←H2, H2←H1。</summary>
    void RotateRoles()
    {
        int h1 = _roleSlot[0], l = _roleSlot[1], c = _roleSlot[2], r = _roleSlot[3], h2 = _roleSlot[4];
        _roleSlot[0] = l; _roleSlot[1] = c; _roleSlot[2] = r; _roleSlot[3] = h2; _roleSlot[4] = h1;
    }

    /// <summary>兜底预载：H2 = next（isNext=true）。显示环永不在此更新。</summary>
    public void UpdateWheelContents(TurnManager.TurnPhase? previous, TurnManager.TurnPhase current, TurnManager.TurnPhase? next)
    {
        if (slots == null || slots.Length != 5) return;
        ApplyContent(_roleSlot[4], next, true);
    }

    void SetPhysAngle(int physIndex, float angle)
    {
        if (physIndex < 0 || physIndex >= slots.Length || slots[physIndex] == null) return;
        float rad = angle * Mathf.Deg2Rad;
        slots[physIndex].GetComponent<RectTransform>().anchoredPosition =
            new Vector2(Mathf.Sin(rad) * _radius, Mathf.Cos(rad) * _radius);
    }

    /// <summary>填充环内容。isNext=true 表示"未来阶段"（Next/NextNext 预载），需跨轮翻转先手。</summary>
    void ApplyContent(int physIndex, TurnManager.TurnPhase? phase, bool isNext)
    {
        if (physIndex < 0 || physIndex >= slots.Length || slots[physIndex] == null) return;
        // AI 对战：AI 回合（EnemyTurn）永远空白——入口统一判断，任何路径不得绕过
        if (SimpleAI.IsAIMatch && phase == TurnManager.TurnPhase.EnemyTurn)
        {
            slots[physIndex].SetEmpty();
            _slotDesc[physIndex] = "AI空白";
            return;
        }
        if (phase == null)
        {
            slots[physIndex].SetEmpty();
            _slotDesc[physIndex] = "空白";
            return;
        }
        switch (phase.Value)
        {
            case TurnManager.TurnPhase.BattlePhase:
                slots[physIndex].SetBattle(battleIcon); // 整个环替换为攻击图片，不显示头像
                _slotDesc[physIndex] = "攻击图标";
                break;
            case TurnManager.TurnPhase.PhaseStart:
                // 准备阶段：显示先手（按 isMyTurnFirst）
                {
                    bool fMine = IsFirstMineForPhase(phase.Value, isNext);
                    Texture2D fAvatar = fMine ? MyAvatar() : OppAvatar();
                    if (fAvatar != null) slots[physIndex].SetAvatar(fAvatar);
                    else slots[physIndex].SetEmpty();
                    _slotDesc[physIndex] = AvatarDesc(fMine, fAvatar);
                }
                break;
            case TurnManager.TurnPhase.MyTurn:
                // 己方行动回合：显示己方头像（行动者视角，AI 对战与联机一致）
                {
                    Texture2D my = MyAvatar();
                    if (my != null) slots[physIndex].SetAvatar(my);
                    else slots[physIndex].SetEmpty();
                    _slotDesc[physIndex] = AvatarDesc(true, my);
                }
                break;
            case TurnManager.TurnPhase.EnemyTurn:
                // 对方行动回合：显示对方头像（AI 对战 OppAvatar=null → 空白；联机显示对手头像）
                {
                    Texture2D opp = OppAvatar();
                    if (opp != null) slots[physIndex].SetAvatar(opp);
                    else slots[physIndex].SetEmpty();
                    _slotDesc[physIndex] = AvatarDesc(false, opp);
                }
                break;
        }
    }

    // ============ 先手判断（同一数据源 tm.isMyTurnFirst）============

    /// <summary>该阶段生效时先手是否己方。
    /// 已验证翻转时机：TurnManager.EndCurrentTurn 在设 BattlePhase 前调 SwapFirstPlayer()
    /// 翻转 isMyTurnFirst（每轮一次）。因此轮内各阶段生效期间 isMyTurnFirst 不变，且轮末 Battle
    /// 前已翻转为下一轮的值——预载任何未来阶段（Next/NextNext，最多跨一轮边界）时，
    /// tm.isMyTurnFirst 已反映该阶段所在轮的先后手，直接返回即可，无需在此翻转。
    /// 若未来翻转时机变化，可改为按 phase 跨过的轮边界数翻转。</summary>
    bool IsFirstMineForPhase(TurnManager.TurnPhase phase, bool isNext)
    {
        var tm = TurnManager.Instance;
        if (tm == null) return true;
        return tm.isMyTurnFirst;
    }

    Texture2D MyAvatar()
    {
        // 己方头像 = LocalSteamID（统一管理器缓存；兜底 SteamDataManager.localAvatar）
        Texture2D tex = SteamAvatarManager.GetAvatarTexture(LobbyConfig.LocalSteamID);
        return tex != null ? tex : (SteamDataManager.Instance != null ? SteamDataManager.Instance.localAvatar : null);
    }

    Texture2D OppAvatar()
    {
        // AI 对战：AI(Remote, server-only) 无 SteamID，AI 头像为空白
        if (SimpleAI.IsAIMatch) return null;
        // 对方头像 = RemoteSteamID（大厅捕获 + 网络 SyncVar 双路填充，统一管理器缓存）
        return SteamAvatarManager.GetAvatarTexture(LobbyConfig.RemoteSteamID);
    }
}
