using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 大厅「卡牌总览」全屏子弹窗：左栏级联筛选 + 右侧卡牌网格 + 右下角 n/总数。
/// </summary>
/// <remarks>2026-09-28 用户原话：「做卡牌总览，点击进入一个类似于作战的全屏，左边一栏（宽度大概为好友
/// 宽度的 3/4），右边是比较大的卡牌预览，大概一排 4-5 个（具体看效果）。左边一栏默认选中全部（这些选择
/// 格子都是一个 ui + 文字，有子背景以及选中变化），选择全部后出现召唤物和法术（在全部下面一栏）（选择后
/// 不再选中全部，即召唤物和法术选择互斥），选择召唤物后出现英雄，神选者，特殊（选择后不再选择召唤物）
/// （这些不互斥允许同时选择），选择法术后出现普通，邪恶，反制（这些不互斥，允许同时选择），另外这三个和
/// 上面三个共用一栏，因为父级互斥所以不会出现重叠，然后选择召唤物子分级后出现：0费，1费，3费，5费，进场，
/// 退场，主动退场，先手，反击，附着，抛置，这些是三个为一栏顺位向下，允许同时选择，不会取消父级选择，
/// 选择法术子分级后出现：0费，1费，2费，3费，4费，5费，这些是三个为一栏顺位向下，允许同时选择，不会取消
/// 父级选择，任何选择都会在右边展示，其之间以及和上顶框和下顶框之间有合适的距离，右下角显示数量/卡牌总数」。
///
/// **级联口径**（本组件实现的那一份）：
///   第 0 栏：全部（默认唯一选中）。
///   第 1 栏：召唤物 / 法术 —— **互斥**（选一个就不选另一个，也不选「全部」）；**常驻显示**，
///           点了子集也不隐藏、不熄灭（2026-09-28 用户「点击召唤物和法术子集不会隐藏召唤物和法术，
///           而是会在下一栏显示」⇒ 子集另起第 2 栏，父级留在原位亮着）。
///   第 2 栏：召唤物 -> 英雄 / 神选者 / 特殊（多选、不取消父级）；
///           法术 -> 普通 / 邪恶 / 反制（多选、不取消父级）—— 父级互斥，所以两组永不共存。
///   第 3 栏：召唤物 -> 0费 1费 3费 5费 进场 退场 主动退场 先手 反击 附着 抛置（多选）；
///           法术   -> 0费 1费 2费 3费 4费 5费（多选）；
///           都是**三个为一栏、顺位向下**，且**不取消父级选择**。
///
/// 过滤 = 根级 AND (第 1 栏选中项 OR) AND (第 2 栏选中项 OR)。同级多选是 OR，跨级是 AND
/// （与战斗里那份 legacy <c>CardCollectionPanel</c> 同口径，但它是在战斗场景、分级规则也不同 —— 这份是大厅专用，别互相改）。
/// 卡牌本体用 <c>CardData.card2DPrefab / spell2DPrefab</c> 引用的那张 2D 预制体（用户：「这些可以复用卡牌预制体引用的美术 ui」）。
/// </remarks>
public class LobbyCardCollectionPanel : MonoBehaviour, ILobbySubPanelOpen
{
    // ── 右栏网格几何（屏幕 px · 1920x1080；左边那栏的几何在生成器里，见 LobbyUIBuilder.Lc*）──
    // 2026-09-28 三改：用户「一排最多4-5张，参考之前的卡牌总览怎么实现的」——
    //   照战斗那份 legacy CardCollectionPanel（Lobby.unity 里那个 inactive 的 CardCollectionPanel 组件）
    //   的实际取值：cardsPerRow 5 / cardScale 2 / cardWidth 83 / cardHeight 146 / hSpacing 25 / vSpacing 25。
    //   ⚠ 卡面尺寸必须是**预制体的自然尺寸 83.33 x 146.33**：原来写 64x84 会把卡框压扁（比例 0.76 vs 0.57），
    //     卡图与金线一起变形 —— 这是"看着怪"的一半原因。
    //   块宽 = 5 x 83 x 2.5 + 4 x 25 x 2.5 = 1287.5，在 1378 的视口里居中（左右各留 45）——
    //   ⚠ 2026-09-28 四改：用户「一排4-5个你就只减少数量不放大吗？左右空着很好看吗」，
    //     所以 CardScale 从 2 抬到 **2.5**（从「只减数量」改成「同时放大铺满」）。
    //     卡面 207.5 x 365（屏幕 px）、行距 427.5；视口高 788 ⇒ 一屏 1.84 行（第二行露大半）。
    const int   GridColumns   = 5;
    const float CardSizeW     = 83f;      // = Card00_New_2D.prefab 根 RectTransform 的 sizeDelta（83.33/146.33）
    const float CardSizeH     = 146f;
    const float CardScale     = 2.5f;     // 一排 5 张铺满视口宽（旧卡牌总览是 2，那样左右会空掉 174）
    const float CardGapX      = 25f;      // 旧面板 hSpacing（卡片单位，随 CardScale 一起放大）
    const float CardGapY      = 25f;      // 旧面板 vSpacing
    // ── 分帧铺卡（2026-09-28「令点开卡牌总览不卡顿」）────────────────────
    // 实测（Stage101 · 编辑态 Play · 卡面已预热）：整开一次 655 ms，其中**只 Instantiate 186 个就占 313 ms**
    // （1.68 ms/张；InitFromTemplate 1 ms、显示刷新 72 ms）—— 「点开」那一下的冻结基本是 Instantiate，
    // 不是读图，所以光靠过场预加载解决不了。改成：先同步铺满一屏，剩下的每帧按时间预算接着铺。
    const int   InstantCards          = 15;   // 同步先铺几张（一屏 = 15 张，点开就有东西看）
    const int   CardsPerFrameBudgetMs = 8;    // 之后每帧最多花几毫秒铺卡
    const float PaneW         = 1378f;    // 视口宽（= 1919 − 445 − 96，与生成器 Lc* 同口径）；运行时优先读真实值

    const string FilterChipTex      = "Assets/_Game/Art/Sprites/Generated/lobby-ui-v1/LobbyChip_Filter.png";
    const string FilterChipHoverTex = "Assets/_Game/Art/Sprites/Generated/lobby-ui-v1/LobbyChip_FilterHover.png";
    const string FilterChipOnTex    = "Assets/_Game/Art/Sprites/Generated/lobby-ui-v1/LobbyChip_FilterOn.png";

    /// <summary>打包后按文件名兜底的那一份（Resources 里那份副本）；编辑器里仍优先走上面的工程路径。</summary>
    const string ResRoot            = "UI/lobby-ui-v1/";

    // ── 枚举口径 ─────────────────────────────────────────────────────────
    enum Root { All, Summon, Spell }

    // ── 生成器填进来的引用 ────────────────────────────────────────────────
    [Header("骨架（由 LobbyUIBuilder 填）")]
    public RectTransform railRoot;         // 左栏（只限位）
    public RectTransform chipLv0;          // 第 0 栏容器（全部）
    public RectTransform chipLv1;          // 第 1 栏容器（召唤物 / 法术 —— 常驻）
    public RectTransform chipLv2;          // 第 2 栏容器（英雄 / 神选者 / 特殊 或 普通 / 邪恶 / 反制）
    public RectTransform chipLv3;          // 第 3 栏容器（费数 / 特性）
    public ScrollRect cardScroll;
    public RectTransform gridRoot;         // 卡牌容器（= cardScroll.content）
    public TextMeshProUGUI counterText;    // 右下角 n / 总数

    public TMP_FontAsset font;

    // ── 运行时状态 ───────────────────────────────────────────────────────
    Root _root = Root.All;
    readonly HashSet<int> _subSel = new HashSet<int>();      // 第 2 栏的类别 / 分型（0..2）
    readonly HashSet<int> _tagSel = new HashSet<int>();      // 第 3 栏的费数 / 特性
    List<CardData> _all = new List<CardData>();
    readonly List<GameObject> _cards = new List<GameObject>();
    readonly List<Chip> _chips = new List<Chip>();
    Texture2D _texN, _texH, _texO;
    bool _built;
    int _shown;                     // 当前筛选下应有多少张（= 计数行的分子）
    int _buildGen;                  // 铺卡换代号：筛选一变就 +1，旧协程见到自己不是当前代就退场
    Coroutine _fillCo;              // 正在分帧铺卡的那条协程
    LobbyCardsIntro _intro;         // 打开面板时「前两栏 + 右侧前两排浮上来」的入场（Play 才建，见 PlayCardsIntro）
    LobbyCardDetailPanel _detail;   // 点一张卡 → 类全屏卡牌详情（同样 Play 才建，见 OpenDetail）
    readonly HashSet<int> _shownChips = new HashSet<int>();                  // 上一次重建后已在屏上的格子（判「新显示」用）
    readonly List<RectTransform> _freshChips = new List<RectTransform>();    // 本次重建里新出现的格子

    class Chip
    {
        public int level;
        public int index;
        public string label;
        public LobbyFilterChip view;
    }

    // ═══════════════════════════════════════════════════════════════════════
    void Awake() { EnsureData(); }

    /// <summary>把「一次性准备」收在一处，并且**谁用谁保**。
    /// 教训（Stage87 实测）：编辑器里给已有物件 AddComponent **不会触发 Awake**，
    /// 所以光把加载写在 Awake 里，编辑态（以及任何 Awake 没跑到的时候）_all 就是空的 —— 右侧永远 0 张。
    /// 改成惰性 + 幂等：_all 为空就现读一次。</summary>
    void EnsureData()
    {
        if (_all.Count > 0) return;
        if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts & Materials/NotoSerifCJKsc-Bold SDF");
        if (_texN == null) _texN = LoadTex(FilterChipTex);
        if (_texH == null) _texH = LoadTex(FilterChipHoverTex);
        if (_texO == null) _texO = LoadTex(FilterChipOnTex);
        if (gridRoot == null && cardScroll != null) gridRoot = cardScroll.content;
        LoadAllCards();
    }

    // ⚠ 入场必须排在 EnsureBuilt 之后：筛选格是运行时建的，先摆位再动它们才有基准位置
    //   （启动时 OnEnable 那次重建发生在面板还关着的时候，不播 —— 播了也看不见）。
    public void OnSubPanelOpened()
    {
        if (_detail != null) _detail.Close();   // 上次关面板时它只是跟着父级一起 inactive，状态还在 —— 开面板先收掉
        EnsureBuilt();
        PlayCardsIntro();
    }

    /// <summary>骨架里的引用是生成器填的；编辑器重跑生成器后、或者引用被清空时，这里兜一次。
    /// 只要容器对得上就刷 —— 不刷的话右侧会一直是空的（Stage85 实测：Refresh 跑在引用回填之前 = 0 张）。</summary>
    void OnEnable()
    {
        if (!Application.isPlaying) return;
        if (!_built && gridRoot != null && chipLv0 != null) EnsureBuilt();
    }

    void OnDisable()
    {
        // 面板被关掉：详情是子物体会跟着 inactive，但「开着」这个状态还在 —— 显式收掉，
        // 顺便把那张 Instantiate 出来的放大卡面销毁，不留垃圾。
        if (_detail != null) _detail.Close();
    }

    /// <summary>把**上一次运行时生出来**的件清干净（幂等前提）。
    /// 本面板的骨架件是编辑器里存的（chipLv0/1/2/3 容器、ScrollRect、计数行），运行时只往里塞「筛选格」和
    /// 「卡牌预览」，那两批会随选择反复重建 —— 清的时候必须清干净，否则重开会叠。
    /// ⚠ 编辑态不能用 <c>Destroy</c>：编辑器下（不在 Play）它只登记删除、当帧不执行，`Destroy` 完
    ///   容器里还留着旧件 —— 实测就是「点一次多出一组格」。用 <c>DestroyImmediate</c> 才是同步的。</summary>
    void ClearSpawnedChips()
    {
        for (int i = 0; i < _chips.Count; i++)
            if (_chips[i].view != null) Kill(_chips[i].view.gameObject);
        _chips.Clear();
        // 兜底：容器里可能还留着不属于 _chips 的记录（上一次跑崩断在中途）——按容器实际子物体再清一遍
        RectTransform[] holders = { chipLv0, chipLv1, chipLv2, chipLv3 };
        for (int c = 0; c < holders.Length; c++)
        {
            if (holders[c] == null) continue;
            for (int i = holders[c].childCount - 1; i >= 0; i--) Kill(holders[c].GetChild(i).gameObject);
        }
    }

    void ClearSpawnedCards()
    {
        for (int i = 0; i < _cards.Count; i++) if (_cards[i] != null) Kill(_cards[i]);
        _cards.Clear();
        if (gridRoot == null) return;
        for (int i = gridRoot.childCount - 1; i >= 0; i--) Kill(gridRoot.GetChild(i).gameObject);
    }

    static void Kill(GameObject go)
    {
        if (go == null) return;
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }

    static Texture2D LoadTex(string path)
    {
        var t = Resources.Load<Texture2D>(ResRoot + System.IO.Path.GetFileNameWithoutExtension(path));
        if (t != null) return t;
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
#else
        return null;
#endif
    }

    void LoadAllCards()
    {
        _all.Clear();
        foreach (var d in Resources.LoadAll<CardData>("CardData")) if (d != null) _all.Add(d);
        foreach (var d in Resources.LoadAll<CardData>("ChosenOneData")) if (d != null) _all.Add(d);
        _all.Sort((a, b) => string.CompareOrdinal(a.templateID, b.templateID));
    }

    void EnsureBuilt()
    {
        if (_built) { Refresh(); return; }
        _built = true;
        Refresh();
    }

    /// <summary>打开面板的入场：左栏**前两栏**一格一组、右侧卡牌**前两排**一排一组，各自上浮 + 淡入；
    /// 第 2 / 3 栏、第三排起的卡、金线与计数行都不动。</summary>
    /// <remarks>2026-09-28 用户：「优化动态进入效果…只限进入界面时展示在玩家视角里的（大概只是前两栏），
    /// 其它的不会有这个动画」→ 追加「右侧卡牌也应做」→ 再修：「不是透明，是类似于左侧栏的滑入，并且第一排和
    /// 第二排不是同时滑入，第一比第二快一点点，后面排就不需要做了」→ 再修：「不再像之前那样 0.32 秒全透明」
    /// （中间那版把右侧排在左栏之后起手、整块淡入，右侧要空 0.32 s —— 作废）。
    /// 入场组件**不进场景**（重跑 LobbyUIBuilder 会把用户手调过的版式冲掉），运行时现建一个挂在本物体上。</remarks>
    void PlayCardsIntro()
    {
        if (!Application.isPlaying) return;      // 编辑态没有 Update，跑一半会把格子留在半透明位置

        var groups = new List<LobbyCardsIntro.Group>();

        // ① 左栏前两栏：一格一组（逐格错开，跟开始界面一样）。
        // ⚠ 从 _chips 取、不遍历容器：Play 里 ClearSpawnedChips 清掉的那批旧格子在开面板那一帧还没销毁，
        //   容器 childCount 会是两倍，序号就错开了。
        int chipN = 0;
        for (int i = 0; i < _chips.Count; i++)
        {
            Chip c = _chips[i];
            if (c.level > 1 || c.view == null) continue;
            var rt = c.view.transform as RectTransform;
            if (rt == null) continue;
            groups.Add(new LobbyCardsIntro.Group(IntroAt + IntroChipStep * chipN, new List<RectTransform> { rt }));
            chipN++;
        }

        // ② 右侧卡牌：一排一组，只做**前两排**；第一排**和左栏第一个格子同时起手**（不再排在左栏后面 ——
        //    用户「不再像之前那样 0.32 秒全透明」），第二排比第一排晚一点点。
        //    第三排起不进场：视口高 788、第二排下沿才到 823，第三排整个在视口外，做了也看不见。
        //    ⚠ 取的是 _cards 前 10 个（RebuildCards 先同步铺 15 张，所以它们一定在），
        //      不是遍历 Grid_Cards —— Play 里销毁到帧末才生效，容器里还压着上一批同名的卡。
        groups.Add(new LobbyCardsIntro.Group(IntroAt, GridRow(0)));
        groups.Add(new LobbyCardsIntro.Group(IntroAt + IntroRowStep, GridRow(1)));
        groups.RemoveAll(gr => gr.items == null || gr.items.Count == 0);
        if (groups.Count == 0) return;

        Intro().PlayGroups(groups);
    }

    /// <summary>切父级（全部 / 召唤物 / 法术）时子集与费数那两栏会整批换成新格子 —— 只让**新出现的**滑出来。</summary>
    /// <remarks>2026-09-28 用户：「左侧栏每次新显示的也应是滑出来的」。已经在屏上的（全部 / 召唤物 / 法术，
    /// 以及同父级下没变的子集）不重播；点「子集 / 费数」那条路根本不重建格子（只刷选中态），所以不用管。</remarks>
    void PlayFreshChipsIntro()
    {
        if (!Application.isPlaying) return;      // 编辑态没有 Update，跑一半会把格子留在半透明位置
        if (_freshChips.Count == 0) return;

        // 一格一组、顺位向下（_freshChips 就是建格顺序：先第 2 栏、再第 3 栏）—— 像一排排滑下来。
        var groups = new List<LobbyCardsIntro.Group>(_freshChips.Count);
        for (int i = 0; i < _freshChips.Count; i++)
            groups.Add(new LobbyCardsIntro.Group(IntroAt + IntroFreshStep * i,
                                                 new List<RectTransform> { _freshChips[i] }));
        Intro().PlayGroups(groups);
    }

    /// <summary>入场组件：**不进场景**（重跑 LobbyUIBuilder 会把用户手调过的版式冲掉），Play 里现建一个。</summary>
    LobbyCardsIntro Intro()
    {
        if (_intro == null) _intro = GetComponent<LobbyCardsIntro>();
        if (_intro == null) _intro = gameObject.AddComponent<LobbyCardsIntro>();
        return _intro;
    }

    /// <summary>卡牌网格里第 <paramref name="row"/> 排的卡（一排 <see cref="GridColumns"/> 张，按 _cards 的铺卡顺序）。</summary>
    List<RectTransform> GridRow(int row)
    {
        var list = new List<RectTransform>();
        int from = row * GridColumns;
        int to = Mathf.Min(from + GridColumns, _cards.Count);
        for (int i = from; i < to; i++)
        {
            if (_cards[i] == null) continue;
            var rt = _cards[i].transform as RectTransform;
            if (rt != null) list.Add(rt);
        }
        return list;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 筛选格
    // ═══════════════════════════════════════════════════════════════════════

    static readonly string[] SubLabels = { "英雄", "神选者", "特殊", "普通", "邪恶", "反制" };

    // 第 1 栏里「召唤物 / 法术」这两个根选项的槽位号 —— 与类别（0..2）/ 分型（3..5）**错开**，
    // 否则第 1 栏同时存在两套 0..2 的 index，选中态会串。它们不参与过滤（过滤由 _root 承担），
    // 只是「点一下切根级」的入口。
    const int IdxRootSummon = 8;
    const int IdxRootSpell  = 9;

    /// <summary>召唤物第 3 栏：3 个一格、顺位向下（先手 / 反击 / 附着 / 抛置 是第 4 行）。</summary>
    static readonly string[] SummonTags =
    {
        "0费", "1费", "3费", "5费",
        "进场", "退场", "主动退场",
        "先手", "反击", "附着",
        "抛置"
    };

    /// <summary>法术第 3 栏：3 个一格、顺位向下。</summary>
    static readonly string[] SpellTags =
    {
        "0费", "1费", "2费",
        "3费", "4费", "5费"
    };

    void Refresh()
    {
        if (gridRoot == null) return;
        RebuildChips();
        RebuildCards();   // 回顶部在 RebuildCards 末尾（点筛选格那条路只走它）
    }

    void RebuildChips()
    {
        EnsureData();
        // 点筛选格会把前两栏的格子整个重建：先把还在动的入场停掉并还原，
        // 否则新格子会继承旧动画的中间态（半透明 / 偏下）。
        if (_intro != null) _intro.Stop();
        ClearChips();

        // 第 0 栏：全部（永远在）
        AddChip(chipLv0, 0, 0, "全部", () => PickRoot(Root.All));

        // 第 1 栏：召唤物 / 法术**常驻**（2026-09-28 用户：点了子集不许把它们藏起来）——
        //   它们只干一件事：切根级。子集另起第 2 栏，父级留在原位、保持点亮。
        AddChip(chipLv1, 1, IdxRootSummon, "召唤物", () => PickRoot(Root.Summon));
        AddChip(chipLv1, 1, IdxRootSpell,  "法术",   () => PickRoot(Root.Spell));

        // 第 2 栏：子集 —— 父级互斥 ⇒ 两组永远不会同时出现，不重叠
        if (_root == Root.Summon)
        {
            for (int i = 0; i < 3; i++) { int k = i; AddChip(chipLv2, 2, k, SubLabels[k], () => PickSub(k)); }        // 英雄 / 神选者 / 特殊
        }
        else if (_root == Root.Spell)
        {
            for (int i = 0; i < 3; i++) { int k = i + 3; AddChip(chipLv2, 2, k, SubLabels[k], () => PickSub(k)); }    // 普通 / 邪恶 / 反制
        }

        // 第 3 栏：费数 / 特性
        if (_root == Root.Summon)
            for (int i = 0; i < SummonTags.Length; i++) { int k = i; AddChip(chipLv3, 3, k, SummonTags[k], () => PickTag(k)); }
        else if (_root == Root.Spell)
            for (int i = 0; i < SpellTags.Length; i++) { int k = i; AddChip(chipLv3, 3, k, SpellTags[k], () => PickTag(k)); }

        RefreshChipStates();
        CollectFreshChips();
    }

    /// <summary>把「这一次重建里新出现」的格子收进 <see cref="_freshChips"/>（上一次就在屏上的不算）。</summary>
    /// <remarks>身份 = 父级 x 层级 x 序号：第 3 栏的 0费 / 1费 在召唤物与法术下**同号**，不带父级会把换过去的
    /// 那几张误判成「没变」而硬跳出来。前两栏（全部 / 召唤物 / 法术）是常驻的，一律不算新。</remarks>
    void CollectFreshChips()
    {
        _freshChips.Clear();
        var now = new HashSet<int>();
        for (int i = 0; i < _chips.Count; i++)
        {
            Chip c = _chips[i];
            int key = ChipKey(c);
            now.Add(key);
            if (c.level < 2 || c.view == null) continue;
            if (_shownChips.Contains(key)) continue;
            var rt = c.view.transform as RectTransform;
            if (rt != null) _freshChips.Add(rt);
        }
        _shownChips.Clear();
        foreach (int k in now) _shownChips.Add(k);
    }

    int ChipKey(Chip c)
    {
        int rootTag = _root == Root.Summon ? 1 : _root == Root.Spell ? 2 : 0;
        return rootTag * 10000 + c.level * 100 + c.index;
    }

    void ClearChips() { ClearSpawnedChips(); }

    /// <summary>建一格并摆位：栏内 3 个一行（格宽 108 + 间隔 12），行距 60。</summary>
    void AddChip(RectTransform parent, int level, int index, string label, Action act)
    {
        // ⚠ 排位按**建格顺序**（本栏第几个），不能按 index —— index 是语义号
        //   （召唤物 / 法术 用 8 / 9 与类别 0..2 错开），拿它算槽位会把「召唤物 / 法术」摆成第 3 / 第 4 行
        //   （2026-09-28 用户那张图里两格一上一下、还差 60 px 就是这么来的）。
        int slot = 0;
        for (int i = 0; i < _chips.Count; i++) if (_chips[i].level == level) slot++;

        RectTransform rt = NewRect(parent, "Chip_" + level + "_" + index + "_" + label,
                                   new Vector2(0f, 1f), new Vector2(0f, 1f), ChipPos(slot),
                                   new Vector2(LcChipW, LcChipH));

        // ⚠ 板必须**居中**在格上：原来 anchor 给 (0,0) + 轴 (0.5,0.5) ⇒ 板心落在格的左下角，
        //   整块板被推下去半格，文字却仍在格中央 —— 看着就是「字和板错位」（2026-09-28 用户那张图）。
        RawImage plate = NewRaw(rt, "ChipPlate", _texN, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0f, new Vector2(LcChipW, LcChipH));
        plate.raycastTarget = true;   // 整格的可点区就是这张板

        // ⚠ 文字要**另建一个子物体、且建在板之后**：同一层里后面的画在上面，直接挂在格根上的
        //   TextMeshProUGUI 属于父级、永远被子的板盖住 —— 板一居中，字就整片看不见了（2026-09-28 实测）。
        RectTransform labelRT = NewRect(rt, "ChipLabel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                        Vector2.zero, new Vector2(LcChipW, LcChipH));
        TextMeshProUGUI text = labelRT.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = label;
        // ⚠ 字号按**板的内金线**算：贴图内缩 18 + 金线 3/2 ⇒ 能写字的地方 ~= 108 - 2 x 19.5 = 69 px。
        //   4 字标签（主动退场）按 26 排是 104 —— 直接顶穿金边（2026-09-28 用户：「有点超出边框」）。
        text.fontSize = label.Length >= 4 ? LcChipFontLong : LcChipFont;
        text.color = Cream;
        text.alignment = TextAlignmentOptions.Midline;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;

        var view = rt.gameObject.AddComponent<LobbyFilterChip>();
        view.plate = plate;
        view.label = text;
        view.normalTexture = _texN;
        view.hoverTexture = _texH;
        view.onTexture = _texO;
        view.normalColor = Cream;
        view.goldColor = GoldBright;
        view.onClick = act;

        _chips.Add(new Chip { level = level, index = index, label = label, view = view });
    }

    /// <summary>槽位 → 位置：栏内 3 个一行（格宽 108 + 间隔 12），行距 60。<paramref name="slot"/> 是**建格顺序**。</summary>
    static Vector2 ChipPos(int slot)
    {
        int row = slot / 3, col = slot % 3;
        return new Vector2(col * (LcChipW + LcChipGap), -(row * LcChipRow));
    }

    void RefreshChipStates()
    {
        for (int i = 0; i < _chips.Count; i++)
        {
            var c = _chips[i];
            if (c.view == null) continue;
            c.view.SetOn(IsOn(c));
        }
    }

    bool IsOn(Chip c)
    {
        switch (c.level)
        {
            case 0:                                   // 「全部」
                return _root == Root.All;
            case 1:                                   // 召唤物 / 法术（常驻；点了子集也保持点亮）
                if (c.index == IdxRootSummon) return _root == Root.Summon;
                if (c.index == IdxRootSpell)  return _root == Root.Spell;
                return false;
            case 2:                                   // 子集：英雄..特殊 / 普通..反制
                return _subSel.Contains(c.index);
            default:                                  // 费数 / 特性
                return _tagSel.Contains(c.index);
        }
    }

    // ── 点击 ─────────────────────────────────────────────────────────────
    void PickRoot(Root r)
    {
        _root = r;
        _subSel.Clear();
        _tagSel.Clear();
        Refresh();
        PlayFreshChipsIntro();   // 子集 / 费数那两栏换了一批 => 新的那批滑出来
    }

    void PickSub(int idx)
    {
        if (_root == Root.Summon && idx > 2) return;
        if (_root == Root.Spell && idx < 3) return;
        if (!_subSel.Remove(idx)) _subSel.Add(idx);
        RebuildCards();
        RefreshChipStates();
    }

    void PickTag(int idx)
    {
        if (!_tagSel.Remove(idx)) _tagSel.Add(idx);
        RebuildCards();
        RefreshChipStates();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 过滤
    // ═══════════════════════════════════════════════════════════════════════

    bool Pass(CardData d)
    {
        if (d == null) return false;
        if (_root == Root.Summon) { if (d.cardType != CardType.Summon) return false; }
        else if (_root == Root.Spell) { if (d.cardType != CardType.Spell) return false; }

        if (_subSel.Count > 0)
        {
            bool ok = false;
            foreach (int i in _subSel)
            {
                if (_root == Root.Summon)
                {
                    if (i == 0 && d.summonType == SummonType.Hero) ok = true;
                    else if (i == 1 && d.summonType == SummonType.ChosenOne) ok = true;
                    else if (i == 2 && d.summonType == SummonType.Special) ok = true;
                }
                else if (_root == Root.Spell)
                {
                    if (i == 3 && d.spellType == SpellType.Normal) ok = true;
                    else if (i == 4 && (d.spellType & SpellType.Evil) != 0) ok = true;
                    else if (i == 5 && (d.spellType & SpellType.Counter) != 0) ok = true;
                }
                if (ok) break;
            }
            if (!ok) return false;
        }

        if (_tagSel.Count > 0)
        {
            var arr = _root == Root.Spell ? SpellTags : SummonTags;
            bool ok = false;
            foreach (int i in _tagSel)
            {
                if (i < 0 || i >= arr.Length) continue;
                if (TagMatch(d, arr[i])) { ok = true; break; }
            }
            if (!ok) return false;
        }
        return true;
    }

    static bool TagMatch(CardData d, string tag)
    {
        switch (tag)
        {
            case "0费": return d.baseCost == 0;
            case "1费": return d.baseCost == 1;
            case "2费": return d.baseCost == 2;
            case "3费": return d.baseCost == 3;
            case "4费": return d.baseCost == 4;
            case "5费": return d.baseCost == 5;
            case "进场":     return d.hasOnEnter;
            case "退场":     return d.hasOnDeath;
            case "主动退场": return d.hasActiveExit;
            case "先手":     return d.hasFirstStrike;
            case "反击":     return d.hasRevenge;
            case "附着":     return d.canAttach;
            case "抛置":     return d.hasDiscard;
        }
        return false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 卡牌网格
    // ═══════════════════════════════════════════════════════════════════════

    void RebuildCards()
    {
        EnsureData();
        if (gridRoot == null) return;
        // 点筛选格 / 切根级会把这批卡整个重建：先把还在动的入场停掉并还原（前两排的卡和左栏的格子都算），
        // 否则新铺的卡会继承旧动画的中间态。
        if (_intro != null) _intro.Stop();
        ClearSpawnedCards();

        float paneW = PaneW;
        if (cardScroll != null && cardScroll.viewport != null && cardScroll.viewport.rect.width > 1f)
            paneW = cardScroll.viewport.rect.width;
        float cellW = (CardSizeW + CardGapX) * CardScale;      // 270
        float cellH = (CardSizeH + CardGapY) * CardScale;      // 427.5
        float blockW = GridColumns * CardSizeW * CardScale + (GridColumns - 1) * CardGapX * CardScale;   // 1287.5
        float gridPadX = Mathf.Max(0f, (paneW - blockW) * 0.5f);                                          // 45

        // 先把过滤结果摊出来（不 Instantiate，只挑模板）—— 张数要提前知道
        var want = new List<CardData>();
        for (int i = 0; i < _all.Count; i++)
        {
            CardData d = _all[i];
            if (d == null || !Pass(d)) continue;
            if ((d.cardType == CardType.Spell ? d.spell2DPrefab : d.card2DPrefab) == null) continue;
            want.Add(d);
        }

        // 容器高度**先按最终张数撑好**：滚动条 / 滑块位置当场就是对的，后面铺卡时不会边走边跳。
        int rows = Mathf.Max(1, Mathf.CeilToInt((float)want.Count / GridColumns));
        gridRoot.anchorMin = new Vector2(0f, 1f);
        gridRoot.anchorMax = new Vector2(0f, 1f);
        gridRoot.pivot = new Vector2(0f, 1f);
        gridRoot.sizeDelta = new Vector2(paneW, rows * cellH);

        _shown = want.Count;                                    // = 计数行的分子（不是「已经铺了几张」，是这一屏该有几张）
        if (counterText != null) counterText.text = want.Count + " / " + _all.Count;

        // 回顶部。⚠ 必须放在这里（不是只放 Refresh）：点筛选格走的是 PickSub / PickTag -> RebuildCards，
        // 不经过 Refresh —— 内容一变小，ScrollRect 保持像素偏移，视口就停在半张卡上（用户最早那张图就是这样）。
        if (cardScroll != null) cardScroll.verticalNormalizedPosition = 1f;

        // 换代：筛选一变，上一条铺卡协程自己退场（否则新旧两条会一起往同一个容器里塞）
        _buildGen++;
        if (_fillCo != null) { StopCoroutine(_fillCo); _fillCo = null; }

        int head = Mathf.Min(InstantCards, want.Count);
        for (int i = 0; i < head; i++) PlaceCard(want[i], i, cellW, cellH, gridPadX);
        if (head >= want.Count) return;

        // 编辑态（没有 Update 循环）没有协程可跑，直接同步铺完；Play 里才分帧
        if (Application.isPlaying && isActiveAndEnabled)
            _fillCo = StartCoroutine(FillCards(want, head, cellW, cellH, gridPadX));
        else
            for (int i = head; i < want.Count; i++) PlaceCard(want[i], i, cellW, cellH, gridPadX);
    }

    /// <summary>分帧把剩下的卡铺完。<paramref name="from"/> 起是还没铺的序号；每帧最多花 <see cref="CardsPerFrameBudgetMs"/> 毫秒。</summary>
    IEnumerator FillCards(List<CardData> want, int from, float cellW, float cellH, float gridPadX)
    {
        int gen = _buildGen;
        int i = from;
        while (i < want.Count)
        {
            if (gen != _buildGen) yield break;                  // 换代了：让位给新的那条
            float t0 = Time.realtimeSinceStartup;
            while (i < want.Count && (Time.realtimeSinceStartup - t0) * 1000f < CardsPerFrameBudgetMs)
            {
                PlaceCard(want[i], i, cellW, cellH, gridPadX);
                i++;
            }
            yield return null;
        }
        _fillCo = null;
    }

    /// <summary>铺一张预览卡（建卡 + 摆位 + 挂悬停 / 点击）。<paramref name="index"/> 是**过滤后**的序号，决定行列。</summary>
    void PlaceCard(CardData d, int index, float cellW, float cellH, float gridPadX)
    {
        // 建卡那段与「卡牌详情」左半区那张放大卡同源（LobbyCardPreview.Create）
        GameObject go = LobbyCardPreview.Create(d, gridRoot, CardScale);
        if (go == null) return;
        go.name = "CardPreview_" + d.templateID;

        RectTransform rt = go.GetComponent<RectTransform>();
        int row = index / GridColumns, col = index % GridColumns;
        rt.anchoredPosition = new Vector2(gridPadX + col * cellW + cellW * 0.5f,
                                          -(row * cellH + cellH * 0.5f));

        // 悬停把卡抬起来一点、点击开详情。⚠ 必须在摆好位之后 Init：它拿当前位置当「静止态」。
        var tile = go.AddComponent<LobbyCardTile>();
        CardData captured = d;
        tile.Init(CardScale, () => OpenDetail(captured));

        _cards.Add(go);
    }

    /// <summary>点一张卡：开这张卡的详情。详情面板 Play 才现建（不进场景 —— 重跑生成器会把版式冲掉）。</summary>
    void OpenDetail(CardData d)
    {
        if (!Application.isPlaying) return;      // 编辑态没有 EventSystem，走不到这儿；保险
        if (d == null) return;
        EnsureData();
        if (_detail == null) _detail = LobbyCardDetailPanel.Create(transform, font);
        _detail.Open(d);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 小工具（与生成器同口径的建件）
    // ═══════════════════════════════════════════════════════════════════════

    static readonly Color Cream = new Color32(240, 232, 210, 236);
    static readonly Color GoldBright = new Color32(228, 203, 132, 255);

    // 左栏几何 —— 与 LobbyUIBuilder 的 Lc* 常量必须一致
    const float LcChipW   = 108f;
    const float LcChipH   = 52f;
    const float LcChipGap = 12f;
    const float LcChipRow = 60f;
    const float LcChipFont = 20f;        // 3 字（召唤物 / 神选者 / 主动…）=> 60，仍在内金线里侧留余量
    const float LcChipFontLong = 16f;    // 4 字（主动退场）=> 64

    // 入场时间轴（秒 · 从点开面板那一刻算）—— 节奏由面板排，动作由 LobbyCardsIntro 放：
    //   左栏前两栏：一格一组，相邻 +IntroChipStep
    //   右侧卡牌：一排一组，第一排与「全部」同刻起手，第二排 +IntroRowStep
    const float IntroAt       = 0.05f;
    const float IntroChipStep = 0.075f;
    const float IntroRowStep  = 0.09f;
    const float IntroFreshStep = 0.045f;  // 切父级后新出现的那批：一格一组往下顺延（14 格 ≈ 0.63 s 起完）

    static RectTransform NewRect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static RawImage NewRaw(Transform parent, string name, Texture tex, Vector2 anchor, Vector2 pivot, float posY, Vector2 size)
    {
        RectTransform rt = NewRect(parent, name, anchor, pivot, new Vector2(0f, posY), size);
        var img = rt.gameObject.AddComponent<RawImage>();
        img.texture = tex;
        img.raycastTarget = false;
        return img;
    }
}
