using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AnotherWorld.EditorTools
{
    /// <summary>
    /// 贴图导入设置守卫。
    ///
    /// 判据只有一条：**屏幕像素 &lt; 源像素（缩小显示）→ 必须预过滤（mipmap + 双线性）；
    /// 屏幕像素 &gt; 源像素（放大显示）→ Point 更锐，不能加 mipmap。**
    ///
    /// 背景：卡图源图 1152×1536，实际只画在 ~90×118 的卡面上（缩小 12.8~18 倍），
    /// 图标 511×511 画在 ~21px（缩小 24 倍）。此前这批贴图是 filterMode=Point + 无 mipmap，
    /// 缩小采样时只取极少数纹素 → 出现「马赛克 / 抖动」。归档在 Art/Old 的旧图当年是
    /// 双线性 + mipmap，所以在缩略图里反而更清楚，这就是对照组。
    ///
    /// 自动化：AssetPostprocessor.OnPreprocessTexture —— 以后新导入的贴图自动套用同一套设置。
    /// 手动：菜单 Tools/设置/贴图导入设置体检 &amp; 修复（可随时重跑，幂等）。
    /// </summary>
    public class TextureImportSettingsGuard : AssetPostprocessor
    {
        /// <summary>宽高都 ≥ 这个值才认为「会被缩小显示」（把 60×40 / 128×32 这类小控件排除掉）。</summary>
        public const int MinSizeForMipmaps = 128;

        public const int AnisoLevel = 4;

        /// <summary>被放大显示的小控件：必须保持 Point + 无 mipmap，白名单硬保护。</summary>
        static readonly HashSet<string> PointWhitelist = new HashSet<string>
        {
            "Assets/_Game/Resources/UI/OverUI.png",
            "Assets/_Game/Resources/UI/GetUI.png",
            "Assets/_Game/Resources/UI/EyeUI.png",
            "Assets/_Game/Resources/UI/PlayerEnergyUI.png",
            "Assets/_Game/Resources/UI/PlayerHealthUI.png",
            "Assets/_Game/Resources/UI/StartUI.png",
        };

        /// <summary>卡图是 alpha-test 裁切（CardCutout.shader / CardFaceSprite.shader 里 clip(a - cutoff)），
        /// mipmap 要保留 alpha 覆盖，否则缩小后卡牌边缘会「化开」。</summary>
        const string CardFolderToken = "/Resources/Cards/";

        /// <summary>必须**原样 1:1 采样、禁止 Unity 缩放**的贴图。
        /// 本项目的 UI 贴图是自己按「屏幕 px x 3」出图的，RawImage 的 sizeDelta = 贴图尺寸 / 3 —— 尺寸被改就全错。
        /// 现状：右下角入口条 LobbyCornerPlate_*（954x414）在 2026-09-26 新增时被默认的 nPOTScale=ToNearest
        /// 吸成 1024x512，连 RectTransform 一起算成了 341x171；这条就是那次补的。
        /// **注意**：lobby-ui-v1 里**旧**那批仍是 ToNearest（1034x445 -> 1024x512，纵向拉伸约 15%），
        /// 那是等用户点头的历史账，**不要**顺手扩到这里来。</summary>
        /// <summary>整套 UI 件：出图脚本按「屏幕 px x 3」出、场景按「贴图 / 3」摆 —— 必须原样 1:1，禁止 Unity 缩放。
        /// 2026-09-27 加 battle-mode-v1：卡 900x1260 是 RawImage 的贴图，被默认的 ToNearest 吸成 512x1024，
        /// 3:4.2 的画面被压成 1:2 再拉回 300x420 的卡框 —— 卡面会横向拉宽。</summary>
        /// <summary>2026-09-27 又加 Icon_InvitePlus（好友行那一格，78x48 -> 234x144 不是 2 的幂）与 invite-v1：「收到邀请」小窗底板 Invite_Plate.png 是 1260x432（屏幕 420x144，
        /// RawImage 整块拉伸铺满）—— 被吸成 1024x512（或 1024x256）就不是那条扁比例了，圆角与金线会跟着变形。</summary>
        /// <summary>2026-09-27 再加 LobbyFriendTab（好友详情左侧那四格的**三态板**，1068x360 不是 2 的幂）+ Icon_FriendTab*：
        /// 板被吸成 1024x512 就不是屏幕 340x104 那块扁比例了（圆角与等比内缩金线会跟着变形）。</summary>
        /// <summary>2026-09-27 再加 LobbyFriendRow（好友列表那一行，1376x112 不是 2 的幂）+ Icon_FriendAct*（拉黑 / 删除两枚徽章）：
        /// 行底板存成 1:1（不是全族那个 x3），被吸成 1024x128 就不是屏幕 1360x96 那条长矩形了。</summary>
        /// <summary>2026-09-27 再加 LobbyConfirmPlate（确认删除 / 拉黑的长条弹窗底板，760x200 = 3.8:1，不是 2 的幂）：
        /// 2026-09-27 又加 LobbyFriendAddInput（「添加好友」那口井的底板，1376x92 也是 1:1，既不是 2 的幂、高又小于 128 —— 两道门都得进）
        /// 与 Icon_FriendSearch（井右端那枚放大镜徽章，256 不吃 npot 那条，进 alpha 那条）。</summary>
        /// 也是存 1:1，被吸成 1024x256 就不是那条长比例了（圆角与等比内缩金线会跟着变形）。</summary>
        static readonly string[] NoNpotScaleFolders = { "/Art/Sprites/Generated/battle-mode-v1/", "/Art/Sprites/Generated/match-wait-v1/", "/Art/Sprites/Generated/match-confirm-v1/", "/Art/Sprites/Generated/battle-loading-v1/", "/Art/Sprites/Generated/invite-v1/" };

        public static bool NeedsNoNpotScale(string path)
        {
            string p = path.Replace('\\', '/');
            foreach (string f in NoNpotScaleFolders) if (p.Contains(f)) return true;
            if (!p.Contains("/Art/Sprites/Generated/lobby-ui-v1/")) return false;
            return Path.GetFileName(p).StartsWith("LobbyCornerPlate_") || Path.GetFileName(p).StartsWith("LobbyChip_") || Path.GetFileName(p).StartsWith("LobbyJoin") || Path.GetFileName(p).StartsWith("Icon_InvitePlus") || Path.GetFileName(p).StartsWith("LobbyFriendTab") || Path.GetFileName(p).StartsWith("LobbyFriendRow") || Path.GetFileName(p).StartsWith("LobbyConfirmPlate") || Path.GetFileName(p).StartsWith("LobbyFriendAddInput");
        }

        /// <summary>带硬 alpha 边（圆角 / 挖空）的 UI 件：导入要做 alpha 扩散，否则缩小后边缘发黑。</summary>
        public static bool NeedsAlphaIsTransparency(string path)
        {
            string p = path.Replace('\\', '/');
            foreach (string f in NoNpotScaleFolders) if (p.Contains(f)) return true;
            if (p.Contains("/Art/Sprites/Generated/lobby-ui-v1/") && (Path.GetFileName(p).StartsWith("LobbyCornerPlate_") || Path.GetFileName(p).StartsWith("LobbyChip_") || Path.GetFileName(p).StartsWith("LobbyJoin") || Path.GetFileName(p).StartsWith("Icon_InvitePlus") || Path.GetFileName(p).StartsWith("Icon_FriendPlus") || Path.GetFileName(p).StartsWith("LobbyFriendTab") || Path.GetFileName(p).StartsWith("Icon_FriendTab") || Path.GetFileName(p).StartsWith("Icon_FriendAct") || Path.GetFileName(p).StartsWith("LobbyFriendRow") || Path.GetFileName(p).StartsWith("LobbyConfirmPlate") || Path.GetFileName(p).StartsWith("LobbyFriendAddInput") || Path.GetFileName(p).StartsWith("Icon_FriendSearch"))) return true;
            return false;
        }

        void OnPreprocessTexture()
        {
            if (!IsCandidate(assetPath)) return;

            var importer = assetImporter as TextureImporter;
            if (importer == null) return;

            // 尺寸门只管「双线性 + mipmap」那一段（小控件不做缩小采样，加了只会糊）。
            // npot / alpha 是**与尺寸无关**的硬要求，永远要套 —— LobbyFriendRow 是 1376x112（高 112 < 128），
            // 2026-09-27 实测：它被这道门挡在 ApplyTo 外面，npot 与 alpha 两条全漏了（表格量出来 ToNearest / false）。
            bool sized = true;
            int w = 0, h = 0;
            try { importer.GetSourceTextureWidthAndHeight(out w, out h); }
            catch { sized = false; }
            if (sized && (w < MinSizeForMipmaps || h < MinSizeForMipmaps)) sized = false;

            if (sized) ApplyFilterPart(importer, assetPath);
            ApplyScalePart(importer, assetPath);
        }

        public static bool IsCandidate(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (!path.Replace('\\', '/').StartsWith("Assets/_Game/")) return false;
            if (path.Contains("/Art/Old/")) return false;
            if (PointWhitelist.Contains(path)) return false;

            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".tga";
        }

        public static bool NeedsFix(TextureImporter imp, string path, bool sized = true)
        {
            if (sized)
            {
                if (imp.filterMode != FilterMode.Bilinear) return true;
                if (!imp.mipmapEnabled) return true;
                if (imp.anisoLevel != AnisoLevel) return true;
                if (path.Contains(CardFolderToken) && !imp.mipMapsPreserveCoverage) return true;
            }
            if (NeedsNoNpotScale(path) && imp.npotScale != TextureImporterNPOTScale.None) return true;
            if (NeedsAlphaIsTransparency(path) && !imp.alphaIsTransparency) return true;
            return false;
        }

        /// <summary>只在导入前置阶段调用，不要在这里 SaveAndReimport。</summary>
        public static void ApplyTo(TextureImporter imp, string path)
        {
            ApplyFilterPart(imp, path);
            ApplyScalePart(imp, path);
        }

        /// <summary>「会被缩小显示」那一段：双线性 + mipmap（+ 卡图 alpha 覆盖）。小控件不做。</summary>
        public static void ApplyFilterPart(TextureImporter imp, string path)
        {
            imp.filterMode = FilterMode.Bilinear;
            imp.anisoLevel = AnisoLevel;

            if (!imp.mipmapEnabled)
            {
                imp.mipmapEnabled = true;
                imp.streamingMipmaps = false;   // 卡图是常驻资源，流式 mipmap 只会带来卡顿
            }

            if (path.Contains(CardFolderToken))
            {
                imp.mipMapsPreserveCoverage = true;
                imp.alphaTestReferenceValue = 0.5f;
            }
        }

        /// <summary>与尺寸无关的两条硬修正：npot 原样采样、alpha 扩散。小控件也必须做。</summary>
        public static void ApplyScalePart(TextureImporter imp, string path)
        {
            if (NeedsNoNpotScale(path)) imp.npotScale = TextureImporterNPOTScale.None;
            if (NeedsAlphaIsTransparency(path)) imp.alphaIsTransparency = true;
        }

        [MenuItem("Tools/设置/贴图导入设置体检 & 修复")]
        static void FixAll()
        {
            var files = new List<string>();
            foreach (var ext in new[] { "*.png", "*.jpg", "*.jpeg", "*.tga" })
                files.AddRange(Directory.GetFiles("Assets/_Game", ext, SearchOption.AllDirectories));

            int changed = 0, ok = 0, skippedSmall = 0, skippedWhite = 0, notTexture = 0;

            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < files.Count; i++)
                {
                    string path = files[i].Replace('\\', '/');
                    bool bar = i % 40 == 0;
                    if (bar) EditorUtility.DisplayProgressBar("贴图导入设置体检", path, (float)i / files.Count);

                    if (!IsCandidate(path)) { skippedWhite++; continue; }

                    var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (imp == null) { notTexture++; continue; }

                    int w, h;
                    try { imp.GetSourceTextureWidthAndHeight(out w, out h); }
                    catch { notTexture++; continue; }

                    bool sized = !(w < MinSizeForMipmaps || h < MinSizeForMipmaps);
                    if (!sized) skippedSmall++;      // 小控件跳过「双线性 + mipmap」，但下面那两条照做

                    if (!NeedsFix(imp, path, sized)) { ok++; continue; }

                    if (sized) ApplyFilterPart(imp, path);
                    ApplyScalePart(imp, path);
                    EditorUtility.SetDirty(imp);
                    imp.SaveAndReimport();
                    changed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
            Debug.Log($"[TextureImportSettingsGuard] 修正 {changed} 张，本来就正确 {ok} 张；"
                    + $"跳过（小图 / 放大显示）{skippedSmall + skippedWhite} 张，非贴图 {notTexture} 张。");
        }

        [MenuItem("Tools/设置/贴图导入设置体检报告（只读）")]
        static void Report()
        {
            var offenders = new List<string>();
            var files = new List<string>();
            foreach (var ext in new[] { "*.png", "*.jpg", "*.jpeg", "*.tga" })
                files.AddRange(Directory.GetFiles("Assets/_Game", ext, SearchOption.AllDirectories));

            for (int i = 0; i < files.Count; i++)
            {
                string path = files[i].Replace('\\', '/');
                if (!IsCandidate(path)) continue;
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;
                bool sized = true;
                int w = 0, h = 0;
                try { imp.GetSourceTextureWidthAndHeight(out w, out h); } catch { sized = false; }
                if (sized && (w < MinSizeForMipmaps || h < MinSizeForMipmaps)) sized = false;
                if (NeedsFix(imp, path, sized)) offenders.Add(path);
            }

            if (offenders.Count == 0) Debug.Log("[TextureImportSettingsGuard] 体检通过：所有「会被缩小」的贴图都是 双线性 + mipmap。");
            else Debug.LogWarning($"[TextureImportSettingsGuard] 还有 {offenders.Count} 张未修正：\n" + string.Join("\n", offenders));
        }
    }
}
