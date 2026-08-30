using UnityEngine;

/// <summary>
/// 全局 GPU 优化——启动时自动执行，无需挂载到场景。
/// 2026-08-30：不再降级画质。保持与编辑器一致的 Ultra（MSAA=2），仅保留 60fps 限帧 + 关 HDR。
/// </summary>
public static class GPUOptimizer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Apply()
    {
        // 1. 保持与编辑器一致的 Ultra 质量（lodBias 2.0 / aniso Forced / MSAA 2 / 阴影全开）
        //    不再 SetQualityLevel(2) 降级到 Medium，避免 Play 模式整体掉质、视觉与编辑不一致。
        QualitySettings.SetQualityLevel(5, true);

        // 2. 限 60fps（卡牌游戏不需要更高）。
        //    必须在 SetQualityLevel 之后设置——否则会被质量预设里的 vSyncCount 覆盖，导致限帧失效。
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;

        // 3. 确保 MSAA=2 抗锯齿，避免边缘锯齿（SetQualityLevel 已带，此处显式确认）。
        QualitySettings.antiAliasing = 2;

        // 4. 只关相机的 HDR（省显存；项目无后处理，不影响清晰度）。
        //    注意：不再设 cam.allowMSAA=false——它会取消上面的 MSAA=2，Play 模式仍会锯齿。
        var allCams = Object.FindObjectsOfType<Camera>();
        foreach (var cam in allCams)
        {
            cam.allowHDR = false;
        }

        // 5. 运行时确认（确认后可删）：应输出 quality=Ultra(5) msaa=2 vSync=0
        Debug.Log($"[GPUOptimizer] quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}({QualitySettings.GetQualityLevel()}) msaa={QualitySettings.antiAliasing} vSync={QualitySettings.vSyncCount} screen={Screen.width}x{Screen.height}");
    }
}
