using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class TableManager : MonoBehaviour
{
    [Header("Table Info")]
    public string tableId;
    public string tableName;
    public int smallBlind = 1;
    public int bigBlind = 2;
    public int minBuyIn = 40;
    public int maxBuyIn = 200;

    [Header("Seats")]
    public List<PlayerSeat> seats = new List<PlayerSeat>();
    public AvatarDatabase avatarDatabase;

    [Header("Buy-In UI")]
    public GameObject buyInPanel;
    public TMP_Text buyInTitleText;
    public Slider buyInSlider;
    public TMP_Text buyInAmountText;
    public TMP_Text minBuyInText;
    public TMP_Text maxBuyInText;
    public Button confirmBuyInButton;
    public Button cancelBuyInButton;

    [Header("Table UI")]
    public TMP_Text tableNameText;
    public TMP_Text blindsText;
    public TMP_Text potText;
    public Button leaveTableButton;

    [Header("Community Cards")]
    public List<Image> communityCards;
    public Sprite cardBackSprite;

    [Header("Seat Rotation")]
    public SeatPositionManager seatPositionManager;

    private PlayerSeat pendingSeat;
    private int localPlayerSeatIndex = -1;

    void Start()
    {
        InitializeTable();
        SetupUI();

        // ============================================================
        // LOAD PLAYERS FROM LOBBY
        // ============================================================
        if (PlayerPrefs.HasKey("SelectedTableId"))
        {
            string lobbyTableId = PlayerPrefs.GetString("SelectedTableId");
            string lobbyTableName = PlayerPrefs.GetString("SelectedTableName", "Unknown");

            UnityEngine.Debug.Log("========================================");
            UnityEngine.Debug.Log($"[TableManager] JOINING FROM LOBBY");
            UnityEngine.Debug.Log($"[TableManager] Table: {lobbyTableName}");

            tableId = lobbyTableId;
            tableName = lobbyTableName;

            PokerTableInfo tableInfo = TableRegistry.Instance.GetTableInfo(tableId);
            if (tableInfo != null)
            {
                tableInfo.isActivelyRendered = true;
                tableInfo.lastRenderedRealtime = Time.realtimeSinceStartup;
            }

            if (tableNameText != null)
                tableNameText.text = tableName;

            if (PlayerPrefs.HasKey("TableSmallBlind"))
            {
                smallBlind = PlayerPrefs.GetInt("TableSmallBlind");
                bigBlind = PlayerPrefs.GetInt("TableBigBlind");
                minBuyIn = bigBlind * 40;
                maxBuyIn = bigBlind * 100;

                if (blindsText != null)
                    blindsText.text = $"${smallBlind}/${bigBlind}";

                UnityEngine.Debug.Log($"[TableManager] Stakes: ${smallBlind}/${bigBlind}");
            }

            if (PlayerPrefs.HasKey("TablePlayerIds"))
            {
                string playerIdsStr = PlayerPrefs.GetString("TablePlayerIds");

                if (!string.IsNullOrEmpty(playerIdsStr))
                {
                    string[] playerIds = playerIdsStr.Split(',');
                    UnityEngine.Debug.Log($"[TableManager] Loading {playerIds.Length} AI players...");

                    int playersSeated = 0;

                    foreach (string playerId in playerIds)
                    {
                        if (string.IsNullOrEmpty(playerId)) continue;

                        AIPlayer aiPlayer = AIPlayerManager.Instance.GetPlayer(playerId);
                        if (aiPlayer == null)
                        {
                            UnityEngine.Debug.LogWarning($"[TableManager] Could not find AI player: {playerId}");
                            continue;
                        }

                        int emptySeat = -1;
                        for (int i = 1; i < seats.Count; i++)
                        {
                            if (seats[i].IsEmpty)
                            {
                                emptySeat = i;
                                break;
                            }
                        }

                        if (emptySeat == -1)
                        {
                            UnityEngine.Debug.LogWarning($"[TableManager] No empty seats for {aiPlayer.PlayerName}");
                            break;
                        }

                        // Truncate name if too long (max 9 characters to fit on desk)
                        string displayName = aiPlayer.PlayerName.Length > 9
                            ? aiPlayer.PlayerName.Substring(0, 9)
                            : aiPlayer.PlayerName;

                        UnityEngine.Debug.Log($"[TableManager]   Seating {displayName} at seat {emptySeat} with ${aiPlayer.ChipsAtTable}, Avatar: {aiPlayer.AvatarId}");

                        // USE THE PLAYER'S ACTUAL AVATAR ID!
                        SeatAI(emptySeat, displayName, aiPlayer.ChipsAtTable, aiPlayer.AvatarId);

                        playersSeated++;

                        if (seats[emptySeat].IsEmpty)
                        {
                            UnityEngine.Debug.LogError($"[TableManager] ERROR: Seat {emptySeat} still empty after SeatAI()!");
                        }
                    }

                    UnityEngine.Debug.Log($"[TableManager] Successfully seated {playersSeated} AI players");
                }
            }

            UnityEngine.Debug.Log("========================================");

            PlayerPrefs.DeleteKey("SelectedTableId");
            PlayerPrefs.DeleteKey("SelectedTableName");
            PlayerPrefs.DeleteKey("JoinMode");
            PlayerPrefs.DeleteKey("TableSmallBlind");
            PlayerPrefs.DeleteKey("TableBigBlind");
            PlayerPrefs.DeleteKey("TablePlayerIds");
            PlayerPrefs.DeleteKey("TableMaxPlayers");
        }
        else
        {
            UnityEngine.Debug.Log("[TableManager] TEST MODE - Opened directly in editor");
        }
    }

    void InitializeTable()
    {
        for (int i = 0; i < seats.Count; i++)
        {
            seats[i].seatIndex = i;
            seats[i].Initialize(avatarDatabase);
            seats[i].OnSeatClicked += OnSeatClicked;
        }

        if (tableNameText != null)
            tableNameText.text = tableName;

        if (blindsText != null)
            blindsText.text = $"${smallBlind}/${bigBlind}";

        if (potText != null)
            potText.text = "Pot: $0";

        if (buyInPanel != null)
            buyInPanel.SetActive(false);
    }

    void SetupUI()
    {
        if (buyInSlider != null)
        {
            buyInSlider.minValue = minBuyIn;
            buyInSlider.maxValue = maxBuyIn;
            buyInSlider.wholeNumbers = true;
            buyInSlider.onValueChanged.AddListener(OnBuyInSliderChanged);
        }

        if (minBuyInText != null)
            minBuyInText.text = $"${minBuyIn}";

        if (maxBuyInText != null)
            maxBuyInText.text = $"${maxBuyIn}";

        if (confirmBuyInButton != null)
            confirmBuyInButton.onClick.AddListener(ConfirmBuyIn);

        if (cancelBuyInButton != null)
            cancelBuyInButton.onClick.AddListener(CancelBuyIn);

        if (leaveTableButton != null)
            leaveTableButton.onClick.AddListener(LeaveTable);
    }

    void OnSeatClicked(PlayerSeat seat)
    {
        Debug.Log($"TableManager received seat click for seat {seat.seatIndex}");

        if (!seat.IsEmpty) return;

        if (localPlayerSeatIndex >= 0)
        {
            Debug.Log("Already seated at this table");
            return;
        }

        if (AccountManager.Instance == null || !AccountManager.Instance.IsLoggedIn)
        {
            Debug.Log("Must be logged in to sit");
            return;
        }

        var player = AccountManager.Instance.CurrentPlayer;

        if (player.Bankroll < minBuyIn)
        {
            Debug.Log("Not enough chips for minimum buy-in");
            return;
        }

        pendingSeat = seat;
        seat.ReserveSeat(player.PlayerId, player.DisplayName, player.AvatarId);

        ShowBuyInPanel(player.Bankroll);
    }

    void ShowBuyInPanel(int playerBankroll)
    {
        if (buyInPanel == null) return;

        buyInPanel.SetActive(true);

        int effectiveMax = Mathf.Min(maxBuyIn, playerBankroll);

        if (buyInSlider != null)
        {
            buyInSlider.minValue = minBuyIn;
            buyInSlider.maxValue = effectiveMax;
            buyInSlider.value = effectiveMax;
        }

        if (buyInTitleText != null)
            buyInTitleText.text = $"Buy-In: {tableName}";

        OnBuyInSliderChanged(buyInSlider != null ? buyInSlider.value : minBuyIn);
    }

    void OnBuyInSliderChanged(float value)
    {
        int amount = Mathf.RoundToInt(value);
        if (buyInAmountText != null)
            buyInAmountText.text = $"${amount}";
    }

    void ConfirmBuyIn()
    {
        if (pendingSeat == null) return;

        int buyInAmount = buyInSlider != null ? Mathf.RoundToInt(buyInSlider.value) : minBuyIn;

        if (AccountManager.Instance != null)
        {
            AccountManager.Instance.UpdateBankroll(-buyInAmount);
        }

        var player = AccountManager.Instance.CurrentPlayer;
        pendingSeat.SeatPlayer(
            player.PlayerId,
            player.DisplayName,
            buyInAmount,
            player.AvatarId,
            true
        );

        PokerGameManager gameManager = FindObjectOfType<PokerGameManager>();
        bool handInProgress = gameManager != null && gameManager.IsHandInProgress;
        if (handInProgress)
        {
            PlayerSeatStatus status = pendingSeat.GetComponent<PlayerSeatStatus>();
            if (status != null)
            {
                status.isWaitingForNextHand = true;
            }

            UnityEngine.Debug.Log($"[TableManager] {player.DisplayName} joined mid-hand - waiting for next hand.");
        }

        localPlayerSeatIndex = pendingSeat.seatIndex;

        if (seatPositionManager != null)
        {
            seatPositionManager.RotateToPlayerSeat(localPlayerSeatIndex);
        }

        buyInPanel.SetActive(false);
        pendingSeat = null;

        Debug.Log($"Seated at position {localPlayerSeatIndex} with ${buyInAmount}");
    }

    void CancelBuyIn()
    {
        if (pendingSeat != null)
        {
            pendingSeat.ClearSeat();
            pendingSeat = null;
        }

        if (buyInPanel != null)
            buyInPanel.SetActive(false);
    }

    public void LeaveTable()
    {
        if (!string.IsNullOrEmpty(tableId))
        {
            PokerTableInfo tableInfo = TableRegistry.Instance.GetTableInfo(tableId);
            if (tableInfo != null)
            {
                tableInfo.isActivelyRendered = false;
                tableInfo.lastRenderedRealtime = Time.realtimeSinceStartup;
            }
        }

        if (localPlayerSeatIndex >= 0 && localPlayerSeatIndex < seats.Count)
        {
            int chipsToReturn = seats[localPlayerSeatIndex].ChipCount;

            if (AccountManager.Instance != null)
            {
                AccountManager.Instance.UpdateBankroll(chipsToReturn);
            }

            seats[localPlayerSeatIndex].ClearSeat();
            localPlayerSeatIndex = -1;
        }

        SceneManager.LoadScene("lobby");
    }

    public void SeatAI(int seatIndex, string name, int chips, int avatarId)
    {
        if (seatIndex < 0 || seatIndex >= seats.Count) return;
        if (!seats[seatIndex].IsEmpty) return;

        seats[seatIndex].SeatPlayer(
            System.Guid.NewGuid().ToString(),
            name,
            chips,
            avatarId,
            false
        );
    }

    public void RemovePlayer(int seatIndex)
    {
        if (seatIndex < 0 || seatIndex >= seats.Count) return;

        PlayerSeat seat = seats[seatIndex];

        PlayerSeatStatus status = seat.GetComponent<PlayerSeatStatus>();
        if (status != null && !status.CanPlayerLeave())
        {
            UnityEngine.Debug.Log($"[TableManager] Cannot remove {seat.PlayerName} mid-hand - marking as AWAY");
            status.MarkAsAway();
            return;
        }

        UnityEngine.Debug.Log($"[TableManager] Removing player from seat {seatIndex}");
        seat.ClearSeat();

        if (seatIndex == localPlayerSeatIndex)
            localPlayerSeatIndex = -1;
    }

    public PlayerSeat GetSeat(int index)
    {
        if (index < 0 || index >= seats.Count) return null;
        return seats[index];
    }

    public PlayerSeat GetLocalPlayerSeat()
    {
        if (localPlayerSeatIndex < 0) return null;
        return seats[localPlayerSeatIndex];
    }

    public int GetEmptySeatCount()
    {
        int count = 0;
        foreach (var seat in seats)
        {
            if (seat.IsEmpty) count++;
        }
        return count;
    }

    public int GetOccupiedSeatCount()
    {
        return seats.Count - GetEmptySeatCount();
    }
}
