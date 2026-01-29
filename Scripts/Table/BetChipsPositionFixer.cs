using UnityEngine;

/// <summary>
/// Attach this to Table or PokerGameManager and run it once to fix all BetChips positions
/// Right-click component → "Fix All Bet Chips Positions"
/// </summary>
public class BetChipsPositionFixer : MonoBehaviour
{
    public TableManager tableManager;

    [Header("Bet Chips Position Settings")]
    [Tooltip("How far below the player seat should bet chips appear?")]
    public float yOffset = -50f;

    [ContextMenu("Fix All Bet Chips Positions")]
    public void FixAllBetChipsPositions()
    {
        if (tableManager == null)
        {
            tableManager = FindObjectOfType<TableManager>();
        }

        if (tableManager == null)
        {
            UnityEngine.Debug.LogError("❌ TableManager not found!");
            return;
        }

        UnityEngine.Debug.Log("====================================");
        UnityEngine.Debug.Log("FIXING BET CHIPS POSITIONS");
        UnityEngine.Debug.Log("====================================\n");

        int fixedCount = 0;

        foreach (var seat in tableManager.seats)
        {
            if (seat == null) continue;

            // Find the BetChips child
            Transform betChipsTransform = seat.transform.Find("BetChips");

            if (betChipsTransform == null)
            {
                UnityEngine.Debug.LogWarning($"⚠ PlayerSeat {seat.seatIndex}: No BetChips child found");
                continue;
            }

            // Get or add RectTransform
            RectTransform rt = betChipsTransform.GetComponent<RectTransform>();
            if (rt == null)
            {
                UnityEngine.Debug.LogError($"❌ PlayerSeat {seat.seatIndex}: BetChips doesn't have RectTransform!");
                continue;
            }

            // Reset anchors to center
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            // Set position relative to parent
            rt.anchoredPosition = new Vector2(0, yOffset);
            rt.localScale = Vector3.one;

            // Make sure it's enabled
            betChipsTransform.gameObject.SetActive(true);

            UnityEngine.Debug.Log($"✓ Fixed PlayerSeat {seat.seatIndex} BetChips - Position: {rt.anchoredPosition}");
            fixedCount++;
        }

        UnityEngine.Debug.Log($"\n====================================");
        UnityEngine.Debug.Log($"✓ Fixed {fixedCount} BetChips positions!");
        UnityEngine.Debug.Log($"====================================");
    }

    [ContextMenu("Test Bet Chips - Show $25 on All Seats")]
    public void TestBetChipsOnAllSeats()
    {
        if (tableManager == null)
        {
            tableManager = FindObjectOfType<TableManager>();
        }

        if (tableManager == null)
        {
            UnityEngine.Debug.LogError("❌ TableManager not found!");
            return;
        }

        UnityEngine.Debug.Log("Testing bet chips - showing $25 on all seated players...");

        foreach (var seat in tableManager.seats)
        {
            if (seat != null && seat.IsSeated && seat.betChipStack != null)
            {
                seat.betChipStack.ShowChips(25);
                UnityEngine.Debug.Log($"✓ Showing $25 bet chips on seat {seat.seatIndex}");
            }
        }
    }

    [ContextMenu("Clear All Bet Chips")]
    public void ClearAllBetChips()
    {
        if (tableManager == null)
        {
            tableManager = FindObjectOfType<TableManager>();
        }

        if (tableManager == null) return;

        foreach (var seat in tableManager.seats)
        {
            if (seat != null && seat.betChipStack != null)
            {
                seat.betChipStack.ClearChips();
            }
        }

        UnityEngine.Debug.Log("✓ Cleared all bet chips");
    }
}
