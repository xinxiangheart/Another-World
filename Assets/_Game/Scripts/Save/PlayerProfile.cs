using System;
using System.Collections.Generic;

/// <summary>
/// [PLACEHOLDER] Player profile data — name, rank, deck collection, match history.
/// </summary>
[Serializable]
public class PlayerProfile
{
    public string playerName = "Player";
    public int matchesPlayed;
    public int matchesWon;
    public int rankPoints;
    public List<string> savedDeckNames = new List<string>();
    public List<MatchRecord> recentMatches = new List<MatchRecord>();
}

[Serializable]
public class MatchRecord
{
    public string opponentName;
    public string deckUsed;
    public bool won;
    public int turnsPlayed;
    public int damageDealt;
    public DateTime date;
}
