using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

/// <summary>
/// 一键生成阶段轮盘（PhaseWheel）UI 结构：
///   PhaseWheel（Canvas 下）
///     └─ MaskArea（RectMask2D，Inspector 手动框定可见范围）
///          └─ WheelContainer（挂 PhaseWheel）
///               ├─ Slot_Hidden1 / Slot_Left / Slot_Center / Slot_Right / Slot_Hidden2
/// 每个 RingSlot：RingBackground（空白环）→ AvatarMask（圆形 Mask）→ AvatarImage；IconImage（阶段图标）。
/// 同时生成 RingSlot.prefab 模板供复用。
/// </summary>
public class PhaseWheelBuilder
{
    const string RING_EMPTY = "Assets/_Game/Art/Sprites/UI/ring_empty.png";
    const string BATTLE_ICON = "Assets/_Game/Art/Sprites/UI/Battle Phase.png";
    const string PHASE_WHEEL = "Assets/_Game/Art/Sprites/UI/Phase Wheel.png";
    const string CIRCLE_PATH = "Assets/_Game/Art/Sprites/UI/PhaseWheelCircle.png";
    const string PREFAB_PATH = "Assets/_Game/Prefabs/Board/RingSlot.prefab";

    /// <summary>5 个环的精确角度（度），整体逆时针 90°：Hidden1=60, Left=120, Center=180(正下), Right=240, Hidden2=300。
    /// 底盘平边朝上（旋转180°）后，中环在圆心正下方，左右环在两侧，隐藏环靠近平边。</summary>
    static readonly float[] SLOT_ANGLES = { 60f, 120f, 180f, 240f, 300f };

    [MenuItem("Tools/异界/创建阶段轮盘")]
    public static void Create()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null) { Debug.LogError("[PhaseWheel] 场景中无 Canvas，请先创建。"); return; }

        // ===== PhaseWheel 根 =====
        var wheelGO = new GameObject("PhaseWheel", typeof(RectTransform));
        wheelGO.transform.SetParent(canvas.transform, false);
        var wheelRT = wheelGO.GetComponent<RectTransform>();
        wheelRT.anchorMin = new Vector2(0.5f, 1f);
        wheelRT.anchorMax = new Vector2(0.5f, 1f);
        wheelRT.pivot = new Vector2(0.5f, 0.5f);
        wheelRT.anchoredPosition = new Vector2(0f, -70f); // 顶部居中，可手动摆
        wheelRT.sizeDelta = new Vector2(500f, 130f);

        // ===== MaskArea（完全手动框定可见范围——不写死尺寸/位置，拖动 RectTransform 边角调整）=====
        var maskGO = new GameObject("MaskArea", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        maskGO.transform.SetParent(wheelGO.transform, false);
        var maskRT = maskGO.GetComponent<RectTransform>();
        maskRT.anchorMin = maskRT.anchorMax = new Vector2(0.5f, 0.5f); // 中心锚点，自由拖动
        maskRT.pivot = new Vector2(0.5f, 0.5f);
        maskRT.sizeDelta = new Vector2(300f, 280f); // 仅初始值，Inspector/Scene 中手动拖动调整
        maskRT.localPosition = Vector3.zero;
        var maskImg = maskGO.GetComponent<Image>();
        maskImg.color = new Color(1f, 1f, 1f, 0.02f); // 近透明底，方便在 Inspector 调可见范围
        maskImg.raycastTarget = false;

        // ===== 静态底盘（Phase Wheel.png，不参与旋转）=====
        var baseGO = CreateImage(wheelGO.transform, "BasePlate", new Vector2(520f, 300f));
        baseGO.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PHASE_WHEEL);
        baseGO.color = Color.white;
        baseGO.transform.SetAsFirstSibling(); // 环之下
        // 平边朝上：旋转 180°（默认渲染平边朝下）。中环在圆心下方。
        baseGO.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);

        // ===== WheelContainer =====
        var contGO = new GameObject("WheelContainer", typeof(RectTransform));
        contGO.transform.SetParent(maskGO.transform, false);
        var contRT = contGO.GetComponent<RectTransform>();
        contRT.anchorMin = Vector2.zero; contRT.anchorMax = Vector2.one;
        contRT.pivot = new Vector2(0.5f, 0.5f);
        contRT.offsetMin = Vector2.zero; contRT.offsetMax = Vector2.zero;

        // ===== 5 个 RingSlot（按角度环形分布）=====
        float slotW = 120f, slotH = 120f, radius = 125f;
        string[] names = { "Slot_Hidden1", "Slot_Left", "Slot_Center", "Slot_Right", "Slot_Hidden2" };
        var slots = new List<RingSlot>();
        for (int i = 0; i < 5; i++)
            slots.Add(CreateRingSlot(contGO.transform, names[i], i, slotW, slotH, radius));

        // ===== PhaseWheel 组件 + 引用 =====
        var pw = wheelGO.AddComponent<PhaseWheel>();
        pw.wheelContainer = contRT;
        pw.slots = slots.ToArray();
        pw.battleIcon = AssetDatabase.LoadAssetAtPath<Sprite>(BATTLE_ICON);
        pw.rotateDuration = 0.4f;

        // ===== 生成 RingSlot.prefab 模板 =====
        EnsurePrefabDirectory();
        PrefabUtility.SaveAsPrefabAsset(slots[0].gameObject, PREFAB_PATH);

        Selection.activeGameObject = wheelGO;
        Undo.RegisterCreatedObjectUndo(wheelGO, "Create PhaseWheel");
        Debug.Log("[PhaseWheel] 已创建：PhaseWheel → MaskArea → WheelContainer → 5 RingSlot。\n" +
                  "请手动调整：① PhaseWheel 位置 ② MaskArea 可见范围（让中间 3 环显示）③ MaskArea 底色 alpha。\n" +
                  $"RingSlot 模板已存 {PREFAB_PATH}");
    }

    static RingSlot CreateRingSlot(Transform parent, string name, int idx, float w, float h, float radius)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        // 按精确角度分布：x = r·sin(θ), y = r·cos(θ)，90°=顶部（中央显示位）
        float theta = SLOT_ANGLES[idx] * Mathf.Deg2Rad;
        rt.anchoredPosition = new Vector2(Mathf.Sin(theta) * radius, Mathf.Cos(theta) * radius);

        var slot = go.AddComponent<RingSlot>();

        // RingBackground（空白环美术，始终显示）
        var bg = CreateImage(go.transform, "RingBackground", new Vector2(w, h));
        bg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RING_EMPTY);
        slot.ringBackground = bg;

        // AvatarMask（圆形 Mask 遮罩）
        var maskRT2 = NewRect("AvatarMask", go.transform);
        maskRT2.sizeDelta = new Vector2(w * 0.62f, h * 0.62f);
        var maskImg = maskRT2.gameObject.AddComponent<Image>();
        maskImg.sprite = GetCircleSprite();
        maskImg.raycastTarget = false;
        var mask = maskRT2.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        // AvatarImage（头像，在 Mask 下）
        var avRT = NewRect("AvatarImage", maskRT2);
        avRT.sizeDelta = new Vector2(w * 0.60f, h * 0.60f);
        var avImg = avRT.gameObject.AddComponent<Image>();
        avImg.raycastTarget = false;
        avImg.enabled = false;
        slot.avatarImage = avImg;

        // IconImage（阶段图标，如攻击回合）
        var iconRT = NewRect("IconImage", go.transform);
        iconRT.sizeDelta = new Vector2(w * 0.5f, h * 0.5f);
        var iconImg = iconRT.gameObject.AddComponent<Image>();
        iconImg.raycastTarget = false;
        iconImg.enabled = false;
        slot.iconImage = iconImg;

        return slot;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localPosition = Vector3.zero;
        return rt;
    }

    static Image CreateImage(Transform parent, string name, Vector2 size)
    {
        var rt = NewRect(name, parent);
        rt.sizeDelta = size;
        var img = rt.gameObject.AddComponent<Image>();
        img.raycastTarget = false;
        return img;
    }

    static Sprite _circleSprite;
    static Sprite GetCircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;
        // 生成持久的圆形 PNG 资产——运行时创建的 Texture2D/Sprite 不序列化，
        // 预制体/场景保存后 AvatarMask 的圆形遮罩会丢失，导致头像显示为方形。
        string abs = Path.Combine(Application.dataPath, "_Game/Art/Sprites/UI/PhaseWheelCircle.png");
        if (!File.Exists(abs))
        {
            int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - r, dy = y - r;
                    tex.SetPixel(x, y, (dx * dx + dy * dy <= r * r) ? Color.white : Color.clear);
                }
            tex.Apply();
            File.WriteAllBytes(abs, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(CIRCLE_PATH);
            Object.DestroyImmediate(tex);
        }
        // 确保导入为 Sprite
        var importer = AssetImporter.GetAtPath(CIRCLE_PATH) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.SaveAndReimport();
        }
        _circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CIRCLE_PATH);
        return _circleSprite;
    }

    static void EnsurePrefabDirectory()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Game/Prefabs/Board"))
        {
            AssetDatabase.CreateFolder("Assets/_Game/Prefabs", "Board");
            AssetDatabase.SaveAssets();
        }
    }
}
