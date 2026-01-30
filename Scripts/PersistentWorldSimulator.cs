using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// PERSISTENT WORLD SIMULATOR
/// Runs continuously across ALL scenes (DontDestroyOnLoad)
/// Simulates ALL tables in real-time regardless of where player is
/// This is the HEART of the persistent poker world
/// </summary>
public class PersistentWorldSimulator : MonoBehaviour
{
    // Singleton
    private static PersistentWorldSimulator instance;
    public static PersistentWorldSimulator Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("PersistentWorldSimulator");
                instance = go.AddComponent<PersistentWorldSimulator>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    [Header("Simulation Settings")]
    [Tooltip("Base hand duration in seconds (adjusted by player count)")]
    public float baseHandDuration = 30f;

    [Tooltip("How often to check for players joining/leaving tables (seconds)")]
    public float dynamicSeatFillingInterval = 45f;

    [Tooltip("How often to clean up broke players (seconds)")]
    public float cleanupInterval = 10f;

    [Tooltip("How often to save AI state (seconds)")]
    public float saveInterval = 60f;

    [Header("Status")]
    public bool isRunning = false;
    public float timeSinceLastFill = 0f;
    public float timeSinceLastCleanup = 0f;
    public float timeSinceLastSave = 0f;

    private PersistentWorldHandSimulation handSimulation;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        UnityEngine.Debug.Log("[PersistentWorld] ★★★ PERSISTENT WORLD SIMULATOR INITIALIZED ★★★");
    }

    /// <summary>
    /// Start the persistent simulation
    /// Called once when lobby first loads
    /// </summary>
    public void StartSimulation()
    {
        if (isRunning)
        {
            UnityEngine.Debug.Log("[PersistentWorld] Already running");
            return;
        }

        UnityEngine.Debug.Log("[PersistentWorld] ★ Starting continuous simulation across all scenes");
        
        if (handSimulation == null)
        {
            handSimulation = new PersistentWorldHandSimulation(baseHandDuration);
        }

        isRunning = true;
        StartCoroutine(PersistentSimulationLoop());
        
        UnityEngine.Debug.Log("[PersistentWorld] ✓ Simulation running - will continue even when player is at table!");
    }

    /// <summary>
    /// Main simulation loop - runs FOREVER
    /// </summary>
    IEnumerator PersistentSimulationLoop()
    {
        const float tickSeconds = 1f;
        while (isRunning)
        {
            // Update timers
            timeSinceLastFill += tickSeconds;
            timeSinceLastCleanup += tickSeconds;
            timeSinceLastSave += tickSeconds;

            // Dynamic seat filling
            if (timeSinceLastFill >= dynamicSeatFillingInterval)
            {
                DynamicSeatFilling();
                timeSinceLastFill = 0f;
            }

            if (handSimulation != null)
            {
                handSimulation.BaseHandDuration = baseHandDuration;
                handSimulation.Tick(tickSeconds);
            }

            // Cleanup broke players
            if (timeSinceLastCleanup >= cleanupInterval)
            {
                CleanupBrokePlayers();
                timeSinceLastCleanup = 0f;
            }

            // Save AI state
            if (timeSinceLastSave >= saveInterval)
            {
                SaveAIState();
                timeSinceLastSave = 0f;
            }

            yield return new WaitForSeconds(tickSeconds); // Check every second
        }

        // Rotate dealer to next occupied seat
        int nextDealer = dealerSeat;
        int safety = 0;
        do
        {
            nextDealer = (nextDealer + 1) % tableInfo.maxSeats;
            safety++;
        }
        while (safety <= tableInfo.maxSeats &&
               (nextDealer >= newState.seats.Count || !newState.seats[nextDealer].isOccupied));

        tableDealerPositions[tableInfo.tableId] = nextDealer;
        tableHandNumbers[tableInfo.tableId] = table.CurrentHandNumber + 1;
    }

    TableData BuildTableDataFromRegistry(PokerTableInfo tableInfo)
    {
        TableData table = new TableData(tableInfo.tableId, tableInfo.stake, tableInfo.maxSeats)
        {
            TableId = tableInfo.tableId
        };

        table.SeatedPlayerIds = new List<string>(new string[tableInfo.maxSeats]);
        table.CurrentPlayers = 0;

        if (tableInfo.currentState != null)
        {
            for (int i = 0; i < tableInfo.currentState.seats.Count && i < tableInfo.maxSeats; i++)
            {
                var seat = tableInfo.currentState.seats[i];
                if (!seat.isOccupied || string.IsNullOrEmpty(seat.playerName))
                {
                    continue;
                }

                var player = AIPlayerManager.Instance.AllPlayers.FirstOrDefault(p => p.PlayerName == seat.playerName);
                if (player != null)
                {
                    table.SeatedPlayerIds[i] = player.PlayerId;
                    table.CurrentPlayers++;
                }
            }
        }

        return table;
    }


    /// <summary>
    /// Fill empty seats at tables (runs continuously)
    /// </summary>
    void DynamicSeatFilling()
    {
        UnityEngine.Debug.Log("[PersistentWorld] 🔄 Dynamic seat filling (runs even while player at table)");

        int seatsFilled = 0;
        var allTables = TableRegistry.Instance.GetAllTables();

        // Sort by stakes (highest first)
        var sortedTables = allTables.OrderByDescending(t => t.stake.BigBlind).ToList();

        foreach (var tableInfo in sortedTables)
        {
            // Skip if table is full
            if (tableInfo.OccupiedSeats >= tableInfo.maxSeats)
                continue;

            // Get target player count for this stake
            int targetPlayers = GetTargetPlayerCount(tableInfo.maxSeats, tableInfo.stake);
            targetPlayers = Mathf.Min(targetPlayers, tableInfo.maxSeats);

            int emptySeats = targetPlayers - tableInfo.OccupiedSeats;

            if (emptySeats <= 0)
                continue;

            // Get available players
            var availablePlayers = AIPlayerManager.Instance.GetAvailablePlayers(tableInfo.stake, emptySeats);

            // Seat them
            int seatsToFill = Mathf.Min(availablePlayers.Count, tableInfo.maxSeats - tableInfo.OccupiedSeats);

            for (int i = 0; i < seatsToFill; i++)
            {
                var player = availablePlayers[i];
                int buyIn = player.GetBuyInAmount(tableInfo.stake);

                // Seat player in AI manager
                player.SitAtTable(tableInfo.tableId, buyIn);

                // Sync to TableRegistry
                SyncPlayerToTableRegistry(tableInfo.tableId, player);

                seatsFilled++;

                string marker = player.Bankroll >= 500000 ? "🐋" : "";
                UnityEngine.Debug.Log($"[PersistentWorld] {marker}{player.PlayerName} joined {tableInfo.tableId} [{tableInfo.OccupiedSeats + 1}/{tableInfo.maxSeats}]");
            }
        }

        if (seatsFilled > 0)
        {
            UnityEngine.Debug.Log($"[PersistentWorld] ✓ Filled {seatsFilled} seats across all tables");
            NotifyLobbyRefresh();
        }
    }

    /// <summary>
    /// Clean up broke players
    /// </summary>
    void CleanupBrokePlayers()
    {
        var brokePlayers = AIPlayerManager.Instance.GetPlayersWhoShouldLeave();

        if (brokePlayers.Count == 0)
            return;

        UnityEngine.Debug.Log($"[PersistentWorld] Cleaning up {brokePlayers.Count} broke players");

        foreach (var player in brokePlayers)
        {
            // Remove from TableRegistry
            if (!string.IsNullOrEmpty(player.CurrentTableId))
            {
                RemovePlayerFromTableRegistry(player.CurrentTableId, player);
            }

            player.LeaveTable(); // Resets bankroll
        }

        NotifyLobbyRefresh();
    }

    /// <summary>
    /// Save AI state
    /// </summary>
    void SaveAIState()
    {
        AIPlayerManager.Instance.SavePlayers();
        UnityEngine.Debug.Log("[PersistentWorld] ✓ Saved AI state");
    }

    /// <summary>
    /// Sync player to TableRegistry
    /// </summary>
    void SyncPlayerToTableRegistry(string tableId, AIPlayer player)
    {
        var tableState = TableRegistry.Instance.GetTableState(tableId);

        if (tableState == null)
        {
            UnityEngine.Debug.LogWarning($"[PersistentWorld] TableState for {tableId} not found!");
            return;
        }

        // Find empty seat
        int seatIndex = -1;
        for (int i = 0; i < tableState.seats.Count; i++)
        {
            if (!tableState.seats[i].isOccupied)
            {
                seatIndex = i;
                break;
            }
        }

        if (seatIndex == -1)
        {
            UnityEngine.Debug.LogWarning($"[PersistentWorld] No empty seat for {player.PlayerName} at {tableId}");
            return;
        }

        // Update seat
        var seat = tableState.seats[seatIndex];
        seat.isOccupied = true;
        seat.playerName = player.PlayerName;
        seat.chipCount = player.ChipsAtTable;
        seat.isSittingOut = false;
        seat.hasFolded = false;
        seat.isAllIn = false;
        seat.currentBet = 0;
        seat.holeCards.Clear();

        // Update registry
        TableRegistry.Instance.UpdateTableState(tableId, tableState);
    }

    /// <summary>
    /// Remove player from TableRegistry
    /// </summary>
    void RemovePlayerFromTableRegistry(string tableId, AIPlayer player)
    {
        var tableState = TableRegistry.Instance.GetTableState(tableId);

        if (tableState == null)
            return;

        // Find player's seat
        for (int i = 0; i < tableState.seats.Count; i++)
        {
            if (tableState.seats[i].playerName == player.PlayerName)
            {
                tableState.seats[i].isOccupied = false;
                tableState.seats[i].playerName = "";
                tableState.seats[i].chipCount = 0;
                tableState.seats[i].isSittingOut = false;
                break;
            }
        }

        TableRegistry.Instance.UpdateTableState(tableId, tableState);
    }

    /// <summary>
    /// Get target player count for a table
    /// </summary>
    int GetTargetPlayerCount(int maxPlayers, StakeLevel stake)
    {
        // High stakes: fewer players (2-6)
        if (stake.BigBlind >= 50000)
            return UnityEngine.Random.Range(2, 7);

        // Mid stakes: moderate (4-8)
        if (stake.BigBlind >= 1000)
            return UnityEngine.Random.Range(4, 9);

        // Low/micro stakes: busy (6-9)
        return UnityEngine.Random.Range(6, maxPlayers + 1);
    }

    /// <summary>
    /// Notify lobby to refresh (if it's active)
    /// </summary>
    void NotifyLobbyRefresh()
    {
        var lobbyManager = FindObjectOfType<LobbyManager>();
        if (lobbyManager != null)
        {
            lobbyManager.RefreshTableList();
        }
    }

    /// <summary>
    /// Stop simulation (call on game quit)
    /// </summary>
    public void StopSimulation()
    {
        isRunning = false;
        UnityEngine.Debug.Log("[PersistentWorld] Simulation stopped");
    }

    void OnApplicationQuit()
    {
        StopSimulation();
    }
}
