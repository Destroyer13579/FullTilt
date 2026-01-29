using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Fills empty seats at the current table in real-time
/// Works in both lobby-joined tables and standalone table testing
/// Pulls AIs from AIPlayerManager (respects server mode persistence)
/// </summary>
public class TableSeatFiller : MonoBehaviour
{
    [Header("References")]
    public TableManager tableManager;
    public PokerGameManager gameManager;

    [Header("Settings")]
    [Tooltip("How often to check for empty seats (seconds)")]
    public float fillCheckInterval = 10f;

    [Tooltip("Delay after player joins before filling seats")]
    public float initialFillDelay = 5f;

    [Tooltip("Minimum players at table (will always fill to at least this many)")]
    public int minimumPlayers = 4;

    [Tooltip("Target fullness (0.5 = 50% full, 0.8 = 80% full)")]
    [Range(0f, 1f)]
    public float targetFullness = 0.7f;

    [Header("Debug")]
    public bool enableDebugLogs = true;

    private bool isInitialized = false;
    private bool isFillingSeats = false;
    private StakeLevel currentStakes;

    void Start()
    {
        if (tableManager == null)
            tableManager = FindObjectOfType<TableManager>();

        if (gameManager == null)
            gameManager = FindObjectOfType<PokerGameManager>();

        if (tableManager == null)
        {
            UnityEngine.Debug.LogError("[TableSeatFiller] No TableManager found!");
            enabled = false;
            return;
        }

        // Create stake level from table manager
        currentStakes = new StakeLevel(tableManager.smallBlind, tableManager.bigBlind);

        Log("=== TABLE SEAT FILLER STARTED ===");
        Log($"Stakes: ${currentStakes.SmallBlind}/${currentStakes.BigBlind}");
        Log($"Target fullness: {targetFullness * 100}%");
        Log($"Minimum players: {minimumPlayers}");

        // Start with delay to let table initialize
        StartCoroutine(InitialFillAfterDelay());
    }

    IEnumerator InitialFillAfterDelay()
    {
        Log($"Waiting {initialFillDelay}s before initial fill...");
        yield return new WaitForSeconds(initialFillDelay);

        Log("Initial fill starting...");
        FillEmptySeats();
        isInitialized = true;

        // Start continuous checking
        StartCoroutine(ContinuousFillRoutine());
    }

    IEnumerator ContinuousFillRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(fillCheckInterval);

            if (!isFillingSeats)
            {
                FillEmptySeats();
            }
        }
    }

    void FillEmptySeats()
    {
        if (isFillingSeats) return;
        isFillingSeats = true;

        // Count current players and empty seats
        int currentPlayers = 0;
        List<int> emptySeats = new List<int>();

        for (int i = 0; i < tableManager.seats.Count; i++)
        {
            if (tableManager.seats[i].IsSeated)
            {
                currentPlayers++;
            }
            else if (tableManager.seats[i].IsEmpty) // Not reserved, not seated
            {
                emptySeats.Add(i);
            }
        }

        int totalSeats = tableManager.seats.Count;
        int targetPlayers = Mathf.Max(minimumPlayers, Mathf.RoundToInt(totalSeats * targetFullness));
        int seatsToFill = Mathf.Max(0, targetPlayers - currentPlayers);

        Log($"[Fill Check] Current: {currentPlayers}/{totalSeats}, Target: {targetPlayers}, Empty: {emptySeats.Count}");

        if (seatsToFill <= 0 || emptySeats.Count == 0)
        {
            Log("Table adequately filled, skipping");
            isFillingSeats = false;
            return;
        }

        // Limit to available empty seats
        seatsToFill = Mathf.Min(seatsToFill, emptySeats.Count);

        Log($"Filling {seatsToFill} seats...");

        // Get available AI players from AIPlayerManager
        if (AIPlayerManager.Instance == null)
        {
            LogWarning("AIPlayerManager not found! Cannot fill seats.");
            isFillingSeats = false;
            return;
        }

        var availablePlayers = AIPlayerManager.Instance.GetAvailablePlayers(currentStakes, seatsToFill);

        if (availablePlayers.Count == 0)
        {
            LogWarning($"No available AI players can afford stakes ${currentStakes.SmallBlind}/${currentStakes.BigBlind} (MaxBuyIn: ${currentStakes.MaxBuyIn:#,0})");

            // Debug: Check why
            int totalAIs = AIPlayerManager.Instance.AllPlayers.Count;
            int unseated = AIPlayerManager.Instance.AllPlayers.Count(p => string.IsNullOrEmpty(p.CurrentTableId));
            int canAfford = AIPlayerManager.Instance.AllPlayers.Count(p => string.IsNullOrEmpty(p.CurrentTableId) && p.CanAffordTable(currentStakes));

            Log($"  Total AIs: {totalAIs}, Unseated: {unseated}, Can afford: {canAfford}");

            isFillingSeats = false;
            return;
        }

        // Actually fill the seats
        int filled = 0;
        for (int i = 0; i < Mathf.Min(availablePlayers.Count, seatsToFill); i++)
        {
            if (i >= emptySeats.Count) break;

            AIPlayer aiPlayer = availablePlayers[i];
            int seatIndex = emptySeats[i];
            int buyIn = aiPlayer.GetBuyInAmount(currentStakes);

            // Use StartCoroutine for delayed seating (looks more natural)
            StartCoroutine(SeatPlayerWithDelay(aiPlayer, seatIndex, buyIn, i * 2f));
            filled++;
        }

        Log($"✓ Filling {filled} seats with AI players");
        isFillingSeats = false;
    }

    IEnumerator SeatPlayerWithDelay(AIPlayer aiPlayer, int seatIndex, int buyIn, float delay)
    {
        yield return new WaitForSeconds(delay);

        PlayerSeat seat = tableManager.GetSeat(seatIndex);
        if (seat == null || !seat.IsEmpty)
        {
            Log($"Seat {seatIndex} no longer available, skipping {aiPlayer.PlayerName}");
            yield break;
        }

        Log($"Seating {aiPlayer.PlayerName} at seat {seatIndex} with ${buyIn:#,0}");

        // Get or create table ID
        string tableId = GetCurrentTableId();

        // Update AI player state
        aiPlayer.SitAtTable(tableId, buyIn);

        // Seat at the table
        seat.SeatPlayer(
            aiPlayer.PlayerId,
            aiPlayer.PlayerName,
            buyIn,
            aiPlayer.AvatarId,
            isLocal: false  // AI player, not local
        );

        // Notify lifecycle manager if in server mode
        if (AILifecycleManager.Instance != null && AILifecycleManager.Instance.isServerMode)
        {
            AILifecycleManager.Instance.JoinTable(aiPlayer, tableId, buyIn);
        }

        // Update table registry state if it exists
        if (TableRegistry.Instance != null)
        {
            TableState state = TableRegistry.Instance.GetTableState(tableId);
            if (state != null)
            {
                // Table state will be updated by the game manager
                Log($"  Table {tableId} found in registry");
            }
        }

        // Save AI state
        AIPlayerManager.Instance.SavePlayers();

        Log($"✓ {aiPlayer.PlayerName} seated successfully");
    }

    /// <summary>
    /// Get the current table's ID from PlayerPrefs or generate one
    /// </summary>
    string GetCurrentTableId()
    {
        // Try to get from PlayerPrefs (set when joining from lobby)
        if (PlayerPrefs.HasKey("SelectedTableId"))
        {
            return PlayerPrefs.GetString("SelectedTableId");
        }

        // Generate a unique ID for this table session
        string tableId = $"table-{currentStakes.SmallBlind}-{currentStakes.BigBlind}-{System.Guid.NewGuid().ToString().Substring(0, 8)}";
        PlayerPrefs.SetString("SelectedTableId", tableId);
        return tableId;
    }

    /// <summary>
    /// Force immediate fill (call from inspector or code)
    /// </summary>
    [ContextMenu("Force Fill Empty Seats")]
    public void ForceFillEmptySeats()
    {
        Log("Force fill triggered");
        FillEmptySeats();
    }

    /// <summary>
    /// Public API: Notify that a player left
    /// </summary>
    public void OnPlayerLeft(int seatIndex)
    {
        Log($"Player left seat {seatIndex}, will refill on next check");
        // Will be picked up on next fill check
    }

    void Log(string message)
    {
        if (enableDebugLogs)
        {
            UnityEngine.Debug.Log($"[TableSeatFiller] {message}");
        }
    }

    void LogWarning(string message)
    {
        if (enableDebugLogs)
        {
            UnityEngine.Debug.LogWarning($"[TableSeatFiller] {message}");
        }
    }
}
