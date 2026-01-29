using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// AI Player playstyle enum
/// </summary>
public enum AIPlaystyle
{
    Tight,
    Loose,
    Aggressive,
    Passive,
    Balanced,
    TightAggressive,
    LooseAggressive,
    TightPassive,
    LoosePassive,
    Maniac
}

[Serializable]
public class AIPlayer
{
    public string PlayerId;
    public string PlayerName;
    public int Bankroll;  // Total money they have
    public int StartingBankroll;
    public int AvatarId;

    // Table state
    public string CurrentTableId;  // null if not seated
    public int ChipsAtTable;  // Chips they have at their current table
    public bool IsSittingOut;  // Are they sitting out?

    // Stats
    public int HandsPlayed;
    public int HandsWon;
    public float VPIP;
    public float PFR;

    // Behavior
    public AIPlaystyle Playstyle;
    public float SkillLevel;

    // ★ NEW: Avatar locking
    public bool AvatarLocked => !string.IsNullOrEmpty(CurrentTableId);  // Can't change avatar while seated

    public AIPlayer(string name, int bankroll, int avatarId = 0)
    {
        PlayerId = Guid.NewGuid().ToString();
        PlayerName = name;
        Bankroll = bankroll;
        StartingBankroll = bankroll;
        AvatarId = avatarId;
        CurrentTableId = null;
        ChipsAtTable = 0;
        IsSittingOut = false;
        HandsPlayed = 0;
        HandsWon = 0;
        VPIP = 0;
        PFR = 0;
        Playstyle = (AIPlaystyle)UnityEngine.Random.Range(0, 10);  // ★ 10 playstyles now
        SkillLevel = UnityEngine.Random.Range(0.2f, 1.0f);
    }

    public int TotalChips => Bankroll + ChipsAtTable;

    public bool CanAffordTable(StakeLevel stakes)
    {
        return Bankroll >= stakes.MaxBuyIn;  // ★ Changed to MaxBuyIn (they always buy max)
    }

    /// <summary>
    /// AI players ALWAYS buy in for the maximum
    /// </summary>
    public int GetBuyInAmount(StakeLevel stakes)
    {
        return Mathf.Min(stakes.MaxBuyIn, Bankroll);
    }

    /// <summary>
    /// Sit at a table with specified buy-in amount
    /// </summary>
    public void SitAtTable(string tableId, int buyInAmount)
    {
        if (string.IsNullOrEmpty(CurrentTableId))
        {
            CurrentTableId = tableId;
            ChipsAtTable = Mathf.Min(buyInAmount, Bankroll);
            Bankroll -= ChipsAtTable;
            IsSittingOut = false;

            UnityEngine.Debug.Log($"[AIPlayer] {PlayerName} sat at table {tableId} with ${ChipsAtTable:#,0}");
        }
    }

    /// <summary>
    /// Update chips at table (called during gameplay)
    /// </summary>
    public void UpdateChips(int newChipAmount)
    {
        ChipsAtTable = newChipAmount;

        // If broke, mark as sitting out
        if (ChipsAtTable == 0)
        {
            IsSittingOut = true;
        }
    }

    /// <summary>
    /// Should this AI leave the table?
    /// </summary>
    public bool ShouldLeaveTable()
    {
        // Leave if broke and sitting out
        return ChipsAtTable == 0 && IsSittingOut;
    }

    /// <summary>
    /// Leave the table and return chips to bankroll
    /// </summary>
    public void LeaveTable()
    {
        if (!string.IsNullOrEmpty(CurrentTableId))
        {
            Bankroll += ChipsAtTable;
            ChipsAtTable = 0;
            CurrentTableId = null;
            IsSittingOut = false;

            // ★ Reset bankroll to minimum if broke
            if (Bankroll < 1000)
            {
                UnityEngine.Debug.Log($"[AIPlayer] {PlayerName} broke! Resetting bankroll from ${Bankroll} to $1,000");
                Bankroll = 1000;
            }
        }
    }

    /// <summary>
    /// Get the best table this AI can afford
    /// Returns stake level index (0-6)
    /// </summary>
    public int GetBestTableTier()
    {
        // Tier by bankroll (they play where they can afford ~100 buy-ins)
        if (Bankroll >= 1000000) return 6;  // $500k/$1M stakes
        if (Bankroll >= 200000) return 5;   // $100k/$200k stakes
        if (Bankroll >= 100000) return 4;   // $50k/$100k stakes
        if (Bankroll >= 20000) return 3;    // $10k/$20k stakes
        if (Bankroll >= 5000) return 2;     // $5k/$10k stakes
        if (Bankroll >= 2000) return 1;     // $500/$1k stakes or $100/$200k
        return 0;  // Micro stakes
    }
}

/// <summary>
/// Enhanced AI Player Manager with:
/// - Persistent save/load
/// - Smart table selection based on bankroll
/// - Dynamic seat filling
/// - Bankroll management
/// </summary>
public class AIPlayerManager
{
    private static AIPlayerManager _instance;
    public static AIPlayerManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = new AIPlayerManager();
            return _instance;
        }
    }

    public List<AIPlayer> AllPlayers = new List<AIPlayer>();
    public int TotalPlayersOnline => AllPlayers.FindAll(p => !string.IsNullOrEmpty(p.CurrentTableId)).Count;

    // Avatar settings
    public int TotalAvatars = 6;  // Set this to match your avatar database count!

    // ★ Save file path
    private string SavePath => Path.Combine(Application.persistentDataPath, "ai_players.json");

    private static string[] FirstNames = {
        "Phil", "Daniel", "Doyle", "Johnny", "Chris", "Tom", "Mike", "Dave",
        "Erik", "Gus", "Patrik", "Viktor", "Fedor", "Jason", "Antonio",
        "Sam", "Vanessa", "Jennifer", "Liv", "Maria", "Kathy", "Annie",
        "Alex", "Jordan", "Taylor", "Casey", "Morgan", "Riley", "Quinn",
        "Jake", "Nick", "Matt", "Ryan", "Steve", "John", "Bob", "Bill",
        "Tony", "Scotty", "Barry", "Elky", "Durrrr", "Isildur", "Jungleman"
    };

    private static string[] LastNames = {
        "Ivey", "Negreanu", "Brunson", "Chan", "Moneymaker", "Dwan", "Antonius",
        "Hansen", "Blom", "Holz", "Mercier", "Esfandiari", "Hellmuth", "Seidel",
        "Smith", "Johnson", "Williams", "Brown", "Jones", "Miller", "Davis",
        "TheKid", "AllIn", "Bluff", "River", "Ace", "King", "Shark", "Fish",
        "Wilson", "Moore", "Taylor", "Anderson", "Thomas", "Jackson", "White"
    };

    private static string[] Suffixes = {
        "", "99", "2000", "X", "Pro", "Jr", "III", "_FTP", "21", "88", "777",
        "23", "42", "69", "420", "V", "IV", "Sr", "FTW", "GG", "EZ"
    };

    /// <summary>
    /// Generate or load AI players
    /// </summary>
    public void Initialize()
    {
        UnityEngine.Debug.LogError("===== AIPlayerManager.Initialize() CALLED =====");
        UnityEngine.Debug.LogError($"AllPlayers count BEFORE: {AllPlayers.Count}");

        // Try to load saved players first
        if (LoadPlayers())
        {
            UnityEngine.Debug.LogError($"✓ LoadPlayers() returned TRUE - Loaded {AllPlayers.Count} players");

            // ★ CRITICAL AUTO-DETECT: Check if players are TOO POOR
            // If richest player has < $100k, the save file is BROKEN
            int richestBankroll = AllPlayers.Count > 0 ? AllPlayers.Max(p => p.Bankroll) : 0;

            if (richestBankroll < 100000)
            {
                // Save file is BROKEN! Everyone is poor!
                UnityEngine.Debug.LogError($"⚠️⚠️⚠️ CRITICAL: Richest player only has ${richestBankroll:#,0}!");
                UnityEngine.Debug.LogError($"⚠️⚠️⚠️ Save file is corrupted or old!");
                UnityEngine.Debug.LogError($"⚠️⚠️⚠️ FORCE REGENERATING with proper bankrolls...");

                ForceRegenerate();
                return;
            }

            // ★ AUTO-DETECT OLD SAVE FILES
            // Check if whales have old small bankrolls
            var whales = AllPlayers.Where(p => p.Bankroll >= 500000).ToList();
            var richWhales = AllPlayers.Where(p => p.Bankroll >= 2000000).ToList();

            UnityEngine.Debug.LogError($"[Auto-Detect] Whales ($500k+): {whales.Count}, Rich whales ($2M+): {richWhales.Count}, Richest: ${richestBankroll:#,0}");

            if (whales.Count < 5)
            {
                // Not enough whales! Regenerate!
                UnityEngine.Debug.LogError($"⚠️ OLD SAVE FILE DETECTED! Only {whales.Count} whales!");
                UnityEngine.Debug.LogError($"   AUTO-REGENERATING with new whale distribution...");

                ForceRegenerate();
                return;
            }

            // ★ CRITICAL DIAGNOSIS: Check loaded whale bankrolls
            var loadedWhales = AllPlayers.Where(p => p.Bankroll >= 500000).ToList();
            UnityEngine.Debug.Log($"[AIPlayerManager] Loaded {loadedWhales.Count} whales from save:");
            foreach (var whale in loadedWhales.Take(5))
            {
                UnityEngine.Debug.Log($"  - {whale.PlayerName}: ${whale.Bankroll:#,0}");
            }

            // Clean up any seated players (tables don't persist between sessions)
            int cleanedUp = 0;
            foreach (var player in AllPlayers)
            {
                if (!string.IsNullOrEmpty(player.CurrentTableId))
                {
                    UnityEngine.Debug.LogError($"[Cleanup] Unseating {player.PlayerName} from table {player.CurrentTableId}");
                    player.LeaveTable();  // Returns chips to bankroll
                    cleanedUp++;
                }
            }

            if (cleanedUp > 0)
            {
                UnityEngine.Debug.LogError($"[AIPlayerManager] ✓ Cleared {cleanedUp} seated players (tables don't persist)");
            }
            else
            {
                UnityEngine.Debug.LogError($"[AIPlayerManager] No seated players to clear");
            }

            // ★ VERIFY cleanup worked
            int stillSeated = AllPlayers.Count(p => !string.IsNullOrEmpty(p.CurrentTableId));
            UnityEngine.Debug.LogError($"[Verify] Players still seated AFTER cleanup: {stillSeated}");
        }
        else
        {
            // Generate new players with varied bankrolls
            UnityEngine.Debug.LogError("[AIPlayerManager] LoadPlayers() returned FALSE - generating new AI players");
            GeneratePlayers(150);  // Create 150 AI players
            SavePlayers();
        }

        UnityEngine.Debug.LogError($"===== Initialize() COMPLETE - AllPlayers: {AllPlayers.Count} =====");
    }

    /// <summary>
    /// EMERGENCY: Force delete save and regenerate
    /// </summary>
    public void ForceRegenerate()
    {
        UnityEngine.Debug.LogError("===== FORCE REGENERATE CALLED =====");

        // Delete save file
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
            UnityEngine.Debug.LogError($"✓ Deleted save file: {SavePath}");
        }

        // Clear and regenerate
        AllPlayers.Clear();
        GeneratePlayers(150);
        SavePlayers();

        UnityEngine.Debug.LogError($"✓ Generated {AllPlayers.Count} fresh players");
    }

    /// <summary>
    /// Generate AI players with varied bankrolls
    /// </summary>
    public void GeneratePlayers(int count)
    {
        AllPlayers.Clear();
        HashSet<string> usedNames = new HashSet<string>();

        // ★ Create 15 "whales" with huge bankrolls first
        for (int i = 0; i < 15; i++)
        {
            string name = GenerateUniqueName(usedNames);
            int avatarId = i % TotalAvatars;
            int bankroll = UnityEngine.Random.Range(2000000, 10000000);  // ★ $2M - $10M (was $500k-$2M)
            AllPlayers.Add(new AIPlayer(name, bankroll, avatarId));
            UnityEngine.Debug.Log($"[AIPlayerManager] Created whale: {name} with ${bankroll:#,0}");
        }

        // ★ Create varied bankroll distribution for remaining players
        int remaining = count - 15;

        // 30% micro stakes ($1k - $5k)
        int microCount = (int)(remaining * 0.30f);
        for (int i = 0; i < microCount; i++)
        {
            string name = GenerateUniqueName(usedNames);
            int avatarId = (i + 15) % TotalAvatars;
            int bankroll = UnityEngine.Random.Range(1000, 5000);
            AllPlayers.Add(new AIPlayer(name, bankroll, avatarId));
        }

        // 30% low stakes ($5k - $20k)
        int lowCount = (int)(remaining * 0.30f);
        for (int i = 0; i < lowCount; i++)
        {
            string name = GenerateUniqueName(usedNames);
            int avatarId = (i + 15 + microCount) % TotalAvatars;
            int bankroll = UnityEngine.Random.Range(5000, 20000);
            AllPlayers.Add(new AIPlayer(name, bankroll, avatarId));
        }

        // 20% mid stakes ($20k - $100k)
        int midCount = (int)(remaining * 0.20f);
        for (int i = 0; i < midCount; i++)
        {
            string name = GenerateUniqueName(usedNames);
            int avatarId = (i + 15 + microCount + lowCount) % TotalAvatars;
            int bankroll = UnityEngine.Random.Range(20000, 100000);
            AllPlayers.Add(new AIPlayer(name, bankroll, avatarId));
        }

        // 15% high stakes ($100k - $500k)
        int highCount = (int)(remaining * 0.15f);
        for (int i = 0; i < highCount; i++)
        {
            string name = GenerateUniqueName(usedNames);
            int avatarId = (i + 15 + microCount + lowCount + midCount) % TotalAvatars;
            int bankroll = UnityEngine.Random.Range(100000, 500000);
            AllPlayers.Add(new AIPlayer(name, bankroll, avatarId));
        }

        // Fill remaining with random distribution
        while (AllPlayers.Count < count)
        {
            string name = GenerateUniqueName(usedNames);
            int avatarId = AllPlayers.Count % TotalAvatars;
            int bankroll = UnityEngine.Random.Range(1000, 50000);
            AllPlayers.Add(new AIPlayer(name, bankroll, avatarId));
        }

        UnityEngine.Debug.Log($"[AIPlayerManager] Generated {count} AI players:");
        UnityEngine.Debug.Log($"  - 15 whales ($500k+)");
        UnityEngine.Debug.Log($"  - {microCount} micro ($1k-$5k)");
        UnityEngine.Debug.Log($"  - {lowCount} low ($5k-$20k)");
        UnityEngine.Debug.Log($"  - {midCount} mid ($20k-$100k)");
        UnityEngine.Debug.Log($"  - {highCount} high ($100k-$500k)");
    }

    private string GenerateUniqueName(HashSet<string> usedNames)
    {
        string name;
        int attempts = 0;
        do
        {
            string first = FirstNames[UnityEngine.Random.Range(0, FirstNames.Length)];
            string last = LastNames[UnityEngine.Random.Range(0, LastNames.Length)];
            string suffix = Suffixes[UnityEngine.Random.Range(0, Suffixes.Length)];
            name = $"{first}{last}{suffix}";
            attempts++;
        } while (usedNames.Contains(name) && attempts < 100);

        usedNames.Add(name);
        return name;
    }

    public AIPlayer GetPlayer(string playerId)
    {
        return AllPlayers.Find(p => p.PlayerId == playerId);
    }

    /// <summary>
    /// Get available players for a specific stake level
    /// Prioritizes players with appropriate bankrolls
    /// </summary>
    public List<AIPlayer> GetAvailablePlayers(StakeLevel stakes, int count)
    {
        // Get players who:
        // 1. Aren't currently seated (use string.IsNullOrEmpty for consistency!)
        // 2. Can afford the max buy-in
        var available = AllPlayers.FindAll(p =>
            string.IsNullOrEmpty(p.CurrentTableId) &&  // ★ FIXED!
            p.CanAffordTable(stakes));

        UnityEngine.Debug.LogError($"[GetAvailablePlayers] Stakes: {stakes.SmallBlind}/{stakes.BigBlind}, MaxBuyIn: ${stakes.MaxBuyIn:#,0}");
        UnityEngine.Debug.LogError($"[GetAvailablePlayers] Found {available.Count} players who can afford");

        if (available.Count == 0)
        {
            // DEBUG: Why can't anyone afford?
            int totalAvailable = AllPlayers.Count(p => string.IsNullOrEmpty(p.CurrentTableId));
            int canAfford = AllPlayers.Count(p => string.IsNullOrEmpty(p.CurrentTableId) && p.Bankroll >= stakes.MaxBuyIn);
            UnityEngine.Debug.LogError($"[GetAvailablePlayers] Total unseated: {totalAvailable}, Can afford ${stakes.MaxBuyIn:#,0}: {canAfford}");

            if (canAfford == 0)
            {
                // Show richest players
                var richest = AllPlayers.Where(p => string.IsNullOrEmpty(p.CurrentTableId))
                    .OrderByDescending(p => p.Bankroll)
                    .Take(5)
                    .ToList();
                UnityEngine.Debug.LogError($"[GetAvailablePlayers] Richest available players:");
                foreach (var p in richest)
                {
                    UnityEngine.Debug.LogError($"  - {p.PlayerName}: ${p.Bankroll:#,0} (need ${stakes.MaxBuyIn:#,0})");
                }
            }
        }

        // Sort by how appropriate this stake is for their bankroll
        // Players with ~100x max buy-in are ideal
        int idealBankroll = stakes.MaxBuyIn * 100;
        available.Sort((a, b) => {
            int distA = Mathf.Abs(a.Bankroll - idealBankroll);
            int distB = Mathf.Abs(b.Bankroll - idealBankroll);
            return distA.CompareTo(distB);
        });

        // Add some randomness (shuffle top 50%)
        int shuffleCount = Mathf.Min(available.Count / 2, count * 3);
        for (int i = shuffleCount - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            var temp = available[i];
            available[i] = available[j];
            available[j] = temp;
        }

        return available.GetRange(0, Mathf.Min(count, available.Count));
    }

    /// <summary>
    /// Get players who should leave their tables
    /// </summary>
    public List<AIPlayer> GetPlayersWhoShouldLeave()
    {
        return AllPlayers.FindAll(p =>
            !string.IsNullOrEmpty(p.CurrentTableId) &&
            p.ShouldLeaveTable());
    }

    /// <summary>
    /// Save AI players to disk
    /// </summary>
    public bool SavePlayers()
    {
        try
        {
            var wrapper = new AIPlayerListWrapper { players = AllPlayers };
            string json = JsonUtility.ToJson(wrapper, true);
            File.WriteAllText(SavePath, json);
            UnityEngine.Debug.Log($"[AIPlayerManager] ✓ Saved {AllPlayers.Count} AI players to {SavePath}");
            return true;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[AIPlayerManager] Failed to save: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Load AI players from disk
    /// </summary>
    public bool LoadPlayers()
    {
        try
        {
            if (!File.Exists(SavePath))
                return false;

            string json = File.ReadAllText(SavePath);
            var wrapper = JsonUtility.FromJson<AIPlayerListWrapper>(json);
            AllPlayers = wrapper.players;

            return AllPlayers != null && AllPlayers.Count > 0;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[AIPlayerManager] Failed to load: {e.Message}");
            return false;
        }
    }
}

[Serializable]
public class AIPlayerListWrapper
{
    public List<AIPlayer> players;
}
