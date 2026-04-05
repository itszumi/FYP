// GCCardDisplay.cs
// Add this component to GCCardPrefab.
// Forces image visibility on every setup call.

using UnityEngine;
using UnityEngine.UI;

public class GCCardDisplay : MonoBehaviour
{
    [Header("Assign the Image component on this prefab")]
    public Image myImage;
    public Sprite cardBackSprite;

    [HideInInspector] public Card cardData;

    private GCGameManager _manager;
    private bool _isPickable = false;
    private int _handIndex = -1;
    private Button _button;

    void Awake()
    {
        _button = GetComponent<Button>();
        if (_button != null)
        {
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(OnClick);
        }
    }

    // ── Human's own cards — face up, not clickable ────────────────────────────
    public void SetupFaceUp(Card c, GCGameManager manager)
    {
        cardData = c;
        _manager = manager;
        _isPickable = false;
        _handIndex = -1;

        ApplySprite(c.cardSprite);
        if (_button != null) _button.interactable = false;
    }

    // ── AI hand area cards — show card back, not clickable ────────────────────
    public void SetupAICard(Card c)
    {
        cardData = c;
        _isPickable = false;

        ApplySprite(cardBackSprite);
        if (_button != null) _button.interactable = false;
    }

    // ── Pick panel — show card back, clickable ────────────────────────────────
    public void SetupFaceDown(Card c, GCGameManager manager, int handIndex)
    {
        cardData = c;
        _manager = manager;
        _isPickable = true;
        _handIndex = handIndex;

        ApplySprite(cardBackSprite);
        if (_button != null) _button.interactable = true;
    }

    // ── Force sprite + color + enabled in one place ───────────────────────────
    private void ApplySprite(Sprite sprite)
    {
        if (myImage == null)
        {
            Debug.LogError("[GCCardDisplay] myImage is NULL — assign it in the prefab Inspector!");
            return;
        }

        myImage.sprite = sprite;
        myImage.color = Color.white;  // full opacity, no tint
        myImage.enabled = true;
        myImage.raycastTarget = true;
        gameObject.SetActive(true);

        if (sprite == null)
            Debug.LogWarning($"[GCCardDisplay] Sprite is NULL on {gameObject.name}. Check cardSprite or cardBackSprite assignment.");
        else
            Debug.Log($"[GCCardDisplay] Applied sprite: {sprite.name} to {gameObject.name}");
    }

    void OnClick()
    {
        if (_isPickable && _manager != null)
            _manager.OnFaceDownCardClicked(_handIndex);
    }

    public void SetHighlight(bool on)
    {
        if (myImage != null)
            myImage.color = on ? new Color(1f, 0.85f, 0.2f, 1f) : Color.white;
    }
}