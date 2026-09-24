using TMPro;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Mirror;

public class DrawCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public TextMeshProUGUI drawCountText;
    public Image buttonImage;

    public float hoverScale = 1.2f;
    public float smoothSpeed = 10f;

    private int remainingDraws = 5;
    private Vector3 originalScale;
    private Vector3 targetScale;
    private CanvasGroup _canvasGroup;

    [Header("抽牌数字：配色与动效")]
    public Color goldDimColor = new Color(0.784f, 0.588f, 0.176f, 1f);   // 有择牌机会：闪金的暗位
    public Color goldLitColor = new Color(1f, 0.925f, 0.588f, 1f);       // 有择牌机会：闪金的亮位
    public Color noChanceColor = new Color(0.451f, 0.451f, 0.478f, 1f);  // 机会归零：灰（优先于金）
    public float goldShineCycle = 0.9f;   // 金色「亮 ↔ 暗」一次呼吸的周期（秒）
    public float popUpTime = 0.09f;       // 数字跳起用时
    public float popDownTime = 0.16f;     // 数字落回用时
    public float popScale = 1.5f;         // 跳到最高点的倍数

    Color _idleColor = Color.white;        // 常规颜色（取预制体上的原色）
    Vector3 _drawCountBaseScale = Vector3.one;
    Vector2 _drawCountBasePos = Vector2.zero;
    Coroutine _popRoutine;

    void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        // If there's a Button on us (added by accident or from prefab), keep it
        // but make sure it doesn't intercept our IPointerClickHandler.
        Button btn = GetComponent<Button>();
        if (btn != null) btn.interactable = true;
    }

    void Start()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;
        if (drawCountText != null)
        {
            _drawCountBaseScale = drawCountText.transform.localScale;
            _drawCountBasePos = drawCountText.rectTransform.anchoredPosition;
            _idleColor = drawCountText.color;
        }
        UpdateDisplay();
    }

    void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * smoothSpeed);
        UpdateDrawCountColor();
    }

    public void ResetForNewPhase()
    {
        remainingDraws = 5;
        UpdateDisplay();
    }

    public int GetRemainingDraws()
    {
        return remainingDraws;
    }

    public void UpdateDisplay()
    {
        if (drawCountText != null)
            drawCountText.text = remainingDraws.ToString();
    }

    /// <summary>抽牌数字染色：机会归零 → 灰（优先级最高）；仍有择牌机会 → 闪金；其余 → 原色。</summary>
    void UpdateDrawCountColor()
    {
        if (drawCountText == null) return;

        if (remainingDraws <= 0)
        {
            drawCountText.color = noChanceColor;
            return;
        }

        NetworkPlayer me = NetworkPlayer.Local;
        if (me != null && me.pickDrawReady)
        {
            // 「闪亮亮」：在暗金与亮金之间来回呼吸，smoothstep 让亮暗过渡不生硬
            float cycle = Mathf.Max(0.05f, goldShineCycle);
            float p = Mathf.PingPong(Time.time / cycle, 1f);
            p = p * p * (3f - 2f * p);
            drawCountText.color = Color.Lerp(goldDimColor, goldLitColor, p);
            return;
        }

        drawCountText.color = _idleColor;
    }

    /// <summary>点击抽牌：数字先「跳一下」，跳到最高点时才换成新数字，然后落回。</summary>
    void PopDrawCount()
    {
        if (drawCountText == null) { UpdateDisplay(); return; }
        if (_popRoutine != null) StopCoroutine(_popRoutine);
        _popRoutine = StartCoroutine(PopDrawCountRoutine(remainingDraws));
    }

    IEnumerator PopDrawCountRoutine(int newValue)
    {
        RectTransform rt = drawCountText.rectTransform;
        float up = Mathf.Max(0.001f, popUpTime);
        float down = Mathf.Max(0.001f, popDownTime);

        ApplyPopScale(rt, 1f);
        float s = 0f;
        while (s < up)
        {
            s += Time.deltaTime;
            ApplyPopScale(rt, Mathf.Lerp(1f, popScale, Mathf.Clamp01(s / up)));
            yield return null;
        }

        drawCountText.text = newValue.ToString();     // 跳到最高点才换数字
        ApplyPopScale(rt, popScale);

        s = 0f;
        while (s < down)
        {
            s += Time.deltaTime;
            ApplyPopScale(rt, Mathf.Lerp(popScale, 1f, Mathf.Clamp01(s / down)));
            yield return null;
        }

        ApplyPopScale(rt, 1f);
        drawCountText.text = remainingDraws.ToString();  // 期间若又抽了一次，收尾对齐最新值
        _popRoutine = null;
    }

    /// <summary>按倍率缩放数字。TMP 默认是「左对齐 + 上对齐」，字形贴在文字矩形的左上角，
    /// 只缩矩形会让数字往左上角飞；这里按字形中心（textBounds.center）反向平移补偿，
    /// 做到「正前方」原地放大缩小。</summary>
    void ApplyPopScale(RectTransform rt, float k)
    {
        rt.localScale = _drawCountBaseScale * k;
        Vector2 c = DrawCountInkCenter();              // 字形中心（相对 pivot）
        rt.anchoredPosition = _drawCountBasePos + c * (1f - k);
    }

    /// <summary>数字「字形」的中心（相对 pivot 的本地坐标）。
    /// 用字符四角的包围盒取，比 textBounds 更贴墨迹中心（后者含 ascender / descender 的空档）。
    /// 场景里这颗文字是左对齐 + 上对齐，墨迹贴在 150x150 矩形的左上角，
    /// 不补偿的话放大就会往左上飘。</summary>
    Vector2 DrawCountInkCenter()
    {
        TMP_TextInfo ti = drawCountText.textInfo;
        if (ti != null && ti.characterCount > 0)
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            bool any = false;
            for (int i = 0; i < ti.characterCount; i++)
            {
                if (!ti.characterInfo[i].isVisible) continue;
                any = true;
                min = Vector2.Min(min, ti.characterInfo[i].vertex_BL.position);
                max = Vector2.Max(max, ti.characterInfo[i].vertex_TR.position);
            }
            if (any) return (min + max) * 0.5f;
        }
        return drawCountText.textBounds.center;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Only allow interaction during your turn
        TurnManager tm = FindObjectOfType<TurnManager>();
        if (tm != null && !tm.IsMyTurn()) return;

        if (remainingDraws > 0)
            targetScale = originalScale * hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = originalScale;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Guard: only during your turn (both online and offline)
        TurnManager tm = FindObjectOfType<TurnManager>();
        if (tm != null && !tm.IsMyTurn())
        {
            Debug.Log($"[DrawCardUI] Click blocked: not MyTurn (phase={tm?.currentPhase})");
            return;
        }

        if (remainingDraws <= 0)
        {
            Debug.Log("[DrawCardUI] Click blocked: no remaining draws");
            return;
        }

        NetworkPlayer player = NetworkPlayer.Local;
        if (player == null)
        {
            Debug.LogError("[DrawCardUI] Click blocked: NetworkPlayer.Local is null");
            return;
        }

        if (NetworkClient.isConnected && !NetworkServer.active)
        {
            // Pure client: pre-decrement to prevent infinite clicking,
            // server will restore via TargetCancelDraw on failure.
            if (!player.UseEnergy(1))
            {
                Debug.LogWarning($"[DrawCardUI] UseEnergy(1) failed! energy={player.currentEnergy}");
                return;
            }
            remainingDraws--;
            PopDrawCount();
            Debug.Log($"[DrawCardUI] Sending CmdRequestDraw, remaining={remainingDraws}");
            player.CmdRequestDraw();
            return;
        }

        // Host/Server or offline: execute draw directly
        Debug.Log($"[DrawCardUI] Direct draw, energy={player.currentEnergy}");
        if (player.UseEnergy(1))
        {
            // 服务端权威：本回合第一次主动抽牌 → 择牌；已用过 / 牌库空 / 牌库不足则照常抽一张。
            // 返回 false = 本次点击作废（择牌面板已开），把能量退回去。
            if (!player.ServerHandleActiveDraw())
            {
                player.currentEnergy += 1;
                return;
            }
            remainingDraws--;
            PopDrawCount();
        }
        else
        {
            Debug.LogWarning($"[DrawCardUI] UseEnergy(1) failed! currentEnergy={player.currentEnergy}");
        }
    }

    public void UseOneDraw()
    {
        if (remainingDraws > 0)
            remainingDraws--;
    }

    /// <summary>服务端拒绝抽牌时恢复客户端状态</summary>
    public void RestoreDraw()
    {
        remainingDraws++;
        UpdateDisplay();
        NetworkPlayer player = NetworkPlayer.Local;
        if (player != null) player.currentEnergy += 1;
        Debug.Log($"[DrawCardUI] Draw restored (server rejected), remaining={remainingDraws}");
    }

    public void SetInteractable(bool enabled)
    {
        if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.interactable = enabled;
        _canvasGroup.blocksRaycasts = enabled;
        Debug.Log($"[DrawCardUI] SetInteractable({enabled})");
    }
}
