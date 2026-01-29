using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages AI player lifecycle in persistent server mode
/// Handles:
/// - Dynamic join/leave decisions
/// - Stake progression (moving to higher/lower tables as bankroll changes)
/// - Avatar switching
/// - Session length simulation
/// </summary>
public class AILifecycleManager : MonoBehaviour
{
    [Header("Server Mode")]
    [Tooltip("Enable persistent server behavior (don't unseat AIs on load)")]
    public bool isServerMode = true;

    [Header("Session Settings")]
    [Tooltip("Average session length in minutes")]
    public float averageSessionLength = 45f;

    [Tooltip("Session length variance (±)")]
    public float sessionLengthVariance = 20f;

    [Header("Lifecycle Timers")]
    [Tooltip("How often to check for leave decisions (seconds)")]
    public float leaveCheckInterval = 60f;

    [Tooltip("How often to check for stake progression (seconds)")]
    public float stakeCheckInterval = 120f;

    [Tooltip("How often to switch avatars (seconds)")]
    public float avatarSwitchInterval = 300f;

    [Header("Leave Probability")]
    [Tooltip("Base chance per check that a player will leave")]
    [Range(0f, 1f)]
    public float baseLeaveChance = 0.05f;

    [Tooltip("Chance increases if lost more than this % of buy-in")]
    [Range(0f, 1f)]
    public float bigLossThreshold = 0.5f;

    [Header("Stake Progression")]
    [Tooltip("Move up if bankroll is this many buy-ins above current stake")]
    public int moveUpThreshold = 150;

    [Tooltip("Move down if bankroll is below this many buy-ins")]
    public int moveDownThreshold = 40;

    [Header("Avatar Switching")]
    [Tooltip("Chance per check that an unseated player switches avatar")]
    [Range(0f, 1f)]
    public float avatarSwitchChance = 0.15f;

    [Header("Debug")]
    public bool enableDebugLogs = true;

    // Track session times for each AI
    private Dictionary<string, float> playerSessionTimes = new Dictionary<string, float>();
    private Dictionary<string, float> playerTargetSessions = new Dictionary<string, float>();

    private static AILifecycleManager _instance;
    public static AILifecycleManager Instance
    {
        get { return _instance; }
    }

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (!isServerMode)
        {
            Log("Lifecycle Manager disabled (not in server mode)");
            return;
        }

        Log("=== AI LIFECYCLE MANAGER STARTED ===");
        Log("Server Mode: ENABLED");

        // Start periodic checks
        StartCoroutine(LeaveCheckRoutine());
        StartCoroutine(StakeProgressionRoutine());
        StartCoroutine(AvatarSwitchRoutine());

        Log("All lifecycle routines started");
    }

    /// <summary>
    /// Call this when server starts to preserve AI table positions
    /// </summary>
    public void InitializeServerMode()
    {
        if (!isServerMode) return;

        Log("Initializing server mode - preserving AI table positions");

        // Initialize session times for currently seated players
        foreach (var player in AIPlayerManager.Instance.AllPlayers)
        {
            if (!string.IsNullOrEmpty(player.CurrentTableId))
            {
                // Player is already seated - give them a random session time
                if (!playerSessionTimes.ContainsKey(player.PlayerId))
                {
                    playerSessionTimes[player.PlayerId] = UnityEngine.Random.Range(0f, averageSessionLength * 60f);
                    playerTargetSessions[player.PlayerId] = GenerateSessionLength();

                    Log($"  {player.PlayerName} already seated - session {playerSessionTimes[player.PlayerId] / 60f:F1}/{playerTargetSessions[player.PlayerId] / 60f:F1} min");
                }
            }
        }
    }

    // === LEAVE DECISION SYSTEM ===

    IEnumerator LeaveCheckRoutine()
    {
        while (isServerMode)
        {
            yield return new WaitForSeconds(leaveCheckInterval);
            ProcessLeaveDecisions();
        }
    }

    void ProcessLeaveDecisions()
    {
        var seatedPlayers = AIPlayerManager.Instance.AllPlayers
            .Where(p => !string.IsNullOrEmpty(p.CurrentTableId))
            .ToList();

        if (seatedPlayers.Count == 0) return;

        Log($"[Leave Check] Checking {seatedPlayers.Count} seated players");

        int leftCount = 0;

        foreach (var player in seatedPlayers)
        {
            // Update session time
            if (!playerSessionTimes.ContainsKey(player.PlayerId))
            {
                playerSessionTimes[player.PlayerId] = 0f;
                playerTargetSessions[player.PlayerId] = GenerateSessionLength();
            }

            playerSessionTimes[player.PlayerId] += leaveCheckInterval;

            // Check if should leave
            if (ShouldPlayerLeave(player))
            {
                LeaveTable(player);
                leftCount++;
            }
        }

        if (leftCount > 0)
        {
            Log($"[Leave Check] {leftCount} players left tables");
        }
    }

    bool ShouldPlayerLeave(AIPlayer player)
    {
        // 1. Broke players leave
        if (player.ChipsAtTable == 0)
        {
            Log($"{player.PlayerName} leaving (broke)");
            return true;
        }

        // 2. Session time exceeded
        float sessionTime = playerSessionTimes[player.PlayerId];
        float targetSession = playerTargetSessions[player.PlayerId];

        if (sessionTime >= targetSession)
        {
            Log($"{player.PlayerName} leaving (session complete: {sessionTime / 60f:F1}/{targetSession / 60f:F1} min)");
            return true;
        }

        // 3. Big losses (psychological quit)
        // Estimate original buy-in as 2x current chips if lost a lot
        if (player.ChipsAtTable < 1000 && UnityEngine.Random.value < baseLeaveChance * 2f)
        {
            Log($"{player.PlayerName} leaving (low chips)");
            return true;
        }

        // 4. Random leave (simulate getting bored/tired)
        float sessionProgress = sessionTime / targetSession;
        float leaveChance = baseLeaveChance * sessionProgress; // More likely to leave as session goes on

        if (UnityEngine.Random.value < leaveChance)
        {
            Log($"{player.PlayerName} leaving (random: {sessionProgress * 100:F0}% through session)");
            return true;
        }

        return false;
    }

    void LeaveTable(AIPlayer player)
    {
        Log($"[Leave] {player.PlayerName} leaving table {player.CurrentTableId}");

        // Update AI player state (returns chips to bankroll)
        player.LeaveTable();

        // Clear session tracking
        playerSessionTimes.Remove(player.PlayerId);
        playerTargetSessions.Remove(player.PlayerId);

        // Save state
        AIPlayerManager.Instance.SavePlayers();
    }

    float GenerateSessionLength()
    {
        // Generate session length in seconds
        float avgSeconds = averageSessionLength * 60f;
        float variance = sessionLengthVariance * 60f;
        return UnityEngine.Random.Range(avgSeconds - variance, avgSeconds + variance);
    }

    // === STAKE PROGRESSION SYSTEM ===

    IEnumerator StakeProgressionRoutine()
    {
        while (isServerMode)
        {
            yield return new WaitForSeconds(stakeCheckInterval);
            ProcessStakeProgression();
        }
    }

    void ProcessStakeProgression()
    {
        var seatedPlayers = AIPlayerManager.Instance.AllPlayers
            .Where(p => !string.IsNullOrEmpty(p.CurrentTableId))
            .ToList();

        if (seatedPlayers.Count == 0) return;

        Log($"[Stake Check] Checking {seatedPlayers.Count} players for stake progression");

        int movedUp = 0;
        int movedDown = 0;

        foreach (var player in seatedPlayers)
        {
            // Get table info from registry
            TableState tableState = null;
            PokerTableInfo tableInfo = null;

            if (TableRegistry.Instance != null)
            {
                tableState = TableRegistry.Instance.GetTableState(player.CurrentTableId);
                tableInfo = TableRegistry.Instance.GetTableInfo(player.CurrentTableId);
            }

            if (tableInfo == null) continue;

            StakeLevel currentStakes = tableInfo.stake;
            int currentTier = player.GetBestTableTier();

            // Calculate buy-ins at current stake
            int buyIns = player.Bankroll / currentStakes.MaxBuyIn;

            // Should move up?
            if (buyIns > moveUpThreshold && currentTier < 6)
            {
                Log($"{player.PlayerName} moving UP (bankroll: ${player.Bankroll:#,0}, {buyIns} buy-ins)");
                MoveToHigherStake(player);
                movedUp++;
            }
            // Should move down?
            else if (buyIns < moveDownThreshold && currentTier > 0)
            {
                Log($"{player.PlayerName} moving DOWN (bankroll: ${player.Bankroll:#,0}, {buyIns} buy-ins)");
                MoveToLowerStake(player);
                movedDown++;
            }
        }

        if (movedUp > 0 || movedDown > 0)
        {
            Log($"[Stake Check] Moved {movedUp} up, {movedDown} down");
        }
    }

    void MoveToHigherStake(AIPlayer player)
    {
        // Leave current table
        LeaveTable(player);

        // Wait a moment (simulate thinking)
        StartCoroutine(RejoinAtNewStake(player, moveUp: true));
    }

    void MoveToLowerStake(AIPlayer player)
    {
        // Leave current table
        LeaveTable(player);

        // Wait a moment
        StartCoroutine(RejoinAtNewStake(player, moveUp: false));
    }

    IEnumerator RejoinAtNewStake(AIPlayer player, bool moveUp)
    {
        yield return new WaitForSeconds(UnityEngine.Random.Range(2f, 5f));

        // Find appropriate table
        int targetTier = player.GetBestTableTier();

        // Find a table at this tier with space from lobby
        var lobbyManager = FindObjectOfType<LobbyManager>();
        if (lobbyManager != null)
        {
            // Use lobby's table population logic
            Log($"  {player.PlayerName} will rejoin via lobby at tier {targetTier}");
        }
        else
        {
            Log($"  No lobby found to handle rejoin for {player.PlayerName}");
        }
    }

    int GetStakeTier(StakeLevel stakes)
    {
        if (stakes.BigBlind >= 1000000) return 6;
        if (stakes.BigBlind >= 200000) return 5;
        if (stakes.BigBlind >= 100000) return 4;
        if (stakes.BigBlind >= 20000) return 3;
        if (stakes.BigBlind >= 10000) return 2;
        if (stakes.BigBlind >= 1000) return 1;
        return 0;
    }

    // === AVATAR SWITCHING SYSTEM ===

    IEnumerator AvatarSwitchRoutine()
    {
        while (isServerMode)
        {
            yield return new WaitForSeconds(avatarSwitchInterval);
            ProcessAvatarSwitching();
        }
    }

    void ProcessAvatarSwitching()
    {
        var unseatedPlayers = AIPlayerManager.Instance.AllPlayers
            .Where(p => string.IsNullOrEmpty(p.CurrentTableId))
            .ToList();

        if (unseatedPlayers.Count == 0) return;

        int switchedCount = 0;

        foreach (var player in unseatedPlayers)
        {
            if (UnityEngine.Random.value < avatarSwitchChance)
            {
                int oldAvatar = player.AvatarId;
                int newAvatar = UnityEngine.Random.Range(0, AIPlayerManager.Instance.TotalAvatars);

                // Make sure it's actually different
                int attempts = 0;
                while (newAvatar == oldAvatar && attempts < 10)
                {
                    newAvatar = UnityEngine.Random.Range(0, AIPlayerManager.Instance.TotalAvatars);
                    attempts++;
                }

                if (newAvatar != oldAvatar)
                {
                    player.AvatarId = newAvatar;
                    switchedCount++;
                    Log($"[Avatar] {player.PlayerName} switched avatar {oldAvatar} → {newAvatar}");
                }
            }
        }

        if (switchedCount > 0)
        {
            Log($"[Avatar] {switchedCount} players switched avatars");
            AIPlayerManager.Instance.SavePlayers();
        }
    }

    // === PUBLIC API ===

    /// <summary>
    /// Force a player to join a specific table (used by dynamic seat filling)
    /// </summary>
    public void JoinTable(AIPlayer player, string tableId, int buyInAmount)
    {
        // Start session tracking
        playerSessionTimes[player.PlayerId] = 0f;
        playerTargetSessions[player.PlayerId] = GenerateSessionLength();

        Log($"[Join] {player.PlayerName} joined table {tableId} with ${buyInAmount:#,0}");
    }

    /// <summary>
    /// Get stats for debugging
    /// </summary>
    public string GetLifecycleStats()
    {
        var seatedPlayers = AIPlayerManager.Instance.AllPlayers
            .Where(p => !string.IsNullOrEmpty(p.CurrentTableId))
            .ToList();

        int totalPlayers = AIPlayerManager.Instance.AllPlayers.Count;
        int unseated = totalPlayers - seatedPlayers.Count;

        float avgSessionProgress = 0f;
        if (seatedPlayers.Count > 0)
        {
            avgSessionProgress = seatedPlayers
                .Where(p => playerSessionTimes.ContainsKey(p.PlayerId) && playerTargetSessions.ContainsKey(p.PlayerId))
                .Average(p => playerSessionTimes[p.PlayerId] / playerTargetSessions[p.PlayerId]);
        }

        return $"AI Lifecycle: {seatedPlayers.Count} seated, {unseated} unseated, avg session: {avgSessionProgress * 100:F0}%";
    }

    void Log(string message)
    {
        if (enableDebugLogs)
        {
            UnityEngine.Debug.Log($"[AILifecycle] {message}");
        }
    }
}
