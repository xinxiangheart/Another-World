using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>召唤落地反馈：冲击环 + 中心闪光 + 落地尘土。
/// 纹理 / 材质 / 四边形全部运行时生成，不新增美术资源；环与闪光是加法混合（队列 3100），
/// 尘土是普通 alpha 混合（队列 3099），都盖在棋盘之上。生命周期结束自动 Destroy，不常驻。</summary>
public static class CardSummonFX
{
    static Mesh _quad;
    static Texture2D _ringTex;
    static Texture2D _flashTex;
    static Texture2D _slotFlashTex;
    static Texture2D _dustTex;
    static Sprite _slotFlashSprite;
    static Material _ringMat;
    static Material _flashMat;
    static Material _dustMat;
    static Runner _runner;

    // 静态类不能挂协程，借一个空 MonoBehaviour 跑生命周期
    class Runner : MonoBehaviour { }

    /// <summary>在落点播一次冲击。size 用于放大/缩小整组特效（默认 1）。</summary>
    public static void PlayImpact(Vector3 worldPos, float size = 1f)
    {
        EnsureRunner();
        float s = Mathf.Max(0.01f, size);
        _runner.StartCoroutine(ImpactRoutine(worldPos, s));
        _runner.StartCoroutine(DustRoutine(worldPos, s));
    }

    /// <summary>槽位落地闪白：贴在槽位上的独立 UI 覆盖层，只负责「亮一下」。
    /// 不读写 BoardSlot.SetSlotColor / SelectionDim，状态色链与压暗完全不受影响；
    /// 卡面在它前面（z 更靠近相机），所以只会从卡四周溢出一圈柔光。</summary>
    public static void PlaySlotFlash(BoardSlot slot, float duration = 0.22f, float size = 1.20f)
    {
        if (slot == null) return;
        EnsureRunner();
        _runner.StartCoroutine(SlotFlashRoutine(slot, Mathf.Max(0.01f, duration), Mathf.Max(1f, size)));
    }

    static void EnsureRunner()
    {
        if (_runner != null) return;
        var go = new GameObject("CardSummonFX");
        Object.DontDestroyOnLoad(go);
        _runner = go.AddComponent<Runner>();
    }

    static IEnumerator SlotFlashRoutine(BoardSlot slot, float duration, float size)
    {
        if (slot == null) yield break;
        EnsureAssets();
        if (_slotFlashSprite == null)
        {
            _slotFlashSprite = Sprite.Create(_slotFlashTex,
                new Rect(0f, 0f, _slotFlashTex.width, _slotFlashTex.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        var go = new GameObject("SummonSlotFlash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(slot.transform, false);

        RectTransform slotRt = slot.transform as RectTransform;
        Vector2 slotSize = slotRt != null && slotRt.rect.width > 0.01f
            ? slotRt.rect.size
            : new Vector2(1.25f, 2.22f) * HandManager.BoardLayoutScale;

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        // 把多出的宽度平均分到四条边，卡面挡住中间后只剩一圈等宽窄边光。
        float border = slotSize.x * (size - 1f) * 0.5f;
        rt.sizeDelta = slotSize + new Vector2(border * 2f, border * 2f);
        // 略向相机方向挪一点：盖住槽位底板，但仍在 3D 卡牌之后（卡再靠前 0.1）
        rt.localPosition = new Vector3(0f, 0f, -0.02f);
        rt.localScale = Vector3.one * 0.99f;

        Image image = go.GetComponent<Image>();
        image.sprite = _slotFlashSprite;
        image.raycastTarget = false;
        image.color = new Color(1f, 0.97f, 0.90f, 0f);

        float t = 0f;
        const float peak = 0.78f;
        while (t < duration && go != null)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            // 只做极小幅度的外扩，避免光边离开卡牌。
            float expand = 1f - Mathf.Pow(1f - p, 3f);
            rt.localScale = Vector3.one * Mathf.Lerp(0.99f, 1.01f, expand);
            // 起亮快、退得干净：前 18% 冲到峰值，之后平方衰减
            float rise = Mathf.Clamp01(p / 0.18f);
            float a = p < 0.18f ? peak * rise : peak * Mathf.Pow(1f - (p - 0.18f) / 0.82f, 2f);
            image.color = new Color(1f, 0.97f, 0.90f, a);
            yield return null;
        }

        if (go != null) Object.Destroy(go);
    }

    static IEnumerator ImpactRoutine(Vector3 pos, float size)
    {
        EnsureAssets();
        if (_quad == null || _ringMat == null || _flashMat == null) yield break;

        Vector3 toCamera = Camera.main != null ? -Camera.main.transform.forward : Vector3.back;
        Vector3 basePos = pos + toCamera * 0.2f;

        GameObject ring = SpawnQuad("SummonRing", _ringMat, basePos);
        GameObject flash = SpawnQuad("SummonFlash", _flashMat, basePos + toCamera * 0.01f);

        var ringBlock = new MaterialPropertyBlock();
        var flashBlock = new MaterialPropertyBlock();

        const float ringDur = 0.28f;
        const float flashDur = 0.18f;
        float t = 0f;
        while (t < ringDur)
        {
            t += Time.deltaTime;

            if (ring != null)
            {
                float p = Mathf.Clamp01(t / ringDur);
                float e = 1f - Mathf.Pow(1f - p, 3f);
                ring.transform.localScale = Vector3.one * (Mathf.Lerp(0.30f, 1.30f, e) * size);
                ringBlock.SetColor("_Color", new Color(1f, 0.90f, 0.68f, 0.55f * (1f - p)));
                ring.GetComponent<MeshRenderer>().SetPropertyBlock(ringBlock);
            }

            if (flash != null)
            {
                float fp = Mathf.Clamp01(t / flashDur);
                flash.transform.localScale = Vector3.one * (Mathf.Lerp(0.9f, 1.35f, fp) * size);
                flashBlock.SetColor("_Color", new Color(1f, 0.95f, 0.85f, 0.45f * (1f - fp)));
                flash.GetComponent<MeshRenderer>().SetPropertyBlock(flashBlock);
                if (fp >= 1f)
                {
                    Object.Destroy(flash);
                    flash = null;
                }
            }

            yield return null;
        }

        if (ring != null) Object.Destroy(ring);
    }

    /// <summary>落地尘土：8 片柔边小方片从槽位底部向外上方散开，0.45s 内淡出。
    /// 与冲击环同帧启动；位置略退到相机方向后面，避免和环 / 闪光抢同一层。</summary>
    static IEnumerator DustRoutine(Vector3 pos, float size)
    {
        EnsureAssets();
        if (_quad == null || _dustMat == null) yield break;

        Vector3 toCamera = Camera.main != null ? -Camera.main.transform.forward : Vector3.back;
        Vector3 origin = pos + toCamera * 0.18f + Vector3.up * 0.06f;

        const int count = 8;
        const float duration = 0.45f;
        var quads = new GameObject[count];
        var renderers = new MeshRenderer[count];
        var blocks = new MaterialPropertyBlock[count];
        var velocities = new Vector3[count];
        var startScales = new float[count];
        var endScales = new float[count];

        for (int i = 0; i < count; i++)
        {
            float angle = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.22f, 0.22f);
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            // 向外散开 + 向上偏；重力在循环里施加，粒子先扬后落
            Vector3 velocity = dir * Random.Range(0.30f, 0.65f)
                             + Vector3.up * Random.Range(0.35f, 0.70f);

            GameObject quad = SpawnQuad("SummonDust", _dustMat, origin + dir * Random.Range(0f, 0.10f));
            quad.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            startScales[i] = Random.Range(0.08f, 0.13f) * size;
            endScales[i] = Random.Range(0.26f, 0.36f) * size;
            quad.transform.localScale = Vector3.one * startScales[i];
            quads[i] = quad;
            renderers[i] = quad.GetComponent<MeshRenderer>();
            blocks[i] = new MaterialPropertyBlock();
            velocities[i] = velocity;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            float grow = 1f - Mathf.Pow(1f - p, 2f);
            float alpha = 0.48f * Mathf.Pow(1f - p, 1.5f);

            for (int i = 0; i < count; i++)
            {
                GameObject quad = quads[i];
                if (quad == null) continue;

                velocities[i] += Vector3.down * (2f * Time.deltaTime);
                quad.transform.position += velocities[i] * Time.deltaTime;

                float scale = Mathf.Lerp(startScales[i], endScales[i], grow);
                quad.transform.localScale = new Vector3(scale, scale * 0.72f, 1f);

                blocks[i].SetColor("_Color", new Color(0.84f, 0.77f, 0.63f, alpha));
                if (renderers[i] != null) renderers[i].SetPropertyBlock(blocks[i]);
            }
            yield return null;
        }

        for (int i = 0; i < count; i++)
            if (quads[i] != null) Object.Destroy(quads[i]);
    }

    static GameObject SpawnQuad(string name, Material material, Vector3 position)
    {
        var go = new GameObject(name);
        go.transform.position = position;

        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = _quad;

        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        return go;
    }

    static void EnsureAssets()
    {
        if (_quad == null) _quad = BuildQuad();
        if (_ringTex == null) _ringTex = BuildRingTexture();
        if (_flashTex == null) _flashTex = BuildFlashTexture();
        if (_slotFlashTex == null) _slotFlashTex = BuildSlotFlashTexture();
        if (_dustTex == null) _dustTex = BuildDustTexture();
        if (_ringMat == null) _ringMat = BuildMaterial(_ringTex);
        if (_flashMat == null) _flashMat = BuildMaterial(_flashTex);
        if (_dustMat == null) _dustMat = BuildMaterial(_dustTex, false, 3099);
    }

    static Mesh BuildQuad()
    {
        // 正反两组三角形：不依赖朝向，任何视角都可见
        var mesh = new Mesh();
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),   new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),   new Vector3(-0.5f, 0.5f, 0f),
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f),
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 };
        mesh.RecalculateNormals();
        return mesh;
    }

    static Texture2D BuildRingTexture()
    {
        const int n = 256;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n - 0.5f;
                float dy = (y + 0.5f) / n - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);

                // 内外都留出渐变：外沿在 0.47 处已经归零，四边形边缘不会把环切出硬边
                float inner = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 0.38f, r));
                float outer = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.38f, 0.47f, r));
                float a = Mathf.Clamp01(inner * outer);

                px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    static Texture2D BuildFlashTexture()
    {
        const int n = 256;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n - 0.5f;
                float dy = (y + 0.5f) / n - 0.5f;
                // 0.47 而不是 0.5：边缘留 6% 透明余量，quad 边界不会切出一圈硬边
                float r = Mathf.Sqrt(dx * dx + dy * dy) / 0.47f;
                float a = Mathf.Pow(Mathf.Clamp01(1f - r), 2f);

                px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    /// <summary>贴边闪光用的长方形软边环：卡面挡住中间，只露出一圈窄窄的光。
    /// 外缘连续渐隐，内缘也保持透明，避免覆盖卡面。</summary>
    static Texture2D BuildSlotFlashTexture()
    {
        // 与「槽位 + 四周等宽窄边」同比例，贴图不会被拉伸。
        const int w = 256;
        const int h = 423;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // 到最近边的像素距离。卡面边缘约在 20 px 处，
                // 因此 0→12 px 渐隐、12→20 px 实体、20 px 之后向内透明。
                float edgeDistance = Mathf.Min(
                    Mathf.Min(x + 0.5f, w - x - 0.5f),
                    Mathf.Min(y + 0.5f, h - y - 0.5f));
                float outer = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 12f, edgeDistance));
                float inner = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(20f, 36f, edgeDistance));
                float a = Mathf.Clamp01(inner * outer);

                px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    /// <summary>尘土用的柔边圆片：中心略实、边缘完全透明，叠 8 片后读作一团散开的浮尘。</summary>
    static Texture2D BuildDustTexture()
    {
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n - 0.5f;
                float dy = (y + 0.5f) / n - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / 0.47f;
                float a = Mathf.Pow(Mathf.Clamp01(1f - r), 1.6f);

                px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    static Material BuildMaterial(Texture2D tex, bool additive = true, int renderQueue = 3100)
    {
        Shader shader = Shader.Find("AnotherWorld/BoardLayer");
        if (shader == null) return null;

        var mat = new Material(shader);
        mat.SetTexture("_MainTex", tex);
        mat.SetColor("_Color", Color.white);
        mat.SetFloat("_SrcBlend", 5f);                                  // SrcAlpha
        mat.SetFloat("_DstBlend", additive ? 1f : 10f);                 // One / OneMinusSrcAlpha
        mat.renderQueue = renderQueue;
        return mat;
    }
}
