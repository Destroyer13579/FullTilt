using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

[Serializable]
public class PlayerAccount
{
    public string PlayerId;
    public string Username;
    public string DisplayName;
    public string PasswordHash;        // Hashed password
    public string Email;
    public int Bankroll;
    public int AvatarId;
    public DateTime CreatedAt;
    public DateTime LastLogin;
    
    // Stats
    public int TotalHandsPlayed;
    public int TotalHandsWon;
    public int TotalTournamentsPlayed;
    public int TotalTournamentsWon;
    public long TotalWinnings;
    public long TotalLosses;

    // Settings
    public bool SoundEnabled;
    public bool MusicEnabled;
    public float SoundVolume;
    public float MusicVolume;
    public bool ShowHandHistory;
    public bool AutoMuck;
    public bool FourColorDeck;

    public PlayerAccount()
    {
        PlayerId = Guid.NewGuid().ToString();
        Bankroll = 1000;  // Starting bankroll
        AvatarId = 0;
        CreatedAt = DateTime.Now;
        LastLogin = DateTime.Now;
        
        // Default settings
        SoundEnabled = true;
        MusicEnabled = true;
        SoundVolume = 1f;
        MusicVolume = 0.5f;
        ShowHandHistory = true;
        AutoMuck = true;
        FourColorDeck = false;
    }

    public static string HashPassword(string password)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            StringBuilder builder = new StringBuilder();
            foreach (byte b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }
            return builder.ToString();
        }
    }

    public bool VerifyPassword(string password)
    {
        return PasswordHash == HashPassword(password);
    }
}
