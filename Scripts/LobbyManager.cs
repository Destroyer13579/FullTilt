using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviour
{
    [Header("UI References")]
    public Transform tableListContent;
    public GameObject tableRowPrefab;
    public TMP_Text totalTablesText;
    public TMP_Text totalPlayersText;
    public TMP_Text tableInfoText;
    public TMP_Text playerListText;

    [Header("Player Info")]
    public TMP_Text playerNameText;
    public TMP_Text playerBankrollText;

    [Header("Action Buttons")]
    public Button joinTableButton;
    public Button spectateTableButton;
    public TMP_Text joinButtonText;  // To show "SIT IN" or "TABLE FULL"

    [Header("Simulation Settings")]
    public int totalAIPlayers = 500;
    public float simulationTickRate = 1f;
    public float handDuration = 30f;

    [Header("Table Generation")]
    public int tablesPerStakeLevel = 4;

    [Header("Scene Names")]
    [Tooltip("Name of your poker table scene (the one with the poker game)")]
    public string tableSceneName = "GameScene";  // Change this to match YOUR scene name!

    [Header("Avatar Database")]
    public AvatarDatabase avatarDatabase;  // Drag your avatar database here in Inspector!

    // Runtime data
    private static List<TableData> allTables = new List<TableData>();
    private Dictionary<string, TableRowUI> tableRows = new Dictionary<string, TableRowUI>();
    private TableData selectedTable = null;
    private static bool lobbyInitialized = false;

    // ★ Persistence flag - prevent re-initialization

    // ★ Poker simulator for actual card-based simulation
    private LobbyPokerSimulator pokerSimulator;
    private Dictionary<string, int> tableDealerPositions = new Dictionary<string, int>();  // Track dealer per table

    // Sorting
    public enum SortColumn { Name, Stakes, Type, Players, AvgPot }
    public SortColumn currentSort = SortColumn.Stakes;
    public bool sortAscending = false;

    // Table names
    private static string[] TableNames = {
        "Athens", "Berlin", "Cairo", "Dublin", "Edinburgh", "Frankfurt",
        "Geneva", "Helsinki", "Istanbul", "Jakarta", "Kyoto", "London",
        "Madrid", "Naples", "Oslo", "Paris", "Quebec", "Rome", "Sydney",
        "Tokyo", "Utrecht", "Vienna", "Warsaw", "Xian", "York", "Zurich",
        "Amsterdam", "Brussels", "Copenhagen", "Denver", "Florence",
        "Glasgow", "Hamburg", "Lyon", "Moscow", "Phoenix", "Seattle"
    };

    void Start()
    {
        UpdatePlayerInfo();
        SetupActionButtons();

        if (lobbyInitialized)
        {
            SyncWithExistingState();
            return;
        }

        InitializeLobby();
        lobbyInitialized = true;
    }

    void SetupActionButtons()
    {
        // Join button
        if (joinTableButton != null)
        {
            joinTableButton.onClick.AddListener(JoinSelectedTable);
            joinTableButton.interactable = false;  // Disabled until table selected
        }

        // Spectate button
        if (spectateTableButton != null)
        {
            spectateTableButton.onClick.AddListener(SpectateSelectedTable);
            spectateTableButton.interactable = false;  // Disabled until table selected
        }
    }

    public void UpdatePlayerInfo()
    {
        if (AccountManager.Instance != null && AccountManager.Instance.IsLoggedIn)
        {
            var player = AccountManager.Instance.CurrentPlayer;

            if (playerNameText != null)
                playerNameText.text = player.DisplayName;

            if (playerBankrollText != null)
                playerBankrollText.text = $"${player.Bankroll:N0}";
        }
        else
        {
            if (playerNameText != null)
                playerNameText.text = "Guest";

            if (playerBankrollText != null)
                playerBankrollText.text = "$0";
        }
    }

    /// <summary>
    /// EMERGENCY: Force regenerate AI players (call from inspector or console)
    /// </summary>
    [ContextMenu("Force Regenerate AI Players")]
    public void ForceRegenerateAIPlayers()
    {
        UnityEngine.Debug.LogError("===== FORCE REGENERATE REQUESTED =====");
        AIPlayerManager.Instance.ForceRegenerate();

        // Repopulate tables
        foreach (var table in allTables)
        {
            table.SeatedPlayerIds.Clear();
            table.CurrentPlayers = 0;
        }

        PopulateTablesWithAISmartly();
        RefreshTableList();

        UnityEngine.Debug.LogError("===== FORCE REGENERATE COMPLETE =====");
    }

    void InitializeLobby()
    {
        bool isServerMode = true;  // ← ADD THIS LINE
        UnityEngine.Debug.Log("[Lobby] ★★★ INITIALIZING LOBBY ★★★");

        // Set avatar count to match avatar database
        if (avatarDatabase != null)
        {
            AIPlayerManager.Instance.TotalAvatars = avatarDatabase.AvatarCount;
            UnityEngine.Debug.Log($"[Lobby] Set AIPlayerManager to use {avatarDatabase.AvatarCount} avatars");
        }
        else
        {
            AIPlayerManager.Instance.TotalAvatars = 6;  // Default fallback
            UnityEngine.Debug.LogWarning("[Lobby] No avatar database, defaulting to 6 avatars");
        }

        // ★ Initialize AI Player Manager only once to preserve persistent state
        if (!AIPlayerManager.Instance.IsInitialized)
        {
            AIPlayerManager.Instance.Initialize();
        }

        // ★ Initialize poker simulator
        pokerSimulator = new LobbyPokerSimulator();
        UnityEngine.Debug.Log("[Lobby] ✓ Poker simulator initialized");

        // ★ Only generate tables if they don't exist yet
        if (allTables.Count == 0)
        {
            UnityEngine.Debug.Log("[Lobby] Generating fresh table list");
            GenerateTables();
        }
        else
        {
            UnityEngine.Debug.Log($"[Lobby] Using existing {allTables.Count} tables");
        }

        // ★ Populate only if tables are empty (avoid wiping persistent state)
        bool hasSeatedPlayers = allTables.Any(t => t.CurrentPlayers > 0);
        if (!hasSeatedPlayers)
        {
            PopulateTablesWithAISmartly();
        }
        RefreshTableList();

        // ★★★ START PERSISTENT WORLD SIMULATOR ★★★
        // This runs FOREVER across all scenes
        PersistentWorldSimulator.Instance.StartSimulation();

        // ★ Start the lobby's simulation loop only if persistent world is not running
        if (!PersistentWorldSimulator.Instance.isRunning)
        {
            StartCoroutine(SimulationLoop());
        }

        // ★★★ START CONTINUOUS LOBBY REFRESH ★★★
        // Updates every 2 seconds even when simulation is running
        StartCoroutine(ContinuousLobbyRefresh());

        UnityEngine.Debug.Log("[Lobby] ✓ Persistent world running - tables will update in real-time!");

        // ★★★ START AI LIFECYCLE MANAGER ★★★
        if (isServerMode)  // You'll need to define this bool at the top
        {
            var lifecycleManager = FindObjectOfType<AILifecycleManager>();
            if (lifecycleManager == null)
            {
                GameObject lifecycleObj = new GameObject("AILifecycleManager");
                lifecycleManager = lifecycleObj.AddComponent<AILifecycleManager>();
                DontDestroyOnLoad(lifecycleObj);
            }
            lifecycleManager.InitializeServerMode();
            UnityEngine.Debug.Log("[Lobby] ✓ AI Lifecycle Manager started");
        }
    }

    void SyncWithExistingState()
    {
        UnityEngine.Debug.Log("[Lobby] Syncing with TableRegistry...");

        // Reinitialize poker simulator if needed
        if (pokerSimulator == null)
        {
            pokerSimulator = new LobbyPokerSimulator();
        }

        // ★ SIMPLE SYNC: Just refresh the UI, don't rebuild tables
        // The tables already exist from first initialization
        // AI players are already tracked in AIPlayerManager
        // Just update the display

        RefreshTableList();
        UpdateTableCountsFromRegistry();
        UpdatePlayerCounts();

        UnityEngine.Debug.Log($"[Lobby] ✓ Synced - displaying {allTables.Count} existing tables");

        // Restart coroutines if not running
        if (!IsInvoking(nameof(DynamicSeatFilling)))
        {
            InvokeRepeating(nameof(DynamicSeatFilling), 30f, 45f);  // ★ Realistic timing
            InvokeRepeating(nameof(CleanupBrokePlayers), 5f, 10f);
            InvokeRepeating(nameof(SaveAIState), 60f, 60f);

            // Also restart simulation loop if persistent world is not running
            if (!PersistentWorldSimulator.Instance.isRunning)
            {
                StartCoroutine(SimulationLoop());
            }
        }
    }

    void GenerateTables()
    {
        allTables.Clear();
        int nameIndex = 0;

        foreach (var stakes in StakeLevels.All)
        {
            // ★ ALL TABLES ARE NOW 9-PLAYER
            for (int i = 0; i < tablesPerStakeLevel; i++)
            {
                allTables.Add(new TableData(GetTableName(ref nameIndex), stakes, 9));
            }
        }

        Debug.Log($"Generated {allTables.Count} tables");

        // ★★★ INTEGRATION: Register all tables with TableRegistry ★★★
        foreach (var table in allTables)
        {
            // Register table with central registry and get the registry's table ID
            string registryTableId = TableRegistry.Instance.RegisterTable(table.Stakes, table.MaxPlayers);

            // CRITICAL: Store the registry ID so we can update it later
            // TableRegistry uses its own IDs, not our TableData IDs
            table.TableId = registryTableId;  // Replace our GUID with registry ID

            UnityEngine.Debug.Log($"[Lobby→Registry] Registered {table.TableName} (Registry ID: {registryTableId})");
        }
        UnityEngine.Debug.Log($"[Lobby→Registry] ✓ All {allTables.Count} tables registered with TableRegistry");
    }

    string GetTableName(ref int index)
    {
        string name = TableNames[index % TableNames.Length];
        int suffix = index / TableNames.Length;
        index++;

        if (suffix > 0)
            return $"{name} {suffix + 1}";
        return name;
    }

    /// <summary>
    /// Smart AI population - ensures whales play at high stakes
    /// </summary>
    void PopulateTablesWithAISmartly()
    {
        UnityEngine.Debug.LogError("===== PopulateTablesWithAISmartly() CALLED =====");

        // ★ CRITICAL: Check if AIPlayerManager even has players!
        if (AIPlayerManager.Instance == null)
        {
            UnityEngine.Debug.LogError("⚠️⚠️⚠️ AIPlayerManager.Instance is NULL!");
            return;
        }

        if (AIPlayerManager.Instance.AllPlayers == null)
        {
            UnityEngine.Debug.LogError("⚠️⚠️⚠️ AIPlayerManager.Instance.AllPlayers is NULL!");
            return;
        }

        UnityEngine.Debug.LogError($"AIPlayerManager.Instance.AllPlayers.Count = {AIPlayerManager.Instance.AllPlayers.Count}");

        if (AIPlayerManager.Instance.AllPlayers.Count == 0)
        {
            UnityEngine.Debug.LogError("⚠️⚠️⚠️ AllPlayers is EMPTY! No players exist!");
            return;
        }

        UnityEngine.Debug.Log("[Lobby] ★ Populating tables SMARTLY with whale distribution");

        // ★ CRITICAL: Debug ALL player states
        int totalPlayers = AIPlayerManager.Instance.AllPlayers.Count;
        int seatedPlayers = AIPlayerManager.Instance.AllPlayers.Count(p => !string.IsNullOrEmpty(p.CurrentTableId));
        int availablePlayers = AIPlayerManager.Instance.AllPlayers.Count(p => string.IsNullOrEmpty(p.CurrentTableId));

        UnityEngine.Debug.LogError($"[Lobby] PLAYER STATE CHECK:");
        UnityEngine.Debug.LogError($"  - Total players: {totalPlayers}");
        UnityEngine.Debug.LogError($"  - Seated (CurrentTableId != null): {seatedPlayers}");
        UnityEngine.Debug.LogError($"  - Available (CurrentTableId == null): {availablePlayers}");

        if (seatedPlayers > 0)
        {
            UnityEngine.Debug.LogError($"[Lobby] ⚠️ PROBLEM: {seatedPlayers} players are still marked as seated!");
            var seatedList = AIPlayerManager.Instance.AllPlayers.Where(p => !string.IsNullOrEmpty(p.CurrentTableId)).Take(10).ToList();
            foreach (var p in seatedList)
            {
                UnityEngine.Debug.LogError($"    - {p.PlayerName}: TableId={p.CurrentTableId}, Bankroll=${p.Bankroll:#,0}, Chips={p.ChipsAtTable}");
            }
        }

        if (availablePlayers == 0)
        {
            UnityEngine.Debug.LogError("⚠️⚠️⚠️ NO AVAILABLE PLAYERS! All players have CurrentTableId set!");
            UnityEngine.Debug.LogError("This means Initialize() cleanup FAILED or wasn't called!");
            return;
        }

        // ★ DEBUG: Check AI player state
        int whaleCount = AIPlayerManager.Instance.AllPlayers.Count(p => p.Bankroll >= 1000000);  // ★ $1M+ are true whales
        int availableWhales = AIPlayerManager.Instance.AllPlayers.Count(p => string.IsNullOrEmpty(p.CurrentTableId) && p.Bankroll >= 1000000);
        int highRollers = AIPlayerManager.Instance.AllPlayers.Count(p => p.Bankroll >= 500000);

        UnityEngine.Debug.Log($"[Lobby] DEBUG: Total: {totalPlayers}, Available: {availablePlayers}, Whales ($1M+): {whaleCount}, Available whales: {availableWhales}, High rollers ($500k+): {highRollers}");

        // Sort tables by stakes (highest first)
        var sortedTables = allTables.OrderByDescending(t => t.Stakes.BigBlind).ToList();

        // First pass: Seat whales at high-stakes tables
        int whalesSeated = 0;
        foreach (var table in sortedTables.Where(t => t.Stakes.BigBlind >= 50000))  // High stakes only
        {
            int targetPlayers = GetRandomPlayerCount(table.MaxPlayers, table.Stakes);

            UnityEngine.Debug.Log($"[Lobby] DEBUG: Table {table.TableName} ({table.Stakes.SmallBlind}/{table.Stakes.BigBlind}) - Target: {targetPlayers} players, MaxBuyIn: ${table.Stakes.MaxBuyIn:#,0}");

            // ★ DETAILED WHALE DIAGNOSIS
            var allWhales = AIPlayerManager.Instance.AllPlayers.Where(p => p.Bankroll >= 500000).ToList();
            var unseatedWhales = allWhales.Where(p => string.IsNullOrEmpty(p.CurrentTableId)).ToList();
            var affordableWhales = unseatedWhales.Where(p => p.CanAffordTable(table.Stakes)).ToList();

            UnityEngine.Debug.Log($"[Lobby] DEBUG WHALE CHECK for {table.TableName}:");
            UnityEngine.Debug.Log($"  - Total whales ($500k+): {allWhales.Count}");
            UnityEngine.Debug.Log($"  - Unseated whales: {unseatedWhales.Count}");
            UnityEngine.Debug.Log($"  - Can afford (>= ${table.Stakes.MaxBuyIn:#,0}): {affordableWhales.Count}");

            if (unseatedWhales.Count > 0 && affordableWhales.Count == 0)
            {
                UnityEngine.Debug.LogError($"[Lobby] ⚠️ PROBLEM: {unseatedWhales.Count} whales available but NONE can afford ${table.Stakes.MaxBuyIn:#,0} max buy-in!");
                foreach (var w in unseatedWhales.Take(3))
                {
                    UnityEngine.Debug.LogError($"    - {w.PlayerName}: ${w.Bankroll:#,0} < ${table.Stakes.MaxBuyIn:#,0}");
                }
            }

            // Get whales specifically (bankroll > $500k)
            var whales = affordableWhales
                .OrderByDescending(p => p.Bankroll)
                .Take(targetPlayers)
                .ToList();

            UnityEngine.Debug.Log($"[Lobby] DEBUG: Found {whales.Count} whales for {table.TableName}");

            // ★ CRITICAL: Don't seat more than MaxPlayers!
            int seatsAvailable = table.MaxPlayers - table.CurrentPlayers;
            int seatsToFill = Mathf.Min(whales.Count, seatsAvailable);

            if (seatsToFill <= 0)
            {
                UnityEngine.Debug.Log($"[Lobby] Table {table.TableName} already has {table.CurrentPlayers}/{table.MaxPlayers} - skipping");
                continue;
            }

            UnityEngine.Debug.Log($"[Lobby] Seating {seatsToFill} whales at {table.TableName} (has {table.CurrentPlayers}, max {table.MaxPlayers})");

            for (int i = 0; i < seatsToFill; i++)
            {
                var whale = whales[i];
                int buyIn = whale.GetBuyInAmount(table.Stakes);
                whale.SitAtTable(table.TableId, buyIn);
                table.SeatedPlayerIds.Add(whale.PlayerId);
                table.CurrentPlayers++;
                whalesSeated++;

                // ★ SYNC WITH TABLEREGISTRY
                SyncPlayerToTableRegistry(table.TableId, whale);

                UnityEngine.Debug.Log($"[Lobby] 🐋 WHALE {whale.PlayerName} (${whale.Bankroll:#,0}) → {table.TableName} ({table.Stakes.SmallBlind}/{table.Stakes.BigBlind}) [{table.CurrentPlayers}/{table.MaxPlayers}]");
            }
        }

        UnityEngine.Debug.Log($"[Lobby] ✓ Seated {whalesSeated} whales at high-stakes tables");

        // Second pass: Fill remaining tables with appropriate players
        UnityEngine.Debug.Log($"[Lobby] ★ Second pass: Filling ALL remaining tables");
        int totalSeated = whalesSeated;

        foreach (var table in allTables)
        {
            if (table.CurrentPlayers >= table.MaxPlayers)
            {
                UnityEngine.Debug.Log($"[Lobby] Table {table.TableName} is full ({table.CurrentPlayers}/{table.MaxPlayers}) - skipping");
                continue;  // Skip if already full
            }

            int targetPlayers = GetRandomPlayerCount(table.MaxPlayers, table.Stakes);
            // ★ CRITICAL: Cap target at MaxPlayers!
            targetPlayers = Mathf.Min(targetPlayers, table.MaxPlayers);
            int emptySeats = targetPlayers - table.CurrentPlayers;

            if (emptySeats <= 0)
                continue;

            UnityEngine.Debug.Log($"[Lobby] Filling {table.TableName} ({table.Stakes.SmallBlind}/{table.Stakes.BigBlind}) - Current: {table.CurrentPlayers}, Target: {targetPlayers}, Need: {emptySeats} players");

            var players = AIPlayerManager.Instance.GetAvailablePlayers(table.Stakes, emptySeats);

            UnityEngine.Debug.Log($"[Lobby] GetAvailablePlayers returned {players.Count} players for {table.TableName}");

            if (players.Count == 0)
            {
                // Diagnose why no players available
                int availableTotal = AIPlayerManager.Instance.AllPlayers.Count(p => string.IsNullOrEmpty(p.CurrentTableId));
                int canAfford = AIPlayerManager.Instance.AllPlayers.Count(p => string.IsNullOrEmpty(p.CurrentTableId) && p.CanAffordTable(table.Stakes));
                UnityEngine.Debug.LogWarning($"[Lobby] ⚠️ No players for {table.TableName}! Available: {availableTotal}, Can afford ${table.Stakes.MaxBuyIn:#,0}: {canAfford}");
            }

            // ★ CRITICAL: Don't exceed MaxPlayers!
            int seatsToFill = Mathf.Min(players.Count, table.MaxPlayers - table.CurrentPlayers);

            for (int i = 0; i < seatsToFill; i++)
            {
                var player = players[i];
                int buyIn = player.GetBuyInAmount(table.Stakes);
                player.SitAtTable(table.TableId, buyIn);
                table.SeatedPlayerIds.Add(player.PlayerId);
                table.CurrentPlayers++;
                totalSeated++;

                // ★ SYNC WITH TABLEREGISTRY
                SyncPlayerToTableRegistry(table.TableId, player);

                UnityEngine.Debug.Log($"[Lobby] Seated {player.PlayerName} at {table.TableName} [{table.CurrentPlayers}/{table.MaxPlayers}]");
            }

            table.AveragePot = table.Stakes.BigBlind * UnityEngine.Random.Range(8f, 25f);
            table.HandsPerHour = UnityEngine.Random.Range(50, 80);

            // Initialize with random hand progress
            float handTime = handDuration / Mathf.Max(2, table.CurrentPlayers);
            table.TimeSinceLastHand = UnityEngine.Random.Range(0f, handTime);
        }

        UpdatePlayerCounts();

        // Log distribution summary
        var highStakes = allTables.Where(t => t.Stakes.BigBlind >= 50000).ToList();
        var totalHighStakesPlayers = highStakes.Sum(t => t.CurrentPlayers);
        var totalHighStakesSeats = highStakes.Count * 9;
        var totalPlayersSeated = allTables.Sum(t => t.CurrentPlayers);
        var totalSeats = allTables.Count * 9;

        UnityEngine.Debug.Log($"[Lobby] ✓ Population complete:");
        UnityEngine.Debug.Log($"  - Total seated: {totalSeated} players across all stakes");
        UnityEngine.Debug.Log($"  - High stakes: {totalHighStakesPlayers}/{totalHighStakesSeats} seats filled");
        UnityEngine.Debug.Log($"  - All tables: {totalPlayersSeated}/{totalSeats} seats filled");
        UnityEngine.Debug.Log($"  - Players online: {AIPlayerManager.Instance.TotalPlayersOnline}");
    }

    void PopulateTablesWithAI()
    {
        // Redirect to smart population
        PopulateTablesWithAISmartly();
    }

    /// <summary>
    /// Smart player count based on stake level - Higher stakes = fewer players
    /// </summary>
    int GetRandomPlayerCount(int maxPlayers, StakeLevel stakes)
    {
        // Determine stake tier (0 = lowest, 6 = highest)
        int tier = 0;
        if (stakes.BigBlind >= 1000000) tier = 6;
        else if (stakes.BigBlind >= 200000) tier = 5;
        else if (stakes.BigBlind >= 100000) tier = 4;
        else if (stakes.BigBlind >= 20000) tier = 3;
        else if (stakes.BigBlind >= 10000) tier = 2;
        else if (stakes.BigBlind >= 1000) tier = 1;
        else tier = 0;

        // Distribution by tier - More players at low stakes, fewer at high
        float fillPercent;
        switch (tier)
        {
            case 0: fillPercent = UnityEngine.Random.Range(0.70f, 0.90f); break;  // Micro: 70-90%
            case 1: fillPercent = UnityEngine.Random.Range(0.60f, 0.80f); break;  // Low: 60-80%
            case 2: fillPercent = UnityEngine.Random.Range(0.50f, 0.70f); break;  // Mid-Low: 50-70%
            case 3: fillPercent = UnityEngine.Random.Range(0.40f, 0.60f); break;  // Mid: 40-60%
            case 4: fillPercent = UnityEngine.Random.Range(0.30f, 0.50f); break;  // Mid-High: 30-50%
            case 5: fillPercent = UnityEngine.Random.Range(0.20f, 0.40f); break;  // High: 20-40%
            case 6: fillPercent = UnityEngine.Random.Range(0.10f, 0.30f); break;  // Nosebleeds: 10-30%
            default: fillPercent = 0.5f; break;
        }

        return Mathf.Max(2, Mathf.RoundToInt(maxPlayers * fillPercent));
    }

    public void RefreshTableList()
    {
        foreach (Transform child in tableListContent)
        {
            Destroy(child.gameObject);
        }
        tableRows.Clear();

        var sortedTables = SortTables(allTables);

        foreach (var table in sortedTables)
        {
            GameObject rowObj = Instantiate(tableRowPrefab, tableListContent);
            TableRowUI rowUI = rowObj.GetComponent<TableRowUI>();

            if (rowUI == null)
                rowUI = rowObj.AddComponent<TableRowUI>();

            rowUI.Initialize(table, this);
            tableRows[table.TableId] = rowUI;
        }
    }

    List<TableData> SortTables(List<TableData> tables)
    {
        IEnumerable<TableData> sorted;

        switch (currentSort)
        {
            case SortColumn.Name:
                sorted = tables.OrderBy(t => t.TableName);
                break;
            case SortColumn.Stakes:
                sorted = tables.OrderBy(t => t.Stakes.BigBlind);
                break;
            case SortColumn.Players:
                sorted = tables.OrderBy(t => t.CurrentPlayers);
                break;
            case SortColumn.AvgPot:
                sorted = tables.OrderBy(t => t.AveragePot);
                break;
            default:
                sorted = tables.OrderBy(t => t.Stakes.BigBlind);
                break;
        }

        if (!sortAscending)
            sorted = sorted.Reverse();

        return sorted.ToList();
    }

    public void SortBy(SortColumn column)
    {
        if (currentSort == column)
            sortAscending = !sortAscending;
        else
        {
            currentSort = column;
            sortAscending = true;
        }
        RefreshTableList();
    }

    public void SortByName() { SortBy(SortColumn.Name); }
    public void SortByStakes() { SortBy(SortColumn.Stakes); }
    public void SortByType() { SortBy(SortColumn.Type); }
    public void SortByPlayers() { SortBy(SortColumn.Players); }
    public void SortByAvgPot() { SortBy(SortColumn.AvgPot); }

    // === SIMULATION ===

    IEnumerator SimulationLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(simulationTickRate);
            SimulateTick();
            UpdateUI();
        }
    }

    void SimulateTick()
    {
        foreach (var table in allTables)
        {
            if (table.CurrentPlayers < 2)
                continue;

            table.TimeSinceLastHand += simulationTickRate;

            float handTime = handDuration / table.CurrentPlayers;
            if (table.TimeSinceLastHand >= handTime)
            {
                SimulateHand(table);
                table.TimeSinceLastHand = 0;
            }
        }

        if (UnityEngine.Random.value < 0.1f)
        {
            SimulatePlayerMovement();
        }

        // ★★★ NOTE: Tables now sync via SimulateHand with full poker state (cards, bets, etc.)
        // No need for simple sync anymore!
    }

    void SyncTablesToRegistry()
    {
        foreach (var table in allTables)
        {
            // Determine if hand is currently in progress
            float handTime = handDuration / Mathf.Max(2, table.CurrentPlayers);
            bool isHandInProgress = table.CurrentPlayers >= 2 &&
                                   table.TimeSinceLastHand > 0 &&
                                   table.TimeSinceLastHand < handTime;

            // Create snapshot of current table state
            TableState snapshot = new TableState
            {
                tableId = table.TableId,
                handNumber = table.CurrentHandNumber,
                totalPot = (int)table.AveragePot,
                currentStreet = isHandInProgress ? "InProgress" : "BetweenHands",
                seats = new List<SeatSnapshot>()
            };

            // Add all seats (occupied and empty)
            for (int i = 0; i < table.MaxPlayers; i++)
            {
                bool isOccupied = i < table.SeatedPlayerIds.Count;
                SeatSnapshot seat = new SeatSnapshot
                {
                    seatIndex = i,
                    isOccupied = isOccupied
                };

                if (isOccupied)
                {
                    var playerId = table.SeatedPlayerIds[i];
                    var aiPlayer = AIPlayerManager.Instance.GetPlayer(playerId);
                    if (aiPlayer != null)
                    {
                        seat.playerName = aiPlayer.PlayerName;
                        seat.chipCount = aiPlayer.ChipsAtTable;
                    }
                }

                snapshot.seats.Add(seat);
            }

            // Update TableRegistry with current state
            TableRegistry.Instance.UpdateTableState(table.TableId, snapshot);
        }
    }

    void SimulateHand(TableData table)
    {
        // Get or initialize dealer position for this table
        if (!tableDealerPositions.ContainsKey(table.TableId))
        {
            tableDealerPositions[table.TableId] = 0;
        }

        int dealerSeat = tableDealerPositions[table.TableId];

        // Run actual poker simulation with cards, betting, etc.
        TableState fullState = pokerSimulator.SimulateHand(table, table.CurrentHandNumber, dealerSeat);

        // Update table registry with the complete state
        TableRegistry.Instance.UpdateTableState(table.TableId, fullState);

        // Move dealer button for next hand (rotate among occupied seats)
        int nextDealer = dealerSeat;
        int safety = 0;
        do
        {
            nextDealer = (nextDealer + 1) % table.MaxPlayers;
            safety++;
        }
        while (safety <= table.MaxPlayers &&
               (nextDealer >= fullState.seats.Count || !fullState.seats[nextDealer].isOccupied));

        tableDealerPositions[table.TableId] = nextDealer;

        // Update hand number
        table.CurrentHandNumber++;

        // Update player chips based on simulation
        var players = table.SeatedPlayerIds
            .Select(id => AIPlayerManager.Instance.GetPlayer(id))
            .Where(p => p != null)
            .ToList();

        // Randomly pick winner and distribute pot (simplified for now)
        if (players.Count >= 2)
        {
            var activePlayers = players.Where(p => !fullState.seats[players.IndexOf(p)].hasFolded).ToList();
            if (activePlayers.Count > 0)
            {
                var winner = activePlayers[UnityEngine.Random.Range(0, activePlayers.Count)];
                int contribution = fullState.totalPot / players.Count;

                foreach (var player in players)
                {
                    int seatIndex = table.SeatedPlayerIds.IndexOf(player.PlayerId);
                    int finalChips = fullState.seats[seatIndex].chipCount;

                    if (player == winner)
                    {
                        finalChips += fullState.totalPot;
                    }

                    player.UpdateChips(finalChips);
                    player.HandsPlayed++;

                    if (player == winner) player.HandsWon++;

                    if (player.ChipsAtTable <= 0)
                    {
                        PlayerLeavesTable(player, table);
                    }
                }
            }
        }

        table.AveragePot = (table.AveragePot * 0.9f) + (fullState.totalPot * 0.1f);
    }

    void SimulatePlayerMovement()
    {
        var seatedPlayers = AIPlayerManager.Instance.AllPlayers
            .Where(p => !string.IsNullOrEmpty(p.CurrentTableId))
            .ToList();

        if (seatedPlayers.Count > 0 && UnityEngine.Random.value < 0.3f)
        {
            var leaving = seatedPlayers[UnityEngine.Random.Range(0, seatedPlayers.Count)];
            var table = allTables.Find(t => t.TableId == leaving.CurrentTableId);
            if (table != null)
            {
                PlayerLeavesTable(leaving, table);
            }
        }

        var availablePlayers = AIPlayerManager.Instance.AllPlayers
            .Where(p => string.IsNullOrEmpty(p.CurrentTableId) && p.Bankroll >= 2)
            .ToList();

        if (availablePlayers.Count > 0)
        {
            var joining = availablePlayers[UnityEngine.Random.Range(0, availablePlayers.Count)];

            var openTables = allTables
                .Where(t => t.CurrentPlayers < t.MaxPlayers && joining.CanAffordTable(t.Stakes))
                .ToList();

            if (openTables.Count > 0)
            {
                var table = openTables[UnityEngine.Random.Range(0, openTables.Count)];
                PlayerJoinsTable(joining, table);
            }
        }
    }

    void PlayerJoinsTable(AIPlayer player, TableData table)
    {
        int buyIn = player.GetBuyInAmount(table.Stakes);
        player.SitAtTable(table.TableId, buyIn);
        table.SeatedPlayerIds.Add(player.PlayerId);
        table.CurrentPlayers++;
    }

    void PlayerLeavesTable(AIPlayer player, TableData table)
    {
        table.SeatedPlayerIds.Remove(player.PlayerId);
        table.CurrentPlayers--;
        player.LeaveTable();  // No parameters needed
    }

    void UpdateUI()
    {
        foreach (var kvp in tableRows)
        {
            var table = allTables.Find(t => t.TableId == kvp.Key);
            if (table != null)
            {
                kvp.Value.UpdateDisplay(table);
            }
        }

        UpdatePlayerCounts();
        UpdateSelectedTableDisplay();
    }

    void UpdatePlayerCounts()
    {
        int totalPlayers = AIPlayerManager.Instance.TotalPlayersOnline;
        int totalTableCount = allTables.Count;

        if (totalPlayersText != null)
            totalPlayersText.text = totalPlayers.ToString();

        if (totalTablesText != null)
            totalTablesText.text = totalTableCount.ToString();
    }

    // === TABLE SELECTION ===

    public void OnTableClicked(TableData table)
    {
        selectedTable = table;
        Debug.Log($"Selected table: {table.TableName} - {table.Stakes.Name}");
        UpdateSelectedTableDisplay();
        UpdateActionButtons();
    }

    void UpdateSelectedTableDisplay()
    {
        if (selectedTable == null)
        {
            if (tableInfoText != null)
                tableInfoText.text = "";
            if (playerListText != null)
                playerListText.text = "Select a table...";
            return;
        }

        if (tableInfoText != null)
        {
            tableInfoText.text = $"{selectedTable.TableName} - {selectedTable.Stakes.Name}";
        }

        if (playerListText != null)
        {
            var players = GetPlayersAtTable(selectedTable.TableId);
            if (players.Count == 0)
            {
                playerListText.text = "No players seated";
            }
            else
            {
                string playerText = "";
                foreach (var player in players)
                {
                    playerText += $"{player.PlayerName}  ${player.ChipsAtTable}\n";
                }
                playerListText.text = playerText;
            }
        }
    }

    void UpdateActionButtons()
    {
        if (selectedTable == null)
        {
            // No table selected
            if (joinTableButton != null)
            {
                joinTableButton.interactable = false;
                if (joinButtonText != null)
                    joinButtonText.text = "Select Table";
            }

            if (spectateTableButton != null)
                spectateTableButton.interactable = false;

            return;
        }

        // Check if player can afford this table
        bool canAfford = false;
        if (AccountManager.Instance != null && AccountManager.Instance.IsLoggedIn)
        {
            int bankroll = AccountManager.Instance.CurrentPlayer.Bankroll;
            canAfford = bankroll >= selectedTable.Stakes.MinBuyIn;
        }

        // Update Join button
        if (joinTableButton != null)
        {
            bool tableFull = selectedTable.CurrentPlayers >= selectedTable.MaxPlayers;

            if (tableFull)
            {
                joinTableButton.interactable = false;
                if (joinButtonText != null)
                    joinButtonText.text = "TABLE FULL";
            }
            else if (!canAfford)
            {
                joinTableButton.interactable = false;
                if (joinButtonText != null)
                    joinButtonText.text = "INSUFFICIENT FUNDS";
            }
            else
            {
                joinTableButton.interactable = true;
                if (joinButtonText != null)
                    joinButtonText.text = "SIT IN";
            }
        }

        // Update Spectate button (always enabled if table selected)
        if (spectateTableButton != null)
            spectateTableButton.interactable = true;
    }

    // === JOIN / SPECTATE ===

    public void JoinSelectedTable()
    {
        if (selectedTable == null)
        {
            Debug.LogWarning("No table selected!");
            return;
        }

        if (selectedTable.CurrentPlayers >= selectedTable.MaxPlayers)
        {
            Debug.LogWarning("Table is full!");
            return;
        }

        // Check if player can afford
        if (AccountManager.Instance == null || !AccountManager.Instance.IsLoggedIn)
        {
            Debug.LogWarning("Not logged in!");
            return;
        }

        int bankroll = AccountManager.Instance.CurrentPlayer.Bankroll;
        if (bankroll < selectedTable.Stakes.MinBuyIn)
        {
            Debug.LogWarning("Insufficient funds!");
            return;
        }

        Debug.Log($"Joining table: {selectedTable.TableName}");

        // Store table ID and player list for the game scene to load
        PlayerPrefs.SetString("SelectedTableId", selectedTable.TableId);
        PlayerPrefs.SetString("SelectedTableName", selectedTable.TableName);
        PlayerPrefs.SetString("JoinMode", "play");

        // Store stake level info
        PlayerPrefs.SetInt("TableSmallBlind", selectedTable.Stakes.SmallBlind);
        PlayerPrefs.SetInt("TableBigBlind", selectedTable.Stakes.BigBlind);

        // Store the AI player IDs at this table (comma-separated)
        string playerIds = string.Join(",", selectedTable.SeatedPlayerIds);
        PlayerPrefs.SetString("TablePlayerIds", playerIds);
        PlayerPrefs.SetInt("TableMaxPlayers", selectedTable.MaxPlayers);

        // ★★★ Check if joining mid-hand (use TableRegistry as source of truth) ★★★
        bool isHandInProgress = false;
        float handTime = 0f;
        TableState registryState = TableRegistry.Instance != null
            ? TableRegistry.Instance.GetTableState(selectedTable.TableId)
            : null;

        if (registryState != null)
        {
            isHandInProgress = registryState.currentStreet != "BetweenHands";
            UnityEngine.Debug.Log($"[Join] Registry street: {registryState.currentStreet}");
        }
        else
        {
            // Fallback to local timing if registry not available
            handTime = handDuration / Mathf.Max(2, selectedTable.CurrentPlayers);
            isHandInProgress = selectedTable.CurrentPlayers >= 2 &&
                               selectedTable.TimeSinceLastHand > 0 &&
                               selectedTable.TimeSinceLastHand < handTime;
        }
        PlayerPrefs.SetInt("JoiningMidHand", isHandInProgress ? 1 : 0);

        // ★★★ DEBUG LOGS
        UnityEngine.Debug.Log($"[Join] === MID-HAND CHECK ===");
        UnityEngine.Debug.Log($"[Join] Table: {selectedTable.TableName}");
        UnityEngine.Debug.Log($"[Join] TimeSinceLastHand: {selectedTable.TimeSinceLastHand:F2}s");
        UnityEngine.Debug.Log($"[Join] HandTime: {handTime:F2}s");
        UnityEngine.Debug.Log($"[Join] CurrentPlayers: {selectedTable.CurrentPlayers}");
        UnityEngine.Debug.Log($"[Join] isHandInProgress: {isHandInProgress}");
        UnityEngine.Debug.Log($"[Join] Flag value: {(isHandInProgress ? 1 : 0)}");
        if (!isHandInProgress)
        {
            UnityEngine.Debug.Log($"[Join] ✓ Joining between hands - normal join");
        }
        UnityEngine.Debug.Log($"[Join] ====================");

        if (isHandInProgress)
        {
            UnityEngine.Debug.Log($"[Join] ⚠️ Joining MID-HAND - player will wait for next hand");
        }

        PlayerPrefs.Save();

        Debug.Log($"[Join] Table: {selectedTable.TableName}, Players: {selectedTable.SeatedPlayerIds.Count}, Stakes: ${selectedTable.Stakes.SmallBlind}/${selectedTable.Stakes.BigBlind}");

        // ★★★ VERIFY PlayerPrefs before loading scene
        UnityEngine.Debug.Log($"[Join] DEBUG: Verifying PlayerPrefs before scene load:");
        UnityEngine.Debug.Log($"[Join] DEBUG:   SelectedTableId = '{PlayerPrefs.GetString("SelectedTableId", "EMPTY")}'");
        UnityEngine.Debug.Log($"[Join] DEBUG:   JoiningMidHand = {PlayerPrefs.GetInt("JoiningMidHand", -1)}");

        // Load the poker table scene
        Debug.Log($"Loading scene: {tableSceneName}");
        SceneManager.LoadScene(tableSceneName);
    }

    public void SpectateSelectedTable()
    {
        if (selectedTable == null)
        {
            Debug.LogWarning("No table selected!");
            return;
        }

        Debug.Log($"Spectating table: {selectedTable.TableName}");

        // Store table ID and player list for spectating
        PlayerPrefs.SetString("SelectedTableId", selectedTable.TableId);
        PlayerPrefs.SetString("SelectedTableName", selectedTable.TableName);
        PlayerPrefs.SetString("JoinMode", "spectate");

        // Store stake level info
        PlayerPrefs.SetInt("TableSmallBlind", selectedTable.Stakes.SmallBlind);
        PlayerPrefs.SetInt("TableBigBlind", selectedTable.Stakes.BigBlind);

        // Store the AI player IDs at this table
        string playerIds = string.Join(",", selectedTable.SeatedPlayerIds);
        PlayerPrefs.SetString("TablePlayerIds", playerIds);
        PlayerPrefs.SetInt("TableMaxPlayers", selectedTable.MaxPlayers);

        PlayerPrefs.Save();

        Debug.Log($"[Spectate] Table: {selectedTable.TableName}, Players: {selectedTable.SeatedPlayerIds.Count}");

        // Load the poker table scene in spectate mode
        Debug.Log($"Loading scene: {tableSceneName}");
        SceneManager.LoadScene(tableSceneName);
    }

    // === PUBLIC METHODS ===

    public TableData GetTable(string tableId)
    {
        return allTables.Find(t => t.TableId == tableId);
    }

    public List<AIPlayer> GetPlayersAtTable(string tableId)
    {
        var table = GetTable(tableId);
        if (table == null) return new List<AIPlayer>();

        return table.SeatedPlayerIds
            .Select(id => AIPlayerManager.Instance.GetPlayer(id))
            .Where(p => p != null)
            .ToList();
    }

    // ========================================
    // ★ DYNAMIC AI MANAGEMENT SYSTEMS
    // ========================================

    /// <summary>
    /// Dynamically fills empty seats across all tables
    /// Called every 15 seconds
    /// </summary>
    /// <summary>
    /// Dynamically fills empty seats - PRIORITIZES HIGH STAKES
    /// Called every 15 seconds
    /// </summary>
    void DynamicSeatFilling()
    {
        int seatsFilled = 0;

        // ★ PRIORITY: Fill high-stakes tables FIRST (highest to lowest)
        var sortedTables = allTables.OrderByDescending(t => t.Stakes.BigBlind).ToList();

        foreach (var table in sortedTables)
        {
            // Skip if table is full
            if (table.CurrentPlayers >= table.MaxPlayers)
                continue;

            // Calculate target players for this stake level
            int targetPlayers = GetRandomPlayerCount(table.MaxPlayers, table.Stakes);
            // ★ CRITICAL: Cap target at MaxPlayers!
            targetPlayers = Mathf.Min(targetPlayers, table.MaxPlayers);
            int emptySeats = targetPlayers - table.CurrentPlayers;

            if (emptySeats <= 0)
                continue;

            // Get available players who can afford this table
            var availablePlayers = AIPlayerManager.Instance.GetAvailablePlayers(table.Stakes, emptySeats);

            // ★ CRITICAL: Don't exceed MaxPlayers!
            int seatsToFill = Mathf.Min(availablePlayers.Count, table.MaxPlayers - table.CurrentPlayers);

            for (int i = 0; i < seatsToFill; i++)
            {
                var player = availablePlayers[i];
                int buyIn = player.GetBuyInAmount(table.Stakes);  // Always max buy-in
                player.SitAtTable(table.TableId, buyIn);
                table.SeatedPlayerIds.Add(player.PlayerId);
                table.CurrentPlayers++;
                seatsFilled++;

                // ★ SYNC WITH TABLEREGISTRY
                SyncPlayerToTableRegistry(table.TableId, player);

                // Log whales with special marker
                string marker = player.Bankroll >= 500000 ? "🐋" : "";
                UnityEngine.Debug.Log($"[DynamicSeatFilling] {marker}{player.PlayerName} (${player.Bankroll:#,0}) joined {table.TableName} with ${buyIn:#,0} [{table.CurrentPlayers}/{table.MaxPlayers}]");
            }
        }

        if (seatsFilled > 0)
        {
            UnityEngine.Debug.Log($"[DynamicSeatFilling] ✓ Filled {seatsFilled} empty seats");
            RefreshTableList();
        }
    }

    /// <summary>
    /// Remove broke players who are sitting out
    /// Called every 10 seconds
    /// </summary>
    void CleanupBrokePlayers()
    {
        var playersToRemove = AIPlayerManager.Instance.GetPlayersWhoShouldLeave();

        if (playersToRemove.Count == 0)
            return;

        foreach (var player in playersToRemove)
        {
            string tableId = player.CurrentTableId;
            var table = allTables.Find(t => t.TableId == tableId);

            if (table != null)
            {
                table.SeatedPlayerIds.Remove(player.PlayerId);
                table.CurrentPlayers--;

                UnityEngine.Debug.Log($"[CleanupBrokePlayers] {player.PlayerName} left {table.TableName} (broke and sitting out)");
            }

            player.LeaveTable();  // Resets bankroll to $1,000 if broke
        }

        UnityEngine.Debug.Log($"[CleanupBrokePlayers] ✓ Removed {playersToRemove.Count} broke players");
        RefreshTableList();
        AIPlayerManager.Instance.SavePlayers();
    }

    /// <summary>
    /// Continuous lobby refresh - updates every 2 seconds
    /// Runs even when player is at a table (lobby stays "alive")
    /// </summary>
    IEnumerator ContinuousLobbyRefresh()
    {
        UnityEngine.Debug.Log("[Lobby] ★ Starting continuous refresh - lobby will stay alive!");
        
        while (true)
        {
            yield return new WaitForSeconds(2f); // Refresh every 2 seconds
            
            // Update table counts from TableRegistry
            UpdateTableCountsFromRegistry();
            
            // Refresh UI
            RefreshTableList();
        }
    }

    /// <summary>
    /// Update table player counts from TableRegistry
    /// This pulls the REAL-TIME data from the persistent world
    /// </summary>
    void UpdateTableCountsFromRegistry()
    {
        var registryTables = TableRegistry.Instance.GetAllTables();
        
        foreach (var regTable in registryTables)
        {
            // Find matching table in lobby list
            var lobbyTable = allTables.FirstOrDefault(t => t.TableId == regTable.tableId);
            
            if (lobbyTable != null)
            {
                // Update with REAL count from registry
                int oldCount = lobbyTable.CurrentPlayers;
                lobbyTable.CurrentPlayers = regTable.OccupiedSeats;
                
                // ALSO update SeatedPlayerIds from registry
                lobbyTable.SeatedPlayerIds.Clear();
                var tableState = TableRegistry.Instance.GetTableState(regTable.tableId);
                if (tableState != null)
                {
                    foreach (var seat in tableState.seats)
                    {
                        if (seat.isOccupied && !string.IsNullOrEmpty(seat.playerName))
                        {
                            // Find player ID by name
                            var player = AIPlayerManager.Instance.AllPlayers.FirstOrDefault(p => p.PlayerName == seat.playerName);
                            if (player != null)
                            {
                                lobbyTable.SeatedPlayerIds.Add(player.PlayerId);
                                player.UpdateChips(seat.chipCount);
                                if (string.IsNullOrEmpty(player.CurrentTableId))
                                {
                                    player.CurrentTableId = regTable.tableId;
                                }
                            }
                        }
                    }

                    lobbyTable.AveragePot = tableState.totalPot;
                    lobbyTable.CurrentHandNumber = tableState.handNumber;
                }
                
                if (oldCount != lobbyTable.CurrentPlayers)
                {
                    // Count changed! Log it
                    UnityEngine.Debug.Log($"[Lobby] {lobbyTable.TableName}: {oldCount}/9 → {lobbyTable.CurrentPlayers}/9 (SeatedPlayerIds: {lobbyTable.SeatedPlayerIds.Count})");
                }
            }
        }
    }

    /// <summary>
    /// Save AI state periodically
    /// Called every minute
    /// </summary>
    void SaveAIState()
    {
        AIPlayerManager.Instance.SavePlayers();
    }

    /// <summary>
    /// Sync a player to TableRegistry when they're seated
    /// This ensures the table scene loads the correct state
    /// </summary>
    void SyncPlayerToTableRegistry(string tableId, AIPlayer player)
    {
        // Get current table state from registry
        var tableState = TableRegistry.Instance.GetTableState(tableId);

        if (tableState == null)
        {
            UnityEngine.Debug.LogWarning($"[Sync] TableState for {tableId} not found in registry!");
            return;
        }

        // Find the player's seat (or assign a new one)
        int seatIndex = -1;
        for (int i = 0; i < tableState.seats.Count; i++)
        {
            if (tableState.seats[i].playerName == player.PlayerName)
            {
                seatIndex = i;
                break;
            }
        }

        // If not found, assign to first empty seat
        if (seatIndex == -1)
        {
            for (int i = 0; i < tableState.seats.Count; i++)
            {
                if (!tableState.seats[i].isOccupied)
                {
                    seatIndex = i;
                    break;
                }
            }
        }

        if (seatIndex == -1)
        {
            UnityEngine.Debug.LogWarning($"[Sync] No empty seat found for {player.PlayerName} at table {tableId}");
            return;
        }

        // Update the seat in the table state
        var seat = tableState.seats[seatIndex];
        seat.isOccupied = true;
        seat.playerName = player.PlayerName;
        seat.chipCount = player.ChipsAtTable;
        seat.isSittingOut = false;  // ★ CRITICAL: NOT sitting out!
        seat.hasFolded = false;
        seat.isAllIn = false;
        seat.currentBet = 0;
        seat.holeCards.Clear();

        // Update the registry
        TableRegistry.Instance.UpdateTableState(tableId, tableState);

        UnityEngine.Debug.Log($"[Sync] Updated {player.PlayerName} in TableRegistry at seat {seatIndex} (${player.ChipsAtTable:#,0} chips, NOT sitting out)");
    }
}
