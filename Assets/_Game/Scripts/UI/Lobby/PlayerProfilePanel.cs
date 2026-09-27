using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 大厅玩家信息面板——显示 Steam 头像和昵称。
/// </summary>
/// <remarks>2026-09-27 改：① 名字的「后补」条件原来只认空串与「未知玩家」，场景里初值写的是「名字」，
/// 于是永远补不上 —— 现在改成「与 Steam 名不同就刷」。
/// ② 新增 <see cref="circularCrop"/>：头像改成圆形（Steam 头像是方的，直接铺进金圆框会把框吃掉）。
/// 裁切在运行时做（拿源图重算一张带透明边的圆图），不用 Mask、不新增贴图。
/// ③ 头像还没到时铺一块灰圆盘占位，不留黑洞。</remarks>
public class PlayerProfilePanel : MonoBehaviour
{
    [Header("头像")] public RawImage avatarImage;
    public AspectRatioFitter avatarFitter;

    [Header("文字")] public TMP_Text nameText;

    [Header("圆形裁切（大厅金圆框用）")] public bool circularCrop = false;

    [Header("头像未就绪时的占位色")] public Color placeholderColor = new Color32(90, 107, 128, 255);

    void Start()
    {
        Refresh();
    }

    void Update()
    {
        var sd = SteamDataManager.Instance;
        if (sd == null) return;

        // 名字：Steam 初始化比 Start 晚 —— 与 Steam 名不一致就刷（不再只认空串 /「未知玩家」）
        if (nameText != null)
        {
            string name = sd.localPlayerName;
            if (!string.IsNullOrEmpty(name) && name != "未知玩家" && nameText.text != name)
                nameText.text = name;
        }

        ApplyAvatar(sd.localAvatar);
    }

    public void Refresh()
    {
        var sd = SteamDataManager.Instance;
        if (sd == null) return;

        if (nameText != null && !string.IsNullOrEmpty(sd.localPlayerName))
            nameText.text = sd.localPlayerName;

        ApplyAvatar(sd.localAvatar);
    }

    // ===================== 头像 =====================

    Texture2D _applied;          // 已经铺上去的源图（占位时为 null、_placeheld 为 true）

    void ApplyAvatar(Texture2D src)
    {
        if (avatarImage == null) return;

        if (src == null)
        {
            if (avatarImage.texture == null) avatarImage.texture = Placeholder();
            return;
        }
        if (_applied == src && avatarImage.texture != null) return;

        avatarImage.texture = Prepare(src);
        if (avatarFitter != null && src.height > 0)
            avatarFitter.aspectRatio = (float)src.width / src.height;
        _applied = src;
    }

    Texture2D Prepare(Texture2D src)
    {
        if (!circularCrop) return src;
        if (src.width <= 0 || src.height <= 0) return src;
        return CircleCrop(src);
    }

    // ===================== 圆形裁切 / 占位（静态缓存，实例间共用） =====================

    static readonly Dictionary<int, Texture2D> _crops = new Dictionary<int, Texture2D>();
    static Texture2D _placeholder;

    /// <summary>把源图裁成圆 —— 圆外 alpha 归零，圆边留 1px 软过渡（源图必须可读；运行时建的头像都满足）。</summary>
    static Texture2D CircleCrop(Texture2D src)
    {
        int id = src.GetInstanceID();
        Texture2D cached;
        if (_crops.TryGetValue(id, out cached) && cached != null) return cached;

        Texture2D outTex;
        try
        {
            int w = src.width, h = src.height;
            Color[] px = src.GetPixels();
            float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f;
            float rad = Mathf.Min(w, h) * 0.5f;
            for (int y = 0; y < h; y++)
            {
                float dy = y - cy;
                for (int x = 0; x < w; x++)
                {
                    float dx = x - cx;
                    float a = Mathf.Clamp01(rad - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    int i = y * w + x;
                    Color c = px[i];
                    c.a *= a;
                    px[i] = c;
                }
            }
            outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            outTex.wrapMode = TextureWrapMode.Clamp;
            outTex.SetPixels(px);
            outTex.Apply();
        }
        catch (UnityException)
        {
            // 源图不可读（Importer 关了 Read/Write）：退回方形，不吞异常、也不报错刷屏
            Debug.LogWarning("[PlayerProfile] 头像贴图不可读，跳过圆形裁切");
            return src;
        }

        _crops[id] = outTex;
        return outTex;
    }

    /// <summary>灰色圆盘占位（Steam 未初始化 / 头像还没异步回来时用）。</summary>
    static Texture2D Placeholder()
    {
        if (_placeholder != null) return _placeholder;

        const int N = 128;
        var px = new Color[N * N];
        float c = (N - 1) * 0.5f, rad = N * 0.5f;
        var grey = new Color(0.42f, 0.47f, 0.53f, 1f);
        for (int y = 0; y < N; y++)
        {
            float dy = y - c;
            for (int x = 0; x < N; x++)
            {
                float dx = x - c;
                float a = Mathf.Clamp01(rad - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f) * 0.85f;
                px[y * N + x] = new Color(grey.r, grey.g, grey.b, a);
            }
        }
        _placeholder = new Texture2D(N, N, TextureFormat.RGBA32, false);
        _placeholder.wrapMode = TextureWrapMode.Clamp;
        _placeholder.SetPixels(px);
        _placeholder.Apply();
        return _placeholder;
    }
}