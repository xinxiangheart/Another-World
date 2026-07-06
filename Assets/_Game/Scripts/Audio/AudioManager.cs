using UnityEngine;

/// <summary>
/// [PLACEHOLDER] Simple audio manager — play SFX and music.
/// SFX: one-shot sounds (card play, damage, heal, UI click).
/// Music: looping background tracks (menu, battle, victory).
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioSource _sfxSource;
    [SerializeField] private AudioSource _musicSource;

    void Awake() { Instance = this; DontDestroyOnLoad(gameObject); }

    public void PlaySFX(AudioClip clip, float volume = 1f) { }
    public void PlayMusic(AudioClip clip, bool loop = true) { }
    public void StopMusic() { }
    public void SetMusicVolume(float v) { }
    public void SetSFXVolume(float v) { }
}
