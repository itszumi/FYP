// CardController.cs
// Hi-Lo game logic.
// Checks the player's guess against the new card and awards/deducts coins.
// Win  → +200 coins   (CurrencyManager.OnWin)
// Lose → -200 coins   (CurrencyManager.OnLose)

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CardController : MonoBehaviour
{
    public static int  dealtCardNumber;
    public static int  newCardNumber;
    public static bool guessHi = false;
    public static bool guessLo = false;

    [Header("Result Text Objects")]
    public GameObject luckyText;
    public GameObject unluckyText;

    [Header("Buttons")]
    public GameObject hiButton;
    public GameObject loButton;
    public GameObject dealButton;

    [Header("Coin UI to refresh after result (optional)")]
    public CurrencyUI currencyUI;

    // ── Unity lifecycle ───────────────────────────────────────────────────────
    void Update()
    {
        if (guessHi)
        {
            guessHi = false;
            hiButton.SetActive(false);
            loButton.SetActive(false);
            StartCoroutine(GuessingHigher());
        }
        if (guessLo)
        {
            guessLo = false;
            hiButton.SetActive(false);
            loButton.SetActive(false);
            StartCoroutine(GuessingLower());
        }
    }

    // ── Coroutines ────────────────────────────────────────────────────────────
    IEnumerator GuessingHigher()
    {
        yield return new WaitForSeconds(1f);

        bool win = (newCardNumber >= dealtCardNumber);
        ShowResult(win);
    }

    IEnumerator GuessingLower()
    {
        yield return new WaitForSeconds(1f);

        bool win = (newCardNumber <= dealtCardNumber);
        ShowResult(win);
    }

    // ── Shared result handler ─────────────────────────────────────────────────
    private void ShowResult(bool win)
    {
        if (win)
        {
            luckyText.SetActive(true);
            CurrencyManager.OnWin();
        }
        else
        {
            unluckyText.SetActive(true);
            CurrencyManager.OnLose();
        }

        // Refresh the on-screen coin display if assigned
        if (currencyUI != null)
            currencyUI.Refresh();
    }
}
