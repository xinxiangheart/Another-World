using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>召唤落地反馈：冲击环 + 中心闪光。
/// 纹理 / 材质 / 四边形全部运行时生成，不新增美术资源；走 BoardLayer 着色器的加法混合，
/// 队列 3100，盖在卡与棋盘之上。生命周期结束自动 Destroy，不常驻。</summary>
public static class CardSummonFX
{
    static Mesh _quad;
    static Texture2D _ringTex;
    static Texture2D _flashTex;
    static Texture2D _slotFlashTex;
    static Sprite _slotFlashSprite;
    static Material _ringMat;
    static Material _flashMat;
    static Runner _runner;

    // 静态类不能挂协程，借一个空 MonoBehaviour 跑生命周期
    class Runner : MonoBehaviour { }

    /// <summary>在落点播一次冲击。size 用于放大/缩小整组特效（默认 1）。</summary>
    public static void PlayImpact(Vector3 worldPos, float size = 1f)
    {
        EnsureRunner();
        _runner.StartCoroutine(ImpactRoutine(worldPos, Mathf.Max(0.01f, size)));
    }

    /// <summary>槽位落地闪白：贴在槽位上的独立 UI 覆盖层，只负责「亮一下」。
    /// 不读写 BoardSlot.SetSlotColor / SelectionDim，状态色链与压暗完全不受影响；
    /// 卡面在它前面（z 更靠近相机），所以只会从卡四周溢出一圈柔光。</summary>
    public static void PlaySlotFlash(BoardSlot slot, float duration = 0.22f, float size = 1.30f)
    {
        if (slot == null) return;
        EnsureRunner();
        _runner.StartCoroutine(SlotFlashRoutine(slot, Mathf.Max(0.01f, duration), Mathf.Max(0.1f, size)));
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
                new Vector2(0.5f, 0.5f), 100f);
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
        rt.sizeDelta = slotSize * size;
        // 略向相机方向挪一点：盖住槽位底板，但仍在 3D 卡牌之后（卡再靠前 0.1）
        rt.localPosition = new Vector3(0f, 0f, -0.02f);

        Image image = go.GetComponent<Image>();
        image.sprite = _slotFlashSprite;
        image.raycastTarget = false;
        image.color = new Color(1f, 0.97f, 0.90f, 0f);

        float t = 0f;
        const float peak = 0.82f;
        while (t < duration && go != null)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
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

        const float ringDur = 0.30f;
        const float flashDur = 0.18f;
        float t = 0f;
        while (t < ringDur)
        {
            t += Time.deltaTime;

            if (ring != null)
            {
                float p = Mathf.Clamp01(t / ringDur);
                float e = 1f - Mathf.Pow(1f - p, 3f);
                ring.transform.localScale = Vector3.one * (Mathf.Lerp(0.45f, 1.65f, e) * size);
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
        if (_ringMat == null) _ringMat = BuildMaterial(_ringTex);
        if (_flashMat == null) _flashMat = BuildMaterial(_flashTex);
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
                float r = Mathf.Sqrt(dx * dx + dy * dy);

                float inner = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.34f, 0.40f, r));
                float outer = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.46f, 0.50f, r));
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
                float r = Mathf.Sqrt(dx * dx + dy * dy) / 0.5f;
                float a = Mathf.Pow(Mathf.Clamp01(1f - r), 2f);

                px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    /// <summary>槽位闪白用的「边缘亮、中心透」方框光：卡面在中间不被洗白，
    /// 只有槽位四周亮一圈，读作金属边框被砸亮。</summary>
    static Texture2D BuildSlotFlashTexture()
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
                float dx = Mathf.Abs((x + 0.5f) / n - 0.5f) * 2f;
                float dy = Mathf.Abs((y + 0.5f) / n - 0.5f) * 2f;
                float m = Mathf.Max(dx, dy);

                float inner = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.78f, m));
                float outer = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.90f, 1.0f, m));
                float a = inner * outer;

                px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    static Material BuildMaterial(Texture2D tex)
    {
        Shader shader = Shader.Find("AnotherWorld/BoardLayer");
        if (shader == null) return null;

        var mat = new Material(shader);
        mat.SetTexture("_MainTex", tex);
        mat.SetColor("_Color", Color.white);
        mat.SetFloat("_SrcBlend", 5f);   // SrcAlpha
        mat.SetFloat("_DstBlend", 1f);   // One（加法）
        mat.renderQueue = 3100;
        return mat;
    }
}
