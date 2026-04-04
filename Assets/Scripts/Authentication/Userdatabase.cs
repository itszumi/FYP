// UserDatabase.cs
// Stores all registered users as a JSON file on disk.
// Location: Application.persistentDataPath/users.json
// (On Windows: %AppData%/../LocalLow/<Company>/<Product>/users.json)
//
// Works alongside LocalAuth — call UserDatabase.Save/Load from LocalAuth methods.
// This is a static utility class; no MonoBehaviour needed.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class UserRecord
{
    public string username;
    public string passwordHash;   // SHA-256 hex from LocalAuth
    public int avatarIndex;
    public string registeredAt;   // ISO 8601 timestamp
    public string lastLoginAt;
    public int currency;
}

[Serializable]
public class UserDatabase
{
    // ── Singleton-style access ────────────────────────────────────────────────
    private static UserDatabase _instance;
    public static UserDatabase DB
    {
        get
        {
            if (_instance == null) _instance = Load();
            return _instance;
        }
    }

    // ── Data ──────────────────────────────────────────────────────────────────
    public List<UserRecord> users = new List<UserRecord>();

    // ── File path ─────────────────────────────────────────────────────────────
    private static string FilePath => Path.Combine(Application.persistentDataPath, "users.json");

    // ─────────────────────────────────────────────────────────────────────────
    #region CRUD

    /// <summary>Add a brand-new user record.</summary>
    public static void AddUser(string username, string passwordHash, int avatarIndex = 0)
    {
        if (FindUser(username) != null)
        {
            Debug.LogWarning($"UserDatabase: user '{username}' already exists.");
            return;
        }

        DB.users.Add(new UserRecord
        {
            username = username,
            passwordHash = passwordHash,
            avatarIndex = avatarIndex,
            registeredAt = DateTime.UtcNow.ToString("o"),
            lastLoginAt = DateTime.UtcNow.ToString("o"),
            currency = 1000
        });
        Save();
    }

    /// <summary>Find a user record by username (case-insensitive). Returns null if not found.</summary>
    public static UserRecord FindUser(string username)
    {
        string lower = username.ToLower();
        return DB.users.Find(u => u.username.ToLower() == lower);
    }

    /// <summary>Update the avatar index for a user and save.</summary>
    public static void UpdateAvatar(string username, int avatarIndex)
    {
        UserRecord rec = FindUser(username);
        if (rec == null) return;
        rec.avatarIndex = avatarIndex;
        Save();
    }

    /// <summary>Update the last login timestamp and save.</summary>
    public static void RecordLogin(string username)
    {
        UserRecord rec = FindUser(username);
        if (rec == null) return;
        rec.lastLoginAt = DateTime.UtcNow.ToString("o");
        Save();
    }

    /// <summary>Update the currency for a user and save.</summary>
    public static void UpdateCurrency(string username, int amount)
    {
        UserRecord rec = FindUser(username);
        if (rec == null) return;
        rec.currency = amount;
        Save();
    }

    /// <summary>Returns all users (read-only copy).</summary>
    public static List<UserRecord> GetAllUsers() => new List<UserRecord>(DB.users);

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Save / Load

    public static void Save()
    {
        try
        {
            string json = JsonUtility.ToJson(DB, prettyPrint: true);
            File.WriteAllText(FilePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"UserDatabase: Failed to save — {e.Message}");
        }
    }

    private static UserDatabase Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                return JsonUtility.FromJson<UserDatabase>(json) ?? new UserDatabase();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"UserDatabase: Failed to load — {e.Message}");
        }
        return new UserDatabase();
    }

    #endregion
}