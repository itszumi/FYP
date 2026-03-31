
using UnityEngine;
using UnityEngine.UI;

public class CurrencyUI : MonoBehaviour
{
    public Text coinText;

    void Start()
    {
        Refresh();
    }

    public void Refresh()
    {
        coinText.text = $"Coins: {CurrencyManager.GetCoins()}";
    }
}