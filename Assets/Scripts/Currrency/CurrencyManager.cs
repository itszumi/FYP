// CurrencyManager.cs
// Per-user coin wallet stored in PlayerPrefs.
// Starting balance: 1000 coins.
// Win  → +200 coins
// Lose → -200 coins (never drops below 0)

using UnityEngine;

public static class CurrencyManager
{
    private const int StartCoins     = 1000;
    private const int RoundStakeCoins = 200;

    // ── Key helper ─────────────────────────────────────────────────────────────
    private static string CoinsKey()
    {
        string user = PlayerSession.GetUser();
        if (string.IsNullOrEmpty(user)) user = "default";
        return "COINS_" + user.ToLower();
    }

    // ── Initialise ─────────────────────────────────────────────────────────────
    /// <summary>
    /// Call once when a user first registers (or after login if needed).
    /// Only sets the balance if the key doesn't already exist.
    /// </summary>
    public static void InitForUser(string username)
    {
        string key = "COINS_" + username.ToLower();
        if (!PlayerPrefs.HasKey(key))
        {
            PlayerPrefs.SetInt(key, StartCoins);
            PlayerPrefs.Save();
        }
    }

    // ── Read ───────────────────────────────────────────────────────────────────
    public static int GetCoins()
    {
        string key = CoinsKey();
        if (!PlayerPrefs.HasKey(key))
        {
            PlayerPrefs.SetInt(key, StartCoins);
            PlayerPrefs.Save();
        }
        return PlayerPrefs.GetInt(key, StartCoins);
    }

    // ── Write ──────────────────────────────────────────────────────────────────
    public static void AddCoins(int amount)
    {
        int current = GetCoins() + amount;
        if (current < 0) current = 0;
        PlayerPrefs.SetInt(CoinsKey(), current);
        PlayerPrefs.Save();
    }

    public static void SetCoins(int value)
    {
        PlayerPrefs.SetInt(CoinsKey(), Mathf.Max(0, value));
        PlayerPrefs.Save();
    }

    // ── Convenience wrappers ───────────────────────────────────────────────────
    /// <summary>Call when the player wins a round (+200 coins).</summary>
    public static void OnWin()  => AddCoins( RoundStakeCoins);

    /// <summary>Call when the player loses a round (-200 coins).</summary>
    public static void OnLose() => AddCoins(-RoundStakeCoins);
}