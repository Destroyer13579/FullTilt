using UnityEngine;

/// <summary>
/// Tests if bet chips spawn correctly
/// Add to PokerGameManager and press T during gameplay
/// </summary>
public class TestBetChips : MonoBehaviour
{
    public TableManager tableManager;
    public int testAmount = 25;

    void Update()
    {
        // Press T to test
        if (Input.GetKeyDown(KeyCode.T))
        {
            TestAllBetChips();
        }

        // Press C to clear
        if (Input.GetKeyDown(KeyCode.C))
        {
            ClearAllBetChips();
        }
    }

    void TestAllBetChips()
    {
        if (tableManager == null)
        {
            tableManager = FindObjectOfType<TableManager>();
        }

        if (tableManager == null)
        {
            UnityEngine.Debug.LogError("No TableManager found!");
            return;
        }

        UnityEngine.Debug.Log($"====================================");
        UnityEngine.Debug.Log($"TESTING BET CHIPS - Showing ${testAmount} on all seats");
        UnityEngine.Debug.Log($"====================================");

        foreach (var seat in tableManager.seats)
        {
            if (seat == null) continue;

            if (seat.betChipStack == null)
            {
                UnityEngine.Debug.LogError($"Seat {seat.seatIndex}: NO betChipStack assigned!");
                continue;
            }

            UnityEngine.Debug.Log($"Showing ${testAmount} on Seat {seat.seatIndex}...");
            seat.betChipStack.ShowChips(testAmount);
        }

        UnityEngine.Debug.Log($"====================================");
        UnityEngine.Debug.Log($"Test complete! Check the table - you should see chips at each white box.");
        UnityEngine.Debug.Log($"Press C to clear all bet chips");
        UnityEngine.Debug.Log($"====================================");
    }

    void ClearAllBetChips()
    {
        if (tableManager == null)
        {
            tableManager = FindObjectOfType<TableManager>();
        }

        if (tableManager == null) return;

        UnityEngine.Debug.Log("Clearing all bet chips...");

        foreach (var seat in tableManager.seats)
        {
            if (seat != null && seat.betChipStack != null)
            {
                seat.betChipStack.ClearChips();
            }
        }

        UnityEngine.Debug.Log("✓ All bet chips cleared");
    }
}
