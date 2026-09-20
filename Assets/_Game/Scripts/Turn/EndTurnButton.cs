using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Mirror;

/// <summary>
/// 结束回合按钮：灰色底盘 + 三态文字。
///   己方回合  -> 「结束回合」白色，可点；鼠标悬停变金（不放大）
///   对方回合  -> 「对方回合」灰色，不可点，不响应悬停
///   攻击回合  -> 「攻击回合」灰色，不可点，不响应悬停
///   （仅 PhaseStart 的过渡瞬间显示「准备阶段」，同样走灰色）
/// 阶段切换时文字绕 Y 轴翻转一次，翻过去就是新文字。
/// 点击时底盘下沉一下（按下感），压暗由 Button 的 ColorTint 负责。
/// </summary>
public class EndTurnButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("引用")]
    public Image plate;
    public TextMeshProUGUI label;

    [Header("文字配色")]
    public Color activeColor = new Color(0.992f, 0.972f, 0.941f, 1f);  // 己方回合：白
    public Color hoverColor  = new Color(0.886f, 0.710f, 0.345f, 1f);  // 悬停：金
    public Color idleColor   = new Color(0.588f, 0.565f, 0.541f, 1f);  // 其它阶段：灰

    [Header("动画")]
    public float flipHalfDuration = 0.11f;  // 翻转一半（0->90 / -90->0）用时
    public float pressDepth = 4f;           // 点击下沉像素
    public float pressDuration = 0.09f;

    Button _button;
    CanvasGroup _canvasGroup;
    bool _hover;
    bool _flipping;
    string _shown = "";
    string _pending;
    Vector2 _basePos;
    Coroutine _press;

    void Awake()
    {
        _button = GetComponent<Button>();
        if (plate == null) plate = GetComponent<Image>();
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _basePos = ((RectTransform)transform).anchoredPosition;
        if (_button != null) _button.onClick.AddListener(EndTurn);
    }

    void OnEnable()
    {
        // 从隐藏恢复时把文字直接落到位，避免补一次翻转
        _shown = "";
        _flipping = false;
        _pending = null;
        ApplyText(DisplayOf(TurnManager.Instance != null ? TurnManager.Instance.currentPhase : TurnManager.TurnPhase.PhaseStart));
        ApplyColor();
    }

    void Update()
    {
        TurnManager tm = TurnManager.Instance;
        if (tm == null) return;
        ApplyText(DisplayOf(tm.currentPhase));
        ApplyColor();
    }

    static string DisplayOf(TurnManager.TurnPhase phase)
    {
        switch (phase)
        {
            case TurnManager.TurnPhase.MyTurn:      return "结束回合";
            case TurnManager.TurnPhase.EnemyTurn:   return "对方回合";
            case TurnManager.TurnPhase.BattlePhase: return "攻击回合";
            default:                                return "准备阶段";
        }
    }

    // ── 文字 ────────────────────────────────────────────────────────────────
    void ApplyText(string want)
    {
        if (label == null || want == _shown) return;
        if (!isActiveAndEnabled) { _shown = want; label.text = want; return; }
        if (_flipping) { _pending = want; return; }
        StartCoroutine(FlipTo(want));
    }

    IEnumerator FlipTo(string want)
    {
        _flipping = true;
        float d = Mathf.Max(0.01f, flipHalfDuration);
        for (float t = 0f; t < d; t += Time.unscaledDeltaTime)
        {
            SetFlipAngle(Mathf.Lerp(0f, 90f, t / d));
            yield return null;
        }
        SetFlipAngle(90f);
        label.text = want;
        _shown = want;
        for (float t = 0f; t < d; t += Time.unscaledDeltaTime)
        {
            SetFlipAngle(Mathf.Lerp(-90f, 0f, t / d));
            yield return null;
        }
        SetFlipAngle(0f);
        _flipping = false;

        if (!string.IsNullOrEmpty(_pending) && _pending != _shown)
        {
            string next = _pending;
            _pending = null;
            ApplyText(next);
        }
    }

    void SetFlipAngle(float degrees)
    {
        if (label == null) return;
        label.rectTransform.localRotation = Quaternion.Euler(0f, degrees, 0f);
    }

    // ── 配色 ────────────────────────────────────────────────────────────────
    void ApplyColor()
    {
        if (label == null) return;
        TurnManager tm = TurnManager.Instance;
        bool myTurn = tm != null && tm.IsMyTurn();
        bool clickable = myTurn && (_button == null || _button.interactable);
        Color c = myTurn ? (_hover && clickable ? hoverColor : activeColor) : idleColor;
        if (label.color != c) label.color = c;
    }

    public void OnPointerEnter(PointerEventData eventData) { _hover = true; ApplyColor(); }
    public void OnPointerExit(PointerEventData eventData)  { _hover = false; ApplyColor(); }

    // ── 点击 ────────────────────────────────────────────────────────────────
    public void OnPointerDown(PointerEventData eventData)
    {
        if (_button != null && !_button.interactable) return;
        if (_press != null) StopCoroutine(_press);
        _press = StartCoroutine(PressRoutine());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_press != null) return;   // 已经在收尾
    }

    IEnumerator PressRoutine()
    {
        RectTransform rt = (RectTransform)transform;
        float d = Mathf.Max(0.01f, pressDuration);
        for (float t = 0f; t < d; t += Time.unscaledDeltaTime)
        {
            rt.anchoredPosition = _basePos + new Vector2(0f, -pressDepth * Mathf.Clamp01(t / d));
            yield return null;
        }
        rt.anchoredPosition = _basePos + new Vector2(0f, -pressDepth);
        float back = d * 1.8f;
        for (float t = 0f; t < back; t += Time.unscaledDeltaTime)
        {
            rt.anchoredPosition = _basePos + new Vector2(0f, -pressDepth * (1f - Mathf.Clamp01(t / back)));
            yield return null;
        }
        rt.anchoredPosition = _basePos;
        _press = null;
    }

    // ── 回合结束 ────────────────────────────────────────────────────────────
    void EndTurn()
    {
        Debug.Log($"[EndTurnButton] Click! Server={NetworkServer.active}, Client={NetworkClient.isConnected}");

        TurnManager turnManager = TurnManager.Instance != null ? TurnManager.Instance : FindObjectOfType<TurnManager>();
        if (turnManager == null) { Debug.LogError("[EndTurnButton] TurnManager not found!"); return; }

        // 客户端：把结束回合指令发给服务器
        if (NetworkClient.isConnected && !NetworkServer.active)
        {
            NetworkPlayer.Local?.CmdEndTurn();
            return;
        }

        // 服务器 / 主机：直接执行
        Debug.Log($"[EndTurnButton] Direct EndCurrentTurn, phase={turnManager.currentPhase}");
        turnManager.EndCurrentTurn();
    }

    public void SetInteractable(bool enabled)
    {
        if (_button == null) _button = GetComponent<Button>();
        if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        if (_button != null) _button.interactable = enabled;
        if (_canvasGroup != null)
        {
            _canvasGroup.interactable = enabled;
            _canvasGroup.blocksRaycasts = enabled;
        }
        ApplyColor();
        Debug.Log($"[EndTurnButton] SetInteractable({enabled})");
    }
}
