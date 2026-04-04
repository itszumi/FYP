// LocalAuth.cs
// Handles registration/login with SHA-256 password hashing.
// Writes to BOTH PlayerPrefs (fast runtime access) AND UserDatabase (persistent JSON).
//
// UserDatabase file location:
//   Windows: %AppData%/../LocalLow/<Company>/<Product>/users.json
//   Android: /data/data/<package>/files/users.json
//   iOS:     <app>/Documents/users.json

using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public static class LocalAuth
{
    // ── PlayerPrefs key helpers ───────────────────────────────────────────────
    private static string PassKey(string user) => "AUTH_PASS_" + user.ToLower();
    private static string AvatarKey(string user) => "AUTH_AVATAR_" + user.ToLower();

    // ── User existence ────────────────────────────────────────────────────────

    public static bool UserExists(string username)
    {
        if (string.IsNullOrEmpty(username)) return false;
        return PlayerPrefs.HasKey(PassKey(username));
    }

    public static string GetLastUser()
    {
        return PlayerPrefs.GetString("AUTH_LASTUSER", "");
    }

    // ── Registration ──────────────────────────────────────────────────────────

    public static bool Register(string username, string password)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)) return false;
        if (UserExists(username)) return false;

        string hash = Hash(password);

        // PlayerPrefs (fast access)
        PlayerPrefs.SetString(PassKey(username), hash);
        PlayerPrefs.SetInt(AvatarKey(username), 0);
        PlayerPrefs.Save();

        // JSON database (persistent storage of all users)
        UserDatabase.AddUser(username, hash, avatarIndex: 0);

        return true;
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    public static bool Login(string username, string password)
    {
        if (!UserExists(username)) return false;

        string storedHash = PlayerPrefs.GetString(PassKey(username), "");
        bool ok = storedHash == Hash(password);

        if (ok)
        {
            PlayerPrefs.SetString("AUTH_LASTUSER", username);
            PlayerPrefs.Save();

            // Record login timestamp in database
            UserDatabase.RecordLogin(username);
        }
        return ok;
    }

    // ── Avatar ────────────────────────────────────────────────────────────────

    public static void SaveAvatar(string username, int avatarIndex)
    {
        // PlayerPrefs
        PlayerPrefs.SetInt(AvatarKey(username), avatarIndex);
        PlayerPrefs.Save();

        // JSON database
        UserDatabase.UpdateAvatar(username, avatarIndex);
    }

    public static int GetAvatar(string username)
    {
        return PlayerPrefs.GetInt(AvatarKey(username), 0);
    }

    // ── Hashing ───────────────────────────────────────────────────────────────

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