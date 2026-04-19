using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class TinPattiManager : MonoBehaviour
{
    public TinPattiDeck deck;
    public GameObject cardPrefab;
    public Transform[] playerAreas;

    [Header("Status Text")]
    public TextMeshProUGUI statusTMP;
    public Text statusText;
    public Button showButton;

    [Header("Game Over Panel")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI gameOverTitle;
    public TextMeshProUGUI gameOverDetail;
    public Button restartBtn;
    public Button homeBtn;

    [Header("Coin UI (optional)")]
    public CurrencyUI currencyUI;

    private List<List<Card>> allHands = new List<List<Card>>();
    private List<List<CardDisplayTinPatti>> allDisplays = new List<List<CardDisplayTinPatti>>();

    void Start()
    {
        showButton.interactable = false;
        SetStatus("READY", Color.white);
        if (gameOverPanel) gameOverPanel.SetActive(false);
    }

    public void GoToMenu()
    {
        SceneManager.LoadScene("Menu");
    }

    public void StartGame()
    {
        StopAllCoroutines();
        SetStatus("DEALING...", Color.white);
        if (gameOverPanel) gameOverPanel.SetActive(false);

        foreach (var area in playerAreas)
            foreach (Transform child in area) Destroy(child.gameObject);

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
        for (int round = 0; round < 3; round++)
        {
            for (int p = 0; p < 5; p++)
            {
                Card c = deck.DrawCard();
                allHands[p].Add(c);
                GameObject obj = Instantiate(cardPrefab, playerAreas[p]);
                CardDisplayTinPatti disp = obj.GetComponent<CardDisplayTinPatti>();
                disp.Setup(c, this, (p == 0));
                allDisplays[p].Add(disp);
                yield return new WaitForSeconds(0.15f);
            }
        }
        SetStatus("TAP SHOW", Color.white);
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
            if (score > bestScore) { bestScore = score; winnerIndex = i; }
        }

        bool humanWon = winnerIndex == 0;

        if (humanWon) CurrencyManager.AddCoins(200);
        else CurrencyManager.AddCoins(-200);

        if (currencyUI != null) currencyUI.Refresh();

        SetStatus("", Color.white);

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);

            if (gameOverTitle != null)
            {
                gameOverTitle.text = humanWon ? "YOU WIN!" : $"BOT {winnerIndex} WINS!";
                gameOverTitle.color = humanWon
                    ? new Color(1f, 0.84f, 0f)
                    : new Color(1f, 0.35f, 0.35f);
            }

            if (gameOverDetail != null)
            {
                gameOverDetail.text = humanWon
                    ? "+200 Coins! Great hand!"
                    : "-200 Coins. Better luck next time!";
                gameOverDetail.color = humanWon
                    ? new Color(0.6f, 1f, 0.6f)
                    : new Color(1f, 0.7f, 0.7f);
            }
        }
    }

    void SetStatus(string msg, Color col)
    {
        if (statusTMP != null) { statusTMP.text = msg; statusTMP.color = col; }
        if (statusText != null) { statusText.text = msg; statusText.color = col; }
    }
}