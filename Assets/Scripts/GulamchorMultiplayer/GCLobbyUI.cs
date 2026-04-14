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

public class GCLobbyUI : MonoBehaviour
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

        // Pre-fill username from PlayerSession
        if (usernameInput != null && PlayerSession.IsLoggedIn())
            usernameInput.text = PlayerSession.GetUser();

        // Wire buttons
        createRoomBtn?.onClick.AddListener(OnCreateRoom);
        joinRoomBtn?.onClick.AddListener(OnJoinRoom);
        backBtn?.onClick.AddListener(() => SceneManager.LoadScene(mainMenuScene));
        startGameBtn?.onClick.AddListener(OnStartGame);
        leaveBtn?.onClick.AddListener(OnLeaveRoom);

        // Wire network manager callbacks
        if (GCNetworkManager.Instance != null)
        {
            GCNetworkManager.Instance.OnConnectionFailedCallback = OnConnectionFailed;
            GCNetworkManager.Instance.OnRoomCreatedCallback = OnRoomCreated;
            GCNetworkManager.Instance.OnRoomCreateFailedCallback = OnRoomFailed;
            GCNetworkManager.Instance.OnRoomJoinedCallback = OnRoomJoined;
            GCNetworkManager.Instance.OnRoomJoinFailedCallback = OnRoomFailed;
            GCNetworkManager.Instance.OnPlayerJoinedCallback = OnPlayerJoined;
            GCNetworkManager.Instance.OnPlayerLeftCallback = OnPlayerLeft;
            GCNetworkManager.Instance.OnGameStartingCallback = OnGameStarting;
        }

        // If already in a room (e.g. scene reloaded), show waiting panel immediately
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

        string code = GCNetworkManager.GenerateRoomCode();
        ShowLoading($"Creating room {code}...");
        GCNetworkManager.Instance.ConnectAndCreateRoom(code, username,
            PlayerSession.IsLoggedIn() ? PlayerSession.GetAvatarIndex() : 0);
    }

    void OnJoinRoom()
    {
        string username = GetUsername();
        if (username == null) return;

        string code = joinCodeInput?.text.Trim().ToUpper() ?? "";
        if (code.Length != 4) { ShowError("Enter a 4-character room code."); return; }

        ShowLoading($"Joining room {code}...");
        GCNetworkManager.Instance.ConnectAndJoinRoom(code, username,
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

        // Set name text
        TextMeshProUGUI nameText = row.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
        if (nameText != null)
            nameText.text = GetDisplayName(player) +
                (player.IsMasterClient ? " [Host]" : "") +
                (player.IsLocal ? " (You)" : "");
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
        startGameBtn.interactable = isMaster && enoughPlayers;

        if (isMaster)
            ShowStatusText(enoughPlayers
                ? "Ready to start! Press Start Game."
                : $"Waiting for players... ({PhotonNetwork.CurrentRoom.PlayerCount}/{GCNetworkManager.Instance?.maxPlayers ?? 5})");
        else
            ShowStatusText($"Waiting for host to start... ({PhotonNetwork.CurrentRoom.PlayerCount}/{GCNetworkManager.Instance?.maxPlayers ?? 5})");
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
        if (startGameBtn) startGameBtn.gameObject.SetActive(PhotonNetwork.IsMasterClient);
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

    string GetUsername()
    {
        string name = usernameInput?.text.Trim() ?? "";
        if (string.IsNullOrEmpty(name)) { ShowError("Enter a username."); return null; }
        if (GCNetworkManager.Instance == null) { ShowError("NetworkManager not found!"); return null; }
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