// GCGameManager.cs — Gulam Chor full implementation
// Human turn: target bot hand ZOOMS in place, cards become clickable
// Pairs animate to center throw pile with slide+rotation

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class GCGameManager : MonoBehaviour
{
    [Header("Deck")]
    public GCDeck deck;
    [Header("Card Prefab")]
    public GameObject cardPrefab;
    [Header("Hand Areas (0=Human, 1-4=Bot)")]
    public Transform[] handAreas;
    [Header("Center Throw Pile")]
    public RectTransform throwPile;
    [Header("Turn Text")]
    public TextMeshProUGUI turnText;
    [Header("Player Card Count Labels (length 5, optional)")]
    public TextMeshProUGUI[] playerCardCountLabels;
    [Header("Game Over")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI gameOverTitle;
    public TextMeshProUGUI gameOverDetails;
    public Button restartButton;
    public Button mainMenuButton;
    [Header("Timing")]
    public float dealPairDelay = 0.35f;
    public float aiThinkDelay = 1.1f;
    public float aiPickDelay = 0.5f;
    public float slideAnimDuration = 0.4f;
    public float handScaleUp = 1.4f;
    public float handScaleDuration = 0.25f;

    private List<List<Card>> allHands = new List<List<Card>>();
    private List<Dictionary<Card, GameObject>> allHandUI = new List<Dictionary<Card, GameObject>>();
    private List<int> activePlayers = new List<int>();
    private int currentTurnIndex = 0;
    private bool gameEnded = false;
    private bool waitingForHuman = false;
    private bool waitingForPair = false;
    private int pickTargetIndex = -1;
    private Card pickedCard = null;
    private Card pairMatchCard = null;

    private const int PLAYER_COUNT = 5;

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
        yield return new WaitForSeconds(0.6f);
        yield return StartCoroutine(GamePhase());
    }

    // ── PHASE 1: DEAL ─────────────────────────────────────────────────────────

    IEnumerator DealPhase()
    {
        allHands.Clear(); allHandUI.Clear(); activePlayers.Clear();
        for (int i = 0; i < PLAYER_COUNT; i++)
        {
            allHands.Add(new List<Card>());
            allHandUI.Add(new Dictionary<Card, GameObject>());
            activePlayers.Add(i);
        }

        deck.CreateDeck();
        var allCards = new List<Card>(deck.cards);
        deck.cards.Clear();
        for (int i = 0; i < allCards.Count; i++)
            allHands[i % PLAYER_COUNT].Add(allCards[i]);

        // Show all cards face-up initially
        RebuildAllHandUI(dealPhase: true);
        SetTurnText("Removing pairs...");
        yield return new WaitForSeconds(0.8f);

        // Collect all pairs
        var allPairs = new List<(int player, Card c1, Card c2)>();
        for (int p = 0; p < PLAYER_COUNT; p++)
        {
            var hand = allHands[p];
            bool found = true;
            while (found)
            {
                found = false;
                for (int i = 0; i < hand.Count && !found; i++)
                    for (int j = i + 1; j < hand.Count && !found; j++)
                        if (hand[i].rank == hand[j].rank)
                        {
                            allPairs.Add((p, hand[i], hand[j]));
                            hand.RemoveAt(j); hand.RemoveAt(i);
                            found = true;
                        }
            }
        }

        // Animate all pairs — simultaneously per player
        foreach (var group in allPairs.GroupBy(x => x.player))
        {
            foreach (var (player, c1, c2) in group)
            {
                StartCoroutine(SendToThrowPile(player, c1));
                StartCoroutine(SendToThrowPile(player, c2));
            }
            yield return new WaitForSeconds(slideAnimDuration + dealPairDelay);
        }

        yield return new WaitForSeconds(0.4f);

        // Flip bots face-down
        for (int p = 1; p < PLAYER_COUNT; p++)
            foreach (var kvp in allHandUI[p])
                kvp.Value.GetComponent<GCCardDisplay>()?.FlipFaceDown();

        RemoveEmptyPlayers();
        UpdateCardCounts();
        SetTurnText("Game starts!");
        yield return new WaitForSeconds(0.8f);
    }

    IEnumerator SendToThrowPile(int playerIndex, Card card)
    {
        if (!allHandUI[playerIndex].ContainsKey(card)) yield break;
        var obj = allHandUI[playerIndex][card];
        var disp = obj.GetComponent<GCCardDisplay>();
        allHandUI[playerIndex].Remove(card);
        obj.transform.SetParent(throwPile.parent, worldPositionStays: true);
        if (disp != null) yield return StartCoroutine(disp.SlideToCenter(throwPile, slideAnimDuration));
    }

    // ── PHASE 2: GAME ─────────────────────────────────────────────────────────

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
                SetTurnText("Your Turn — Pick a card!");

                // Zoom target hand in place and make clickable
                yield return StartCoroutine(ZoomHand(target, true));
                MakeHandClickable(target);

                pickTargetIndex = target;
                waitingForHuman = true;
                while (waitingForHuman) yield return null;

                yield return StartCoroutine(ZoomHand(target, false));
            }
            else
            {
                SetTurnText($"{PlayerName(current)}'s Turn");
                yield return new WaitForSeconds(aiThinkDelay);
                int pick = Random.Range(0, allHands[target].Count);
                yield return StartCoroutine(BotPick(current, target, pick));
            }

            RemoveEmptyPlayers();
            if (activePlayers.Count <= 1) { EndGame(); yield break; }

            AdvanceTurn();
            UpdateCardCounts();
            yield return new WaitForSeconds(0.2f);
        }
    }

    IEnumerator ZoomHand(int playerIndex, bool zoomIn)
    {
        if (handAreas == null || playerIndex >= handAreas.Length) yield break;
        Transform hand = handAreas[playerIndex];
        float start = hand.localScale.x;
        float end = zoomIn ? handScaleUp : 1f;
        float e = 0f;
        while (e < handScaleDuration)
        {
            e += Time.deltaTime;
            float s = Mathf.Lerp(start, end, e / handScaleDuration);
            hand.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        hand.localScale = new Vector3(end, end, 1f);
    }

    void MakeHandClickable(int playerIndex)
    {
        var hand = allHands[playerIndex];
        for (int i = 0; i < hand.Count; i++)
        {
            if (!allHandUI[playerIndex].ContainsKey(hand[i])) continue;
            int idx = i;
            allHandUI[playerIndex][hand[i]].GetComponent<GCCardDisplay>()
                ?.SetupFaceDown(hand[i], this, idx);
        }
    }

    void ResetHandNonClickable(int playerIndex)
    {
        foreach (var kvp in allHandUI[playerIndex])
            kvp.Value.GetComponent<GCCardDisplay>()?.SetupAICard(kvp.Key);
    }

    // Called by GCCardDisplay when human clicks a face-down card
    public void OnFaceDownCardClicked(int cardIndex)
    {
        if (!waitingForHuman) return;
        ResetHandNonClickable(pickTargetIndex);
        StartCoroutine(HandleHumanPick(cardIndex));
    }

    IEnumerator HandleHumanPick(int cardIndex)
    {
        int target = pickTargetIndex;
        cardIndex = Mathf.Clamp(cardIndex, 0, allHands[target].Count - 1);

        pickedCard = allHands[target][cardIndex];
        allHands[target].RemoveAt(cardIndex);

        if (allHandUI[target].ContainsKey(pickedCard))
        {
            Destroy(allHandUI[target][pickedCard]);
            allHandUI[target].Remove(pickedCard);
        }

        pairMatchCard = allHands[0].FirstOrDefault(c => c.rank == pickedCard.rank);

        if (pairMatchCard != null)
        {
            // Add picked card + highlight both for tap-to-discard
            allHands[0].Add(pickedCard);
            var pObj = SpawnCard(0, pickedCard, faceUp: true);
            pObj?.GetComponent<GCCardDisplay>()?.SetupSelectablePair(pickedCard, this, 0);
            pObj?.GetComponent<GCCardDisplay>()?.SetHighlight(true);

            if (allHandUI[0].ContainsKey(pairMatchCard))
            {
                var md = allHandUI[0][pairMatchCard].GetComponent<GCCardDisplay>();
                md?.SetupSelectablePair(pairMatchCard, this, 0);
                md?.SetHighlight(true);
            }

            SetTurnText($"Got {pickedCard.rank}! Tap the pair to discard.");
            waitingForPair = true;
            while (waitingForPair) yield return null;
        }
        else
        {
            allHands[0].Add(pickedCard);
            SpawnCard(0, pickedCard, faceUp: true);
            SetTurnText($"Picked {pickedCard.rank} of {pickedCard.suit}. No pair.");
            yield return new WaitForSeconds(1f);
        }

        pickTargetIndex = -1;
        pickedCard = pairMatchCard = null;
        waitingForHuman = false;
    }

    public void OnPairCardSelected(Card card)
    {
        if (!waitingForPair) return;
        StartCoroutine(DiscardHumanPair());
    }

    IEnumerator DiscardHumanPair()
    {
        Card pc = pickedCard, mc = pairMatchCard;
        allHands[0].Remove(pc);
        allHands[0].Remove(mc);

        foreach (Card c in new[] { pc, mc })
        {
            if (!allHandUI[0].ContainsKey(c)) continue;
            var obj = allHandUI[0][c];
            allHandUI[0].Remove(c);
            obj.transform.SetParent(throwPile.parent, worldPositionStays: true);
            StartCoroutine(obj.GetComponent<GCCardDisplay>()?.SlideToCenter(throwPile, slideAnimDuration) ?? Empty());
        }

        SetTurnText($"You discarded a pair of {pc.rank}s!");
        yield return new WaitForSeconds(slideAnimDuration + 0.3f);
        waitingForPair = false;
    }

    IEnumerator BotPick(int botIdx, int targetIdx, int cardIdx)
    {
        cardIdx = Mathf.Clamp(cardIdx, 0, allHands[targetIdx].Count - 1);
        Card picked = allHands[targetIdx][cardIdx];
        allHands[targetIdx].RemoveAt(cardIdx);

        if (allHandUI[targetIdx].ContainsKey(picked))
        {
            Destroy(allHandUI[targetIdx][picked]);
            allHandUI[targetIdx].Remove(picked);
        }

        yield return new WaitForSeconds(aiPickDelay);

        Card match = allHands[botIdx].FirstOrDefault(c => c.rank == picked.rank);
        if (match != null)
        {
            allHands[botIdx].Remove(match);
            if (allHandUI[botIdx].ContainsKey(match))
            {
                var mo = allHandUI[botIdx][match];
                allHandUI[botIdx].Remove(match);
                mo.transform.SetParent(throwPile.parent, worldPositionStays: true);
                StartCoroutine(mo.GetComponent<GCCardDisplay>()?.SlideToCenter(throwPile, slideAnimDuration) ?? Empty());
            }
            var po = Instantiate(cardPrefab, throwPile.parent);
            po.GetComponent<GCCardDisplay>()?.SetupAICard(picked);
            StartCoroutine(po.GetComponent<GCCardDisplay>()?.SlideToCenter(throwPile, slideAnimDuration) ?? Empty());
            SetTurnText($"{PlayerName(botIdx)} threw a pair of {picked.rank}s!");
            yield return new WaitForSeconds(slideAnimDuration + 0.4f);
        }
        else
        {
            allHands[botIdx].Add(picked);
            SpawnCard(botIdx, picked, faceUp: false);
            SetTurnText($"{PlayerName(botIdx)} picked a card. No pair.");
            yield return new WaitForSeconds(0.6f);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void RebuildAllHandUI(bool dealPhase)
    {
        for (int i = 0; i < PLAYER_COUNT; i++)
        {
            if (handAreas == null || i >= handAreas.Length || handAreas[i] == null) continue;
            foreach (Transform child in handAreas[i]) Destroy(child.gameObject);
            allHandUI[i].Clear();
            foreach (Card c in allHands[i]) SpawnCard(i, c, faceUp: dealPhase || i == 0);
        }
    }

    GameObject SpawnCard(int playerIndex, Card c, bool faceUp)
    {
        if (handAreas == null || playerIndex >= handAreas.Length || handAreas[playerIndex] == null) return null;
        var obj = Instantiate(cardPrefab, handAreas[playerIndex]);
        var disp = obj.GetComponent<GCCardDisplay>();
        if (disp == null) { Debug.LogError("[GC] GCCardDisplay missing!"); return obj; }
        if (faceUp) disp.SetupFaceUp(c, this);
        else disp.SetupAICard(c);
        allHandUI[playerIndex][c] = obj;
        return obj;
    }

    void RemoveEmptyPlayers()
    {
        foreach (int p in activePlayers.Where(p => allHands[p].Count == 0).ToList())
        {
            activePlayers.Remove(p);
            SetTurnText($"{PlayerName(p)} is OUT!");
        }
        if (activePlayers.Count > 0) currentTurnIndex %= activePlayers.Count;
    }

    void AdvanceTurn()
    {
        if (activePlayers.Count > 0) currentTurnIndex = (currentTurnIndex + 1) % activePlayers.Count;
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

    int CurrentPlayer => activePlayers.Count > 0 ? activePlayers[currentTurnIndex % activePlayers.Count] : -1;

    string PlayerName(int i) => i == 0 ? (PlayerSession.IsLoggedIn() ? PlayerSession.GetUser() : "You") : $"Bot {i}";

    IEnumerator Empty() { yield break; }

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
        bool hl = loser == 0;
        if (gameOverTitle != null) gameOverTitle.text = hl ? "You are the Gulam Chor!" : loser >= 0 ? $"{PlayerName(loser)} is the Gulam Chor!" : "Game Over!";
        if (gameOverDetails != null) gameOverDetails.text = hl ? "Stuck with the lone Jack. Better luck next time!" : $"You escaped! {PlayerName(loser)} got the Jack!";
        if (turnText != null) turnText.gameObject.SetActive(false);
    }

    public void RestartGame()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        gameEnded = waitingForHuman = waitingForPair = false;
        currentTurnIndex = 0;
        if (turnText != null) turnText.gameObject.SetActive(true);
        StartCoroutine(RunGame());
    }

    void SetTurnText(string text)
    {
        if (turnText != null) { turnText.gameObject.SetActive(true); turnText.text = text; }
        Debug.Log($"[GC] {text}");
    }

    void UpdateCardCounts()
    {
        if (playerCardCountLabels == null) return;
        for (int i = 0; i < PLAYER_COUNT && i < playerCardCountLabels.Length; i++)
            if (playerCardCountLabels[i] != null)
                playerCardCountLabels[i].text = !activePlayers.Contains(i) ? "OUT" : $"{allHands[i].Count}";
    }
}