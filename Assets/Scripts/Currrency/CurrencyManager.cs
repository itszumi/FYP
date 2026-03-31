using UnityEngine;

public static class CurrencyManager
{
    private const string CoinsKey = "PLAYER_COINS";
    private const int StartCoins = 1000;

    public static int GetCoins()
    {
        if (!PlayerPrefs.HasKey(CoinsKey))
        {
            PlayerPrefs.SetInt(CoinsKey, StartCoins);
            PlayerPrefs.Save();
        }
        return PlayerPrefs.GetInt(CoinsKey);
    }

    public static void AddCoins(int amount)
    {
        int current = GetCoins();
        current += amount;
        if (current < 0) current = 0;
        PlayerPrefs.SetInt(CoinsKey, current);
        PlayerPrefs.Save();
    }

    public static void SetCoins(int value)
    {
        PlayerPrefs.SetInt(CoinsKey, value);
        PlayerPrefs.Save();
    }
}