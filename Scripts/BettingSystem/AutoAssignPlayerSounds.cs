using UnityEngine;

/// <summary>
/// Automatically assigns action sounds to all PokerPlayerController components
/// Run this once to set up sounds on all AI players
/// </summary>
public class AutoAssignPlayerSounds : MonoBehaviour
{
    [Header("Action Sounds (Assign These)")]
    public AudioClip foldSound;
    public AudioClip checkSound;
    public AudioClip betCallSound;
    public AudioClip raiseSound;

    [Header("References")]
    public TableManager tableManager;

    [ContextMenu("Assign Sounds to All Players")]
    public void AssignSoundsToAllPlayers()
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
        UnityEngine.Debug.Log("ASSIGNING SOUNDS TO ALL PLAYERS");
        UnityEngine.Debug.Log("====================================\n");

        int successCount = 0;
        int skipCount = 0;

        foreach (var seat in tableManager.seats)
        {
            if (seat == null) continue;

            PokerPlayerController controller = seat.GetComponent<PokerPlayerController>();
            
            if (controller == null)
            {
                UnityEngine.Debug.LogWarning($"⚠ {seat.name} has no PokerPlayerController!");
                skipCount++;
                continue;
            }

            // Assign sounds
            controller.foldSound = foldSound;
            controller.checkSound = checkSound;
            controller.betCallSound = betCallSound;
            controller.raiseSound = raiseSound;

            UnityEngine.Debug.Log($"✓ Assigned sounds to {seat.name}");
            successCount++;
        }

        UnityEngine.Debug.Log("\n====================================");
        UnityEngine.Debug.Log($"✓ COMPLETE!");
        UnityEngine.Debug.Log($"  Assigned: {successCount} players");
        UnityEngine.Debug.Log($"  Skipped: {skipCount} players");
        UnityEngine.Debug.Log("====================================");
        UnityEngine.Debug.Log("\n🔊 All AI players will now make sounds!");
    }

    [ContextMenu("Clear All Player Sounds")]
    public void ClearAllPlayerSounds()
    {
        if (tableManager == null)
        {
            tableManager = FindObjectOfType<TableManager>();
        }

        if (tableManager == null) return;

        foreach (var seat in tableManager.seats)
        {
            if (seat == null) continue;

            PokerPlayerController controller = seat.GetComponent<PokerPlayerController>();
            if (controller != null)
            {
                controller.foldSound = null;
                controller.checkSound = null;
                controller.betCallSound = null;
                controller.raiseSound = null;
            }
        }

        UnityEngine.Debug.Log("Cleared all player sounds");
    }
}
