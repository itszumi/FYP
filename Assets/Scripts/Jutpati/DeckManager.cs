using System.Collections.Generic;
using UnityEngine;


public class DeckManager : MonoBehaviour
{
    [Header("Card Sprites")]
    // Drag your 52 sprites here in the Inspector!
    // Order: Clubs (A-K), Diamonds (A-K), Hearts (A-K), Spades (A-K)
    public List<Sprite> allCardSprites;

    [Header("Game Data")]
    public List<Card> deck = new List<Card>();

    // Suits array for naming
    private string[] suits = { "Clubs", "Diamonds", "Hearts", "Spades" };

    void Start()
    {
        CreateDeck();
        ShuffleDeck();
    }

    // Inside your DeckManager.cs
    public void CreateDeck()
    {
        string[] suits = { "Hearts", "Diamonds", "Clubs", "Spades" };
        // We define the names of the ranks here
        string[] rankNames = { "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K", "A" };

        int spriteIndex = 0;

        for (int i = 0; i < suits.Length; i++)
        {
            for (int r = 0; r < rankNames.Length; r++)
            {
                if (spriteIndex < allCardSprites.Count)
                {
                    // 1. Get the rank name from our array using the index 'r'
                    string rankText = rankNames[r];
                    string suitText = suits[i];

                    // 2. Create the card (Order: Rank, Suit, Sprite)
                    Card newCard = new Card(rankText, suitText, allCardSprites[spriteIndex]);

                    // 3. Add to the deck list
                    deck.Add(newCard);

                    spriteIndex++;
                }
            }
        }
    }

    public void ShuffleDeck()
    {
        // Fisher-Yates Shuffle Algorithm (Standard for card games)
        for (int i = 0; i < deck.Count; i++)
        {
            Card temp = deck[i];
            int randomIndex = Random.Range(i, deck.Count);

            // Swap current card with random card
            deck[i] = deck[randomIndex];
            deck[randomIndex] = temp;
        }
        Debug.Log("Deck Shuffled!");
    }

    // --- CRITICAL ADDITION ---
    // You MUST have this function, or your Game Manager will have red errors.
    public Card DrawCard()
    {
        // 1. Check if deck is empty
        if (deck.Count <= 0)
        {
            Debug.LogWarning("Deck is empty!");
            return null;
        }

        // 2. Get the top card
        Card topCard = deck[0];

        // 3. Remove it so it can't be drawn again
        deck.RemoveAt(0);

        // 4. Return it to the player
        return topCard;
    }
}