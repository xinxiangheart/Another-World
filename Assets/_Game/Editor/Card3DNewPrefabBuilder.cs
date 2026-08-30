using UnityEngine;
using UnityEditor;
using TMPro;

/// <summary>
/// 一键生成新 3D 手牌卡牌预制体（独立于旧 3D 卡 Card00_3D，不修改任何旧资产）。
/// 模型：Assets/_Game/Art/Models/Summon/Card00_New.fbx（0.9×1.6×0.03 薄盒，卡面 +Z / 卡背+侧面 -Z）。
/// 卡面材质：CardComposite 三层合成，由 CardDisplay3D.ApplyArtFromCard 按 Cards/ 目录路径加载：
///   _BgTex = 费用卡框 Cards/SummonCard_{cost}；_BorderTex = 前缀底图 Cards/PrefixArtBG/{English}；
///   _ArtTex = cardSprite2D → Cards/{tid}_Front → 镜像 Cards/Summon 目录。
/// 背面材质：新 card_new_back.mat（CardCutout + Cards/Back.png）。
/// 布局：以新 2D 预制体 Card00_New_2D 为基准（0.9×1.6 卡 ×0.0108/0.01093 换算），
///   卡面 = 网格(CostFrameBase/ArtworkArea)，文字 + 图标 + 三排图标对齐 2D：
///   - NameText 卡名(顶部横幅) / CostIcon+CostText 左上(部分超边) / TypeIcon 顶部中央
///   - HealthIcon+HealthText 左下(超边) / AttackIcon+AttackText 右下(超边)
///   - PrefixIconsArea / TraitIconsArea / StatusIconsArea 三排
/// 朝向：卡根运行时 Euler(0,180,0)，所有正面元素 localRotation = identity（世界法线 -Z 朝相机）。
///   文字用 identity（区别于旧卡 localY180——那在双面 TMP 下是镜像显示）。
/// 挂载：Card3DInstance / CardDisplay3D / CardIcons3D / Card3DHover / DamageSourceMarker / BoxCollider。
/// 菜单：Tools → 卡牌 → 生成新3D手牌预制体
/// </summary>
public static class Card3DNewPrefabBuilder
{
    const string FBXPath = "Assets/_Game/Art/Models/Summon/Card00_New.fbx";
    const string FrontMatPath = "Assets/_Game/Art/Materials/card_new_front.mat";
    const string BackMatPath = "Assets/_Game/Art/Materials/card_new_back.mat";
    const string FontPath = "Assets/_Game/Fonts/NotoSerifCJKsc-Black SDF.asset";
    const string PrefabPath = "Assets/_Game/Prefabs/Cards/Summon/Card00_New_3D.prefab";

    // 旧 3D 卡文字显示参数（保证在 0.9×1.6 卡上渲染一致）
    static readonly Vector3 TextScale = new Vector3(1.4814816f, 1f, 0.8333331f);
    const float TextZ = 0.1f;      // 文字 z
    const float IconZ = 0.06f;     // 图标 z（文字下层）
    const float RowZ  = 0.06f;     // 三排 z

    // 2D 布局换算（×0.0108 / ×0.01093），角标按"部分超出卡边"外延调整
    static readonly Vector2 NamePos   = new Vector2(0f,     0.64f);   // NameText 顶部横幅
    static readonly Vector2 CostPos   = new Vector2(-0.40f, 0.74f);   // 左上，左超边
    static readonly Vector2 TypePos   = new Vector2(0f,     0.72f);   // 顶部中央
    static readonly Vector2 HealthPos = new Vector2(-0.40f, -0.63f);  // 左下，左超边
    static readonly Vector2 AttackPos = new Vector2(0.40f,  -0.63f);  // 右下，右超边
    static readonly Vector2 PrefixRowPos = new Vector2(0f, 0.52f);    // 前缀排
    static readonly Vector2 TraitRowPos  = new Vector2(0f, -0.54f);   // 特性排
    static readonly Vector2 StatusRowPos = new Vector2(0f, -0.76f);   // 状态排

    [MenuItem("Tools/卡牌/生成新3D手牌预制体")]
    public static void CreatePrefab()
    {
        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FBXPath);
        if (fbx == null) { Debug.LogError($"[Card3DNew] 找不到模型: {FBXPath}"); return; }
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null) { Debug.LogError($"[Card3DNew] 找不到字体: {FontPath}"); return; }

        // ── 材质：新 CardComposite 卡面 + 复用卡背 ──
        Material frontMat = AssetDatabase.LoadAssetAtPath<Material>(FrontMatPath);
        if (frontMat == null)
        {
            Shader composite = Shader.Find("AnotherWorld/CardComposite");
            if (composite == null) { Debug.LogError("[Card3DNew] 找不到 shader: AnotherWorld/CardComposite"); return; }
            frontMat = new Material(composite) { name = "card_new_front" };
            AssetDatabase.CreateAsset(frontMat, FrontMatPath);
        }
        Material backMat = AssetDatabase.LoadAssetAtPath<Material>(BackMatPath);
        if (backMat == null)
        {
            // 新背面材质：CardCutout + Cards/Back.png（不动共享的 cardback.mat）
            Shader cutout = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Game/Art/Shaders/CardCutout.shader");
            if (cutout == null) { Debug.LogError("[Card3DNew] 找不到 shader: AnotherWorld/CardCutout"); return; }
            backMat = new Material(cutout) { name = "card_new_back" };
            Texture2D backTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Sprites/Cards/Back.png");
            if (backTex != null) backMat.SetTexture("_MainTex", backTex);
            AssetDatabase.CreateAsset(backMat, BackMatPath);
        }

        // ── 实例化模型 ──
        GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        root.name = "Card00_New_3D";
        root.transform.localScale = Vector3.one;
        root.transform.localRotation = Quaternion.identity;

        // ── 材质槽：slot0=卡面(CardComposite)  slot1=卡背 ──
        MeshRenderer mr = root.GetComponentInChildren<MeshRenderer>();
        if (mr == null) { Debug.LogError("[Card3DNew] 模型无 MeshRenderer"); Object.DestroyImmediate(root); return; }
        Material[] mats = new Material[Mathf.Max(2, mr.sharedMaterials.Length)];
        mats[0] = frontMat;
        for (int i = 1; i < mats.Length; i++) mats[i] = backMat;
        mr.sharedMaterials = mats;

        // ── 脚本（与旧 3D 卡一致 + 图标显示；Animator 由 Card3DInstance.Awake 运行时补）──
        root.AddComponent<Card3DInstance>();
        CardDisplay3D display = root.AddComponent<CardDisplay3D>();
        CardIcons3D icons = root.AddComponent<CardIcons3D>();
        root.AddComponent<Card3DHover>();
        root.AddComponent<DamageSourceMarker>();

        // ── BoxCollider（比卡面略大便于点击，可调）──
        BoxCollider bc = root.GetComponent<BoxCollider>();
        if (bc == null) bc = root.AddComponent<BoxCollider>();
        bc.size = new Vector3(1.1f, 1.9f, 0.03f);
        bc.center = Vector3.zero;

        // ── 文字（identity 朝向，按 2D 布局定位）──
        // 字号比例对齐 2D（名 8.2 < 费 10 ≈ 血 9.8 < 攻 10.55）：名字最小、攻最大
        TextMeshPro nameT = CreateTextChild(root, "NameText", font, "卡名", 0.55f, 0.75f, NamePos);
        TextMeshPro costT = CreateTextChild(root, "CostText", font, "0", 0.72f, 0.92f, CostPos);
        TextMeshPro atkT  = CreateTextChild(root, "AttackText", font, "0", 0.75f, 0.95f, AttackPos);
        TextMeshPro hpT   = CreateTextChild(root, "HealthText", font, "0", 0.70f, 0.90f, HealthPos);

        // ── 角标图标 SpriteRenderer（identity 朝向，运行时由 CardIcons3D 填 sprite）──
        SpriteRenderer costIcon   = CreateIconChild(root, "CostIcon",   CostPos);
        SpriteRenderer typeIcon   = CreateIconChild(root, "TypeIcon",   TypePos);
        SpriteRenderer healthIcon = CreateIconChild(root, "HealthIcon", HealthPos);
        SpriteRenderer attackIcon = CreateIconChild(root, "AttackIcon", AttackPos);

        // ── 三排容器（运行时动态生成子图标）──
        Transform prefixRow = CreateRowChild(root, "PrefixIconsArea", PrefixRowPos);
        Transform traitRow  = CreateRowChild(root, "TraitIconsArea",  TraitRowPos);
        Transform statusRow = CreateRowChild(root, "StatusIconsArea", StatusRowPos);

        // ── 接线显示脚本 ──
        display.nameText = nameT;
        display.costText = costT;
        display.attackText = atkT;
        display.healthText = hpT;
        display.prefixText = null; // 前缀由 PrefixIconsArea 图标排显示，不显示字符串

        icons.costIcon = costIcon;
        icons.typeIcon = typeIcon;
        icons.healthIcon = healthIcon;
        icons.attackIcon = attackIcon;
        icons.prefixIconsRow = prefixRow;
        icons.traitIconsRow = traitRow;
        icons.statusIconsRow = statusRow;

        // ── 保存预制体 ──
        string dir = System.IO.Path.GetDirectoryName(PrefabPath);
        if (!AssetDatabase.IsValidFolder(dir))
        {
            string parent = System.IO.Path.GetDirectoryName(dir).Replace('\\', '/');
            string folder = System.IO.Path.GetFileName(dir);
            AssetDatabase.CreateFolder(parent, folder);
        }
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        Debug.Log($"[Card3DNew] 预制体已生成: {PrefabPath}（CardComposite 卡面 + 文字/图标按 2D 布局，三排图标运行时填充）");
    }

    /// <summary>创建 TMP 3D 文字子物体：identity 朝向（卡根 Y180 后正面朝相机），位置按 2D 换算。</summary>
    static TextMeshPro CreateTextChild(GameObject parent, string name, TMP_FontAsset font,
        string content, float sizeMin, float sizeMax, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshPro));
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = new Vector3(pos.x, pos.y, TextZ);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = TextScale;

        TextMeshPro tmp = go.GetComponent<TextMeshPro>();
        tmp.font = font;
        tmp.fontSize = sizeMin;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = sizeMin;
        tmp.fontSizeMax = sizeMax;
        tmp.color = Color.white; // 对齐 2D 新卡（Card00_New_2D 文字为白色）
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.text = content;
        tmp.rectTransform.sizeDelta = new Vector2(0.5f, 0.5f);
        return tmp;
    }

    /// <summary>创建角标图标 SpriteRenderer：identity 朝向，运行时 CardIcons3D 填 sprite 并按 cornerIconSize 缩放。</summary>
    static SpriteRenderer CreateIconChild(GameObject parent, string name, Vector2 pos)
    {
        var go = new GameObject(name, typeof(SpriteRenderer));
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = new Vector3(pos.x, pos.y, IconZ);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.GetComponent<SpriteRenderer>();
    }

    /// <summary>创建三排图标容器（空 Transform，运行时 CardIcons3D 动态添加子 SpriteRenderer 沿 X 排列）。</summary>
    static Transform CreateRowChild(GameObject parent, string name, Vector2 pos)
    {
        var go = new GameObject(name, typeof(Transform));
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = new Vector3(pos.x, pos.y, RowZ);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }
}
