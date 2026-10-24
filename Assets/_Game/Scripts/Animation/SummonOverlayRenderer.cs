using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 召唤演出专用最高层渲染通道。
/// 演出期间把卡牌整体临时移到独立 Layer，用自己的相机渲染到 RenderTexture，
/// 再由 Screen Space Overlay 的最顶层 RawImage 显示。这样无论手牌、展示卡、棋盘、
/// 任何世界物体甚至其它 Overlay 面板，都不可能盖住飞行中的卡。
/// 演出结束恢复卡牌原 Layer 与主相机裁剪掩码。
/// </summary>
[DefaultExecutionOrder(32500)]
public sealed class SummonOverlayRenderer : MonoBehaviour
{
    // 场景 TagManager 中未命名的空闲层，专用于召唤演出。
    const int OverlayLayer = 31;
    const int OverlaySortingOrder = 32767;

    sealed class LayerRecord
    {
        public readonly List<Transform> transforms = new List<Transform>();
        public readonly Dictionary<Transform, int> originalLayers = new Dictionary<Transform, int>();
    }

    static SummonOverlayRenderer s_instance;

    readonly Dictionary<Card3DInstance, LayerRecord> _owners = new Dictionary<Card3DInstance, LayerRecord>();

    Camera _mainCamera;
    Camera _overlayCamera;
    RenderTexture _texture;
    RawImage _rawImage;
    Canvas _canvas;
    Camera _maskCamera;
    bool _savedMainLayerBit;
    int _lastWidth;
    int _lastHeight;
    int _lastAntiAliasing;
    float _nextTextureRetryTime;

    public static bool Begin(Card3DInstance owner)
    {
        if (owner == null || !Application.isPlaying) return false;
        SummonOverlayRenderer inst = Ensure();
        if (inst == null) return false;
        return inst.BeginInternal(owner);
    }

    public static void End(Card3DInstance owner)
    {
        if (s_instance == null || owner == null) return;
        s_instance.EndInternal(owner);
    }

    static SummonOverlayRenderer Ensure()
    {
        if (s_instance != null) return s_instance;

        GameObject host = new GameObject("[SummonOverlayRenderer]",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        s_instance = host.AddComponent<SummonOverlayRenderer>();
        s_instance.Build();
        return s_instance;
    }

    void Build()
    {
        _canvas = GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.overrideSorting = true;
        _canvas.sortingOrder = OverlaySortingOrder;
        _canvas.pixelPerfect = false;

        CanvasGroup group = GetComponent<CanvasGroup>();
        group.alpha = 1f;
        group.interactable = false;
        group.blocksRaycasts = false;

        GameObject rawGo = new GameObject("SummonOverlayImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        rawGo.transform.SetParent(transform, false);
        RectTransform rect = (RectTransform)rawGo.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);

        _rawImage = rawGo.GetComponent<RawImage>();
        _rawImage.raycastTarget = false;
        _rawImage.maskable = false;
        _rawImage.color = Color.white;

        GameObject camGo = new GameObject("SummonOverlayCamera", typeof(Camera));
        camGo.transform.SetParent(transform, false);
        _overlayCamera = camGo.GetComponent<Camera>();
        _overlayCamera.enabled = false;

        SetActive(false);
    }

    bool BeginInternal(Card3DInstance owner)
    {
        if (_owners.ContainsKey(owner)) return true;

        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null) return false;

        // 先把通道本身准备好，再动卡牌 Layer。通道失败时卡牌仍留在主相机里，
        // 不会出现“Layer 已改、两个相机却都没渲染”的彻底不可见。
        EnsureTexture();
        if (_overlayCamera == null || _rawImage == null || _texture == null)
        {
            Debug.LogWarning("[SummonOverlay] 渲染通道不可用，继续使用主相机显示召唤卡。");
            return false;
        }

        LayerRecord record = new LayerRecord();
        AddOwnerTransforms(owner, record);
        _owners[owner] = record;

        // 主相机不再渲染这张卡；它只出现在 Overlay 通道里，避免重影。
        ExcludeFromMainCamera();

        SyncCamera();
        SetActive(true);
        Debug.Log($"[SummonOverlay] 已启用最高层通道 owner={owner.name} rt={_texture.width}x{_texture.height}");
        return true;
    }

    void EndInternal(Card3DInstance owner)
    {
        LayerRecord record;
        if (_owners.TryGetValue(owner, out record))
        {
            _owners.Remove(owner);
            RestoreOwnerTransforms(record);
        }

        if (_owners.Count == 0)
        {
            SetActive(false);
            RestoreMainCamera();
            Debug.Log("[SummonOverlay] 演出结束，关闭最高层通道。");
        }
    }

    static void AddOwnerTransforms(Card3DInstance owner, LayerRecord record)
    {
        if (owner == null || record == null) return;
        Transform[] all = owner.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || record.originalLayers.ContainsKey(t)) continue;
            record.originalLayers[t] = t.gameObject.layer;
            record.transforms.Add(t);
            t.gameObject.layer = OverlayLayer;
        }
    }

    static void RestoreOwnerTransforms(LayerRecord record)
    {
        if (record == null) return;
        for (int i = 0; i < record.transforms.Count; i++)
        {
            Transform t = record.transforms[i];
            if (t == null) continue;
            int original;
            if (record.originalLayers.TryGetValue(t, out original))
                t.gameObject.layer = original;
        }
        record.transforms.Clear();
        record.originalLayers.Clear();
    }

    void SetActive(bool active)
    {
        if (_rawImage != null) _rawImage.enabled = active;
        if (_overlayCamera != null) _overlayCamera.enabled = active;
    }

    void EnsureTexture()
    {
        if (Time.unscaledTime < _nextTextureRetryTime) return;

        // 图形设备重置 / 编辑器切换渲染后端后，RT 可能还挂着但已经失效。
        if (_texture != null && !_texture.IsCreated())
        {
            if (_overlayCamera != null) _overlayCamera.targetTexture = null;
            _texture.Release();
            Destroy(_texture);
            _texture = null;
        }

        int w = Mathf.Max(1, Screen.width);
        int h = Mathf.Max(1, Screen.height);
        int aa = _mainCamera != null && _mainCamera.allowMSAA
            ? Mathf.Max(1, QualitySettings.antiAliasing) : 1;
        if (_texture != null && _lastWidth == w && _lastHeight == h && _lastAntiAliasing == aa) return;

        if (_overlayCamera != null) _overlayCamera.targetTexture = null;
        if (_texture != null)
        {
            _texture.Release();
            Destroy(_texture);
        }

        try
        {
            _texture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            _texture.name = "SummonOverlayRT";
            _texture.filterMode = FilterMode.Bilinear;
            _texture.wrapMode = TextureWrapMode.Clamp;
            _texture.antiAliasing = aa;
            _texture.useMipMap = false;
            _texture.autoGenerateMips = false;
            _texture.Create();
            if (!_texture.IsCreated() && aa > 1)
            {
                // 个别显卡/编辑器的 MSAA RT 组合不可用：退回无 MSAA，保证通道一定存在。
                _texture.Release();
                Destroy(_texture);
                aa = 1;
                _texture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                _texture.name = "SummonOverlayRT";
                _texture.filterMode = FilterMode.Bilinear;
                _texture.wrapMode = TextureWrapMode.Clamp;
                _texture.antiAliasing = 1;
                _texture.useMipMap = false;
                _texture.autoGenerateMips = false;
                _texture.Create();
            }
            if (!_texture.IsCreated()) throw new System.Exception("IsCreated() == false");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SummonOverlay] RenderTexture 创建失败：{e.Message}");
            _texture = null;
            _nextTextureRetryTime = Time.unscaledTime + 1f;
            return;
        }

        _lastWidth = w;
        _lastHeight = h;
        _lastAntiAliasing = aa;

        if (_rawImage != null) _rawImage.texture = _texture;
        if (_overlayCamera != null) _overlayCamera.targetTexture = _texture;
    }

    void SyncCamera()
    {
        if (_overlayCamera == null || _mainCamera == null) return;

        _overlayCamera.CopyFrom(_mainCamera);
        _overlayCamera.transform.SetPositionAndRotation(
            _mainCamera.transform.position, _mainCamera.transform.rotation);
        _overlayCamera.projectionMatrix = _mainCamera.projectionMatrix;
        _overlayCamera.cullingMask = 1 << OverlayLayer;
        _overlayCamera.targetTexture = _texture;
        _overlayCamera.clearFlags = CameraClearFlags.SolidColor;
        _overlayCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        _overlayCamera.depth = _mainCamera.depth + 100f;
        // RT 是 LDR ARGB32；HDR 不能跟随主相机，否则部分平台会把整张 RT 判为不可用。
        _overlayCamera.allowHDR = false;
        _overlayCamera.allowMSAA = _texture != null && _texture.antiAliasing > 1;
        _overlayCamera.useOcclusionCulling = false;
        _overlayCamera.allowDynamicResolution = false;
        _overlayCamera.rect = new Rect(0f, 0f, 1f, 1f);
    }

    void ExcludeFromMainCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        if (_maskCamera != cam)
        {
            RestoreMainCamera();
            _maskCamera = cam;
            _savedMainLayerBit = (cam.cullingMask & (1 << OverlayLayer)) != 0;
        }
        cam.cullingMask &= ~(1 << OverlayLayer);
    }

    void RestoreMainCamera()
    {
        if (_maskCamera == null) return;
        int mask = _maskCamera.cullingMask;
        if (_savedMainLayerBit)
            mask |= 1 << OverlayLayer;
        else
            mask &= ~(1 << OverlayLayer);
        _maskCamera.cullingMask = mask;
        _maskCamera = null;
        _savedMainLayerBit = false;
    }

    void LateUpdate()
    {
        if (_owners.Count == 0) return;
        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null) return;

        EnsureTexture();
        if (_texture == null || _overlayCamera == null)
        {
            // 通道中途失效时把卡牌还给主相机。宁可退出专用通道，也不能让它停在
            // 一个没人渲染的 Layer 上。下一秒 RT 恢复后会重新接入。
            SetActive(false);
            RestoreMainCamera();
            foreach (KeyValuePair<Card3DInstance, LayerRecord> kv in _owners)
                RestoreOwnerTransforms(kv.Value);
            return;
        }

        // 演出期间新生成的子物体也要一起进入专用层；同时每帧重新压掉主相机的该层，
        // 防止其它脚本中途重设 cullingMask 造成重影。
        foreach (KeyValuePair<Card3DInstance, LayerRecord> kv in _owners)
        {
            if (kv.Key == null) continue;
            AddOwnerTransforms(kv.Key, kv.Value);
        }
        ExcludeFromMainCamera();
        SyncCamera();
    }

    void OnDestroy()
    {
        RestoreMainCamera();

        foreach (KeyValuePair<Card3DInstance, LayerRecord> kv in _owners)
            RestoreOwnerTransforms(kv.Value);
        _owners.Clear();

        if (_overlayCamera != null) _overlayCamera.targetTexture = null;
        if (_texture != null)
        {
            _texture.Release();
            Destroy(_texture);
            _texture = null;
        }

        if (s_instance == this) s_instance = null;
    }
}
