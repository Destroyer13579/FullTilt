using UnityEngine;

/// <summary>
/// Example usage of TableRegistry
/// Add this to PokerGameManager.Update() to test with keyboard shortcuts
/// </summary>
public class TableRegistryTester : MonoBehaviour
{
    void Update()
    {
        // Press '1' to register some test tables
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            TestRegisterTables();
        }
        
        // Press '2' to print table status
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            TableRegistry.Instance.PrintTableStatus();
        }
        
        // Press '3' to test finding open tables
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            TestFindOpenTables();
        }
        
        // Press '4' to test table spawning logic
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            TestSpawnLogic();
        }
    }
    
    void TestRegisterTables()
    {
        UnityEngine.Debug.Log("=== TESTING: Registering Tables ===");
        
        // Create stake levels
        StakeLevel lowStakes = new StakeLevel(50, 100);    // $50/$100
        StakeLevel mediumStakes = new StakeLevel(100, 200); // $100/$200
        StakeLevel highStakes = new StakeLevel(500, 1000); // $500/$1000
        
        // Register a few tables at different stakes
        string table1 = TableRegistry.Instance.RegisterTable(lowStakes);
        string table2 = TableRegistry.Instance.RegisterTable(mediumStakes);
        string table3 = TableRegistry.Instance.RegisterTable(highStakes);
        
        // Simulate some players at table1
        TableState state1 = TableRegistry.Instance.GetTableState(table1);
        state1.totalPot = 500;
        state1.currentStreet = "Flop";
        
        // Add some fake occupied seats
        for (int i = 0; i < 5; i++)
        {
            SeatSnapshot seat = new SeatSnapshot
            {
                seatIndex = i,
                isOccupied = true,
                playerName = $"Player{i}",
                chipCount = 1000 + (i * 100)
            };
            state1.seats.Add(seat);
        }
        
        TableRegistry.Instance.UpdateTableState(table1, state1);
        
        UnityEngine.Debug.Log($"Created 3 test tables");
        TableRegistry.Instance.PrintTableStatus();
    }
    
    void TestFindOpenTables()
    {
        UnityEngine.Debug.Log("=== TESTING: Finding Open Tables ===");
        
        // Create stake levels
        StakeLevel lowStakes = new StakeLevel(50, 100);
        StakeLevel mediumStakes = new StakeLevel(100, 200);
        
        // Find tables with open seats at different stakes
        var openLow = TableRegistry.Instance.FindTablesWithOpenSeats(lowStakes);
        var openMedium = TableRegistry.Instance.FindTablesWithOpenSeats(mediumStakes);
        
        UnityEngine.Debug.Log($"Open tables at {lowStakes.Name}: {openLow.Count}");
        UnityEngine.Debug.Log($"Open tables at {mediumStakes.Name}: {openMedium.Count}");
        
        foreach (var table in openLow)
        {
            UnityEngine.Debug.Log($"  {table.tableId}: {table.AvailableSeats} seats available");
        }
    }
    
    void TestSpawnLogic()
    {
        UnityEngine.Debug.Log("=== TESTING: Spawn Logic ===");
        
        // Create different stake levels
        StakeLevel[] stakes = {
            new StakeLevel(50, 100),   // Low
            new StakeLevel(100, 200),  // Medium
            new StakeLevel(500, 1000)  // High
        };
        
        foreach (var stake in stakes)
        {
            bool shouldSpawn = TableRegistry.Instance.ShouldSpawnNewTable(stake);
            UnityEngine.Debug.Log($"{stake.Name}: Should spawn new table? {shouldSpawn}");
        }
    }
}

/// <summary>
/// Add these methods to PokerGameManager to integrate with TableRegistry
/// </summary>
public static class TableRegistryIntegration
{
    /// <summary>
    /// Example: Register your current table when game starts
    /// Add this to PokerGameManager.Start()
    /// </summary>
    public static string RegisterCurrentTable(TableManager tableManager)
    {
        // Determine stake level from table's big blind
        StakeLevel stake = GetStakeLevelFromBlind(tableManager.bigBlind);
        
        // Register the table
        string tableId = TableRegistry.Instance.RegisterTable(stake, tableManager.seats.Count);
        
        UnityEngine.Debug.Log($"[TableRegistry] Registered current table as: {tableId}");
        
        return tableId;
    }
    
    /// <summary>
    /// Example: Update registry whenever your table state changes
    /// Call this after taking a snapshot in PokerGameManager
    /// </summary>
    public static void SyncTableToRegistry(string tableId, TableState currentState)
    {
        TableRegistry.Instance.UpdateTableState(tableId, currentState);
    }
    
    /// <summary>
    /// Helper: Convert big blind amount to stake level
    /// </summary>
    private static StakeLevel GetStakeLevelFromBlind(int bigBlind)
    {
        // Return the appropriate StakeLevel based on big blind amount
        if (bigBlind <= 20) return new StakeLevel(10, 20);
        if (bigBlind <= 100) return new StakeLevel(50, 100);
        if (bigBlind <= 200) return new StakeLevel(100, 200);
        if (bigBlind <= 1000) return new StakeLevel(500, 1000);
        if (bigBlind <= 2000) return new StakeLevel(1000, 2000);
        return new StakeLevel(5000, 10000);
    }
}
