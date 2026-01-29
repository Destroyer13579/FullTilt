using System.Collections.Generic;
using UnityEngine;

public class SeatPositionManager : MonoBehaviour
{
    [Header("References")]
    public TableManager tableManager;

    [Header("Seat Visual Positions (stored on first run)")]
    [SerializeField] private List<Vector2> originalPositions = new List<Vector2>();
    [SerializeField] private List<Vector3> originalScales = new List<Vector3>();

    private bool positionsCaptured = false;

    void Start()
    {
        if (tableManager == null)
            tableManager = FindObjectOfType<TableManager>();

        // Capture positions on first run
        if (!positionsCaptured && tableManager != null && tableManager.seats.Count > 0)
        {
            CaptureCurrentPositions();
        }
    }

    /// <summary>
    /// Saves the current seat positions
    /// </summary>
    [ContextMenu("Capture Current Positions")]
    public void CaptureCurrentPositions()
    {
        if (tableManager == null || tableManager.seats.Count == 0)
        {
            UnityEngine.Debug.LogWarning("No seats to capture!");
            return;
        }

        originalPositions.Clear();
        originalScales.Clear();

        foreach (var seat in tableManager.seats)
        {
            if (seat != null)
            {
                RectTransform rt = seat.GetComponent<RectTransform>();
                if (rt != null)
                {
                    originalPositions.Add(rt.anchoredPosition);
                    originalScales.Add(rt.localScale);
                }
            }
        }

        positionsCaptured = true;
        UnityEngine.Debug.Log($"Captured {originalPositions.Count} seat positions");
    }

    /// <summary>
    /// Rotates all seats so the player's logical seat appears at position 0 (bottom)
    /// </summary>
    public void RotateToPlayerSeat(int playerLogicalSeat)
    {
        if (!positionsCaptured || originalPositions.Count == 0)
        {
            CaptureCurrentPositions();
        }

        if (tableManager == null || tableManager.seats.Count != originalPositions.Count)
        {
            UnityEngine.Debug.LogWarning("Seat count mismatch!");
            return;
        }

        int seatCount = tableManager.seats.Count;

        // Move each seat to its rotated visual position
        // Player's seat (playerLogicalSeat) should end up at visual position 0
        for (int i = 0; i < seatCount; i++)
        {
            PlayerSeat seat = tableManager.seats[i];
            if (seat == null) continue;

            // Calculate which visual position this logical seat should occupy
            // If player sat at seat 3, then:
            // - Seat 3 goes to position 0
            // - Seat 4 goes to position 1
            // - Seat 5 goes to position 2
            // etc.
            int visualPos = (i - playerLogicalSeat + seatCount) % seatCount;

            RectTransform rt = seat.GetComponent<RectTransform>();
            if (rt != null && visualPos < originalPositions.Count)
            {
                rt.anchoredPosition = originalPositions[visualPos];
                rt.localScale = originalScales[visualPos];
            }
        }

        UnityEngine.Debug.Log($"Table rotated: Logical seat {playerLogicalSeat} now at bottom");
    }

    /// <summary>
    /// Resets all seats to their original positions
    /// </summary>
    public void ResetRotation()
    {
        if (tableManager == null) return;

        for (int i = 0; i < tableManager.seats.Count && i < originalPositions.Count; i++)
        {
            PlayerSeat seat = tableManager.seats[i];
            if (seat == null) continue;

            RectTransform rt = seat.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = originalPositions[i];
                rt.localScale = originalScales[i];
            }
        }
    }
}
