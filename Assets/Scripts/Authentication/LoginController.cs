
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

    [Header("Avatar Selection Panel")]
    public AvatarSelectionController avatarSelectionController;

    [Header("Scene after login")]
    public string nextSceneAfterLogin = "Game Selection";

    private bool _isRegisterMode = true;
    private bool _eyeHeld = false;

    public Selectable firstSelected;
    void Start()
    {
        if (avatarSelectionController != null)
            avatarSelectionController.gameObject.SetActive(false);

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

        var system = EventSystem.current;
        
    }

    void Update()
    {
        // TMP_InputField uses contentType enum too
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

        // ---- New Input System for Tab/Shift+Tab navigation ----
        if (Keyboard.current != null)
        {
            if (Keyboard.current.tabKey.wasPressedThisFrame)
            {
                bool isShift = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
                var current = EventSystem.current.currentSelectedGameObject?.GetComponent<Selectable>();

                Selectable target = null;
                if (isShift)
                    target = current?.FindSelectableOnUp();
                else
                    target = current?.FindSelectableOnDown();

                if (target != null)
                    target.Select();
            }
        }
    }

    // ── Eye hold-to-reveal ────────────────────────────────────────────────────
    private void SetupEyeButton()
    {
        EventTrigger trigger = eyeButton.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = eyeButton.gameObject.AddComponent<EventTrigger>();

        trigger.triggers.Clear();

        EventTrigger.Entry down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener((_) => _eyeHeld = true);
        trigger.triggers.Add(down);

        EventTrigger.Entry up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        up.callback.AddListener((_) => _eyeHeld = false);
        trigger.triggers.Add(up);

        EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
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
            switchLabel.text = _isRegisterMode ? "Already have an account? Login" : "New here? Register";
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

        if (pass != confirm)
        {
            statusText.text = "Passwords do not match.";
            return;
        }

        if (LocalAuth.UserExists(user))
        {
            statusText.text = "Username already taken.";
            return;
        }

        bool ok = LocalAuth.Register(user, pass);
        if (!ok) { statusText.text = "Registration failed."; return; }

        statusText.text = "Account created! Pick your avatar.";

        if (avatarSelectionController != null)
        {
            avatarSelectionController.nextScene = nextSceneAfterLogin;
            avatarSelectionController.Show(user);
        }
        else
        {
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
            SceneManager.LoadScene(nextSceneAfterLogin);
        }
        else
        {
            statusText.text = "Incorrect password.";
        }
    }
}