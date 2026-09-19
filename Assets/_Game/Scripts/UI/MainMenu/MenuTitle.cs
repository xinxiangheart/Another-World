using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MenuTitle — 开局场景（Welcome）标题「Another World」的序列帧播放器，2026-09-19。
///
/// 标题本身是场景里摆好的真实物体（MenuTitle 下 6 层 Image：Shadow / Outline / Fill /
/// Another / PortalGlow / Portal），位置、大小、缩放直接在 Inspector 里调，本脚本不参与布局。
/// 它只做一件事：按 frameFps 循环播放 o 里传送门的序列帧（不是代码旋转）。
/// 换序列帧 / 改速度 / 换贴图都在 Inspector 上做。
/// </summary>
public class MenuTitle : MonoBehaviour
{
    [Tooltip("o 里的传送门，最上面那一层 Image")]
    [SerializeField] Image portalImage;

    [Tooltip("传送门序列帧，按顺序循环播放；首尾已做成无缝")]
    [SerializeField] Sprite[] portalFrames;

    [Tooltip("播放速度（帧/秒）：12 帧 / 8fps = 1.5 秒一轮")]
    [SerializeField] float frameFps = 8f;

    float _t;
    int _frame = -1;

    void OnEnable()
    {
        _t = 0f;
        _frame = -1;
        Apply(0);
    }

    void Update()
    {
        if (portalImage == null || portalFrames == null || portalFrames.Length == 0) return;
        _t += Time.unscaledDeltaTime;                        // 主菜单即使暂停也照播
        Apply(Mathf.FloorToInt(_t * frameFps));
    }

    void Apply(int index)
    {
        int n = portalFrames == null ? 0 : portalFrames.Length;
        if (n == 0) return;
        int f = ((index % n) + n) % n;                       // 负数也不会越界
        if (f == _frame) return;
        if (portalFrames[f] == null) return;
        _frame = f;
        portalImage.sprite = portalFrames[f];
    }

#if UNITY_EDITOR
    /// <summary>在 Inspector 里改帧率 / 换帧时立刻看到结果，不用进 Play。</summary>
    void OnValidate()
    {
        if (frameFps < 0.1f) frameFps = 0.1f;
        if (portalImage != null && portalFrames != null && portalFrames.Length > 0 && portalFrames[0] != null)
        {
            portalImage.sprite = portalFrames[0];
            _frame = 0;
        }
    }
#endif
}
