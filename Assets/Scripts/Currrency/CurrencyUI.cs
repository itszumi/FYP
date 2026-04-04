// CurrencyUI.cs — Uses TextMesh Pro

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CurrencyUI : MonoBehaviour
{
    [Header("Coin display")]
    public TextMeshProUGUI coinText;

    [Header("Avatar display (optional)")]
    public Image avatarImage;

    void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (coinText != null)
            coinText.text = $"Coins: {CurrencyManager.GetCoins()}";

        if (avatarImage != null && AvatarManager.Instance != null)
            avatarImage.sprite = AvatarManager.Instance.GetSprite(PlayerSession.GetAvatarIndex());
    }
}