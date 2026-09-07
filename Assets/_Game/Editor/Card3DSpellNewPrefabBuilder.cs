using UnityEngine;
using UnityEditor;
using TMPro;

/// <summary>
/// 一键生成新 3D 法术手牌预制体 SpellCard00_New_3D（复用 3D 召唤物 Card00_New_3D 的模型/材质/层级，独立于旧资产）。
/// 保留：能量(CostIcon+CostText)、卡框(CardFrame=SpellCard_0..5 法术框)、卡背、原画(CardArt)、效果文本(EffectText TMP3D 中央偏下)。
/// 剔除：攻击/生命/类别 UI、三排图标、CardIcons3D。
/// 层级/朝向/Composite(_BgTex/_BorderTex/_ArtTex)/MPB 与 Card3DNewPrefabBuilder 同构：
///   CardRoot(脚本+BoxCollider) → ModelRoot(0 子物体, fbx 网格) + UIComponents(文字/图标/卡面三层)。
/// 挂载：Card3DInstance / CardDisplay3DSpell / Card3DHover / DamageSourceMarker / BoxCollider。
/// 菜单：Tools → 卡牌 → 生成新3D法术手牌预制体
/// </summary>
public static class Card3DSpellNewPrefabBuilder
{
    const string FBXPath = "Assets/_Game/Art/Models/Summon/Card00_New.fbx";
    const string FrontMatPath = "Assets/_Game/Art/Materials/card_new_front.mat";
    const string BackMatPath = "Assets/_Game/Art/Materials/card_new_back.mat";
    const string FontPath = "Assets/_Game/Fonts/NotoSerifCJKsc-Black SDF.asset";
    const string PrefabPath = "Assets/_Game/Prefabs/Cards/Spell/SpellCard00_New_3D.prefab";

    static readonly Vector3 TextScale = new Vector3(1.4814816f, 1f, 0.8333331f);
    const float TextZ = 0.16f;
    const float IconZ = 0.14f;
    const float FaceArtZ = 0.13f, FacePrefixZ = 0.12f, FaceFrameZ = 0.11f;
    static readonly Vector2 FrameSize = new Vector2(0.9f, 1.6f);
    static readonly Vector2 ArtAreaSize = new Vector2(0.69f, 0.92f);

    // 2D 法术布局换算（×0.0108/×0.01093）
    static readonly Vector2 NamePos   = new Vector2(0f,    0.66f);  // 卡名顶部
    static readonly Vector2 CostPos   = new Vector2(-0.40f, 0.74f); // 能量左上(部分超边)
    static readonly Vector2 EffectPos = new Vector2(0f,   -0.05f);  // 效果文本 中央偏下

    [MenuItem("Tools/卡牌/生成新3D法术手牌预制体")]
    public static void CreateSpellPrefab()
    {
        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FBXPath);
        if (fbx == null) { Debug.LogError($"[Card3DSpell] 找不到模型: {FBXPath}"); return; }
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null) { Debug.LogError($"[Card3DSpell] 找不到字体: {FontPath}"); return; }
        Material frontMat = AssetDatabase.LoadAssetAtPath<Material>(FrontMatPath);
        Material backMat = AssetDatabase.LoadAssetAtPath<Material>(BackMatPath);
        Material faceSpriteMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/card_new_front_sprite.mat");
        if (frontMat == null || backMat == null) { Debug.LogError("[Card3DSpell] 卡面/卡背材质缺失（先生成 3D 召唤物预制体或检查 Art/Materials）"); return; }

        // ── CardRoot ──
        GameObject cardRoot = new GameObject("CardRoot");
        cardRoot.transform.localScale = new Vector3(1.4f, 1.4f, 1f);
        cardRoot.transform.localPosition = new Vector3(0f, -5.55f, 0f);
        cardRoot.transform.localRotation = Quaternion.identity;

        // ModelRoot（必须第 0 子物体）：模型网格，独立缩放
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
        MeshRenderer mr = fbxInstance.GetComponentInChildren<MeshRenderer>();
        if (mr == null) { Debug.LogError("[Card3DSpell] 模型无 MeshRenderer"); Object.DestroyImmediate(cardRoot); return; }
        Material[] mats = new Material[Mathf.Max(2, mr.sharedMaterials.Length)];
        mats[0] = frontMat;
        for (int i = 1; i < mats.Length; i++) mats[i] = backMat;
        mr.sharedMaterials = mats;

        // UIComponents（文字/能量图标/效果文本/卡面三层）
        GameObject uiRoot = new GameObject("UIComponents");
        uiRoot.transform.SetParent(cardRoot.transform, false);
        uiRoot.transform.localPosition = Vector3.zero;
        uiRoot.transform.localRotation = Quaternion.identity;
        uiRoot.transform.localScale = Vector3.one;

        // ── 脚本 ──
        cardRoot.AddComponent<Card3DInstance>();
        CardDisplay3DSpell display = cardRoot.AddComponent<CardDisplay3DSpell>();
        cardRoot.AddComponent<Card3DHover>();
        cardRoot.AddComponent<DamageSourceMarker>();
        BoxCollider bc = cardRoot.GetComponent<BoxCollider>();
        if (bc == null) bc = cardRoot.AddComponent<BoxCollider>();
        bc.size = new Vector3(1.1f, 1.9f, 0.03f);
        bc.center = Vector3.zero;

        // ── 文字 / 能量图标（法术版：无攻血/类别/三排）──
        TextMeshPro nameT   = CreateTextChild(uiRoot, "NameText",   font, "卡名", 0.72f, 0.92f, NamePos);
        TextMeshPro costT   = CreateTextChild(uiRoot, "CostText",   font, "0",    0.72f, 0.92f, CostPos);
        TextMeshPro effectT = CreateTextChild(uiRoot, "EffectText", font, "",     0.5f,  0.72f, EffectPos);
        effectT.enableWordWrapping = true;
        SpriteRenderer costIcon = CreateIconChild(uiRoot, "CostIcon", CostPos);

        // ── 卡面三层 SpriteRenderer（框=法术框；原画/底图占 ArtworkArea）──
        SpriteRenderer frameSR = CreateFaceSR(uiRoot, "CardFrame", FaceFrameZ,
            FitScale(LoadEditorSprite("Cards/Back And Front/Spell/SpellCard_0"), FrameSize.x, FrameSize.y));
        SpriteRenderer prefixSR = CreateFaceSR(uiRoot, "PrefixBg", FacePrefixZ,
            FitScale(LoadEditorSprite("Cards/PrefixArtBG/Abyss"), ArtAreaSize.x, ArtAreaSize.y));
        SpriteRenderer artSR = CreateFaceSR(uiRoot, "CardArt", FaceArtZ,
            FitScale(LoadEditorSprite("Cards/Spell/Normal/1/SpellCard_{02101}"), ArtAreaSize.x, ArtAreaSize.y));
        if (faceSpriteMat != null) { frameSR.sharedMaterial = faceSpriteMat; prefixSR.sharedMaterial = faceSpriteMat; artSR.sharedMaterial = faceSpriteMat; }

        // ── 接线显示脚本 ──
        display.nameText = nameT;
        display.costText = costT;
        display.effectText = effectT;
        display.prefixText = null;
        display.attackText = null;
        display.healthText = null;
        display.costIcon = costIcon;
        display.energyIconSprite = LoadEditorSprite("UI/Cost");
        display.frameSR = frameSR;
        display.prefixBgSR = prefixSR;
        display.cardArtSR = artSR;
        // 拖入 Sprite 数组：卡框=法术框 SpellCard_0..5；前缀底图 5 + 通用；卡背
        display.costFrameSprites = new Sprite[]
        {
            LoadEditorSprite("Cards/Back And Front/Spell/SpellCard_0"), LoadEditorSprite("Cards/Back And Front/Spell/SpellCard_1"),
            LoadEditorSprite("Cards/Back And Front/Spell/SpellCard_2"), LoadEditorSprite("Cards/Back And Front/Spell/SpellCard_3"),
            LoadEditorSprite("Cards/Back And Front/Spell/SpellCard_4"), LoadEditorSprite("Cards/Back And Front/Spell/SpellCard_5"),
        };
        display.prefixArtSprites = new Sprite[]
        {
            LoadEditorSprite("Cards/PrefixArtBG/Psychic"), LoadEditorSprite("Cards/PrefixArtBG/Abyss"),
            LoadEditorSprite("Cards/PrefixArtBG/Mech"),    LoadEditorSprite("Cards/PrefixArtBG/Blood"),
            LoadEditorSprite("Cards/PrefixArtBG/Scroll"),
        };
        display.defaultPrefixArtSprite = LoadEditorSprite("Cards/PrefixArtBG/Common");
        display.cardBackSprite = LoadEditorSprite("Cards/Back");

        // ── 保存 ──
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
        Debug.Log($"[Card3DSpell] 预制体已生成: {PrefabPath}（卡框/前缀背景/卡图三层可手调比例；EffectText 位置可在场景微调）");
    }

    static Sprite LoadEditorSprite(string relativePath)
        => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Resources/" + relativePath + ".png");

    static SpriteRenderer CreateFaceSR(GameObject parent, string name, float z, Vector3 scale)
    {
        var go = new GameObject(name, typeof(SpriteRenderer));
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, z);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = scale;
        return go.GetComponent<SpriteRenderer>();
    }

    static Vector3 FitScale(Sprite s, float targetW, float targetH)
    {
        if (s == null) return Vector3.one;
        return new Vector3(targetW / Mathf.Max(0.001f, s.bounds.size.x),
                           targetH / Mathf.Max(0.001f, s.bounds.size.y), 1f);
    }

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
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.text = content;
        tmp.rectTransform.sizeDelta = new Vector2(0.5f, 0.5f);
        return tmp;
    }

    static SpriteRenderer CreateIconChild(GameObject parent, string name, Vector2 pos)
    {
        var go = new GameObject(name, typeof(SpriteRenderer));
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = new Vector3(pos.x, pos.y, IconZ);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.GetComponent<SpriteRenderer>();
    }
}
