using System.Collections;
using UnityEngine;

/// <summary>
/// 普通退场"残影"：在真卡销毁前，把其 SpriteRenderer 视觉克隆成独立静态残影(禁交互/免同步)，
/// 用 CardDeathSprite shader 播"自下而上 灰→黑 + 淡出重叠"，播完销毁。真模型照常走原销毁/同步。
/// 只由普通退场(非反击/非主动退场)调用。
/// </summary>
public class CardDeathGhost : MonoBehaviour
{
    public float duration = 0.55f;   // 总时长
    public float fadeDelay = 0.16f;  // 淡出波落后压暗波（秒）→ 未完全黑就开始消失

    Material _mat;
    float _minY;
    float _maxY;

    static readonly Color Gray = new Color(0.55f, 0.55f, 0.55f, 1f);
    static readonly Color Black = new Color(0f, 0f, 0f, 1f);

    /// <summary>从模型生成残影并播放。model 随后会被正常销毁/同步。纯表现。</summary>
    public static void Play(GameObject model)
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

        var ghost = new GameObject(model.name + "_deathGhost");
        ghost.transform.position = model.transform.position;
        ghost.transform.rotation = model.transform.rotation;
        ghost.transform.localScale = model.transform.lossyScale;

        var mat = new Material(shader);
        mat.SetFloat("_MinY", b.min.y);
        mat.SetFloat("_MaxY", b.max.y);

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
        comp.StartCoroutine(comp.Run());
    }

    IEnumerator Run()
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);

            // ease-out 推进：底部(早)快、越往上越慢，但总时长不变 → 灰黑波与淡出波"加速起步、渐缓收尾"
            float e = 1f - Mathf.Pow(1f - p, 3f);
            float dark = e;
            float fade = Mathf.Max(0f, e - (duration > 0f ? fadeDelay / duration : 0f));

            _mat.SetFloat("_Death", dark);
            _mat.SetFloat("_Fade", fade);
            _mat.SetColor("_TintColor", Color.Lerp(Gray, Black, e)); // 压暗目标色同步缓出：先灰后黑
            yield return null;
        }

        _mat.SetFloat("_Death", 1f);
        _mat.SetFloat("_Fade", 1f);
        _mat.SetColor("_TintColor", Black);
        Destroy(ghostHolder());
    }

    GameObject ghostHolder() => gameObject;

    void OnDestroy()
    {
        if (_mat != null) Destroy(_mat); // 实例材质随残影清理
    }
}
