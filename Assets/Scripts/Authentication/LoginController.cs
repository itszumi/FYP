using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LoginUIController : MonoBehaviour
{
    [Header("Inputs")]
    public InputField usernameInput;
    public InputField passwordInput;
    public InputField confirmPasswordInput;

    [Header("Buttons")]
    public Button continueButton;
    public Button switchModeButton;
    public Button eyeButton;

    [Header("UI")]
    public Text modeTitleText;
    public Text statusText;

    private bool isRegisterMode = true;
    private bool eyeHeld = false;

    void Start()
    {
        if (LocalAuth.HasUser())
        {
            usernameInput.text = LocalAuth.GetSavedUsername();
            usernameInput.interactable = false;
            isRegisterMode = false;
        }

        UpdateModeUI();
        continueButton.onClick.AddListener(OnContinueClicked);

        eyeButton.onClick.AddListener(() => { });
    }

    void Update()
    {
        if (eyeHeld)
        {
            passwordInput.contentType = InputField.ContentType.Standard;
            confirmPasswordInput.contentType = InputField.ContentType.Standard;
        }
        else
        {
            passwordInput.contentType = InputField.ContentType.Password;
            confirmPasswordInput.contentType = InputField.ContentType.Password;
        }
        passwordInput.ForceLabelUpdate();
        confirmPasswordInput.ForceLabelUpdate();
    }

    public void OnEyeDown()
    {
        eyeHeld = true;
    }

    public void OnEyeUp()
    {
        eyeHeld = false;
    }

    public void ToggleMode()
    {
        isRegisterMode = !isRegisterMode;
        UpdateModeUI();
    }

    private void UpdateModeUI()
    {
        confirmPasswordInput.gameObject.SetActive(isRegisterMode);
        modeTitleText.text = isRegisterMode ? "Create Account" : "Login";
        switchModeButton.GetComponentInChildren<Text>().text = isRegisterMode ? "Have account? Login" : "New? Register";
    }

    private void OnContinueClicked()
    {
        statusText.text = "";

        if (isRegisterMode)
        {
            if (passwordInput.text != confirmPasswordInput.text)
            {
                statusText.text = "Passwords do not match.";
                return;
            }

            bool ok = LocalAuth.Register(usernameInput.text, passwordInput.text);
            if (!ok) { statusText.text = "Invalid username/password."; return; }

            statusText.text = "Account created!";
            isRegisterMode = false;
            UpdateModeUI();
        }
        else
        {
            if (LocalAuth.Login(passwordInput.text))
            {
                SceneManager.LoadScene("MainMenu");
            }
            else
            {
                statusText.text = "Wrong password.";
            }
        }
    }
}