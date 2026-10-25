using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>01505 封锁者：敌方空格进场封锁的纯表现层。状态先结算，这里只负责底板、双链、四钉与齿轮锁。</summary>
public static class SlotLockFX01505
{
    const string RootName = "SlotLock01505";
    const string ResRoot = "UI/SlotLock01505/";
    const float BaseSlotWidth = 540f;

    static Sprite _plate;
    static Sprite _tack;
    static Sprite _gear;
    static Sprite _chainA;
    static Sprite _chainB;
    static bool _loadTried;

    static readonly HashSet<BoardSlot> _playing = new HashSet<BoardSlot>();

    sealed class Parts
    {
        public GameObject root;
        public Image plate;
        public Image chainA;
        public Image chainB;
        public Image gear;
        public RectTransform gearRt;
        public Image[] tacks;
        public RectTransform[] tackRts;
        public Vector2[] tackStartOffset;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _playing.Clear();
        _plate = null;
        _tack = null;
        _gear = null;
        _chainA = null;
        _chainB = null;
        _loadTried = false;
    }

    public static bool IsPlaying(BoardSlot slot) => slot != null && _playing.Contains(slot);

    public static void Sync(BoardSlot slot, bool blocked)
    {
        if (slot == null) return;
        if (!blocked)
        {
            Clear(slot);
            return;
        }
        if (_playing.Contains(slot)) return;
        if (slot.transform.Find(RootName) != null) return;
        EnsureOverlay(slot);
    }

    public static void Clear(BoardSlot slot)
    {
        if (slot == null) return;
        Transform root = slot.transform.Find(RootName);
        if (root != null) Object.Destroy(root.gameObject);
        _playing.Remove(slot);
    }

    public static IEnumerator Play(BoardSlot slot)
    {
        Parts p = EnsureOverlay(slot);
        if (p == null) yield break;

        _playing.Add(slot);
        ResetToStart(p, slot);

        const float total = 0.78f;
        const float chainStart = 0.30f;
        const float chainDuration = 0.18f;
        const float gearStart = 0.51f;
        const float gearDrop = 0.12f;
        const float gearLand = gearStart + gearDrop;

        bool flashed = false;
        float t = 0f;
        while (t < total)
        {
            if (slot == null || p.root == null)
            {
                _playing.Remove(slot);
                yield break;
            }

            t += Time.deltaTime;

            float plateU = EaseOutCubic(Mathf.Clamp01(t / 0.14f));
            p.plate.color = new Color(1f, 1f, 1f, plateU);

            float[] delays = { 0.045f, 0.095f, 0.145f, 0.195f };
            for (int i = 0; i < p.tacks.Length; i++)
            {
                float u = Mathf.Clamp01((t - delays[i]) / 0.075f);
                float insert = u * u;
                float alpha = Mathf.Clamp01(u * 2.2f);
                float scale = Mathf.Lerp(1.55f, 1f, insert);
                float after = t - (delays[i] + 0.075f);
                if (after > 0f)
                {
                    float b = Mathf.Clamp01(after / 0.10f);
                    scale += Mathf.Sin(b * Mathf.PI) * (1f - b) * 0.10f;
                }
                p.tacks[i].color = new Color(1f, 1f, 1f, alpha);
                p.tackRts[i].localScale = Vector3.one * scale;
                p.tackRts[i].anchoredPosition = Vector2.Lerp(p.tackStartOffset[i], Vector2.zero, insert);
            }

            float cu = EaseOutCubic(Mathf.Clamp01((t - chainStart) / chainDuration));
            float chainScale = Mathf.Lerp(0.02f, 1f, cu);
            p.chainA.rectTransform.localScale = new Vector3(chainScale, chainScale, 1f);
            p.chainB.rectTransform.localScale = new Vector3(chainScale, chainScale, 1f);

            float gu = Mathf.Clamp01((t - gearStart) / gearDrop);
            float drop = gu * gu;
            float appear = Mathf.Clamp01(gu * 2.5f);
            if (t < gearLand)
            {
                p.gearRt.anchoredPosition = new Vector2(0f, Mathf.Lerp(84f, 0f, drop));
                p.gearRt.localScale = Vector3.one * Mathf.Lerp(1.22f, 1f, drop);
                p.gearRt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-25f, 0f, drop));
            }
            else
            {
                float b = Mathf.Clamp01((t - gearLand) / 0.13f);
                float squash = Mathf.Sin(b * Mathf.PI) * (1f - b);
                p.gearRt.anchoredPosition = Vector2.zero;
                p.gearRt.localScale = new Vector3(1f + squash * 0.08f, 1f - squash * 0.065f, 1f);
                p.gearRt.localRotation = Quaternion.identity;
            }
            p.gear.color = new Color(1f, 1f, 1f, appear);

            if (!flashed && t >= gearLand)
            {
                flashed = true;
                CardSummonFX.PlaySlotFlash(slot, 0.16f, 1.04f);
            }

            yield return null;
        }

        SetFinal(p);
        _playing.Remove(slot);
    }

    static Parts EnsureOverlay(BoardSlot slot)
    {
        LoadAssets();
        if (_plate == null || _tack == null || _gear == null || _chainA == null || _chainB == null)
            return null;

        Transform existing = slot.transform.Find(RootName);
        if (existing != null)
        {
            Parts bound = BindParts(existing.gameObject);
            if (bound != null) return bound;
            Object.Destroy(existing.gameObject);
        }

        RectTransform slotRt = slot.transform as RectTransform;
        Vector2 slotSize = slotRt != null && slotRt.rect.width > 0.0001f
            ? slotRt.rect.size
            : new Vector2(1.25f, 2.22f) * HandManager.BoardLayoutScale;

        GameObject root = new GameObject(RootName, typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(slot.transform, false);
        RectTransform rootRt = (RectTransform)root.transform;
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.anchoredPosition = Vector2.zero;
        rootRt.localPosition = new Vector3(0f, 0f, -0.035f);
        rootRt.localRotation = Quaternion.identity;
        rootRt.localScale = Vector3.one;

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        group.alpha = 1f;
        group.interactable = false;
        group.blocksRaycasts = false;

        Image plate = CreateImage(root.transform, "Plate", _plate);
        Stretch(plate.rectTransform);

        Vector2 pivotA = new Vector2(76f / BaseSlotWidth, 1f - 88f / 960f);
        Vector2 pivotB = new Vector2(464f / BaseSlotWidth, 1f - 88f / 960f);
        Image chainA = CreateChain(root.transform, "ChainA", _chainA, pivotA, slotSize);
        Image chainB = CreateChain(root.transform, "ChainB", _chainB, pivotB, slotSize);

        Vector2[] positions =
        {
            new Vector2(0.17478f, 0.88919f),
            new Vector2(0.82522f, 0.88919f),
            new Vector2(0.82522f, 0.11081f),
            new Vector2(0.17478f, 0.11081f),
        };
        float[] rotations = { -135f, 135f, 45f, -45f };
        Vector2[] outward =
        {
            new Vector2(-0.7071f, 0.7071f),
            new Vector2(0.7071f, 0.7071f),
            new Vector2(0.7071f, -0.7071f),
            new Vector2(-0.7071f, -0.7071f),
        };

        Image[] tacks = new Image[4];
        RectTransform[] tackRts = new RectTransform[4];
        Vector2[] tackStartOffset = new Vector2[4];
        float tackSide = slotSize.x * (88f / BaseSlotWidth);

        for (int i = 0; i < 4; i++)
        {
            Image tack = CreateImage(root.transform, "Tack_" + i, _tack);
            RectTransform rt = tack.rectTransform;
            rt.anchorMin = positions[i];
            rt.anchorMax = positions[i];
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(tackSide, tackSide);
            rt.anchoredPosition = Vector2.zero;
            rt.localRotation = Quaternion.Euler(0f, 0f, rotations[i]);
            tacks[i] = tack;
            tackRts[i] = rt;
            tackStartOffset[i] = outward[i] * (slotSize.x * 0.035f);
        }

        Image gear = CreateImage(root.transform, "Gear", _gear);
        RectTransform gearRt = gear.rectTransform;
        gearRt.anchorMin = new Vector2(0.5f, 0.5f);
        gearRt.anchorMax = new Vector2(0.5f, 0.5f);
        gearRt.pivot = new Vector2(0.5f, 0.5f);
        float gearSide = slotSize.x * (184f / BaseSlotWidth);
        gearRt.sizeDelta = new Vector2(gearSide, gearSide);
        gearRt.anchoredPosition = Vector2.zero;
        gearRt.localRotation = Quaternion.identity;

        root.transform.SetAsLastSibling();
        return new Parts
        {
            root = root,
            plate = plate,
            chainA = chainA,
            chainB = chainB,
            gear = gear,
            gearRt = gearRt,
            tacks = tacks,
            tackRts = tackRts,
            tackStartOffset = tackStartOffset,
        };
    }

    static Parts BindParts(GameObject root)
    {
        Parts p = new Parts
        {
            root = root,
            plate = FindImage(root.transform, "Plate"),
            chainA = FindImage(root.transform, "ChainA"),
            chainB = FindImage(root.transform, "ChainB"),
            gear = FindImage(root.transform, "Gear"),
        };
        if (p.plate == null || p.chainA == null || p.chainB == null || p.gear == null) return null;
        p.gearRt = p.gear.rectTransform;
        p.tacks = new Image[4];
        p.tackRts = new RectTransform[4];
        p.tackStartOffset = new Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            p.tacks[i] = FindImage(root.transform, "Tack_" + i);
            if (p.tacks[i] == null) return null;
            p.tackRts[i] = p.tacks[i].rectTransform;
        }
        return p;
    }

    static void ResetToStart(Parts p, BoardSlot slot)
    {
        p.plate.color = new Color(1f, 1f, 1f, 0f);
        p.chainA.rectTransform.localScale = new Vector3(0.02f, 0.02f, 1f);
        p.chainB.rectTransform.localScale = new Vector3(0.02f, 0.02f, 1f);
        p.gear.color = new Color(1f, 1f, 1f, 0f);
        p.gearRt.anchoredPosition = new Vector2(0f, 84f);
        p.gearRt.localScale = Vector3.one * 1.22f;
        p.gearRt.localRotation = Quaternion.Euler(0f, 0f, -25f);

        Vector2[] outward =
        {
            new Vector2(-0.7071f, 0.7071f),
            new Vector2(0.7071f, 0.7071f),
            new Vector2(0.7071f, -0.7071f),
            new Vector2(-0.7071f, -0.7071f),
        };
        RectTransform slotRt = slot != null ? slot.transform as RectTransform : null;
        Vector2 slotSize = slotRt != null && slotRt.rect.width > 0.0001f
            ? slotRt.rect.size
            : new Vector2(1.25f, 2.22f) * HandManager.BoardLayoutScale;
        for (int i = 0; i < p.tacks.Length; i++)
        {
            p.tacks[i].color = new Color(1f, 1f, 1f, 0f);
            p.tackRts[i].localScale = Vector3.one * 1.55f;
            p.tackStartOffset[i] = outward[i] * (slotSize.x * 0.035f);
            p.tackRts[i].anchoredPosition = p.tackStartOffset[i];
        }
    }

    static void SetFinal(Parts p)
    {
        if (p == null || p.root == null) return;
        p.plate.color = Color.white;
        p.chainA.rectTransform.localScale = Vector3.one;
        p.chainB.rectTransform.localScale = Vector3.one;
        p.gear.color = Color.white;
        p.gearRt.anchoredPosition = Vector2.zero;
        p.gearRt.localScale = Vector3.one;
        p.gearRt.localRotation = Quaternion.identity;
        for (int i = 0; i < p.tacks.Length; i++)
        {
            p.tacks[i].color = Color.white;
            p.tackRts[i].anchoredPosition = Vector2.zero;
            p.tackRts[i].localScale = Vector3.one;
        }
    }

    static Image CreateImage(Transform parent, string name, Sprite sprite)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.preserveAspect = false;
        image.color = Color.white;
        return image;
    }

    static Image CreateChain(Transform parent, string name, Sprite sprite, Vector2 pivot, Vector2 slotSize)
    {
        Image image = CreateImage(parent, name, sprite);
        RectTransform rt = image.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = pivot;
        rt.sizeDelta = slotSize;
        rt.anchoredPosition = (pivot - new Vector2(0.5f, 0.5f)) * slotSize;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
        return image;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Image FindImage(Transform root, string name)
    {
        Transform t = root.Find(name);
        return t != null ? t.GetComponent<Image>() : null;
    }

    static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

    static void LoadAssets()
    {
        if (_loadTried) return;
        _loadTried = true;
        _plate = LoadSprite("SlotLock_Plate_01505");
        _tack = LoadSprite("SlotLock_Tack_01505");
        _gear = LoadSprite("SlotLock_Gear_01505");
        _chainA = LoadSprite("SlotLock_ChainA_01505");
        _chainB = LoadSprite("SlotLock_ChainB_01505");
        if (_plate == null || _tack == null || _gear == null || _chainA == null || _chainB == null)
            Debug.LogWarning("[01505] Slot lock sprites missing under Resources/" + ResRoot);
    }

    static Sprite LoadSprite(string name)
    {
        string path = ResRoot + name;
        Texture2D tex = Resources.Load<Texture2D>(path);
        if (tex != null)
        {
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }
        return Resources.Load<Sprite>(path);
    }
}
