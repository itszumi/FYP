// GCGameManager.cs
// Gulam Chor — 1 human (Player 0) vs 4 AI bots.
//
// Mirrors your JutpatiGameManager structure exactly:
//   - allHands        = List<List<Card>>
//   - allHandUI       = List<Dictionary<Card, GameObject>>
//   - cardPrefab      = GCCardDisplay prefab
//   - handAreas[]     = 5 Transforms (0 = human bottom, 1-4 = AI positions)
//   - BotTurnsLoop()  = IEnumerator like Jutpati
//
// ── Scene Hierarchy ───────────────────────────────────────────────────────────
//   GCGameManager (this script + GCDeck component)
//   Canvas
//     HandArea0          Human cards (bottom)
//     HandArea1-4        AI cards (top/sides) — show card backs
//     FaceDownPickPanel  Shown when human must pick from an AI
//       PickPromptText   TMP "Pick a card from CPU X"
//       FaceDownArea     cards spawn here
//     StatusText         TMP — turn info
//     MessageText        TMP — transient pair/pick messages
//     PlayerInfoPanel    5 sub-panels (name + card count)
//     GameOverPanel
//       GameOverTitle    TMP
//       GameOverDetails  TMP
//       RestartButton
//       MainMenuButton
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class GCGameManager : MonoBehaviour
{
    // ── Inspector References ──────────────────────────────────────────────────

    [Header("Deck")]
    public GCDeck deck;

    [Header("Card Prefab")]
    [Tooltip("Prefab with GCCardDisplay + Button + Image — same as your Jutpati cardPrefab.")]
    public GameObject cardPrefab;

    [Header("Hand Areas (0 = Human, 1-4 = CPU)")]
    [Tooltip("5 Transforms. Index 0 = human hand at bottom. 1-4 = AI hands around the table.")]
    public Transform[] handAreas;   // length 5

    [Header("Face-Down Pick Panel")]
    [Tooltip("Panel shown when human must click a face-down card from an AI.")]
    public GameObject faceDownPickPanel;
    public Transform faceDownArea;        // cards spawn here
    public TextMeshProUGUI pickPromptText;

    [Header("Status UI")]
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI messageText;

    [Header("Player Info Panels (0=Human, 1-4=CPU)")]
    [Tooltip("5 panels showing player name + card count. Leave null to skip.")]
    public TextMeshProUGUI[] playerNameLabels;    // length 5
    public TextMeshProUGUI[] playerCardCountLabels; // length 5
    public Image[] playerTurnHighlights;  // length 5 — lit when it's their turn

    [Header("Game Over")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI gameOverTitle;
    public TextMeshProUGUI gameOverDetails;
    public Button restartButton;
    public Button mainMenuButton;

    [Header("AI Settings")]
    public float aiPickDelay = 1.2f;  // seconds before AI picks
    public float aiResultDelay = 0.8f;  // seconds after AI picks (show result)

    // ── Runtime State ─────────────────────────────────────────────────────────

    // allHands[i] = cards in player i's hand
    private List<List<Card>> allHands = new List<List<Card>>();

    // allHandUI[i][card] = the spawned GameObject for that card
    private List<Dictionary<Card, GameObject>> allHandUI = new List<Dictionary<Card, GameObject>>();

    // Which players are still active (have cards)
    private List<int> activePlayers = new List<int>();

    private int currentTurnIndex = 0;   // index into activePlayers
    private bool gameEnded = false;
    private bool waitingForHuman = false;

    // The AI player the human is currently picking from
    private int pickTargetIndex = -1;

    private const int PLAYER_COUNT = 5;  // 0 = human, 1-4 = AI

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    void Start()
    {
        if (restartButton != null) restartButton.onClick.AddListener(RestartGame);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(() => SceneManager.LoadScene("MainMenu"));
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (faceDownPickPanel != null) faceDownPickPanel.SetActive(false);
        if (messageText != null) messageText.gameObject.SetActive(false);

        SetupGame();
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    public void SetupGame()
    {
        StopAllCoroutines();

        gameEnded = false;
        waitingForHuman = false;
        currentTurnIndex = 0;
        pickTargetIndex = -1;

        // Clear all hand areas
        foreach (Transform area in handAreas)
            foreach (Transform child in area) Destroy(child.gameObject);

        if (faceDownPickPanel != null) faceDownPickPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (messageText != null) messageText.gameObject.SetActive(false);

        // Init data structures
        allHands.Clear();
        allHandUI.Clear();
        activePlayers.Clear();

        for (int i = 0; i < PLAYER_COUNT; i++)
        {
            allHands.Add(new List<Card>());
            allHandUI.Add(new Dictionary<Card, GameObject>());
            activePlayers.Add(i);
        }

        // Build and deal 51-card deck
        deck.CreateDeck();
        DealCards();

        // Remove initial pairs from ALL players' hands
        for (int i = 0; i < PLAYER_COUNT; i++)
            RemovePairsFromHand(i);

        // Remove players who already emptied their hand
        RemoveEmptyPlayers();

        // Rebuild all hand UI after pair removal
        RebuildAllHandUI();

        UpdatePlayerInfoPanels();
        SetStatus(GetTurnStatusText());

        Debug.Log("[GC] Game started. Turn: " + CurrentPlayer);
        StartCoroutine(TurnLoop());
    }

    // ── Deal cards round-robin ────────────────────────────────────────────────

    void DealCards()
    {
        int i = 0;
        while (deck.CardsRemaining > 0)
        {
            Card c = deck.DrawCard();
            if (c != null)
                allHands[i % PLAYER_COUNT].Add(c);
            i++;
        }
    }

    // ── Pair removal ─────────────────────────────────────────────────────────

    void RemovePairsFromHand(int playerIndex)
    {
        List<Card> hand = allHands[playerIndex];
        bool found = true;

        while (found)
        {
            found = false;
            for (int i = 0; i < hand.Count; i++)
            {
                for (int j = i + 1; j < hand.Count; j++)
                {
                    if (hand[i].rank == hand[j].rank)
                    {
                        Debug.Log($"[GC] {PlayerName(playerIndex)} removed pair: {hand[i].rank}");
                        hand.RemoveAt(j);
                        hand.RemoveAt(i);
                        found = true;
                        break;
                    }
                }
                if (found) break;
            }
        }
    }

    // ── Build UI for all hands ────────────────────────────────────────────────

    void RebuildAllHandUI()
    {
        for (int i = 0; i < PLAYER_COUNT; i++)
        {
            // Clear existing UI
            foreach (Transform child in handAreas[i]) Destroy(child.gameObject);
            allHandUI[i].Clear();

            foreach (Card c in allHands[i])
                SpawnCardInHand(i, c);
        }
    }

    void SpawnCardInHand(int playerIndex, Card c)
    {
        GameObject obj = Instantiate(cardPrefab, handAreas[playerIndex]);
        GCCardDisplay disp = obj.GetComponent<GCCardDisplay>();

        if (playerIndex == 0)
            disp.SetupFaceUp(c, this);    // human sees their own cards
        else
            disp.SetupFaceUp(c, this);    // AI cards show face-up in their area
                                          // (they'll show face-down only in the pick panel)

        allHandUI[playerIndex][c] = obj;
    }

    // ── Main turn loop (mirrors BotTurnsLoop in Jutpati) ─────────────────────

    IEnumerator TurnLoop()
    {
        while (!gameEnded)
        {
            if (activePlayers.Count <= 1) { EndGame(); yield break; }

            int current = CurrentPlayer;
            int target = GetPickTarget(current);

            if (target == -1) { EndGame(); yield break; }

            SetStatus($"{PlayerName(current)}'s turn — picking from {PlayerName(target)}...");
            HighlightTurn(current);

            if (current == 0)
            {
                // ── Human turn ────────────────────────────────────────────
                ShowFaceDownPickPanel(target);
                waitingForHuman = true;
                while (waitingForHuman) yield return null;
            }
            else
            {
                // ── AI turn ───────────────────────────────────────────────
                yield return new WaitForSeconds(aiPickDelay);

                int pickIndex = Random.Range(0, allHands[target].Count);
                yield return StartCoroutine(ProcessPick(current, target, pickIndex));
            }

            // Remove players who emptied their hand
            RemoveEmptyPlayers();

            if (activePlayers.Count <= 1) { EndGame(); yield break; }

            AdvanceTurn();
            UpdatePlayerInfoPanels();
            SetStatus(GetTurnStatusText());

            yield return new WaitForSeconds(0.2f);
        }
    }

    // ── Face-down pick panel ──────────────────────────────────────────────────

    void ShowFaceDownPickPanel(int targetPlayerIndex)
    {
        pickTargetIndex = targetPlayerIndex;

        // Clear old cards
        foreach (Transform child in faceDownArea) Destroy(child.gameObject);

        if (pickPromptText != null)
            pickPromptText.text = $"Pick a card from {PlayerName(targetPlayerIndex)}!";

        List<Card> targetHand = allHands[targetPlayerIndex];
        for (int i = 0; i < targetHand.Count; i++)
        {
            int capturedIndex = i;
            GameObject obj = Instantiate(cardPrefab, faceDownArea);
            GCCardDisplay disp = obj.GetComponent<GCCardDisplay>();
            disp.SetupFaceDown(targetHand[i], this, capturedIndex);
        }

        faceDownPickPanel.SetActive(true);
    }

    // ── Called by GCCardDisplay when human clicks a face-down card ────────────
    public void OnFaceDownCardClicked(int cardIndex)
    {
        if (!waitingForHuman || pickTargetIndex == -1) return;

        faceDownPickPanel.SetActive(false);
        foreach (Transform child in faceDownArea) Destroy(child.gameObject);

        StartCoroutine(HandleHumanPick(cardIndex));
    }

    IEnumerator HandleHumanPick(int cardIndex)
    {
        yield return StartCoroutine(ProcessPick(0, pickTargetIndex, cardIndex));
        pickTargetIndex = -1;
        waitingForHuman = false;
    }

    // ── Core pick logic ───────────────────────────────────────────────────────

    IEnumerator ProcessPick(int pickerIndex, int targetIndex, int cardIndex)
    {
        // Safety clamp
        cardIndex = Mathf.Clamp(cardIndex, 0, allHands[targetIndex].Count - 1);

        Card picked = allHands[targetIndex][cardIndex];
        allHands[targetIndex].RemoveAt(cardIndex);

        // Remove card UI from target's hand
        if (allHandUI[targetIndex].ContainsKey(picked))
        {
            Destroy(allHandUI[targetIndex][picked]);
            allHandUI[targetIndex].Remove(picked);
        }

        Debug.Log($"[GC] {PlayerName(pickerIndex)} picked [{picked.rank} of {picked.suit}] from {PlayerName(targetIndex)}");

        // Check if picked card pairs with anything in picker's hand
        Card matchedCard = allHands[pickerIndex].FirstOrDefault(c => c.rank == picked.rank);

        if (matchedCard != null)
        {
            // Remove the pair — both cards discarded
            allHands[pickerIndex].Remove(matchedCard);
            if (allHandUI[pickerIndex].ContainsKey(matchedCard))
            {
                Destroy(allHandUI[pickerIndex][matchedCard]);
                allHandUI[pickerIndex].Remove(matchedCard);
            }

            string pairMsg = $"{PlayerName(pickerIndex)} picked {picked.rank} → Pair thrown! 🃏";
            ShowMessage(pairMsg);
            Debug.Log($"[GC] Pair: {picked.rank} of {picked.suit} + {matchedCard.rank} of {matchedCard.suit}");
        }
        else
        {
            // No pair — add to picker's hand
            allHands[pickerIndex].Add(picked);
            SpawnCardInHand(pickerIndex, picked);

            ShowMessage($"{PlayerName(pickerIndex)} picked a card. No pair.");
        }

        UpdatePlayerInfoPanels();
        yield return new WaitForSeconds(aiResultDelay);
    }

    // ── Remove players with empty hands ──────────────────────────────────────

    void RemoveEmptyPlayers()
    {
        List<int> toRemove = new List<int>();

        foreach (int p in activePlayers)
        {
            if (allHands[p].Count == 0)
            {
                toRemove.Add(p);
                ShowMessage($"{PlayerName(p)} is OUT!");
                Debug.Log($"[GC] {PlayerName(p)} emptied hand — OUT.");
            }
        }

        foreach (int p in toRemove)
            activePlayers.Remove(p);

        // Keep turnIndex valid
        if (activePlayers.Count > 0)
            currentTurnIndex = currentTurnIndex % activePlayers.Count;
    }

    // ── Turn helpers ──────────────────────────────────────────────────────────

    void AdvanceTurn()
    {
        if (activePlayers.Count == 0) return;
        currentTurnIndex = (currentTurnIndex + 1) % activePlayers.Count;
    }

    /// <summary>Returns the player index that `current` picks FROM (next in circle).</summary>
    int GetPickTarget(int current)
    {
        int idx = activePlayers.IndexOf(current);
        for (int i = 1; i < activePlayers.Count; i++)
        {
            int candidate = activePlayers[(idx + i) % activePlayers.Count];
            if (allHands[candidate].Count > 0)
                return candidate;
        }
        return -1;
    }

    int CurrentPlayer => activePlayers.Count > 0
        ? activePlayers[currentTurnIndex % activePlayers.Count]
        : -1;

    string PlayerName(int index) => index == 0
        ? (PlayerSession.IsLoggedIn() ? PlayerSession.GetUser() : "You")
        : $"CPU {index}";

    // ── Game over ─────────────────────────────────────────────────────────────

    void EndGame()
    {
        gameEnded = true;

        int loser = activePlayers.Count == 1 ? activePlayers[0] : -1;

        Debug.Log(loser != -1
            ? $"[GC] GAME OVER — {PlayerName(loser)} is the Gulam Chor!"
            : "[GC] GAME OVER — edge case, no single loser.");

        // Currency: human wins = +150, human loses = -150
        if (loser == 0) CurrencyManager.AddCoins(-150);
        else if (loser != -1) CurrencyManager.AddCoins(150);

        // Reveal all AI hands
        for (int i = 1; i < PLAYER_COUNT; i++)
        {
            foreach (var kvp in allHandUI[i])
            {
                Image img = kvp.Value.GetComponent<Image>();
                if (img != null) img.sprite = kvp.Key.cardSprite;
            }
        }

        if (gameOverPanel == null) return;
        gameOverPanel.SetActive(true);

        bool humanLost = loser == 0;

        if (gameOverTitle != null)
            gameOverTitle.text = humanLost
                ? "You are the Gulam Chor! 🃏"
                : loser != -1 ? $"{PlayerName(loser)} is the Gulam Chor!" : "Game Over!";

        if (gameOverDetails != null)
            gameOverDetails.text = humanLost
                ? "You were stuck with the lone Jack. Better luck next time!"
                : $"You escaped! {PlayerName(loser)} got stuck with the Jack. Well played!";
    }

    public void RestartGame()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        SetupGame();
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    void SetStatus(string text)
    {
        if (statusText != null) statusText.text = text;
    }

    string GetTurnStatusText()
    {
        int cp = CurrentPlayer;
        if (cp == -1) return "";
        return cp == 0 ? "YOUR TURN: Pick a card from CPU" : $"{PlayerName(cp)}'s turn...";
    }

    void ShowMessage(string text)
    {
        if (messageText == null) return;
        StopCoroutine(nameof(MessageCoroutine));
        StartCoroutine(MessageCoroutine(text));
    }

    IEnumerator MessageCoroutine(string text)
    {
        messageText.text = text;
        messageText.gameObject.SetActive(true);
        yield return new WaitForSeconds(2f);
        messageText.gameObject.SetActive(false);
    }

    void HighlightTurn(int playerIndex)
    {
        if (playerTurnHighlights == null) return;
        for (int i = 0; i < playerTurnHighlights.Length; i++)
        {
            if (playerTurnHighlights[i] != null)
                playerTurnHighlights[i].enabled = (i == playerIndex);
        }
    }

    void UpdatePlayerInfoPanels()
    {
        for (int i = 0; i < PLAYER_COUNT; i++)
        {
            bool isOut = !activePlayers.Contains(i);

            if (playerNameLabels != null && i < playerNameLabels.Length && playerNameLabels[i] != null)
                playerNameLabels[i].text = i == 0
                    ? $"★ {PlayerName(0)}"
                    : PlayerName(i);

            if (playerCardCountLabels != null && i < playerCardCountLabels.Length && playerCardCountLabels[i] != null)
                playerCardCountLabels[i].text = isOut
                    ? "OUT ✓"
                    : $"{allHands[i].Count} cards";
        }
    }
}