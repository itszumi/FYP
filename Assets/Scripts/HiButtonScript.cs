using UnityEngine;

public class HiButtonScript : MonoBehaviour
{
    public GameObject[] dealCard;
    public int cardGenerate;
    public void DealHiCard()
    {

        cardGenerate = Random.Range(2, 15);
        dealCard[cardGenerate].SetActive(true);

        CardController.newCardNumber = cardGenerate;
        CardController.guessHi = true;
    }
}
