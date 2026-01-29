using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines a chip denomination (value and sprite)
/// </summary>
[System.Serializable]
public class ChipDenomination
{
    public int value;           // e.g., 1, 5, 25, 100, 500, 1000
    public Sprite sprite;       // The chip sprite for this denomination
    public string displayName;  // e.g., "White", "Red", "Green", "Black"
}

[CreateAssetMenu(fileName = "ChipSpriteDatabase", menuName = "Poker/Chip Sprite Database")]
public class ChipSpriteDatabase : ScriptableObject
{
    [Header("Chip Denominations")]
    [Tooltip("Define all chip types from smallest to largest value")]
    public List<ChipDenomination> denominations = new List<ChipDenomination>();

    [Header("Display Settings")]
    [Tooltip("Maximum chips to show in a single stack before combining")]
    public int maxChipsPerStack = 5;

    [Tooltip("Should we always show higher denominations when possible?")]
    public bool preferHigherDenominations = true;

    /// <summary>
    /// Breaks down an amount into chip denominations
    /// Returns a list of (chipValue, count) pairs
    /// </summary>
    public List<(int value, int count)> GetChipBreakdown(int amount)
    {
        List<(int value, int count)> breakdown = new List<(int value, int count)>();
        
        if (amount <= 0) return breakdown;

        int remaining = amount;

        // Sort denominations from highest to lowest
        var sortedDenoms = new List<ChipDenomination>(denominations);
        sortedDenoms.Sort((a, b) => b.value.CompareTo(a.value));

        if (preferHigherDenominations)
        {
            // Use highest denominations first
            foreach (var denom in sortedDenoms)
            {
                if (remaining >= denom.value)
                {
                    int count = remaining / denom.value;
                    breakdown.Add((denom.value, count));
                    remaining -= count * denom.value;
                }
            }
        }
        else
        {
            // More realistic breakdown - limit stacks
            foreach (var denom in sortedDenoms)
            {
                if (remaining >= denom.value)
                {
                    int count = Mathf.Min(remaining / denom.value, maxChipsPerStack);
                    breakdown.Add((denom.value, count));
                    remaining -= count * denom.value;
                }
            }
        }

        // If there's still remaining (shouldn't happen with proper denominations), use smallest chip
        if (remaining > 0 && sortedDenoms.Count > 0)
        {
            var smallest = sortedDenoms[sortedDenoms.Count - 1];
            int count = (remaining + smallest.value - 1) / smallest.value; // Round up
            breakdown.Add((smallest.value, count));
        }

        return breakdown;
    }

    /// <summary>
    /// Gets the sprite for a specific chip value
    /// </summary>
    public Sprite GetChipSprite(int value)
    {
        foreach (var denom in denominations)
        {
            if (denom.value == value && denom.sprite != null)
            {
                return denom.sprite;
            }
        }

        UnityEngine.Debug.LogWarning($"No chip sprite found for value ${value}");
        return null;
    }

    /// <summary>
    /// Gets display name for a chip value
    /// </summary>
    public string GetChipName(int value)
    {
        foreach (var denom in denominations)
        {
            if (denom.value == value)
            {
                return string.IsNullOrEmpty(denom.displayName) ? $"${value}" : denom.displayName;
            }
        }
        return $"${value}";
    }

    [ContextMenu("Test - Breakdown $1,267")]
    void TestBreakdown()
    {
        var breakdown = GetChipBreakdown(1267);
        UnityEngine.Debug.Log("=== Breakdown of $1,267 ===");
        foreach (var (value, count) in breakdown)
        {
            UnityEngine.Debug.Log($"{count}x ${value} chip ({GetChipName(value)})");
        }
    }

    [ContextMenu("Test - Breakdown $3")]
    void TestSmallBreakdown()
    {
        var breakdown = GetChipBreakdown(3);
        UnityEngine.Debug.Log("=== Breakdown of $3 ===");
        foreach (var (value, count) in breakdown)
        {
            UnityEngine.Debug.Log($"{count}x ${value} chip ({GetChipName(value)})");
        }
    }
}
