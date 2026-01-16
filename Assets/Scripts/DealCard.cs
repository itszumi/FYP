using UnityEngine;

public class DealCard : MonoBehaviour
{
    public GameObject[] dealCard;
    public int cardGenerate;
    public void DealMyNewCard() { 
        
        cardGenerate = Random.Range(2, 15);
        dealCard[cardGenerate].SetActive(true);

        CardController.dealtCardNumber = cardGenerate;

    }



}
