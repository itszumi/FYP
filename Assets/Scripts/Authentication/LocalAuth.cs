using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public static class LocalAuth
{
    private const string UserKey = "AUTH_USERNAME";
    private const string PassHashKey = "AUTH_PASSHASH";

    public static bool HasUser()
    {
        return PlayerPrefs.HasKey(UserKey) && PlayerPrefs.HasKey(PassHashKey);
    }

    public static string GetSavedUsername()
    {
        return PlayerPrefs.GetString(UserKey, "");
    }

    public static bool Register(string username, string password)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)) return false;

        string hash = Hash(password);
        PlayerPrefs.SetString(UserKey, username);
        PlayerPrefs.SetString(PassHashKey, hash);
        PlayerPrefs.Save();
        return true;
    }

    public static bool Login(string password)
    {
        if (!HasUser()) return false;

        string savedHash = PlayerPrefs.GetString(PassHashKey, "");
        return savedHash == Hash(password);
    }

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