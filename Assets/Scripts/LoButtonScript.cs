using UnityEngine;

public class LoButtonScript : MonoBehaviour
{
    public GameObject[] dealCard;
    public int cardGenerate;
    public void DealLoCard()
    {

        cardGenerate = Random.Range(2, 15);
        dealCard[cardGenerate].SetActive(true);

        CardController.newCardNumber = cardGenerate;
        CardController.guessLo = true;
    }
}
