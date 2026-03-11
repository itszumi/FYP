using UnityEngine;
using UnityEngine.UI;

public class CardDisplay : MonoBehaviour
{
    public Card cardData;
    public Image myImage;
    public Sprite cardBackSprite;
    private JutpatiGameManager gameManager;
    public bool isInteractable = false;
    public bool isOnFloor = false; // New: is this card in the discard pile?

    public void Setup(Card c, JutpatiGameManager manager, bool isFaceUp, bool canClick)
    {
        cardData = c;
        gameManager = manager;
        isInteractable = canClick;

        if (isFaceUp)
            myImage.sprite = c.cardSprite;
        else
            myImage.sprite = cardBackSprite;

        myImage.color = Color.white; 
    }

    public void OnClick()
    {
        if (isOnFloor && gameManager != null)
        {
            gameManager.OnDiscardPileClicked(this);
        }
        else if (isInteractable && gameManager != null)
        {
            gameManager.OnCardClicked(cardData);
        }
    }
}