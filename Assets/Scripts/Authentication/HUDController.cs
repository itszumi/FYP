// HUDController.cs
// HUD with sprite-based Pause / Back / Resume buttons.
// Persists across all scenes via DontDestroyOnLoad.
//
// HUD Hierarchy:
//   HUDCanvas
//     HUDPanel
//       LeftBtn          ← Button with Image component (shows pause OR back sprite)
//       RightSection
//         Username       ← TextMeshProUGUI
//         AvatarImage    ← Image
//     PausePanel         ← hidden by default
//       ResumeBtn        ← Button with resume sprite
//       RestartBtn       ← Button with restart sprite
//       HomeBtn          ← Button with home sprite

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class HUDController : MonoBehaviour
{
    public static HUDController Instance { get; private set; }

    [Header("HUD Panel")]
    public GameObject hudPanel;

    [Header("Left Button (Pause in game / Back in menus)")]
    public Button leftBtn;
    public Image leftBtnImage;   // Image on the button — we swap sprites
    public Sprite pauseSprite;
    public Sprite resumeSprite;
    public Sprite backSprite;

    [Header("Avatar + Username")]
    public Image avatarImage;
    public TextMeshProUGUI usernameText;

    [Header("Pause Panel")]
    public GameObject pausePanel;
    public Button resumeBtn;
    public Button restartBtn;
    public Button homeBtn;

    [Header("Scene Classification")]
    public string[] hiddenScenes = { "Login" };
    public string[] menuScenes = { "Menu", "Game Selection", "MultiplayerLobby" };
    public string homeScene = "Menu";

    private bool _isPaused = false;

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        leftBtn?.onClick.AddListener(OnLeftButtonClicked);
        resumeBtn?.onClick.AddListener(ResumeGame);
        restartBtn?.onClick.AddListener(RestartGame);
        homeBtn?.onClick.AddListener(GoHome);

        if (pausePanel) pausePanel.SetActive(false);
        Refresh();
    }

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Time.timeScale = 1f;
        _isPaused = false;
        if (pausePanel) pausePanel.SetActive(false);
        Refresh();
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    public void Refresh()
    {
        string scene = SceneManager.GetActiveScene().name;
        bool loggedIn = PlayerSession.IsLoggedIn();
        bool hide = IsInList(scene, hiddenScenes);

        if (hudPanel) hudPanel.SetActive(loggedIn && !hide);
        if (!loggedIn || hide) return;

        // Avatar
        if (avatarImage != null && AvatarManager.Instance != null)
        {
            int idx = LocalAuth.GetAvatar(PlayerSession.GetUser());
            PlayerSession.SetUser(PlayerSession.GetUser(), idx);
            avatarImage.sprite = AvatarManager.Instance.GetSprite(idx);
        }

        // Username
        if (usernameText != null)
            usernameText.text = PlayerSession.GetUser();

        // Left button sprite
        bool isMenu = IsInList(scene, menuScenes);
        if (leftBtn != null)
        {
            // Hide back button on the home scene itself
            bool show = isMenu ? scene != homeScene : true;
            leftBtn.gameObject.SetActive(show);

            if (leftBtnImage != null)
                leftBtnImage.sprite = isMenu ? backSprite : pauseSprite;
        }
    }

    // ── Left button ───────────────────────────────────────────────────────────

    void OnLeftButtonClicked()
    {
        string scene = SceneManager.GetActiveScene().name;
        bool isMenu = IsInList(scene, menuScenes);

        if (isMenu) GoBack();
        else TogglePause();
    }

    void GoBack() { Time.timeScale = 1f; SceneManager.LoadScene(homeScene); }

    void TogglePause()
    {
        if (_isPaused) ResumeGame();
        else PauseGame();
    }

    // ── Pause / Resume ────────────────────────────────────────────────────────

    public void PauseGame()
    {
        _isPaused = true;
        Time.timeScale = 0f;
        if (pausePanel) pausePanel.SetActive(true);
        if (leftBtnImage && resumeSprite) leftBtnImage.sprite = resumeSprite;
    }

    public void ResumeGame()
    {
        _isPaused = false;
        Time.timeScale = 1f;
        if (pausePanel) pausePanel.SetActive(false);
        if (leftBtnImage && pauseSprite) leftBtnImage.sprite = pauseSprite;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        _isPaused = false;
        if (pausePanel) pausePanel.SetActive(false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void GoHome()
    {
        Time.timeScale = 1f;
        _isPaused = false;
        if (pausePanel) pausePanel.SetActive(false);
        SceneManager.LoadScene(homeScene);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    bool IsInList(string scene, string[] list)
    {
        if (list == null) return false;
        foreach (string s in list)
            if (string.Equals(scene, s, System.StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}