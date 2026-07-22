using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 无畏者(01319)反制牌选择弹窗。
/// 列出对方所有已放置未触发的反制牌——默认显示背面，单选，确认按钮在选中后出现。
/// </summary>
public class CounterSelectionPanel : MonoBehaviour
{
    public static CounterSelectionPanel Instance { get; private set; }

    [Header("Root")]
    public GameObject panelRoot;
    public Transform cardContainer;
    public Button confirmButton;
    public Button cancelButton;

    [Header("Layout")]
    public int cardsPerRow = 5;
    public float cardSpacing = 25f, rowSpacing = 25f;
    public float startX = -172f, startY = 282f;

    List<CounterCard> _counters;
    CounterCard _selected;
    List<GameObject> _spawned = new List<GameObject>();
    Action<CounterCard> _onConfirm;
    Action _onCancel;
    bool _done;

    void Awake() { Instance = this; panelRoot.SetActive(false); }

    public void Show(List<CounterCard> enemyCounters, Action<CounterCard> onConfirm, Action onCancel = null)
    {
        _counters = enemyCounters;
        _onConfirm = onConfirm;
        _onCancel = onCancel;
        _selected = null;
        _done = false;

        foreach (Transform t in cardContainer) Destroy(t.gameObject);
        _spawned.Clear();

        // 隐藏手牌
        var player = Player.Instance;
        if (player != null)
        {
            player.handCards.RemoveAll(c => c == null);
            foreach (var c in player.handCards) if (c) c.SetActive(false);
        }
        var hm = FindObjectOfType<HandManager>();
        if (hm) hm.SetHandAreaRaycast(false);

        var prefab = player?.spellCardPrefab2D;
        if (prefab == null) { Debug.LogError("[CounterSelectionPanel] spellCardPrefab2D 未绑定"); panelRoot.SetActive(false); return; }

        for (int i = 0; i < _counters.Count; i++)
        {
            var cc = _counters[i];
            var td = cc.template;
            if (td == null) continue;

            var go = Instantiate(prefab, cardContainer);
            _spawned.Add(go);

            // 填充 CardInstance 用于显示
            var di = go.GetComponent<CardInstance>() ?? go.AddComponent<CardInstance>();
            di.templateID = td.templateID;
            di.currentCost = td.baseCost;

            // 背面显示（隐藏状态）
            bool showBack = !IsRevealed(cc);
            var disp = go.GetComponent<CardDisplay2D>();
            if (disp != null)
            {
                if (showBack)
                    ShowBackFace(disp, td);
                else
                    ShowFrontFace(disp, td, di);
            }

            // 高亮框
            var highlight = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            highlight.color = new Color(1, 1, 1, 0); // 透明默认

            // 点击 = 单选 toggle
            var captured = cc;
            var capturedGo = go;
            var capturedHl = highlight;
            var btn = go.GetComponent<Button>() ?? go.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                if (_selected == captured)
                {
                    // 再次点击 → 取消选中
                    _selected = null;
                    capturedHl.color = new Color(1, 1, 1, 0);
                }
                else
                {
                    // 清除旧选中
                    if (_selected != null)
                    {
                        int prevIdx = _counters.IndexOf(_selected);
                        if (prevIdx >= 0 && prevIdx < _spawned.Count)
                        {
                            var prevHl = _spawned[prevIdx].GetComponent<Image>();
                            if (prevHl) prevHl.color = new Color(1, 1, 1, 0);
                        }
                    }
                    _selected = captured;
                    capturedHl.color = new Color(1f, 0.84f, 0f, 0.5f); // 金色选中
                }
                confirmButton.gameObject.SetActive(_selected != null);
            });

            // Transform 布局
            int row = i / cardsPerRow, col = i % cardsPerRow;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(startX + col * cardSpacing, startY - row * rowSpacing);
        }

        confirmButton.gameObject.SetActive(false);
        confirmButton.onClick.RemoveAllListeners();
        confirmButton.onClick.AddListener(OnConfirm);
        cancelButton.onClick.RemoveAllListeners();
        cancelButton.onClick.AddListener(OnCancel);

        panelRoot.SetActive(true);
    }

    void OnConfirm()
    {
        if (_selected == null || _done) return;
        _done = true;
        panelRoot.SetActive(false);
        Cleanup();
        _onConfirm?.Invoke(_selected);
    }

    void OnCancel()
    {
        if (_done) return;
        _done = true;
        panelRoot.SetActive(false);
        Cleanup();
        _onCancel?.Invoke();
    }

    void Cleanup()
    {
        foreach (var go in _spawned) Destroy(go);
        _spawned.Clear();
        var hm = FindObjectOfType<HandManager>();
        if (hm) hm.SetHandAreaRaycast(true);
        var cd = FindObjectOfType<CardDrag>();
        if (cd) cd.SetButtonsInteractable(true);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 可见性判定
    // ═══════════════════════════════════════════════════════════════════

    static bool IsRevealed(CounterCard cc)
    {
        // 对方反制牌默认隐藏。未来：真视之眼(01130)在场时可见正面。
        return false;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 2D 卡片显示
    // ═══════════════════════════════════════════════════════════════════

    static void ShowBackFace(CardDisplay2D disp, CardData td)
    {
        // 名字显示"反制牌"
        if (disp.nameText != null) disp.nameText.text = "反制牌";
        if (disp.prefixText != null) disp.prefixText.text = "";
        if (disp.attackText != null) disp.attackText.gameObject.SetActive(false);
        if (disp.healthText != null) disp.healthText.gameObject.SetActive(false);
        if (disp.costText != null) disp.costText.gameObject.SetActive(false);
        if (disp.effectText != null) disp.effectText.gameObject.SetActive(false);

        // 替换为卡背贴图
        var img = disp.GetComponent<UnityEngine.UI.Image>();
        if (img != null && td.spellCardBackSprite2D != null)
            img.sprite = td.spellCardBackSprite2D;
    }

    static void ShowFrontFace(CardDisplay2D disp, CardData td, CardInstance di)
    {
        // 正面正常显示
        disp.RefreshWithInstance(di);
    }
}
