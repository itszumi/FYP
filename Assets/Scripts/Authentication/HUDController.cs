// HUDController.cs
// Auto-finds AvatarImage and UsernameText by GameObject name inside the HUD panel,
// so you never need to drag anything into the Inspector at runtime.
//
// ── Required Hierarchy ────────────────────────────────────────────────────────
//   HUDCanvas          (Canvas, Screen Space Overlay, Sort Order 10)
//     Panel            ← assign this as "Hud Panel" in the Inspector
//       AvatarImage    (GameObject name must be exactly "AvatarImage")
//       UsernameText   (GameObject name must be exactly "UsernameText")
//       LogoutButton   (GameObject name must be exactly "LogoutButton")  ← optional
//
// Only "Hud Panel" needs to be dragged in. Everything inside it is found by name.
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class HUDController : MonoBehaviour
{
    public static HUDController Instance { get; private set; }

    [Header("HUD Root")]
    [Tooltip("Drag the Panel child of HUDCanvas here. Children are found automatically.")]
    public GameObject hudPanel;

    [Header("Scenes where HUD is hidden")]
    public string[] hiddenScenes = { "Login", "Splash", "Boot" };

    // ── Found at runtime ──────────────────────────────────────────────────────
    private Image _avatarImage;
    private TextMeshProUGUI _usernameText;
    private Button _logoutButton;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        FindHUDChildren();
    }

    private void Start()
    {
        if (_logoutButton != null)
            _logoutButton.onClick.AddListener(OnLogoutClicked);

        Refresh();
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    // ── Auto-find children by name ────────────────────────────────────────────

    private void FindHUDChildren()
    {
        if (hudPanel == null)
        {
            Debug.LogError("HUDController: Hud Panel is not assigned!");
            return;
        }

        // Find by exact GameObject name anywhere inside the panel
        Transform avatarT = hudPanel.transform.Find("AvatarImage");
        Transform usernameT = hudPanel.transform.Find("UsernameText");
        Transform logoutT = hudPanel.transform.Find("LogoutButton");

        if (avatarT != null) _avatarImage = avatarT.GetComponent<Image>();
        if (usernameT != null) _usernameText = usernameT.GetComponent<TextMeshProUGUI>();
        if (logoutT != null) _logoutButton = logoutT.GetComponent<Button>();

        // Friendly warnings if something is missing
        if (_avatarImage == null) Debug.LogWarning("HUDController: 'AvatarImage' Image not found inside HUD Panel.");
        if (_usernameText == null) Debug.LogWarning("HUDController: 'UsernameText' TextMeshProUGUI not found inside HUD Panel.");
    }

    // ── Scene callback ────────────────────────────────────────────────────────

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Refresh();
    }

    // ── Public Refresh ────────────────────────────────────────────────────────

    /// <summary>
    /// Re-reads PlayerSession + AvatarManager and updates the HUD.
    /// Call this after login or avatar selection completes.
    /// </summary>
    public void Refresh()
    {
        bool shouldShow = PlayerSession.IsLoggedIn() && !IsCurrentSceneHidden();
        hudPanel.SetActive(shouldShow);
        if (!shouldShow) return;

        // Username
        if (_usernameText != null)
            _usernameText.text = PlayerSession.GetUser();

        // Avatar sprite
        if (_avatarImage != null && AvatarManager.Instance != null)
            _avatarImage.sprite = AvatarManager.Instance.GetSprite(PlayerSession.GetAvatarIndex());
    }

    // ── Logout ────────────────────────────────────────────────────────────────

    private void OnLogoutClicked()
    {
        PlayerSession.Clear();
        hudPanel.SetActive(false);
        SceneManager.LoadScene("Login");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool IsCurrentSceneHidden()
    {
        string current = SceneManager.GetActiveScene().name;
        foreach (string s in hiddenScenes)
            if (string.Equals(current, s, System.StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}