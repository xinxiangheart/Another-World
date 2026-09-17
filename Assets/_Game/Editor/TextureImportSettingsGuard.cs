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

        void OnPreprocessTexture()
        {
            if (!IsCandidate(assetPath)) return;

            var importer = assetImporter as TextureImporter;
            if (importer == null) return;

            int w, h;
            try { importer.GetSourceTextureWidthAndHeight(out w, out h); }
            catch { return; }
            if (w < MinSizeForMipmaps || h < MinSizeForMipmaps) return;

            ApplyTo(importer, assetPath);
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

        public static bool NeedsFix(TextureImporter imp, string path)
        {
            if (imp.filterMode != FilterMode.Bilinear) return true;
            if (!imp.mipmapEnabled) return true;
            if (imp.anisoLevel != AnisoLevel) return true;
            if (path.Contains(CardFolderToken) && !imp.mipMapsPreserveCoverage) return true;
            return false;
        }

        /// <summary>只在导入前置阶段调用，不要在这里 SaveAndReimport。</summary>
        public static void ApplyTo(TextureImporter imp, string path)
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
                    if (w < MinSizeForMipmaps || h < MinSizeForMipmaps) { skippedSmall++; continue; }

                    if (!NeedsFix(imp, path)) { ok++; continue; }

                    ApplyTo(imp, path);
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
                int w, h;
                try { imp.GetSourceTextureWidthAndHeight(out w, out h); } catch { continue; }
                if (w < MinSizeForMipmaps || h < MinSizeForMipmaps) continue;
                if (NeedsFix(imp, path)) offenders.Add(path);
            }

            if (offenders.Count == 0) Debug.Log("[TextureImportSettingsGuard] 体检通过：所有「会被缩小」的贴图都是 双线性 + mipmap。");
            else Debug.LogWarning($"[TextureImportSettingsGuard] 还有 {offenders.Count} 张未修正：\n" + string.Join("\n", offenders));
        }
    }
}