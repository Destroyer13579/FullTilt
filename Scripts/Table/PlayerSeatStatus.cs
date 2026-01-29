using UnityEngine;

/// <summary>
/// Extension to track if a player is away/disconnected during a hand
/// This prevents players from leaving mid-hand and handles auto-play
/// </summary>
public class PlayerSeatStatus : MonoBehaviour
{
    private PlayerSeat seat;

    [Header("Status")]
    public bool isAway = false;           // Player disconnected/left during hand
    public bool canLeave = true;          // Can player stand up right now?
    public bool isWaitingForNextHand = false;  // Player joined mid-hand, waiting to be dealt in

    void Awake()
    {
        seat = GetComponent<PlayerSeat>();
    }

    /// <summary>
    /// Mark player as away (disconnected during hand)
    /// They'll auto-fold to bets, auto-check otherwise
    /// </summary>
    public void MarkAsAway()
    {
        if (seat != null && seat.IsSeated)
        {
            isAway = true;
            UnityEngine.Debug.Log($"[SeatStatus] ========================================");
            UnityEngine.Debug.Log($"[SeatStatus] {seat.PlayerName} marked as AWAY - will auto-play");
            UnityEngine.Debug.Log($"[SeatStatus] IsSeated: {seat.IsSeated}");
            UnityEngine.Debug.Log($"[SeatStatus] ChipCount: {seat.ChipCount}");

            // Check what's visible
            if (seat.card1Image != null)
                UnityEngine.Debug.Log($"[SeatStatus] Card1 active: {seat.card1Image.gameObject.activeSelf}");
            if (seat.card2Image != null)
                UnityEngine.Debug.Log($"[SeatStatus] Card2 active: {seat.card2Image.gameObject.activeSelf}");

            UnityEngine.Debug.Log($"[SeatStatus] MarkAsAway() called from:");
            UnityEngine.Debug.Log(System.Environment.StackTrace);
            UnityEngine.Debug.Log($"[SeatStatus] ========================================");
        }
    }

    /// <summary>
    /// Player returned to table
    /// </summary>
    public void MarkAsReturned()
    {
        isAway = false;
        UnityEngine.Debug.Log($"[SeatStatus] {seat.PlayerName} returned to table");
    }

    /// <summary>
    /// Prevent player from leaving during a hand
    /// </summary>
    public void LockSeat()
    {
        canLeave = false;
        UnityEngine.Debug.Log($"[SeatStatus] {seat?.PlayerName ?? gameObject.name} seat LOCKED (hand in progress)");
    }

    /// <summary>
    /// Allow player to leave (hand completed)
    /// </summary>
    public void UnlockSeat()
    {
        canLeave = true;

        // If player was away and hand is over, they can now leave
        if (isAway && seat != null && seat.IsSeated)
        {
            UnityEngine.Debug.Log($"[SeatStatus] {seat.PlayerName} can now leave (hand complete, was away)");
            // Optionally auto-remove them here
            // seat.StandUp();
        }
    }

    /// <summary>
    /// Check if player can leave right now
    /// </summary>
    public bool CanPlayerLeave()
    {
        return canLeave;
    }

    /// <summary>
    /// Player wants to leave - handle based on whether hand is in progress
    /// </summary>
    /// <summary>
    /// Player wants to leave - handle based on whether hand is in progress
    /// NOTE: This sets chips to 0 to exclude from next hand.
    /// If your PlayerSeat has a StandUp() or ClearSeat() method, call that instead!
    /// </summary>
    public bool TryLeave()
    {
        if (canLeave)
        {
            // Can leave immediately
            UnityEngine.Debug.Log($"[SeatStatus] {seat?.PlayerName ?? gameObject.name} leaving immediately (no hand in progress)");

            // Clear chips and visuals (IsSeated and PlayerName are read-only)
            // Setting chips to 0 excludes them from next hand
            if (seat != null)
            {
                seat.UpdateChips(0);
                seat.HideCards();
                seat.ClearBet();
            }

            // TODO: If you have a proper unseat method, use it:
            // seat.StandUp();
            // OR tableManager.UnseatPlayer(seat);

            return true; // Successfully left
        }
        else
        {
            // Hand in progress - mark as away
            UnityEngine.Debug.Log($"[SeatStatus] {seat?.PlayerName ?? gameObject.name} cannot leave now - marking as AWAY");
            MarkAsAway();
            return false; // Can't leave yet, marked as away
        }
    }

    /// <summary>
    /// Get auto-action for away player (called by betting system)
    /// </summary>
    public PlayerActionData GetAutoAction(BettingState state)
    {
        if (!isAway) return null;

        string playerName = seat?.PlayerName ?? "Unknown";

        // Auto-check if possible, otherwise auto-fold
        if (state.canCheck)
        {
            UnityEngine.Debug.Log($"[SeatStatus] {playerName} (AWAY) auto-checks");
            return new PlayerActionData(PokerAction.Check, 0, playerName);
        }
        else
        {
            UnityEngine.Debug.Log($"[SeatStatus] {playerName} (AWAY) auto-folds to bet");
            return new PlayerActionData(PokerAction.Fold, 0, playerName);
        }
    }
}
