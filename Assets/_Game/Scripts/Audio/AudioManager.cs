using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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
///
/// 背景音乐：按场景自动起停（见 _sceneMusic），随场景切换淡入淡出，不用在场景里挂任何东西；
/// 音乐只吃主音量（AudioListener.volume），另有 _musicVolume 这一档音乐自己的音量。
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("音效剪辑映射（可选，Inspector 覆盖代码默认映射）")]
    [SerializeField] private SoundClipMapping[] mappings;

    [Header("背景音乐")]
    [Tooltip("音乐音量，乘在主音量之上")]
    [SerializeField] private float musicVolume = 0.55f;
    [Tooltip("进场景淡入时长（秒）")]
    [SerializeField] private float musicFadeIn = 2f;
    [Tooltip("离开场景淡出时长（秒）")]
    [SerializeField] private float musicFadeOut = 1f;

    private Dictionary<SoundEffectType, AudioClip> _clips;
    private AudioSource _sfxSource;
    private AudioSource _musicSource;
    private AudioClip _musicPlaying;
    private Coroutine _musicFade;

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
        (SoundEffectType.Attack,   "Audio/SFX/Attack"),
        (SoundEffectType.AttackHero, "Audio/SFX/Attack"),   // 打英雄复用攻击音效
        (SoundEffectType.ButtonHover, "Audio/SFX/ButtonHover"),
        (SoundEffectType.ButtonClick, "Audio/SFX/ButtonClick"),
    };

    /// <summary>场景 → 该场景的背景音乐（Resources 路径）。没配的场景 = 静默（会把上一场景的音乐淡出）。</summary>
    static readonly (string scene, string path)[] _sceneMusic = new[]
    {
        ("Welcome", "Audio/Music/MenuAmbient"),
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

        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.playOnAwake = false;
        _musicSource.loop = true;
        _musicSource.spatialBlend = 0f; // 2D 音乐
        _musicSource.volume = 0f;

        SceneManager.sceneLoaded += OnSceneLoaded;

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

        // 3. 背景音乐：补第一场景那一次（sceneLoaded 事件在我们订阅之前就发过了）
        ApplySceneMusic(SceneManager.GetActiveScene().name);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ── 背景音乐 ───────────────────────────────────────────────────────────

    /// <summary>
    /// RuntimeInitializeOnLoadMethod(AfterSceneLoad) 在第一场景载入**之后**才跑，
    /// 所以第一个场景的 sceneLoaded 事件我们收不到 —— 这里主动补一次。
    /// </summary>
    void OnSceneLoaded(Scene scene, LoadSceneMode mode) { ApplySceneMusic(scene.name); }

    /// <summary>切到某个场景时该放什么音乐；没配的就淡出静音。</summary>
    public void ApplySceneMusic(string sceneName)
    {
        string path = null;
        foreach (var (scene, p) in _sceneMusic)
            if (scene == sceneName) { path = p; break; }

        if (path == null) { StopMusic(musicFadeOut); return; }

        var clip = Resources.Load<AudioClip>(path);
        if (clip == null) { Debug.LogWarning($"[AudioManager] 未找到音乐资源: Resources/{path}"); return; }
        PlayMusic(clip, musicVolume, musicFadeIn);
    }

    /// <summary>放背景音乐（循环）。已经在放同一首就什么都不做，免得每次回开始界面都从头起。</summary>
    public void PlayMusic(AudioClip clip, float volume = -1f, float fadeIn = 2f)
    {
        if (clip == null || _musicSource == null) return;
        if (volume < 0f) volume = musicVolume;
        if (_musicPlaying == clip && _musicSource.isPlaying) return;

        _musicPlaying = clip;
        _musicSource.clip = clip;
        _musicSource.loop = true;
        _musicSource.volume = fadeIn > 0.0001f ? 0f : volume;
        _musicSource.Play();

        if (_musicFade != null) StopCoroutine(_musicFade);
        _musicFade = StartCoroutine(FadeMusicTo(volume, fadeIn, false));
    }

    /// <summary>淡出并停掉背景音乐。</summary>
    public void StopMusic(float fadeOut = 1f)
    {
        if (_musicSource == null) return;
        _musicPlaying = null;
        if (!_musicSource.isPlaying) return;

        if (_musicFade != null) StopCoroutine(_musicFade);
        _musicFade = StartCoroutine(FadeMusicTo(0f, fadeOut, true));
    }

    IEnumerator FadeMusicTo(float target, float time, bool stopAtEnd)
    {
        float from = _musicSource.volume, t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            _musicSource.volume = Mathf.Lerp(from, target, Mathf.Clamp01(t / Mathf.Max(0.0001f, time)));
            yield return null;
        }
        _musicSource.volume = target;
        if (stopAtEnd) _musicSource.Stop();
        _musicFade = null;
    }

    /// <summary>播放指定音效（未配置则打警告，不报错）。</summary>
    public void Play(SoundEffectType type)
    {
        Play(type, 1f, 1f);
    }

    /// <summary>播放指定音效（带音量 0~1、音调 pitch）。</summary>
    public void Play(SoundEffectType type, float volume = 1f, float pitch = 1f)
    {
        if (_clips != null && _clips.TryGetValue(type, out var clip))
        {
            _sfxSource.pitch = pitch;
            // 音效音量走玩家设置（默认 100%）；主音量由 AudioListener.volume 承担
            _sfxSource.PlayOneShot(clip, volume * GameSettings.SfxVolume);
            _sfxSource.pitch = 1f; // 播完恢复默认，避免影响后续
        }
        else
            Debug.LogWarning($"[AudioManager] 未配置音效: {type}");
    }
}
