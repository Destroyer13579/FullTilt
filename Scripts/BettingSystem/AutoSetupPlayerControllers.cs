using UnityEngine;

/// <summary>
/// Auto-adds PokerPlayerController to all PlayerSeats with one click
/// Attach to TableManager or PokerGameManager
/// </summary>
public class AutoSetupPlayerControllers : MonoBehaviour
{
    public TableManager tableManager;

    [Header("AI Settings (applied to all AI players)")]
    public float minThinkTime = 0.5f;
    public float maxThinkTime = 3.0f;
    public float fastActionChance = 0.3f;

    [ContextMenu("Auto Add PokerPlayerController to All Seats")]
    public void AutoSetupAll()
    {
        if (tableManager == null)
        {
            tableManager = FindObjectOfType<TableManager>();
        }

        if (tableManager == null)
        {
            UnityEngine.Debug.LogError("❌ No TableManager found!");
            return;
        }

        UnityEngine.Debug.Log("====================================");
        UnityEngine.Debug.Log("AUTO SETUP POKER PLAYER CONTROLLERS");
        UnityEngine.Debug.Log("====================================\n");

        int added = 0;
        int existing = 0;

        foreach (var seat in tableManager.seats)
        {
            if (seat == null) continue;

            // Check if already has PokerPlayerController
            PokerPlayerController controller = seat.GetComponent<PokerPlayerController>();
            
            if (controller == null)
            {
                // Add PokerPlayerController component
                controller = seat.gameObject.AddComponent<PokerPlayerController>();
                
                // Configure AI settings
                controller.minThinkTime = minThinkTime;
                controller.maxThinkTime = maxThinkTime;
                controller.fastActionChance = fastActionChance;
                
                UnityEngine.Debug.Log($"✓ Added PokerPlayerController to {seat.name}");
                added++;
            }
            else
            {
                UnityEngine.Debug.Log($"  {seat.name} already has PokerPlayerController");
                existing++;
            }
        }

        UnityEngine.Debug.Log("\n====================================");
        UnityEngine.Debug.Log($"✓ Setup Complete!");
        UnityEngine.Debug.Log($"  Added: {added}");
        UnityEngine.Debug.Log($"  Already had: {existing}");
        UnityEngine.Debug.Log($"  Total seats: {tableManager.seats.Count}");
        UnityEngine.Debug.Log("====================================");
        UnityEngine.Debug.Log("\nPokerPlayerController auto-detects AI vs Human!");
        UnityEngine.Debug.Log("No need to configure each seat separately.");
    }

    [ContextMenu("Remove All PokerPlayerControllers")]
    public void RemoveAll()
    {
        if (tableManager == null)
        {
            tableManager = FindObjectOfType<TableManager>();
        }

        if (tableManager == null) return;

        int removed = 0;

        foreach (var seat in tableManager.seats)
        {
            if (seat == null) continue;

            PokerPlayerController controller = seat.GetComponent<PokerPlayerController>();
            if (controller != null)
            {
                DestroyImmediate(controller);
                removed++;
            }
        }

        UnityEngine.Debug.Log($"Removed {removed} PokerPlayerController components");
    }
}
