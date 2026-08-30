using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// 把 2D 新卡预制体 Card00_New_2D 直接转换成 3D 预制体：
///   UI Image        → SpriteRenderer
///   TextMeshProUGUI → TextMeshPro 3D
///   保持父子层级；anchoredPosition → localPosition；sizeDelta → localScale
///   换算系数由 2D 卡(83.33×146.33) 与 3D 卡(0.9×1.6) 决定，读取 2D 实际值，不写死。
/// 3D 网格（0.9×1.6×0.03 薄盒）作为底板承载转换后的组件。
/// 菜单：Tools → 卡牌 → 生成新3D手牌预制体
/// </summary>
public static class Card3DNewPrefabBuilder
{
    const string Src2DPath = "Assets/_Game/Prefabs/Cards/Summon/Card00_New_2D.prefab";
    const string FBXPath = "Assets/_Game/Art/Models/Summon/Card00_New.fbx";
    const string FrontMatPath = "Assets/_Game/Art/Materials/card_new_front.mat";
    const string BackMatPath = "Assets/_Game/Art/Materials/card_new_back.mat";
    const string FontPath = "Assets/_Game/Fonts/NotoSerifCJKsc-Black SDF.asset";
    const string PrefabPath = "Assets/_Game/Prefabs/Cards/Summon/Card00_New_3D.prefab";

    // 2D 卡 83.33×146.33 → 3D 卡 0.9×1.6（换算系数由此决定，读取 2D 值不写死）
    const float CARD_W2D = 83.33f, CARD_H2D = 146.33f;
    const float CARD_W3D = 0.9f, CARD_H3D = 1.6f;

    [MenuItem("Tools/卡牌/生成新3D手牌预制体")]
    public static void CreatePrefab()
    {
        // ── 读取 2D 源 ──
        GameObject src2D = AssetDatabase.LoadAssetAtPath<GameObject>(Src2DPath);
        if (src2D == null) { Debug.LogError($"[Card3DNew] 找不到 2D 源: {Src2DPath}"); return; }
        CardDisplay2DNew d2 = src2D.GetComponent<CardDisplay2DNew>();
        if (d2 == null) { Debug.LogError("[Card3DNew] 2D 源无 CardDisplay2DNew"); return; }

        // ── 材质 ──
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
            Shader cutout = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Game/Art/Shaders/CardCutout.shader");
            if (cutout == null) { Debug.LogError("[Card3DNew] 找不到 shader: AnotherWorld/CardCutout"); return; }
            backMat = new Material(cutout) { name = "card_new_back" };
            Texture2D backTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Sprites/Cards/Back.png");
            if (backTex != null) backMat.SetTexture("_MainTex", backTex);
            AssetDatabase.CreateAsset(backMat, BackMatPath);
        }
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null) { Debug.LogError($"[Card3DNew] 找不到字体: {FontPath}"); return; }

        // ── 3D 底板（网格 + 脚本）──
        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FBXPath);
        if (fbx == null) { Debug.LogError($"[Card3DNew] 找不到模型: {FBXPath}"); return; }
        GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        root.name = "Card00_New_3D";
        root.transform.localScale = Vector3.one;
        root.transform.localRotation = Quaternion.identity;

        MeshRenderer mr = root.GetComponentInChildren<MeshRenderer>();
        if (mr == null) { Debug.LogError("[Card3DNew] 模型无 MeshRenderer"); Object.DestroyImmediate(root); return; }
        Material[] mats = new Material[Mathf.Max(2, mr.sharedMaterials.Length)];
        mats[0] = backMat;
        for (int i = 1; i < mats.Length; i++) mats[i] = frontMat;
        mr.sharedMaterials = mats;

        BoxCollider bc = root.GetComponent<BoxCollider>();
        if (bc == null) bc = root.AddComponent<BoxCollider>();
        bc.size = new Vector3(CARD_W3D, CARD_H3D, 0.06f);

        root.AddComponent<Card3DInstance>();
        CardDisplay3D display = root.AddComponent<CardDisplay3D>();
        CardIcons3D icons = root.AddComponent<CardIcons3D>();
        root.AddComponent<Card3DHover>();
        root.AddComponent<DamageSourceMarker>();

        // ── 正反面容器（2D 层级）──
        Transform frontFace = CreateStructuralNode(root, "FrontFace");
        Transform backFace = CreateStructuralNode(root, "BackFace");

        // ── CostFrameBase（2D 拉伸铺满卡 → 3D 铺满 0.9×1.6）──
        SpriteRenderer costFrameSR = CreateFaceSprite(frontFace.gameObject, "CostFrameBase",
            ConvPos(d2.costFrame.rectTransform, 0.02f), Quaternion.identity);
        costFrameSR.transform.localScale = ConvScale(d2.costFrame.rectTransform);

        // ── ArtworkArea（2D 64×84，位置 (0,1.8)，不铺满）──
        Transform artwork = FindChild(d2.frontFace.transform, "ArtworkArea");
        Transform artwork3D = CreateStructuralNode(frontFace.gameObject, "ArtworkArea");
        if (artwork != null) artwork3D.localPosition = ConvPos(artwork.GetComponent<RectTransform>(), 0.03f);

        // ── PrefixArtBG（2D 在 ArtworkArea 内，64×84）──
        SpriteRenderer prefixArtBGSR = CreateFaceSprite(artwork3D.gameObject, "PrefixArtBG",
            ConvPos(d2.prefixArtBG.rectTransform, 0.04f), Quaternion.identity);
        prefixArtBGSR.transform.localScale = ConvScale(d2.prefixArtBG.rectTransform);

        // ── CardArt（2D 拉伸铺满 ArtworkArea）──
        SpriteRenderer cardArtSR = CreateFaceSprite(artwork3D.gameObject, "CardArt",
            ConvPos(d2.cardArt.rectTransform, 0.05f), Quaternion.identity);
        cardArtSR.transform.localScale = ConvScale(d2.cardArt.rectTransform);

        // ── 4 个文字（2D TMPUGUI → 3D TMP，位置/字号读 2D）──
        TextMeshPro nameT = CreateText3D(frontFace, d2.cardNameText, font, ConvPos(d2.cardNameText.rectTransform, 0.1f));
        TextMeshPro costT = CreateText3D(frontFace, d2.cardCostText, font, ConvPos(d2.cardCostText.rectTransform, 0.1f));
        TextMeshPro atkT  = CreateText3D(frontFace, d2.cardAttackText, font, ConvPos(d2.cardAttackText.rectTransform, 0.1f));
        TextMeshPro hpT   = CreateText3D(frontFace, d2.cardHealthText, font, ConvPos(d2.cardHealthText.rectTransform, 0.1f));

        // ── 4 个角标图标（2D Image → 3D SR）──
        SpriteRenderer costIcon   = CreateFaceSprite(frontFace.gameObject, "CostIcon",   ConvPos(d2.costIcon.rectTransform, 0.06f), Quaternion.identity);
        SpriteRenderer typeIcon   = CreateFaceSprite(frontFace.gameObject, "TypeIcon",   ConvPos(d2.typeIcon.rectTransform, 0.06f), Quaternion.identity);
        SpriteRenderer healthIcon = CreateFaceSprite(frontFace.gameObject, "HealthIcon", ConvPos(d2.healthIcon.rectTransform, 0.06f), Quaternion.identity);
        SpriteRenderer attackIcon = CreateFaceSprite(frontFace.gameObject, "AttackIcon", ConvPos(d2.attackIcon.rectTransform, 0.06f), Quaternion.identity);

        // ── 三排容器（位置/间距读 2D HLG）──
        Transform prefixRow = CreateRow(frontFace, d2.prefixIconsArea, "PrefixIconsArea");
        Transform traitRow  = CreateRow(frontFace, d2.traitIconsArea,  "TraitIconsArea");
        Transform statusRow = CreateRow(frontFace, d2.statusIconsArea, "StatusIconsArea");

        // ── BackFace / CardBackImage（2D 拉伸铺满 → 3D 铺满背面）──
        SpriteRenderer cardBackSR = CreateFaceSprite(backFace.gameObject, "CardBackImage",
            ConvPos(d2.cardBackImage.rectTransform, -0.1f), Quaternion.Euler(0f, 180f, 0f));
        cardBackSR.transform.localScale = ConvScale(d2.cardBackImage.rectTransform);

        // ── 接线显示脚本 ──
        display.nameText = nameT;
        display.costText = costT;
        display.attackText = atkT;
        display.healthText = hpT;
        display.costFrameSR = costFrameSR;
        display.prefixArtBGSR = prefixArtBGSR;
        display.cardArtSR = cardArtSR;
        display.cardBackSR = cardBackSR;

        icons.costIcon = costIcon;
        icons.typeIcon = typeIcon;
        icons.healthIcon = healthIcon;
        icons.attackIcon = attackIcon;
        icons.prefixIconsRow = prefixRow;
        icons.traitIconsRow = traitRow;
        icons.statusIconsRow = statusRow;

        // ── 保存 ──
        string dir = System.IO.Path.GetDirectoryName(PrefabPath);
        if (!AssetDatabase.IsValidFolder(dir))
        {
            string parent = System.IO.Path.GetDirectoryName(dir).Replace('\\', '/');
            string folder = System.IO.Path.GetFileName(dir);
            AssetDatabase.CreateFolder(parent, folder);
        }
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        SyncAllSummonPrefab3D();
        AssetDatabase.SaveAssets();
        Debug.Log($"[Card3DNew] 已从 2D 转换生成: {PrefabPath}");
    }

    // ═══════════════ 转换辅助 ═══════════════

    /// <summary>2D anchoredPosition → 3D localPosition（z 取定值）</summary>
    static Vector3 ConvPos(RectTransform rt, float z)
        => new Vector3(rt.anchoredPosition.x / CARD_W2D * CARD_W3D, rt.anchoredPosition.y / CARD_H2D * CARD_H3D, z);

    /// <summary>2D rect 实际尺寸 → 3D localScale（1×1 sprite 占该区域，用户可手调）</summary>
    static Vector3 ConvScale(RectTransform rt)
        => new Vector3(rt.rect.width / CARD_W2D * CARD_W3D, rt.rect.height / CARD_H2D * CARD_H3D, 1f);

    /// <summary>创建 3D TMP 文字：位置/字号读 2D TMPUGUI，加黑描边（运行时由显示脚本加，避免编辑器泄漏）</summary>
    static TextMeshPro CreateText3D(Transform parent, TMP_Text src, TMP_FontAsset font, Vector3 pos)
    {
        var go = new GameObject(src.name, typeof(RectTransform), typeof(TextMeshPro));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.identity;
        TextMeshPro tmp = go.GetComponent<TextMeshPro>();
        tmp.font = font;
        tmp.fontSize = src.fontSize;
        tmp.color = src.color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.text = src.text;
        return tmp;
    }

    /// <summary>创建三排容器：位置读 2D，间距存到容器名对应字段（运行时参考静态图标 + 此间距）</summary>
    static Transform CreateRow(Transform parent, RectTransform src, string name)
    {
        Transform row = CreateStructuralNode(parent.gameObject, name);
        row.localPosition = ConvPos(src, 0.06f);
        return row;
    }

    /// <summary>创建结构容器（Transform）</summary>
    static Transform CreateStructuralNode(GameObject parent, string name)
    {
        var go = new GameObject(name, typeof(Transform));
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    /// <summary>创建 SpriteRenderer（位置/朝向给定）</summary>
    static SpriteRenderer CreateFaceSprite(GameObject parent, string name, Vector3 pos, Quaternion rot)
    {
        var go = new GameObject(name, typeof(SpriteRenderer));
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = Vector3.one;
        return go.GetComponent<SpriteRenderer>();
    }

    /// <summary>在父级下按名找子物体（2D 的 ArtworkArea 等）</summary>
    static Transform FindChild(Transform parent, string name)
    {
        if (parent == null) return null;
        foreach (Transform t in parent)
            if (t.name == name) return t;
        return null;
    }

    /// <summary>把全部召唤物 CardData 的 prefab3D 引用指向新预制体根（重新生成时 fileID 会变，必须回写）</summary>
    static void SyncAllSummonPrefab3D()
    {
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefabAsset == null) { Debug.LogWarning("[Card3DNew] 找不到生成的预制体，跳过引用同步"); return; }
        Card3DInstance holder = prefabAsset.GetComponentInChildren<Card3DInstance>();
        if (holder == null) { Debug.LogWarning("[Card3DNew] 预制体无 Card3DInstance，跳过引用同步"); return; }

        int updated = 0;
        foreach (string folder in new[] { "CardData", "ChosenOneData" })
        {
            foreach (CardData data in Resources.LoadAll<CardData>(folder))
            {
                if (data.cardType != CardType.Summon) continue;
                var so = new SerializedObject(data);
                var prop = so.FindProperty("prefab3D");
                if (prop.objectReferenceValue != holder.gameObject)
                {
                    prop.objectReferenceValue = holder.gameObject;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    updated++;
                }
            }
        }
        Debug.Log($"[Card3DNew] 已同步 {updated} 个召唤物 prefab3D 引用 → {PrefabPath}");
    }
}
