using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages a complete betting round (PreFlop, Flop, Turn, River)
/// Handles turn order, player actions, pot management
/// </summary>
public class BettingRoundManager : MonoBehaviour
{
    [Header("References")]
    public TableManager tableManager;
    public PlayerUIController playerUI;
    public PokerGameManager gameManager;  // For updating pot display in real-time

    [Header("Settings")]
    public float actionDelay = 0.5f;  // Delay between actions

    // Current round state
    private int currentBet;
    private int minimumRaise;
    private int pot;
    private List<int> activePlayers;           // Seat indices still in hand
    private Dictionary<int, int> playerBets;   // How much each player has bet this round
    private Dictionary<int, bool> hasFolded;   // Which players have folded
    private Dictionary<int, bool> hasActed;    // NEW: Which players have acted this round
    private int lastRaiserIndex = -1;          // Track who raised last
    private bool roundComplete = false;

    public int CurrentBet => currentBet;

    /// <summary>
    /// Run a complete betting round
    /// </summary>
    public IEnumerator RunBettingRound(List<int> players, int startingPlayerIndex, int initialPot, int bigBlindAmount, Dictionary<int, int> existingBets = null)
    {
        UnityEngine.Debug.Log($"=== Starting Betting Round ===");

        // Initialize
        activePlayers = new List<int>(players);
        playerBets = new Dictionary<int, int>();
        hasFolded = new Dictionary<int, bool>();
        hasActed = new Dictionary<int, bool>();  // Initialize hasActed
        pot = initialPot;
        // Initialize current bet
        // PreFlop: current bet is BB (from blinds)
        // Postflop: current bet is 0 (fresh betting round)
        if (existingBets != null && existingBets.Count > 0)
        {
            // PreFlop - find highest existing bet (should be BB)
            currentBet = 0;
            foreach (var bet in existingBets.Values)
            {
                if (bet > currentBet)
                    currentBet = bet;
            }
            UnityEngine.Debug.Log($"[Betting] PreFlop - Starting with current bet: ${currentBet} (from blinds)");
        }
        else
        {
            // Postflop - fresh betting round, no bets yet
            currentBet = 0;
            UnityEngine.Debug.Log($"[Betting] Postflop - Starting with current bet: $0 (fresh round)");
        }

        minimumRaise = bigBlindAmount;
        lastRaiserIndex = -1;
        roundComplete = false;

        foreach (int seatIndex in activePlayers)
        {
            // Use existing bets if provided (for blinds), otherwise 0
            playerBets[seatIndex] = (existingBets != null && existingBets.ContainsKey(seatIndex)) ? existingBets[seatIndex] : 0;
            hasFolded[seatIndex] = false;
            hasActed[seatIndex] = false;  // NEW: No one has acted yet
        }

        UnityEngine.Debug.Log($"[Betting] Current bet: ${currentBet}, Pot: ${pot}");
        foreach (var kvp in playerBets)
        {
            if (kvp.Value > 0)
            {
                UnityEngine.Debug.Log($"[Betting] Seat {kvp.Key} already has ${kvp.Value} in pot");
            }
        }

        // CRITICAL: Check if betting should even occur
        // Count players who can still bet (have chips and haven't folded)
        int playersWhoCanBet = 0;
        foreach (int seatIndex in activePlayers)
        {
            if (!hasFolded[seatIndex] && tableManager.seats[seatIndex].ChipCount > 0)
            {
                playersWhoCanBet++;
            }
        }

        UnityEngine.Debug.Log($"[Betting] Players who can bet: {playersWhoCanBet}");

        // If 0-1 players can bet, skip betting entirely (everyone all-in or folded)
        if (playersWhoCanBet <= 1)
        {
            UnityEngine.Debug.Log("[Betting] Everyone is all-in or folded except 0-1 players - SKIPPING BETTING, going to showdown!");

            // Return result immediately - no betting needed
            // Set allInShowdown flag to trigger immediate card reveal
            var noActionResult = new BettingRoundResult
            {
                pot = pot,
                activePlayers = activePlayers.Where(i => !hasFolded[i]).ToList(),
                allInShowdown = true  // Trigger immediate card reveal!
            };

            yield return noActionResult;
            yield break;
        }

        // Determine action order (start after button/blinds)
        int actionIndex = activePlayers.IndexOf(startingPlayerIndex);
        if (actionIndex == -1) actionIndex = 0;

        int actionsThisRound = 0;
        int playersToAct = activePlayers.Count;
        int lastActorSeatIndex = -1; // Track who acted last to detect infinite loops

        // Betting continues until everyone has acted and bet is matched (or only 1 player left)
        while (!roundComplete)
        {
            int seatIndex = activePlayers[actionIndex];
            PlayerSeat seat = tableManager.seats[seatIndex];
            PlayerSeatStatus seatStatus = seat != null ? seat.GetComponent<PlayerSeatStatus>() : null;

            // SAFETY CHECK: Detect if same player is acting twice in a row (infinite loop bug!)
            if (lastActorSeatIndex == seatIndex && actionsThisRound > 0)
            {
                UnityEngine.Debug.LogError($"[Betting] INFINITE LOOP DETECTED! {seat.PlayerName} (seat {seatIndex}) acting twice in a row!");
                UnityEngine.Debug.LogError($"[Betting]   Current bet: ${currentBet}");
                UnityEngine.Debug.LogError($"[Betting]   Player bet: ${playerBets[seatIndex]}");
                UnityEngine.Debug.LogError($"[Betting]   lastRaiserIndex: {lastRaiserIndex}");
                UnityEngine.Debug.LogError($"[Betting]   hasActed: {(hasActed.ContainsKey(seatIndex) ? hasActed[seatIndex].ToString() : "not set")}");
                UnityEngine.Debug.LogError($"[Betting] BREAKING LOOP TO PREVENT FREEZE!");
                roundComplete = true;
                break;
            }

            // Skip if folded, all-in, empty, or waiting for next hand
            if (seat == null || !seat.IsSeated)
            {
                hasFolded[seatIndex] = true;
                hasActed[seatIndex] = true;
                actionIndex = (actionIndex + 1) % activePlayers.Count;
                continue;
            }

            if (seatStatus != null && seatStatus.isWaitingForNextHand)
            {
                UnityEngine.Debug.Log($"[Betting] Seat {seatIndex} waiting for next hand - skipping action");
                hasFolded[seatIndex] = true;
                hasActed[seatIndex] = true;
                actionIndex = (actionIndex + 1) % activePlayers.Count;
                continue;
            }

            if (hasFolded[seatIndex] || seat.ChipCount == 0)
            {
                // Move CLOCKWISE (increment with your array order)
                actionIndex = (actionIndex + 1) % activePlayers.Count;
                continue;
            }

            // Check if this player needs to act
            bool needsToAct = playerBets[seatIndex] < currentBet ||
                              (actionsThisRound < playersToAct); // Everyone acts at least once

            if (!needsToAct)
            {
                // Player doesn't need to act - skip them
                // Move CLOCKWISE (increment with your array order)
                actionIndex = (actionIndex + 1) % activePlayers.Count;

                // Check if round is complete
                if (CheckRoundComplete())
                {
                    roundComplete = true;
                    break;
                }
                continue;
            }

            // Player acts
            UnityEngine.Debug.Log($"[Betting] === {seat.PlayerName}'s turn ===");
            UnityEngine.Debug.Log($"[Betting]   Current bet: ${currentBet}");
            UnityEngine.Debug.Log($"[Betting]   Player bet: ${playerBets[seatIndex]}");
            UnityEngine.Debug.Log($"[Betting]   To call: ${currentBet - playerBets[seatIndex]}");
            UnityEngine.Debug.Log($"[Betting]   Action #{actionsThisRound + 1}");
            UnityEngine.Debug.Log($"[Betting]   lastRaiserIndex: {lastRaiserIndex}");
            UnityEngine.Debug.Log($"[Betting]   hasActed[{seatIndex}]: {(hasActed.ContainsKey(seatIndex) ? hasActed[seatIndex].ToString() : "not set")}");

            int amountToCall = currentBet - playerBets[seatIndex];

            BettingState state = new BettingState(
                amountToCall,           // Amount this player needs to call
                currentBet,             // Actual current bet total
                minimumRaise,           // Minimum raise amount
                pot                     // Current pot
            );

            PlayerActionData action = null;

            // Get PokerPlayerController component (handles both AI and Human)
            PokerPlayerController player = seat.GetComponent<PokerPlayerController>();
            if (player == null)
            {
                UnityEngine.Debug.LogError($"[Betting] No PokerPlayerController component on {seat.PlayerName}!");
                yield break;
            }

            // Show turn indicator
            seat.ShowTurnIndicator();

            // Request action (automatically routes to AI or Human)
            bool actionReceived = false;
            UnityEngine.Debug.Log($"[Betting] Calling RequestAction for {seat.PlayerName}...");

            yield return player.RequestAction(state, playerUI, (a) =>
            {
                UnityEngine.Debug.Log($"[Betting] CALLBACK RECEIVED for {seat.PlayerName}: {a.action}");
                action = a;
                actionReceived = true;
            });

            UnityEngine.Debug.Log($"[Betting] RequestAction returned. ActionReceived: {actionReceived}, Action: {action?.action}");

            if (action == null)
            {
                UnityEngine.Debug.LogError($"[Betting] No action received from {seat.PlayerName}!");
                yield break;
            }

            UnityEngine.Debug.Log($"[Betting] Processing action: {seat.PlayerName} {action.action}");

            // Process action
            ProcessAction(seatIndex, action);

            // Hide turn indicator
            seat.HideTurnIndicator();

            // Mark player as having acted
            hasActed[seatIndex] = true;

            // Update last actor (for infinite loop detection)
            lastActorSeatIndex = seatIndex;

            actionsThisRound++;

            // Check if only 1 player left (everyone else folded)
            int playersRemaining = activePlayers.Count(i => !hasFolded[i]);
            if (playersRemaining <= 1)
            {
                UnityEngine.Debug.Log($"[Betting] Only {playersRemaining} player(s) left - ending round");
                roundComplete = true;
                break;
            }

            // Delay before next action
            yield return new WaitForSeconds(actionDelay);

            // Move to next player CLOCKWISE (increment with your array order)
            actionIndex = (actionIndex + 1) % activePlayers.Count;

            // Check if round is complete
            if (CheckRoundComplete())
            {
                roundComplete = true;
                break;
            }

            // Safety: max 100 actions per round
            if (actionsThisRound > 100)
            {
                UnityEngine.Debug.LogError("[Betting] Too many actions - breaking loop");
                break;
            }
        }

        UnityEngine.Debug.Log($"=== Betting Round Complete - Pot: ${pot} ===");

        // Hide all turn indicators
        foreach (int seatIndex in activePlayers)
        {
            PlayerSeat seat = tableManager.seats[seatIndex];
            if (seat != null)
            {
                seat.HideTurnIndicator();
            }
        }

        // Return final state
        yield return new BettingRoundResult
        {
            pot = pot,
            activePlayers = activePlayers.Where(i => !hasFolded[i]).ToList(),
            allInShowdown = false  // Normal betting, cards stay hidden
        };
    }

    void ProcessAction(int seatIndex, PlayerActionData action)
    {
        PlayerSeat seat = tableManager.seats[seatIndex];

        UnityEngine.Debug.Log($"[Betting] {action}");

        switch (action.action)
        {
            case PokerAction.Fold:
                hasFolded[seatIndex] = true;
                // DON'T clear bet chips - they stay on table until collected to pot!
                // seat.ClearBet();  ← Removed - bets stay visible
                seat.FoldCards(); // Animate cards folding (Full Tilt style)
                seat.ShowAction("FOLD");  // Show "FOLD" in light blue
                UnityEngine.Debug.Log($"[Betting] {seat.PlayerName} folds - bet chips remain on table");
                break;

            case PokerAction.Check:
                // No chips involved
                seat.ShowAction("CHECK");  // Show "CHECK" in light blue
                break;

            case PokerAction.Call:
                int callAmount = currentBet - playerBets[seatIndex];
                callAmount = Mathf.Min(callAmount, seat.ChipCount);

                seat.UpdateChips(seat.ChipCount - callAmount);
                seat.UpdateBet(playerBets[seatIndex] + callAmount);
                playerBets[seatIndex] += callAmount;
                pot += callAmount;

                // Update pot display immediately
                UpdatePotDisplayNow();

                // Show "CALL" action
                seat.ShowAction("CALL");
                break;

            case PokerAction.Bet:
                seat.UpdateChips(seat.ChipCount - action.amount);
                seat.ShowBet(action.amount);
                playerBets[seatIndex] = action.amount;
                currentBet = action.amount;
                minimumRaise = action.amount;
                pot += action.amount;
                lastRaiserIndex = seatIndex;

                // Update pot display immediately
                UpdatePotDisplayNow();

                // Show "BET" action
                seat.ShowAction("BET");

                // Reset hasActed - everyone needs to respond to the bet
                foreach (int playerIndex in activePlayers)
                {
                    if (!hasFolded[playerIndex])
                        hasActed[playerIndex] = false;
                }
                hasActed[seatIndex] = true;  // This player just acted

                UnityEngine.Debug.Log($"[Betting] {seat.PlayerName} bet ${action.amount} - everyone must act again");
                break;

            case PokerAction.Raise:
                int raiseAmount = action.amount;
                int toAdd = raiseAmount - playerBets[seatIndex];

                seat.UpdateChips(seat.ChipCount - toAdd);
                seat.UpdateBet(raiseAmount);
                playerBets[seatIndex] = raiseAmount;

                int raiseIncrease = raiseAmount - currentBet;
                minimumRaise = Mathf.Max(minimumRaise, raiseIncrease);
                currentBet = raiseAmount;
                pot += toAdd;
                lastRaiserIndex = seatIndex;

                // Update pot display immediately
                UpdatePotDisplayNow();

                // Show "RAISE" action
                seat.ShowAction("RAISE");

                // Reset hasActed - everyone needs to respond to the raise
                foreach (int playerIndex in activePlayers)
                {
                    if (!hasFolded[playerIndex])
                        hasActed[playerIndex] = false;
                }
                hasActed[seatIndex] = true;  // This player just acted

                UnityEngine.Debug.Log($"[Betting] {seat.PlayerName} raised to ${raiseAmount} - everyone must act again");
                break;

            case PokerAction.AllIn:
                int allInAmount = seat.ChipCount;
                seat.UpdateChips(0);
                seat.UpdateBet(playerBets[seatIndex] + allInAmount);
                playerBets[seatIndex] += allInAmount;

                if (playerBets[seatIndex] > currentBet)
                {
                    int increase = playerBets[seatIndex] - currentBet;
                    minimumRaise = Mathf.Max(minimumRaise, increase);
                    currentBet = playerBets[seatIndex];
                    lastRaiserIndex = seatIndex;

                    // All-in raised the bet - everyone needs to respond
                    foreach (int playerIndex in activePlayers)
                    {
                        if (!hasFolded[playerIndex])
                            hasActed[playerIndex] = false;
                    }
                    hasActed[seatIndex] = true;  // This player just acted

                    UnityEngine.Debug.Log($"[Betting] {seat.PlayerName} went all-in for ${playerBets[seatIndex]} (raise) - everyone must act again");
                }
                else
                {
                    UnityEngine.Debug.Log($"[Betting] {seat.PlayerName} went all-in for ${playerBets[seatIndex]} (call/under-call)");
                }

                pot += allInAmount;

                // Update pot display immediately
                UpdatePotDisplayNow();

                // Show "ALL IN" action (no hyphen - matches persistent display)
                seat.ShowAction("ALL IN");
                break;
        }

        // Update chip displays for all players (shows "ALL-IN" for 0 chip players IN THE HAND)
        // Only update players who are actually in the hand
        AllInDisplayHandler.UpdateAllChipDisplays(tableManager, handInProgress: true, activePlayers);
    }

    bool CheckRoundComplete()
    {
        // Get players still in hand (not folded)
        // IMPORTANT: Include all-in players (ChipCount == 0) - they're still in the hand!
        var playersInHand = activePlayers
            .Where(i => !hasFolded[i])
            .ToList();

        // Only 0-1 players left (everyone else folded) - round over
        if (playersInHand.Count <= 1)
        {
            UnityEngine.Debug.Log($"[Betting] Only {playersInHand.Count} player(s) left in hand - round complete");
            return true;
        }

        // Get players who can still bet (have chips left)
        var playersWhoCanBet = playersInHand
            .Where(i => tableManager.seats[i].ChipCount > 0)
            .ToList();

        // CRITICAL: Check if everyone has had a chance to act
        // Only check players who can still bet - all-in players don't need to act again
        foreach (int seatIndex in playersWhoCanBet)
        {
            if (!hasActed.ContainsKey(seatIndex) || !hasActed[seatIndex])
            {
                UnityEngine.Debug.Log($"[Betting] Player {seatIndex} ({tableManager.seats[seatIndex].PlayerName}) hasn't acted yet - round not complete");
                return false;  // Someone hasn't acted yet
            }
        }

        // Check if all players who can bet have matched the current bet
        foreach (int seatIndex in playersWhoCanBet)
        {
            if (playerBets[seatIndex] < currentBet)
            {
                UnityEngine.Debug.Log($"[Betting] Player {seatIndex} ({tableManager.seats[seatIndex].PlayerName}) hasn't matched bet (${playerBets[seatIndex]} < ${currentBet}) - round not complete");
                return false;  // Someone still needs to call
            }
        }

        UnityEngine.Debug.Log($"[Betting] Round complete - everyone acted and matched bet of ${currentBet}");
        return true;  // Everyone has acted and matched
    }

    // Public getter for pot
    public int CurrentPot => pot;

    /// <summary>
    /// Update the pot display in real-time (called after each action that changes pot)
    /// </summary>
    void UpdatePotDisplayNow()
    {
        if (gameManager != null)
        {
            gameManager.UpdatePotDisplayFromBetting(pot);
        }
    }
}

/// <summary>
/// Result of a betting round
/// </summary>
public class BettingRoundResult
{
    public int pot;
    public List<int> activePlayers;  // Players still in hand
    public bool allInShowdown;       // True if everyone is all-in (trigger immediate card reveal)
}
