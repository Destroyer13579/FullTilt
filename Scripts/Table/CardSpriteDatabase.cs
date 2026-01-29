using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CardSpriteDatabase", menuName = "Poker/Card Sprite Database")]
public class CardSpriteDatabase : ScriptableObject
{
    [Header("Card Back")]
    public Sprite cardBackSprite;

    [Header("=== EASY SETUP: Drag all 52 sliced sprites here ===")]
    [Tooltip("Select all 52 sliced card sprites and drag them here. They should be in order: Row1=Suit1 (A-K), Row2=Suit2, etc.")]
    public List<Sprite> allCardSprites = new List<Sprite>();

    [Header("Sprite Sheet Layout Settings")]
    [Tooltip("Does the first card start with Ace? (true=A first, false=2 first)")]
    public bool aceFirst = true;

    [Tooltip("After the first card, do ranks go in descending order? (true=A,K,Q,J... false=A,2,3,4...)")]
    public bool descendingRanks = false;

    [Tooltip("Suit order from first row to last (check your sprite sheet)")]
    public Suit row0Suit = Suit.Spades;
    public Suit row1Suit = Suit.Hearts;
    public Suit row2Suit = Suit.Diamonds;
    public Suit row3Suit = Suit.Clubs;

    // Dictionary for name-based lookup
    private Dictionary<string, Sprite> spriteLookup;
    private bool lookupBuilt = false;

    public Sprite GetCardSprite(Card card)
    {
        if (card == null) return cardBackSprite;

        // Use index-based lookup (sprites are named Cards6_0, Cards6_1, etc., not ace_spades)
        int index = GetCardIndex(card);
        if (allCardSprites != null && index >= 0 && index < allCardSprites.Count && allCardSprites[index] != null)
        {
            return allCardSprites[index];
        }

        // Fall back to name-based lookup as last resort
        BuildLookupIfNeeded();

        if (spriteLookup != null)
        {
            string key = GetCardKey(card); // e.g., "ace_spades"

            if (spriteLookup.TryGetValue(key, out Sprite sprite))
            {
                return sprite;
            }
        }

        UnityEngine.Debug.LogWarning($"Card sprite not found for: {card} (index {index})");
        return cardBackSprite;
    }

    private void BuildLookupIfNeeded()
    {
        if (lookupBuilt && spriteLookup != null) return;

        spriteLookup = new Dictionary<string, Sprite>();

        if (allCardSprites != null)
        {
            foreach (var sprite in allCardSprites)
            {
                if (sprite != null)
                {
                    // Add by sprite name (lowercase)
                    spriteLookup[sprite.name.ToLower()] = sprite;
                }
            }
        }

        lookupBuilt = true;
    }

    private string GetCardKey(Card card)
    {
        string rankStr = GetRankString(card.rank);
        string suitStr = card.suit.ToString().ToLower();
        return $"{rankStr}_{suitStr}"; // e.g., "seven_clubs"
    }

    private string GetRankString(Rank rank)
    {
        switch (rank)
        {
            case Rank.Two: return "two";
            case Rank.Three: return "three";
            case Rank.Four: return "four";
            case Rank.Five: return "five";
            case Rank.Six: return "six";
            case Rank.Seven: return "seven";
            case Rank.Eight: return "eight";
            case Rank.Nine: return "nine";
            case Rank.Ten: return "ten";
            case Rank.Jack: return "jack";
            case Rank.Queen: return "queen";
            case Rank.King: return "king";
            case Rank.Ace: return "ace";
            default: return rank.ToString().ToLower();
        }
    }

    private int GetCardIndex(Card card)
    {
        // Find which row this suit is in
        int row = -1;
        if (card.suit == row0Suit) row = 0;
        else if (card.suit == row1Suit) row = 1;
        else if (card.suit == row2Suit) row = 2;
        else if (card.suit == row3Suit) row = 3;

        if (row == -1) return -1;

        // Find column based on rank
        int col;
        if (aceFirst && descendingRanks)
        {
            // A-K-Q-J-10-9-8-7-6-5-4-3-2
            // A=0, K=1, Q=2, J=3, 10=4, 9=5, 8=6, 7=7, 6=8, 5=9, 4=10, 3=11, 2=12
            if (card.rank == Rank.Ace) col = 0;
            else col = 14 - (int)card.rank;
        }
        else if (aceFirst && !descendingRanks)
        {
            // A-2-3-4-5-6-7-8-9-10-J-Q-K
            // A=0, 2=1, 3=2, 4=3, 5=4, 6=5, 7=6, 8=7, 9=8, 10=9, J=10, Q=11, K=12
            if (card.rank == Rank.Ace) col = 0;
            else col = (int)card.rank - 1;
        }
        else if (!aceFirst && descendingRanks)
        {
            // K-Q-J-10-9-8-7-6-5-4-3-2-A
            // K=0, Q=1, J=2, 10=3, 9=4, 8=5, 7=6, 6=7, 5=8, 4=9, 3=10, 2=11, A=12
            if (card.rank == Rank.Ace) col = 12;
            else col = 13 - (int)card.rank;
        }
        else // !aceFirst && !descendingRanks
        {
            // 2-3-4-5-6-7-8-9-10-J-Q-K-A
            // 2=0, 3=1, 4=2, 5=3, 6=4, 7=5, 8=6, 9=7, 10=8, J=9, Q=10, K=11, A=12
            if (card.rank == Rank.Ace) col = 12;
            else col = (int)card.rank - 2;
        }

        return row * 13 + col;
    }

    public Sprite GetCardSprite(Suit suit, Rank rank)
    {
        return GetCardSprite(new Card(suit, rank));
    }

    // Debug helper
    [ContextMenu("Test - Print Card Keys")]
    public void DebugPrintKeys()
    {
        UnityEngine.Debug.Log("=== Card Key Mapping ===");
        foreach (Suit suit in System.Enum.GetValues(typeof(Suit)))
        {
            foreach (Rank rank in System.Enum.GetValues(typeof(Rank)))
            {
                Card card = new Card(suit, rank);
                UnityEngine.Debug.Log($"{card} -> {GetCardKey(card)}");
            }
        }
    }
}
