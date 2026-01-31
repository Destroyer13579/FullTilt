using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Simulates actual poker hands in the lobby with real cards, betting, etc.
/// Each table runs a full simulation that can be loaded when a player joins
/// </summary>
public class LobbyPokerSimulator
{
    // Card deck
    private static readonly string[] suits = { "♠", "♥", "♦", "♣" };
    private static readonly string[] ranks = { "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K", "A" };

    private List<string> deck;
    private System.Random random;

    public LobbyPokerSimulator()
    {
        // ★ Use high-precision time for better randomness
        int seed = System.Guid.NewGuid().GetHashCode();
        random = new System.Random(seed);
        deck = new List<string>();
    }

    /// <summary>
    /// Simulate a complete hand for a table and return the state
    /// </summary>
    public TableState SimulateHand(TableData table, int handNumber, int dealerSeat)
    {
        // Create initial state
        TableState state = new TableState
        {
            tableId = table.TableId,
            handNumber = handNumber,
            dealerButtonSeat = dealerSeat,
            totalPot = 0,
            currentBet = 0,
            boardCards = new List<string>(),
            seats = new List<SeatSnapshot>()
        };

        // Get active players
        List<int> activePlayers = new List<int>();
        for (int i = 0; i < table.SeatedPlayerIds.Count; i++)
        {
            var playerId = table.SeatedPlayerIds[i];
            var aiPlayer = AIPlayerManager.Instance.GetPlayer(playerId);
            if (aiPlayer != null && aiPlayer.ChipsAtTable > 0)
            {
                activePlayers.Add(i);
            }
        }

        if (activePlayers.Count < 2)
        {
            state.currentStreet = "BetweenHands";
            state.bettingComplete = false;
            return state;  // Not enough players
        }

        // Create seats
        for (int i = 0; i < table.MaxPlayers; i++)
        {
            bool isOccupied = i < table.SeatedPlayerIds.Count;
            SeatSnapshot seat = new SeatSnapshot
            {
                seatIndex = i,
                isOccupied = isOccupied,
                hasFolded = false,
                isAllIn = false,
                currentBet = 0
            };

            if (isOccupied)
            {
                var playerId = table.SeatedPlayerIds[i];
                var aiPlayer = AIPlayerManager.Instance.GetPlayer(playerId);
                if (aiPlayer != null)
                {
                    seat.playerName = aiPlayer.PlayerName;
                    seat.chipCount = aiPlayer.ChipsAtTable;
                }
            }

            state.seats.Add(seat);
        }

        // Setup blinds
        state.smallBlindSeat = GetNextActiveSeat(activePlayers, dealerSeat);
        state.bigBlindSeat = GetNextActiveSeat(activePlayers, state.smallBlindSeat);

        int smallBlind = table.Stakes.SmallBlind;
        int bigBlind = table.Stakes.BigBlind;

        // Post blinds
        PostBlind(state, state.smallBlindSeat, smallBlind);
        PostBlind(state, state.bigBlindSeat, bigBlind);
        state.currentBet = bigBlind;

        // Shuffle and deal hole cards
        ShuffleDeck();
        DealHoleCards(state, activePlayers);

        // Randomly pick a street to stop at (simulate being mid-hand)
        float progress = (float)random.NextDouble();  // Use our System.Random instance

        if (progress < 0.05f)
        {
            // PreFlop only (5% - between hands)
            state.currentStreet = "PreFlop";
            SimulatePreFlopBetting(state, activePlayers, bigBlind);
            state.bettingComplete = true;  // Betting done, ready to deal flop
        }
        else if (progress < 0.40f)
        {
            // At Flop (35% - 3 cards)
            SimulatePreFlopBetting(state, activePlayers, bigBlind);

            // ★ Check if enough players remain after PreFlop
            int playersRemaining = CountActivePlayers(state, activePlayers);
            if (playersRemaining < 2)
            {
                // Hand over - only 1 player left, stop at PreFlop
                state.currentStreet = "PreFlop";
                state.bettingComplete = true;
                return state;
            }

            DealFlop(state);
            state.currentStreet = "Flop";
            SimulateFlopBetting(state, activePlayers);
            state.bettingComplete = true;  // Betting done, ready to deal turn
        }
        else if (progress < 0.70f)
        {
            // At Turn
            SimulatePreFlopBetting(state, activePlayers, bigBlind);

            // ★ Check after PreFlop
            int playersAfterPreFlop = CountActivePlayers(state, activePlayers);
            if (playersAfterPreFlop < 2)
            {
                state.currentStreet = "PreFlop";
                state.bettingComplete = true;
                return state;
            }

            DealFlop(state);
            SimulateFlopBetting(state, activePlayers);

            // ★ Check after Flop
            int playersAfterFlop = CountActivePlayers(state, activePlayers);
            if (playersAfterFlop < 2)
            {
                state.currentStreet = "Flop";
                state.bettingComplete = true;
                return state;
            }

            DealTurn(state);
            state.currentStreet = "Turn";
            SimulateTurnBetting(state, activePlayers);
            state.bettingComplete = true;  // Betting done, ready to deal river
        }
        else
        {
            // At River
            SimulatePreFlopBetting(state, activePlayers, bigBlind);

            // ★ Check after PreFlop
            int playersAfterPreFlop = CountActivePlayers(state, activePlayers);
            if (playersAfterPreFlop < 2)
            {
                state.currentStreet = "PreFlop";
                state.bettingComplete = true;
                return state;
            }

            DealFlop(state);
            SimulateFlopBetting(state, activePlayers);

            // ★ Check after Flop
            int playersAfterFlop = CountActivePlayers(state, activePlayers);
            if (playersAfterFlop < 2)
            {
                state.currentStreet = "Flop";
                state.bettingComplete = true;
                return state;
            }

            DealTurn(state);
            SimulateTurnBetting(state, activePlayers);

            // ★ Check after Turn
            int playersAfterTurn = CountActivePlayers(state, activePlayers);
            if (playersAfterTurn < 2)
            {
                state.currentStreet = "Turn";
                state.bettingComplete = true;
                return state;
            }

            DealRiver(state);
            state.currentStreet = "River";
            SimulateRiverBetting(state, activePlayers);
            state.bettingComplete = true;  // Betting done, ready for showdown
        }

        return state;
    }

    private void ShuffleDeck()
    {
        deck.Clear();
        foreach (var suit in suits)
        {
            foreach (var rank in ranks)
            {
                deck.Add(rank + suit);
            }
        }

        // Fisher-Yates shuffle
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            string temp = deck[i];
            deck[i] = deck[j];
            deck[j] = temp;
        }
    }

    private void DealHoleCards(TableState state, List<int> activePlayers)
    {
        foreach (int seatIndex in activePlayers)
        {
            var seat = state.seats[seatIndex];
            if (!seat.hasFolded && seat.chipCount > 0)
            {
                seat.holeCards.Add(deck[0]);
                deck.RemoveAt(0);
                seat.holeCards.Add(deck[0]);
                deck.RemoveAt(0);
            }
        }
    }

    private void DealFlop(TableState state)
    {
        deck.RemoveAt(0);  // Burn card
        state.boardCards.Add(deck[0]);
        deck.RemoveAt(0);
        state.boardCards.Add(deck[0]);
        deck.RemoveAt(0);
        state.boardCards.Add(deck[0]);
        deck.RemoveAt(0);
    }

    private void DealTurn(TableState state)
    {
        deck.RemoveAt(0);  // Burn card
        state.boardCards.Add(deck[0]);
        deck.RemoveAt(0);
    }

    private void DealRiver(TableState state)
    {
        deck.RemoveAt(0);  // Burn card
        state.boardCards.Add(deck[0]);
        deck.RemoveAt(0);
    }

    private void PostBlind(TableState state, int seatIndex, int amount)
    {
        var seat = state.seats[seatIndex];
        int actualAmount = Mathf.Min(amount, seat.chipCount);
        seat.currentBet = actualAmount;
        seat.chipCount -= actualAmount;
        state.totalPot += actualAmount;

        if (seat.chipCount == 0)
        {
            seat.isAllIn = true;
        }
    }

    private void SimulatePreFlopBetting(TableState state, List<int> activePlayers, int bigBlind)
    {
        // Simple simulation: random calls/folds/raises
        foreach (int seatIndex in activePlayers)
        {
            var seat = state.seats[seatIndex];
            if (seat.currentBet < state.currentBet && !seat.hasFolded && !seat.isAllIn)
            {
                float action = (float)random.NextDouble();  // Use our System.Random
                if (action < 0.6f)
                {
                    // Call
                    int callAmount = state.currentBet - seat.currentBet;
                    int actualAmount = Mathf.Min(callAmount, seat.chipCount);
                    seat.currentBet += actualAmount;
                    seat.chipCount -= actualAmount;
                    state.totalPot += actualAmount;
                    if (seat.chipCount == 0) seat.isAllIn = true;
                }
                else if (action < 0.8f)
                {
                    // Fold
                    seat.hasFolded = true;
                }
                else
                {
                    // Raise
                    int raiseAmount = bigBlind * random.Next(2, 5);  // Use our System.Random
                    int actualAmount = Mathf.Min(raiseAmount, seat.chipCount);
                    seat.currentBet += actualAmount;
                    seat.chipCount -= actualAmount;
                    state.totalPot += actualAmount;
                    state.currentBet = seat.currentBet;
                    if (seat.chipCount == 0) seat.isAllIn = true;
                }
            }
        }
    }

    private void SimulateFlopBetting(TableState state, List<int> activePlayers)
    {
        // Reset bets for new street
        foreach (var seat in state.seats)
        {
            seat.currentBet = 0;
        }
        state.currentBet = 0;

        // Simulate betting
        foreach (int seatIndex in activePlayers)
        {
            var seat = state.seats[seatIndex];
            if (!seat.hasFolded && !seat.isAllIn)
            {
                float action = (float)random.NextDouble();  // Use our System.Random
                if (action < 0.4f)
                {
                    // Check/Call
                    if (state.currentBet > 0)
                    {
                        int callAmount = state.currentBet - seat.currentBet;
                        int actualAmount = Mathf.Min(callAmount, seat.chipCount);
                        seat.currentBet += actualAmount;
                        seat.chipCount -= actualAmount;
                        state.totalPot += actualAmount;
                        if (seat.chipCount == 0) seat.isAllIn = true;
                    }
                }
                else if (action < 0.7f)
                {
                    // Fold (if facing bet)
                    if (state.currentBet > 0)
                    {
                        seat.hasFolded = true;
                    }
                }
                else
                {
                    // Bet/Raise
                    int betAmount = state.totalPot / 2;
                    int actualAmount = Mathf.Min(betAmount, seat.chipCount);
                    seat.currentBet += actualAmount;
                    seat.chipCount -= actualAmount;
                    state.totalPot += actualAmount;
                    state.currentBet = seat.currentBet;
                    if (seat.chipCount == 0) seat.isAllIn = true;
                }
            }
        }
    }

    private void SimulateTurnBetting(TableState state, List<int> activePlayers)
    {
        SimulateFlopBetting(state, activePlayers);  // Same logic
    }

    private void SimulateRiverBetting(TableState state, List<int> activePlayers)
    {
        SimulateFlopBetting(state, activePlayers);  // Same logic
    }

    private int GetNextActiveSeat(List<int> activePlayers, int currentSeat)
    {
        int index = activePlayers.IndexOf(currentSeat);
        if (index < 0) return activePlayers[0];
        return activePlayers[(index + 1) % activePlayers.Count];
    }

    /// <summary>
    /// Count how many players are still active (not folded) in the hand
    /// </summary>
    private int CountActivePlayers(TableState state, List<int> activePlayers)
    {
        int count = 0;
        foreach (int seatIndex in activePlayers)
        {
            var seat = state.seats[seatIndex];
            if (!seat.hasFolded && seat.chipCount > 0)
            {
                count++;
            }
        }
        return count;
    }
}
