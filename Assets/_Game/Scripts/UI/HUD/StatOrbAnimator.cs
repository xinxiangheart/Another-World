using UnityEngine;

/// <summary>
/// 己方生命 / 能量圆环的动画。只动挂载物体（Ring）自己的 RectTransform，本体（Circle）不参与。
/// · Breath：呼吸 —— 环在 1x 与 breathMaxScale 之间平滑来回（靠近主体再远离）
/// · Spin  ：绕主体旋转 —— 按 spinSpeed 度/秒自转
/// </summary>
public class StatOrbAnimator : MonoBehaviour
{
    public enum Mode { Breath, Spin }

    [Tooltip("Breath = 呼吸（靠近/远离）；Spin = 绕主体旋转")]
    public Mode mode = Mode.Breath;

    [Header("Breath")]
    [Tooltip("环相对自身初始缩放的最大倍率（1.12 = 最远时放大到 112%）")]
    public float breathMaxScale = 1.12f;
    [Tooltip("一次呼吸（放大 → 缩回）的时长，秒")]
    public float breathPeriod = 3f;
    [Tooltip("初始相位 0–1，用来错开多个环的节奏")]
    public float breathPhase = 0f;

    [Header("Spin")]
    [Tooltip("每秒旋转角度（负值反向）")]
    public float spinSpeed = 24f;

    Vector3 _baseScale = Vector3.one;
    float _t;

    void Awake()
    {
        _baseScale = transform.localScale;
        _t = breathPhase;
    }

    void OnEnable()
    {
        _t = breathPhase;
    }

    void Update()
    {
        if (mode == Mode.Breath)
        {
            _t += Time.deltaTime / Mathf.Max(0.01f, breathPeriod);
            float k = (Mathf.Cos(_t * Mathf.PI * 2f) + 1f) * 0.5f;   // 0 → 1 → 0
            transform.localScale = _baseScale * Mathf.Lerp(1f, breathMaxScale, k);
        }
        else
        {
            transform.Rotate(0f, 0f, -spinSpeed * Time.deltaTime, Space.Self);
        }
    }
}
