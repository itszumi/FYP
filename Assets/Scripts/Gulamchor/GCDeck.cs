// GCDeck.cs
// Matches your existing Deck.cs / DeckManager.cs pattern.
// Builds a 51-card deck — Hearts Jack is removed (it becomes the lone "Gulam" card).
// Drag your 52 card sprites into allCardSprites in the Inspector (same order as DeckManager).

using System.Collections.Generic;
using UnityEngine;

public class GCDeck : MonoBehaviour
{
    [Header("Card Sprites — same 52 sprites as your DeckManager, same order")]
    public List<Sprite> allCardSprites;

    // The built deck (51 cards)
    public List<Card> cards = new List<Card>();

    // Which Jack is removed — Hearts Jack
    private const string REMOVED_SUIT = "Hearts";
    private const string REMOVED_RANK = "J";

    // ── Build ─────────────────────────────────────────────────────────────────

    public void CreateDeck()
    {
        cards.Clear();

        string[] suits = { "Hearts", "Diamonds", "Clubs", "Spades" };
        string[] rankNames = { "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K", "A" };

        int spriteIndex = 0;

        foreach (string suit in suits)
        {
            foreach (string rank in rankNames)
            {
                if (spriteIndex < allCardSprites.Count)
                {
                    Sprite spr = allCardSprites[spriteIndex];
                    spriteIndex++;

                    // Skip Hearts J — this becomes the lone Gulam card
                    if (suit == REMOVED_SUIT && rank == REMOVED_RANK)
                    {
                        Debug.Log("[GCDeck] Removed Hearts J from deck (Gulam card).");
                        continue;
                    }

                    cards.Add(new Card(rank, suit, spr));
                }
            }
        }

        Debug.Log($"[GCDeck] Deck created: {cards.Count} cards (should be 51).");
        Shuffle();
    }

    // ── Shuffle ───────────────────────────────────────────────────────────────

    public void Shuffle()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            Card temp = cards[i];
            int randomIndex = Random.Range(i, cards.Count);
            cards[i] = cards[randomIndex];
            cards[randomIndex] = temp;
        }
    }

    // ── Draw ──────────────────────────────────────────────────────────────────

    public Card DrawCard()
    {
        if (cards.Count == 0)
        {
            Debug.LogWarning("[GCDeck] Deck is empty!");
            return null;
        }
        Card top = cards[0];
        cards.RemoveAt(0);
        return top;
    }

    public int CardsRemaining => cards.Count;
}