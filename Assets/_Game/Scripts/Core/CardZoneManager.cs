using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [PLACEHOLDER] Manages all card zones: Hand, Deck, Graveyard, Exile, Board.
/// Replaces scattered handCards/DeckManager/GraveyardManager logic.
/// </summary>
public enum CardZone { Hand, Deck, Graveyard, Exile, Board }

public class CardZoneManager : MonoBehaviour
{
    public static CardZoneManager Instance { get; private set; }

    public System.Action<CardInstance, CardZone, CardZone> OnCardMoved;

    void Awake() { Instance = this; }

    public void MoveCard(CardInstance ci, CardZone from, CardZone to) { }
    public List<CardInstance> GetCardsInZone(CardZone zone) => new List<CardInstance>();
    public int Count(CardZone zone) => 0;
    public void ShuffleIntoDeck(List<CardInstance> cards) { }
}
