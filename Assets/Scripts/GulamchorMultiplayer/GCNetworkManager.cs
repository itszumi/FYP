// GCNetworkManager.cs
// Singleton that handles Photon connection, room creation and joining.
// Attach to a persistent GameObject in your Lobby scene.
// Survives scene loads via DontDestroyOnLoad.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class GCNetworkManager : MonoBehaviourPunCallbacks
{
    public static GCNetworkManager Instance { get; private set; }

    [Header("Scene Names")]
    public string lobbyScene = "GCMultiplayerLobby";
    public string gameScene = "GCMultiplayer";

    [Header("Room Settings")]
    public int maxPlayers = 5;
    public int minPlayersToStart = 2;

    // Local player info (set before connecting)
    [HideInInspector] public string localPlayerName = "";
    [HideInInspector] public int localAvatarIndex = 0;

    // Callbacks to Lobby UI
    public System.Action OnConnectedCallback;
    public System.Action<string> OnConnectionFailedCallback;
    public System.Action OnRoomCreatedCallback;
    public System.Action<string> OnRoomCreateFailedCallback;
    public System.Action OnRoomJoinedCallback;
    public System.Action<string> OnRoomJoinFailedCallback;
    public System.Action<Player> OnPlayerJoinedCallback;
    public System.Action<Player> OnPlayerLeftCallback;
    public System.Action OnAllPlayersReadyCallback;
    public System.Action OnGameStartingCallback;

    private bool _isConnecting = false;

    // Custom property keys
    public const string PROP_NAME = "name";
    public const string PROP_AVATAR = "avatar";
    public const string PROP_READY = "ready";
    public const string PROP_TURN = "turn";   // current turn actor number
    public const string PROP_STARTED = "started";

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        Debug.Log($"[GCNet] Awake called. Instance={Instance}, this={this.GetInstanceID()}");
        if (Instance != null && Instance != this)
        {
            Debug.Log("[GCNet] Duplicate detected - destroying this one");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log($"[GCNet] Instance SET successfully. Scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        PhotonNetwork.AutomaticallySyncScene = true;
    }

    void OnDestroy()
    {
        Debug.LogError("[GCNet] NetworkManager DESTROYED! Instance will be null. Stack: " + System.Environment.StackTrace);
        if (Instance == this) Instance = null;
    }

    public void ConnectAndCreateRoom(string roomCode, string playerName, int avatarIndex)
    {
        if (_isConnecting) return;
        localPlayerName = playerName;
        localAvatarIndex = avatarIndex;
        _pendingRoomCode = roomCode.ToUpper();
        _pendingAction = PendingAction.Create;
        _isConnecting = true;

        if (PhotonNetwork.IsConnected)
            ExecutePendingAction();
        else
        {
            PhotonNetwork.NickName = playerName;
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public void ConnectAndJoinRoom(string roomCode, string playerName, int avatarIndex)
    {
        if (_isConnecting) return;
        localPlayerName = playerName;
        localAvatarIndex = avatarIndex;
        _pendingRoomCode = roomCode.ToUpper();
        _pendingAction = PendingAction.Join;
        _isConnecting = true;

        if (PhotonNetwork.IsConnected)
            ExecutePendingAction();
        else
        {
            PhotonNetwork.NickName = playerName;
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public void LeaveRoom()
    {
        if (PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom();
    }

    public void Disconnect()
    {
        if (PhotonNetwork.IsConnected)
            PhotonNetwork.Disconnect();
    }

    // ── Room helpers ──────────────────────────────────────────────────────────

    /// <summary>Master client calls this to start the game for everyone.</summary>
    public void StartGame()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        // Lock room so no new players join mid-game
        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.IsVisible = false;

        // Set started flag
        var props = new Hashtable { { PROP_STARTED, true } };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);

        // Load game scene on all clients
        PhotonNetwork.LoadLevel(gameScene);
    }

    /// <summary>Returns a random 4-character room code.</summary>
    public static string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        char[] code = new char[4];
        for (int i = 0; i < 4; i++)
            code[i] = chars[Random.Range(0, chars.Length)];
        return new string(code);
    }

    // Sets this player's custom properties visible to all
    public void SetLocalPlayerProperties()
    {
        var props = new Hashtable
        {
            { PROP_NAME,   localPlayerName  },
            { PROP_AVATAR, localAvatarIndex },
            { PROP_READY,  false            }
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    // ── Pending action ────────────────────────────────────────────────────────

    private enum PendingAction { None, Create, Join }
    private PendingAction _pendingAction = PendingAction.None;
    private string _pendingRoomCode = "";

    void ExecutePendingAction()
    {
        SetLocalPlayerProperties();

        if (_pendingAction == PendingAction.Create)
        {
            var opts = new RoomOptions
            {
                MaxPlayers = (byte)maxPlayers,
                IsOpen = true,
                IsVisible = true,
                PublishUserId = false,
                CustomRoomProperties = new Hashtable { { PROP_STARTED, false } },
                CustomRoomPropertiesForLobby = new string[] { PROP_STARTED }
            };
            PhotonNetwork.CreateRoom(_pendingRoomCode, opts);
        }
        else if (_pendingAction == PendingAction.Join)
        {
            PhotonNetwork.JoinRoom(_pendingRoomCode);
        }

        _pendingAction = PendingAction.None;
        _isConnecting = false;
    }

    // ── Photon Callbacks ──────────────────────────────────────────────────────

    public override void OnConnectedToMaster()
    {
        Debug.Log("[GCNet] Connected to Master");
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("[GCNet] Joined Lobby");
        OnConnectedCallback?.Invoke();
        if (_pendingAction != PendingAction.None)
            ExecutePendingAction();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        _isConnecting = false;
        Debug.LogWarning($"[GCNet] Disconnected: {cause}");
        OnConnectionFailedCallback?.Invoke(cause.ToString());
    }

    public override void OnCreatedRoom()
    {
        Debug.Log($"[GCNet] Room created: {PhotonNetwork.CurrentRoom.Name}");
        OnRoomCreatedCallback?.Invoke();
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        _isConnecting = false;
        Debug.LogWarning($"[GCNet] Create room failed: {message}");
        OnRoomCreateFailedCallback?.Invoke(message);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"[GCNet] Joined room: {PhotonNetwork.CurrentRoom.Name}");
        SetLocalPlayerProperties();
        OnRoomJoinedCallback?.Invoke();
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        _isConnecting = false;
        Debug.LogWarning($"[GCNet] Join room failed: {message}");
        OnRoomJoinFailedCallback?.Invoke(message);
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"[GCNet] Player joined: {newPlayer.NickName}");
        OnPlayerJoinedCallback?.Invoke(newPlayer);
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"[GCNet] Player left: {otherPlayer.NickName}");
        OnPlayerLeftCallback?.Invoke(otherPlayer);
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        Debug.Log($"[GCNet] New master client: {newMasterClient.NickName}");
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(PROP_STARTED))
        {
            bool started = (bool)propertiesThatChanged[PROP_STARTED];
            if (started) OnGameStartingCallback?.Invoke();
        }
    }
}