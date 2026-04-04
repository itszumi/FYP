// LocalAuth.cs
// Handles local registration/login with SHA-256 password hashing.
// Supports multiple accounts – each user's data is stored under their own keys.
// Avatar index (0–5) is also stored per-user.

using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public static class LocalAuth
{
    // ── Key helpers ────────────────────────────────────────────────────────────
    private static string PassKey(string user)   => "AUTH_PASS_"   + user.ToLower();
    private static string AvatarKey(string user) => "AUTH_AVATAR_" + user.ToLower();

    // ── Account helpers ────────────────────────────────────────────────────────

    /// <summary>Returns true if this username already has a stored account.</summary>
    public static bool UserExists(string username)
    {
        if (string.IsNullOrEmpty(username)) return false;
        return PlayerPrefs.HasKey(PassKey(username));
    }

    /// <summary>Returns the last successfully logged-in username (used to auto-fill the field).</summary>
    public static string GetLastUser()
    {
        return PlayerPrefs.GetString("AUTH_LASTUSER", "");
    }

    // ── Registration ───────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a new account. Returns false if the username is already taken
    /// or if either field is empty.
    /// </summary>
    public static bool Register(string username, string password)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)) return false;
        if (UserExists(username)) return false; // don't overwrite existing account

        PlayerPrefs.SetString(PassKey(username), Hash(password));
        PlayerPrefs.SetInt(AvatarKey(username), 0); // default avatar
        PlayerPrefs.Save();
        return true;
    }

    // ── Login ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates credentials. On success writes the last-user key and returns true.
    /// </summary>
    public static bool Login(string username, string password)
    {
        if (!UserExists(username)) return false;

        string storedHash = PlayerPrefs.GetString(PassKey(username), "");
        bool ok = storedHash == Hash(password);

        if (ok)
        {
            PlayerPrefs.SetString("AUTH_LASTUSER", username);
            PlayerPrefs.Save();
        }
        return ok;
    }

    // ── Avatar ─────────────────────────────────────────────────────────────────

    public static void SaveAvatar(string username, int avatarIndex)
    {
        PlayerPrefs.SetInt(AvatarKey(username), avatarIndex);
        PlayerPrefs.Save();
    }

    public static int GetAvatar(string username)
    {
        return PlayerPrefs.GetInt(AvatarKey(username), 0);
    }

    // ── Hashing ────────────────────────────────────────────────────────────────

    private static string Hash(string input)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] data = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            StringBuilder sb = new StringBuilder();
            foreach (byte b in data) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}