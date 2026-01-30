using System;
using System.Collections.Generic;
using System.Linq;

public class PersistentWorldHandSimulation
{
    private readonly LobbyPokerSimulator pokerSimulator;
    private readonly Dictionary<string, float> tableHandTimers = new Dictionary<string, float>();
    private readonly Dictionary<string, int> tableDealerPositions = new Dictionary<string, int>();
    private readonly Dictionary<string, int> tableHandNumbers = new Dictionary<string, int>();

    public float BaseHandDuration { get; set; }

    public PersistentWorldHandSimulation(float baseHandDuration)
    {
        BaseHandDuration = baseHandDuration;
        pokerSimulator = new LobbyPokerSimulator();
    }

    public void Tick(float deltaSeconds)
    {
        var allTables = TableRegistry.Instance.GetAllTables();
        foreach (var tableInfo in allTables)
        {
            int occupiedSeats = tableInfo.OccupiedSeats;
            if (occupiedSeats < 2)
            {
                continue;
            }

            if (!tableHandTimers.ContainsKey(tableInfo.tableId))
            {
                tableHandTimers[tableInfo.tableId] = 0f;
            }

            float handTime = BaseHandDuration / System.Math.Max(2, occupiedSeats);
            tableHandTimers[tableInfo.tableId] += deltaSeconds;

            if (tableHandTimers[tableInfo.tableId] >= handTime)
            {
                tableHandTimers[tableInfo.tableId] = 0f;
                SimulateHandForTable(tableInfo);
            }
        }
    }

    private void SimulateHandForTable(PokerTableInfo tableInfo)
    {
        TableData tableData = BuildTableDataFromRegistry(tableInfo);
        if (tableData.CurrentPlayers < 2)
        {
            return;
        }

        if (!tableDealerPositions.ContainsKey(tableInfo.tableId))
        {
            tableDealerPositions[tableInfo.tableId] = 0;
        }

        if (!tableHandNumbers.ContainsKey(tableInfo.tableId))
        {
            tableHandNumbers[tableInfo.tableId] = tableInfo.currentState != null ? tableInfo.currentState.handNumber : 0;
        }

        int dealerSeat = tableDealerPositions[tableInfo.tableId];
        tableData.CurrentHandNumber = tableHandNumbers[tableInfo.tableId];

        TableState newState = pokerSimulator.SimulateHand(tableData, tableData.CurrentHandNumber, dealerSeat);
        TableRegistry.Instance.UpdateTableState(tableInfo.tableId, newState);

        foreach (var seat in newState.seats)
        {
            if (!seat.isOccupied || string.IsNullOrEmpty(seat.playerName))
            {
                continue;
            }

            var player = AIPlayerManager.Instance.AllPlayers.FirstOrDefault(p => p.PlayerName == seat.playerName);
            if (player != null)
            {
                player.UpdateChips(seat.chipCount);
                if (string.IsNullOrEmpty(player.CurrentTableId))
                {
                    player.CurrentTableId = tableInfo.tableId;
                }
            }
        }

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
        tableHandNumbers[tableInfo.tableId] = tableData.CurrentHandNumber + 1;
    }

    private TableData BuildTableDataFromRegistry(PokerTableInfo tableInfo)
    {
        TableData tableData = new TableData(tableInfo.tableId, tableInfo.stake, tableInfo.maxSeats)
        {
            TableId = tableInfo.tableId
        };

        tableData.SeatedPlayerIds = new List<string>(new string[tableInfo.maxSeats]);
        tableData.CurrentPlayers = 0;

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
                    tableData.SeatedPlayerIds[i] = player.PlayerId;
                    tableData.CurrentPlayers++;
                }
            }
        }

        return tableData;
    }
}
