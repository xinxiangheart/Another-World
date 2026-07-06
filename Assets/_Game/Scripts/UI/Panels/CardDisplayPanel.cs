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

        foreach (Transform t in cardContainer) Destroy(t.gameObject);
        createdCards.Clear();

        for (int i = 0; i < cards.Count; i++)
        {
            var ci = cards[i];
            if (!ci) continue;
            var td = CardDatabase.Instance?.GetTemplate(ci.templateID);
            if (!td) continue;

            var prefab = td.cardType == CardType.Spell ? player.spellCardPrefab2D : player.cardPrefab2D;
            var go = Instantiate(prefab, cardContainer);

            var cv = go.GetComponent<CardView>();
            if (cv) { cv.enabled = false; cv.handManager = null; }
            var cd2 = go.GetComponent<CardDrag>();
            if (cd2) cd2.enabled = false;

            var di = go.GetComponent<CardInstance>() ?? go.AddComponent<CardInstance>();
            di.templateID = ci.templateID;
            di.instanceID = ci.instanceID;
            di.currentCost = ci.currentCost;
            di.currentAttack = ci.currentAttack;
            di.currentHealth = ci.currentHealth;
            di.currentMaxHealth = ci.currentMaxHealth;
            di.currentTier = ci.currentTier;
            di.prefixes = ci.prefixes;
            go.GetComponent<CardDisplay2D>()?.RefreshWithInstance(di);

            int row = i / cardsPerRow, col = i % cardsPerRow;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(83.333333f, 146.333333f);
            rt.anchoredPosition = new Vector2(startX + col * (83.333333f + cardSpacing), startY - row * (146.333333f + rowSpacing));

            if (f == null || f(ci))
            {
                var btn = go.AddComponent<Button>();
                var cap = ci;
                var capGo = go;
                btn.onClick.AddListener(() =>
                {
                    Debug.Log($"卡片被点击: {cap.templateID}");
                    Click(cap, capGo);
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
        crt.sizeDelta = new Vector2(crt.sizeDelta.x, Mathf.Max(vh, rows * (146.333333f + rowSpacing)));

        panelRoot.SetActive(true);
        panelRoot.transform.SetAsLastSibling();
    }

    public void ShowWithCallback(List<CardInstance> list, Func<CardInstance, bool> f, Action ok, string txt = "确认")
    {
        Show(list, f, txt);
        onOk = ok;
    }
    void Click(CardInstance ci, GameObject go)
    {
        Debug.Log($"Click进入: multiSelect={multiSelect}, ci={ci?.templateID}");

        if (multiSelect)
        {
            bool alreadySelected = selectedCards.Exists(s => s.instanceID == ci.instanceID);
            if (alreadySelected)
            {
                selectedCards.RemoveAll(s => s.instanceID == ci.instanceID);
                go.transform.localScale = Vector3.one;
            }
            else
            {
                selectedCards.Add(ci);
                go.transform.localScale = Vector3.one * 1.15f;
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
                Debug.Log($"费用检测: selectedCards.Count={selectedCards.Count}, totalCost={totalCost}, maxTotalCost={maxTotalCost}");
                if (selectedCards.Count == 0 || totalCost > maxTotalCost)
                    showConfirm = false;
            }

            Debug.Log($"showConfirm={showConfirm}, selectedCards.Count={selectedCards.Count}");

            if (showConfirm && selectedCards.Count > 0)
            {
                var csb = ConfirmSelectionButton.Instance;
                if (csb)
                {
                    csb.gameObject.SetActive(true);
                    csb.Show(() =>
                    {
                        Debug.Log($"确认按钮点击: onOk={onOk != null}");
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
            if (selected == ci)
            {
                selected = null;
                go.transform.localScale = Vector3.one;
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
                            c.transform.localScale = Vector3.one;
                            break;
                        }
                    }
                }

                selected = ci;
                go.transform.localScale = Vector3.one * 1.15f;
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
    }

    public CardInstance GetSelectedCard() => selected;
    public List<CardInstance> GetSelectedCards() => selectedCards;
}