// GCCardDisplay.cs
// ROOT CAUSE FIX: Button component's color transition was overriding image color.
// Fix: set Button.transition = None so it never tints the image.
// This is why cards went dim after EnablePickMode/DisablePickMode cycle.

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
            // ── ROOT CAUSE FIX ────────────────────────────────────────────────
            // Button.transition = None stops Unity from tinting the Image
            // with Normal/Highlighted/Pressed colors. Without this, every
            // time a button is interacted with or its interactable state changes,
            // Unity reapplies the NormalColor tint (which was grey) over the image.
            _button.transition = Selectable.Transition.None;

            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(OnClick);
        }

        ForceWhite();
    }

    void Start()
    {
        ForceWhite();
    }

    private void ForceWhite()
    {
        if (myImage != null)
        {
            myImage.color = Color.white;
            myImage.enabled = true;
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

    public void SetupSelectablePair(Card c, GCGameManager manager, int handIndex)
    {
        cardData = c;
        _manager = manager;
        _isPickable = false;
        _handIndex = handIndex;
        ApplySprite(c.cardSprite);
        if (_button != null)
        {
            _button.interactable = true;
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => manager.OnPairCardSelected(c));
        }
    }

    public void FlipFaceDown()
    {
        if (myImage != null && cardBackSprite != null)
        {
            myImage.sprite = cardBackSprite;
            myImage.color = Color.white;
        }
    }

    public void FlipFaceUp()
    {
        if (myImage != null && cardData != null && cardData.cardSprite != null)
        {
            myImage.sprite = cardData.cardSprite;
            myImage.color = Color.white;
        }
    }

    // ── Slide to throw pile ───────────────────────────────────────────────────

    public IEnumerator SlideToCenter(RectTransform throwPile, float duration = 0.35f,
                                     float scatterX = 150f, float scatterY = 80f)
    {
        RectTransform rt = GetComponent<RectTransform>();
        if (rt == null) yield break;

        FlipFaceUp();

        Vector3 startPos = rt.position;
        Vector3 scatter = new Vector3(
            Random.Range(-scatterX, scatterX),
            Random.Range(-scatterY, scatterY), 0f);
        Vector3 targetPos = throwPile.position + scatter;
        float targetRot = Random.Range(-45f, 45f);
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
        if (myImage == null)
        {
            Debug.LogError("[GCCardDisplay] myImage is NULL!");
            return;
        }
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