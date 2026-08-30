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
/// 层级：CardRoot（脚本/碰撞体/占地）→ ModelRoot（模型网格，独立缩放） + UIComponents（文字/图标/三排）。
///   运行时脚本用 GetComponentInChildren<MeshRenderer>() 取模型网格（ModelRoot 必须是 CardRoot 第 0 子物体）。
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
    // 卡面元素 z 必须 > 0.1：卡面网格前表面约在本地 z≈0.1，低于它会被不透明卡面遮挡（0.02~0.06 全被遮，0.1+ 正常）。
    // 全部抬到 0.1 以上，前->后间隔 0.01~0.02 即可正常渲染。
    const float TextZ = 0.16f;      // 文字 z（最前）
    const float IconZ = 0.14f;     // 图标 z
    const float RowZ  = 0.15f;     // 三排 z

    // 卡面三层 SpriteRenderer z（前->后：卡图 > 前缀背景 > 卡框；均低于图标/三排）
    const float FaceArtZ    = 0.13f;   // 卡图 z（最前）
    const float FacePrefixZ = 0.12f;   // 前缀背景 z
    const float FaceFrameZ  = 0.11f;   // 卡框 z（最下）
    // 默认铺满尺寸（生成器按贴图 bounds 反算 localScale；生成后可手调，运行时不重算）
    static readonly Vector2 FrameSize   = new Vector2(0.9f, 1.6f);   // 卡框铺满卡面
    static readonly Vector2 ArtAreaSize = new Vector2(0.69f, 0.92f); // 前缀背景/卡图（ArtworkArea 比例）

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

        // ── 层级：CardRoot(位置/占地/逻辑) → ModelRoot(模型网格,可独立缩放) + UIComponents(文字/图标/三排) ──
        GameObject cardRoot = new GameObject("CardRoot");
        cardRoot.transform.localScale = Vector3.one;
        cardRoot.transform.localRotation = Quaternion.identity;

        // ModelRoot：只放模型网格——调整它的 Scale 只缩放模型，不影响文字/图标。
        // 注意：ModelRoot 必须是 CardRoot 的第 0 个子物体，保证运行时 GetComponentInChildren<MeshRenderer>()
        // 先命中模型网格（TMP 文字也有自己的 MeshRenderer，在 UIComponents 分支里，不能被先找到）。
        GameObject modelRoot = new GameObject("ModelRoot");
        modelRoot.transform.SetParent(cardRoot.transform, false);
        modelRoot.transform.localPosition = Vector3.zero;
        modelRoot.transform.localRotation = Quaternion.identity;
        modelRoot.transform.localScale = Vector3.one;

        GameObject fbxInstance = (GameObject)PrefabUtility.InstantiatePrefab(fbx, modelRoot.transform);
        fbxInstance.name = "CardModel";
        fbxInstance.transform.localPosition = Vector3.zero;
        fbxInstance.transform.localRotation = Quaternion.identity;
        fbxInstance.transform.localScale = Vector3.one;

        // ── 材质槽：slot0=卡面(CardComposite)  slot1=卡背 ──
        MeshRenderer mr = fbxInstance.GetComponentInChildren<MeshRenderer>();
        if (mr == null) { Debug.LogError("[Card3DNew] 模型无 MeshRenderer"); Object.DestroyImmediate(cardRoot); return; }
        Material[] mats = new Material[Mathf.Max(2, mr.sharedMaterials.Length)];
        mats[0] = frontMat;
        for (int i = 1; i < mats.Length; i++) mats[i] = backMat;
        mr.sharedMaterials = mats;

        // UIComponents：文字/图标/三排容器，不跟随 ModelRoot 缩放
        GameObject uiRoot = new GameObject("UIComponents");
        uiRoot.transform.SetParent(cardRoot.transform, false);
        uiRoot.transform.localPosition = Vector3.zero;
        uiRoot.transform.localRotation = Quaternion.identity;
        uiRoot.transform.localScale = Vector3.one;

        // ── 脚本（挂 CardRoot；MeshRenderer 在 ModelRoot 子层级，运行时用 GetComponentInChildren 取）──
        cardRoot.AddComponent<Card3DInstance>();
        CardDisplay3D display = cardRoot.AddComponent<CardDisplay3D>();
        CardIcons3D icons = cardRoot.AddComponent<CardIcons3D>();
        cardRoot.AddComponent<Card3DHover>();
        cardRoot.AddComponent<DamageSourceMarker>();

        // ── BoxCollider（比卡面略大便于点击，可调）──
        BoxCollider bc = cardRoot.GetComponent<BoxCollider>();
        if (bc == null) bc = cardRoot.AddComponent<BoxCollider>();
        bc.size = new Vector3(1.1f, 1.9f, 0.03f);
        bc.center = Vector3.zero;

        // ── 文字（identity 朝向，按 2D 布局定位）──
        // 字号比例对齐 2D（名 8.2 < 费 10 ≈ 血 9.8 < 攻 10.55）：名字最小、攻最大
        TextMeshPro nameT = CreateTextChild(uiRoot, "NameText", font, "卡名", 0.55f, 0.75f, NamePos);
        TextMeshPro costT = CreateTextChild(uiRoot, "CostText", font, "0", 0.72f, 0.92f, CostPos);
        TextMeshPro atkT  = CreateTextChild(uiRoot, "AttackText", font, "0", 0.75f, 0.95f, AttackPos);
        TextMeshPro hpT   = CreateTextChild(uiRoot, "HealthText", font, "0", 0.70f, 0.90f, HealthPos);

        // ── 角标图标 SpriteRenderer（identity 朝向，运行时由 CardIcons3D 填 sprite）──
        SpriteRenderer costIcon   = CreateIconChild(uiRoot, "CostIcon",   CostPos);
        SpriteRenderer typeIcon   = CreateIconChild(uiRoot, "TypeIcon",   TypePos);
        SpriteRenderer healthIcon = CreateIconChild(uiRoot, "HealthIcon", HealthPos);
        SpriteRenderer attackIcon = CreateIconChild(uiRoot, "AttackIcon", AttackPos);

        // ── 三排容器（运行时动态生成子图标）──
        Transform prefixRow = CreateRowChild(uiRoot, "PrefixIconsArea", PrefixRowPos);
        Transform traitRow  = CreateRowChild(uiRoot, "TraitIconsArea",  TraitRowPos);
        Transform statusRow = CreateRowChild(uiRoot, "StatusIconsArea", StatusRowPos);

        // ── 卡面三层 SpriteRenderer（卡框/前缀背景/卡图）。
        //    默认比例按实际贴图 bounds 反算（编辑器加载）；生成后可手调，运行时不重算/不覆盖/不缩放。──
        SpriteRenderer frameSR  = CreateFaceSR(uiRoot, "CardFrame", FaceFrameZ,
            FitScale(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Sprites/Cards/SummonCard_0.png"), FrameSize.x, FrameSize.y));
        SpriteRenderer prefixSR = CreateFaceSR(uiRoot, "PrefixBg", FacePrefixZ,
            FitScale(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Sprites/Cards/PrefixArtBG/Abyss.png"), ArtAreaSize.x, ArtAreaSize.y));
        SpriteRenderer artSR    = CreateFaceSR(uiRoot, "CardArt", FaceArtZ,
            FitScale(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Sprites/Cards/Summon/Hero/1/SummonCard_{01103}.png"), ArtAreaSize.x, ArtAreaSize.y));

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

        // ── 填入图标 Sprite（对齐 2D Card00_New_2D 拖入字段；均有路径兜底，填上保证精确一致）──
        icons.energyIconSprite        = LoadEditorSprite("UI/Cost");
        icons.attackIconSprite        = LoadEditorSprite("UI/Attack");
        icons.healthIconSprite        = LoadEditorSprite("UI/Health");
        icons.heroTypeSprite          = LoadEditorSprite("UI/Hero");
        icons.chosenOneTypeSprite     = LoadEditorSprite("UI/Chosen");
        icons.specialTypeSprite       = LoadEditorSprite("UI/Special");
        icons.prefixPsychicSprite     = LoadEditorSprite("Icons/Prefixes/Psychic");
        icons.prefixAbyssSprite       = LoadEditorSprite("Icons/Prefixes/Abyss");
        icons.prefixMechSprite        = LoadEditorSprite("Icons/Prefixes/Mech");
        icons.prefixBloodsongSprite   = LoadEditorSprite("Icons/Prefixes/Blood");
        icons.prefixScrollSprite      = LoadEditorSprite("Icons/Prefixes/Scroll");
        icons.traitFirstStrikeSprite  = LoadEditorSprite("UI/First");
        icons.traitOnEnterSprite      = LoadEditorSprite("UI/Enter");
        icons.traitRevengeSprite      = LoadEditorSprite("UI/Reverge");
        icons.traitDeathrattleSprite  = LoadEditorSprite("UI/Leave");
        icons.traitActiveExitSprite   = LoadEditorSprite("UI/Exit");
        icons.traitDiscardSprite      = LoadEditorSprite("UI/Discard");
        icons.traitAttachSprite       = LoadEditorSprite("UI/Attach");
        icons.statusShieldSprite      = LoadEditorSprite("Icons/Buffs/Shield");
        icons.statusBuffSprite        = LoadEditorSprite("Icons/Buffs/Buff");
        icons.statusDebuffSprite      = LoadEditorSprite("Icons/Buffs/DeBuff");

        // ── 拖入 Sprite 数组（对齐 2D Card00_New_2D：卡框按费用 6 张、前缀底图按前缀 5 张 + 通用 + 卡背）──
        display.costFrameSprites = new Sprite[]
        {
            LoadEditorSprite("Cards/SummonCard_0"), LoadEditorSprite("Cards/SummonCard_1"),
            LoadEditorSprite("Cards/SummonCard_2"), LoadEditorSprite("Cards/SummonCard_3"),
            LoadEditorSprite("Cards/SummonCard_4"), LoadEditorSprite("Cards/SummonCard_5"),
        };
        display.prefixArtSprites = new Sprite[]
        {
            LoadEditorSprite("Cards/PrefixArtBG/Psychic"), LoadEditorSprite("Cards/PrefixArtBG/Abyss"),
            LoadEditorSprite("Cards/PrefixArtBG/Mech"),    LoadEditorSprite("Cards/PrefixArtBG/Blood"),
            LoadEditorSprite("Cards/PrefixArtBG/Scroll"),
        };
        display.defaultPrefixArtSprite = LoadEditorSprite("Cards/PrefixArtBG/Common");
        display.cardBackSprite = LoadEditorSprite("Cards/Back");

        // ── 接线卡面三层（预览字段由 CardDisplay3D.OnValidate 在预制体里直接拖入显示）──
        display.frameSR = frameSR;
        display.prefixBgSR = prefixSR;
        display.cardArtSR = artSR;

        // ── 保存预制体 ──
        string dir = System.IO.Path.GetDirectoryName(PrefabPath);
        if (!AssetDatabase.IsValidFolder(dir))
        {
            string parent = System.IO.Path.GetDirectoryName(dir).Replace('\\', '/');
            string folder = System.IO.Path.GetFileName(dir);
            AssetDatabase.CreateFolder(parent, folder);
        }
        PrefabUtility.SaveAsPrefabAsset(cardRoot, PrefabPath);
        Object.DestroyImmediate(cardRoot);

        AssetDatabase.SaveAssets();
        Debug.Log($"[Card3DNew] 预制体已生成: {PrefabPath}（卡框/前缀背景/卡图三层 SpriteRenderer 可手调比例 + 文字/图标按 2D 布局，三排图标运行时填充）");
    }

    /// <summary>编辑器下从 Art/Sprites/ 相对路径加载 Sprite（供图标/预览字段填充）。</summary>
    static Sprite LoadEditorSprite(string relativePath)
        => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Sprites/" + relativePath + ".png");

    /// <summary>创建卡面 SpriteRenderer（identity 朝向，居中，z 定，比例用传入值——预制体里可手调，运行时不重算）。</summary>
    static SpriteRenderer CreateFaceSR(GameObject parent, string name, float z, Vector3 scale)
    {
        var go = new GameObject(name, typeof(SpriteRenderer));
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, z);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = scale;
        return go.GetComponent<SpriteRenderer>();
    }

    /// <summary>按目标世界尺寸反算 SpriteRenderer localScale（拉伸铺满目标矩形；找不到贴图回退 scale=1 自然尺寸，用户再手调）。</summary>
    static Vector3 FitScale(Sprite s, float targetW, float targetH)
    {
        if (s == null) return Vector3.one;
        return new Vector3(targetW / Mathf.Max(0.001f, s.bounds.size.x),
                           targetH / Mathf.Max(0.001f, s.bounds.size.y), 1f);
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
