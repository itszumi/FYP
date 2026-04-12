using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
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

    [Header("Throw Pile Scatter Range")]
    public float scatterX = 150f;
    public float scatterY = 80f;

    [Header("Throw Pile Drop Radius — how close to pile center to count as a drop")]
    public float dropRadius = 200f;

    [Header("Turn Text")]
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
    public float dealShowDuration = 1.2f;
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
    private bool waitingForInitialDiscard = false;
    private Card pickedCard = null;
    private Card pairMatchCard = null;

    // Pairs remaining for human to discard in deal phase
    private List<(Card, Card)> _humanInitialPairs = new List<(Card, Card)>();

    // Direct mouse pick
    private bool _pickModeActive = false;
    private int _pickModeTarget = -1;

    // Throw pile tint tracking
    private GameObject _lastThrownA = null;
    private GameObject _lastThrownB = null;

    private const int PLAYER_COUNT = 5;

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Start()
    {
        restartButton?.onClick.AddListener(RestartGame);
        mainMenuButton?.onClick.AddListener(() => SceneManager.LoadScene("MainMenu"));

        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (turnText != null) turnText.gameObject.SetActive(false);

        ClearThrowPile();
        StartCoroutine(RunGame());
    }

    void Update()
    {
        if (!_pickModeActive || !waitingForHuman) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePos = mouse.position.ReadValue();

            List<Card> hand = allHands[_pickModeTarget];
            for (int i = 0; i < hand.Count; i++)
            {
                Card card = hand[i];
                if (!allHandUI[_pickModeTarget].ContainsKey(card)) continue;

                RectTransform rt = allHandUI[_pickModeTarget][card].GetComponent<RectTransform>();
                if (rt == null) continue;

                if (RectTransformUtility.RectangleContainsScreenPoint(rt, mousePos))
                {
                    Debug.Log($"[GC] Hit card {i}: {card.rank} of {card.suit}");
                    _pickModeActive = false;
                    OnFaceDownCardClicked(i);
                    return;
                }
            }
        }
    }

    // ── Check if position is over throw pile (used by drag) ──────────────────
    public bool IsOverThrowPile(Vector3 worldPos)
    {
        if (throwPile == null) return false;
        return Vector3.Distance(worldPos, throwPile.position) < dropRadius;
    }

    IEnumerator RunGame()
    {
        yield return StartCoroutine(DealPhase());
        yield return new WaitForSeconds(0.8f);
        yield return StartCoroutine(GamePhase());
    }

    // ── PHASE 1 — DEAL ────────────────────────────────────────────────────────

    IEnumerator DealPhase()
    {
        allHands.Clear();
        allHandUI.Clear();
        activePlayers.Clear();
        _lastThrownA = null;
        _lastThrownB = null;
        _humanInitialPairs.Clear();

        for (int i = 0; i < PLAYER_COUNT; i++)
        {
            allHands.Add(new List<Card>());
            allHandUI.Add(new Dictionary<Card, GameObject>());
            activePlayers.Add(i);
        }

        deck.CreateDeck();
        List<Card> allCards = new List<Card>(deck.cards);
        deck.cards.Clear();
        for (int i = 0; i < allCards.Count; i++)
            allHands[i % PLAYER_COUNT].Add(allCards[i]);

        RebuildAllHandUI(faceUp: false);
        UpdateCardCounts();

        SetTurnText("Dealing cards...");
        yield return new WaitForSeconds(dealShowDuration);

        SetTurnText("Removing pairs...");

        // Find all pairs for bots (animate to pile)
        var botPairs = new List<(int player, Card c1, Card c2)>();

        // Find human pairs separately
        List<Card> humanHand = allHands[0];
        bool found = true;
        while (found)
        {
            found = false;
            for (int i = 0; i < humanHand.Count && !found; i++)
                for (int j = i + 1; j < humanHand.Count && !found; j++)
                    if (humanHand[i].rank == humanHand[j].rank)
                    {
                        _humanInitialPairs.Add((humanHand[i], humanHand[j]));
                        humanHand.RemoveAt(j);
                        humanHand.RemoveAt(i);
                        found = true;
                    }
        }

        // Find bot pairs
        for (int p = 1; p < PLAYER_COUNT; p++)
        {
            List<Card> hand = allHands[p];
            found = true;
            while (found)
            {
                found = false;
                for (int i = 0; i < hand.Count && !found; i++)
                    for (int j = i + 1; j < hand.Count && !found; j++)
                        if (hand[i].rank == hand[j].rank)
                        {
                            botPairs.Add((p, hand[i], hand[j]));
                            hand.RemoveAt(j);
                            hand.RemoveAt(i);
                            found = true;
                        }
            }
        }

        // Animate bot pairs to pile simultaneously
        foreach (var (player, c1, c2) in botPairs)
        {
            StartCoroutine(AnimateCardToThrowPile(player, c1));
            StartCoroutine(AnimateCardToThrowPile(player, c2));
        }

        yield return new WaitForSeconds(slideAnimDuration + 0.5f);

        // Tint all bot pile cards yellow
        TintAllPileCardsYellow();

        // Put all pair cards back into human hand for self-selection
        foreach (var (c1, c2) in _humanInitialPairs)
        {
            allHands[0].Add(c1);
            allHands[0].Add(c2);
        }

        // Show all human cards face-up, let user find and discard pairs
        if (_humanInitialPairs.Count > 0)
        {
            // Rebuild human hand UI with all cards including pairs
            foreach (Transform child in handAreas[0]) Destroy(child.gameObject);
            allHandUI[0].Clear();
            foreach (Card c in allHands[0])
                SpawnCardInHand(0, c, faceUp: true);

            SetTurnText("Select pairs to discard! Click two matching cards.");
            yield return StartCoroutine(HumanSelfSelectPairs());
        }

        // Rebuild human hand UI after pair removal (remaining cards only)
        foreach (Transform child in handAreas[0]) Destroy(child.gameObject);
        allHandUI[0].Clear();
        foreach (Card c in allHands[0])
            SpawnCardInHand(0, c, faceUp: true);

        // Bot hands face-down
        for (int p = 1; p < PLAYER_COUNT; p++)
            foreach (var kvp in allHandUI[p])
                kvp.Value.GetComponent<GCCardDisplay>()?.FlipFaceDown();

        RemoveEmptyPlayers();
        UpdateCardCounts();

        SetTurnText("Game starts!");
        yield return new WaitForSeconds(0.8f);
    }

    // ── Human self-selects pairs to discard ───────────────────────────────────

    private Card _initialFirstSelected = null;
    private GCCardDisplay _initialFirstSelectedDisp = null;
    private bool _waitingForInitialSelect = false;

    IEnumerator HumanSelfSelectPairs()
    {
        // Count how many pairs need to be discarded
        int pairsLeft = _humanInitialPairs.Count;

        // Make all hand cards clickable for selection
        EnableInitialCardSelection();

        while (pairsLeft > 0)
        {
            _waitingForInitialSelect = true;
            while (_waitingForInitialSelect) yield return null;
            pairsLeft--;

            // Re-enable selection for remaining cards
            if (pairsLeft > 0)
                EnableInitialCardSelection();
        }

        _humanInitialPairs.Clear();
        _initialFirstSelected = null;
        _initialFirstSelectedDisp = null;
    }

    void EnableInitialCardSelection()
    {
        foreach (var kvp in allHandUI[0])
        {
            Card card = kvp.Key;
            GCCardDisplay disp = kvp.Value.GetComponent<GCCardDisplay>();
            if (disp == null) continue;

            // Wire each card button to OnInitialCardClicked
            Button btn = kvp.Value.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                Card captured = card;
                btn.onClick.AddListener(() => OnInitialCardClicked(captured));
                btn.interactable = true;
            }
        }
    }

    public void OnInitialCardClicked(Card card)
    {
        if (!allHandUI[0].ContainsKey(card)) return;
        GCCardDisplay disp = allHandUI[0][card].GetComponent<GCCardDisplay>();

        if (_initialFirstSelected == null)
        {
            // First card selected — highlight it
            _initialFirstSelected = card;
            _initialFirstSelectedDisp = disp;
            disp?.SetPairHighlight(true);
        }
        else if (_initialFirstSelected == card)
        {
            // Same card clicked — deselect
            _initialFirstSelected = null;
            disp?.SetPairHighlight(false);
            _initialFirstSelectedDisp = null;
        }
        else if (_initialFirstSelected.rank == card.rank)
        {
            // Match found — highlight second card and discard both
            disp?.SetPairHighlight(true);
            StartCoroutine(DiscardInitialSelectedPair(
                _initialFirstSelected, card,
                _initialFirstSelectedDisp, disp));
        }
        else
        {
            // Not a match — flash red briefly then deselect
            StartCoroutine(FlashNoMatch(disp));
        }
    }

    IEnumerator FlashNoMatch(GCCardDisplay disp)
    {
        if (disp?.myImage != null)
            disp.myImage.color = new Color(1f, 0.3f, 0.3f, 1f); // red flash
        yield return new WaitForSeconds(0.3f);
        disp?.SetPairHighlight(false);
    }

    IEnumerator DiscardInitialSelectedPair(Card c1, Card c2,
        GCCardDisplay d1, GCCardDisplay d2)
    {
        // Reset selection state
        _initialFirstSelected = null;
        _initialFirstSelectedDisp = null;

        // Remove from hand data
        allHands[0].Remove(c1);
        allHands[0].Remove(c2);

        // Animate both to pile
        GameObject obj1 = allHandUI[0].ContainsKey(c1) ? allHandUI[0][c1] : null;
        GameObject obj2 = allHandUI[0].ContainsKey(c2) ? allHandUI[0][c2] : null;

        allHandUI[0].Remove(c1);
        allHandUI[0].Remove(c2);

        if (obj1 != null)
        {
            obj1.transform.SetParent(throwPile.parent, worldPositionStays: true);
            StartCoroutine(obj1.GetComponent<GCCardDisplay>()
                ?.SlideToCenter(throwPile, slideAnimDuration, scatterX, scatterY)
                ?? EmptyCoroutine());
        }
        if (obj2 != null)
        {
            obj2.transform.SetParent(throwPile.parent, worldPositionStays: true);
            StartCoroutine(obj2.GetComponent<GCCardDisplay>()
                ?.SlideToCenter(throwPile, slideAnimDuration, scatterX, scatterY)
                ?? EmptyCoroutine());
        }

        TintThrowPile(obj1, obj2);
        SetTurnText("Pair discarded! Select next pair.");

        yield return new WaitForSeconds(slideAnimDuration + 0.3f);

        _waitingForInitialSelect = false;
    }

    IEnumerator AnimateCardToThrowPile(int playerIndex, Card card)
    {
        if (!allHandUI[playerIndex].ContainsKey(card)) yield break;
        GameObject obj = allHandUI[playerIndex][card];
        allHandUI[playerIndex].Remove(card);
        obj.transform.SetParent(throwPile.parent, worldPositionStays: true);
        GCCardDisplay disp = obj.GetComponent<GCCardDisplay>();
        if (disp != null)
            yield return StartCoroutine(disp.SlideToCenter(throwPile, slideAnimDuration, scatterX, scatterY));
    }

    // ── Tint helpers ──────────────────────────────────────────────────────────

    void TintAllPileCardsYellow()
    {
        foreach (Transform child in throwPile.parent)
        {
            if (child == throwPile.transform) continue;
            Image img = child.GetComponent<Image>();
            if (img != null) img.color = new Color(1f, 0.95f, 0.75f, 1f);
        }
        _lastThrownA = null;
        _lastThrownB = null;
    }

    void TintThrowPile(GameObject newA, GameObject newB)
    {
        Color lightYellow = new Color(1f, 0.95f, 0.75f, 1f);

        if (_lastThrownA != null)
        {
            Image img = _lastThrownA.GetComponent<Image>();
            if (img != null) img.color = lightYellow;
        }
        if (_lastThrownB != null)
        {
            Image img = _lastThrownB.GetComponent<Image>();
            if (img != null) img.color = lightYellow;
        }

        if (newA != null)
        {
            Image img = newA.GetComponent<Image>();
            if (img != null) img.color = Color.white;
        }
        if (newB != null)
        {
            Image img = newB.GetComponent<Image>();
            if (img != null) img.color = Color.white;
        }

        _lastThrownA = newA;
        _lastThrownB = newB;
    }

    // ── PHASE 2 — GAME ────────────────────────────────────────────────────────

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

                EnablePickMode(target);
                _pickModeActive = true;
                _pickModeTarget = target;

                yield return StartCoroutine(ScaleHandArea(target, pickScaleMultiplier, pickScaleDuration));

                waitingForHuman = true;
                while (waitingForHuman) yield return null;

                _pickModeActive = false;
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

        // Bring to front when scaling up only
        if (targetScale > 1f)
            area.SetAsLastSibling();

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
        Debug.Log($"[GC] EnablePickMode: {PlayerName(targetIndex)} has {allHands[targetIndex].Count} cards");
    }

    void DisablePickMode(int targetIndex)
    {
        pickTargetIndex = -1;
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
            allHands[0].Add(pickedCard);
            GameObject pickedObj = Instantiate(cardPrefab, handAreas[0]);
            GCCardDisplay pickedDisp = pickedObj.GetComponent<GCCardDisplay>();
            pickedDisp?.SetupFaceUp(pickedCard, this);
            allHandUI[0][pickedCard] = pickedObj;

            if (pickedDisp != null)
                yield return StartCoroutine(pickedDisp.PopAnimation());

            // Get match card display
            GCCardDisplay matchDisp = null;
            if (allHandUI[0].ContainsKey(pairMatchCard))
                matchDisp = allHandUI[0][pairMatchCard].GetComponent<GCCardDisplay>();

            // Setup both as gameplay pairs pointing to each other
            pickedDisp?.SetupGameplayPair(pickedCard, this, matchDisp);
            matchDisp?.SetupGameplayPair(pairMatchCard, this, pickedDisp);

            // Set partner references cross-way
            pickedDisp?.SetDragPartner(matchDisp);
            matchDisp?.SetDragPartner(pickedDisp);

            // Highlight both yellow
            pickedDisp?.SetPairHighlight(true);
            matchDisp?.SetPairHighlight(true);

            SetTurnText($"Pair of {pickedCard.rank}s! Click or drag to throw pile.");

            waitingForPairDiscard = true;
            while (waitingForPairDiscard) yield return null;
        }
        else
        {
            allHands[0].Add(pickedCard);
            SpawnCardInHand(0, pickedCard, faceUp: true);

            if (allHandUI[0].ContainsKey(pickedCard))
            {
                GCCardDisplay newDisp = allHandUI[0][pickedCard].GetComponent<GCCardDisplay>();
                if (newDisp != null)
                    yield return StartCoroutine(newDisp.PopAnimation());
            }

            SetTurnText($"You picked {pickedCard.rank} of {pickedCard.suit}. No pair.");
            yield return new WaitForSeconds(0.8f);
        }

        pickTargetIndex = -1;
        pickedCard = null;
        pairMatchCard = null;
        waitingForHuman = false;
    }

    // Called by click OR drag-drop on both initial pairs and gameplay pairs
    public void OnPairCardSelected(Card card)
    {
        if (waitingForInitialDiscard)
        {
            StartCoroutine(DiscardInitialPair());
            return;
        }
        if (waitingForPairDiscard)
            StartCoroutine(DiscardHumanPair());
    }

    IEnumerator DiscardInitialPair()
    {
        Card c1 = pickedCard;
        Card c2 = pairMatchCard;

        allHands[0].Remove(c1);
        allHands[0].Remove(c2);

        GameObject thrownA = null;
        GameObject thrownB = null;
        bool first = true;

        foreach (Card c in new[] { c1, c2 })
        {
            if (!allHandUI[0].ContainsKey(c)) continue;
            GameObject obj = allHandUI[0][c];
            allHandUI[0].Remove(c);
            obj.transform.SetParent(throwPile.parent, worldPositionStays: true);
            StartCoroutine(obj.GetComponent<GCCardDisplay>()
                ?.SlideToCenter(throwPile, slideAnimDuration, scatterX, scatterY)
                ?? EmptyCoroutine());
            if (first) { thrownA = obj; first = false; }
            else thrownB = obj;
        }

        TintThrowPile(thrownA, thrownB);
        yield return new WaitForSeconds(slideAnimDuration + 0.3f);

        waitingForInitialDiscard = false;
    }

    IEnumerator DiscardHumanPair()
    {
        allHands[0].Remove(pickedCard);
        allHands[0].Remove(pairMatchCard);

        GameObject thrownA = null;
        GameObject thrownB = null;
        bool first = true;

        foreach (Card c in new[] { pickedCard, pairMatchCard })
        {
            if (!allHandUI[0].ContainsKey(c)) continue;
            GameObject obj = allHandUI[0][c];
            allHandUI[0].Remove(c);
            obj.transform.SetParent(throwPile.parent, worldPositionStays: true);
            StartCoroutine(obj.GetComponent<GCCardDisplay>()
                ?.SlideToCenter(throwPile, slideAnimDuration, scatterX, scatterY)
                ?? EmptyCoroutine());
            if (first) { thrownA = obj; first = false; }
            else thrownB = obj;
        }

        SetTurnText($"You discarded a pair of {pickedCard.rank}s!");
        TintThrowPile(thrownA, thrownB);

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

            GameObject matchObj = null;
            if (allHandUI[botIndex].ContainsKey(match))
            {
                matchObj = allHandUI[botIndex][match];
                allHandUI[botIndex].Remove(match);
                matchObj.transform.SetParent(throwPile.parent, worldPositionStays: true);
                matchObj.GetComponent<GCCardDisplay>()?.FlipFaceUp();
                StartCoroutine(matchObj.GetComponent<GCCardDisplay>()
                    ?.SlideToCenter(throwPile, slideAnimDuration, scatterX, scatterY)
                    ?? EmptyCoroutine());
            }

            GameObject po = Instantiate(cardPrefab, handAreas[botIndex]);
            GCCardDisplay pd = po.GetComponent<GCCardDisplay>();
            pd?.SetupFaceUp(picked, this);
            po.transform.SetParent(throwPile.parent, worldPositionStays: true);
            StartCoroutine(pd?.SlideToCenter(throwPile, slideAnimDuration, scatterX, scatterY)
                ?? EmptyCoroutine());

            SetTurnText($"{PlayerName(botIndex)} threw a pair of {picked.rank}s!");
            TintThrowPile(po, matchObj);

            yield return new WaitForSeconds(slideAnimDuration + 0.5f);
        }
        else
        {
            allHands[botIndex].Add(picked);
            SpawnCardInHand(botIndex, picked, faceUp: false);
            SetTurnText($"{PlayerName(botIndex)} picked a card.");
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
        if (handAreas == null || playerIndex >= handAreas.Length || handAreas[playerIndex] == null) return;
        if (cardPrefab == null) { Debug.LogError("[GC] cardPrefab is NULL!"); return; }
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
        foreach (int p in toRemove) activePlayers.Remove(p);
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
        ClearThrowPile();
        gameEnded = false;
        waitingForHuman = false;
        waitingForInitialDiscard = false;
        waitingForPairDiscard = false;
        _pickModeActive = false;
        currentTurnIndex = 0;
        _lastThrownA = null;
        _lastThrownB = null;
        if (turnText != null) turnText.gameObject.SetActive(true);
        StartCoroutine(RunGame());
    }

    void ClearThrowPile()
    {
        if (throwPile == null) return;
        var toDestroy = new List<GameObject>();
        foreach (Transform child in throwPile.parent)
        {
            if (child == throwPile.transform) continue;
            if (child.gameObject == cardPrefab) continue;
            if (child.GetComponent<GCCardDisplay>() != null)
                toDestroy.Add(child.gameObject);
        }
        foreach (var go in toDestroy) Destroy(go);
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
        {
            if (playerCardCountLabels[i] == null) continue;
            playerCardCountLabels[i].text = !activePlayers.Contains(i)
                ? "OUT" : $"{allHands[i].Count}";
        }
    }
}