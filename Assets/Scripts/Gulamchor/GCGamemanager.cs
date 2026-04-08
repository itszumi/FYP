// GCGameManager.cs
// Fixes:
// 1. Deal phase: all cards shown face-up → pairs animate face-up to pile → then bot hands flip down
// 2. Game phase: picked/discarded cards animate face-up to pile
// 3. Dim cards: handled in GCCardDisplay (ForceWhite in Awake+Start)
// 4. "New Text": remove the old MessageText GameObject from your Canvas manually

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class GCGameManager : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Deck")]
    public GCDeck deck;

    [Header("Card Prefab (must have GCCardDisplay)")]
    public GameObject cardPrefab;

    [Header("Hand Areas (0=Human, 1-4=Bot)")]
    public Transform[] handAreas;

    [Header("Center Throw Pile — empty RectTransform at screen center")]
    public RectTransform throwPile;

    [Header("Throw Pile Scatter Range")]
    public float scatterX = 150f;
    public float scatterY = 80f;

    [Header("Turn Text — only UI text during gameplay")]
    public TextMeshProUGUI turnText;

    [Header("Player Card Count Labels (optional, length 5)")]
    public TextMeshProUGUI[] playerCardCountLabels;

    [Header("Game Over")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI gameOverTitle;
    public TextMeshProUGUI gameOverDetails;
    public Button restartButton;
    public Button mainMenuButton;

    [Header("Timing")]
    public float dealShowDuration = 1.2f;  // how long to show all face-up before removing pairs
    public float aiThinkDelay = 1.2f;
    public float aiPickDelay = 0.5f;
    public float slideAnimDuration = 0.35f;

    [Header("Pick Scale")]
    public float pickScaleMultiplier = 1.5f;
    public float pickScaleDuration = 0.25f;

    // ── Runtime ───────────────────────────────────────────────────────────────

    private List<List<Card>> allHands = new List<List<Card>>();
    private List<Dictionary<Card, GameObject>> allHandUI = new List<Dictionary<Card, GameObject>>();
    private List<int> activePlayers = new List<int>();
    private int currentTurnIndex = 0;
    private bool gameEnded = false;
    private bool waitingForHuman = false;
    private int pickTargetIndex = -1;

    private bool waitingForPairDiscard = false;
    private Card pickedCard = null;
    private Card pairMatchCard = null;

    private const int PLAYER_COUNT = 5;

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Start()
    {
        restartButton?.onClick.AddListener(RestartGame);
        mainMenuButton?.onClick.AddListener(() => SceneManager.LoadScene("MainMenu"));

        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (turnText != null) turnText.gameObject.SetActive(false);

        StartCoroutine(RunGame());
    }

    IEnumerator RunGame()
    {
        yield return StartCoroutine(DealPhase());
        yield return new WaitForSeconds(0.8f);
        yield return StartCoroutine(GamePhase());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PHASE 1 — DEAL
    // ─────────────────────────────────────────────────────────────────────────

    IEnumerator DealPhase()
    {
        allHands.Clear();
        allHandUI.Clear();
        activePlayers.Clear();

        for (int i = 0; i < PLAYER_COUNT; i++)
        {
            allHands.Add(new List<Card>());
            allHandUI.Add(new Dictionary<Card, GameObject>());
            activePlayers.Add(i);
        }

        // Deal evenly round-robin
        deck.CreateDeck();
        List<Card> allCards = new List<Card>(deck.cards);
        deck.cards.Clear();
        for (int i = 0; i < allCards.Count; i++)
            allHands[i % PLAYER_COUNT].Add(allCards[i]);

        // ── Show ALL hands face-up so player sees their cards ─────────────────
        RebuildAllHandUI(faceUp: true);
        UpdateCardCounts();

        SetTurnText("Dealing cards...");
        yield return new WaitForSeconds(dealShowDuration);

        SetTurnText("Removing pairs...");

        // Find all pairs across all players
        var allPairs = new List<(int player, Card c1, Card c2)>();

        for (int p = 0; p < PLAYER_COUNT; p++)
        {
            List<Card> hand = allHands[p];
            bool found = true;

            while (found)
            {
                found = false;
                for (int i = 0; i < hand.Count && !found; i++)
                {
                    for (int j = i + 1; j < hand.Count && !found; j++)
                    {
                        if (hand[i].rank == hand[j].rank)
                        {
                            allPairs.Add((p, hand[i], hand[j]));
                            hand.RemoveAt(j);
                            hand.RemoveAt(i);
                            found = true;
                        }
                    }
                }
            }
        }

        // ── Animate all pairs to pile simultaneously — face-up ────────────────
        foreach (var (player, c1, c2) in allPairs)
        {
            StartCoroutine(AnimateCardToThrowPile(player, c1));
            StartCoroutine(AnimateCardToThrowPile(player, c2));
        }

        yield return new WaitForSeconds(slideAnimDuration + 0.5f);

        // ── NOW flip bot hands face-down ──────────────────────────────────────
        for (int p = 1; p < PLAYER_COUNT; p++)
            foreach (var kvp in allHandUI[p])
                kvp.Value.GetComponent<GCCardDisplay>()?.FlipFaceDown();

        RemoveEmptyPlayers();
        UpdateCardCounts();

        SetTurnText("Game starts!");
        yield return new WaitForSeconds(0.8f);
    }

    IEnumerator AnimateCardToThrowPile(int playerIndex, Card card)
    {
        if (!allHandUI[playerIndex].ContainsKey(card)) yield break;

        GameObject obj = allHandUI[playerIndex][card];
        allHandUI[playerIndex].Remove(card);

        // Re-parent so card can move freely across canvas
        obj.transform.SetParent(throwPile.parent, worldPositionStays: true);

        GCCardDisplay disp = obj.GetComponent<GCCardDisplay>();
        if (disp != null)
            // SlideToCenter calls FlipFaceUp() internally before animating
            yield return StartCoroutine(
                disp.SlideToCenter(throwPile, slideAnimDuration, scatterX, scatterY));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PHASE 2 — GAME
    // ─────────────────────────────────────────────────────────────────────────

    IEnumerator GamePhase()
    {
        while (!gameEnded)
        {
            if (activePlayers.Count <= 1) { EndGame(); yield break; }

            int current = CurrentPlayer;
            int target = GetPickTarget(current);
            if (target == -1) { EndGame(); yield break; }

            UpdateCardCounts();

            if (current == 0)
            {
                SetTurnText("Your Turn — pick a card!");
                yield return new WaitForSeconds(0.4f);

                yield return StartCoroutine(ScaleHandArea(target, pickScaleMultiplier, pickScaleDuration));
                EnablePickMode(target);

                waitingForHuman = true;
                while (waitingForHuman) yield return null;

                DisablePickMode(target);
                yield return StartCoroutine(ScaleHandArea(target, 1f, pickScaleDuration));
            }
            else
            {
                SetTurnText($"{PlayerName(current)}'s Turn");
                yield return new WaitForSeconds(aiThinkDelay);

                int pickIdx = Random.Range(0, allHands[target].Count);
                yield return StartCoroutine(BotPick(current, target, pickIdx));
            }

            RemoveEmptyPlayers();
            if (activePlayers.Count <= 1) { EndGame(); yield break; }

            AdvanceTurn();
            UpdateCardCounts();
            yield return new WaitForSeconds(0.2f);
        }
    }

    // ── Scale ─────────────────────────────────────────────────────────────────

    IEnumerator ScaleHandArea(int playerIndex, float targetScale, float duration)
    {
        if (handAreas == null || playerIndex >= handAreas.Length) yield break;
        Transform area = handAreas[playerIndex];
        Vector3 fromScale = area.localScale;
        Vector3 toScale = Vector3.one * targetScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            area.localScale = Vector3.Lerp(fromScale, toScale, elapsed / duration);
            yield return null;
        }
        area.localScale = toScale;
    }

    // ── Pick mode ─────────────────────────────────────────────────────────────

    void EnablePickMode(int targetIndex)
    {
        pickTargetIndex = targetIndex;
        List<Card> hand = allHands[targetIndex];

        for (int i = 0; i < hand.Count; i++)
        {
            Card card = hand[i];
            if (!allHandUI[targetIndex].ContainsKey(card)) continue;
            int idx = i;
            allHandUI[targetIndex][card].GetComponent<GCCardDisplay>()
                ?.SetupFaceDown(card, this, idx);
        }
    }

    void DisablePickMode(int targetIndex)
    {
        foreach (var kvp in allHandUI[targetIndex])
            kvp.Value.GetComponent<GCCardDisplay>()?.SetupAICard(kvp.Key);
    }

    public void OnFaceDownCardClicked(int cardIndex)
    {
        if (!waitingForHuman || pickTargetIndex == -1) return;
        StartCoroutine(HandleHumanPick(cardIndex));
    }

    IEnumerator HandleHumanPick(int cardIndex)
    {
        cardIndex = Mathf.Clamp(cardIndex, 0, allHands[pickTargetIndex].Count - 1);
        pickedCard = allHands[pickTargetIndex][cardIndex];

        allHands[pickTargetIndex].RemoveAt(cardIndex);
        if (allHandUI[pickTargetIndex].ContainsKey(pickedCard))
        {
            Destroy(allHandUI[pickTargetIndex][pickedCard]);
            allHandUI[pickTargetIndex].Remove(pickedCard);
        }

        pairMatchCard = allHands[0].FirstOrDefault(c => c.rank == pickedCard.rank);

        if (pairMatchCard != null)
        {
            // Add picked card to hand and highlight both
            allHands[0].Add(pickedCard);
            GameObject pickedObj = Instantiate(cardPrefab, handAreas[0]);
            GCCardDisplay pickedDisp = pickedObj.GetComponent<GCCardDisplay>();
            pickedDisp?.SetupFaceUp(pickedCard, this);
            pickedDisp?.SetHighlight(true);
            allHandUI[0][pickedCard] = pickedObj;

            if (allHandUI[0].ContainsKey(pairMatchCard))
            {
                GCCardDisplay md = allHandUI[0][pairMatchCard].GetComponent<GCCardDisplay>();
                md?.SetupSelectablePair(pairMatchCard, this, 0);
                md?.SetHighlight(true);
            }
            pickedDisp?.SetupSelectablePair(pickedCard, this, 0);

            SetTurnText($"Pair of {pickedCard.rank}s! Click a highlighted card to discard.");

            waitingForPairDiscard = true;
            while (waitingForPairDiscard) yield return null;
        }
        else
        {
            allHands[0].Add(pickedCard);
            SpawnCardInHand(0, pickedCard, faceUp: true);
            SetTurnText($"You picked {pickedCard.rank} of {pickedCard.suit}. No pair.");
            yield return new WaitForSeconds(1f);
        }

        pickTargetIndex = -1;
        pickedCard = null;
        pairMatchCard = null;
        waitingForHuman = false;
    }

    public void OnPairCardSelected(Card card)
    {
        if (!waitingForPairDiscard) return;
        StartCoroutine(DiscardHumanPair());
    }

    IEnumerator DiscardHumanPair()
    {
        allHands[0].Remove(pickedCard);
        allHands[0].Remove(pairMatchCard);

        foreach (Card c in new[] { pickedCard, pairMatchCard })
        {
            if (!allHandUI[0].ContainsKey(c)) continue;
            GameObject obj = allHandUI[0][c];
            allHandUI[0].Remove(c);
            obj.transform.SetParent(throwPile.parent, worldPositionStays: true);
            StartCoroutine(
                obj.GetComponent<GCCardDisplay>()
                   ?.SlideToCenter(throwPile, slideAnimDuration, scatterX, scatterY)
                ?? EmptyCoroutine());
        }

        SetTurnText($"You discarded a pair of {pickedCard.rank}s!");
        yield return new WaitForSeconds(slideAnimDuration + 0.4f);

        waitingForPairDiscard = false;
    }

    // ── Bot pick ──────────────────────────────────────────────────────────────

    IEnumerator BotPick(int botIndex, int targetIndex, int cardIdx)
    {
        cardIdx = Mathf.Clamp(cardIdx, 0, allHands[targetIndex].Count - 1);

        Card picked = allHands[targetIndex][cardIdx];
        allHands[targetIndex].RemoveAt(cardIdx);

        if (allHandUI[targetIndex].ContainsKey(picked))
        {
            Destroy(allHandUI[targetIndex][picked]);
            allHandUI[targetIndex].Remove(picked);
        }

        yield return new WaitForSeconds(aiPickDelay);

        Card match = allHands[botIndex].FirstOrDefault(c => c.rank == picked.rank);

        if (match != null)
        {
            allHands[botIndex].Remove(match);

            // Animate match card to pile face-up
            if (allHandUI[botIndex].ContainsKey(match))
            {
                GameObject mo = allHandUI[botIndex][match];
                allHandUI[botIndex].Remove(match);
                mo.transform.SetParent(throwPile.parent, worldPositionStays: true);
                // Flip face-up before sliding
                mo.GetComponent<GCCardDisplay>()?.FlipFaceUp();
                StartCoroutine(
                    mo.GetComponent<GCCardDisplay>()
                      ?.SlideToCenter(throwPile, slideAnimDuration, scatterX, scatterY)
                    ?? EmptyCoroutine());
            }

            // Spawn picked card face-up and animate to pile
            GameObject po = Instantiate(cardPrefab, handAreas[botIndex]);
            GCCardDisplay pd = po.GetComponent<GCCardDisplay>();
            pd?.SetupFaceUp(picked, this); // face-up so player can see what was picked
            po.transform.SetParent(throwPile.parent, worldPositionStays: true);
            StartCoroutine(
                pd?.SlideToCenter(throwPile, slideAnimDuration, scatterX, scatterY)
                ?? EmptyCoroutine());

            SetTurnText($"{PlayerName(botIndex)} threw a pair of {picked.rank}s!");
            yield return new WaitForSeconds(slideAnimDuration + 0.5f);
        }
        else
        {
            allHands[botIndex].Add(picked);
            SpawnCardInHand(botIndex, picked, faceUp: false);
            SetTurnText($"{PlayerName(botIndex)} picked a card. No pair.");
            yield return new WaitForSeconds(0.6f);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void RebuildAllHandUI(bool faceUp)
    {
        for (int i = 0; i < PLAYER_COUNT; i++)
        {
            if (handAreas == null || i >= handAreas.Length || handAreas[i] == null) continue;
            foreach (Transform child in handAreas[i]) Destroy(child.gameObject);
            allHandUI[i].Clear();
            foreach (Card c in allHands[i])
                SpawnCardInHand(i, c, faceUp: faceUp || i == 0);
        }
    }

    void SpawnCardInHand(int playerIndex, Card c, bool faceUp)
    {
        if (handAreas == null || playerIndex >= handAreas.Length
            || handAreas[playerIndex] == null) return;

        GameObject obj = Instantiate(cardPrefab, handAreas[playerIndex]);
        GCCardDisplay disp = obj.GetComponent<GCCardDisplay>();
        if (disp == null) return;

        if (faceUp) disp.SetupFaceUp(c, this);
        else disp.SetupAICard(c);

        allHandUI[playerIndex][c] = obj;
    }

    void RemoveEmptyPlayers()
    {
        var toRemove = activePlayers.Where(p => allHands[p].Count == 0).ToList();
        foreach (int p in toRemove)
        {
            activePlayers.Remove(p);
            Debug.Log($"[GC] {PlayerName(p)} is OUT.");
        }
        if (activePlayers.Count > 0)
            currentTurnIndex = currentTurnIndex % activePlayers.Count;
    }

    void AdvanceTurn()
    {
        if (activePlayers.Count == 0) return;
        currentTurnIndex = (currentTurnIndex + 1) % activePlayers.Count;
    }

    int GetPickTarget(int current)
    {
        int idx = activePlayers.IndexOf(current);
        for (int i = 1; i < activePlayers.Count; i++)
        {
            int c = activePlayers[(idx + i) % activePlayers.Count];
            if (allHands[c].Count > 0) return c;
        }
        return -1;
    }

    int CurrentPlayer => activePlayers.Count > 0
        ? activePlayers[currentTurnIndex % activePlayers.Count] : -1;

    string PlayerName(int i) => i == 0
        ? (PlayerSession.IsLoggedIn() ? PlayerSession.GetUser() : "You")
        : $"Bot {i}";

    IEnumerator EmptyCoroutine() { yield break; }

    // ── Game over ─────────────────────────────────────────────────────────────

    void EndGame()
    {
        gameEnded = true;
        int loser = activePlayers.Count == 1 ? activePlayers[0] : -1;

        if (loser == 0) CurrencyManager.AddCoins(-150);
        else if (loser >= 0) CurrencyManager.AddCoins(150);

        for (int i = 1; i < PLAYER_COUNT; i++)
            foreach (var kvp in allHandUI[i])
                kvp.Value.GetComponent<GCCardDisplay>()?.SetupFaceUp(kvp.Key, this);

        if (gameOverPanel != null) gameOverPanel.SetActive(true);

        bool humanLost = loser == 0;
        if (gameOverTitle != null)
            gameOverTitle.text = humanLost
                ? "You are the Gulam Chor!"
                : loser >= 0 ? $"{PlayerName(loser)} is the Gulam Chor!" : "Game Over!";

        if (gameOverDetails != null)
            gameOverDetails.text = humanLost
                ? "Stuck with the lone Jack. Better luck next time!"
                : $"You escaped! {PlayerName(loser)} got the Jack!";

        if (turnText != null) turnText.gameObject.SetActive(false);
    }

    public void RestartGame()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        if (throwPile != null)
            foreach (Transform child in throwPile.parent)
                if (child != throwPile.transform
                    && child.GetComponent<GCCardDisplay>() != null)
                    Destroy(child.gameObject);

        gameEnded = false;
        waitingForHuman = false;
        currentTurnIndex = 0;

        if (turnText != null) turnText.gameObject.SetActive(true);
        StartCoroutine(RunGame());
    }

    // ── UI ────────────────────────────────────────────────────────────────────

    void SetTurnText(string text)
    {
        if (turnText != null) { turnText.gameObject.SetActive(true); turnText.text = text; }
        Debug.Log($"[GC] {text}");
    }

    void UpdateCardCounts()
    {
        if (playerCardCountLabels == null) return;
        for (int i = 0; i < PLAYER_COUNT && i < playerCardCountLabels.Length; i++)
        {
            if (playerCardCountLabels[i] == null) continue;
            playerCardCountLabels[i].text = !activePlayers.Contains(i)
                ? "OUT" : $"{allHands[i].Count}";
        }
    }
}