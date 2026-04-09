using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class AvatarSelectionController : MonoBehaviour
{
    [Header("Avatar Buttons (0-5)")]
    public Button[] avatarButtons;

    [Header("Confirm Button")]
    public Button confirmButton;

    [Header("Selector (ring + tick sprite — child of AvatarPanel)")]
    public RectTransform selector;

    [Header("Selector Size")]
    public float selectorWidth = 145f;
    public float selectorHeight = 140f;

    [Header("Scene to load after confirm")]
    public string nextScene = "Game Selection";

    private int _selected = 0;
    private string _username = "";

    public void Show(string username)
    {
        _username = username;
        _selected = 0;
        gameObject.SetActive(true);

        // Hide selector initially
        if (selector != null)
        {
            selector.sizeDelta = new Vector2(selectorWidth, selectorHeight);
            selector.gameObject.SetActive(false);
        }

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

        if (selector != null)
        {
            selector.gameObject.SetActive(true);
            selector.position = avatarButtons[_selected].transform.position;
            selector.sizeDelta = new Vector2(selectorWidth, selectorHeight);
        }
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