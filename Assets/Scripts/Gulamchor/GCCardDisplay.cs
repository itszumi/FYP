using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class GCCardDisplay : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Assign the Image component on this prefab")]
    public Image myImage;
    public Sprite cardBackSprite;

    [HideInInspector] public Card cardData;

    private GCGameManager _manager;
    private bool _isPickable = false;
    private bool _isDraggable = false;
    private bool _isGameplayPair = false;
    private int _handIndex = -1;
    private Button _button;

    private GCCardDisplay _dragPartner = null;
    private Vector3 _partnerOffset = Vector3.zero;

    private RectTransform _rectTransform;
    private Canvas _canvas;
    private Vector3 _originalPosition;
    private Vector3 _partnerOriginalPosition;
    private Transform _originalParent;
    private Transform _partnerOriginalParent;
    private int _originalSiblingIndex;

    void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();

        _button = GetComponent<Button>();
        if (_button != null)
        {
            ColorBlock cb = _button.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = Color.white;
            cb.pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = Color.white;
            cb.colorMultiplier = 1f;
            _button.colors = cb;

            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(OnClick);
        }

        ForceWhite();
    }

    void Start() { ForceWhite(); }

    private void ForceWhite()
    {
        if (myImage != null)
        {
            myImage.color = Color.white;
            myImage.enabled = true;
        }
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    public void SetupFaceUp(Card c, GCGameManager manager)
    {
        cardData = c;
        _manager = manager;
        _isPickable = false;
        _isDraggable = false;
        _isGameplayPair = false;
        _handIndex = -1;
        _dragPartner = null;
        ApplySprite(c.cardSprite);
        if (_button != null) _button.interactable = false;
    }

    public void SetupAICard(Card c)
    {
        cardData = c;
        _isPickable = false;
        _isDraggable = false;
        _isGameplayPair = false;
        _dragPartner = null;
        ApplySprite(cardBackSprite);
        if (_button != null) _button.interactable = false;
    }

    public void SetupFaceDown(Card c, GCGameManager manager, int handIndex)
    {
        cardData = c;
        _manager = manager;
        _isPickable = true;
        _isDraggable = false;
        _isGameplayPair = false;
        _handIndex = handIndex;
        _dragPartner = null;
        ApplySprite(cardBackSprite);
        if (_button != null) _button.interactable = true;
    }

    public void SetupGameplayPair(Card c, GCGameManager manager, GCCardDisplay partner)
    {
        cardData = c;
        _manager = manager;
        _isPickable = false;
        _isDraggable = true;
        _isGameplayPair = true;
        _dragPartner = partner;
        ApplySprite(c.cardSprite);
        if (_button != null)
        {
            _button.interactable = true;
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => manager.OnPairCardSelected(c));
        }
    }

    public void SetupDraggablePair(Card c, GCGameManager manager)
    {
        cardData = c;
        _manager = manager;
        _isPickable = false;
        _isDraggable = true;
        _isGameplayPair = false;
        _dragPartner = null;
        ApplySprite(c.cardSprite);
        if (_button != null)
        {
            _button.interactable = true;
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => manager.OnPairCardSelected(c));
        }
    }

    public void SetupSelectablePair(Card c, GCGameManager manager, int handIndex)
    {
        cardData = c;
        _manager = manager;
        _isPickable = false;
        _isDraggable = false;
        _isGameplayPair = false;
        _handIndex = handIndex;
        _dragPartner = null;
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

    public void SetDragPartner(GCCardDisplay partner) { _dragPartner = partner; }

    // ── Drag handlers ─────────────────────────────────────────────────────────

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!_isDraggable || _manager == null) return;

        _originalPosition = _rectTransform.position;
        _originalParent = transform.parent;
        _originalSiblingIndex = transform.GetSiblingIndex();

        // Find canvas
        if (_canvas == null) _canvas = GetComponentInParent<Canvas>();

        transform.SetParent(_canvas.transform, worldPositionStays: true);
        transform.SetAsLastSibling();
        if (myImage != null) myImage.raycastTarget = false;

        if (_isGameplayPair && _dragPartner != null)
        {
            _partnerOriginalPosition = _dragPartner._rectTransform.position;
            _partnerOriginalParent = _dragPartner.transform.parent;
            _partnerOffset = _dragPartner._rectTransform.position - _rectTransform.position;
            _dragPartner.transform.SetParent(_canvas.transform, worldPositionStays: true);
            _dragPartner.transform.SetAsLastSibling();
            if (_dragPartner.myImage != null) _dragPartner.myImage.raycastTarget = false;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDraggable) return;
        _rectTransform.position += new Vector3(eventData.delta.x, eventData.delta.y, 0f);
        if (_isGameplayPair && _dragPartner != null)
            _dragPartner._rectTransform.position = _rectTransform.position + _partnerOffset;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_isDraggable || _manager == null) return;
        if (myImage != null) myImage.raycastTarget = true;
        if (_dragPartner?.myImage != null) _dragPartner.myImage.raycastTarget = true;

        bool droppedOnPile = _manager.IsOverThrowPile(_rectTransform.position);
        if (droppedOnPile)
        {
            _manager.OnPairCardSelected(cardData);
        }
        else
        {
            transform.SetParent(_originalParent, worldPositionStays: true);
            transform.SetSiblingIndex(_originalSiblingIndex);
            _rectTransform.position = _originalPosition;
            if (_isGameplayPair && _dragPartner != null)
            {
                _dragPartner.transform.SetParent(_partnerOriginalParent, worldPositionStays: true);
                _dragPartner._rectTransform.position = _partnerOriginalPosition;
            }
        }
    }

    // ── Slide to throw pile ───────────────────────────────────────────────────
    // throwPile is the RectTransform of the pile container.
    // Since cards are now children of throwPile, we animate to local position
    // (scatter offset from center) rather than world position.

    public IEnumerator SlideToCenter(RectTransform throwPile, float duration = 0.35f,
                                     float scatterX = 150f, float scatterY = 80f)
    {
        RectTransform rt = GetComponent<RectTransform>();
        if (rt == null) yield break;

        FlipFaceUp();

        // Start position in world space
        Vector3 startWorldPos = rt.position;

        // Target: throwPile center + random scatter in local space
        // Convert scatter local offset to world space
        Vector3 scatterLocal = new Vector3(
            Random.Range(-scatterX, scatterX),
            Random.Range(-scatterY, scatterY),
            0f);

        // If card is child of throwPile, target is local scatterLocal
        // If not, target is throwPile.position + scatter
        Vector3 targetWorldPos;
        if (rt.parent == throwPile)
            // Card is child of throwPile — animate from current world pos to pile center + scatter
            targetWorldPos = throwPile.position + scatterLocal;
        else
            targetWorldPos = throwPile.position + scatterLocal;

        float targetRot = Random.Range(-30f, 30f);
        float startRot = rt.eulerAngles.z;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            rt.position = Vector3.Lerp(startWorldPos, targetWorldPos, t);
            rt.rotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(startRot, targetRot, t));
            yield return null;
        }
        rt.position = targetWorldPos;
        rt.rotation = Quaternion.Euler(0f, 0f, targetRot);
    }

    // ── Pop animation ─────────────────────────────────────────────────────────

    public IEnumerator PopAnimation(float duration = 0.3f)
    {
        RectTransform rt = GetComponent<RectTransform>();
        if (rt == null) yield break;

        Vector3 originalScale = rt.localScale;
        Vector3 popScale = originalScale * 1.3f;
        float half = duration / 2f;
        float elapsed = 0f;

        while (elapsed < half) { elapsed += Time.deltaTime; rt.localScale = Vector3.Lerp(originalScale, popScale, elapsed / half); yield return null; }
        elapsed = 0f;
        while (elapsed < half) { elapsed += Time.deltaTime; rt.localScale = Vector3.Lerp(popScale, originalScale, elapsed / half); yield return null; }
        rt.localScale = originalScale;
    }

    // ── Highlight ─────────────────────────────────────────────────────────────

    public void SetHighlight(bool on)
    {
        if (myImage != null)
            myImage.color = on ? new Color(1f, 0.85f, 0.2f, 1f) : Color.white;
    }

    public void SetPairHighlight(bool on)
    {
        if (myImage != null)
            myImage.color = on ? new Color(1f, 0.92f, 0.2f, 1f) : Color.white;
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
}