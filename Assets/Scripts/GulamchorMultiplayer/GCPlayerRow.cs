using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Realtime;

public class GCPlayerRow : MonoBehaviour
{
    public Image avatarImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI statusText;

    void Awake()
    {
        if (avatarImage == null) avatarImage = transform.Find("AvatarImage")?.GetComponent<Image>();
        if (nameText == null) nameText = transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
        if (statusText == null) statusText = transform.Find("StatusText")?.GetComponent<TextMeshProUGUI>();

        // Fix layout on the row itself
        var hlg = GetComponent<HorizontalLayoutGroup>();
        if (hlg != null)
        {
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.spacing = 12f;
            hlg.padding = new RectOffset(10, 10, 8, 8);
        }

        // Fix avatar size
        if (avatarImage != null)
        {
            var rt = avatarImage.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(50f, 50f);
            avatarImage.preserveAspect = true;
        }

        // Fix name text
        if (nameText != null)
        {
            var rt = nameText.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(180f, 50f);
            nameText.fontSize = 16f;
            nameText.alignment = TextAlignmentOptions.MidlineLeft;
            nameText.overflowMode = TextOverflowModes.Ellipsis;
        }

        // Fix status text
        if (statusText != null)
        {
            var rt = statusText.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(80f, 50f);
            statusText.fontSize = 13f;
            statusText.color = new Color(0.7f, 0.7f, 0.7f);
            statusText.alignment = TextAlignmentOptions.MidlineRight;
        }

        // Row background
        var bg = GetComponent<Image>();
        if (bg != null) bg.color = new Color(0.18f, 0.14f, 0.08f, 0.9f);

        // Row size
        var rowRt = GetComponent<RectTransform>();
        if (rowRt != null) rowRt.sizeDelta = new Vector2(340f, 66f);
    }

    public void Setup(Player player, bool isLocal)
    {
        string displayName = GetName(player);
        string suffix = (player.IsMasterClient ? " [Host]" : "") + (isLocal ? " (You)" : "");

        if (nameText != null)
        {
            nameText.text = displayName + suffix;
            nameText.color = isLocal ? new Color(1f, 0.84f, 0f) : Color.white;
        }

        if (avatarImage != null && AvatarManager.Instance != null)
        {
            int idx = 0;
            if (player.CustomProperties.TryGetValue(GCNetworkManager.PROP_AVATAR, out object avObj))
            {
                if (avObj is int i) idx = i;
                else if (avObj is byte b) idx = b;
            }
            else if (isLocal && PlayerSession.IsLoggedIn())
                idx = PlayerSession.GetAvatarIndex();

            Sprite spr = AvatarManager.Instance.GetSprite(idx);
            if (spr != null) { avatarImage.sprite = spr; avatarImage.color = Color.white; }
        }

        if (statusText != null)
            statusText.text = isLocal ? "Ready ✓" : "Waiting...";
    }

    string GetName(Player player)
    {
        if (player.CustomProperties.TryGetValue(GCNetworkManager.PROP_NAME, out object n))
            return n.ToString();
        return string.IsNullOrEmpty(player.NickName) ? $"Player {player.ActorNumber}" : player.NickName;
    }
}