// LoginUIController.cs
// Fixed: AvatarPanel now correctly shows after registration.
// The panel is kept ACTIVE in the scene but rendered invisible via CanvasGroup,
// so AvatarSelectionController.Start() always runs and is ready to call Show().

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.InputSystem;

public class LoginUIController : MonoBehaviour
{
    [Header("Input Fields")]
    public TMP_InputField usernameInput;
    public TMP_InputField passwordInput;
    public TMP_InputField confirmPasswordInput;

    [Header("Buttons")]
    public Button continueButton;
    public Button switchModeButton;
    public Button eyeButton;

    [Header("UI Texts")]
    public TextMeshProUGUI modeTitleText;
    public TextMeshProUGUI statusText;

    [Header("Login Panel (to hide during avatar selection)")]
    public GameObject loginPanel;           // drag the LoginPanel GameObject here

    [Header("Avatar Selection Panel")]
    public AvatarSelectionController avatarSelectionController;

    [Header("Scene after login")]
    public string nextSceneAfterLogin = "Game Selection";

    private bool _isRegisterMode = true;
    private bool _eyeHeld = false;

    void Start()
    {
        // ── IMPORTANT: Keep AvatarPanel's GameObject ACTIVE in the scene.
        // We hide it by disabling its CanvasGroup instead of the GameObject,
        // so all child scripts (AvatarSelectionController) can initialise.
        // If you don't use CanvasGroup, just make sure the panel starts visible
        // in the Editor and this script will hide it:
        if (avatarSelectionController != null)
            SetAvatarPanelVisible(false);

        string lastUser = LocalAuth.GetLastUser();
        if (!string.IsNullOrEmpty(lastUser))
        {
            usernameInput.text = lastUser;
            usernameInput.interactable = false;
            _isRegisterMode = false;
        }

        UpdateModeUI();

        continueButton.onClick.AddListener(OnContinueClicked);
        switchModeButton.onClick.AddListener(ToggleMode);

        SetupEyeButton();
    }

    // ── Eye hold-to-reveal ────────────────────────────────────────────────────

    void Update()
    {
        TMP_InputField.ContentType ct = _eyeHeld
            ? TMP_InputField.ContentType.Standard
            : TMP_InputField.ContentType.Password;

        if (passwordInput.contentType != ct)
        {
            passwordInput.contentType = ct;
            confirmPasswordInput.contentType = ct;
            passwordInput.ForceLabelUpdate();
            confirmPasswordInput.ForceLabelUpdate();
        }

        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            bool isShift = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
            var current = EventSystem.current.currentSelectedGameObject?.GetComponent<Selectable>();
            Selectable target = isShift ? current?.FindSelectableOnUp() : current?.FindSelectableOnDown();
            target?.Select();
        }
    }

    private void SetupEyeButton()
    {
        EventTrigger trigger = eyeButton.gameObject.GetComponent<EventTrigger>()
                            ?? eyeButton.gameObject.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener((_) => _eyeHeld = true);
        trigger.triggers.Add(down);

        var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        up.callback.AddListener((_) => _eyeHeld = false);
        trigger.triggers.Add(up);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener((_) => _eyeHeld = false);
        trigger.triggers.Add(exit);
    }

    public void OnEyeDown() => _eyeHeld = true;
    public void OnEyeUp() => _eyeHeld = false;

    // ── Mode switching ────────────────────────────────────────────────────────

    public void ToggleMode()
    {
        _isRegisterMode = !_isRegisterMode;
        usernameInput.interactable = true;
        statusText.text = "";
        UpdateModeUI();
    }

    private void UpdateModeUI()
    {
        confirmPasswordInput.gameObject.SetActive(_isRegisterMode);
        modeTitleText.text = _isRegisterMode ? "Create Account" : "Login";

        TextMeshProUGUI switchLabel = switchModeButton.GetComponentInChildren<TextMeshProUGUI>();
        if (switchLabel != null)
            switchLabel.text = _isRegisterMode
                ? "Already have an account? Login"
                : "New here? Register";
    }

    // ── Continue button ───────────────────────────────────────────────────────

    private void OnContinueClicked()
    {
        statusText.text = "";
        string user = usernameInput.text.Trim();
        string pass = passwordInput.text;

        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        {
            statusText.text = "Please fill in all fields.";
            return;
        }

        if (_isRegisterMode) HandleRegister(user, pass);
        else HandleLogin(user, pass);
    }

    private void HandleRegister(string user, string pass)
    {
        string confirm = confirmPasswordInput.text;

        if (pass != confirm) { statusText.text = "Passwords do not match."; return; }
        if (LocalAuth.UserExists(user)) { statusText.text = "Username already taken."; return; }

        bool ok = LocalAuth.Register(user, pass);
        if (!ok) { statusText.text = "Registration failed."; return; }

        statusText.text = "Account created! Pick your avatar.";

        if (avatarSelectionController != null)
        {
            // Hide login form, show avatar panel
            if (loginPanel != null) loginPanel.SetActive(false);
            SetAvatarPanelVisible(true);

            avatarSelectionController.nextScene = nextSceneAfterLogin;
            avatarSelectionController.Show(user);
        }
        else
        {
            // Fallback: no avatar panel assigned
            CurrencyManager.InitForUser(user);
            PlayerSession.SetUser(user, 0);
            SceneManager.LoadScene(nextSceneAfterLogin);
        }
    }

    private void HandleLogin(string user, string pass)
    {
        if (!LocalAuth.UserExists(user))
        {
            statusText.text = "No account found. Please register.";
            return;
        }

        if (LocalAuth.Login(user, pass))
        {
            int avatar = LocalAuth.GetAvatar(user);
            PlayerSession.SetUser(user, avatar);
            HUDController.Instance?.Refresh();
            SceneManager.LoadScene(nextSceneAfterLogin);
        }
        else
        {
            statusText.text = "Incorrect password.";
        }
    }

    // ── Avatar panel visibility ───────────────────────────────────────────────

    // Uses CanvasGroup so the GameObject stays active (scripts keep running)
    // but it becomes invisible and non-interactive.
    private void SetAvatarPanelVisible(bool visible)
    {
        if (avatarSelectionController == null) return;

        GameObject panelGO = avatarSelectionController.gameObject;

        CanvasGroup cg = panelGO.GetComponent<CanvasGroup>();
        if (cg == null) cg = panelGO.AddComponent<CanvasGroup>();

        cg.alpha = visible ? 1f : 0f;
        cg.interactable = visible;
        cg.blocksRaycasts = visible;
    }
}