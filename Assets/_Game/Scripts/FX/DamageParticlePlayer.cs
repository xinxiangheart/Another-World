using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// DamageParticlePlayer — 伤害粒子单粒子播放器（表现层，不参与伤害计算）
// ============================================================================
//
// 红色发光单粒子 + 短拖尾，从起点飞向目标终点，到达时触发 onArrive（弹伤害数字）。
// 两种轨迹：
//   PlayStraight : 起点 → 终点 直飞（ease-in 命中感）
//   PlaySpecial  : 格子/自身特殊轨迹 —— 先向Y+移D → 绕起点顺时针转一圈 → 直飞终点
//
// 对象池管理（DamageFX 统一获取/回收）。世界空间对象，与卡牌同坐标系。
// ============================================================================

public class DamageParticlePlayer : MonoBehaviour
{
    static readonly Queue<DamageParticlePlayer> _pool = new Queue<DamageParticlePlayer>();
    static Transform _poolRoot;
    static Sprite _sharedSprite;

    SpriteRenderer _sr;
    TrailRenderer _trail;
    Coroutine _routine;

    /// <summary>从池取一个粒子播放器（无则新建），返回前设为活动。</summary>
    public static DamageParticlePlayer Get()
    {
        EnsurePool();
        DamageParticlePlayer p = _pool.Count > 0 ? _pool.Dequeue() : Create();
        p.gameObject.SetActive(true);
        if (p._trail != null) p._trail.Clear();
        return p;
    }

    /// <summary>直飞模式：从 from 飞到 to，到达触发 onArrive。</summary>
    public void PlayStraight(Vector3 from, Vector3 to, float duration, Action onArrive)
    {
        StopCurrent();
        _routine = StartCoroutine(StraightRoutine(from, to, duration, onArrive));
    }

    /// <summary>特殊轨迹：先向上移D → 绕起点顺时针一圈 → 飞向终点，到达触发 onArrive。</summary>
    public void PlaySpecial(Vector3 start, Vector3 target, float duration, Action onArrive)
    {
        StopCurrent();
        _routine = StartCoroutine(SpecialRoutine(start, target, duration, onArrive));
    }

    IEnumerator StraightRoutine(Vector3 from, Vector3 to, float duration, Action onArrive)
    {
        transform.position = AtDepth(from);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float eased = k * k; // ease-in：先慢后快，命中感
            transform.position = AtDepth(Vector3.Lerp(from, to, eased));
            yield return null;
        }
        transform.position = AtDepth(to);
        onArrive?.Invoke();
        Release();
    }

    IEnumerator SpecialRoutine(Vector3 start, Vector3 target, float duration, Action onArrive)
    {
        transform.position = AtDepth(start);
        float radius = 0.6f; // 上移距离 = 环绕半径

        // 1) 向 Y+ 移动一段距离
        Vector3 lift = AtDepth(start + Vector3.up * radius);
        float liftT = 0f;
        while (liftT < duration * 0.25f)
        {
            liftT += Time.deltaTime;
            transform.position = AtDepth(Vector3.Lerp(start, lift, Mathf.Clamp01(liftT / (duration * 0.25f))));
            yield return null;
        }
        transform.position = lift;

        // 2) 以 start 为圆心、radius 为半径顺时针快速转一圈
        float circleDur = duration * 0.35f;
        float angle = 0f;
        while (angle < 360f)
        {
            angle += (360f / circleDur) * Time.deltaTime;
            float rad = Mathf.Deg2Rad * angle;
            transform.position = AtDepth(start + new Vector3(Mathf.Sin(rad), Mathf.Cos(rad), 0f) * radius);
            yield return null;
        }

        // 3) 直飞向终点
        float flyT = 0f;
        float flyDur = duration * 0.4f;
        Vector3 from = transform.position;
        while (flyT < flyDur)
        {
            flyT += Time.deltaTime;
            float k = Mathf.Clamp01(flyT / flyDur);
            transform.position = AtDepth(Vector3.Lerp(from, target, k * k));
            yield return null;
        }
        transform.position = AtDepth(target);
        onArrive?.Invoke();
        Release();
    }

    /// <summary>粒子统一在固定深度平面飞行（卡前 z=-5.9），避免随目标穿卡/深度不一。</summary>
    const float FIXED_DEPTH = -5.9f;
    static Vector3 AtDepth(Vector3 p)
    {
        p.z = FIXED_DEPTH;
        return p;
    }

    void StopCurrent()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
    }

    void Release()
    {
        _routine = null;
        if (_trail != null) _trail.Clear();
        gameObject.SetActive(false);
        _pool.Enqueue(this);
    }

    // ═══════════════════════════════════════════════════════════════════

    static void EnsurePool()
    {
        if (_poolRoot != null) return;
        _poolRoot = new GameObject("DamageParticlePool").transform;
        UnityEngine.Object.DontDestroyOnLoad(_poolRoot.gameObject); // 跨场景存活（与 DamageFloaterPool 一致）
    }

    static DamageParticlePlayer Create()
    {
        var go = new GameObject("DamageParticle");
        go.transform.SetParent(_poolRoot, false);

        var p = go.AddComponent<DamageParticlePlayer>();
        p.BuildVisual();
        return p;
    }

    void BuildVisual()
    {
        // 发光粒子：红色圆（边缘渐变模拟发光）。
        // 不创建运行时材质——用 SpriteRenderer 默认材质（避免 new Material 触发 GPU 断言 SUCCEEDED(hr)）
        if (_sharedSprite == null)
            _sharedSprite = MakeCircleSprite(32, new Color(1f, 0.25f, 0.2f, 1f));
        _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.sprite = _sharedSprite;
        _sr.sortingOrder = 200;

        // 短拖尾（startColor/endColor 控制红色渐隐）——不创建运行时材质，用 TrailRenderer 默认材质
        _trail = gameObject.AddComponent<TrailRenderer>();
        _trail.startColor = new Color(1f, 0.3f, 0.2f, 0.9f);
        _trail.endColor = new Color(1f, 0.1f, 0.1f, 0f);
        _trail.time = 0.25f;
        _trail.startWidth = 0.12f;
        _trail.endWidth = 0.02f;
        _trail.minVertexDistance = 0.05f;

        transform.localScale = new Vector3(0.6f, 0.6f, 1f);
    }

    /// <summary>运行时生成一个中心实心圆贴图（不依赖外部美术）。size=像素边长（32→0.32 世界单位）。</summary>
    static Sprite MakeCircleSprite(int size, Color color)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = (size - 1) * 0.5f;
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / r;
                float a = Mathf.Clamp01(1f - d); // 边缘渐变 → 发光感
                tex.SetPixel(x, y, new Color(color.r, color.g, color.b, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
