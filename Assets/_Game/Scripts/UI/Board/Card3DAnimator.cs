using UnityEngine;

// ============================================================================
// Card3DAnimator — 3D 卡牌漂浮 + 呼吸缩放动画
// ============================================================================
//
// 用 Mathf.Sin(Time.time + phase) 驱动，不依赖 DOTween。
// 漂浮：在 baseLocalPos 基础上做小幅 Y 轴正弦偏移。
// 呼吸：在 baseScale 基础上做小幅正弦缩放。
// 每个实例用随机 phase 错开，避免所有卡牌同频共振。
// ============================================================================

public class Card3DAnimator : MonoBehaviour
{
    private Vector3 _baseLocalPos;
    private Vector3 _baseScale;
    private float _phase;
    private AnimationConfig _config;

    void Start()
    {
        _baseLocalPos = transform.localPosition;
        _baseScale = transform.localScale;
        _phase = Random.Range(0f, Mathf.PI * 2f);
        _config = AnimationConfig.Load();
    }

    /// <summary>换位/移动后重捕基准位置——否则漂浮动画每帧把模型拉回原位置，视觉上"没移动"。</summary>
    public void UpdateBaseLocalPos()
    {
        _baseLocalPos = transform.localPosition;
    }

    void Update()
    {
        if (_config == null) return;

        // 漂浮：Y 轴正弦偏移
        float yOffset = Mathf.Sin(Time.time * _config.floatFrequency + _phase)
                        * _config.floatAmplitude;
        transform.localPosition = _baseLocalPos + new Vector3(0, yOffset, 0);

        // 呼吸：正弦缩放
        float breathe = 1f + Mathf.Sin(Time.time * (Mathf.PI * 2f / _config.breatheDuration) + _phase)
                        * (_config.breatheMaxScale - 1f);
        transform.localScale = _baseScale * breathe;
    }
}
