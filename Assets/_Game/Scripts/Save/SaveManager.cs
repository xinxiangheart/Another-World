using UnityEngine;

/// <summary>
/// [PLACEHOLDER] Persistent save system for player profile, settings, deck configurations.
/// Uses PlayerPrefs for simple data, JSON file for complex data.
/// </summary>
public static class SaveManager
{
    private const string DECK_KEY = "player_decks";
    private const string SETTINGS_KEY = "game_settings";
    private const string STATS_KEY = "match_history";

    public static void SaveDeck(string name, string[] templateIDs) { /* TODO: 待实现 */ }
    public static string[] LoadDeck(string name) { /* TODO: 待实现 */ return null; }
    public static void SaveSettings() { /* TODO: 待实现 */ }
    public static void LoadSettings() { /* TODO: 待实现 */ }
}
