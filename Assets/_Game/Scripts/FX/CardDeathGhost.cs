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

    Material _mat;
    Vector3 _basePos;
    Vector3 _baseScale;

    static readonly Color Gray = new Color(0.55f, 0.55f, 0.55f, 1f);
    static readonly Color Black = new Color(0f, 0f, 0f, 1f);
    static readonly Color Gold = new Color(1f, 0.82f, 0.25f, 1f);

    public static void Play(GameObject model) => Launch(model, false);
    public static void PlayActive(GameObject model) => Launch(model, true);

    /// <summary>从模型生成残影并播放。model 随后会被正常销毁/同步。纯表现。</summary>
    static void Launch(GameObject model, bool active)
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

        var ghost = new GameObject(model.name + (active ? "_activeDeathGhost" : "_deathGhost"));
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
        comp.StartCoroutine(comp.Run(active));
    }

    IEnumerator Run(bool active)
    {
        if (active) yield return RunActive();
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

    void OnDestroy()
    {
        if (_mat != null) Destroy(_mat); // 实例材质随残影清理
    }
}
