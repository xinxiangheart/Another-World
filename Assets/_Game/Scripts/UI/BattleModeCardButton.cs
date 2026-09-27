using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>子弹窗里的模式卡（战斗面板：匹配 / 排位；「其它」面板：离线模式）：悬停换预提亮的贴图 + 标题变金；点击进流程。
/// **未连接 Steam 时**：标题置灰、悬停不给金、点击不进入匹配 / 排位（**离线模式不受这条限制**）。</summary>
/// <remarks>2026-09-27 用户：「接入鼠标悬停交互（变金色），匹配点击后退出这个子弹窗进入之前做好的匹配流程，在大厅等待匹配」；
/// 同日追加「若玩家未连接 steam，匹配和排位本身字体将会变成灰色并无法点击进入匹配或排位」+
/// 「在匹配或者排位中再次点击匹配或者排位是不会再次进入匹配或者排位进程」。
///
/// 两张悬停贴图由 Tools/cardframe/BattleModeV1.ps1 -hover 出（石面提亮 + 金 GOLD→GOLD_L），形体一致所以不跳位。
/// 与 LobbyIconHover 同一路数：没有 Button，直接用 IPointer* 接口；卡自己的 RawImage 是 raycast 目标
/// （卡上 Text_Title 的 raycastTarget 在接线时置 false）。
///
/// 点「匹配」= 关掉所在子弹窗 + QuickMatchPanel.Open()（匹配逻辑照旧，界面换成 MatchWaitPanel 小窗）。
/// 点「排位」= 暂时走占位弹窗（排位赛还没做）。
/// 点「离线模式」（挂在「其它」子弹窗里）= 关掉子弹窗 + LobbyManager.EnterOfflineBattle()，直接进 Game 场景。</remarks>
public class BattleModeCardButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public enum Kind { QuickMatch, Ranked, Offline }

    [Header("这张卡是谁")]
    public Kind kind = Kind.QuickMatch;

    [Header("卡面 / 标题")]
    public RawImage plate;
    public TextMeshProUGUI label;

    [Header("常态 / 悬停贴图")]
    public Texture normalTexture;
    public Texture hoverTexture;

    [Header("标题配色（常态色在 Awake 里从 label 现取）")]
    public Color hoverColor = new Color32(228, 203, 132, 255);   // 亮金 #E4CB84
    [Tooltip("未连接 Steam 时的字色 —— 取本套「钢」#8EA2B4 压暗，不另起颜色。")]
    public Color disabledColor = new Color32(142, 162, 180, 190);
    [HideInInspector] public Color normalColor;

    [Header("点击")]
    [Tooltip("点「匹配」时先关掉的子弹窗（一般就是 Panel_Battle）。")]
    public LobbySubPanel panel;
    [Tooltip("「排位」暂时弹的占位窗。")]
    public LobbyPopup popup;
    public string popupTitle = "排位赛开发中";

    /// <summary>Steam 连接判定。默认 = API 已初始化 + 已登录；测试 / 强制离线模式可覆盖它。</summary>
    public static System.Func<bool> steamGateOverride;

    bool _disabled;

    /// <summary>这张卡要不要 Steam 连接（匹配 / 排位要；离线模式不要 —— 它本来就是给没连 Steam 的人用的）。</summary>
    public bool RequiresSteam { get { return kind != Kind.Offline; } }

    static bool SteamReady()
    {
        if (steamGateOverride != null) return steamGateOverride();
        // 没初始化时 SteamUser 会抛 InvalidOperationException（同 SteamManager.Update 那个坑），所以整段兜住
        try { return SteamManager.Initialized && Steamworks.SteamUser.BLoggedOn(); }
        catch { return false; }
    }

    void Awake()
    {
        if (plate == null) plate = GetComponent<RawImage>();
        if (label == null) label = GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null) normalColor = label.color;
    }

    void OnEnable() { RefreshGate(); }
    void OnDisable() { ApplyNormal(); }

    void Update()
    {
        if (_disabled && SteamReady()) RefreshGate();   // 中途连上 Steam 就自己恢复（只在置灰时轮询）
    }

    /// <summary>按 Steam 连接状态切「可用 / 置灰」。未连接 = 灰字 + 点不动（悬停也不变金）。</summary>
    public void RefreshGate()
    {
        _disabled = RequiresSteam && !SteamReady();
        ApplyNormal();
    }

    public bool IsDisabled { get { return _disabled; } }

    public void OnPointerEnter(PointerEventData eventData) { ApplyHover(); }
    public void OnPointerExit(PointerEventData eventData) { ApplyNormal(); }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_disabled)
        {
            Debug.Log("[BattleModeCard] 未连接 Steam —— 「" + kind + "」置灰不可点");
            return;
        }
        // 离线模式（用户 2026-09-27：「只有一个『离线模式』，点击后直接跳转到 Game 场景」）：
        // 不联机、不进匹配池 —— 关掉所在子弹窗，直接走离线开局那条路。
        if (kind == Kind.Offline)
        {
            if (panel != null) panel.Close();
            LobbyManager.EnterOfflineBattle();
            return;
        }


        var qm = QuickMatchPanel.Instance;

        // 保险（用户 2026-09-27）：匹配进行中，点哪张卡都不重入流程 —— 只把子弹窗收掉，
        // 把还在跑的小窗留在眼前（计时不重置）。
        // 注：排位流程还没做（现在只弹占位窗）；等它落地时要在这儿一并判它自己的 IsBusy。
        if (qm != null && qm.IsMatching)
        {
            if (panel != null) panel.Close();
            qm.SurfaceWait();
            Debug.Log("[BattleModeCard] 已在匹配中，忽略重复进入（" + kind + "）");
            return;
        }

        if (kind == Kind.QuickMatch)
        {
            if (panel != null) panel.Close();
            if (qm != null) qm.Open();
            else Debug.LogError("[BattleModeCard] 场景里没有 QuickMatchPanel.Instance —— 匹配流程起不来");
        }
        else
        {
            if (popup != null) popup.Show(popupTitle);
        }
    }

    void ApplyHover()
    {
        if (_disabled) return;      // 不可用：悬停不给任何反馈，免得看着像能点
        if (label != null) label.color = hoverColor;
        if (plate != null && hoverTexture != null) plate.texture = hoverTexture;
    }

    void ApplyNormal()
    {
        if (label != null) label.color = _disabled ? disabledColor : normalColor;
        if (plate != null && normalTexture != null) plate.texture = normalTexture;
    }
}
