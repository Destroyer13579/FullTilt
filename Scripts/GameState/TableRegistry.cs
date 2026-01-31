using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Central registry for all active poker tables in the system
/// This is the "single source of truth" for table states
/// Tables can run in background (lightweight) or be actively rendered
/// </summary>
public class TableRegistry : MonoBehaviour
{
    // Singleton instance
    private static TableRegistry instance;
    public static TableRegistry Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("TableRegistry");
                instance = go.AddComponent<TableRegistry>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    // All active tables
    private Dictionary<string, PokerTableInfo> activeTables = new Dictionary<string, PokerTableInfo>();

    // Tables organized by stake level for quick lookup (using BigBlind as key)
    private Dictionary<int, List<string>> tablesByStake = new Dictionary<int, List<string>>();

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        UnityEngine.Debug.Log("[TableRegistry] Initialized - Ready to manage tables");
    }

    /// <summary>
    /// Register a new table in the system
    /// </summary>
    public string RegisterTable(StakeLevel stake, int maxSeats = 9)
    {
        string tableId = GenerateTableId(stake);

        PokerTableInfo tableInfo = new PokerTableInfo
        {
            tableId = tableId,
            stake = stake,
            maxSeats = maxSeats,
            currentState = new TableState { tableId = tableId },
            isActivelyRendered = false,
            createdTime = DateTime.Now
        };

        activeTables[tableId] = tableInfo;

        // Add to stake lookup (using BigBlind as key)
        int stakeKey = stake.BigBlind;
        if (!tablesByStake.ContainsKey(stakeKey))
        {
            tablesByStake[stakeKey] = new List<string>();
        }
        tablesByStake[stakeKey].Add(tableId);

        UnityEngine.Debug.Log($"[TableRegistry] Registered new table: {tableId} ({stake.Name})");

        return tableId;
    }

    /// <summary>
    /// Update a table's current state (called when table state changes)
    /// </summary>
    public void UpdateTableState(string tableId, TableState newState)
    {
        if (activeTables.ContainsKey(tableId))
        {
            activeTables[tableId].currentState = newState;
            activeTables[tableId].lastUpdated = DateTime.Now;
        }
        else
        {
            UnityEngine.Debug.LogWarning($"[TableRegistry] Tried to update unknown table: {tableId}");
        }
    }

    /// <summary>
    /// Get a table's current state
    /// </summary>
    public TableState GetTableState(string tableId)
    {
        if (activeTables.ContainsKey(tableId))
        {
            return activeTables[tableId].currentState;
        }

        UnityEngine.Debug.LogWarning($"[TableRegistry] Tried to get state for unknown table: {tableId}");
        return null;
    }

    /// <summary>
    /// Get table info (metadata + state)
    /// </summary>
    public PokerTableInfo GetTableInfo(string tableId)
    {
        if (activeTables.ContainsKey(tableId))
        {
            return activeTables[tableId];
        }
        return null;
    }

    /// <summary>
    /// Find all tables at a specific stake level
    /// </summary>
    public List<PokerTableInfo> GetTablesAtStake(StakeLevel stake)
    {
        List<PokerTableInfo> tables = new List<PokerTableInfo>();

        int stakeKey = stake.BigBlind;
        if (tablesByStake.ContainsKey(stakeKey))
        {
            foreach (string tableId in tablesByStake[stakeKey])
            {
                if (activeTables.ContainsKey(tableId))
                {
                    tables.Add(activeTables[tableId]);
                }
            }
        }

        return tables;
    }

    /// <summary>
    /// Find tables with open seats at a specific stake
    /// </summary>
    public List<PokerTableInfo> FindTablesWithOpenSeats(StakeLevel stake, int seatsNeeded = 1)
    {
        List<PokerTableInfo> openTables = new List<PokerTableInfo>();

        List<PokerTableInfo> stakeTables = GetTablesAtStake(stake);

        foreach (var table in stakeTables)
        {
            int occupiedSeats = table.currentState.seats.Count(s => s.isOccupied);
            int availableSeats = table.maxSeats - occupiedSeats;

            if (availableSeats >= seatsNeeded)
            {
                openTables.Add(table);
            }
        }

        return openTables;
    }

    /// <summary>
    /// Check if we need to spawn a new table at this stake
    /// (all tables are full or nearly full)
    /// </summary>
    public bool ShouldSpawnNewTable(StakeLevel stake)
    {
        List<PokerTableInfo> openTables = FindTablesWithOpenSeats(stake, 2);

        // If no tables with 2+ open seats, spawn a new one
        return openTables.Count == 0;
    }

    /// <summary>
    /// Remove/close a table (when it becomes empty)
    /// </summary>
    public void UnregisterTable(string tableId)
    {
        if (activeTables.ContainsKey(tableId))
        {
            PokerTableInfo table = activeTables[tableId];

            // Remove from stake lookup (using BigBlind as key)
            int stakeKey = table.stake.BigBlind;
            if (tablesByStake.ContainsKey(stakeKey))
            {
                tablesByStake[stakeKey].Remove(tableId);
            }

            activeTables.Remove(tableId);

            UnityEngine.Debug.Log($"[TableRegistry] Unregistered table: {tableId}");
        }
    }

    /// <summary>
    /// Get all active tables
    /// </summary>
    public List<PokerTableInfo> GetAllTables()
    {
        return activeTables.Values.ToList();
    }

    /// <summary>
    /// Get count of tables at each stake (keyed by BigBlind amount)
    /// </summary>
    public Dictionary<int, int> GetTableCountsByStake()
    {
        Dictionary<int, int> counts = new Dictionary<int, int>();

        foreach (var kvp in tablesByStake)
        {
            counts[kvp.Key] = kvp.Value.Count;
        }

        return counts;
    }

    /// <summary>
    /// Mark a table as actively rendered (player is viewing it)
    /// </summary>
    public void SetTableActivelyRendered(string tableId, bool isActive)
    {
        if (activeTables.ContainsKey(tableId))
        {
            activeTables[tableId].isActivelyRendered = isActive;
            UnityEngine.Debug.Log($"[TableRegistry] Table {tableId} render status: {isActive}");
        }
    }

    // Generate unique table ID
    private string GenerateTableId(StakeLevel stake)
    {
        int stakeKey = stake.BigBlind;
        int tableNumber = 1;
        if (tablesByStake.ContainsKey(stakeKey))
        {
            tableNumber = tablesByStake[stakeKey].Count + 1;
        }

        // Use stake name (e.g., "$50/$100") in table ID
        string stakeName = stake.Name.Replace("$", "").Replace("/", "_");
        return $"{stakeName}_Table{tableNumber}_{UnityEngine.Random.Range(1000, 9999)}";
    }

    /// <summary>
    /// Debug: Print all active tables
    /// </summary>
    public void PrintTableStatus()
    {
        UnityEngine.Debug.Log("=== TABLE REGISTRY STATUS ===");
        UnityEngine.Debug.Log($"Total active tables: {activeTables.Count}");

        foreach (var kvp in GetTableCountsByStake())
        {
            UnityEngine.Debug.Log($"  {kvp.Key}: {kvp.Value} tables");
        }

        foreach (var table in activeTables.Values)
        {
            int occupied = table.currentState.seats.Count(s => s.isOccupied);
            UnityEngine.Debug.Log($"  [{table.tableId}] - {occupied}/{table.maxSeats} seats - Pot: ${table.currentState.totalPot} - {table.currentState.currentStreet}");
        }

        UnityEngine.Debug.Log("============================");
    }
}

/// <summary>
/// Information about a poker table
/// </summary>
[Serializable]
public class PokerTableInfo
{
    public string tableId;
    public StakeLevel stake;
    public int maxSeats;

    // Current game state (snapshot)
    public TableState currentState;

    // Is this table currently being rendered in Unity?
    // (false = running in background, true = player is viewing it)
    public bool isActivelyRendered;

    // Timestamps
    public DateTime createdTime;
    public DateTime lastUpdated;

    // Helper properties
    public int OccupiedSeats => currentState?.seats?.Count(s => s.isOccupied) ?? 0;
    public int AvailableSeats => maxSeats - OccupiedSeats;
    public bool IsFull => AvailableSeats == 0;
    public bool IsEmpty => OccupiedSeats == 0;
}
