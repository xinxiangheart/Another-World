using UnityEngine;

/// <summary>相机冲击（落地 / 受击）：短促、指数衰减、结束精确复位。
/// 只挂在 Camera.main 上；不改 Time.timeScale，也不做长时间晃动。
/// 多处同时触发时取「剩余强度」与「新强度」的较大值，不叠加成大幅抖动。</summary>
public class CameraShake : MonoBehaviour
{
    Vector3 _applied;
    float _amp;
    float _dur;
    float _t;

    public static void Impulse(float amplitude, float duration)
    {
        if (amplitude <= 0f || duration <= 0f) return;
        // 入场镜头每帧 SetPositionAndRotation，这期间施加偏移只会被覆盖或打架
        if (GameIntroCamera.Playing) return;
        Camera cam = Camera.main;
        if (cam == null) return;

        CameraShake shake = cam.GetComponent<CameraShake>();
        if (shake == null) shake = cam.gameObject.AddComponent<CameraShake>();
        shake.AddImpulse(amplitude, duration);
    }

    void AddImpulse(float amplitude, float duration)
    {
        float remainingAmp = _dur > 0f ? _amp * (1f - Mathf.Clamp01(_t / _dur)) : 0f;
        float remainingDur = _dur > 0f ? Mathf.Max(0f, _dur - _t) : 0f;

        _amp = Mathf.Max(remainingAmp, amplitude);
        _dur = Mathf.Max(remainingDur, duration);
        _t = 0f;
    }

    void LateUpdate()
    {
        // 先撤掉上一帧的偏移，保证不会累积漂移
        if (_applied != Vector3.zero)
        {
            transform.position -= _applied;
            _applied = Vector3.zero;
        }

        if (_dur <= 0f || _t >= _dur) return;

        // 入场镜头开始后撤回已有偏移并停手，把相机完全交还 GameIntroCamera
        if (GameIntroCamera.Playing)
        {
            _dur = 0f;
            _t = 0f;
            _amp = 0f;
            return;
        }

        _t += Time.deltaTime;
        float p = Mathf.Clamp01(_t / _dur);
        float k = (1f - p) * (1f - p);

        // 主冲击向下（落地感），叠一点横向随机抖动；幅度随时间平方衰减
        Vector3 offset = Vector3.down * (_amp * k)
                       + Random.insideUnitSphere * (_amp * 0.35f * k);

        transform.position += offset;
        _applied = offset;
    }

    void OnDisable()
    {
        if (_applied != Vector3.zero)
        {
            transform.position -= _applied;
            _applied = Vector3.zero;
        }
    }
}
