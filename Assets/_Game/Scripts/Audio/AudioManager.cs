using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AudioManager — 统一音效播放入口。
/// 所有音效必须通过 AudioManager.Play(SoundEffectType) 播放，
/// 禁止在其它地方直接调用 AudioSource.PlayOneShot / Play。
///
/// 自动创建：若场景中未挂载，运行时自动创建 DontDestroyOnLoad 单例（无需手动拖到场景）。
/// 音效映射：由 _defaultMappings 代码配置（Resources 路径），集中在此处管理。
///
/// 新增音效步骤：
///   ① SoundEffectType 加一项枚举；
///   ② 下方 _defaultMappings 加一条 (type, "Resources/Audio/SFX/xxx") 映射；
///   ③ 在对应入口调 AudioManager.Instance?.Play(SoundEffectType.新类型)。
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("音效剪辑映射（可选，Inspector 覆盖代码默认映射）")]
    [SerializeField] private SoundClipMapping[] mappings;

    private Dictionary<SoundEffectType, AudioClip> _clips;
    private AudioSource _sfxSource;

    [System.Serializable]
    public class SoundClipMapping
    {
        public SoundEffectType type;
        public AudioClip clip;
    }

    /// <summary>代码默认映射：type → Resources 路径（相对 Resources 目录）。集中在此维护。</summary>
    static readonly (SoundEffectType type, string resourcePath)[] _defaultMappings = new[]
    {
        (SoundEffectType.DrawCard, "Audio/SFX/DrawCard"),
        // 后续新音效在这里加，例如：
        // (SoundEffectType.PlayCard, "Audio/SFX/PlayCard"),
        // (SoundEffectType.Attack,   "Audio/SFX/Attack"),
    };

    /// <summary>场景未挂载时自动创建单例（游戏启动即生效，无需手动拖场景）。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureExists()
    {
        if (Instance == null)
        {
            var go = new GameObject("AudioManager");
            go.AddComponent<AudioManager>();
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _sfxSource = GetComponent<AudioSource>();
        if (_sfxSource == null) _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;
        _sfxSource.spatialBlend = 0f; // 2D 音效

        _clips = new Dictionary<SoundEffectType, AudioClip>();

        // 1. 代码默认映射（Resources 加载）
        foreach (var (type, path) in _defaultMappings)
        {
            var clip = Resources.Load<AudioClip>(path);
            if (clip != null && !_clips.ContainsKey(type))
                _clips[type] = clip;
            else if (clip == null)
                Debug.LogWarning($"[AudioManager] 未找到音效资源: Resources/{path}");
        }

        // 2. Inspector 映射（覆盖代码默认）
        if (mappings != null)
        {
            foreach (var m in mappings)
                if (m != null && m.clip != null)
                    _clips[m.type] = m.clip;
        }
    }

    /// <summary>播放指定音效（未配置则打警告，不报错）。</summary>
    public void Play(SoundEffectType type)
    {
        if (_clips != null && _clips.TryGetValue(type, out var clip))
            _sfxSource.PlayOneShot(clip);
        else
            Debug.LogWarning($"[AudioManager] 未配置音效: {type}");
    }

    /// <summary>播放指定音效（带音量，0~1）。</summary>
    public void Play(SoundEffectType type, float volume)
    {
        if (_clips != null && _clips.TryGetValue(type, out var clip))
            _sfxSource.PlayOneShot(clip, volume);
        else
            Debug.LogWarning($"[AudioManager] 未配置音效: {type}");
    }
}
