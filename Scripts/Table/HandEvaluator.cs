using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// TEXAS HOLD'EM HAND RANKINGS (highest to lowest):
/// 
/// 1. ROYAL FLUSH     - A, K, Q, J, 10 of the same suit
///                      The unbeatable hand. All royal flushes are equal.
///                      
/// 2. STRAIGHT FLUSH  - 5 consecutive cards of the same suit
///                      Ranked by highest card. A-2-3-4-5 (steel wheel) is lowest.
///                      
/// 3. FOUR OF A KIND  - 4 cards of the same rank (quads)
///                      Ranked by quad rank, then kicker.
///                      Example: KKKK2 beats QQQQA
///                      
/// 4. FULL HOUSE      - 3 of a kind + a pair (boat)
///                      Ranked by trips first, then pair.
///                      Example: 33322 beats 222AA
///                      
/// 5. FLUSH           - 5 cards of the same suit
///                      Ranked by highest card, then next highest, etc.
///                      Example: AJ852 of hearts beats KQJ109 of spades
///                      
/// 6. STRAIGHT        - 5 consecutive cards of any suit
///                      Ranked by highest card. A-2-3-4-5 (wheel) is lowest.
///                      A can be high (AKQJT) or low (A2345) but not wrap (QKA23).
///                      
/// 7. THREE OF A KIND - 3 cards of the same rank (trips/set)
///                      Ranked by trip rank, then kickers.
///                      Example: 777AK beats 666AK
///                      
/// 8. TWO PAIR        - 2 different pairs
///                      Ranked by high pair, then low pair, then kicker.
///                      Example: AAKK2 beats AAQQ3
///                      
/// 9. ONE PAIR        - 2 cards of the same rank
///                      Ranked by pair rank, then kickers (3 kickers).
///                      Example: AA543 beats AAK32 (same pair, compare kickers)
///                      
/// 10. HIGH CARD      - No made hand
///                      Ranked by highest card, then next highest, etc.
///                      Example: AK842 beats AK832
/// 
/// TIE-BREAKER RULES:
/// - Suits never break ties in Texas Hold'em
/// - If hands are identical in rank and all kickers, it's a split pot
/// - Players make best 5-card hand from 7 cards (2 hole + 5 community)
/// </summary>
public enum HandRank
{
    HighCard = 0,
    OnePair = 1,
    TwoPair = 2,
    ThreeOfAKind = 3,
    Straight = 4,
    Flush = 5,
    FullHouse = 6,
    FourOfAKind = 7,
    StraightFlush = 8,
    RoyalFlush = 9
}

/// <summary>
/// Represents an evaluated poker hand with rank and comparison values
/// </summary>
[System.Serializable]
public class EvaluatedHand : IComparable<EvaluatedHand>
{
    public HandRank Rank;
    public List<int> CompareValues;      // All values needed for comparison (in order)
    public List<Card> BestFiveCards;     // The 5 cards that make this hand
    public string Description;           // Human-readable description

    public EvaluatedHand()
    {
        CompareValues = new List<int>();
        BestFiveCards = new List<Card>();
    }

    /// <summary>
    /// Compare two hands. Returns:
    /// > 0 if this hand wins
    /// < 0 if other hand wins  
    /// = 0 if exact tie (split pot)
    /// </summary>
    public int CompareTo(EvaluatedHand other)
    {
        if (other == null) return 1;

        // First compare hand rank
        if (Rank != other.Rank)
            return Rank.CompareTo(other.Rank);

        // Same rank - compare values in order
        for (int i = 0; i < CompareValues.Count && i < other.CompareValues.Count; i++)
        {
            if (CompareValues[i] != other.CompareValues[i])
                return CompareValues[i].CompareTo(other.CompareValues[i]);
        }

        // Exact tie
        return 0;
    }

    public bool Beats(EvaluatedHand other) => CompareTo(other) > 0;
    public bool Ties(EvaluatedHand other) => CompareTo(other) == 0;
    public bool LosesTo(EvaluatedHand other) => CompareTo(other) < 0;
}

/// <summary>
/// Texas Hold'em Hand Evaluator
/// </summary>
public static class HandEvaluator
{
    /// <summary>
    /// Evaluates the best 5-card hand from hole cards + community cards
    /// </summary>
    public static EvaluatedHand EvaluateBestHand(List<Card> holeCards, List<Card> communityCards)
    {
        if (holeCards == null || communityCards == null)
        {
            UnityEngine.Debug.LogError("Null cards passed to EvaluateBestHand");
            return null;
        }

        // Combine all available cards (2 hole + up to 5 community = 7 max)
        List<Card> allCards = new List<Card>();
        allCards.AddRange(holeCards);
        allCards.AddRange(communityCards);

        if (allCards.Count < 5)
        {
            UnityEngine.Debug.LogWarning($"Not enough cards to evaluate: {allCards.Count}");
            return null;
        }

        // Generate all possible 5-card combinations
        List<List<Card>> combinations = GetCombinations(allCards, 5);

        // Find the best hand
        EvaluatedHand bestHand = null;
        foreach (var combo in combinations)
        {
            EvaluatedHand hand = EvaluateFiveCards(combo);
            if (bestHand == null || hand.CompareTo(bestHand) > 0)
            {
                bestHand = hand;
            }
        }

        return bestHand;
    }

    /// <summary>
    /// Evaluates exactly 5 cards and returns the hand rank
    /// </summary>
    public static EvaluatedHand EvaluateFiveCards(List<Card> cards)
    {
        if (cards.Count != 5)
        {
            UnityEngine.Debug.LogError($"EvaluateFiveCards requires exactly 5 cards, got {cards.Count}");
            return null;
        }

        // Sort cards by rank (highest first)
        List<Card> sorted = cards.OrderByDescending(c => (int)c.rank).ToList();
        
        // Check for flush and straight
        bool isFlush = CheckFlush(sorted);
        bool isStraight = CheckStraight(sorted, out int straightHigh);

        // Group by rank for pair detection
        var groups = sorted.GroupBy(c => c.rank)
                          .OrderByDescending(g => g.Count())
                          .ThenByDescending(g => (int)g.Key)
                          .ToList();

        int[] counts = groups.Select(g => g.Count()).ToArray();

        EvaluatedHand result = new EvaluatedHand { BestFiveCards = sorted };

        // === CHECK HANDS FROM HIGHEST TO LOWEST ===

        // ROYAL FLUSH: A-K-Q-J-10 same suit
        if (isFlush && isStraight && straightHigh == 14)
        {
            result.Rank = HandRank.RoyalFlush;
            result.CompareValues = new List<int> { 14 }; // All royals equal
            result.Description = $"Royal Flush ({sorted[0].suit})";
            return result;
        }

        // STRAIGHT FLUSH: 5 consecutive same suit
        if (isFlush && isStraight)
        {
            result.Rank = HandRank.StraightFlush;
            result.CompareValues = new List<int> { straightHigh };
            result.Description = $"Straight Flush, {GetRankName(straightHigh)} high";
            return result;
        }

        // FOUR OF A KIND: 4 same rank
        if (counts.Length >= 1 && counts[0] == 4)
        {
            int quadRank = (int)groups[0].Key;
            int kicker = (int)groups[1].Key;
            result.Rank = HandRank.FourOfAKind;
            result.CompareValues = new List<int> { quadRank, kicker };
            result.Description = $"Four of a Kind, {GetRankName(quadRank)}s";
            return result;
        }

        // FULL HOUSE: 3 of a kind + pair
        if (counts.Length >= 2 && counts[0] == 3 && counts[1] >= 2)
        {
            int tripRank = (int)groups[0].Key;
            int pairRank = (int)groups[1].Key;
            result.Rank = HandRank.FullHouse;
            result.CompareValues = new List<int> { tripRank, pairRank };
            result.Description = $"Full House, {GetRankName(tripRank)}s full of {GetRankName(pairRank)}s";
            return result;
        }

        // FLUSH: 5 same suit
        if (isFlush)
        {
            result.Rank = HandRank.Flush;
            result.CompareValues = sorted.Select(c => (int)c.rank).ToList();
            result.Description = $"Flush, {GetRankName((int)sorted[0].rank)} high";
            return result;
        }

        // STRAIGHT: 5 consecutive
        if (isStraight)
        {
            result.Rank = HandRank.Straight;
            result.CompareValues = new List<int> { straightHigh };
            result.Description = $"Straight, {GetRankName(straightHigh)} high";
            return result;
        }

        // THREE OF A KIND: 3 same rank
        if (counts.Length >= 1 && counts[0] == 3)
        {
            int tripRank = (int)groups[0].Key;
            List<int> kickers = groups.Skip(1).Take(2).Select(g => (int)g.Key).ToList();
            result.Rank = HandRank.ThreeOfAKind;
            result.CompareValues = new List<int> { tripRank };
            result.CompareValues.AddRange(kickers);
            result.Description = $"Three of a Kind, {GetRankName(tripRank)}s";
            return result;
        }

        // TWO PAIR: 2 different pairs
        if (counts.Length >= 2 && counts[0] == 2 && counts[1] == 2)
        {
            int highPair = (int)groups[0].Key;
            int lowPair = (int)groups[1].Key;
            int kicker = (int)groups[2].Key;
            result.Rank = HandRank.TwoPair;
            result.CompareValues = new List<int> { highPair, lowPair, kicker };
            result.Description = $"Two Pair, {GetRankName(highPair)}s and {GetRankName(lowPair)}s";
            return result;
        }

        // ONE PAIR: 2 same rank
        if (counts.Length >= 1 && counts[0] == 2)
        {
            int pairRank = (int)groups[0].Key;
            List<int> kickers = groups.Skip(1).Take(3).Select(g => (int)g.Key).ToList();
            result.Rank = HandRank.OnePair;
            result.CompareValues = new List<int> { pairRank };
            result.CompareValues.AddRange(kickers);
            result.Description = $"Pair of {GetRankName(pairRank)}s";
            return result;
        }

        // HIGH CARD: Nothing
        result.Rank = HandRank.HighCard;
        result.CompareValues = sorted.Select(c => (int)c.rank).ToList();
        result.Description = $"High Card, {GetRankName((int)sorted[0].rank)}";
        return result;
    }

    /// <summary>
    /// Check if all 5 cards are the same suit
    /// </summary>
    private static bool CheckFlush(List<Card> cards)
    {
        return cards.All(c => c.suit == cards[0].suit);
    }

    /// <summary>
    /// Check if cards form a straight. Handles ace-low (wheel).
    /// Returns the high card value via out parameter.
    /// </summary>
    private static bool CheckStraight(List<Card> cards, out int highCard)
    {
        List<int> values = cards.Select(c => (int)c.rank).Distinct().OrderByDescending(v => v).ToList();
        highCard = values[0];

        // Need exactly 5 unique values
        if (values.Count != 5)
            return false;

        // Normal straight: difference of 4 between high and low
        if (values[0] - values[4] == 4)
            return true;

        // Wheel (A-2-3-4-5): Ace=14, so check for 14,5,4,3,2
        if (values[0] == 14 && values[1] == 5 && values[2] == 4 && values[3] == 3 && values[4] == 2)
        {
            highCard = 5; // 5 is high in a wheel
            return true;
        }

        return false;
    }

    /// <summary>
    /// Generate all k-combinations from a list
    /// </summary>
    private static List<List<Card>> GetCombinations(List<Card> cards, int k)
    {
        List<List<Card>> result = new List<List<Card>>();
        GenerateCombos(cards, k, 0, new List<Card>(), result);
        return result;
    }

    private static void GenerateCombos(List<Card> cards, int k, int start, List<Card> current, List<List<Card>> result)
    {
        if (current.Count == k)
        {
            result.Add(new List<Card>(current));
            return;
        }

        for (int i = start; i < cards.Count; i++)
        {
            current.Add(cards[i]);
            GenerateCombos(cards, k, i + 1, current, result);
            current.RemoveAt(current.Count - 1);
        }
    }

    /// <summary>
    /// Determines winner(s) from multiple hands. Returns list of seat indices.
    /// Multiple winners = split pot.
    /// </summary>
    public static List<int> DetermineWinners(Dictionary<int, EvaluatedHand> playerHands)
    {
        if (playerHands == null || playerHands.Count == 0)
            return new List<int>();

        List<int> winners = new List<int>();
        EvaluatedHand bestHand = null;

        foreach (var kvp in playerHands)
        {
            if (kvp.Value == null) continue;

            if (bestHand == null)
            {
                bestHand = kvp.Value;
                winners.Add(kvp.Key);
            }
            else
            {
                int cmp = kvp.Value.CompareTo(bestHand);
                if (cmp > 0)
                {
                    // New best hand
                    bestHand = kvp.Value;
                    winners.Clear();
                    winners.Add(kvp.Key);
                }
                else if (cmp == 0)
                {
                    // Tie - split pot
                    winners.Add(kvp.Key);
                }
            }
        }

        return winners;
    }

    /// <summary>
    /// Get human-readable rank name
    /// </summary>
    private static string GetRankName(int rank)
    {
        switch (rank)
        {
            case 2: return "Two";
            case 3: return "Three";
            case 4: return "Four";
            case 5: return "Five";
            case 6: return "Six";
            case 7: return "Seven";
            case 8: return "Eight";
            case 9: return "Nine";
            case 10: return "Ten";
            case 11: return "Jack";
            case 12: return "Queen";
            case 13: return "King";
            case 14: return "Ace";
            default: return rank.ToString();
        }
    }
}

// ============================================================================
// UNIT TESTS - Attach to a GameObject and use Context Menu to run tests
// ============================================================================
public class HandEvaluatorTests : MonoBehaviour
{
    [ContextMenu("Run All Hand Tests")]
    public void RunAllTests()
    {
        int passed = 0;
        int failed = 0;

        // Test each hand type
        if (TestRoyalFlush()) passed++; else failed++;
        if (TestStraightFlush()) passed++; else failed++;
        if (TestFourOfAKind()) passed++; else failed++;
        if (TestFullHouse()) passed++; else failed++;
        if (TestFlush()) passed++; else failed++;
        if (TestStraight()) passed++; else failed++;
        if (TestWheel()) passed++; else failed++;
        if (TestThreeOfAKind()) passed++; else failed++;
        if (TestTwoPair()) passed++; else failed++;
        if (TestOnePair()) passed++; else failed++;
        if (TestHighCard()) passed++; else failed++;

        // Test comparisons
        if (TestFullHouseBeatsFlush()) passed++; else failed++;
        if (TestHigherPairWins()) passed++; else failed++;
        if (TestKickerMatters()) passed++; else failed++;
        if (TestSplitPot()) passed++; else failed++;
        if (TestBestHandFrom7Cards()) passed++; else failed++;

        UnityEngine.Debug.Log($"=== TEST RESULTS: {passed} passed, {failed} failed ===");
    }

    bool TestRoyalFlush()
    {
        var cards = MakeCards("As Ks Qs Js Ts");
        var hand = HandEvaluator.EvaluateFiveCards(cards);
        bool pass = hand.Rank == HandRank.RoyalFlush;
        UnityEngine.Debug.Log($"Royal Flush: {(pass ? "PASS" : "FAIL")} - {hand.Description}");
        return pass;
    }

    bool TestStraightFlush()
    {
        var cards = MakeCards("9h 8h 7h 6h 5h");
        var hand = HandEvaluator.EvaluateFiveCards(cards);
        bool pass = hand.Rank == HandRank.StraightFlush && hand.CompareValues[0] == 9;
        UnityEngine.Debug.Log($"Straight Flush: {(pass ? "PASS" : "FAIL")} - {hand.Description}");
        return pass;
    }

    bool TestFourOfAKind()
    {
        var cards = MakeCards("Kc Kd Kh Ks 2d");
        var hand = HandEvaluator.EvaluateFiveCards(cards);
        bool pass = hand.Rank == HandRank.FourOfAKind && hand.CompareValues[0] == 13;
        UnityEngine.Debug.Log($"Four of a Kind: {(pass ? "PASS" : "FAIL")} - {hand.Description}");
        return pass;
    }

    bool TestFullHouse()
    {
        var cards = MakeCards("Qc Qd Qh Jc Jd");
        var hand = HandEvaluator.EvaluateFiveCards(cards);
        bool pass = hand.Rank == HandRank.FullHouse && hand.CompareValues[0] == 12 && hand.CompareValues[1] == 11;
        UnityEngine.Debug.Log($"Full House: {(pass ? "PASS" : "FAIL")} - {hand.Description}");
        return pass;
    }

    bool TestFlush()
    {
        var cards = MakeCards("Ad Jd 8d 5d 2d");
        var hand = HandEvaluator.EvaluateFiveCards(cards);
        bool pass = hand.Rank == HandRank.Flush && hand.CompareValues[0] == 14;
        UnityEngine.Debug.Log($"Flush: {(pass ? "PASS" : "FAIL")} - {hand.Description}");
        return pass;
    }

    bool TestStraight()
    {
        var cards = MakeCards("Ts 9h 8d 7c 6s");
        var hand = HandEvaluator.EvaluateFiveCards(cards);
        bool pass = hand.Rank == HandRank.Straight && hand.CompareValues[0] == 10;
        UnityEngine.Debug.Log($"Straight: {(pass ? "PASS" : "FAIL")} - {hand.Description}");
        return pass;
    }

    bool TestWheel()
    {
        var cards = MakeCards("As 2h 3d 4c 5s");
        var hand = HandEvaluator.EvaluateFiveCards(cards);
        bool pass = hand.Rank == HandRank.Straight && hand.CompareValues[0] == 5; // 5 is high in wheel
        UnityEngine.Debug.Log($"Wheel (A-5 Straight): {(pass ? "PASS" : "FAIL")} - {hand.Description}");
        return pass;
    }

    bool TestThreeOfAKind()
    {
        var cards = MakeCards("7c 7d 7h Kc 2s");
        var hand = HandEvaluator.EvaluateFiveCards(cards);
        bool pass = hand.Rank == HandRank.ThreeOfAKind && hand.CompareValues[0] == 7;
        UnityEngine.Debug.Log($"Three of a Kind: {(pass ? "PASS" : "FAIL")} - {hand.Description}");
        return pass;
    }

    bool TestTwoPair()
    {
        var cards = MakeCards("Jc Jd 4h 4c As");
        var hand = HandEvaluator.EvaluateFiveCards(cards);
        bool pass = hand.Rank == HandRank.TwoPair && hand.CompareValues[0] == 11 && hand.CompareValues[1] == 4;
        UnityEngine.Debug.Log($"Two Pair: {(pass ? "PASS" : "FAIL")} - {hand.Description}");
        return pass;
    }

    bool TestOnePair()
    {
        var cards = MakeCards("Tc Td Ah 8c 3s");
        var hand = HandEvaluator.EvaluateFiveCards(cards);
        bool pass = hand.Rank == HandRank.OnePair && hand.CompareValues[0] == 10;
        UnityEngine.Debug.Log($"One Pair: {(pass ? "PASS" : "FAIL")} - {hand.Description}");
        return pass;
    }

    bool TestHighCard()
    {
        var cards = MakeCards("Ac Qd 9h 6c 3s");
        var hand = HandEvaluator.EvaluateFiveCards(cards);
        bool pass = hand.Rank == HandRank.HighCard && hand.CompareValues[0] == 14;
        UnityEngine.Debug.Log($"High Card: {(pass ? "PASS" : "FAIL")} - {hand.Description}");
        return pass;
    }

    bool TestFullHouseBeatsFlush()
    {
        var fullHouse = HandEvaluator.EvaluateFiveCards(MakeCards("3c 3d 3h 2c 2d"));
        var flush = HandEvaluator.EvaluateFiveCards(MakeCards("Ad Kd Qd Jd 9d"));
        bool pass = fullHouse.CompareTo(flush) > 0;
        UnityEngine.Debug.Log($"Full House beats Flush: {(pass ? "PASS" : "FAIL")}");
        return pass;
    }

    bool TestHigherPairWins()
    {
        var pairAces = HandEvaluator.EvaluateFiveCards(MakeCards("Ac Ad Kh Qc Js"));
        var pairKings = HandEvaluator.EvaluateFiveCards(MakeCards("Kc Kd Ah Qc Js"));
        bool pass = pairAces.CompareTo(pairKings) > 0;
        UnityEngine.Debug.Log($"Pair of Aces beats Pair of Kings: {(pass ? "PASS" : "FAIL")}");
        return pass;
    }

    bool TestKickerMatters()
    {
        var pairAcesKingKicker = HandEvaluator.EvaluateFiveCards(MakeCards("Ac Ad Kh Qc Js"));
        var pairAcesQueenKicker = HandEvaluator.EvaluateFiveCards(MakeCards("As Ah Qh Jc Ts"));
        bool pass = pairAcesKingKicker.CompareTo(pairAcesQueenKicker) > 0;
        UnityEngine.Debug.Log($"Same pair, King kicker beats Queen kicker: {(pass ? "PASS" : "FAIL")}");
        return pass;
    }

    bool TestSplitPot()
    {
        var hand1 = HandEvaluator.EvaluateFiveCards(MakeCards("Ac Ad Kh Qc Js"));
        var hand2 = HandEvaluator.EvaluateFiveCards(MakeCards("As Ah Kc Qd Jc"));
        bool pass = hand1.CompareTo(hand2) == 0;
        UnityEngine.Debug.Log($"Identical hands split pot: {(pass ? "PASS" : "FAIL")}");
        return pass;
    }

    bool TestBestHandFrom7Cards()
    {
        // Hole cards: Ah Kh
        // Community: Qh Jh Th 2c 3d
        // Best hand should be Royal Flush
        var holeCards = MakeCards("Ah Kh");
        var community = MakeCards("Qh Jh Th 2c 3d");
        var hand = HandEvaluator.EvaluateBestHand(holeCards, community);
        bool pass = hand.Rank == HandRank.RoyalFlush;
        UnityEngine.Debug.Log($"Best hand from 7 cards (Royal Flush): {(pass ? "PASS" : "FAIL")} - {hand.Description}");
        return pass;
    }

    // Helper to create cards from shorthand (e.g., "As Kh Qd Jc Ts")
    List<Card> MakeCards(string shorthand)
    {
        List<Card> cards = new List<Card>();
        string[] parts = shorthand.Split(' ');
        
        foreach (string p in parts)
        {
            if (p.Length < 2) continue;
            
            Rank rank;
            switch (p[0])
            {
                case 'A': rank = Rank.Ace; break;
                case 'K': rank = Rank.King; break;
                case 'Q': rank = Rank.Queen; break;
                case 'J': rank = Rank.Jack; break;
                case 'T': rank = Rank.Ten; break;
                case '9': rank = Rank.Nine; break;
                case '8': rank = Rank.Eight; break;
                case '7': rank = Rank.Seven; break;
                case '6': rank = Rank.Six; break;
                case '5': rank = Rank.Five; break;
                case '4': rank = Rank.Four; break;
                case '3': rank = Rank.Three; break;
                case '2': rank = Rank.Two; break;
                default: continue;
            }

            Suit suit;
            switch (p[1])
            {
                case 's': suit = Suit.Spades; break;
                case 'h': suit = Suit.Hearts; break;
                case 'd': suit = Suit.Diamonds; break;
                case 'c': suit = Suit.Clubs; break;
                default: continue;
            }

            cards.Add(new Card(suit, rank));
        }

        return cards;
    }
}
