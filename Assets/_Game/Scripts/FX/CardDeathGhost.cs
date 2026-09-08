using System.Collections;
using UnityEngine;

/// <summary>
/// 退场"残影"：在真卡销毁前，把其 SpriteRenderer 视觉克隆成独立静态残影(禁交互/免同步)，
/// 用 CardDeathSprite shader 播放，播完销毁。真模型照常走原销毁/同步。
/// 两种模式：
///   Gray(普通退场)：自下而上 灰→黑 + 淡出重叠，ease-out 推进(底部快、顶部慢)，总 ~duration。
///   Active(主动退场)：先 略微放大+升高，再 闪一次金色 + 匀速从下到上消失，总时长更长。
/// </summary>
public class CardDeathGhost : MonoBehaviour
{
    // ── 普通退场(灰黑) ──
    public float duration = 0.55f;    // 普通总时长
    public float fadeDelay = 0.16f;   // 灰黑路径淡出波落后压暗波(秒)

    // ── 主动退场(金闪) ──
    [Header("主动退场")]
    public float activeRise = 0.16f;     // 阶段① 放大+升高时长
    public float activeFade = 0.34f;     // 阶段② 金色闪烁+匀速淡出时长（已缩短）
    public float raiseAmount = 0.12f;    // 升高量(世界单位)
    public float scaleBoost = 1.06f;     // 放大倍率
    public float activeFadeLag = 0.08f;  // 金闪路径淡出波落后压暗波(归一化 匀速)

    // ── 抛置(硬币式翻转) ──
    [Header("抛置")]
    public float discardFlip = 0.20f;    // 绕 Y 轴翻转一圈时长(已加快)
    public float discardFade = 0.18f;    // 翻转完迅速淡出时长
    public float discardSpin = 360f;     // 翻转角度
    public float discardFlipSign = -1f;  // 翻转方向(+1/-1；默认反向)
    public float discardHover = 0.16f;   // 翻转时向上抬起的弧高(世界单位,已加大)，避免薄卡/背面穿模

    Material _mat;
    Vector3 _basePos;
    Vector3 _baseScale;

    static readonly Color Gray = new Color(0.55f, 0.55f, 0.55f, 1f);
    static readonly Color Black = new Color(0f, 0f, 0f, 1f);
    static readonly Color Gold = new Color(1f, 0.82f, 0.25f, 1f);
    static readonly Color WhiteFade = new Color(1f, 1f, 1f, 1f);

    // kind: 0=普通灰黑, 1=主动金闪, 2=抛置翻转
    public static void Play(GameObject model) => Launch(model, 0);
    public static void PlayActive(GameObject model) => Launch(model, 1);
    public static void PlayDiscard(GameObject model) => Launch(model, 2);

    /// <summary>从模型生成残影并播放。model 随后会被正常销毁/同步。纯表现。</summary>
    static void Launch(GameObject model, int kind)
    {
        if (model == null) return;
        var shader = Shader.Find("Custom/CardDeathSprite");
        if (shader == null)
        {
            Debug.LogWarning("[CardDeathGhost] 找不到 Custom/CardDeathSprite shader，跳过退场残影");
            return;
        }

        var sprites = model.GetComponentsInChildren<SpriteRenderer>(true);
        if (sprites.Length == 0) return; // 无 Sprite 视觉（如纯网格盒）→ 不做残影

        // 求整卡世界包围盒（用于 shader 的 Y 归一化）
        var rends = model.GetComponentsInChildren<Renderer>(true);
        Bounds b = rends.Length > 0 ? rends[0].bounds : new Bounds(model.transform.position, Vector3.one);
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

        string kindName = kind == 2 ? "_discardGhost" : (kind == 1 ? "_activeDeathGhost" : "_deathGhost");
        var ghost = new GameObject(model.name + kindName);
        ghost.transform.position = model.transform.position;
        ghost.transform.rotation = model.transform.rotation;
        ghost.transform.localScale = model.transform.lossyScale;

        var mat = new Material(shader);
        mat.SetFloat("_MinY", b.min.y);
        mat.SetFloat("_MaxY", b.max.y);
        mat.SetFloat("_Additive", 0f);

        foreach (var sr in sprites)
        {
            if (sr == null) continue;
            var go = new GameObject("s");
            go.transform.SetParent(ghost.transform, false);
            go.transform.localPosition = sr.transform.localPosition;
            go.transform.localRotation = sr.transform.localRotation;
            go.transform.localScale = sr.transform.localScale;
            var nsr = go.AddComponent<SpriteRenderer>();
            nsr.sprite = sr.sprite;
            nsr.sortingOrder = sr.sortingOrder;
            nsr.sortingLayerName = sr.sortingLayerName;
            nsr.color = Color.white;
            nsr.sharedMaterial = mat; // 同一残影共享一份材质，统一驱动
        }

        var comp = ghost.AddComponent<CardDeathGhost>();
        comp._mat = mat;
        comp._basePos = ghost.transform.position;
        comp._baseScale = ghost.transform.localScale;
        comp.StartCoroutine(comp.Run(kind));
    }

    IEnumerator Run(int kind)
    {
        if (kind == 1) yield return RunActive();
        else if (kind == 2) yield return RunDiscard();
        else yield return RunGray();
        Destroy(gameObject);
    }

    /// <summary>普通退场：灰黑 + 淡出重叠，ease-out 推进，总时长 duration。</summary>
    IEnumerator RunGray()
    {
        _mat.SetFloat("_Additive", 0f);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            float e = 1f - Mathf.Pow(1f - p, 3f); // ease-out：底部快、越往上越慢
            float dark = e;
            float fade = Mathf.Max(0f, e - (duration > 0f ? fadeDelay / duration : 0f));

            _mat.SetFloat("_Death", dark);
            _mat.SetFloat("_Fade", fade);
            _mat.SetColor("_TintColor", Color.Lerp(Gray, Black, e));
            yield return null;
        }
        _mat.SetFloat("_Death", 1f);
        _mat.SetFloat("_Fade", 1f);
        _mat.SetColor("_TintColor", Black);
    }

    /// <summary>主动退场：先 放大+升高；再 闪一次金色 + 匀速自下而上消失(非缓出)，总时长更长。</summary>
    IEnumerator RunActive()
    {
        _mat.SetColor("_TintColor", Gold);

        // 阶段①：略微放大并升高
        float t = 0f;
        while (t < activeRise)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / activeRise);
            float e = 1f - Mathf.Pow(1f - p, 3f);
            transform.localScale = _baseScale * Mathf.Lerp(1f, scaleBoost, e);
            transform.position = _basePos + Vector3.up * (raiseAmount * e);
            yield return null;
        }
        transform.localScale = _baseScale * scaleBoost;
        transform.position = _basePos + Vector3.up * raiseAmount;

        // 阶段②：金色"闪一次" + 匀速(linear) 自下而上消失
        float flashWindow = Mathf.Min(0.14f, activeFade * 0.3f); // 金色辉光只在开头闪一次，随后衰减(不再反复闪)
        float ft = 0f;
        while (ft < activeFade)
        {
            ft += Time.deltaTime;
            float p = Mathf.Clamp01(ft / activeFade); // 匀速推进，不用缓出
            float dark = p;
            float fade = Mathf.Max(0f, p - activeFadeLag);

            // 单次闪亮：p 未过 flashWindow 时辉光满，其后线性衰减到 0 —— 只亮一次
            float flash = 1f - Mathf.Clamp01(p / flashWindow);
            _mat.SetFloat("_Death", dark);
            _mat.SetFloat("_Fade", fade);
            _mat.SetFloat("_Additive", 0.45f * flash);
            yield return null;
        }
        _mat.SetFloat("_Death", 1f);
        _mat.SetFloat("_Fade", 1f);
        _mat.SetFloat("_Additive", 0f);
    }

    /// <summary>抛置：绕卡面内水平轴(局部 X=宽度方向)快速翻转一圈，翻完整卡迅速淡出。</summary>
    IEnumerator RunDiscard()
    {
        _mat.SetFloat("_Additive", 0f);
        _mat.SetFloat("_Death", 0f); // 波前归零：整卡不透明、无自下而上裁剪
        _mat.SetFloat("_Fade", 0f);
        _mat.SetColor("_TintColor", WhiteFade);

        // 阶段①：绕 Y 轴(残影局部竖直轴 transform.up)翻转一圈 360°。
        // 方向由 discardFlipSign 控制；翻转过程按 sin(π·progress) 弧线略微上抬，避免薄卡边穿模。
        Vector3 axis = transform.up;
        float ang = 0f;
        float dir = discardFlipSign >= 0f ? 1f : -1f;
        float perSec = discardSpin / Mathf.Max(0.0001f, discardFlip);
        while (ang < discardSpin - 0.01f)
        {
            float step = Mathf.Min(Time.deltaTime * perSec, discardSpin - ang);
            transform.Rotate(axis, dir * step, Space.World);
            ang += step;

            float prog = Mathf.Clamp01(ang / discardSpin);
            float raise = Mathf.Sin(prog * Mathf.PI) * discardHover; // 顶点在转角一半
            var pos = transform.position;
            pos.y = _basePos.y + raise;
            transform.position = pos;
            yield return null;
        }
        var endPos = transform.position;
        endPos.y = _basePos.y;
        transform.position = endPos;

        // 阶段②：翻完 → 整卡迅速淡出（_TintColor.alpha 全局淡出）
        float t = 0f;
        while (t < discardFade)
        {
            t += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(t / discardFade);
            _mat.SetColor("_TintColor", new Color(1f, 1f, 1f, a));
            yield return null;
        }
        _mat.SetColor("_TintColor", new Color(1f, 1f, 1f, 0f));
    }

    void OnDestroy()
    {
        if (_mat != null) Destroy(_mat); // 实例材质随残影清理
    }
}
