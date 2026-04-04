
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TinPattiManager : MonoBehaviour
{
    public TinPattiDeck deck;
    public GameObject cardPrefab;
    public Transform[] playerAreas;
    public Text statusText;
    public Button showButton;

    [Header("Coin UI to refresh after result (optional)")]
    public CurrencyUI currencyUI;

    private List<List<Card>> allHands = new List<List<Card>>();
    private List<List<CardDisplayTinPatti>> allDisplays = new List<List<CardDisplayTinPatti>>();

    void Start()
    {
        showButton.interactable = false;
        statusText.text = "READY";
    }

    public void StartGame()
    {
        StopAllCoroutines();
        statusText.text = "DEALING...";
        statusText.color = Color.white;

        // Clear Table
        foreach (var area in playerAreas) { foreach (Transform child in area) Destroy(child.gameObject); }
        allHands.Clear();
        allDisplays.Clear();

        deck.CreateDeck();

        for (int i = 0; i < 5; i++)
        {
            allHands.Add(new List<Card>());
            allDisplays.Add(new List<CardDisplayTinPatti>());
        }

        StartCoroutine(DealCards());
    }

    IEnumerator DealCards()
    {
        for (int round = 0; round < 3; round++) // Exactly 3 rounds
        {
            for (int p = 0; p < 5; p++)
            {
                Card c = deck.DrawCard();
                allHands[p].Add(c);

                GameObject obj = Instantiate(cardPrefab, playerAreas[p]);
                CardDisplayTinPatti disp = obj.GetComponent<CardDisplayTinPatti>();
                disp.Setup(c, this, (p == 0)); // Only player (0) is face up
                allDisplays[p].Add(disp);

                yield return new WaitForSeconds(0.15f);
            }
        }
        statusText.text = "TAP SHOW";
        showButton.interactable = true;
    }

    public void OnShowClicked()
    {
        showButton.interactable = false;
        int bestScore = -1;
        int winnerIndex = -1;

        for (int i = 0; i < 5; i++)
        {
            foreach (var disp in allDisplays[i]) disp.FlipToFront();

            int score = TeenPattiEvaluator.GetHandScore(allHands[i]);
            if (score > bestScore)
            {
                bestScore = score;
                winnerIndex = i;
            }
        }

        statusText.text = (winnerIndex == 0) ? "YOU WIN!" : "BOT " + winnerIndex + " WINS!";
        statusText.color = Color.yellow;

        if (winnerIndex == 0)
            CurrencyManager.OnWin();
        else
            CurrencyManager.OnLose();

        // Refresh on-screen coin display
        if (currencyUI != null)
            currencyUI.Refresh();
    }
}