using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

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
            for (int j = 0; j < 7; j++) { AddCardToHand(i, deck.DrawCard()); }
        }

        jokerCard = deck.DrawCard();
        ShowJokerUI();
        SortPlayerHand();
        UpdateTurnUI();
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
        display.Setup(c, this, (playerIndex == 0), (playerIndex == 0));
        allHandUI[playerIndex].Add(c, newCardObj);
    }

    public void OnDrawButtonClicked()
    {
        if (gameEnded || currentPlayerIndex != 0 || hasDrawn) return;
        AddCardToHand(0, deck.DrawCard());
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
            PerformDiscard(0, selectedCard);
            selectedCard = null;
            hasDrawn = false;
            StartCoroutine(BotTurnsLoop());
        }
    }

    void PerformDiscard(int playerIndex, Card c)
    {
        allHands[playerIndex].Remove(c);
        if (allHandUI[playerIndex].ContainsKey(c))
        {
            Destroy(allHandUI[playerIndex][c]);
            allHandUI[playerIndex].Remove(c);
        }

        GameObject discardObj = Instantiate(cardPrefab, discardFloor);
        discardObj.transform.localPosition = new Vector3(Random.Range(-50, 50), Random.Range(-30, 30), 0);
        lastDiscardedDisplay = discardObj.GetComponent<CardDisplay>();
        lastDiscardedDisplay.Setup(c, this, true, true);
        lastDiscardedDisplay.isOnFloor = true;
    }

    IEnumerator BotTurnsLoop()
    {
        for (int i = 1; i <= 4; i++)
        {
            if (gameEnded) yield break;
            currentPlayerIndex = i;
            UpdateTurnUI();
            yield return new WaitForSeconds(1f);
            AddCardToHand(i, deck.DrawCard());
            if (CheckWin(i)) { EndGame("BOT " + i + " WINS!"); yield break; }
            yield return new WaitForSeconds(0.5f);
            PerformDiscard(i, allHands[i][0]);
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

    // --- MATHEMATICAL WIN CHECK ---
    bool CheckWin(int playerIndex)
    {
        List<Card> hand = allHands[playerIndex];
        if (hand.Count < 8) return false;

        // 1. Create a checklist of ranks
        List<string> checklist = hand.Select(c => c.rank).ToList();
        string jRank = jokerCard.rank;

        // 2. Remove all Jokers (Wildcards)
        int jokers = 0;
        for (int i = checklist.Count - 1; i >= 0; i--)
        {
            if (checklist[i] == jRank) { jokers++; checklist.RemoveAt(i); }
        }

        // 3. Remove all Natural Pairs
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

        // 4. Remaining cards are Singles that MUST use a Joker
        if (jokers >= checklist.Count)
        {
            int remainingJokers = jokers - checklist.Count;
            // Any leftover jokers must be in pairs (e.g. 2 jokers make 1 jut)
            return remainingJokers % 2 == 0;
        }

        return false;
    }

    void EndGame(string message)
    {
        gameEnded = true;
        turnStatusText.text = message;
        turnStatusText.color = Color.yellow;
        // Flip bot cards
        for (int i = 1; i < 5; i++)
        {
            foreach (var kvp in allHandUI[i]) kvp.Value.GetComponent<Image>().sprite = kvp.Key.cardSprite;
        }
    }

    void UpdateTurnUI()
    {
        if (gameEnded) return;
        turnStatusText.text = (currentPlayerIndex == 0) ?
            (hasDrawn ? "YOUR TURN: DISCARD" : "YOUR TURN: DRAW") :
            $"BOT {currentPlayerIndex} TURN...";
    }
}