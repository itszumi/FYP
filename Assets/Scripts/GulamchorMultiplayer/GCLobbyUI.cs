// GCLobbyUI.cs
// Attach this to a Canvas/Manager GameObject in your GCMultiplayerLobby scene.
// Handles: Create Room, Join Room, Player List, Start Game button.
//
// Hierarchy expected:
//   LobbyCanvas
//     MainPanel            ← shown first
//       UsernameInput      (TMP_InputField)
//       CreateRoomBtn      (Button)
//       JoinCodeInput      (TMP_InputField)
//       JoinRoomBtn        (Button)
//       BackBtn            (Button)
//     WaitingPanel         ← shown while in room waiting for players
//       RoomCodeText       (TMP_Text — shows the 4-digit code)
//       PlayersGrid        (VerticalLayoutGroup — spawn player rows here)
//       StatusText         (TMP_Text)
//       StartGameBtn       (Button — host only)
//       LeaveBtn           (Button)
//     LoadingPanel         ← shown while connecting
//       LoadingText        (TMP_Text)

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class GCLobbyUi : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainPanel;
    public GameObject waitingPanel;
    public GameObject loadingPanel;

    [Header("Main Panel")]
    public TMP_InputField usernameInput;
    public Button createRoomBtn;
    public TMP_InputField joinCodeInput;
    public Button joinRoomBtn;
    public Button backBtn;
    public TextMeshProUGUI errorText;

    [Header("Waiting Panel")]
    public TextMeshProUGUI roomCodeDisplay;
    public Transform playersGrid;
    public TextMeshProUGUI statusText;
    public Button startGameBtn;
    public Button leaveBtn;

    [Header("Loading")]
    public TextMeshProUGUI loadingText;

    [Header("Player Row Prefab")]
    public GameObject playerRowPrefab; // has a TMP text child named "NameText"

    [Header("Network Manager (drag NetworkManager GO here)")]
    public GCNetworkManager networkManager;

    // Private cache — not affected by DontDestroyOnLoad nullification
    private static GCNetworkManager _cachedNetMgr;

    [Header("Scene")]
    public string mainMenuScene = "MainMenu";

    // Track player rows
    private Dictionary<int, GameObject> _playerRows = new Dictionary<int, GameObject>();

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Start()
    {
        // Force all panels to correct initial state
        if (mainPanel) mainPanel.SetActive(true);
        if (waitingPanel) waitingPanel.SetActive(false);
        if (loadingPanel) loadingPanel.SetActive(false);
        if (startGameBtn) startGameBtn.gameObject.SetActive(false);

        if (errorText) errorText.text = "";

        // Style panels
        var canvasBg = GetComponent<UnityEngine.UI.Image>();
        if (canvasBg != null) canvasBg.color = new Color(0.08f, 0.08f, 0.14f, 1f);
        if (waitingPanel != null)
        {
            var img = waitingPanel.GetComponent<UnityEngine.UI.Image>();
            if (img != null) img.color = new Color(0.12f, 0.10f, 0.06f, 0.97f);
        }
        if (mainPanel != null)
        {
            var img = mainPanel.GetComponent<UnityEngine.UI.Image>();
            if (img != null) img.color = new Color(0.12f, 0.10f, 0.06f, 0.97f);
        }

        // Pre-fill username from PlayerSession
        if (usernameInput != null && PlayerSession.IsLoggedIn())
            usernameInput.text = PlayerSession.GetUser();

        Debug.Log("[GCLobby] Start — Instance is: " + (GCNetworkManager.Instance == null ? "NULL" : GCNetworkManager.Instance.gameObject.name));
        Debug.Log("[GCLobby] Start — networkManager field is: " + (networkManager == null ? "NULL" : networkManager.gameObject.name));

        // Wire buttons
        createRoomBtn?.onClick.AddListener(OnCreateRoom);
        joinRoomBtn?.onClick.AddListener(OnJoinRoom);
        backBtn?.onClick.AddListener(() => SceneManager.LoadScene(mainMenuScene));
        startGameBtn?.onClick.AddListener(OnStartGame);
        leaveBtn?.onClick.AddListener(OnLeaveRoom);

        // Wire NetworkManager immediately — try direct field first, then Instance
        var netMgr = GetNetMgr();
        if (netMgr != null)
        {
            networkManager = netMgr;
            WireCallbacks(netMgr);
            Debug.Log("[GCLobby] NetworkManager wired in Start: " + netMgr.gameObject.name);
        }
        else
        {
            // Fallback — wait for it
            StartCoroutine(WireNetworkManagerDelayed());
        }

        // If already in a room show waiting panel
        if (PhotonNetwork.InRoom)
        {
            ShowWaitingPanel(PhotonNetwork.CurrentRoom.Name);
            RefreshAllPlayers();
        }
    }

    void WireCallbacks(GCNetworkManager netMgr)
    {
        netMgr.OnConnectionFailedCallback = OnConnectionFailed;
        netMgr.OnRoomCreatedCallback = OnRoomCreated;
        netMgr.OnRoomCreateFailedCallback = OnRoomFailed;
        netMgr.OnRoomJoinedCallback = OnRoomJoined;
        netMgr.OnRoomJoinFailedCallback = OnRoomFailed;
        netMgr.OnPlayerJoinedCallback = OnPlayerJoined;
        netMgr.OnPlayerLeftCallback = OnPlayerLeft;
        netMgr.OnGameStartingCallback = OnGameStarting;
    }

    System.Collections.IEnumerator WireNetworkManagerDelayed()
    {
        // Wait up to 3 seconds for GCNetworkManager to be available
        float timeout = 3f;
        float elapsed = 0f;
        GCNetworkManager netMgr = null;

        while (netMgr == null && elapsed < timeout)
        {
            netMgr = GetNetMgr();
            if (netMgr == null)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
        }

        if (netMgr == null)
        {
            Debug.LogError("[GCLobby] GCNetworkManager not found after waiting! Check scene setup.");
            yield break;
        }

        // Save reference
        networkManager = netMgr;
        WireCallbacks(netMgr);
        Debug.Log("[GCLobby] NetworkManager wired via coroutine: " + netMgr.gameObject.name);

        if (PhotonNetwork.InRoom)
        {
            ShowWaitingPanel(PhotonNetwork.CurrentRoom.Name);
            RefreshAllPlayers();
        }
    }

    void OnDestroy()
    {
        // Clear callbacks to avoid stale references
        if (GCNetworkManager.Instance != null)
        {
            GCNetworkManager.Instance.OnConnectionFailedCallback = null;
            GCNetworkManager.Instance.OnRoomCreatedCallback = null;
            GCNetworkManager.Instance.OnRoomCreateFailedCallback = null;
            GCNetworkManager.Instance.OnRoomJoinedCallback = null;
            GCNetworkManager.Instance.OnRoomJoinFailedCallback = null;
            GCNetworkManager.Instance.OnPlayerJoinedCallback = null;
            GCNetworkManager.Instance.OnPlayerLeftCallback = null;
            GCNetworkManager.Instance.OnGameStartingCallback = null;
        }
    }

    // ── Button handlers ───────────────────────────────────────────────────────

    void OnCreateRoom()
    {
        string username = GetUsername();
        if (username == null) return;
        var netMgr = GetNetMgr();
        if (netMgr == null) { ShowError("NetworkManager not found!"); return; }
        string code = GCNetworkManager.GenerateRoomCode();
        ShowLoading($"Creating room {code}...");
        netMgr.ConnectAndCreateRoom(code, username,
            PlayerSession.IsLoggedIn() ? PlayerSession.GetAvatarIndex() : 0);
    }

    void OnJoinRoom()
    {
        string username = GetUsername();
        if (username == null) return;
        string code = joinCodeInput?.text.Trim().ToUpper() ?? "";
        if (code.Length != 4) { ShowError("Enter a 4-character room code."); return; }
        var netMgr = GetNetMgr();
        if (netMgr == null) { ShowError("NetworkManager not found!"); return; }
        ShowLoading($"Joining room {code}...");
        netMgr.ConnectAndJoinRoom(code, username,
            PlayerSession.IsLoggedIn() ? PlayerSession.GetAvatarIndex() : 0);
    }

    void OnStartGame()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;
        if (playerCount < (GCNetworkManager.Instance?.minPlayersToStart ?? 2))
        {
            ShowStatusText($"Need at least {GCNetworkManager.Instance?.minPlayersToStart ?? 2} players to start.");
            return;
        }
        GCNetworkManager.Instance?.StartGame();
    }

    void OnLeaveRoom()
    {
        GCNetworkManager.Instance?.LeaveRoom();
        ClearPlayerRows();
        ShowPanel(mainPanel);
    }

    // ── Network callbacks ─────────────────────────────────────────────────────

    void OnRoomCreated()
    {
        ShowWaitingPanel(PhotonNetwork.CurrentRoom.Name);
        RefreshAllPlayers();
    }

    void OnRoomJoined()
    {
        ShowWaitingPanel(PhotonNetwork.CurrentRoom.Name);
        RefreshAllPlayers();
    }

    void OnRoomFailed(string error)
    {
        ShowPanel(mainPanel);
        ShowError($"Failed: {error}");
    }

    void OnConnectionFailed(string error)
    {
        ShowPanel(mainPanel);
        ShowError($"Connection failed: {error}");
    }

    void OnPlayerJoined(Player player)
    {
        // Refresh all rows so host label updates correctly
        RefreshAllPlayers();
        ShowStatusText($"{GetDisplayName(player)} joined!");
    }

    void OnPlayerLeft(Player player)
    {
        RemovePlayerRow(player.ActorNumber);
        UpdateStartButton();
        ShowStatusText($"A player left the room.");
    }

    void OnGameStarting()
    {
        ShowLoading("Game starting...");
    }

    // ── Player rows ───────────────────────────────────────────────────────────

    void RefreshAllPlayers()
    {
        ClearPlayerRows();
        foreach (var player in PhotonNetwork.PlayerList)
            AddPlayerRow(player);
        UpdateStartButton();
    }

    void AddPlayerRow(Player player)
    {
        if (_playerRows.ContainsKey(player.ActorNumber)) return;
        if (playerRowPrefab == null || playersGrid == null) return;

        GameObject row = Instantiate(playerRowPrefab, playersGrid);
        _playerRows[player.ActorNumber] = row;

        // Use GCPlayerRow.Setup if available (handles avatar + name)
        GCPlayerRow gcRow = row.GetComponent<GCPlayerRow>();
        if (gcRow != null)
        {
            gcRow.Setup(player, player.IsLocal);
            return;
        }

        // Fallback — set name text directly
        TextMeshProUGUI nameText = row.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
        if (nameText != null)
            nameText.text = GetDisplayName(player) +
                (player.IsMasterClient ? " [Host]" : "") +
                (player.IsLocal ? " (You)" : "");

        // Fallback avatar
        Image avatarImg = row.transform.Find("AvatarImage")?.GetComponent<Image>();
        if (avatarImg != null && AvatarManager.Instance != null)
        {
            int idx = 0;
            if (player.CustomProperties.TryGetValue(GCNetworkManager.PROP_AVATAR, out object avObj))
                idx = (int)avObj;
            Sprite spr = AvatarManager.Instance.GetSprite(idx);
            if (spr != null) avatarImg.sprite = spr;
        }
    }

    void RemovePlayerRow(int actorNumber)
    {
        if (!_playerRows.ContainsKey(actorNumber)) return;
        Destroy(_playerRows[actorNumber]);
        _playerRows.Remove(actorNumber);
    }

    void ClearPlayerRows()
    {
        foreach (var kvp in _playerRows)
            if (kvp.Value != null) Destroy(kvp.Value);
        _playerRows.Clear();
    }

    void UpdateStartButton()
    {
        if (startGameBtn == null) return;
        bool isMaster = PhotonNetwork.IsMasterClient;
        bool enoughPlayers = PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.PlayerCount >=
            (GCNetworkManager.Instance?.minPlayersToStart ?? 2);

        startGameBtn.gameObject.SetActive(isMaster);
        startGameBtn.interactable = enoughPlayers;

        var btnText = startGameBtn.GetComponentInChildren<TextMeshProUGUI>();
        if (btnText != null) btnText.text = "Start Game";

        if (isMaster)
            ShowStatusText(enoughPlayers
                ? "All players ready! Press Start Game."
                : $"Waiting for players... ({PhotonNetwork.CurrentRoom?.PlayerCount ?? 0}/{GCNetworkManager.Instance?.maxPlayers ?? 5})");
        else
            ShowStatusText($"Waiting for host to start... ({PhotonNetwork.CurrentRoom?.PlayerCount ?? 0}/{GCNetworkManager.Instance?.maxPlayers ?? 5})");
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    void ShowPanel(GameObject panel)
    {
        if (mainPanel) mainPanel.SetActive(false);
        if (waitingPanel) waitingPanel.SetActive(false);
        if (loadingPanel) loadingPanel.SetActive(false);
        if (panel) panel.SetActive(true);
    }

    void ShowLoading(string msg)
    {
        ShowPanel(loadingPanel);
        if (loadingText) loadingText.text = msg;
    }

    void ShowWaitingPanel(string code)
    {
        ShowPanel(waitingPanel);
        if (roomCodeDisplay) roomCodeDisplay.text = $"Room Code: {code}";

        // Hide Create/Join — already in room
        createRoomBtn?.gameObject.SetActive(false);
        joinRoomBtn?.gameObject.SetActive(false);

        // Show Start only for host
        if (startGameBtn)
            startGameBtn.gameObject.SetActive(PhotonNetwork.IsMasterClient);

        UpdateStartButton();
    }

    void ShowError(string msg)
    {
        if (errorText) { errorText.text = msg; errorText.gameObject.SetActive(true); }
        Debug.LogWarning($"[GCLobby] {msg}");
    }

    void ShowStatusText(string msg)
    {
        if (statusText) statusText.text = msg;
    }

    GCNetworkManager GetNetMgr()
    {
        if (GCNetworkManager.Instance != null)
            return GCNetworkManager.Instance;
        var found = FindFirstObjectByType<GCNetworkManager>(FindObjectsInactive.Include);
        Debug.Log("[GCLobby] GetNetMgr: " + (found == null ? "NULL" : found.gameObject.name));
        return found;
    }

    string GetUsername()
    {
        string name = usernameInput?.text.Trim() ?? "";
        if (string.IsNullOrEmpty(name)) { ShowError("Enter a username."); return null; }
        if (errorText) errorText.text = "";
        return name;
    }

    string GetDisplayName(Player player)
    {
        if (player.CustomProperties.TryGetValue(GCNetworkManager.PROP_NAME, out object name))
            return name.ToString();
        return player.NickName ?? $"Player {player.ActorNumber}";
    }
}