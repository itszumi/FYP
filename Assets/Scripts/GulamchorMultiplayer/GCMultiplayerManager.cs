// GCMultiplayerManager.cs
// The networked version of GCGameManager.
// Master Client deals cards and controls turn order.
// Each player sees their own cards face-up, opponents face-down.
// RPC calls sync all game actions across devices.
//
// Attach to a Manager GameObject in your GCMultiplayer scene (same layout as single player).

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class GCMultiplayerManager : MonoBehaviourPunCallbacks
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Deck")]
    public GCDeck deck;

    [Header("Card Prefab")]
    public GameObject cardPrefab;

    [Header("Hand Areas (0=Local Player, 1..N=Others in join order)")]
    public Transform[] handAreas;           // size 5 max

    [Header("Center Throw Pile")]
    public RectTransform throwPile;

    [Header("Scatter")]
    public float scatterX = 150f;
    public float scatterY = 80f;
    public float dropRadius = 200f;

    [Header("UI")]
    public TextMeshProUGUI turnText;
    public TextMeshProUGUI[] playerNameLabels;    // shown above each hand area
    public TextMeshProUGUI[] playerCardCountLabels;

    [Header("Game Over")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI gameOverTitle;
    public TextMeshProUGUI gameOverDetails;
    public Button mainMenuButton;

    [Header("Timing")]
    public float dealShowDuration = 1.0f;
    public float slideAnimDuration = 0.35f;
    public float aiPickDelay = 1.2f;
    public float pickScaleMultiplier = 1.5f;
    public float pickScaleDuration = 0.25f;

    // ── Runtime ───────────────────────────────────────────────────────────────

    // playerOrder[i] = Photon ActorNumber of the player at seat i
    // Seat 0 is always the local player.
    private List<int> _playerOrder = new List<int>();   // actor numbers in turn order
    private List<int> _activePlayers = new List<int>();  // actor numbers still in game

    // Local card data — ONLY filled for local player's hand
    // For opponents we only track count + GameObject references
    private List<Card> _myHand = new List<Card>();
    private Dictionary<Card, GameObject> _myHandUI = new Dictionary<Card, GameObject>();

    // Opponent card GameObjects (face-down) keyed by actor number → list of GOs
    private Dictionary<int, List<GameObject>> _opponentCardGOs =
        new Dictionary<int, List<GameObject>>();

    // Opponent hand counts (synced via RPC)
    private Dictionary<int, int> _opponentCardCounts =
        new Dictionary<int, int>();

    // Turn tracking
    private int _currentTurnActorNumber = -1;
    private int _pickTargetActorNumber = -1;
    private bool _waitingForMyPick = false;
    private bool _waitingForPairDiscard = false;
    private bool _gameEnded = false;

    // Initial pair selection
    private Card _initialFirstSelected = null;
    private GCCardDisplay _initialFirstDisp = null;

    // Gameplay pair
    private Card _pickedCard = null;
    private Card _pairMatchCard = null;

    // Throw pile tint
    private GameObject _lastThrownA = null;
    private GameObject _lastThrownB = null;

    // Direct mouse pick
    private bool _pickModeActive = false;

    // Map actor number → seat index (0 = local)
    private Dictionary<int, int> _actorToSeat = new Dictionary<int, int>();

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Start()
    {
        mainMenuButton?.onClick.AddListener(GoToMainMenu);
        if (gameOverPanel) gameOverPanel.SetActive(false);
        if (turnText)
        {
            turnText.gameObject.SetActive(false);
            turnText.text = ""; // clear any leftover default text
        }

        // Clear any stacked default text labels in the scene
        var allTMPs = FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None);
        foreach (var tmp in allTMPs)
            if (tmp.text == "New Text") tmp.text = "";

        ClearThrowPile();
        BuildPlayerOrder();
        SetupPlayerLabels();

        // Fix throw pile — anchor to canvas center with fixed size
        // Cards spawn as children of throwPile so position is always relative to throwPile center
        if (throwPile != null)
        {
            throwPile.anchorMin = new Vector2(0.5f, 0.5f);
            throwPile.anchorMax = new Vector2(0.5f, 0.5f);
            throwPile.pivot = new Vector2(0.5f, 0.5f);
            throwPile.anchoredPosition = Vector2.zero;
            throwPile.sizeDelta = new Vector2(300f, 300f);
        }

        if (PhotonNetwork.IsMasterClient)
            StartCoroutine(DealPhase());
        else
            SetTurnText("Waiting for host to deal...");
    }

    void Update()
    {
        if (!_pickModeActive || !_waitingForMyPick) return;

        var mouse = Mouse.current;
        if (mouse == null) return;
        if (!mouse.leftButton.wasPressedThisFrame) return;

        Vector2 mousePos = mouse.position.ReadValue();

        // Check if click lands on any face-down card in pick target's area
        if (!_opponentCardGOs.ContainsKey(_pickTargetActorNumber)) return;
        var cards = _opponentCardGOs[_pickTargetActorNumber];

        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] == null) continue;
            RectTransform rt = cards[i].GetComponent<RectTransform>();
            if (rt == null) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(rt, mousePos))
            {
                _pickModeActive = false;
                StartCoroutine(HandleMyPick(i));
                return;
            }
        }
    }

    // ── Build player order ────────────────────────────────────────────────────

    void BuildPlayerOrder()
    {
        _playerOrder.Clear();
        _actorToSeat.Clear();
        _opponentCardGOs.Clear();
        _opponentCardCounts.Clear();

        // Local player is always seat 0
        _playerOrder.Add(PhotonNetwork.LocalPlayer.ActorNumber);

        // Add others in actor number order for consistency
        foreach (var p in PhotonNetwork.PlayerList.OrderBy(p => p.ActorNumber))
            if (p.ActorNumber != PhotonNetwork.LocalPlayer.ActorNumber)
                _playerOrder.Add(p.ActorNumber);

        for (int i = 0; i < _playerOrder.Count; i++)
        {
            _actorToSeat[_playerOrder[i]] = i;
            if (i > 0)
            {
                _opponentCardGOs[_playerOrder[i]] = new List<GameObject>();
                _opponentCardCounts[_playerOrder[i]] = 0;
            }
        }

        _activePlayers = new List<int>(_playerOrder);
    }

    void SetupPlayerLabels()
    {
        int playerCount = _playerOrder.Count;

        // Hide all areas first
        for (int i = 0; i < (handAreas?.Length ?? 0); i++)
        {
            if (handAreas[i] != null) handAreas[i].gameObject.SetActive(false);
            if (playerNameLabels != null && i < playerNameLabels.Length && playerNameLabels[i] != null)
                playerNameLabels[i].gameObject.SetActive(false);
            if (playerCardCountLabels != null && i < playerCardCountLabels.Length && playerCardCountLabels[i] != null)
                playerCardCountLabels[i].gameObject.SetActive(false);
        }

        // Show only active seats
        for (int i = 0; i < playerCount && i < (handAreas?.Length ?? 0); i++)
        {
            if (handAreas[i] != null) handAreas[i].gameObject.SetActive(true);
            if (playerNameLabels != null && i < playerNameLabels.Length && playerNameLabels[i] != null)
                playerNameLabels[i].gameObject.SetActive(true);
            if (playerCardCountLabels != null && i < playerCardCountLabels.Length && playerCardCountLabels[i] != null)
                playerCardCountLabels[i].gameObject.SetActive(true);
        }

        // Set name labels
        for (int i = 0; i < playerCount && i < (playerNameLabels?.Length ?? 0); i++)
        {
            if (playerNameLabels[i] == null) continue;
            Player p = GetPhotonPlayer(_playerOrder[i]);
            string name = p != null ? GetDisplayName(p) : $"Player {i + 1}";
            playerNameLabels[i].text = (i == 0) ? $"{name} (You)" : name;
        }

        // NOTE: Hand area RectTransforms are set in Unity Inspector — we don't touch them here
        // Just make sure HorizontalLayoutGroup settings are correct on each hand area
        for (int i = 0; i < playerCount && i < (handAreas?.Length ?? 0); i++)
        {
            if (handAreas[i] == null) continue;
            var hlg = handAreas[i].GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            if (hlg != null)
            {
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
                hlg.childControlWidth = false;
                hlg.childControlHeight = false;
            }
        }
    }

    // ── PHASE 1: DEAL (Master Client only) ────────────────────────────────────

    IEnumerator DealPhase()
    {
        SetTurnText("Dealing cards...");
        yield return new WaitForSeconds(dealShowDuration);

        deck.CreateDeck();
        List<Card> allCards = new List<Card>(deck.cards);
        deck.cards.Clear();

        int playerCount = _playerOrder.Count;
        var hands = new Dictionary<int, List<Card>>();
        foreach (int actor in _playerOrder) hands[actor] = new List<Card>();

        for (int i = 0; i < allCards.Count; i++)
            hands[_playerOrder[i % playerCount]].Add(allCards[i]);

        // Reset ready counter BEFORE sending any RPCs to avoid race condition
        _playersReadyCount = 0;
        _totalPlayers = _playerOrder.Count;

        // Send each player their hand privately
        foreach (int actor in _playerOrder)
        {
            string serialized = SerializeCards(hands[actor]);
            Player target = GetPhotonPlayer(actor);
            if (target != null)
                photonView.RPC("RPC_ReceiveHand", target, serialized);
        }

        // Tell everyone initial card counts
        foreach (int actor in _playerOrder)
        {
            photonView.RPC("RPC_SetOpponentCount", RpcTarget.All,
                actor, hands[actor].Count);
        }

        photonView.RPC("RPC_DealComplete", RpcTarget.All);

        // Wait until all players signal ready OR timeout after 120 seconds
        float timeout = 240f;
        float elapsed = 0f;
        while (_playersReadyCount < _totalPlayers && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(0.5f);

        // Decide who goes first
        int firstActor = _activePlayers.Min();
        photonView.RPC("RPC_StartTurn", RpcTarget.All, firstActor);
    }

    // ── RPC: Receive hand ─────────────────────────────────────────────────────

    [PunRPC]
    void RPC_ReceiveHand(string serializedCards)
    {
        _myHand.Clear();
        _myHandUI.Clear();
        if (handAreas != null && handAreas.Length > 0 && handAreas[0] != null)
            foreach (Transform child in handAreas[0]) Destroy(child.gameObject);

        List<Card> cards = DeserializeCards(serializedCards);
        _myHand = cards;

        // Spawn all cards face-up
        foreach (Card c in _myHand)
            SpawnMyCard(c);

        // Scale all cards to fit screen AFTER all are spawned
        ScaleAllHandCards();

        // Tell everyone my initial card count
        photonView.RPC("RPC_SetOpponentCount", RpcTarget.Others,
            PhotonNetwork.LocalPlayer.ActorNumber, _myHand.Count);

        // Start initial discard phase with a short delay so player sees their cards
        StartCoroutine(BeginInitialDiscardPhase());
    }

    // Track ready players (Master Client only) — HashSet prevents double-counting
    private int _playersReadyCount = 0;
    private int _totalPlayers = 0;
    private bool _iHaveSignaledReady = false; // prevent this client from signaling twice

    bool HasAnyPairs(List<Card> hand)
    {
        for (int i = 0; i < hand.Count; i++)
            for (int j = i + 1; j < hand.Count; j++)
                if (hand[i].rank == hand[j].rank)
                    return true;
        return false;
    }

    void SignalReadyToStart()
    {
        if (_iHaveSignaledReady) return; // never signal twice
        _iHaveSignaledReady = true;
        SetTurnText("No more pairs! Waiting for other players...");
        photonView.RPC("RPC_SetOpponentCount", RpcTarget.Others,
            PhotonNetwork.LocalPlayer.ActorNumber, _myHand.Count);
        photonView.RPC("RPC_PlayerReadyToStart", RpcTarget.MasterClient);
    }

    void CheckIfInitialDiscardComplete()
    {
        if (!HasAnyPairs(_myHand))
            SignalReadyToStart();
    }

    [PunRPC]
    void RPC_PlayerReadyToStart()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        _playersReadyCount++;
        Debug.Log($"[GCNet] Players ready: {_playersReadyCount}/{_totalPlayers}");
        SetTurnText($"Waiting for players... {_playersReadyCount}/{_totalPlayers} ready");
    }

    IEnumerator BeginInitialDiscardPhase()
    {
        // Reset ready flag for this game
        _iHaveSignaledReady = false;

        // Give player 1.5 seconds to see their cards
        yield return new WaitForSeconds(1.5f);

        if (!HasAnyPairs(_myHand))
        {
            // No pairs at all - signal ready
            SignalReadyToStart();
        }
        else
        {
            EnableInitialPairSelection();
            SetTurnText("Select your pairs to discard!");
        }
    }

    [PunRPC]
    void RPC_DealComplete()
    {
        SetTurnText("Cards dealt! Discard your pairs.");
    }

    [PunRPC]
    void RPC_SetOpponentCount(int actorNumber, int count)
    {
        if (!_opponentCardCounts.ContainsKey(actorNumber))
            _opponentCardCounts[actorNumber] = 0;
        _opponentCardCounts[actorNumber] = count;

        // Only rebuild UI for OTHER players (not ourselves)
        if (actorNumber != PhotonNetwork.LocalPlayer.ActorNumber)
            RebuildOpponentUI(actorNumber, count);

        UpdateCardCountLabels();
    }

    // ── RPC: Turn management ──────────────────────────────────────────────────

    [PunRPC]
    void RPC_StartTurn(int actorNumber)
    {
        _currentTurnActorNumber = actorNumber;
        bool isMyTurn = actorNumber == PhotonNetwork.LocalPlayer.ActorNumber;

        string playerName = GetActorDisplayName(actorNumber);
        SetTurnText(isMyTurn ? "Your Turn — pick a card!" : $"{playerName}'s Turn");

        if (isMyTurn)
        {
            // Find pick target (next active player)
            int targetActor = GetPickTarget(actorNumber);
            if (targetActor == -1) { EndGame(actorNumber); return; }
            _pickTargetActorNumber = targetActor;

            StartCoroutine(BeginMyPickTurn(targetActor));
        }
        // Other clients just watch
    }

    IEnumerator BeginMyPickTurn(int targetActorNumber)
    {
        yield return new WaitForSeconds(0.3f);
        int targetSeat = _actorToSeat.ContainsKey(targetActorNumber)
            ? _actorToSeat[targetActorNumber] : -1;
        if (targetSeat > 0 && targetSeat < handAreas.Length)
            yield return StartCoroutine(ScaleHandArea(targetSeat, pickScaleMultiplier, pickScaleDuration));

        // Make opponent cards clickable via Update() mouse detection
        _waitingForMyPick = true;
        _pickModeActive = true;
    }

    // ── Handle my pick ────────────────────────────────────────────────────────

    IEnumerator HandleMyPick(int cardIndex)
    {
        _waitingForMyPick = false;

        int targetSeat = _actorToSeat.ContainsKey(_pickTargetActorNumber)
            ? _actorToSeat[_pickTargetActorNumber] : -1;
        if (targetSeat > 0)
            yield return StartCoroutine(ScaleHandArea(targetSeat, 1f, pickScaleDuration));

        // Tell everyone: I picked card at index [cardIndex] from [_pickTargetActorNumber]
        photonView.RPC("RPC_PlayerPickedCard",
            RpcTarget.All,
            PhotonNetwork.LocalPlayer.ActorNumber,
            _pickTargetActorNumber,
            cardIndex);
    }

    // ── RPC: Card pick (runs on ALL clients) ──────────────────────────────────

    [PunRPC]
    void RPC_PlayerPickedCard(int pickerActor, int fromActor, int cardIndex)
    {
        bool iAmPicker = pickerActor == PhotonNetwork.LocalPlayer.ActorNumber;
        bool iAmSource = fromActor == PhotonNetwork.LocalPlayer.ActorNumber;

        if (iAmSource)
        {
            // Remove card from my hand and send to picker
            if (cardIndex < _myHand.Count)
            {
                Card sent = _myHand[cardIndex];
                _myHand.RemoveAt(cardIndex);
                if (_myHandUI.ContainsKey(sent))
                {
                    Destroy(_myHandUI[sent]);
                    _myHandUI.Remove(sent);
                }
                ScaleAllHandCards();

                photonView.RPC("RPC_ReceivePickedCard",
                    GetPhotonPlayer(pickerActor),
                    sent.rank, sent.suit);

                // Tell everyone my EXACT new count (single source of truth)
                photonView.RPC("RPC_SetOpponentCount", RpcTarget.Others,
                    PhotonNetwork.LocalPlayer.ActorNumber, _myHand.Count);
            }
        }
        else if (!iAmPicker)
        {
            // I'm a spectator — source sends exact count via RPC_SetOpponentCount
            // Don't decrement here — wait for SetOpponentCount RPC which has exact value
            // Just update the display text
        }
        // Note: picker's hand will be updated in RPC_ReceivePickedCard

        if (!iAmPicker)
            SetTurnText($"{GetActorDisplayName(pickerActor)} picked a card from {GetActorDisplayName(fromActor)}");

        UpdateCardCountLabels();
    }

    // ── RPC: Picker receives actual card data ─────────────────────────────────

    [PunRPC]
    void RPC_ReceivePickedCard(string rank, string suit)
    {
        // Find matching sprite from deck
        Card pickedCard = FindCardInDeck(rank, suit);
        if (pickedCard == null)
        {
            Debug.LogWarning($"[GCNet] Could not find card {rank} of {suit} in deck!");
            FinishMyTurn(hasPair: false);
            return;
        }

        _pickedCard = pickedCard;

        // Check if I have a matching rank in my hand
        _pairMatchCard = _myHand.FirstOrDefault(c => c.rank == pickedCard.rank);

        if (_pairMatchCard != null)
        {
            // Pair formed — add picked card to hand and show both
            _myHand.Add(pickedCard);
            GameObject pickedObj = SpawnMyCard(pickedCard);
            GCCardDisplay pickedDisp = pickedObj?.GetComponent<GCCardDisplay>();
            GCCardDisplay matchDisp = _myHandUI.ContainsKey(_pairMatchCard)
                ? _myHandUI[_pairMatchCard].GetComponent<GCCardDisplay>() : null;

            // Setup as gameplay pair
            pickedDisp?.SetupGameplayPair(pickedCard, null, matchDisp);
            matchDisp?.SetupGameplayPair(_pairMatchCard, null, pickedDisp);

            // Wire click for multiplayer discard
            if (pickedObj != null)
            {
                Button b = pickedObj.GetComponent<Button>();
                if (b != null) { b.onClick.RemoveAllListeners(); b.onClick.AddListener(OnPairClickedForDiscard); }
            }
            if (_myHandUI.ContainsKey(_pairMatchCard))
            {
                Button b = _myHandUI[_pairMatchCard].GetComponent<Button>();
                if (b != null) { b.onClick.RemoveAllListeners(); b.onClick.AddListener(OnPairClickedForDiscard); }
            }

            pickedDisp?.SetPairHighlight(true);
            matchDisp?.SetPairHighlight(true);

            SetTurnText($"Pair of {pickedCard.rank}s! Click to discard.");
            _waitingForPairDiscard = true;
            // Wait for OnPairClickedForDiscard to be called
        }
        else
        {
            // No pair — just add to hand
            _myHand.Add(pickedCard);
            SpawnMyCard(pickedCard);
            SetTurnText($"You picked {pickedCard.rank} of {pickedCard.suit}. No pair.");
            StartCoroutine(DelayedTurnEnd());
        }
    }

    IEnumerator DelayedTurnEnd()
    {
        yield return new WaitForSeconds(0.8f);
        FinishMyTurn(hasPair: false);
    }

    void OnPairClickedForDiscard()
    {
        if (!_waitingForPairDiscard) return;
        _waitingForPairDiscard = false;
        StartCoroutine(DiscardMyPair());
    }

    IEnumerator DiscardMyPair()
    {
        Card c1 = _pickedCard;
        Card c2 = _pairMatchCard;

        _myHand.Remove(c1);
        _myHand.Remove(c2);

        GameObject obj1 = _myHandUI.ContainsKey(c1) ? _myHandUI[c1] : null;
        GameObject obj2 = _myHandUI.ContainsKey(c2) ? _myHandUI[c2] : null;
        _myHandUI.Remove(c1);
        _myHandUI.Remove(c2);

        foreach (GameObject obj in new[] { obj1, obj2 })
        {
            if (obj == null) continue;
            obj.GetComponent<GCCardDisplay>()?.FlipFaceUp();
            obj.transform.SetParent(throwPile, worldPositionStays: true);
            StartCoroutine(obj.GetComponent<GCCardDisplay>()
                ?.SlideToCenter(throwPile, slideAnimDuration, scatterX, scatterY)
                ?? EmptyCoroutine());
        }

        TintThrowPile(obj1, obj2);
        SetTurnText($"You discarded a pair of {c1.rank}s!");

        // Tell everyone — send rank, suits AND exact new count
        photonView.RPC("RPC_PairDiscarded",
            RpcTarget.All,
            PhotonNetwork.LocalPlayer.ActorNumber,
            c1.rank, c1.suit, c2.suit, _myHand.Count);

        yield return new WaitForSeconds(slideAnimDuration + 0.4f);
        FinishMyTurn(hasPair: true);
    }

    // ── RPC: Pair discarded (runs on ALL clients) ─────────────────────────────

    [PunRPC]
    void RPC_PairDiscarded(int actorNumber, string rank, string suit1, string suit2, int exactNewCount)
    {
        bool isMe = actorNumber == PhotonNetwork.LocalPlayer.ActorNumber;
        if (!isMe)
        {
            // Set EXACT count — never decrement (avoids double-counting bugs)
            _opponentCardCounts[actorNumber] = exactNewCount;
            RebuildOpponentUI(actorNumber, exactNewCount);

            // Show 2 face-UP cards animating to throw pile
            Card c1 = DeserializeCard(rank, suit1);
            Card c2 = DeserializeCard(rank, suit2);

            GameObject lastA = null, lastB = null;
            foreach (Card c in new[] { c1, c2 })
            {
                if (c == null) continue;
                GameObject tmp = Instantiate(cardPrefab, throwPile);
                GCCardDisplay disp = tmp.GetComponent<GCCardDisplay>();
                if (disp != null)
                {
                    disp.SetupFaceUp(c, null);
                    RectTransform rt = tmp.GetComponent<RectTransform>();
                    if (rt != null) rt.anchoredPosition = new Vector2(
                        Random.Range(-200f, 200f), Random.Range(-100f, 100f));
                    StartCoroutine(disp.SlideToCenter(throwPile, slideAnimDuration, scatterX, scatterY));
                }
                if (lastA == null) lastA = tmp; else lastB = tmp;
            }
            TintThrowPile(lastA, lastB);

            SetTurnText($"{GetActorDisplayName(actorNumber)} discarded a pair of {rank}s!");
        }

        UpdateCardCountLabels();
    }

    // ── Finish turn + advance ─────────────────────────────────────────────────

    void FinishMyTurn(bool hasPair)
    {
        _pickedCard = null;
        _pairMatchCard = null;

        // Tell master client to advance the turn
        photonView.RPC("RPC_TurnComplete",
            RpcTarget.MasterClient,
            PhotonNetwork.LocalPlayer.ActorNumber,
            _myHand.Count);
    }

    [PunRPC]
    void RPC_TurnComplete(int completedActorNumber, int handCount)
    {
        // Only Master Client handles turn advancement
        if (!PhotonNetwork.IsMasterClient) return;

        // Update count for this player
        _opponentCardCounts[completedActorNumber] = handCount;

        // Remove players with empty hands
        var toRemove = _activePlayers.Where(a =>
        {
            if (a == PhotonNetwork.LocalPlayer.ActorNumber)
                return _myHand.Count == 0;
            return _opponentCardCounts.ContainsKey(a) && _opponentCardCounts[a] == 0;
        }).ToList();

        foreach (int a in toRemove)
        {
            _activePlayers.Remove(a);
            photonView.RPC("RPC_PlayerOut", RpcTarget.All, a);
        }

        if (_activePlayers.Count <= 1)
        {
            int loser = _activePlayers.Count == 1 ? _activePlayers[0] : -1;
            photonView.RPC("RPC_GameOver", RpcTarget.All, loser);
            return;
        }

        // Find next active player
        int idx = _activePlayers.IndexOf(completedActorNumber);
        int nextIdx = (idx + 1) % _activePlayers.Count;
        int nextActor = _activePlayers[nextIdx];

        photonView.RPC("RPC_StartTurn", RpcTarget.All, nextActor);
    }

    [PunRPC]
    void RPC_PlayerOut(int actorNumber)
    {
        _activePlayers.Remove(actorNumber);
        string name = GetActorDisplayName(actorNumber);
        SetTurnText($"{name} is OUT!");
        UpdateCardCountLabels();
    }

    // ── RPC: Game Over ────────────────────────────────────────────────────────

    [PunRPC]
    void RPC_GameOver(int loserActorNumber)
    {
        _gameEnded = true;
        bool iAmLoser = loserActorNumber == PhotonNetwork.LocalPlayer.ActorNumber;

        if (iAmLoser) CurrencyManager.AddCoins(-150);
        else CurrencyManager.AddCoins(150);

        string loserName = loserActorNumber == -1
            ? "Unknown"
            : GetActorDisplayName(loserActorNumber);

        if (gameOverPanel) gameOverPanel.SetActive(true);
        if (gameOverTitle)
            gameOverTitle.text = iAmLoser
                ? "You are the Gulam Chor!"
                : $"{loserName} is the Gulam Chor!";
        if (gameOverDetails)
            gameOverDetails.text = iAmLoser
                ? "You were stuck with the lone Jack!"
                : $"You escaped! {loserName} got the Jack!";
        if (turnText) turnText.gameObject.SetActive(false);
    }

    // ── Initial pair discard (each player does this independently) ────────────

    // Called after RPC_ReceiveHand — player self-selects their pairs
    public void EnableInitialPairSelection()
    {
        foreach (var kvp in _myHandUI)
        {
            Button btn = kvp.Value?.GetComponent<Button>();
            if (btn == null) continue;
            Card captured = kvp.Key;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnInitialCardClicked(captured));
            btn.interactable = true;
        }
    }

    public void OnInitialCardClicked(Card card)
    {
        if (!_myHandUI.ContainsKey(card)) return;
        GCCardDisplay disp = _myHandUI[card].GetComponent<GCCardDisplay>();

        if (_initialFirstSelected == null)
        {
            _initialFirstSelected = card;
            _initialFirstDisp = disp;
            disp?.SetPairHighlight(true);
        }
        else if (_initialFirstSelected == card)
        {
            _initialFirstSelected = null;
            disp?.SetPairHighlight(false);
            _initialFirstDisp = null;
        }
        else if (_initialFirstSelected.rank == card.rank)
        {
            disp?.SetPairHighlight(true);
            StartCoroutine(DiscardInitialSelectedPair(_initialFirstSelected, card,
                _initialFirstDisp, disp));
        }
        else
        {
            StartCoroutine(FlashNoMatch(disp));
        }
    }

    IEnumerator FlashNoMatch(GCCardDisplay disp)
    {
        if (disp?.myImage != null) disp.myImage.color = new Color(1f, 0.3f, 0.3f, 1f);
        yield return new WaitForSeconds(0.3f);
        disp?.SetPairHighlight(false);
    }

    IEnumerator DiscardInitialSelectedPair(Card c1, Card c2,
        GCCardDisplay d1, GCCardDisplay d2)
    {
        _initialFirstSelected = null;
        _initialFirstDisp = null;

        _myHand.Remove(c1);
        _myHand.Remove(c2);

        GameObject obj1 = _myHandUI.ContainsKey(c1) ? _myHandUI[c1] : null;
        GameObject obj2 = _myHandUI.ContainsKey(c2) ? _myHandUI[c2] : null;
        _myHandUI.Remove(c1);
        _myHandUI.Remove(c2);

        foreach (GameObject obj in new[] { obj1, obj2 })
        {
            if (obj == null) continue;
            obj.GetComponent<GCCardDisplay>()?.FlipFaceUp();
            // Parent to throwPile itself — cards slide to throwPile's center (local 0,0)
            obj.transform.SetParent(throwPile, worldPositionStays: true);
            StartCoroutine(obj.GetComponent<GCCardDisplay>()
                ?.SlideToCenter(throwPile, slideAnimDuration, scatterX, scatterY)
                ?? EmptyCoroutine());
        }

        TintThrowPile(obj1, obj2);
        SetTurnText("Pair discarded! Keep going.");

        // Single RPC — passes exact count, shows animation, updates UI on all screens
        photonView.RPC("RPC_PairDiscarded",
            RpcTarget.Others,
            PhotonNetwork.LocalPlayer.ActorNumber,
            c1.rank, c1.suit, c2.suit, _myHand.Count);

        yield return new WaitForSeconds(slideAnimDuration + 0.3f);
        ScaleAllHandCards();
        CheckIfInitialDiscardComplete();
    }

    // ── Opponent UI ───────────────────────────────────────────────────────────

    void RebuildOpponentUI(int actorNumber, int count)
    {
        int seat = _actorToSeat.ContainsKey(actorNumber) ? _actorToSeat[actorNumber] : -1;
        if (seat < 1 || seat >= handAreas.Length || handAreas[seat] == null) return;

        // Destroy old cards
        if (_opponentCardGOs.ContainsKey(actorNumber))
        {
            foreach (var go in _opponentCardGOs[actorNumber])
                if (go != null) Destroy(go);
            _opponentCardGOs[actorNumber].Clear();
        }
        else
            _opponentCardGOs[actorNumber] = new List<GameObject>();

        if (count == 0) return;

        // Spawn face-down cards
        for (int i = 0; i < count; i++)
        {
            GameObject obj = Instantiate(cardPrefab, handAreas[seat]);
            obj.GetComponent<GCCardDisplay>()?.SetupAICard(new Card("?", "?", null));
            _opponentCardGOs[actorNumber].Add(obj);
        }

        // Apply same fit calculation as player hand
        StartCoroutine(ApplyHandFit(handAreas[seat]));
    }

    void MakeOpponentCardsClickable(int actorNumber, bool clickable)
    {
        if (!_opponentCardGOs.ContainsKey(actorNumber)) return;
        var cards = _opponentCardGOs[actorNumber];
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] == null) continue;
            Button btn = cards[i].GetComponent<Button>();
            if (btn != null) btn.interactable = clickable;
        }
    }

    // ── Card helpers ──────────────────────────────────────────────────────────

    GameObject SpawnMyCard(Card c)
    {
        if (handAreas == null || handAreas.Length == 0 || handAreas[0] == null) return null;
        if (cardPrefab == null) return null;
        GameObject obj = Instantiate(cardPrefab, handAreas[0]);
        obj.GetComponent<GCCardDisplay>()?.SetupFaceUp(c, null);
        _myHandUI[c] = obj;
        return obj;
    }

    // Call after ALL cards spawned — calculates exact spacing to fit within hand area width
    void ScaleAllHandCards()
    {
        if (handAreas == null || handAreas[0] == null) return;
        int count = handAreas[0].childCount;
        if (count == 0) return;

        // Wait one frame for layout to update, then calculate
        StartCoroutine(ApplyHandFit(handAreas[0]));
    }

    IEnumerator ApplyHandFit(Transform area)
    {
        // Wait multiple frames for layout to fully update in all window sizes
        yield return null;
        yield return null;
        yield return new WaitForEndOfFrame();

        RectTransform areaRT = area.GetComponent<RectTransform>();
        if (areaRT == null) yield break;

        int count = area.childCount;
        if (count == 0) yield break;

        // Get card width from prefab rect (more reliable than from spawned card)
        RectTransform firstCard = area.GetChild(0).GetComponent<RectTransform>();
        float cardW = (firstCard != null && firstCard.rect.width > 0)
            ? firstCard.rect.width : 90f;

        // Get available width — use canvas width as fallback for windowed mode
        float availableW = areaRT.rect.width;
        if (availableW <= 10f)
        {
            // Fallback: get from canvas
            Canvas c = GetComponentInParent<Canvas>();
            if (c == null) c = FindFirstObjectByType<Canvas>();
            if (c != null) availableW = ((RectTransform)c.transform).rect.width - 40f;
            else availableW = Screen.width - 40f;
        }

        // Calculate exact spacing to fit all cards in one row
        float spacing;
        float totalW = count * cardW;
        if (count <= 1) spacing = 3f;
        else if (totalW <= availableW) spacing = Mathf.Min(5f, (availableW - totalW) / (count - 1));
        else spacing = (availableW - totalW) / (count - 1);

        // Never overlap more than 75% of card width
        spacing = Mathf.Max(spacing, -cardW * 0.75f);

        var hlg = area.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        if (hlg != null)
        {
            hlg.spacing = spacing;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
        }

        // Keep cards at full scale — spacing handles fit
        foreach (Transform child in area)
            child.localScale = Vector3.one;
    }

    List<(Card, Card)> FindAndRemovePairs(List<Card> hand)
    {
        var pairs = new List<(Card, Card)>();
        bool found = true;
        while (found)
        {
            found = false;
            for (int i = 0; i < hand.Count && !found; i++)
                for (int j = i + 1; j < hand.Count && !found; j++)
                    if (hand[i].rank == hand[j].rank)
                    {
                        pairs.Add((hand[i], hand[j]));
                        hand.RemoveAt(j);
                        hand.RemoveAt(i);
                        found = true;
                    }
        }
        return pairs;
    }

    Card FindCardInDeck(string rank, string suit)
    {
        // Look in deck's sprites to reconstruct the card
        string[] suits = { "Hearts", "Diamonds", "Clubs", "Spades" };
        string[] ranks = { "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K", "A" };

        int spriteIndex = 0;
        foreach (string s in suits)
        {
            foreach (string r in ranks)
            {
                if (spriteIndex < deck.allCardSprites.Count)
                {
                    Sprite spr = deck.allCardSprites[spriteIndex];
                    spriteIndex++;
                    if (s == rank && r == suit) // intentionally swapped — suit param is "rank" in serialization
                        continue;
                    if (r == rank && s == suit)
                        return new Card(r, s, spr);
                }
            }
        }
        return null;
    }

    // Rebuild card with proper sprite from deck's allCardSprites
    Card DeserializeCard(string rank, string suit)
    {
        string[] suits = { "Hearts", "Diamonds", "Clubs", "Spades" };
        string[] ranks = { "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K", "A" };
        int idx = 0;
        foreach (string s in suits)
            foreach (string r in ranks)
            {
                if (r == rank && s == suit && idx < deck.allCardSprites.Count)
                    return new Card(r, s, deck.allCardSprites[idx]);
                idx++;
            }
        return new Card(rank, suit, null);
    }

    // ── Serialization ──────────────────────────────────────────────────────────

    string SerializeCards(List<Card> cards)
    {
        return string.Join(",", cards.Select(c => $"{c.rank}:{c.suit}"));
    }

    List<Card> DeserializeCards(string data)
    {
        var result = new List<Card>();
        if (string.IsNullOrEmpty(data)) return result;
        foreach (string token in data.Split(','))
        {
            var parts = token.Split(':');
            if (parts.Length == 2)
                result.Add(DeserializeCard(parts[0], parts[1]));
        }
        return result;
    }

    // ── Turn helpers ──────────────────────────────────────────────────────────

    int GetPickTarget(int currentActor)
    {
        int idx = _activePlayers.IndexOf(currentActor);
        for (int i = 1; i < _activePlayers.Count; i++)
        {
            int candidate = _activePlayers[(idx + i) % _activePlayers.Count];
            int count = candidate == PhotonNetwork.LocalPlayer.ActorNumber
                ? _myHand.Count
                : (_opponentCardCounts.ContainsKey(candidate) ? _opponentCardCounts[candidate] : 0);
            if (count > 0) return candidate;
        }
        return -1;
    }

    // ── Scale ─────────────────────────────────────────────────────────────────

    IEnumerator ScaleHandArea(int seatIndex, float targetScale, float duration)
    {
        if (handAreas == null || seatIndex >= handAreas.Length) yield break;
        Transform area = handAreas[seatIndex];
        if (targetScale > 1f) area.SetAsLastSibling();

        Vector3 from = area.localScale;
        Vector3 to = Vector3.one * targetScale;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            area.localScale = Vector3.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        area.localScale = to;
    }

    // ── Tint ──────────────────────────────────────────────────────────────────

    void TintThrowPile(GameObject newA, GameObject newB)
    {
        Color yellow = new Color(1f, 0.95f, 0.75f, 1f);
        if (_lastThrownA != null)
        {
            var disp = _lastThrownA.GetComponent<GCCardDisplay>();
            if (disp?.myImage != null) disp.myImage.color = yellow;
        }
        if (_lastThrownB != null)
        {
            var disp = _lastThrownB.GetComponent<GCCardDisplay>();
            if (disp?.myImage != null) disp.myImage.color = yellow;
        }
        if (newA != null)
        {
            var disp = newA.GetComponent<GCCardDisplay>();
            if (disp?.myImage != null) disp.myImage.color = Color.white;
        }
        if (newB != null)
        {
            var disp = newB.GetComponent<GCCardDisplay>();
            if (disp?.myImage != null) disp.myImage.color = Color.white;
        }
        _lastThrownA = newA;
        _lastThrownB = newB;
    }

    // ── Clear pile ────────────────────────────────────────────────────────────

    void ClearThrowPile()
    {
        if (throwPile == null) return;
        var toDestroy = new List<GameObject>();
        foreach (Transform child in throwPile)
        {
            if (child.GetComponent<GCCardDisplay>() != null)
                toDestroy.Add(child.gameObject);
        }
        foreach (var go in toDestroy) Destroy(go);
    }

    // ── Game Over ─────────────────────────────────────────────────────────────

    void EndGame(int loserActor)
    {
        if (PhotonNetwork.IsMasterClient)
            photonView.RPC("RPC_GameOver", RpcTarget.All, loserActor);
    }

    void GoToMainMenu()
    {
        GCNetworkManager.Instance?.LeaveRoom();
        SceneManager.LoadScene("MainMenu");
    }

    // ── Photon callbacks ──────────────────────────────────────────────────────

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (_gameEnded) return;
        SetTurnText($"{GetDisplayName(otherPlayer)} disconnected.");
        _activePlayers.Remove(otherPlayer.ActorNumber);
        if (_opponentCardCounts.ContainsKey(otherPlayer.ActorNumber))
        {
            _opponentCardCounts.Remove(otherPlayer.ActorNumber);
            int seat = _actorToSeat.ContainsKey(otherPlayer.ActorNumber)
                ? _actorToSeat[otherPlayer.ActorNumber] : -1;
            if (seat > 0 && seat < handAreas.Length)
                foreach (Transform child in handAreas[seat]) Destroy(child.gameObject);
        }

        if (_activePlayers.Count <= 1 && !_gameEnded)
            EndGame(_activePlayers.Count == 1 ? _activePlayers[0] : -1);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    Player GetPhotonPlayer(int actorNumber)
    {
        foreach (var p in PhotonNetwork.PlayerList)
            if (p.ActorNumber == actorNumber) return p;
        return null;
    }

    string GetDisplayName(Player p)
    {
        if (p.CustomProperties.TryGetValue(GCNetworkManager.PROP_NAME, out object name))
            return name.ToString();
        return p.NickName ?? $"Player {p.ActorNumber}";
    }

    string GetActorDisplayName(int actorNumber)
    {
        if (actorNumber == PhotonNetwork.LocalPlayer.ActorNumber) return "You";
        Player p = GetPhotonPlayer(actorNumber);
        return p != null ? GetDisplayName(p) : $"Player {actorNumber}";
    }

    void SetTurnText(string text)
    {
        if (turnText != null) { turnText.gameObject.SetActive(true); turnText.text = text; }
        Debug.Log($"[GCNet] {text}");
    }

    void UpdateCardCountLabels()
    {
        if (playerCardCountLabels == null) return;
        for (int i = 0; i < _playerOrder.Count && i < playerCardCountLabels.Length; i++)
        {
            if (playerCardCountLabels[i] == null) continue;
            int actor = _playerOrder[i];
            bool active = _activePlayers.Contains(actor);
            int count = actor == PhotonNetwork.LocalPlayer.ActorNumber
                ? _myHand.Count
                : (_opponentCardCounts.ContainsKey(actor) ? _opponentCardCounts[actor] : 0);
            playerCardCountLabels[i].text = active ? count.ToString() : "OUT";
        }
    }

    IEnumerator EmptyCoroutine() { yield break; }
}