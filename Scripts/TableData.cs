using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StakeLevel
{
    public string Name;           // e.g., "$1/$2"
    public int SmallBlind;
    public int BigBlind;
    public int MinBuyIn;          // Usually 20x BB
    public int MaxBuyIn;          // Usually 100x BB

    public StakeLevel(int sb, int bb)
    {
        SmallBlind = sb;
        BigBlind = bb;
        MinBuyIn = bb * 20;

        // ★ REALISTIC MAX BUY-INS FOR ALL STAKES
        // Cap at reasonable amounts based on typical poker room rules
        if (bb >= 500000)
        {
            // Nosebleeds ($500k/$1M): Capped at $2M
            MaxBuyIn = 2000000;
        }
        else if (bb >= 100000)
        {
            // Very high ($100k/$200k): Capped at $1M
            MaxBuyIn = 1000000;
        }
        else if (bb >= 50000)
        {
            // High ($50k/$100k): Capped at $500k
            MaxBuyIn = 500000;
        }
        else if (bb >= 10000)
        {
            // Mid-high ($10k/$20k): Capped at $200k (not 150x BB!)
            MaxBuyIn = 200000;
        }
        else if (bb >= 5000)
        {
            // Mid ($5k/$10k): Capped at $100k (not 150x BB!)
            MaxBuyIn = 100000;
        }
        else if (bb >= 1000)
        {
            // Mid-low ($1k/$3k): 100x BB
            MaxBuyIn = bb * 100;
        }
        else if (bb >= 100)
        {
            // Low ($100/$200): 100x BB
            MaxBuyIn = bb * 100;
        }
        else
        {
            // Micro ($1/$2, $5/$10): 100x BB
            MaxBuyIn = bb * 100;
        }

        // Format name based on size
        if (bb >= 1000)
            Name = $"${sb / 1000}k/${bb / 1000}k";
        else
            Name = $"${sb}/${bb}";
    }
}

[Serializable]
public class TableData
{
    public string TableId;
    public string TableName;
    public StakeLevel Stakes;
    public int MaxPlayers;        // 6 or 9
    public int CurrentPlayers;
    public List<string> SeatedPlayerIds;  // AI player IDs at this table
    public float AveragePot;
    public int HandsPerHour;
    public bool IsActive;

    // For syncing with actual gameplay
    public int CurrentHandNumber;
    public float TimeSinceLastHand;

    public TableData(string name, StakeLevel stakes, int maxPlayers)
    {
        TableId = Guid.NewGuid().ToString();
        TableName = name;
        Stakes = stakes;
        MaxPlayers = maxPlayers;
        CurrentPlayers = 0;
        SeatedPlayerIds = new List<string>();
        AveragePot = 0;
        HandsPerHour = 0;
        IsActive = true;
        CurrentHandNumber = 0;
        TimeSinceLastHand = 0;
    }

    public string GetPlayerCountDisplay()
    {
        return $"{CurrentPlayers}/{MaxPlayers}";
    }

    public string GetTypeDisplay()
    {
        return "NL";  // No Limit - we can expand later for PL, FL
    }

    public string GetAvgPotDisplay()
    {
        if (AveragePot >= 1000)
            return $"${AveragePot / 1000:F1}k";
        return $"${AveragePot:F0}";
    }
}

// Static class to hold all stake levels
public static class StakeLevels
{
    public static List<StakeLevel> All = new List<StakeLevel>
    {
        new StakeLevel(1, 2),
        new StakeLevel(5, 10),
        new StakeLevel(15, 30),
        new StakeLevel(25, 50),
        new StakeLevel(100, 200),
        new StakeLevel(200, 400),
        new StakeLevel(300, 600),
        new StakeLevel(1000, 2000),
        new StakeLevel(1500, 3000),
        new StakeLevel(5000, 10000),
        new StakeLevel(10000, 20000),
        new StakeLevel(50000, 100000),
        new StakeLevel(100000, 200000),
        new StakeLevel(500000, 1000000)
    };

    public static StakeLevel GetByBlinds(int sb, int bb)
    {
        return All.Find(s => s.SmallBlind == sb && s.BigBlind == bb);
    }
}
