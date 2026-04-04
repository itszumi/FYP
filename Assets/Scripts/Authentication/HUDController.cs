// HUDController.cs
// Matches your exact HUD hierarchy:
//   HUDCanvas
//     Panel
//       Image     ← avatar (exact GameObject name "Image")
//       Username  ← username label (exact GameObject name "Username")
//
// Only "Panel" is dragged in. Image and Username are found by name automatically.
// Persists across all scenes via DontDestroyOnLoad.
// Auto-hides on scenes listed in hiddenScenes[].

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class HUDController : MonoBehaviour
{
    public static HUDController Instance { get; private set; }

    [Header("HUD Root")]
    [Tooltip("Drag the 'Panel' child of HUDCanvas here.")]
    public GameObject hudPanel;

    [Header("Scenes where HUD is hidden")]
    public string[] hiddenScenes = { "Login", "Splash", "Boot" };

    // Found automatically by GameObject name
    private Image _avatarImage;
    private TextMeshProUGUI _usernameText;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        FindChildren();
    }

    private void Start() => Refresh();

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    // ── Find children by their exact GameObject names ─────────────────────────

    private void FindChildren()
    {
        if (hudPanel == null) { Debug.LogError("HUDController: Hud Panel not assigned!"); return; }

        // Matches your hierarchy: Panel > Image, Panel > Username
        Transform imgT = hudPanel.transform.Find("Image");
        Transform userT = hudPanel.transform.Find("Username");

        if (imgT != null) _avatarImage = imgT.GetComponent<Image>();
        if (userT != null) _usernameText = userT.GetComponent<TextMeshProUGUI>();

        if (_avatarImage == null) Debug.LogWarning("HUDController: Child 'Image' (Image component) not found in Panel.");
        if (_usernameText == null) Debug.LogWarning("HUDController: Child 'Username' (TextMeshProUGUI) not found in Panel.");
    }

    // ── Scene load ────────────────────────────────────────────────────────────

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Refresh();

    // ── Public Refresh ────────────────────────────────────────────────────────

    /// <summary>Call after login or avatar selection to update avatar + username.</summary>
    public void Refresh()
    {
        bool shouldShow = PlayerSession.IsLoggedIn() && !IsHiddenScene();
        hudPanel.SetActive(shouldShow);
        if (!shouldShow) return;

        // Username — pulled from PlayerSession (set during login/register)
        if (_usernameText != null)
            _usernameText.text = PlayerSession.GetUser();

        // Avatar — index stored in PlayerPrefs via LocalAuth.SaveAvatar()
        if (_avatarImage != null && AvatarManager.Instance != null)
        {
            int idx = LocalAuth.GetAvatar(PlayerSession.GetUser()); // always read latest from prefs
            PlayerSession.SetUser(PlayerSession.GetUser(), idx);    // keep session in sync
            _avatarImage.sprite = AvatarManager.Instance.GetSprite(idx);
        }
        Debug.Log($"[HUD] IsLoggedIn={PlayerSession.IsLoggedIn()} | User={PlayerSession.GetUser()} | AvatarIdx={PlayerSession.GetAvatarIndex()} | AvatarManager={AvatarManager.Instance != null} | Sprite={AvatarManager.Instance?.GetSprite(PlayerSession.GetAvatarIndex())?.name}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool IsHiddenScene()
    {
        string current = SceneManager.GetActiveScene().name;
        foreach (string s in hiddenScenes)
            if (string.Equals(current, s, System.StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}