using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles joining poker tables mid-hand
/// Key feature: Players can join while hand is in progress and wait for next hand
/// </summary>
public class TableJoiner : MonoBehaviour
{
    [Header("References")]
    public PokerGameManager gameManager;
    public TableManager tableManager;

    [Header("Settings")]
    public bool allowSpectating = true;
    public bool allowMidHandJoin = true;  // If false, must wait until table is between hands

    /// <summary>
    /// Attempt to join the table (main entry point)
    /// </summary>
    public JoinResult TryJoinTable(string playerId, string playerName, int buyInAmount, int avatarId = 0)
    {
        JoinResult result = new JoinResult();
        result.playerId = playerId;
        result.playerName = playerName;

        // Check if table has space
        int openSeatIndex = FindOpenSeat();
        if (openSeatIndex == -1)
        {
            result.success = false;
            result.reason = "Table is full";
            UnityEngine.Debug.Log($"[TableJoiner] {playerName} cannot join - table is full");
            return result;
        }

        // Check if hand is in progress
        bool handInProgress = gameManager.IsHandInProgress;

        if (handInProgress)
        {
            // Hand is in progress - join as "waiting for next hand"
            result = JoinMidHand(playerId, playerName, buyInAmount, avatarId, openSeatIndex);
        }
        else
        {
            // No hand in progress - normal join
            result = JoinNormal(playerId, playerName, buyInAmount, avatarId, openSeatIndex);
        }

        return result;
    }

    /// <summary>
    /// Join when hand is in progress - player waits for next hand
    /// </summary>
    private JoinResult JoinMidHand(string playerId, string playerName, int buyInAmount, int avatarId, int seatIndex)
    {
        JoinResult result = new JoinResult();
        result.success = true;
        result.seatIndex = seatIndex;
        result.joinedMidHand = true;
        result.waitingForNextHand = true;

        // Seat the player but mark them as waiting
        PlayerSeat seat = tableManager.seats[seatIndex];
        seat.SeatPlayer(playerId, playerName, buyInAmount, avatarId, isLocal: playerId == "LOCAL_PLAYER");

        // Mark as sitting out for this hand (won't be dealt in)
        PlayerSeatStatus status = seat.GetComponent<PlayerSeatStatus>();
        if (status != null)
        {
            status.isWaitingForNextHand = true;
            UnityEngine.Debug.Log($"[TableJoiner] {playerName} marked as waiting for next hand");
        }

        // ★ Removed "WAITING" text display - player will sit quietly without UI clutter

        UnityEngine.Debug.Log($"[TableJoiner] {playerName} joined mid-hand at seat {seatIndex} - waiting for next hand");
        UnityEngine.Debug.Log($"[TableJoiner] Current game state: {gameManager.CurrentState}");

        result.reason = "Joined successfully - waiting for next hand to begin";
        return result;
    }

    /// <summary>
    /// Normal join when no hand is in progress
    /// </summary>
    private JoinResult JoinNormal(string playerId, string playerName, int buyInAmount, int avatarId, int seatIndex)
    {
        JoinResult result = new JoinResult();
        result.success = true;
        result.seatIndex = seatIndex;
        result.joinedMidHand = false;
        result.waitingForNextHand = false;

        // Seat the player normally
        PlayerSeat seat = tableManager.seats[seatIndex];
        seat.SeatPlayer(playerId, playerName, buyInAmount, avatarId, isLocal: playerId == "LOCAL_PLAYER");

        UnityEngine.Debug.Log($"[TableJoiner] {playerName} joined at seat {seatIndex} - will be dealt in next hand");

        result.reason = "Joined successfully - will be dealt in next hand";
        return result;
    }

    /// <summary>
    /// Join existing table from TableRegistry (for table switching)
    /// </summary>
    public JoinResult JoinFromRegistry(string tableId, string playerId, string playerName, int buyInAmount, int avatarId = 0)
    {
        // Get table state from registry
        TableState state = TableRegistry.Instance.GetTableState(tableId);
        if (state == null)
        {
            JoinResult failResult = new JoinResult();
            failResult.success = false;
            failResult.reason = "Table not found in registry";
            return failResult;
        }

        // Apply snapshot to render the table
        gameManager.ApplySnapshot(state);

        // Now join normally
        return TryJoinTable(playerId, playerName, buyInAmount, avatarId);
    }

    /// <summary>
    /// Handle player leaving waiting state (when hand starts)
    /// Call this from PokerGameManager.StartNewHand()
    /// </summary>
    public void ProcessWaitingPlayers()
    {
        foreach (var seat in tableManager.seats)
        {
            if (seat != null && seat.IsSeated)
            {
                PlayerSeatStatus status = seat.GetComponent<PlayerSeatStatus>();
                if (status != null && status.isWaitingForNextHand)
                {
                    // Clear waiting flag - player will now be dealt in
                    status.isWaitingForNextHand = false;

                    // Clear "WAITING" text
                    seat.UpdateChips(seat.ChipCount);

                    UnityEngine.Debug.Log($"[TableJoiner] {seat.PlayerName} is no longer waiting - will be dealt in");
                }
            }
        }
    }

    /// <summary>
    /// Find first open seat
    /// </summary>
    private int FindOpenSeat()
    {
        for (int i = 0; i < tableManager.seats.Count; i++)
        {
            if (tableManager.seats[i].IsEmpty)
            {
                return i;
            }
        }
        return -1;  // No open seats
    }

    /// <summary>
    /// Check if player can see specific cards (for card visibility rules)
    /// </summary>
    public bool CanSeeCards(string playerId, int seatIndex)
    {
        // Player can only see their own cards
        PlayerSeat seat = tableManager.seats[seatIndex];
        return seat != null && seat.IsSeated && seat.PlayerId == playerId;
    }

    /// <summary>
    /// Get list of open seats
    /// </summary>
    public List<int> GetOpenSeats()
    {
        List<int> openSeats = new List<int>();
        for (int i = 0; i < tableManager.seats.Count; i++)
        {
            if (tableManager.seats[i].IsEmpty)
            {
                openSeats.Add(i);
            }
        }
        return openSeats;
    }

    /// <summary>
    /// Check if player is waiting for next hand
    /// </summary>
    public bool IsWaitingForNextHand(int seatIndex)
    {
        if (seatIndex < 0 || seatIndex >= tableManager.seats.Count)
            return false;

        PlayerSeat seat = tableManager.seats[seatIndex];
        PlayerSeatStatus status = seat?.GetComponent<PlayerSeatStatus>();

        return status != null && status.isWaitingForNextHand;
    }
}

/// <summary>
/// Result of a join attempt
/// </summary>
public class JoinResult
{
    public bool success;
    public string reason;
    public string playerId;
    public string playerName;
    public int seatIndex = -1;
    public bool joinedMidHand;
    public bool waitingForNextHand;

    public override string ToString()
    {
        return $"JoinResult: {(success ? "SUCCESS" : "FAILED")} - {reason} (Seat: {seatIndex})";
    }
}
