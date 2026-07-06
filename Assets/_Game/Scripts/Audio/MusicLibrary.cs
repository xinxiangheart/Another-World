using UnityEngine;

/// <summary>
/// [PLACEHOLDER] ScriptableObject asset holding all music tracks mapped by ID.
/// </summary>
[CreateAssetMenu(fileName = "MusicLibrary", menuName = "Game/Audio/Music Library")]
public class MusicLibrary : ScriptableObject
{
    public AudioClip mainMenu;
    public AudioClip lobby;
    public AudioClip battleNormal;
    public AudioClip battleTense;    // when HP is low
    public AudioClip victory;
    public AudioClip defeat;
}
