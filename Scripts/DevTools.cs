using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DevTools : MonoBehaviour
{
    [Header("Settings")]
    public int addAmount = 1000;

    [Header("UI (Optional)")]
    public Button addFundsButton;
    public TMP_Text bankrollDisplay;

    void Start()
    {
        if (addFundsButton != null)
            addFundsButton.onClick.AddListener(AddFunds);
    }

    void Update()
    {
        // Press F1 to add funds anytime
        if (Input.GetKeyDown(KeyCode.F1))
        {
            AddFunds();
        }

        // Press F2 to add 10,000
        if (Input.GetKeyDown(KeyCode.F2))
        {
            AddFundsAmount(10000);
        }

        // Press F3 to set bankroll to 1,000,000
        if (Input.GetKeyDown(KeyCode.F3))
        {
            SetBankroll(1000000);
        }
    }

    public void AddFunds()
    {
        AddFundsAmount(addAmount);
    }

    public void AddFundsAmount(int amount)
    {
        if (AccountManager.Instance != null && AccountManager.Instance.IsLoggedIn)
        {
            AccountManager.Instance.UpdateBankroll(amount);
            Debug.Log($"Added ${amount}. New bankroll: ${AccountManager.Instance.CurrentPlayer.Bankroll}");
            UpdateDisplay();
        }
        else
        {
            Debug.Log("Not logged in!");
        }
    }

    public void SetBankroll(int amount)
    {
        if (AccountManager.Instance != null && AccountManager.Instance.IsLoggedIn)
        {
            var player = AccountManager.Instance.CurrentPlayer;
            int difference = amount - player.Bankroll;
            AccountManager.Instance.UpdateBankroll(difference);
            Debug.Log($"Bankroll set to ${amount}");
            UpdateDisplay();
        }
    }

    void UpdateDisplay()
    {
        if (bankrollDisplay != null && AccountManager.Instance != null && AccountManager.Instance.IsLoggedIn)
        {
            bankrollDisplay.text = $"${AccountManager.Instance.CurrentPlayer.Bankroll:N0}";
        }

        // Also update LobbyManager if it exists
        var lobbyManager = FindObjectOfType<LobbyManager>();
        if (lobbyManager != null)
        {
            lobbyManager.UpdatePlayerInfo();
        }
    }
}
