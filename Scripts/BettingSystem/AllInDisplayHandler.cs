using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Updates chip count display to show "ALL-IN" when player is all-in
/// Attach this to each PlayerSeat or run from PokerGameManager
/// </summary>
public class AllInDisplayHandler : MonoBehaviour
{
    /// <summary>
    /// Update a player's chip display to show "ALL-IN" if they're all-in, "SITTING OUT" if broke
    /// Call this whenever chips change or after dealing cards
    /// </summary>
    public static void UpdateChipDisplay(PlayerSeat seat, TextMeshProUGUI chipText, bool handInProgress = true)
    {
        if (seat == null || chipText == null) return;

        // ★ CRITICAL: Don't override if displaying an action (WINNER, FOLD, etc.)
        // Check if text is currently showing an action message
        string currentText = chipText.text.ToUpper();
        if (currentText == "WINNER" || currentText == "FOLD" || currentText == "CHECK" ||
            currentText == "CALL" || currentText == "RAISE" || currentText == "BET")
        {
            // Action is being displayed - don't override it!
            return;
        }

        // If player has 0 chips
        if (seat.ChipCount == 0)
        {
            if (handInProgress && seat.IsSeated)
            {
                // During hand: Player is all-in
                chipText.text = "ALL-IN";
                chipText.color = Color.blue; // Blue color for all-in
                chipText.enableAutoSizing = false; // Disable auto-sizing
                // Font size will use whatever is set in the TextMeshPro component
                UnityEngine.Debug.Log($"[AllIn] {seat.PlayerName} is ALL-IN");
            }
            else
            {
                // After hand or not in hand: Player is sitting out
                chipText.text = "SITTING OUT";
                chipText.color = Color.gray;

                // Enable auto-sizing to fit the text
                chipText.enableAutoSizing = true;
                chipText.fontSizeMin = 8;   // Minimum font size
                chipText.fontSizeMax = 24;  // Maximum font size (adjust if needed)

                UnityEngine.Debug.Log($"[ChipDisplay] {seat.PlayerName} is sitting out");
            }
        }
        else
        {
            // Normal chip display with comma formatting
            chipText.text = $"${seat.ChipCount:#,0}";
            chipText.color = Color.white; // Normal color
            chipText.enableAutoSizing = false; // Disable auto-sizing for normal display
        }
    }

    /// <summary>
    /// Update all players' chip displays at once
    /// Call this from PokerGameManager after each action or before showdown
    /// </summary>
    public static void UpdateAllChipDisplays(TableManager tableManager, bool handInProgress = true, List<int> activePlayers = null)
    {
        if (tableManager == null) return;

        for (int i = 0; i < tableManager.seats.Count; i++)
        {
            var seat = tableManager.seats[i];
            if (seat != null)
            {
                // If activePlayers list provided, check if this player is in the hand
                bool isInHand = (activePlayers == null) || activePlayers.Contains(i);

                // If player is NOT in the hand, they should show "SITTING OUT" not "ALL-IN"
                bool effectiveHandInProgress = handInProgress && isInHand;

                // Use extension method which searches for the TextMeshProUGUI
                seat.UpdateChipDisplay(effectiveHandInProgress);
            }
        }
    }
}

/// <summary>
/// Extension methods to easily call from PlayerSeat
/// </summary>
public static class PlayerSeatAllInExtensions
{
    /// <summary>
    /// Check if this player is all-in (0 chips but still in hand)
    /// </summary>
    public static bool IsAllIn(this PlayerSeat seat)
    {
        return seat != null && seat.IsSeated && seat.ChipCount == 0;
    }

    /// <summary>
    /// Update this seat's chip display (shows "ALL-IN" if all-in)
    /// Searches for TextMeshProUGUI in children to update
    /// </summary>
    public static void UpdateChipDisplay(this PlayerSeat seat, bool handInProgress = true)
    {
        if (seat == null) return;

        // Try to find chip text component in children
        // This searches for ANY TextMeshProUGUI component in the seat's children
        var chipTexts = seat.GetComponentsInChildren<TextMeshProUGUI>();

        // Find the one that displays chip count (usually contains "$" or numbers)
        // ★ BUT SKIP any text in BetChips (that's for showing bet amounts, not player chips!)
        TextMeshProUGUI chipText = null;
        foreach (var text in chipTexts)
        {
            // ★ Skip if this text is in BetChips hierarchy (bet amount text)
            Transform parent = text.transform.parent;
            bool isInBetChips = false;
            while (parent != null)
            {
                if (parent.name == "BetChips")
                {
                    isInBetChips = true;
                    break;
                }
                parent = parent.parent;
            }

            if (isInBetChips)
            {
                continue; // Skip bet amount text
            }

            // Look for text that looks like a chip count (player's total chips)
            if (text.text.Contains("$") || text.text == "ALL-IN" || text.text == "SITTING OUT" ||
                (int.TryParse(text.text, out _)))
            {
                chipText = text;
                break;
            }
        }

        // If we found it, update it
        if (chipText != null)
        {
            AllInDisplayHandler.UpdateChipDisplay(seat, chipText, handInProgress);
        }
    }
}
