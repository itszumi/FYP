using UnityEngine;
using UnityEngine.UI;

public class CardDisplayTinPatti : MonoBehaviour
{
    public Card cardData;
    public Image myImage;
    public Sprite cardBackSprite;

    private void Awake()
    {
        if (myImage == null) myImage = GetComponent<Image>();
    }

    public void Setup(Card c, TinPattiManager manager, bool isFaceUp)
    {
        cardData = c;
        if (myImage == null) myImage = GetComponent<Image>();

        if (isFaceUp)
        {
            if (c.cardSprite == null)
            {
                // THIS IS THE CONSOLE DEBUG
                Debug.LogError($"[ERROR] Front Image missing for: {c.rank} of {c.suit}!");
                myImage.color = Color.magenta; // Turns bright pink so you can see the error
            }
            else
            {
                myImage.sprite = c.cardSprite;
                myImage.color = Color.white;
            }
        }
        else
        {
            if (cardBackSprite == null)
            {
                Debug.LogError("[ERROR] Card Back Sprite is NULL in the Inspector!");
                myImage.color = Color.red; // Turns red if you forgot the back image
            }
            else
            {
                myImage.sprite = cardBackSprite;
                myImage.color = Color.white;
            }
        }
    }

    public void FlipToFront()
    {
        if (myImage == null) myImage = GetComponent<Image>();
        if (cardData.cardSprite != null)
        {
            myImage.sprite = cardData.cardSprite;
            myImage.color = Color.white;
        }
        else
        {
            Debug.LogError($"[ERROR] Cannot flip! No sprite for: {cardData.rank} of {cardData.suit}");
        }
    }
}