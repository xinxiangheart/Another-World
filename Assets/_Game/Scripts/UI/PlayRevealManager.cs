using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 打出展示动画管理器：屏幕右侧 1/3 中心，FIFO 依次"闪烁→展示→淡出"。
/// 懒加载。实例化 2D 展示卡时，父级 = 主 Canvas（与 2D 面板/手牌同层），
/// 不挂到任何卡牌逻辑/手牌容器里；展示完销毁，不影响原逻辑。
/// back=true 时：召唤物 → CardDisplay2DNew.ShowBack()（基类 ShowBack 在召唤 prefab 不切正反面，不能用）；
///               法术 → CardDisplay2DSpell.ShowBack()。
/// </summary>
public class PlayRevealManager : MonoBehaviour
{
    public static PlayRevealManager Instance { get; private set; }

    [Header("节奏")]
    public float growIn = 0.14f;   // 闪烁进入（亮起 + 轻微过冲）
    public float holdShow = 0.9f;  // 展示停留
    public float fadeOut = 0.35f;  // 淡出
    public float popScale = 1.06f; // 进入过冲倍率

    // 右侧 1/3 的水平中心 = 5/6；垂直居中
    const float RIGHT_THIRD_CENTER_X = 5f / 6f;
    const float CENTER_Y = 0.5f;

    readonly Queue<(CardData td, bool back)> _queue = new Queue<(CardData, bool)>();
    bool _playing;
    Transform _canvas; // 主 Canvas：展示卡直接挂它下面
    int _seq;

    /// <summary>统一隐藏源：该 3D 模型此刻是否对本端显示为卡背（读 Card3DHover.isHidden，与场上同源）。无模型→false。</summary>
    public static bool IsHiddenBack(GameObject model)
    {
        if (model == null) return false;
        var h = model.GetComponent<Card3DHover>();
        return h != null && h.isHidden;
    }

    public static void Show(CardData td, bool back)
    {
        if (td == null) return;
        EnsureInstance();
        if (Instance == null) return;
        Instance._queue.Enqueue((td, back));
        Instance.ProcessIfIdle();
    }

    static void EnsureInstance()
    {
        if (Instance != null) return;
        var canvasGo = FindMainCanvas();
        if (canvasGo == null) return;

        // 管理器自身是 Canvas 下的一个小节点（仅承载协程/队列），展示卡不挂它下面
        var go = new GameObject("PlayRevealManager", typeof(PlayRevealManager));
        go.transform.SetParent(canvasGo.transform, false);

        var mgr = go.GetComponent<PlayRevealManager>();
        mgr._canvas = canvasGo.transform;
        Instance = mgr;
        mgr.transform.SetAsLastSibling();
    }

    static GameObject FindMainCanvas()
    {
        // 优先与手牌/2D 面板同一 Canvas（Player.handArea 所在），保证展示卡与其同层
        if (Player.Instance != null && Player.Instance.handArea != null)
        {
            var c = Player.Instance.handArea.GetComponentInParent<Canvas>();
            if (c != null) return c.gameObject;
        }
        var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
        foreach (var c in canvases)
            if (c != null && c.renderMode == RenderMode.ScreenSpaceOverlay) return c.gameObject;
        if (canvases.Length > 0) return canvases[0].gameObject;
        return null;
    }

    void ProcessIfIdle()
    {
        if (_playing || _queue.Count == 0) return;
        StartCoroutine(PlayLoop());
    }

    IEnumerator PlayLoop()
    {
        _playing = true;
        while (_queue.Count > 0)
        {
            var (td, back) = _queue.Dequeue();
            yield return StartCoroutine(PlayOne(td, back));
        }
        _playing = false;
    }

    IEnumerator PlayOne(CardData td, bool back)
    {
        var go = BuildCard(td, back);
        if (go == null) yield break;

        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        Vector3 baseScale = go.transform.localScale;

        // ── 卡面终态门 ──────────────────────────────────────────────────
        // 展示卡 Instantiate 后 BuildCard 立刻切面，但 CardDisplay2DSpell.Start() 在本帧稍后
        // 无条件 ShowFront()（CardDisplay2DNew.Start() 则 Refresh()），会覆盖刚设的卡背 →
        // 法术类（反制牌）展示恒显示正面。等本帧所有 Start() 跑完再重设终态；此刻 alpha=0
        // 不可见，不会闪正面。原则：展示动画的卡背必须在"对展示玩家隐藏"的最终态确定之后读。
        yield return null;
        if (back && go != null) ApplyBackFace(go, td);

        // ① 闪烁进：亮起 + 轻微过冲缩放
        float t = 0f;
        while (t < growIn)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / growIn);
            float e = 1f - Mathf.Pow(1f - p, 3f);
            cg.alpha = Mathf.Lerp(0f, 1f, e);
            go.transform.localScale = baseScale * Mathf.Lerp(0.72f, popScale, e);
            yield return null;
        }
        cg.alpha = 1f;
        go.transform.localScale = baseScale;

        // ② 展示停留
        yield return new WaitForSeconds(holdShow);

        // ③ 淡出
        t = 0f;
        while (t < fadeOut)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Lerp(1f, 0f, t / fadeOut);
            yield return null;
        }
        cg.alpha = 0f;
        Destroy(go); // 播放完销毁
    }

    GameObject BuildCard(CardData td, bool back)
    {
        var player = Player.Instance;
        GameObject prefab = player != null
            ? (td.cardType == CardType.Spell ? player.spellCardPrefab2D : player.cardPrefab2D)
            : null;
        if (prefab == null)
        {
            Debug.LogWarning("[PlayReveal] 无 2D 卡预制体(Player.cardPrefab2D/spellCardPrefab2D)，跳过展示");
            return null;
        }

        // 父级 = 主 Canvas（与 2D 面板/手牌同层），不挂在手牌/卡牌逻辑容器下
        var go = Instantiate(prefab, _canvas != null ? _canvas : transform.root);

        // 不参与交互
        var cv = go.GetComponent<CardView>();
        if (cv != null) { cv.enabled = false; cv.handManager = null; }
        var drag = go.GetComponent<CardDrag>();
        if (drag != null) drag.enabled = false;

        // 定位：右侧 1/3 中心（相对主 Canvas 全幅的 5/6 处，垂直居中）
        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(RIGHT_THIRD_CENTER_X, CENTER_Y);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }

        Player.Scale2DCard(go); // 与手牌/卡组展示一致的运行时缩放

        var di = go.GetComponent<CardInstance>();
        if (di == null) di = go.AddComponent<CardInstance>();
        di.InitFromTemplate(td, 0, null);
        di.instanceID = "_reveal_" + (++_seq); // 唯一临时 id

        var base2d = go.GetComponent<CardDisplay2D>(); // 召唤=Compat(转发New)；法术=CardDisplay2DSpell
        if (back)
        {
            ApplyBackFace(go, td); // 立即切背（防 prefab 默认正面闪一帧）；Start() 后再补一次见 PlayOne
        }
        else
        {
            base2d?.RefreshWithInstance(di); // 新实例默认正面
        }
        return go;
    }

    /// <summary>把展示卡切到卡背（按预制体实际挂的显示组件分流）。
    /// 幂等：BuildCard 立即调一次防闪，PlayOne 在 Start() 之后再调一次定终态。</summary>
    static void ApplyBackFace(GameObject go, CardData td)
    {
        if (go == null) return;
        var dNew = go.GetComponent<CardDisplay2DNew>(); // 召唤物必须走 New.ShowBack 才真正翻背
        if (dNew != null) { dNew.ShowBack(); return; }
        var dSpell = go.GetComponent<CardDisplay2DSpell>();
        if (dSpell != null) { dSpell.ShowBack(td, "反制牌"); return; }
        go.GetComponent<CardDisplay2D>()?.ShowBack(td, "反制牌"); // 基类兜底（不切正反面，视觉有限）
    }
}
