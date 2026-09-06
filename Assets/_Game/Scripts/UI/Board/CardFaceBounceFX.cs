using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 3D 卡面元素弹跳（纯表现，不改判定）：按元素 id 驱动 localScale 倍率 1→1.25→1
/// （上升 0.16s ease-out / 回落 0.28s ease-out，类似召唤动画但作用在单个文字/图标上）。
///
/// 静止缩放(rest)按 id 首次注册（EnsureElement）后固定，不再随动画中的 localScale 变化——
/// 否则中断重入时会把"放大中的值"误当静止基准造成漂移。
///
/// 中断重入：Bounce 时若同 id 已有动画，记录当前显示倍率(CurrentMult)，新动画从该倍率
/// 升到同一峰值(peak)再归 1 —— 连续变化(如生命连扣)从中断处续弹，最终放大程度与从 0 起一致。
///
/// 由 Card3DInstance.Awake 自动挂到卡根；召唤期/首刷抑制由调用方用
/// Card3DInstance.ElementBounceAllowed 门控（不在此判断）。
/// 文本元素持久存在；图标三排每次刷新重建：重建前调用方 Cancel(旧) 保留倍率，
/// 重建后 EnsureElement(新) + Bounce 即从中断倍率续弹（或 force 弹新出现的）。
/// </summary>
public class CardFaceBounceFX : MonoBehaviour
{
    public float peak = 2.5f;     // 峰值放大倍率（相对正常放大幅度 = peak-1 = 1.5，为基准 0.25 的 6 倍）
    public float riseDur = 0.16f; // 上升段时长（m0→peak）
    public float fallDur = 0.28f; // 回落段时长（peak→1.0）

    readonly Dictionary<string, Vector3> _rest = new Dictionary<string, Vector3>();
    readonly Dictionary<string, float> _mult = new Dictionary<string, float>();
    readonly Dictionary<string, Coroutine> _routines = new Dictionary<string, Coroutine>();

    /// <summary>当前显示倍率（默认 1）。重建前 Cancel 保留此值供续弹。</summary>
    public float CurrentMult(string id)
    {
        float v;
        return _mult.TryGetValue(id, out v) ? v : 1f;
    }

    /// <summary>注册静止缩放（仅首次写入；元素静止时调用，如图标刚建好/文本未在动画）。</summary>
    public void EnsureElement(string id, Transform target)
    {
        if (target == null || _rest.ContainsKey(id)) return;
        _rest[id] = target.localScale;
    }

    /// <summary>停止某元素动画，保留当前倍率（图标被重建/打断时调用，供 Bounce 续弹）。</summary>
    public void Cancel(string id)
    {
        Coroutine c;
        if (_routines.TryGetValue(id, out c) && c != null) StopCoroutine(c);
        _routines.Remove(id);
    }

    /// <summary>彻底遗忘某元素状态（图标已消失时调用）。</summary>
    public void Drop(string id)
    {
        Cancel(id);
        _mult.Remove(id);
        _rest.Remove(id);
    }

    /// <summary>播放/续播弹跳。force=true 即使静止也弹（新出现/变化）；
    /// force=false 仅当当前非静止(倍率&gt;1)时从中断倍率续弹，否则什么都不做。</summary>
    public void Bounce(string id, Transform target, bool force = false)
    {
        Cancel(id);
        Vector3 rest;
        if (!_rest.TryGetValue(id, out rest))
        {
            if (target == null) return;
            rest = target.localScale;
            _rest[id] = rest;
        }
        float m0 = CurrentMult(id);
        if (!force && m0 < 1.001f)
        {
            if (target != null) target.localScale = rest;
            _mult[id] = 1f;
            return;
        }
        m0 = Mathf.Clamp(m0, 0.01f, peak);
        _mult[id] = m0;
        if (target == null) { _routines.Remove(id); return; }
        _routines[id] = StartCoroutine(BounceRoutine(id, target, rest, m0));
    }

    IEnumerator BounceRoutine(string id, Transform target, Vector3 rest, float m0)
    {
        // 上升段：m0 → peak（短，ease-out）
        {
            float t = 0f;
            float dur = Mathf.Max(0.001f, riseDur);
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                float e = 1f - Mathf.Pow(1f - k, 3f);
                float m = Mathf.Lerp(m0, peak, e);
                _mult[id] = m;
                if (target != null) target.localScale = rest * m;
                else { _mult[id] = m0; Cancel(id); yield break; }
                yield return null;
            }
        }
        // 回落段：peak → 1.0（稍长，ease-out）
        {
            float t = 0f;
            float dur = Mathf.Max(0.001f, fallDur);
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                float e = 1f - Mathf.Pow(1f - k, 3f);
                float m = Mathf.Lerp(peak, 1f, e);
                _mult[id] = m;
                if (target != null) target.localScale = rest * m;
                else break;
                yield return null;
            }
        }
        _mult[id] = 1f;
        if (target != null) target.localScale = rest;
        _routines.Remove(id);
    }
}
