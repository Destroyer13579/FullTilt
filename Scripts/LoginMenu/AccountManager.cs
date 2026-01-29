using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

public class AccountManager : MonoBehaviour
{
    public static AccountManager Instance { get; private set; }
    
    public PlayerAccount CurrentPlayer { get; private set; }
    public bool IsLoggedIn => CurrentPlayer != null;

    // Events for UI to subscribe to
    public event Action<PlayerAccount> OnLoginSuccess;
    public event Action<string> OnLoginFailed;
    public event Action<PlayerAccount> OnRegisterSuccess;
    public event Action<string> OnRegisterFailed;
    public event Action OnLogout;

    private Dictionary<string, PlayerAccount> allAccounts = new Dictionary<string, PlayerAccount>();
    private string saveFilePath;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            saveFilePath = Path.Combine(Application.persistentDataPath, "accounts.json");
            LoadAllAccounts();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // === REGISTRATION ===
    public void Register(string username, string password, string email, string displayName = null)
    {
        // Validate inputs
        if (string.IsNullOrEmpty(username) || username.Length < 3)
        {
            OnRegisterFailed?.Invoke("Username must be at least 3 characters");
            return;
        }

        if (string.IsNullOrEmpty(password) || password.Length < 6)
        {
            OnRegisterFailed?.Invoke("Password must be at least 6 characters");
            return;
        }

        // Check if username already exists
        string lowerUsername = username.ToLower();
        if (allAccounts.ContainsKey(lowerUsername))
        {
            OnRegisterFailed?.Invoke("Username already taken");
            return;
        }

        // Create new account
        PlayerAccount newAccount = new PlayerAccount
        {
            Username = username,
            DisplayName = string.IsNullOrEmpty(displayName) ? username : displayName,
            PasswordHash = PlayerAccount.HashPassword(password),
            Email = email
        };

        allAccounts[lowerUsername] = newAccount;
        SaveAllAccounts();

        Debug.Log($"Account created: {username}");
        OnRegisterSuccess?.Invoke(newAccount);

        // Auto-login after registration
        CurrentPlayer = newAccount;
        OnLoginSuccess?.Invoke(CurrentPlayer);
    }

    // === LOGIN ===
    public void Login(string username, string password)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            OnLoginFailed?.Invoke("Please enter username and password");
            return;
        }

        string lowerUsername = username.ToLower();
        
        if (!allAccounts.ContainsKey(lowerUsername))
        {
            OnLoginFailed?.Invoke("Account not found");
            return;
        }

        PlayerAccount account = allAccounts[lowerUsername];
        
        if (!account.VerifyPassword(password))
        {
            OnLoginFailed?.Invoke("Incorrect password");
            return;
        }

        // Success!
        account.LastLogin = DateTime.Now;
        CurrentPlayer = account;
        SaveAllAccounts();

        Debug.Log($"Login successful: {username}");
        OnLoginSuccess?.Invoke(CurrentPlayer);
    }

    // === LOGOUT ===
    public void Logout()
    {
        if (CurrentPlayer != null)
        {
            SaveAllAccounts();
            Debug.Log($"Logged out: {CurrentPlayer.Username}");
            CurrentPlayer = null;
            OnLogout?.Invoke();
        }
    }

    // === ACCOUNT UPDATES ===
    public void UpdateBankroll(int amount)
    {
        if (CurrentPlayer != null)
        {
            CurrentPlayer.Bankroll += amount;
            SaveAllAccounts();
        }
    }

    public void SetAvatar(int avatarId)
    {
        if (CurrentPlayer != null)
        {
            CurrentPlayer.AvatarId = avatarId;
            SaveAllAccounts();
        }
    }

    public void UpdateDisplayName(string newName)
    {
        if (CurrentPlayer != null && !string.IsNullOrEmpty(newName))
        {
            CurrentPlayer.DisplayName = newName;
            SaveAllAccounts();
        }
    }

    public void UpdateSettings(PlayerAccount updatedSettings)
    {
        if (CurrentPlayer != null)
        {
            CurrentPlayer.SoundEnabled = updatedSettings.SoundEnabled;
            CurrentPlayer.MusicEnabled = updatedSettings.MusicEnabled;
            CurrentPlayer.SoundVolume = updatedSettings.SoundVolume;
            CurrentPlayer.MusicVolume = updatedSettings.MusicVolume;
            CurrentPlayer.ShowHandHistory = updatedSettings.ShowHandHistory;
            CurrentPlayer.AutoMuck = updatedSettings.AutoMuck;
            CurrentPlayer.FourColorDeck = updatedSettings.FourColorDeck;
            SaveAllAccounts();
        }
    }

    // === SAVE/LOAD ===
    private void SaveAllAccounts()
    {
        try
        {
            AccountSaveData saveData = new AccountSaveData
            {
                Accounts = new List<PlayerAccount>(allAccounts.Values)
            };
            string json = JsonUtility.ToJson(saveData, true);
            File.WriteAllText(saveFilePath, json);
            Debug.Log($"Accounts saved to {saveFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save accounts: {e.Message}");
        }
    }

    private void LoadAllAccounts()
    {
        try
        {
            if (File.Exists(saveFilePath))
            {
                string json = File.ReadAllText(saveFilePath);
                AccountSaveData saveData = JsonUtility.FromJson<AccountSaveData>(json);
                
                allAccounts.Clear();
                foreach (var account in saveData.Accounts)
                {
                    allAccounts[account.Username.ToLower()] = account;
                }
                Debug.Log($"Loaded {allAccounts.Count} accounts");
            }
            else
            {
                Debug.Log("No save file found, starting fresh");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load accounts: {e.Message}");
        }
    }

    // === UTILITY ===
    public bool UsernameExists(string username)
    {
        return allAccounts.ContainsKey(username.ToLower());
    }

    public PlayerAccount GetAccount(string username)
    {
        string lower = username.ToLower();
        return allAccounts.ContainsKey(lower) ? allAccounts[lower] : null;
    }
}

[Serializable]
public class AccountSaveData
{
    public List<PlayerAccount> Accounts = new List<PlayerAccount>();
}
