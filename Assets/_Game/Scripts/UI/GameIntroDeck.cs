using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 入场 3D 牌堆（2026-09-19）：入场镜头飞行期间，画面右侧中央有一摞 3D 卡牌从空中迅速落下、
/// 堆成牌堆，最后一张正好落在 UI 开始浮现的那一刻；牌堆默认留在场上（收不收起见 collapseDuration）。
///
/// 只用 3D 卡预制体里的模型盒（ModelRoot）：
///   · 正面那一整套 UIComponents（卡框 / 前缀底图 / 卡图 / 文字 / 图标 / 三排）整棵删掉；
///   · 模型盒 +Z 是卡面、-Z 是卡背，卡牌保持 identity 朝向时相机正好落在卡背那一侧 ——
///     所以牌堆看到的永远是卡背；
///   · Card3DInstance / CardDisplay3D / Card3DHover / CardIcons3D / DamageSourceMapper、BoxCollider，
///     以及 Card3DInstance.Awake 顺手挂上的漂浮 / 攻击 / 弹跳组件一并删掉（漂浮会把牌拉离落点，
///     碰撞体还会去吃鼠标悬停）。
///
/// 落点由「终态相机的视口坐标」反算：换分辨率 / 换宽高比，牌堆都待在同一个地方，
/// 与槽位列保持距离（视口值见 GameIntroCamera 的 deckViewport）。
///
/// 时间轴自走：Tick(dt) 由 GameIntroCamera.Update 每帧喂，和镜头那段协程互不牵扯。
/// 每张牌在「该它出场那一帧」才 Instantiate —— 十张拆到两秒里建，不会在开场卡一下。
/// </summary>
public class GameIntroDeck
{
    class Card
    {
        public GameObject go;
        public Transform tr;
        public Vector3 from, to;
        public Quaternion fromRot, toRot;
        public float spawnAt, landAt;
    }

    // ── 由 GameIntroCamera 填（对应它 Inspector 上的同名字段）──
    public GameObject cardPrefab;
    public Camera cam;
    public Vector3 endCamPos;
    public Quaternion endCamRot;
    public float planeZ = -5.7f;
    public int cardCount = 10;
    public float firstLandTime = 0.6f;
    public float lastLandTime = 1.44f;
    public float fallDuration = 0.22f;
    public float holdAfterLand = 0.2f;
    public float collapseDuration = 0.5f;
    public Vector2 viewport = new Vector2(0.835f, 0.5f);

    const float FallHeight = 2.8f;          // 从落点上方多高处掉下来
    const float SpawnBiasZ = 0.55f;         // 生成点再往相机这边偏一点（往观众方向落）
    const float SpawnScatterX = 0.3f;       // 生成点横向散开
    const float StackStepY = 0.024f;        // 每张往上错开一点（整齐的等距）
    const float StackStepZ = -0.012f;       // 每张往相机错开一点（越晚落越靠前 → 最后一张压在最上面）
    const float MaterializePortion = 0.22f; // 下落前段用来「生成」（缩放入场）
    const float SettleDuration = 0.10f;     // 落地压缩回弹时长
    const float SettleSquash = 0.10f;       // 落地压扁幅度
    const float CollapseDrop = 0.35f;       // 收起时同时下沉的距离

    // 私有随机源：不碰 UnityEngine.Random 的全局序列（免得把发牌的随机带偏），顺带每局长得一样
    readonly List<Card> _cards = new List<Card>();
    readonly System.Random _rng = new System.Random(20260919);
    Transform _root;
    Vector3 _baseScale = Vector3.one;
    float _t;
    float _lastLand;
    float _collapseAt = float.PositiveInfinity;
    float _endAt = float.PositiveInfinity;   // 收起结束时刻；不收起时是 +∞
    bool _idle;

    /// <summary>牌堆已经收干净（不收起时恒为 false —— 那种情况要一直留着）。</summary>
    public bool Finished { get { return _t >= _endAt; } }

    /// <summary>排好这一摞牌的「剧本」（只算数，牌等到该出场那一帧才建）。</summary>
    public void Build()
    {
        if (cardPrefab == null || cam == null) return;

        GameObject holder = new GameObject("IntroCardDecks");
        holder.transform.SetParent(null, false);
        holder.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        _root = holder.transform;
        _baseScale = cardPrefab.transform.localScale;

        int n = Mathf.Max(1, cardCount);
        float first = Mathf.Max(0f, firstLandTime);
        float last = Mathf.Max(first + 0.05f, lastLandTime);
        _lastLand = last;

        BuildPile(viewport, n, first, last);

        _collapseAt = collapseDuration > 0f ? last + Mathf.Max(0f, holdAfterLand) : float.PositiveInfinity;
        _endAt = collapseDuration > 0f ? _collapseAt + collapseDuration : float.PositiveInfinity;
    }

    float Rand(float min, float max)
    {
        return min + (float)_rng.NextDouble() * (max - min);
    }

    void BuildPile(Vector2 viewport, int n, float firstLand, float lastLand)
    {
        Vector3 anchor = ViewportToWorld(viewport);
        for (int i = 0; i < n; i++)
        {
            float k = n > 1 ? (float)i / (n - 1) : 1f;
            float landAt = Mathf.Lerp(firstLand, lastLand, k);
            float fall = fallDuration;

            Vector3 to = anchor + new Vector3(0f, i * StackStepY, i * StackStepZ);
            Quaternion toRot = Quaternion.identity;      // 落定后张张对齐，牌堆才整齐
            Vector3 from = to + new Vector3(Rand(-SpawnScatterX, SpawnScatterX),
                                            FallHeight + i * 0.06f,
                                            SpawnBiasZ);
            Quaternion fromRot = Quaternion.Euler(Rand(-24f, -12f),   // 落下来这一路是歪的，
                                                  Rand(-9f, 9f),       // 落地时转正
                                                  Rand(-7f, 7f));

            _cards.Add(new Card
            {
                from = from,
                to = to,
                fromRot = fromRot,
                toRot = toRot,
                landAt = landAt,
                spawnAt = landAt - fall,
            });
        }
    }

    /// <summary>每帧推一次（GameIntroCamera.Update 喂 dt）。</summary>
    public void Tick(float dt)
    {
        if (_root == null || _idle) return;
        _t += dt;

        float collapse = 0f;
        if (collapseDuration > 0f && _t > _collapseAt)
            collapse = Mathf.Clamp01((_t - _collapseAt) / Mathf.Max(0.0001f, collapseDuration));
        float collapseEase = collapse * collapse;       // 越收越快

        for (int i = 0; i < _cards.Count; i++)
        {
            Card c = _cards[i];

            // 该它出场了才建：20 张分开建，开场不会卡一下
            if (c.go == null)
            {
                if (_t < c.spawnAt) continue;
                c.go = CreateCardBack(c.from, c.fromRot);
                c.tr = c.go.transform;
                if (c.tr == null) continue;
            }

            float p = Mathf.Clamp01((_t - c.spawnAt) / Mathf.Max(0.0001f, fallDuration));
            float fallEase = p * p;                     // 越掉越快（重力）
            Vector3 pos = Vector3.LerpUnclamped(c.from, c.to, fallEase);
            Quaternion rot = Quaternion.SlerpUnclamped(c.fromRot, c.toRot, fallEase);

            // 生成：下落前段迅速长出来
            float grow = Mathf.Clamp01(p / MaterializePortion);
            float s = grow * grow * (3f - 2f * grow);
            float sxz = 1f, sy = 1f;

            // 落地：压一下再弹回来
            float q = Mathf.Clamp01((_t - c.landAt) / SettleDuration);
            if (q > 0f && q < 1f)
            {
                float bump = Mathf.Sin(q * Mathf.PI);
                pos.y += 0.05f * bump;
                sy = 1f - SettleSquash * bump;
                sxz = 1f + SettleSquash * 0.5f * bump;
            }

            // 收起：一边缩小一边往下沉（collapseEase = 0 时完全不影响）
            if (collapseEase > 0f)
            {
                s *= 1f - collapseEase;
                pos.y -= CollapseDrop * collapseEase;
            }

            c.tr.localPosition = pos;
            c.tr.localRotation = rot;
            c.tr.localScale = Vector3.Scale(_baseScale, new Vector3(sxz, sy, 1f) * s);
        }

        // 收完了 / （不收起时）全部定格了，就不用再算了
        if (!float.IsPositiveInfinity(_endAt) && _t >= _endAt) _idle = true;
        else if (float.IsPositiveInfinity(_endAt) && _t >= _lastLand + SettleDuration) _idle = true;
    }

    /// <summary>整棵删掉（默认不收起 —— 牌堆一直留在场上，由 GameIntroCamera 留着不调这个）。</summary>
    public void Dispose()
    {
        _cards.Clear();
        if (_root != null) Object.Destroy(_root.gameObject);
        _root = null;
        _idle = true;
    }

    /// <summary>
    /// 只留卡背的一张卡：模型盒留下，其余脚本 / 碰撞体 / 正面容器全删。
    /// 卡牌 identity 朝向 → 相机在卡背那一侧。
    /// </summary>
    GameObject CreateCardBack(Vector3 pos, Quaternion rot)
    {
        GameObject card = Object.Instantiate(cardPrefab, pos, rot, _root);
        card.name = "DeckCard";

        // 先关掉再删：Destroy 要等这一帧结束才生效，先 enabled=false 免得 Start/Update 抢跑
        MonoBehaviour[] scripts = card.GetComponents<MonoBehaviour>();
        for (int i = 0; i < scripts.Length; i++)
        {
            if (scripts[i] == null) continue;
            scripts[i].enabled = false;
            Object.Destroy(scripts[i]);
        }
        Collider[] colliders = card.GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null) Object.Destroy(colliders[i]);

        Transform model = card.transform.Find("ModelRoot");
        if (model == null)
        {
            MeshRenderer mr = card.GetComponentInChildren<MeshRenderer>(true);
            model = mr != null ? mr.transform : null;
        }
        if (model != null)
        {
            for (int i = card.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = card.transform.GetChild(i);
                if (child == model || model.IsChildOf(child)) continue;   // 模型那一支留着
                Object.Destroy(child.gameObject);
            }
        }

        // CardDisplay3D.Awake 会顺手 ShowFront()（把 ModelRoot 关掉）—— 这里把模型盒重新点亮
        if (model != null) model.gameObject.SetActive(true);

        card.transform.localScale = Vector3.zero;   // 先缩到 0，由 Tick 长出来
        return card;
    }

    /// <summary>终态画面的视口坐标 → 相机前方 planeZ 平面上的世界点。</summary>
    Vector3 ViewportToWorld(Vector2 viewport)
    {
        float halfTan = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float tanY = halfTan;
        float tanX = halfTan * Mathf.Max(0.05f, cam.aspect);

        Vector3 forward = endCamRot * Vector3.forward;
        Vector3 right = endCamRot * Vector3.right;
        Vector3 up = endCamRot * Vector3.up;

        float fz = Mathf.Abs(forward.z) < 0.0001f ? 0.0001f : forward.z;
        float depth = (planeZ - endCamPos.z) / fz;         // 沿视线走到 planeZ 那个平面
        Vector3 center = endCamPos + forward * depth;
        return center + right * ((viewport.x * 2f - 1f) * depth * tanX)
                      + up * ((viewport.y * 2f - 1f) * depth * tanY);
    }
}
