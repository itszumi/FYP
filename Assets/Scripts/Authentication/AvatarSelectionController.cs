// AvatarSelectionController.cs
// Attach to a Panel GameObject that contains 6 avatar Button children.
// After the player registers, LoginUIController calls Show(username).
// They pick an avatar, then click Confirm to proceed to the main game scene.
//
// Hierarchy expected:
//   AvatarPanel
//     AvatarButton0  (Button + Image child)
//     AvatarButton1
//     ...
//     AvatarButton5
//     ConfirmButton  (Button)

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class AvatarSelectionController : MonoBehaviour
{
    [Header("Avatar Buttons (assign in order 0-5)")]
    public Button[] avatarButtons;          // 6 buttons, each with an Image child

    [Header("Confirm Button")]
    public Button confirmButton;

    [Header("Selected Highlight Color")]
    public Color selectedColor = new Color(1f, 0.85f, 0.2f, 1f); // gold
    public Color unselectedColor = Color.white;

    [Header("Scene to load after avatar pick")]
    public string nextScene = "Game Selection";

    private int _selected = 0;
    private string _username = "";

    // ── Called from LoginUIController right after registration succeeds ────────
    public void Show(string username)
    {
        _username = username;
        _selected = 0;
        gameObject.SetActive(true);
        RefreshHighlight();

        for (int i = 0; i < avatarButtons.Length; i++)
        {
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
        // Persist chosen avatar
        LocalAuth.SaveAvatar(_username, _selected);

        // Initialise currency for this user (first-time: gives 1000 coins)
        CurrencyManager.InitForUser(_username);

        // Set session
        PlayerSession.SetUser(_username, _selected);

        // Refresh HUD so it picks up the new avatar + username immediately
        HUDController.Instance?.Refresh();

        SceneManager.LoadScene(nextScene);
    }
}