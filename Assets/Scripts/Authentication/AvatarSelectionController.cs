// AvatarSelectionController.cs
// Matches your scene hierarchy:
//   Canvas
//     AvatarPanel
//       AvatarButton0 ... AvatarButton5   (each Button has a child Image with the sprite)
//       ConfirmButton
//
// LoginUIController calls Show(username) after registration.
// On confirm: saves avatar to LocalAuth + PlayerPrefs, sets PlayerSession, refreshes HUD.

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class AvatarSelectionController : MonoBehaviour
{
    [Header("Avatar Buttons (assign in order 0–5)")]
    public Button[] avatarButtons;

    [Header("Confirm Button")]
    public Button confirmButton;

    [Header("Selection Colors")]
    public Color selectedColor = new Color(1f, 0.85f, 0.2f, 1f); // gold
    public Color unselectedColor = Color.white;

    [Header("Scene to load after avatar pick")]
    public string nextScene = "Game Selection";

    private int _selected = 0;
    private string _username = "";

    // ── Called from LoginUIController after registration ──────────────────────
    public void Show(string username)
    {
        _username = username;
        _selected = 0;

        gameObject.SetActive(true);
        RefreshHighlight();

        // Set sprites on buttons from AvatarManager
        for (int i = 0; i < avatarButtons.Length; i++)
        {
            // Each button's own Image shows the avatar sprite
            Image btnImage = avatarButtons[i].GetComponent<Image>();
            if (btnImage != null && AvatarManager.Instance != null)
                btnImage.sprite = AvatarManager.Instance.GetSprite(i);

            int idx = i;
            avatarButtons[i].onClick.RemoveAllListeners();
            avatarButtons[i].onClick.AddListener(() => SelectAvatar(idx));
        }

        confirmButton.onClick.RemoveAllListeners();
        confirmButton.onClick.AddListener(OnConfirm);
    }

    private void SelectAvatar(int index)
    {
        _selected = index;
        RefreshHighlight();
    }

    private void RefreshHighlight()
    {
        for (int i = 0; i < avatarButtons.Length; i++)
        {
            Image img = avatarButtons[i].GetComponent<Image>();
            if (img != null)
                img.color = (i == _selected) ? selectedColor : unselectedColor;
        }
    }

    private void OnConfirm()
    {
        // 1. Save avatar to PlayerPrefs under this user
        LocalAuth.SaveAvatar(_username, _selected);

        // 2. Init currency (first login)
        CurrencyManager.InitForUser(_username);

        // 3. Set runtime session
        PlayerSession.SetUser(_username, _selected);

        // 4. Refresh HUD immediately so avatar + username appear
        HUDController.Instance?.Refresh();

        // 5. Load game
        SceneManager.LoadScene(nextScene);
        Debug.Log($"[Avatar] Confirming avatar index: {_selected} for user: {_username}");
        LocalAuth.SaveAvatar(_username, _selected);
    }
}
