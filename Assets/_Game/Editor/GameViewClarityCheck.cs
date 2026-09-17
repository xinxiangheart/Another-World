using System.Text;
using UnityEditor;
using UnityEngine;

namespace AnotherWorld.EditorTools
{
    /// <summary>
    /// Game 视图清晰度诊断 / 修复（编辑器侧，2026-09-17 新增）。
    ///
    /// 现象：**统一放大幅度下，Game 视图明显比 Scene 视图糊。**
    ///
    /// 原因：Scene 视图把 UI 直接按最终屏幕像素栅格化，中间没有任何缓冲；
    /// Game 视图则先把游戏渲染进一个「逻辑尺寸」的缓冲（本机实测 992×558），
    /// 再按 pixelsPerPoint（150% 显示缩放 → 1.5）**放大**铺满窗口 ——
    /// 于是文字、图标、卡图整幅画面一起被重采样糊一遍。
    /// 逐像素实测（同一处「随机匹配」）：Game 72×16px、笔画暗像素均值亮度 144；
    /// Scene 73×17px、均值 106 —— 尺寸几乎一样、笔画却浅一大截，就是被放大过的证据。
    ///
    /// 开关：Game 视图工具栏 Aspect 下拉旁那个方块按钮 = Low Resolution Aspect Ratios，
    /// 它开着时 Unity **忽略 DPI**，把渲染分辨率压到视图的逻辑尺寸而不是物理尺寸。
    /// 它的状态存在 Game 视图窗口的序列化字段 m_LowResolutionForAspectRatios 里
    /// —— 所以本工具用 SerializedObject 直接读写它，不需要反射。
    ///
    /// 菜单：
    ///   Tools/设置/Game 视图清晰度诊断（只读）
    ///   Tools/设置/Game 视图：关闭低分辨率渲染
    ///
    /// 注意：这只影响**编辑器预览**。构建版按原生分辨率跑，走的本来就是 Scene 视图那条路。
    /// </summary>
    public static class GameViewClarityCheck
    {
        const string PropTargetSize = "m_TargetSize";
        const string PropLowRes     = "m_LowResolutionForAspectRatios";
        const string PropZoomScale  = "m_ZoomArea.m_Scale";

        [MenuItem("Tools/设置/Game 视图清晰度诊断（只读）", false, 40)]
        public static void Diagnose()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Game 视图清晰度诊断]");
            sb.AppendLine("  pixelsPerPoint（Windows 显示缩放）= " + EditorGUIUtility.pixelsPerPoint.ToString("0.##"));
            sb.AppendLine("  Screen.width x height（游戏看到的渲染尺寸）= " + Screen.width + " x " + Screen.height);

            var gv = FindGameView();
            if (gv == null)
            {
                sb.AppendLine("  没找到 Game 视图窗口：先打开 Game 视图（Window > General > Game）再点本菜单。");
                Debug.Log(sb.ToString());
                return;
            }

            float pp = EditorGUIUtility.pixelsPerPoint;
            float winW = gv.position.width  * pp;
            float winH = gv.position.height * pp;

            var so = new SerializedObject(gv);
            var sizeProp  = so.FindProperty(PropTargetSize);
            var scaleProp = so.FindProperty(PropZoomScale);
            var lowProp   = so.FindProperty(PropLowRes);

            float renderW = sizeProp != null ? sizeProp.vector2Value.x : Screen.width;
            float ratio = winW > 1f ? renderW / winW : 1f;

            sb.AppendLine("  Game 视图窗口 = " + Mathf.RoundToInt(gv.position.width) + " x " + Mathf.RoundToInt(gv.position.height) + " 点"
                          + "  →  " + Mathf.RoundToInt(winW) + " x " + Mathf.RoundToInt(winH) + " 物理像素");
            if (sizeProp != null)
                sb.AppendLine("  渲染目标 m_TargetSize = " + sizeProp.vector2Value.x.ToString("0") + " x " + sizeProp.vector2Value.y.ToString("0"));
            if (scaleProp != null)
                sb.AppendLine("  显示缩放 m_ZoomArea.m_Scale = " + scaleProp.vector2Value.x.ToString("0.##"));

            if (lowProp != null && lowProp.isArray)
            {
                int on = 0;
                for (int i = 0; i < lowProp.arraySize; i++)
                    if (lowProp.GetArrayElementAtIndex(i).boolValue) on++;
                sb.AppendLine("  Low Resolution Aspect Ratios 处于开启状态的档位 = " + on + " / " + lowProp.arraySize);
            }

            if (ratio < 0.95f)
                sb.AppendLine("  [警告] 游戏只渲染到窗口物理像素的 " + (ratio * 100f).ToString("0") + "% —— 先渲染再放大，必然发虚（Scene 视图没有这道缓冲，所以它清晰）。"
                              + "\n         处理：点「Tools/设置/Game 视图：关闭低分辨率渲染」，或把分辨率下拉改成 Fixed Resolution 1920x1080。");
            else
                sb.AppendLine("  [正常] 渲染尺寸 ≈ 窗口物理像素，没有中间放大 —— 已经是 Scene 视图那种清晰度。");

            Debug.Log(sb.ToString());
        }

        [MenuItem("Tools/设置/Game 视图：关闭低分辨率渲染", false, 41)]
        public static void FixLowRes()
        {
            var gv = FindGameView();
            if (gv == null)
            {
                Debug.LogWarning("[Game 视图清晰度] 没找到 Game 视图窗口，无法修改。先打开 Game 视图再点本菜单。");
                return;
            }

            var so = new SerializedObject(gv);
            var prop = so.FindProperty(PropLowRes);
            if (prop == null || !prop.isArray)
            {
                Debug.LogWarning("[Game 视图清晰度] 当前 Unity 版本没有 " + PropLowRes + " 字段：请手动点 Game 视图工具栏 Aspect 下拉旁那个方块按钮。");
                return;
            }

            so.Update();
            int changed = 0;
            for (int i = 0; i < prop.arraySize; i++)
            {
                var e = prop.GetArrayElementAtIndex(i);
                if (e.boolValue) { e.boolValue = false; changed++; }
            }
            if (changed > 0) { so.ApplyModifiedProperties(); gv.Repaint(); }

            Debug.Log("[Game 视图清晰度] 已关闭 " + changed + " 个档位的 Low Resolution Aspect Ratios（原本就是关的则为 0）。\n"
                      + "若画面没有立刻变化：点一下 Game 视图窗口让它重绘（或按 Ctrl+P 重进 Play 模式），再点一次「诊断」核对渲染尺寸。");
        }

        /// <summary>编辑器里找 Game 视图窗口：GameView 是内部类型，按类型名匹配即可（不用反射私有成员）。</summary>
        static EditorWindow FindGameView()
        {
            var wins = Resources.FindObjectsOfTypeAll<EditorWindow>();
            for (int i = 0; i < wins.Length; i++)
            {
                var w = wins[i];
                if (w != null && w.GetType().Name == "GameView") return w;
            }
            return null;
        }
    }
}