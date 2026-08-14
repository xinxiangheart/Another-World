using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardDisplayPanel : MonoBehaviour
{
    public static CardDisplayPanel Instance { get; private set; }
    public GameObject panelRoot;
    public Transform cardContainer;
    public ScrollRect scrollRect;
    public int cardsPerRow = 5;
    public float cardSpacing = 25f, rowSpacing = 25f, startX = -172f, startY = 282f;

    public bool multiSelect = false;
    public bool showBack;        // 无畏者等：显示卡牌背面而非正面
    public string backLabel = "反制牌"; // 背面标题（可为召唤物自定义）
    public bool enableCostCheck;
    public int maxTotalCost;
    private List<CardInstance> cards;
    private Func<CardInstance, bool> filter;
    private CardInstance selected;
    private List<CardInstance> selectedCards = new List<CardInstance>();
    private Action onOk;
    private List<GameObject> createdCards = new List<GameObject>();

    void Awake() { Instance = this; panelRoot.SetActive(false); }

    public void Show(List<CardInstance> list, Func<CardInstance, bool> f, string txt = "确认")
    {
        cards = list; filter = f; onOk = null; selected = null;
        selectedCards.Clear();

        var player = Player.Instance;
        if (player != null)
        {
            player.handCards.RemoveAll(c => c == null);
            foreach (var c in player.handCards) if (c) c.SetActive(false);
        }

        var cd = FindObjectOfType<CardDrag>();
        if (cd) cd.SetButtonsInteractable(false);
        Card3DHover.allowDiscard = false;

        var hm = FindObjectOfType<HandManager>();
        if (hm) hm.SetHandAreaRaycast(false);

        // 弹窗期间禁止结束回合 — 任何需要玩家查看/选择的弹窗都应阻塞阶段推进
        FindObjectOfType<EndTurnButton>()?.SetInteractable(false);

        foreach (Transform t in cardContainer) Destroy(t.gameObject);
        createdCards.Clear();

        // Game 场景：Scale2DCard 设 localScale=3，视觉变大但 sizeDelta 不变
        // 布局间距必须匹配视觉物理大小（= sizeDelta × localScale）
        bool isGame = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Game";
        float scl = isGame ? 3f : 1f;

        float rawCardW = 83.333333f;
        float rawCardH = 146.333333f;
        float spX = cardSpacing;       // 间距也随 scales
        float spY = rowSpacing;
        float sx = startX;
        float sy = startY;

        for (int i = 0; i < cards.Count; i++)
        {
            var ci = cards[i];
            if (!ci) continue;
            var td = CardDatabase.Instance?.GetTemplate(ci.templateID);
            if (!td) continue;

            var prefab = td.cardType == CardType.Spell ? player.spellCardPrefab2D : player.cardPrefab2D;
            var go = Instantiate(prefab, cardContainer);
            if (isGame) Player.Scale2DCard(go);

            var cv = go.GetComponent<CardView>();
            if (cv) { cv.enabled = false; cv.handManager = null; }
            var cd2 = go.GetComponent<CardDrag>();
            if (cd2) cd2.enabled = false;

            var di = go.GetComponent<CardInstance>() ?? go.AddComponent<CardInstance>();
            di.templateID = ci.templateID;
            // instanceID 可能为 null/空（临时 CardInstance），同名不同实例的卡会因 null==null 被误判为同一张卡。
            // 用 _temp_ + 索引 生成唯一临时 ID，确保 Click 中按 instanceID 的去重/反选逻辑正确。
            di.instanceID = string.IsNullOrEmpty(ci.instanceID) ? $"_temp_{i}" : ci.instanceID;
            di.currentCost = ci.currentCost;
            di.currentAttack = ci.currentAttack;
            di.currentHealth = ci.currentHealth;
            di.currentMaxHealth = ci.currentMaxHealth;
            di.currentTier = ci.currentTier;
            di.prefixes = ci.prefixes;
            // 赋予的特性文本也复制 — 详情面板需要展示
            di.grantedTraitTexts = ci.grantedTraitTexts != null ? new System.Collections.Generic.List<string>(ci.grantedTraitTexts) : null;
            di.hasShield = ci.hasShield;
            di.poisoned = ci.poisoned;
            // 背面模式：ShowBack（无畏者等隐藏卡），正常模式：RefreshWithInstance
            if (showBack)
                go.GetComponent<CardDisplay2D>()?.ShowBack(td, backLabel);
            else
                go.GetComponent<CardDisplay2D>()?.RefreshWithInstance(di);

            int row = i / cardsPerRow, col = i % cardsPerRow;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(rawCardW, rawCardH);
            rt.anchoredPosition = new Vector2(
                sx + col * (rawCardW * scl + spX),
                sy - row * (rawCardH * scl + spY));

            if (f == null || f(ci))
            {
                var btn = go.AddComponent<Button>();
                var capGo = go;
                // 使用 go 上的 CardInstance（instanceID 已保证唯一）而非原始 ci，
                // 否则 instanceID 为 null 的临时卡会被误判为同一张
                var panelCard = di;
                btn.onClick.AddListener(() =>
                {
                    Click(panelCard, capGo);
                });
            }
            else
            {
                go.AddComponent<CanvasGroup>().alpha = 0.4f;
            }
            go.AddComponent<CardHover>();
            createdCards.Add(go);
        }

        int rows = Mathf.CeilToInt((float)cards.Count / cardsPerRow);
        float vh = scrollRect.viewport.rect.height;
        var crt = cardContainer.GetComponent<RectTransform>();
        crt.sizeDelta = new Vector2(crt.sizeDelta.x, Mathf.Max(vh, rows * (rawCardH * scl + spY)));

        panelRoot.SetActive(true);
        panelRoot.transform.SetAsLastSibling();
    }

    public void ShowWithCallback(List<CardInstance> list, Func<CardInstance, bool> f, Action ok, string txt = "确认")
    {
        // AI 环境：不弹面板，自动选第一个合法牌并触发回调
        if (SimpleAI.IsAIEvaluating)
        {
            cards = list;
            filter = f;
            selected = null;
            selectedCards.Clear();
            if (list != null)
            {
                foreach (var ci in list)
                {
                    if (ci == null) continue;
                    if (f == null || f(ci))
                    {
                        if (multiSelect) selectedCards.Add(ci);
                        else selected = ci;
                        break;
                    }
                }
            }
            onOk = ok;
            ok?.Invoke();
            return;
        }

        Show(list, f, txt);
        onOk = ok;
    }
    void Click(CardInstance ci, GameObject go)
    {
        Debug.Log($"Click进入: multiSelect={multiSelect}, ci={ci?.templateID}, iid={ci?.instanceID}");

        float scl = (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Game") ? 3f : 1f;
        Vector3 baseScale = Vector3.one * scl;

        if (multiSelect)
        {
            bool alreadySelected = selectedCards.Exists(s => s.instanceID == ci.instanceID);
            if (alreadySelected)
            {
                selectedCards.RemoveAll(s => s.instanceID == ci.instanceID);
                go.transform.localScale = baseScale;
            }
            else
            {
                selectedCards.Add(ci);
                go.transform.localScale = baseScale * 1.15f;
            }

            bool showConfirm = true;
            if (enableCostCheck)
            {
                int totalCost = 0;
                foreach (CardInstance c in selectedCards)
                {
                    CardData td = CardDatabase.Instance?.GetTemplate(c.templateID);
                    if (td != null) totalCost += td.baseCost;
                }
                if (selectedCards.Count == 0 || totalCost > maxTotalCost)
                    showConfirm = false;
            }

            if (showConfirm && selectedCards.Count > 0)
            {
                var csb = ConfirmSelectionButton.Instance;
                if (csb)
                {
                    csb.gameObject.SetActive(true);
                    csb.Show(() =>
                    {
                        onOk?.Invoke();
                        Hide();
                    });
                }
            }
            else
            {
                ConfirmSelectionButton.Instance?.Hide();
            }
        }
        else
        {
            if (selected != null && selected.instanceID == ci.instanceID)
            {
                selected = null;
                go.transform.localScale = baseScale;
                ConfirmSelectionButton.Instance?.Hide();
            }
            else
            {
                if (selected != null)
                {
                    foreach (var c in createdCards)
                    {
                        var inst = c.GetComponent<CardInstance>();
                        if (inst != null && inst.instanceID == selected.instanceID)
                        {
                            c.transform.localScale = baseScale;
                            break;
                        }
                    }
                }

                selected = ci;
                go.transform.localScale = baseScale * 1.15f;
                Test1Panel.Instance?.Show(ci);
                var csb = ConfirmSelectionButton.Instance;
                if (csb)
                {
                    csb.gameObject.SetActive(true);
                    csb.Show(() =>
                    {
                        onOk?.Invoke();
                        Hide();
                    });
                }
            }
        }
    }

    public void Hide()
    {
        showBack = false;
        enableCostCheck = false;
        panelRoot.SetActive(false);
        var csb = ConfirmSelectionButton.Instance;
        if (csb) csb.Hide();

        var player = Player.Instance;
        if (player != null)
        {
            player.handCards.RemoveAll(c => c == null);
            foreach (var c in player.handCards) if (c) c.SetActive(true);
        }

        var cd = FindObjectOfType<CardDrag>();
        if (cd) cd.SetButtonsInteractable(true);
        Card3DHover.allowDiscard = true;

        var hm = FindObjectOfType<HandManager>();
        if (hm)
        {
            hm.SetHandAreaRaycast(true);
            hm.RefreshLayout(true);
        }

        // 弹窗关闭后恢复结束回合按钮
        FindObjectOfType<EndTurnButton>()?.SetInteractable(true);
    }

    public CardInstance GetSelectedCard() => selected;
    public List<CardInstance> GetSelectedCards() => selectedCards;
}