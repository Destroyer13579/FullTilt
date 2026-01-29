using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages AI players at a poker table
/// TEST MODE: Spawns random AI for testing
/// LOBBY MODE: Disabled - uses AI players from lobby
/// </summary>
public class AITableManager : MonoBehaviour
{
    [Header("References")]
    public TableManager tableManager;
    public AvatarDatabase avatarDatabase;

    [Header("AI Settings")]
    public int minAIPlayers = 2;
    public int maxAIPlayers = 6;
    public float joinCheckInterval = 5f;
    public float leaveCheckInterval = 30f;
    public float initialSpawnDelay = 1f;

    [Header("Buy-In Settings")]
    public int minBuyInMultiplier = 40;
    public int maxBuyInMultiplier = 100;

    [Header("AI Names")]
    public List<string> aiFirstNames = new List<string>
    {
        "Mike", "Phil", "Daniel", "Doyle", "Chris", "Antonio", "Gus", "Tom",
        "Jennifer", "Vanessa", "Liv", "Maria", "Kathy", "Annie", "Victoria",
        "Johnny", "Scotty", "Erik", "Jason", "Barry", "Sam", "Patrik"
    };

    public List<string> aiLastNames = new List<string>
    {
        "Chan", "Hellmuth", "Negreanu", "Brunson", "Moneymaker", "Esfandiari",
        "Hansen", "Dwan", "Tilly", "Selbst", "Boeree", "Ho", "Liebert", "Duke",
        "Coren", "Moss", "Nguyen", "Seidel", "Mercier", "Greenstein", "Farha", "Antonius"
    };

    private List<AIPlayerData> activeAIPlayers = new List<AIPlayerData>();
    private HashSet<string> usedNames = new HashSet<string>();
    private bool isTestMode = true;

    void Start()
    {
            if (tableManager == null)
                tableManager = FindObjectOfType<TableManager>();

            if (avatarDatabase == null && tableManager != null)
                avatarDatabase = tableManager.avatarDatabase;

            // =========================================================
            // CRITICAL CHECK: Are we joining from lobby?
            // Check BEFORE PlayerPrefs might be deleted!
            // =========================================================

            // IMPORTANT: Check immediately on Awake, not Start!
            // But since we're in Start, check if ANY seats are already occupied
            bool seatsAlreadyOccupied = false;
            if (tableManager != null)
            {
                foreach (var seat in tableManager.seats)
                {
                    if (!seat.IsEmpty)
                    {
                        seatsAlreadyOccupied = true;
                        break;
                    }
                }
            }

            if (seatsAlreadyOccupied)
            {
                // Lobby already loaded players - don't spawn test AI!
                isTestMode = false;
                UnityEngine.Debug.Log("========================================");
                UnityEngine.Debug.Log("[AITableManager] LOBBY MODE DETECTED");
                UnityEngine.Debug.Log("[AITableManager] Seats already occupied by lobby players");
                UnityEngine.Debug.Log("[AITableManager] AI spawning DISABLED");
                UnityEngine.Debug.Log("========================================");
                return;
            }

            // TEST MODE - Spawn AI for testing
            isTestMode = true;
            UnityEngine.Debug.Log("========================================");
            UnityEngine.Debug.Log("[AITableManager] TEST MODE");
            UnityEngine.Debug.Log("[AITableManager] Spawning random AI for testing");
            UnityEngine.Debug.Log("========================================");

            StartCoroutine(InitialSpawn());
            StartCoroutine(AIJoinRoutine());
            StartCoroutine(AILeaveRoutine());
    }

    IEnumerator InitialSpawn()
    {
        if (!isTestMode) yield break;

        yield return new WaitForSeconds(initialSpawnDelay);

        int initialCount = UnityEngine.Random.Range(minAIPlayers, maxAIPlayers + 1);
        for (int i = 0; i < initialCount; i++)
        {
            TrySpawnAI();
            yield return new WaitForSeconds(UnityEngine.Random.Range(2f, 4f));
        }
    }

    IEnumerator AIJoinRoutine()
    {
        while (isTestMode)
        {
            yield return new WaitForSeconds(joinCheckInterval);

            if (activeAIPlayers.Count < maxAIPlayers && UnityEngine.Random.value > 0.5f)
            {
                TrySpawnAI();
            }
        }
    }

    IEnumerator AILeaveRoutine()
    {
        while (isTestMode)
        {
            yield return new WaitForSeconds(leaveCheckInterval);

            if (activeAIPlayers.Count > minAIPlayers && UnityEngine.Random.value > 0.7f)
            {
                TryRemoveRandomAI();
            }
        }
    }

    void TrySpawnAI()
    {
        if (!isTestMode) return;
        if (tableManager == null) return;

        int emptySeat = FindEmptySeat();
        if (emptySeat == -1) return;

        string aiName = GenerateUniqueName();
        int avatarId = GetRandomAvatarId();
        int bankroll = GenerateAIBankroll();

        AIPlayerData aiPlayer = new AIPlayerData
        {
            Name = aiName,
            SeatIndex = emptySeat,
            AvatarId = avatarId,
            Chips = 0,
            Bankroll = bankroll
        };

        activeAIPlayers.Add(aiPlayer);
        usedNames.Add(aiName);

        StartCoroutine(AIJoinSequence(aiPlayer));
    }

    IEnumerator AIJoinSequence(AIPlayerData aiPlayer)
    {
        PlayerSeat seat = tableManager.GetSeat(aiPlayer.SeatIndex);
        if (seat == null) yield break;

        seat.ReserveSeat(
            System.Guid.NewGuid().ToString(),
            aiPlayer.Name,
            aiPlayer.AvatarId
        );

        UnityEngine.Debug.Log($"[AITableManager] AI '{aiPlayer.Name}' reserved seat {aiPlayer.SeatIndex}...");

        float thinkTime = UnityEngine.Random.Range(1.5f, 3.5f);
        yield return new WaitForSeconds(thinkTime);

        int maxTableBuyIn = tableManager.bigBlind * maxBuyInMultiplier;
        int minTableBuyIn = tableManager.bigBlind * minBuyInMultiplier;

        int affordableBuyIn = Mathf.Min(aiPlayer.Bankroll, maxTableBuyIn);
        float buyInPercent = UnityEngine.Random.Range(0.90f, 1.0f);
        int buyIn = Mathf.RoundToInt(affordableBuyIn * buyInPercent);
        buyIn = Mathf.Clamp(buyIn, minTableBuyIn, maxTableBuyIn);

        aiPlayer.Chips = buyIn;

        seat.SeatPlayer(
            System.Guid.NewGuid().ToString(),
            aiPlayer.Name,
            buyIn,
            aiPlayer.AvatarId,
            false
        );

        UnityEngine.Debug.Log($"[AITableManager] AI '{aiPlayer.Name}' sat down with ${buyIn}");
    }

    int GenerateAIBankroll()
    {
        int bigBlind = tableManager != null ? tableManager.bigBlind : 2;
        int minBuyIn = bigBlind * minBuyInMultiplier;
        int maxBuyIn = bigBlind * maxBuyInMultiplier;

        float roll = UnityEngine.Random.value;

        if (roll < 0.2f)
            return UnityEngine.Random.Range(minBuyIn, maxBuyIn / 2);
        else if (roll < 0.6f)
            return UnityEngine.Random.Range(maxBuyIn / 2, Mathf.RoundToInt(maxBuyIn * 0.8f));
        else
            return UnityEngine.Random.Range(Mathf.RoundToInt(maxBuyIn * 0.8f), maxBuyIn);
    }

    void TryRemoveRandomAI()
    {
        if (!isTestMode) return;
        if (activeAIPlayers.Count == 0) return;

        int index = UnityEngine.Random.Range(0, activeAIPlayers.Count);
        AIPlayerData aiPlayer = activeAIPlayers[index];

        StartCoroutine(AILeaveSequence(aiPlayer, index));
    }

    IEnumerator AILeaveSequence(AIPlayerData aiPlayer, int listIndex)
    {
        UnityEngine.Debug.Log($"[AITableManager] AI '{aiPlayer.Name}' is leaving the table...");

        yield return new WaitForSeconds(UnityEngine.Random.Range(0.5f, 1.5f));

        tableManager.RemovePlayer(aiPlayer.SeatIndex);
        usedNames.Remove(aiPlayer.Name);

        if (listIndex < activeAIPlayers.Count && activeAIPlayers[listIndex].Name == aiPlayer.Name)
        {
            activeAIPlayers.RemoveAt(listIndex);
        }
        else
        {
            activeAIPlayers.RemoveAll(ai => ai.Name == aiPlayer.Name);
        }

        UnityEngine.Debug.Log($"[AITableManager] AI '{aiPlayer.Name}' left the table");
    }

    int FindEmptySeat()
    {
        if (tableManager == null) return -1;

        List<int> emptySeats = new List<int>();
        for (int i = 1; i < tableManager.seats.Count; i++)
        {
            if (tableManager.seats[i].IsEmpty)
            {
                emptySeats.Add(i);
            }
        }

        if (emptySeats.Count == 0) return -1;

        return emptySeats[UnityEngine.Random.Range(0, emptySeats.Count)];
    }

    string GenerateUniqueName()
    {
        string name;
        int attempts = 0;

        do
        {
            string firstName = aiFirstNames[UnityEngine.Random.Range(0, aiFirstNames.Count)];
            string lastName = aiLastNames[UnityEngine.Random.Range(0, aiLastNames.Count)];
            name = $"{firstName} {lastName.Substring(0, 1)}.";
            attempts++;
        }
        while (usedNames.Contains(name) && attempts < 100);

        return name;
    }

    int GetRandomAvatarId()
    {
        if (avatarDatabase == null || avatarDatabase.AvatarCount == 0)
            return 0;

        return UnityEngine.Random.Range(0, avatarDatabase.AvatarCount);
    }

    public void OnPlayerJoined()
    {
        if (!isTestMode) return;

        if (activeAIPlayers.Count < minAIPlayers)
        {
            StartCoroutine(SpawnMultipleAI(minAIPlayers - activeAIPlayers.Count));
        }
    }

    IEnumerator SpawnMultipleAI(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(1f, 3f));
            TrySpawnAI();
        }
    }

    public AIPlayerData GetAIAtSeat(int seatIndex)
    {
        return activeAIPlayers.Find(ai => ai.SeatIndex == seatIndex);
    }

    public bool IsAISeat(int seatIndex)
    {
        return activeAIPlayers.Exists(ai => ai.SeatIndex == seatIndex);
    }

    public bool IsTestMode()
    {
        return isTestMode;
    }
}

[System.Serializable]
public class AIPlayerData
{
    public string Name;
    public int SeatIndex;
    public int AvatarId;
    public int Chips;
    public int Bankroll;
}
