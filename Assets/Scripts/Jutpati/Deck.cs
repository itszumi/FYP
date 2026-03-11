using System.Collections.Generic;
using UnityEngine;

public class Deck : MonoBehaviour
{
    public List<Card> cards = new List<Card>();
    public List<Sprite> cardSprites; // Assign your 52 sprites here in Inspector

    public void CreateDeck()
    {
        string[] ranks = { "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K", "A" };
        string[] suits = { "Hearts", "Diamonds", "Clubs", "Spades" };

        int spriteIndex = 0;
        foreach (string s in suits)
        {
            foreach (string r in ranks)
            {
                if (spriteIndex < cardSprites.Count)
                {
                    cards.Add(new Card(r, s, cardSprites[spriteIndex]));
                    spriteIndex++;
                }
            }
        }
        Shuffle();
    }


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

    public Card DrawCard()
    {
        if (cards.Count == 0) return null;
        Card c = cards[0];
        cards.RemoveAt(0);
        return c;
    }
}