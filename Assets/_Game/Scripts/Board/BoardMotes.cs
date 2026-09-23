using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战场微尘层（可调数量 / 出现间隔）。
///
/// 用法：挂在棋盘 Canvas 下的一个空 GameObject 上，尺寸与棋盘一致（19.2 x 10.8 世界单位，
/// 即 BoardManager 那套「1 UI 单位 = 1 世界单位」的独立 Canvas），Z 贴在 L7 微尘层的排序位。
/// 把 Assets/_Game/Art/Sprites/Generated/Board_MoteDot.png（或任意小圆点）拖到 moteSprite。
///
/// 这套是「粒子路线」：数量、出现间隔、寿命、速度、大小、透明度全部是 Inspector 字段，
/// 运行时可实时拖，不需要重出贴图。底下那张 Board_Motes.png 是「贴图路线」的静态微尘，
/// 两层可以同时用：静态层保画风下限，这一层做随机节奏。
///
/// 亮度纪律：默认 alphaRange 上限 0.22、尺寸 0.028~0.075 世界单位（约 3~8 px），
/// 刻意压得很低，避免微尘抢到卡牌主体。
/// </summary>
[DisallowMultipleComponent]
public class BoardMotes : MonoBehaviour
{
    [Header("贴图与画布")]
    [Tooltip("单颗微尘贴图，推荐 Generated/Board_MoteDot.png")]
    public Sprite moteSprite;
    [Tooltip("铺开的范围（世界单位），与棋盘一致即可")]
    public Vector2 areaSize = new Vector2(19.2f, 10.8f);

    [Header("数量与节奏")]
    [Tooltip("同时存在的最大颗数")]
    public int maxMotes = 24;
    [Tooltip("每隔多少秒出现一颗；<= 0 表示刚消失就立刻补下一颗")]
    public float spawnInterval = 0.45f;
    [Tooltip("每颗存活多少秒（含首尾淡入淡出）")]
    public float lifetime = 9f;
    [Tooltip("开局先预铺几颗（让开场画面不是空的）")]
    public int prefill = 12;

    [Header("运动")]
    [Tooltip("向上漂移，世界单位/秒")]
    public float riseSpeed = 0.045f;
    [Tooltip("横向漂移，世界单位/秒（正负随机）")]
    public float driftSpeed = 0.05f;
    [Tooltip("左右摆动幅度，世界单位")]
    public float swayAmplitude = 0.06f;
    [Tooltip("左右摆动频率，次/秒")]
    public float swayFrequency = 0.3f;

    [Header("大小与透明度")]
    public Vector2 sizeRange = new Vector2(0.028f, 0.075f);
    public Vector2 alphaRange = new Vector2(0.06f, 0.22f);
    public Color tint = new Color(0.78f, 0.86f, 1f, 1f);
    [Range(0.01f, 0.5f)]
    [Tooltip("首尾各占寿命多少比例用于淡入/淡出")]
    public float fadePortion = 0.25f;

    [Header("随机与时间")]
    public int seed = 20260922;
    [Tooltip("勾上则不受 Time.timeScale 影响")]
    public bool useUnscaledTime = false;

    class Mote
    {
        public RectTransform rt;
        public Image img;
        public bool alive;
        public float born;
        public float life;
        public Vector2 start;
        public float dir;
        public float phase;
        public float sway;
        public float baseAlpha;
    }

    readonly List<Mote> _motes = new List<Mote>();
    RectTransform _root;
    System.Random _rng;
    float _t;
    float _spawnAcc;

    void Awake()
    {
        _rng = new System.Random(seed);
        _root = GetComponent<RectTransform>();
        if (_root == null) _root = gameObject.AddComponent<RectTransform>();
        _root.sizeDelta = areaSize;

        EnsurePool(Mathf.Max(prefill, maxMotes));

        // 预铺：给随机年龄，让开场就有一些在半空中
        for (int i = 0; i < Mathf.Clamp(prefill, 0, _motes.Count); i++)
        {
            Spawn(_motes[i]);
            _motes[i].born -= (float)(_rng.NextDouble() * lifetime);
        }
        _t = 0f;
        for (int i = 0; i < _motes.Count; i++)
            if (_motes[i].alive) Step(_motes[i], 0f);
    }

    void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (dt <= 0f) return;
        _t += dt;

        EnsurePool(maxMotes);

        if (spawnInterval <= 0f)
        {
            for (int i = 0; i < _motes.Count; i++)
                if (!_motes[i].alive) Spawn(_motes[i]);
        }
        else
        {
            _spawnAcc += dt;
            if (_spawnAcc >= spawnInterval)
            {
                _spawnAcc = 0f;
                SpawnFree();
            }
        }

        for (int i = 0; i < _motes.Count; i++) Step(_motes[i], dt);
    }

    // 数量可在 Inspector 里随时调大调小：调大即补建，调小则等多出来的自然消失
    void EnsurePool(int n)
    {
        n = Mathf.Max(0, n);
        while (_motes.Count < n)
        {
            var go = new GameObject("Mote" + _motes.Count, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_root, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            var img = go.GetComponent<Image>();
            img.sprite = moteSprite;
            img.raycastTarget = false;
            img.enabled = false;
            _motes.Add(new Mote { rt = rt, img = img });
        }
    }

    void SpawnFree()
    {
        int alive = 0;
        for (int i = 0; i < _motes.Count; i++)
        {
            if (_motes[i].alive) alive++;
        }
        if (alive >= Mathf.Max(0, maxMotes)) return;
        for (int i = 0; i < _motes.Count; i++)
        {
            if (!_motes[i].alive) { Spawn(_motes[i]); return; }
        }
    }

    void Spawn(Mote m)
    {
        float halfW = areaSize.x * 0.5f;
        float halfH = areaSize.y * 0.5f;
        m.alive = true;
        m.born = _t;
        m.life = lifetime * (0.75f + (float)_rng.NextDouble() * 0.5f);
        m.start = new Vector2(
            ((float)_rng.NextDouble() * 2f - 1f) * halfW,
            ((float)_rng.NextDouble() * 2f - 1f) * halfH);
        m.dir = ((float)_rng.NextDouble() * 2f - 1f);
        m.phase = (float)_rng.NextDouble() * Mathf.PI * 2f;
        m.sway = swayAmplitude * (0.5f + (float)_rng.NextDouble());
        m.baseAlpha = Mathf.Lerp(alphaRange.x, alphaRange.y, (float)_rng.NextDouble());

        float s = Mathf.Lerp(sizeRange.x, sizeRange.y, (float)_rng.NextDouble());
        m.rt.sizeDelta = new Vector2(s, s);
        m.img.enabled = true;
        m.img.color = new Color(tint.r, tint.g, tint.b, 0f);
        m.rt.anchoredPosition = m.start;
    }

    void Step(Mote m, float dt)
    {
        if (!m.alive) return;
        float age = _t - m.born;
        if (age >= m.life)
        {
            m.alive = false;
            m.img.enabled = false;
            return;
        }
        float u = Mathf.Clamp01(age / m.life);
        float fp = Mathf.Clamp(fadePortion, 0.01f, 0.5f);
        float a = m.baseAlpha;
        if (u < fp) a *= u / fp;
        else if (u > 1f - fp) a *= (1f - u) / fp;

        m.img.color = new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(a));

        float x = m.start.x + driftSpeed * m.dir * age + Mathf.Sin(m.phase + age * swayFrequency * Mathf.PI * 2f) * m.sway;
        float y = m.start.y + riseSpeed * age;
        m.rt.anchoredPosition = new Vector2(x, y);
    }

    void OnValidate()
    {
        maxMotes = Mathf.Max(0, maxMotes);
        prefill = Mathf.Max(0, prefill);
        lifetime = Mathf.Max(0.1f, lifetime);
        if (sizeRange.y < sizeRange.x) sizeRange.y = sizeRange.x;
        if (alphaRange.y < alphaRange.x) alphaRange.y = alphaRange.x;
    }
}

