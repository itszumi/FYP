// GCCardDisplay.cs
// Supports face-up, face-down, AI card display + slide-to-center discard animation.

using System.Collections;
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

    // ── Setup methods ─────────────────────────────────────────────────────────

    public void SetupFaceUp(Card c, GCGameManager manager)
    {
        cardData = c;
        _manager = manager;
        _isPickable = false;
        _handIndex = -1;
        ApplySprite(c.cardSprite);
        if (_button != null) _button.interactable = false;
    }

    public void SetupAICard(Card c)
    {
        cardData = c;
        _isPickable = false;
        ApplySprite(cardBackSprite);
        if (_button != null) _button.interactable = false;
    }

    public void SetupFaceDown(Card c, GCGameManager manager, int handIndex)
    {
        cardData = c;
        _manager = manager;
        _isPickable = true;
        _handIndex = handIndex;
        ApplySprite(cardBackSprite);
        if (_button != null) _button.interactable = true;
    }

    // Make card selectable from human hand (for pair discard selection)
    public void SetupSelectablePair(Card c, GCGameManager manager, int handIndex)
    {
        cardData = c;
        _manager = manager;
        _isPickable = false; // uses different click path
        _handIndex = handIndex;
        ApplySprite(c.cardSprite);
        if (_button != null)
        {
            _button.interactable = true;
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => manager.OnPairCardSelected(c));
        }
    }

    // Flip to face down (called after deal phase)
    public void FlipFaceDown()
    {
        if (myImage != null && cardBackSprite != null)
            myImage.sprite = cardBackSprite;
    }

    // ── Slide animation to center throw pile ──────────────────────────────────

    public IEnumerator SlideToCenter(RectTransform throwPile, float duration = 0.4f)
    {
        RectTransform rt = GetComponent<RectTransform>();
        if (rt == null) yield break;

        // Convert throw pile center to local canvas position
        Vector3 startPos = rt.position;
        Vector3 targetPos = throwPile.position;
        float targetRot = Random.Range(-40f, 40f);
        float startRot = rt.eulerAngles.z;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            rt.position = Vector3.Lerp(startPos, targetPos, t);
            rt.rotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(startRot, targetRot, t));
            yield return null;
        }

        rt.position = targetPos;
        rt.rotation = Quaternion.Euler(0f, 0f, targetRot);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void ApplySprite(Sprite sprite)
    {
        if (myImage == null) { Debug.LogError("[GCCardDisplay] myImage is NULL!"); return; }
        myImage.sprite = sprite;
        myImage.color = Color.white;
        myImage.enabled = true;
        gameObject.SetActive(true);
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