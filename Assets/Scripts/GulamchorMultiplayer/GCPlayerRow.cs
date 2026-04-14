// GCPlayerRow.cs
// Attach to the PlayerRow prefab used in the lobby waiting panel.
// The prefab needs:
//   - TextMeshProUGUI child named "NameText"
//   - (Optional) Image child named "AvatarImage"
//   - (Optional) TextMeshProUGUI child named "StatusText"

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Realtime;

public class GCPlayerRow : MonoBehaviour
{
    [Header("Auto-found by name")]
    public TextMeshProUGUI nameText;
    public Image avatarImage;
    public TextMeshProUGUI statusText;

    void Awake()
    {
        if (nameText == null) nameText = transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
        if (avatarImage == null) avatarImage = transform.Find("AvatarImage")?.GetComponent<Image>();
        if (statusText == null) statusText = transform.Find("StatusText")?.GetComponent<TextMeshProUGUI>();
    }

    public void Setup(Player player, bool isLocal)
    {
        // Get display name from custom properties
        string displayName = player.NickName ?? $"Player {player.ActorNumber}";
        if (player.CustomProperties.TryGetValue(GCNetworkManager.PROP_NAME, out object nameObj))
            displayName = nameObj.ToString();

        string suffix = "";
        if (player.IsMasterClient) suffix += " [Host]";
        if (isLocal) suffix += " (You)";

        if (nameText) nameText.text = displayName + suffix;

        // Set avatar if AvatarManager is available
        if (avatarImage != null && AvatarManager.Instance != null)
        {
            int avatarIdx = 0;
            if (player.CustomProperties.TryGetValue(GCNetworkManager.PROP_AVATAR, out object avObj))
                avatarIdx = (int)avObj;
            Sprite spr = AvatarManager.Instance.GetSprite(avatarIdx);
            if (spr != null) avatarImage.sprite = spr;
        }

        if (statusText) statusText.text = "Waiting...";
    }
}