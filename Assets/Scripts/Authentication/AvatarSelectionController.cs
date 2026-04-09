using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class AvatarSelectionController : MonoBehaviour
{
    [Header("Avatar Buttons (0-5)")]
    public Button[] avatarButtons;

    [Header("Confirm Button")]
    public Button confirmButton;

    [Header("Selector — place inside AvatarPanel (NOT AvatarGrid)")]
    [Tooltip("Gold ring that moves to whichever avatar is selected")]
    public RectTransform selector;

    [Tooltip("TickMark child of Selector")]
    public GameObject tickMark;

    [Header("Selector Size — should match button size")]
    public float selectorSize = 110f;

    [Header("Scene to load after confirm")]
    public string nextScene = "Game Selection";

    private int _selected = 0;
    private string _username = "";

    // ── Called from LoginUIController after registration ──────────────────────
    public void Show(string username)
    {
        _username = username;
        _selected = 0;
        gameObject.SetActive(true);

        // Set sprites on AvatarImage child of each button
        for (int i = 0; i < avatarButtons.Length; i++)
        {
            if (AvatarManager.Instance != null)
            {
                Transform avatarImgT = avatarButtons[i].transform.Find("AvatarImage");
                if (avatarImgT != null)
                {
                    Image img = avatarImgT.GetComponent<Image>();
                    if (img != null)
                        img.sprite = AvatarManager.Instance.GetSprite(i);
                }
                else
                {
                    // Fallback: set on button's own Image if no AvatarImage child
                    Image img = avatarButtons[i].GetComponent<Image>();
                    if (img != null)
                        img.sprite = AvatarManager.Instance.GetSprite(i);
                }
            }

            int idx = i;
            avatarButtons[i].onClick.RemoveAllListeners();
            avatarButtons[i].onClick.AddListener(() => SelectAvatar(idx));
        }

        confirmButton.onClick.RemoveAllListeners();
        confirmButton.onClick.AddListener(OnConfirm);

        // Setup selector size
        if (selector != null)
        {
            selector.sizeDelta = new Vector2(selectorSize, selectorSize);
            selector.gameObject.SetActive(false);
        }

        if (tickMark != null)
            tickMark.SetActive(false);

        RefreshHighlight();
    }

    private void SelectAvatar(int index)
    {
        _selected = index;
        RefreshHighlight();
    }

    private void RefreshHighlight()
    {
        if (avatarButtons == null || avatarButtons.Length == 0) return;
        if (_selected < 0 || _selected >= avatarButtons.Length) return;

        // Move selector to selected button's world position
        if (selector != null)
        {
            selector.gameObject.SetActive(true);
            selector.position = avatarButtons[_selected].transform.position;
        }

        // Show tick mark
        if (tickMark != null)
            tickMark.SetActive(true);
    }

    private void OnConfirm()
    {
        LocalAuth.SaveAvatar(_username, _selected);
        CurrencyManager.InitForUser(_username);
        PlayerSession.SetUser(_username, _selected);
        HUDController.Instance?.Refresh();
        SceneManager.LoadScene(nextScene);
    }
}