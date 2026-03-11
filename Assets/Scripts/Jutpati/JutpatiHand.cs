using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class JutpatiHand : MonoBehaviour
{
    public List<Card> myCards = new List<Card>();
    public bool isBot = false;

    [Header("UI References")]
    public Transform handArea;
    public GameObject cardPrefab;
    public JutpatiGameManager gm;

    private Dictionary<Card, GameObject> cardObjects = new Dictionary<Card, GameObject>();

    public void AddCard(Card c)
    {
        if (c == null) return;
        myCards.Add(c);

        // 1. Spawn the UI Object
        GameObject newCardObj = Instantiate(cardPrefab, handArea);

        // 2. Get the Display component
        CardDisplay display = newCardObj.GetComponent<CardDisplay>();

        // 3. Setup with THREE arguments now: Card, GameManager, and FaceUp status
        // If isBot is true, !isBot is false (Face Down)
        // If isBot is false, !isBot is true (Face Up)
        display.Setup(c, gm, !isBot, !isBot);

        // 4. Track it
        cardObjects.Add(c, newCardObj);
    }

    public void RemoveCard(Card c)
    {
        if (myCards.Contains(c))
        {
            myCards.Remove(c);

            if (cardObjects.ContainsKey(c))
            {
                Destroy(cardObjects[c]);
                cardObjects.Remove(c);
            }
        }
    }

    public Card BotDecideDiscard()
    {
        if (myCards.Count == 0) return null;
        var groups = myCards.GroupBy(c => c.rank);
        foreach (var group in groups)
        {
            if (group.Count() % 2 != 0) return group.First();
        }
        return myCards[0];
    }
}