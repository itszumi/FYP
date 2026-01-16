using System.Collections;
using UnityEngine;

public class CardController : MonoBehaviour
{
    public static int dealtCardNumber;
    public static int newCardNumber;
    public GameObject luckyText;
    public GameObject unluckyText;
    public static bool guessHi = false;
    public static bool guessLo = false;
    public GameObject hiButton;
    public GameObject loButton;
    public GameObject dealButton;

    //equal num is win


    void Update()
    {

        if (guessHi == true)
        {
            guessHi = false;
            hiButton.SetActive(false);
            loButton.SetActive(false);
            StartCoroutine(GuessingHigher());
        }
        if (guessLo == true)
        {
            guessLo = false;
            hiButton.SetActive(false);
            loButton.SetActive(false);
            StartCoroutine(GuessingLower());
        }

    }

    IEnumerator GuessingHigher()
    {

        yield return new WaitForSeconds(1);
        if (newCardNumber >= dealtCardNumber)
        {
            luckyText.SetActive(true);

        }
        else
        {
            unluckyText.SetActive(true);

        }
    }
    IEnumerator GuessingLower()
    {

        yield return new WaitForSeconds(1);
        if (newCardNumber <= dealtCardNumber)
        {
            luckyText.SetActive(true);

        }
        else
        {
            unluckyText.SetActive(true);

        }
    }
}
