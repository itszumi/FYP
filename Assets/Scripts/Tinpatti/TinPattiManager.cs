using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TinPattiManager : MonoBehaviour
{
    public TinPattiDeck deck;
    public GameObject cardPrefab;
    public Transform[] playerAreas;

    [Header("Status — use TextMeshProUGUI for best styling")]
    public TextMeshProUGUI statusTMP;   // assign TMP version in Inspector
    public Text statusText;             // legacy fallback (assign if no TMP)

    public Button showButton;

    [Header("Win Panel (optional — nicer than status text)")]
    public GameObject winPanel;        // a separate panel that slides in
    public TextMeshProUGUI winTitleText;   // big bold title "YOU WIN!" / "BOT 3 WINS!"
    public TextMeshProUGUI winDetailText;  // optional subtitle
    public Button playAgainBtn;

    [Header("Coin UI to refresh after result (optional)")]
    public CurrencyUI currencyUI;

    private List<List<Card>> allHands = new List<List<Card>>();
    private List<List<CardDisplayTinPatti>> allDisplays = new List<List<CardDisplayTinPatti>>();

    void Start()
    {
        showButton.interactable = false;
        SetStatus("READY", Color.white);
        if (winPanel) winPanel.SetActive(false);
        playAgainBtn?.onClick.AddListener(StartGame);
    }

    public void StartGame()
    {
        StopAllCoroutines();
        SetStatus("DEALING...", Color.white);
        if (winPanel) winPanel.SetActive(false);

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
        string title = humanWon ? "YOU WIN!" : $"BOT {winnerIndex} WINS!";
        string detail = humanWon ? "Great hand! Coins added." : "Better luck next time!";

        // Show win panel if available (preferred)
        if (winPanel != null)
        {
            winPanel.SetActive(true);
            if (winTitleText != null)
            {
                winTitleText.text = title;
                // Gold for win, red-ish for loss
                winTitleText.color = humanWon
                    ? new Color(1f, 0.84f, 0f)     // gold
                    : new Color(1f, 0.35f, 0.35f);  // soft red
            }
            if (winDetailText != null)
                winDetailText.text = detail;
        }
        else
        {
            // Fallback — style the status text nicely
            Color col = humanWon
                ? new Color(1f, 0.84f, 0f)
                : new Color(1f, 0.35f, 0.35f);
            SetStatus(title, col);
        }

        if (humanWon) CurrencyManager.OnWin();
        else CurrencyManager.OnLose();

        if (currencyUI != null) currencyUI.Refresh();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void SetStatus(string msg, Color col)
    {
        if (statusTMP != null) { statusTMP.text = msg; statusTMP.color = col; }
        if (statusText != null) { statusText.text = msg; statusText.color = col; }
    }
}