using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages side pots when players go all-in with different stack sizes
/// Handles pot splitting and eligibility just like Full Tilt Poker
/// </summary>
public class SidePotManager
{
    /// <summary>
    /// Represents a single pot (main or side) with eligible players
    /// </summary>
    public class Pot
    {
        public int amount;                  // Total chips in this pot
        public List<int> eligiblePlayers;   // Seat indices who can win this pot
        public int capPerPlayer;            // Max contribution per player for this pot

        public Pot(int amount, List<int> eligiblePlayers, int capPerPlayer)
        {
            this.amount = amount;
            this.eligiblePlayers = new List<int>(eligiblePlayers);
            this.capPerPlayer = capPerPlayer;
        }

        public override string ToString()
        {
            return $"Pot: ${amount}, Eligible: [{string.Join(",", eligiblePlayers)}], Cap: ${capPerPlayer}";
        }
    }

    private TableManager tableManager;

    public SidePotManager(TableManager tableManager)
    {
        this.tableManager = tableManager;
    }

    /// <summary>
    /// Calculate side pots from player bets
    /// Returns list of pots (main pot first, then side pots in order)
    /// </summary>
    public List<Pot> CalculatePots(Dictionary<int, int> playerBets, List<int> activePlayers, Dictionary<int, bool> hasFolded)
    {
        List<Pot> pots = new List<Pot>();

        // Get players who are in the hand (not folded) with their bets
        var playersInHand = activePlayers
            .Where(i => !hasFolded.ContainsKey(i) || !hasFolded[i])
            .Where(i => playerBets.ContainsKey(i) && playerBets[i] > 0)
            .OrderBy(i => playerBets[i])  // Sort by bet amount (smallest first)
            .ToList();

        if (playersInHand.Count == 0)
        {
            return pots;  // No pots to create
        }

        // Track how much each player has left to contribute
        Dictionary<int, int> remainingBets = new Dictionary<int, int>();
        foreach (int seatIndex in playersInHand)
        {
            remainingBets[seatIndex] = playerBets[seatIndex];
        }

        int previousCap = 0;

        // Create pots for each all-in level
        while (playersInHand.Count > 0)
        {
            // Find the smallest remaining bet
            int smallestBet = remainingBets[playersInHand[0]];
            int capForThisPot = smallestBet - previousCap;

            if (capForThisPot <= 0)
            {
                // Remove players with no remaining contribution
                playersInHand.RemoveAt(0);
                continue;
            }

            // Calculate pot amount (everyone still in contributes up to the cap)
            int potAmount = 0;
            foreach (int seatIndex in playersInHand)
            {
                int contribution = Mathf.Min(capForThisPot, remainingBets[seatIndex]);
                potAmount += contribution;
                remainingBets[seatIndex] -= contribution;
            }

            // Create pot with current eligible players
            Pot pot = new Pot(potAmount, new List<int>(playersInHand), smallestBet);
            pots.Add(pot);

            UnityEngine.Debug.Log($"[SidePot] Created pot #{pots.Count}: ${potAmount}, Eligible: [{string.Join(",", playersInHand.Select(i => tableManager.seats[i].PlayerName))}], Cap: ${smallestBet}");

            previousCap = smallestBet;

            // Remove players who are all-in at this level
            playersInHand.RemoveAll(i => remainingBets[i] == 0);
        }

        return pots;
    }

    /// <summary>
    /// Calculate immediate refunds when someone bets more than anyone can call
    /// Returns dictionary of seat index -> refund amount
    /// </summary>
    public Dictionary<int, int> CalculateRefunds(Dictionary<int, int> playerBets, List<int> activePlayers, Dictionary<int, bool> hasFolded)
    {
        Dictionary<int, int> refunds = new Dictionary<int, int>();

        // Get players in hand (not folded)
        var playersInHand = activePlayers
            .Where(i => !hasFolded.ContainsKey(i) || !hasFolded[i])
            .Where(i => playerBets.ContainsKey(i))
            .ToList();

        if (playersInHand.Count <= 1)
        {
            return refunds;  // No refunds needed
        }

        foreach (int seatIndex in playersInHand)
        {
            int myBet = playerBets[seatIndex];

            // Find the highest bet from OTHER players
            int maxOtherPlayerBet = playersInHand
                .Where(i => i != seatIndex)
                .Select(i => playerBets[i])
                .Max();

            // If I bet more than anyone else can match, refund the excess
            if (myBet > maxOtherPlayerBet)
            {
                int excessBet = myBet - maxOtherPlayerBet;
                refunds[seatIndex] = excessBet;

                UnityEngine.Debug.Log($"[SidePot] {tableManager.seats[seatIndex].PlayerName} bet ${myBet} but max callable is ${maxOtherPlayerBet} - refunding ${excessBet}");
            }
        }

        return refunds;
    }

    /// <summary>
    /// Award pots to winners at showdown
    /// Returns dictionary of seat index -> total winnings
    /// </summary>
    public Dictionary<int, int> AwardPots(List<Pot> pots, Dictionary<int, HandRank> handRanks, List<int> activePlayers)
    {
        Dictionary<int, int> winnings = new Dictionary<int, int>();

        foreach (var pot in pots)
        {
            UnityEngine.Debug.Log($"[SidePot] Awarding pot of ${pot.amount} to eligible players: [{string.Join(",", pot.eligiblePlayers.Select(i => tableManager.seats[i].PlayerName))}]");

            // Find the best hand among eligible players
            var eligibleHandRanks = pot.eligiblePlayers
                .Where(i => handRanks.ContainsKey(i))
                .Select(i => new { SeatIndex = i, Rank = handRanks[i] })
                .ToList();

            if (eligibleHandRanks.Count == 0)
            {
                UnityEngine.Debug.LogWarning($"[SidePot] No eligible players with hand ranks for pot ${pot.amount}");
                continue;
            }

            // Sort by hand rank (best first)
            eligibleHandRanks.Sort((a, b) => -a.Rank.CompareTo(b.Rank));

            // Find all players with the best hand (for split pots)
            HandRank bestRank = eligibleHandRanks[0].Rank;
            var winners = eligibleHandRanks
                .Where(h => h.Rank.CompareTo(bestRank) == 0)
                .Select(h => h.SeatIndex)
                .ToList();

            // Split pot among winners
            int sharePerWinner = pot.amount / winners.Count;
            int remainder = pot.amount % winners.Count;

            foreach (int seatIndex in winners)
            {
                int share = sharePerWinner;

                // Give remainder to first winner (or split evenly if multiple)
                if (remainder > 0)
                {
                    share++;
                    remainder--;
                }

                if (!winnings.ContainsKey(seatIndex))
                {
                    winnings[seatIndex] = 0;
                }
                winnings[seatIndex] += share;

                UnityEngine.Debug.Log($"[SidePot] {tableManager.seats[seatIndex].PlayerName} wins ${share} from pot");
            }
        }

        return winnings;
    }
}
