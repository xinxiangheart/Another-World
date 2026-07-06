using UnityEngine;

/// <summary>
/// [PLACEHOLDER] ScriptableObject asset holding all SFX AudioClips mapped by ID.
/// </summary>
[CreateAssetMenu(fileName = "SFXLibrary", menuName = "Game/Audio/SFX Library")]
public class SFXLibrary : ScriptableObject
{
    public AudioClip cardPlay;
    public AudioClip cardDraw;
    public AudioClip damageLight;
    public AudioClip damageHeavy;
    public AudioClip heal;
    public AudioClip shield;
    public AudioClip summon;
    public AudioClip death;
    public AudioClip turnStart;
    public AudioClip victory;
    public AudioClip defeat;
    public AudioClip uiClick;
    public AudioClip uiHover;
}
