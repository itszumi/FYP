using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class JutpatiGameManager : MonoBehaviour
{
    public Deck deck;
    public GameObject cardPrefab;

    [Header("UI References")]
    public Transform[] handAreas;
    public Transform discardFloor;
    public Transform jokerArea;
    public Text turnStatusText;

    [Header("Game Over Panel")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI gameOverTitle;
    public TextMeshProUGUI gameOverDetail;
    public Button restartBtn;
    public Button homeBtn;

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

    void Start()
    {
        if (gameOverPanel) gameOverPanel.SetActive(false);
        SetupGame();
    }

    public void GoToMenu()
    {
        SceneManager.LoadScene("Menu");
    }

    public void SetupGame()
    {
        StopAllCoroutines();
        gameEnded = false;
        hasDrawn = false;
        currentPlayerIndex = 0;
        selectedCard = null;
        if (turnStatusText) turnStatusText.text = "";
        if (gameOverPanel) gameOverPanel.SetActive(false);

        foreach (var area in handAreas)
            foreach (Transform child in area) Destroy(child.gameObject);
        foreach (Transform child in discardFloor) Destroy(child.gameObject);
        foreach (Transform child in jokerArea) Destroy(child.gameObject);

        allHands.Clear();
        allHandUI.Clear();

        deck.CreateDeck();

        for (int i = 0; i < 5; i++)
        {
            allHands.Add(new List<Card>());
            allHandUI.Add(new Dictionary<Card, GameObject>());
            for (int j = 0; j < 7; j++) AddCardToHand(i, DrawCardForGame());
        }

        jokerCard = DrawCardForGame();
        ShowJokerUI();
        SortPlayerHand();
        UpdateTurnUI();
    }

    // ── Draw ─────────────────────────────────────────────────────────────────

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
        Card nonJoker = allHands[botIndex].FirstOrDefault(c => c.rank != jRank);
        return nonJoker ?? allHands[botIndex][0];
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
        if (display == null) { Debug.LogError("CardDisplay missing!"); return; }
        display.Setup(c, this, (playerIndex == 0), (playerIndex == 0));
        allHandUI[playerIndex].Add(c, newCardObj);
    }

    // ── Player actions ────────────────────────────────────────────────────────

    public void OnDrawButtonClicked()
    {
        if (gameEnded || currentPlayerIndex != 0 || hasDrawn) return;
        AddCardToHand(0, DrawCardForGame());
        hasDrawn = true;
        SortPlayerHand();
        if (CheckWin(0)) EndGame(true, -1);
        else UpdateTurnUI();
    }

    public void OnDiscardPileClicked(CardDisplay floorCard)
    {
        if (gameEnded || currentPlayerIndex != 0 || hasDrawn || floorCard != lastDiscardedDisplay) return;
        if (allHands[0].Any(c => c.rank == floorCard.cardData.rank
                              || c.rank == jokerCard.rank
                              || floorCard.cardData.rank == jokerCard.rank))
        {
            Card cData = floorCard.cardData;
            deck.RemoveFromDiscard(cData);
            Destroy(floorCard.gameObject);
            lastDiscardedDisplay = null;
            AddCardToHand(0, cData);
            hasDrawn = true;
            SortPlayerHand();
            if (CheckWin(0)) EndGame(true, -1);
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
                if (turnStatusText) turnStatusText.text = "JOKER CANNOT BE DISCARDED";
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
        if (c.rank == jokerCard.rank) return;

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

    // ── Bot logic ─────────────────────────────────────────────────────────────

    bool BotShouldTakeDiscard(int botIndex, Card topCard)
    {
        if (topCard == null) return false;
        string jRank = jokerCard.rank;
        if (topCard.rank == jRank) return allHands[botIndex].Count > 0;
        bool hasSameRank = allHands[botIndex].Any(c => c.rank == topCard.rank);
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
                else AddCardToHand(i, DrawCardForGame());
            }
            else AddCardToHand(i, DrawCardForGame());

            if (CheckWin(i)) { EndGame(false, i); yield break; }

            yield return new WaitForSeconds(0.5f);
            PerformDiscard(i, GetBotDiscardCard(i));
        }

        currentPlayerIndex = 0;
        UpdateTurnUI();
    }

    // ── Win check ─────────────────────────────────────────────────────────────

    void SortPlayerHand()
    {
        allHands[0] = allHands[0].OrderBy(c => c.rank).ToList();
        for (int i = 0; i < allHands[0].Count; i++)
            allHandUI[0][allHands[0][i]].transform.SetSiblingIndex(i);
    }

    bool CheckWin(int playerIndex)
    {
        List<Card> hand = allHands[playerIndex];
        if (hand.Count < 8) return false;

        List<string> checklist = hand.Select(c => c.rank).ToList();
        string jRank = jokerCard.rank;

        int jokers = 0;
        for (int i = checklist.Count - 1; i >= 0; i--)
            if (checklist[i] == jRank) { jokers++; checklist.RemoveAt(i); }

        checklist.Sort();
        for (int i = checklist.Count - 2; i >= 0; i--)
            if (checklist[i] == checklist[i + 1])
            {
                checklist.RemoveAt(i + 1);
                checklist.RemoveAt(i);
                i--;
            }

        if (jokers >= checklist.Count)
            return (jokers - checklist.Count) % 2 == 0;

        return false;
    }

    // ── End game ──────────────────────────────────────────────────────────────

    void EndGame(bool humanWon, int botWinner)
    {
        gameEnded = true;

        // Flip all bot cards face-up
        for (int i = 1; i < 5; i++)
            foreach (var kvp in allHandUI[i])
                kvp.Value.GetComponent<Image>().sprite = kvp.Key.cardSprite;

        // Coins
        if (humanWon)
            CurrencyManager.AddCoins(200);
        else
            CurrencyManager.AddCoins(-200);

        // Refresh all coin UI on screen
        var allCurrencyUI = FindObjectsByType<CurrencyUI>(FindObjectsSortMode.None);
        foreach (var ui in allCurrencyUI) ui.Refresh();

        // Legacy status text (hide it)
        if (turnStatusText) turnStatusText.text = "";

        // Show game over panel
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);

            if (gameOverTitle != null)
            {
                gameOverTitle.text = humanWon ? "JUTPATTI!" : $"Bot {botWinner} Wins!";
                gameOverTitle.color = humanWon
                    ? new Color(1f, 0.84f, 0f)      // gold
                    : new Color(1f, 0.35f, 0.35f);   // red
            }

            if (gameOverDetail != null)
            {
                gameOverDetail.text = humanWon
                    ? "+200 coins! Well played!"
                    : "-200 coins. Better luck next time!";
                gameOverDetail.color = humanWon
                    ? new Color(0.6f, 1f, 0.6f)   // light green
                    : new Color(1f, 0.7f, 0.7f);   // light red
            }
        }
    }

    void UpdateTurnUI()
    {
        if (gameEnded || turnStatusText == null) return;
        turnStatusText.text = (currentPlayerIndex == 0)
            ? (hasDrawn ? "YOUR TURN: DISCARD" : "YOUR TURN: DRAW")
            : $"BOT {currentPlayerIndex} TURN...";
    }
}