
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class JutpatiGameManager : MonoBehaviour
{
    public Deck deck;
    public GameObject cardPrefab;

    [Header("UI References")]
    public Transform[] handAreas;
    public Transform discardFloor;
    public Transform jokerArea;
    public Text turnStatusText;

    private List<List<Card>> allHands = new List<List<Card>>();
    private List<Dictionary<Card, GameObject>> allHandUI = new List<Dictionary<Card, GameObject>>();

    private Card jokerCard;
    private int currentPlayerIndex = 0;
    private Card selectedCard;
    private bool hasDrawn = false;
    private CardDisplay lastDiscardedDisplay;
    private bool gameEnded = false;

    [Header("Discard Visuals")]
    public float discardRadius = 70f;
    public Color discardHighlight = new Color(1f, 1f, 0.8f, 1f);

    void Start() { SetupGame(); }

    public void SetupGame()
    {
        StopAllCoroutines();
        gameEnded = false;
        hasDrawn = false;
        currentPlayerIndex = 0;
        selectedCard = null;
        turnStatusText.text = "";

        foreach (var area in handAreas) { foreach (Transform child in area) Destroy(child.gameObject); }
        foreach (Transform child in discardFloor) Destroy(child.gameObject);
        foreach (Transform child in jokerArea) Destroy(child.gameObject);

        allHands.Clear();
        allHandUI.Clear();

        deck.CreateDeck();

        for (int i = 0; i < 5; i++)
        {
            allHands.Add(new List<Card>());
            allHandUI.Add(new Dictionary<Card, GameObject>());
            for (int j = 0; j < 7; j++) { AddCardToHand(i, DrawCardForGame()); }
        }

        jokerCard = DrawCardForGame();
        ShowJokerUI();
        SortPlayerHand();
        UpdateTurnUI();
    }

    // Draw a card, reshuffling discard pile if needed
    Card DrawCardForGame()
    {
        if (deck.CardsRemaining == 0 && deck.DiscardCount > 0)
        {
            deck.ReshuffleFromDiscard();
            ClearDiscardFloor();
        }

        return deck.DrawCard();
    }
    Card GetBotDiscardCard(int botIndex)
    {
        string jRank = jokerCard.rank;

        // Try to discard a non-joker card first
        Card nonJoker = allHands[botIndex].FirstOrDefault(c => c.rank != jRank);
        if (nonJoker != null) return nonJoker;

        // If hand is only jokers, discard first one
        return allHands[botIndex][0];
    }

    void ClearDiscardFloor()
    {
        foreach (Transform child in discardFloor) Destroy(child.gameObject);
        lastDiscardedDisplay = null;
    }

    void ShowJokerUI()
    {
        GameObject jObj = Instantiate(cardPrefab, jokerArea);
        jObj.GetComponent<CardDisplay>().Setup(jokerCard, this, true, false);
    }

    void AddCardToHand(int playerIndex, Card c)
    {
        if (c == null) return;

        allHands[playerIndex].Add(c);

        GameObject newCardObj = Instantiate(cardPrefab, handAreas[playerIndex]);
        CardDisplay display = newCardObj.GetComponent<CardDisplay>();

        if (display == null)
        {
            Debug.LogError("CardDisplay missing on cardPrefab used in Jutpati!");
            return;
        }

        display.Setup(c, this, (playerIndex == 0), (playerIndex == 0));
        allHandUI[playerIndex].Add(c, newCardObj);
    }

    public void OnDrawButtonClicked()
    {
        if (gameEnded || currentPlayerIndex != 0 || hasDrawn) return;
        AddCardToHand(0, DrawCardForGame());
        hasDrawn = true;
        SortPlayerHand();
        if (CheckWin(0)) EndGame("JUTPATTI! YOU WIN!");
        else UpdateTurnUI();
    }

    public void OnDiscardPileClicked(CardDisplay floorCard)
    {
        if (gameEnded || currentPlayerIndex != 0 || hasDrawn || floorCard != lastDiscardedDisplay) return;

        if (allHands[0].Any(c => c.rank == floorCard.cardData.rank || c.rank == jokerCard.rank || floorCard.cardData.rank == jokerCard.rank))
        {
            Card cData = floorCard.cardData;
            deck.RemoveFromDiscard(cData);
            Destroy(floorCard.gameObject);
            lastDiscardedDisplay = null;
            AddCardToHand(0, cData);
            hasDrawn = true;
            SortPlayerHand();
            if (CheckWin(0)) EndGame("JUTPATTI! YOU WIN!");
            else UpdateTurnUI();
        }
    }

    public void OnCardClicked(Card card)
    {
        if (gameEnded || currentPlayerIndex != 0 || !hasDrawn) return;
        selectedCard = card;

        foreach (var ui in allHandUI[0].Values) ui.GetComponent<Image>().color = Color.white;
        allHandUI[0][card].GetComponent<Image>().color = Color.yellow;
    }

    public void OnDiscardButtonClicked()
    {
        if (currentPlayerIndex == 0 && selectedCard != null)
        {
            if (selectedCard.rank == jokerCard.rank)
            {
                turnStatusText.text = "JOKER CANNOT BE DISCARDED";
                return;
            }
            PerformDiscard(0, selectedCard);
            selectedCard = null;
            hasDrawn = false;
            StartCoroutine(BotTurnsLoop());
        }
    }

    void PerformDiscard(int playerIndex, Card c)
    {
        if (c.rank == jokerCard.rank)
        {
            // Joker can never be thrown
            return;
        }
        allHands[playerIndex].Remove(c);
        if (allHandUI[playerIndex].ContainsKey(c))
        {
            Destroy(allHandUI[playerIndex][c]);
            allHandUI[playerIndex].Remove(c);
        }

        deck.AddToDiscard(c);

        GameObject discardObj = Instantiate(cardPrefab, discardFloor);
        Vector2 offset = Random.insideUnitCircle * discardRadius;
        discardObj.transform.localPosition = new Vector3(offset.x, offset.y, 0);
        discardObj.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

        // Remove highlight from previous top discard
        if (lastDiscardedDisplay != null)
        {
            var oldImg = lastDiscardedDisplay.GetComponent<Image>();
            if (oldImg != null) oldImg.color = discardHighlight;
        }

        lastDiscardedDisplay = discardObj.GetComponent<CardDisplay>();
        lastDiscardedDisplay.Setup(c, this, true, true);
        lastDiscardedDisplay.isOnFloor = true;


        var img = discardObj.GetComponent<Image>();
        if (img != null) img.color = Color.white;
    }
    // Bot should take discard ONLY if it makes a pair (jut)
    bool BotShouldTakeDiscard(int botIndex, Card topCard)
    {
        if (topCard == null) return false;

        string jRank = jokerCard.rank;

        // If top card is a joker, bot should only take it if it already has any single card to pair
        if (topCard.rank == jRank)
        {
            return allHands[botIndex].Count > 0;
        }

        // If bot has same rank -> pair
        bool hasSameRank = allHands[botIndex].Any(c => c.rank == topCard.rank);

        // If bot has joker -> can pair with this card
        bool hasJoker = allHands[botIndex].Any(c => c.rank == jRank);

        return hasSameRank || hasJoker;
    }
    IEnumerator BotTurnsLoop()
    {
        for (int i = 1; i <= 4; i++)
        {
            if (gameEnded) yield break;

            currentPlayerIndex = i;
            UpdateTurnUI();
            yield return new WaitForSeconds(1f);

            // Bot tries to take top discard if matches
            if (lastDiscardedDisplay != null)
            {
                Card top = lastDiscardedDisplay.cardData;

                if (BotShouldTakeDiscard(i, top))
                {
                    deck.RemoveFromDiscard(top);
                    Destroy(lastDiscardedDisplay.gameObject);
                    lastDiscardedDisplay = null;
                    AddCardToHand(i, top);
                }
                else
                {
                    AddCardToHand(i, DrawCardForGame());
                }
            }
            else
            {
                AddCardToHand(i, DrawCardForGame());
            }

            if (CheckWin(i)) { EndGame("BOT " + i + " WINS!"); yield break; }

            yield return new WaitForSeconds(0.5f);
            PerformDiscard(i, GetBotDiscardCard(i));
        }

        currentPlayerIndex = 0;
        UpdateTurnUI();
    }

    void SortPlayerHand()
    {
        allHands[0] = allHands[0].OrderBy(c => c.rank).ToList();
        for (int i = 0; i < allHands[0].Count; i++)
        {
            Card c = allHands[0][i];
            allHandUI[0][c].transform.SetSiblingIndex(i);
        }
    }

    bool CheckWin(int playerIndex)
    {
        List<Card> hand = allHands[playerIndex];
        if (hand.Count < 8) return false;

        List<string> checklist = hand.Select(c => c.rank).ToList();
        string jRank = jokerCard.rank;

        int jokers = 0;
        for (int i = checklist.Count - 1; i >= 0; i--)
        {
            if (checklist[i] == jRank) { jokers++; checklist.RemoveAt(i); }
        }

        checklist.Sort();
        for (int i = checklist.Count - 2; i >= 0; i--)
        {
            if (checklist[i] == checklist[i + 1])
            {
                checklist.RemoveAt(i + 1);
                checklist.RemoveAt(i);
                i--;
            }
        }

        if (jokers >= checklist.Count)
        {
            int remainingJokers = jokers - checklist.Count;
            return remainingJokers % 2 == 0;
        }

        return false;
    }

    void EndGame(string message)
    {
        gameEnded = true;
        turnStatusText.text = message;
        turnStatusText.color = Color.yellow;

        for (int i = 1; i < 5; i++)
        {
            foreach (var kvp in allHandUI[i])
                kvp.Value.GetComponent<Image>().sprite = kvp.Key.cardSprite;
        }
        if (message.Contains("YOU WIN"))
            CurrencyManager.AddCoins(200);
        else if (message.Contains("BOT"))
            CurrencyManager.AddCoins(-200);
    }

    void UpdateTurnUI()
    {
        if (gameEnded) return;
        turnStatusText.text = (currentPlayerIndex == 0) ?
            (hasDrawn ? "YOUR TURN: DISCARD" : "YOUR TURN: DRAW") :
            $"BOT {currentPlayerIndex} TURN...";
    }
}