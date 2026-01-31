using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum GameState
{
    WaitingForPlayers,
    StartingHand,
    PostingBlinds,
    DealingCards,
    PreFlop,
    Flop,
    Turn,
    River,
    Showdown,
    HandComplete
}

public class PokerGameManager : MonoBehaviour
{
    [Header("References")]
    public TableManager tableManager;
    public CardSpriteDatabase cardDatabase;

    [Header("UI - Dealer Button")]
    public Image dealerButtonImage;
    public List<Transform> dealerButtonPositions; // Position near each seat

    [Header("UI - Community Cards")]
    public List<Image> communityCardImages; // 5 images for flop/turn/river
    public Transform communityCardsContainer;

    [Header("UI - Pot")]
    public TMP_Text potText;
    public TMP_Text mainPotText;

    [Header("Game Settings")]
    public int minPlayersToStart = 2;
    public float dealDelay = 0.15f;              // Card dealing speed (was 0.3f - now faster!)
    public float allInShowdownDelay = 0.4f;      // Community card delay during all-in showdowns (was 1.0f - now faster!)
    public float showdownPauseDelay = 0.3f;      // Pause after revealing cards/dealing at showdown (was 1.5-2.0s - MUCH faster now!)
    public float betweenHandsDelay = 3f;
    public bool autoStartHands = true;

    [Header("Chips")]
    public ChipStack potChipStack;
    public ChipSpriteDatabase chipDatabase;
    public ChipSweepAnimator chipSweepAnimator;

    [Header("Betting")]
    public BettingRoundManager bettingManager;
    public PlayerUIController playerUI;

    [Header("Audio")]
    public AudioClip cardDealSound;     // Sound when dealing each card
    public AudioClip blindPostSound;    // Sound when posting blinds (chip bet sound)
    private AudioSource audioSource;

    [Header("Card Animation")]
    public Transform deckPosition;      // Position cards fly from (top of screen)
    public CardDealAnimator cardAnimator;  // Handles card flying animations

    [Header("State (Debug)")]
    [SerializeField] private GameState currentState = GameState.WaitingForPlayers;
    [SerializeField] private int dealerSeatIndex = -1;
    [SerializeField] private int smallBlindSeatIndex = -1;
    [SerializeField] private int bigBlindSeatIndex = -1;
    [SerializeField] private int currentPlayerIndex = -1;
    [SerializeField] private int pot = 0;

    // Internal
    private Deck deck = new Deck();
    private List<Card> communityCards = new List<Card>();
    private Dictionary<int, List<Card>> playerHands = new Dictionary<int, List<Card>>();
    private Dictionary<int, int> playerBets = new Dictionary<int, int>(); // Track current bets for each player
    private List<int> activePlayers = new List<int>(); // Seat indices of players in hand
    private bool isHandInProgress = false;

    // ★ Mid-hand join flag (read once at start)
    private bool joiningMidHand = false;
    private string savedTableId = "";  // ★ Store table ID before TableManager deletes it!
    private bool loadedFromSnapshot = false;  // ★ Skip dealing cards if loaded from snapshot
    private bool bettingCompleteFromSnapshot = false;  // ★ Skip betting round if complete
    private float registrySyncInterval = 1f;
    private float registrySyncTimer = 0f;
    private int handNumber = 0;

    void Start()
    {
        UnityEngine.Debug.Log("[PokerGameManager] START - Script initialized!");

        // ★★★ READ PLAYERPREFS IMMEDIATELY (before TableManager.Start() deletes them!) ★★★
        savedTableId = PlayerPrefs.GetString("SelectedTableId", "");
        joiningMidHand = PlayerPrefs.GetInt("JoiningMidHand", 0) == 1;

        UnityEngine.Debug.Log($"[PokerGameManager] Saved table ID: '{savedTableId}'");

        if (joiningMidHand)
        {
            UnityEngine.Debug.Log("[PokerGameManager] ⚠️ MID-HAND FLAG DETECTED IN START!");
        }

        // Clear the flag immediately so it doesn't persist
        PlayerPrefs.SetInt("JoiningMidHand", 0);
        PlayerPrefs.Save();

        if (tableManager == null)
            tableManager = FindObjectOfType<TableManager>();

        if (!string.IsNullOrEmpty(savedTableId))
        {
            TableState snapshot = TableRegistry.Instance.GetTableState(savedTableId);
            if (snapshot != null)
            {
                handNumber = snapshot.handNumber;
            }
        }

        // Setup AudioSource for dealing sounds
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;

        // Start checking for enough players
        StartCoroutine(GameLoop());
    }

    IEnumerator GameLoop()
    {
        while (true)
        {
            switch (currentState)
            {
                case GameState.WaitingForPlayers:
                    yield return WaitForPlayers();
                    break;

                case GameState.StartingHand:
                    yield return StartNewHand();
                    break;

                case GameState.PostingBlinds:
                    yield return PostBlinds();
                    break;

                case GameState.DealingCards:
                    yield return DealHoleCards();
                    break;

                case GameState.PreFlop:
                    // ★ SKIP betting round if betting was already complete from snapshot
                    if (bettingCompleteFromSnapshot)
                    {
                        UnityEngine.Debug.Log("[PokerGameManager] Skipping PreFlop betting - already complete from snapshot");
                        bettingCompleteFromSnapshot = false;  // Clear flag

                        // Collect any remaining bets and advance to Flop
                        yield return CollectBetsToPot();
                        currentState = GameState.Flop;
                        break;  // Exit PreFlop case, continue to Flop
                    }
                    else if (activePlayers.Count >= 2)
                    {
                        // PreFlop betting round
                        yield return RunBettingRound();
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning($"[PokerGameManager] Skipping PreFlop betting - only {activePlayers.Count} active players");
                        currentState = GameState.HandComplete;
                        break;
                    }

                    // Check if only 1 player left (everyone else folded)
                    if (currentState == GameState.HandComplete)
                    {
                        break; // Skip to HandComplete - don't collect bets or deal flop
                    }

                    // Check if everyone is all-in
                    bool everyoneAllIn = IsEveryoneAllIn();

                    // ALWAYS collect bets to pot first (even during all-in showdown)
                    // This moves bet chips to center BEFORE revealing cards
                    yield return CollectBetsToPot();

                    // THEN reveal cards if everyone is all-in
                    if (everyoneAllIn)
                    {
                        UnityEngine.Debug.Log("[All-In Showdown] Everyone all-in - revealing cards!");
                        yield return RevealAllCards();
                        yield return new WaitForSeconds(showdownPauseDelay); // Pause to show cards
                    }

                    currentState = GameState.Flop;
                    break;

                case GameState.Flop:
                    // Deal flop (instant if everyone all-in, otherwise normal)
                    // ★ SKIP dealing if we loaded from snapshot (cards already on board)
                    if (loadedFromSnapshot)
                    {
                        UnityEngine.Debug.Log("[PokerGameManager] Skipping DealFlop - cards loaded from snapshot");
                        loadedFromSnapshot = false;  // Clear flag
                    }
                    else if (IsEveryoneAllIn())
                    {
                        yield return DealFlopInstant();
                    }
                    else
                    {
                        yield return DealFlop();
                    }

                    // ★ SKIP betting round if betting was already complete from snapshot
                    if (bettingCompleteFromSnapshot)
                    {
                        UnityEngine.Debug.Log("[PokerGameManager] Skipping Flop betting - already complete from snapshot");
                        bettingCompleteFromSnapshot = false;  // Clear flag

                        // Collect any remaining bets and advance to Turn
                        yield return CollectBetsToPot();
                        currentState = GameState.Turn;
                        break;  // Exit Flop case, continue to Turn
                    }
                    else if (activePlayers.Count >= 2)
                    {
                        yield return RunBettingRound();
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning($"[PokerGameManager] Skipping Flop betting - only {activePlayers.Count} active players");
                        currentState = GameState.HandComplete;
                        break;
                    }

                    // Check if only 1 player left
                    if (currentState == GameState.HandComplete)
                    {
                        break; // Skip collecting bets and dealing turn
                    }

                    // Check if everyone is all-in after flop betting
                    bool everyoneAllInFlop = IsEveryoneAllIn();

                    // ALWAYS collect bets to pot first
                    yield return CollectBetsToPot();

                    // THEN reveal cards if everyone is all-in
                    if (everyoneAllInFlop)
                    {
                        UnityEngine.Debug.Log("[All-In Showdown] Everyone all-in on flop!");
                        yield return RevealAllCards();
                        yield return new WaitForSeconds(showdownPauseDelay);
                    }

                    currentState = GameState.Turn;
                    break;

                case GameState.Turn:
                    // ★ SKIP dealing if we loaded from snapshot (cards already on board)
                    if (loadedFromSnapshot)
                    {
                        UnityEngine.Debug.Log("[PokerGameManager] Skipping DealTurn - cards loaded from snapshot");
                        loadedFromSnapshot = false;  // Clear flag
                    }
                    else
                    {
                        yield return DealTurn();
                    }

                    // ★ SKIP betting round if betting was already complete from snapshot
                    if (bettingCompleteFromSnapshot)
                    {
                        UnityEngine.Debug.Log("[PokerGameManager] Skipping Turn betting - already complete from snapshot");
                        bettingCompleteFromSnapshot = false;  // Clear flag

                        // Collect any remaining bets and advance to River
                        yield return CollectBetsToPot();
                        currentState = GameState.River;
                        break;  // Exit Turn case, continue to River
                    }
                    else if (activePlayers.Count >= 2)
                    {
                        yield return RunBettingRound();
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning($"[PokerGameManager] Skipping Turn betting - only {activePlayers.Count} active players");
                        currentState = GameState.HandComplete;
                        break;
                    }

                    // Check if only 1 player left
                    if (currentState == GameState.HandComplete)
                    {
                        break; // Skip collecting bets and dealing river
                    }

                    // Check if everyone is all-in after turn betting
                    bool everyoneAllInTurn = IsEveryoneAllIn();

                    // ALWAYS collect bets to pot first
                    yield return CollectBetsToPot();

                    // THEN reveal cards if everyone is all-in
                    if (everyoneAllInTurn)
                    {
                        UnityEngine.Debug.Log("[All-In Showdown] Everyone all-in on turn!");
                        yield return RevealAllCards();
                        yield return new WaitForSeconds(showdownPauseDelay);
                    }

                    currentState = GameState.River;
                    break;

                case GameState.River:
                    // ★ SKIP dealing if we loaded from snapshot (cards already on board)
                    if (loadedFromSnapshot)
                    {
                        UnityEngine.Debug.Log("[PokerGameManager] Skipping DealRiver - cards loaded from snapshot");
                        loadedFromSnapshot = false;  // Clear flag
                    }
                    else
                    {
                        yield return DealRiver();
                    }

                    // ★ SKIP betting round if betting was already complete from snapshot
                    if (bettingCompleteFromSnapshot)
                    {
                        UnityEngine.Debug.Log("[PokerGameManager] Skipping River betting - already complete from snapshot");
                        bettingCompleteFromSnapshot = false;  // Clear flag

                        // Collect any remaining bets and advance to Showdown
                        yield return CollectBetsToPot();
                        currentState = GameState.Showdown;
                        break;  // Exit River case, continue to Showdown
                    }
                    else if (activePlayers.Count >= 2)
                    {
                        yield return RunBettingRound();
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning($"[PokerGameManager] Skipping River betting - only {activePlayers.Count} active players");
                        currentState = GameState.HandComplete;
                        break;
                    }

                    // Check if only 1 player left
                    if (currentState == GameState.HandComplete)
                    {
                        break; // Skip collecting bets and go to HandComplete
                    }

                    yield return CollectBetsToPot();
                    currentState = GameState.Showdown;
                    break;

                case GameState.Showdown:
                    yield return DoShowdown();
                    currentState = GameState.HandComplete;
                    break;

                case GameState.HandComplete:
                    yield return EndHand();
                    break;
            }

            yield return null;
        }
    }

    IEnumerator WaitForPlayers()
    {
        // ★★★ CHECK IF JOINING MID-HAND FIRST (before waiting) ★★★
        if (joiningMidHand)
        {
            UnityEngine.Debug.Log("[PokerGameManager] ⚠️ MID-HAND JOIN DETECTED!");
            UnityEngine.Debug.Log("[PokerGameManager] Waiting for AI players to be loaded...");

            // Wait for AI players to be seated (from TableManager)
            while (GetSeatedPlayerCount() < minPlayersToStart)
            {
                yield return new WaitForSeconds(0.5f);
            }

            UnityEngine.Debug.Log($"[PokerGameManager] AI players ready ({GetSeatedPlayerCount()} seated)");
            UnityEngine.Debug.Log("[PokerGameManager] Loading hand from TableRegistry...");

            // Use savedTableId (stored in Start before TableManager deletes it)
            string tableId = savedTableId;
            UnityEngine.Debug.Log($"[PokerGameManager] DEBUG: Using saved table ID: '{tableId}'");

            if (!string.IsNullOrEmpty(tableId))
            {
                UnityEngine.Debug.Log($"[PokerGameManager] ✓ Table ID found: {tableId}");

                // Load the full snapshot from TableRegistry
                TableState snapshot = TableRegistry.Instance.GetTableState(tableId);

                if (snapshot != null && snapshot.currentStreet != "BetweenHands")
                {
                    UnityEngine.Debug.Log($"[PokerGameManager] ✓ Loaded hand state:");
                    UnityEngine.Debug.Log($"  - Hand #{snapshot.handNumber}");
                    UnityEngine.Debug.Log($"  - Street: {snapshot.currentStreet}");
                    UnityEngine.Debug.Log($"  - Pot: ${snapshot.totalPot}");
                    UnityEngine.Debug.Log($"  - Board: {string.Join(" ", snapshot.boardCards)}");

                    // Apply the snapshot to restore the hand
                    ApplySnapshot(snapshot);

                    // ★ Set flags to skip dealing cards AND skip betting if complete
                    loadedFromSnapshot = true;
                    // bettingCompleteFromSnapshot is set inside ApplySnapshot

                    UnityEngine.Debug.Log("[PokerGameManager] ✓ Hand loaded - you're watching mid-hand!");
                    UnityEngine.Debug.Log($"[PokerGameManager] ✓ Flags: loadedFromSnapshot={loadedFromSnapshot}, bettingComplete={bettingCompleteFromSnapshot}");
                }
                else if (snapshot == null)
                {
                    UnityEngine.Debug.LogWarning($"[PokerGameManager] No snapshot found in registry for table {tableId}");
                    yield return new WaitForSeconds(1f);
                    currentState = GameState.StartingHand;
                }
                else
                {
                    UnityEngine.Debug.Log($"[PokerGameManager] Snapshot found but hand between hands (street={snapshot.currentStreet})");
                    yield return new WaitForSeconds(1f);
                    currentState = GameState.StartingHand;
                }
            }
            else
            {
                UnityEngine.Debug.LogError("[PokerGameManager] No table ID saved!");
                yield return new WaitForSeconds(1f);
                currentState = GameState.StartingHand;
            }

            yield break;  // Exit - don't run normal waiting logic
        }

        // Normal join - wait for enough players
        while (GetSeatedPlayerCount() < minPlayersToStart)
        {
            yield return new WaitForSeconds(1f);
        }

        UnityEngine.Debug.Log($"Enough players ({GetSeatedPlayerCount()}) - starting hand!");

        if (autoStartHands)
        {
            yield return new WaitForSeconds(2f); // Brief delay before starting
            currentState = GameState.StartingHand;
        }
    }

    IEnumerator StartNewHand()
    {
        UnityEngine.Debug.Log("=== Starting New Hand ===");
        isHandInProgress = true;
        handNumber++;

        // ★ Process players who joined mid-hand - they can now be dealt in
        TableJoiner joiner = GetComponent<TableJoiner>();
        if (joiner != null)
        {
            joiner.ProcessWaitingPlayers();
        }

        // Reset
        deck.Reset();
        deck.Shuffle();
        communityCards.Clear();
        playerHands.Clear();
        playerBets.Clear(); // Reset player bets
        pot = 0;

        // Clear pot chips
        if (potChipStack != null)
        {
            potChipStack.ClearChips();
        }

        // Clear pot text
        if (potText != null) potText.text = "";
        if (mainPotText != null) mainPotText.text = "";

        // Clear community cards display
        foreach (var img in communityCardImages)
        {
            if (img != null)
            {
                img.gameObject.SetActive(false);
            }
        }

        // Clear player cards and bet chips
        foreach (var seat in tableManager.seats)
        {
            if (seat != null)
            {
                seat.HideCards();
                seat.ClearBet();
                seat.ResetAllInState();  // ★ Reset all-in flag for new hand
            }
        }

        // Get active players (those with chips)
        activePlayers.Clear();
        for (int i = 0; i < tableManager.seats.Count; i++)
        {
            var seat = tableManager.seats[i];

            // Debug: Check every seat
            if (seat != null)
            {
                UnityEngine.Debug.Log($"[StartHand] Seat {i}: IsSeated={seat.IsSeated}, Chips={seat.ChipCount}, Name='{seat.PlayerName}'");
            }

            // CRITICAL: Only include players who are seated AND have chips > 0 AND not waiting
            // Players with 0 chips are sitting out and should NOT be dealt in
            // Players marked as waiting for next hand should also NOT be dealt in yet
            PlayerSeatStatus status = seat?.GetComponent<PlayerSeatStatus>();
            bool isWaiting = (status != null && status.isWaitingForNextHand);

            if (seat != null && seat.IsSeated && seat.ChipCount > 0 && !isWaiting)
            {
                activePlayers.Add(i);
                UnityEngine.Debug.Log($"[StartHand] ✓ Seat {i} ({seat.PlayerName}) added to hand with ${seat.ChipCount}");

                // Lock seat - player can't leave during hand
                if (status != null)
                {
                    status.LockSeat();
                }
            }
            else if (isWaiting)
            {
                // Player is waiting for next hand (joined mid-hand)
                UnityEngine.Debug.Log($"[StartHand] ⏸ Seat {i} ({seat.PlayerName}) WAITING - will be dealt in next hand");
            }
            else if (seat != null && seat.ChipCount == 0 && seat.IsSeated)
            {
                // Player is sitting out (broke, 0 chips)
                UnityEngine.Debug.Log($"[StartHand] ✗ Seat {i} SITTING OUT (0 chips) - NOT dealt in");
            }
            else if (seat != null)
            {
                UnityEngine.Debug.Log($"[StartHand] ✗ Seat {i} excluded (IsSeated={seat.IsSeated}, Chips={seat.ChipCount})");
            }
        }

        UnityEngine.Debug.Log($"[StartHand] Total active players: {activePlayers.Count}");

        // Update chip displays BEFORE hand starts
        // This ensures sitting out players show "SITTING OUT" (gray)
        // and players with chips show their chip count (white)
        AllInDisplayHandler.UpdateAllChipDisplays(tableManager, handInProgress: false);

        if (activePlayers.Count < minPlayersToStart)
        {
            UnityEngine.Debug.Log("Not enough players with chips!");
            currentState = GameState.WaitingForPlayers;
            yield break;
        }

        // Move dealer button
        MoveDealer();

        // Determine blinds
        SetBlindPositions();

        yield return new WaitForSeconds(0.5f);
        currentState = GameState.PostingBlinds;
    }

    void MoveDealer()
    {
        // Find next active player after current dealer (CLOCKWISE = increment with your array)
        if (dealerSeatIndex == -1)
        {
            // First hand - dealer is first active player
            dealerSeatIndex = activePlayers[0];
        }
        else
        {
            // Move to next active player CLOCKWISE (increment with your array order)
            int currentIndex = activePlayers.IndexOf(dealerSeatIndex);
            if (currentIndex == -1)
            {
                dealerSeatIndex = activePlayers[0];
            }
            else
            {
                dealerSeatIndex = activePlayers[(currentIndex + 1) % activePlayers.Count];
            }
        }

        // Update dealer button visual
        UpdateDealerButton();
        UnityEngine.Debug.Log($"Dealer is now seat {dealerSeatIndex}");
    }

    void UpdateDealerButton()
    {
        if (dealerButtonImage == null) return;

        dealerButtonImage.gameObject.SetActive(true);

        // Position near dealer's seat
        if (dealerButtonPositions != null && dealerSeatIndex < dealerButtonPositions.Count)
        {
            if (dealerButtonPositions[dealerSeatIndex] != null)
            {
                dealerButtonImage.transform.position = dealerButtonPositions[dealerSeatIndex].position;
            }
        }
    }

    void SetBlindPositions()
    {
        int dealerIndex = activePlayers.IndexOf(dealerSeatIndex);

        if (activePlayers.Count == 2)
        {
            // Heads up - dealer is small blind
            smallBlindSeatIndex = dealerSeatIndex;
            // Big blind is CLOCKWISE from dealer (increment with your array order)
            bigBlindSeatIndex = activePlayers[(dealerIndex + 1) % activePlayers.Count];
        }
        else
        {
            // Normal - SB is CLOCKWISE from dealer, BB is CLOCKWISE from SB
            // CLOCKWISE = increment with your array order
            smallBlindSeatIndex = activePlayers[(dealerIndex + 1) % activePlayers.Count];
            bigBlindSeatIndex = activePlayers[(dealerIndex + 2) % activePlayers.Count];
        }

        UnityEngine.Debug.Log($"Blinds: SB = seat {smallBlindSeatIndex}, BB = seat {bigBlindSeatIndex}");
    }

    IEnumerator PostBlinds()
    {
        // Get blind amounts from table
        int bb = tableManager.bigBlind;
        int sb = bb / 2; // FORCE small blind to be exactly half of big blind

        UnityEngine.Debug.Log($"[PostBlinds] Blinds: SB = ${sb}, BB = ${bb}");

        // Post small blind
        PlayerSeat sbSeat = tableManager.seats[smallBlindSeatIndex];
        int sbAmount = Mathf.Min(sb, sbSeat.ChipCount);
        sbSeat.UpdateChips(sbSeat.ChipCount - sbAmount);
        sbSeat.ShowBet(sbAmount);  // Show bet chips in front of player
        pot += sbAmount;
        playerBets[smallBlindSeatIndex] = sbAmount; // Track blind bet

        // Play blind post sound
        if (blindPostSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(blindPostSound);
        }

        UnityEngine.Debug.Log($"{sbSeat.PlayerName} posts small blind ${sbAmount}");

        yield return new WaitForSeconds(dealDelay);

        // Post big blind
        PlayerSeat bbSeat = tableManager.seats[bigBlindSeatIndex];
        int bbAmount = Mathf.Min(bb, bbSeat.ChipCount);
        bbSeat.UpdateChips(bbSeat.ChipCount - bbAmount);
        bbSeat.ShowBet(bbAmount);  // Show bet chips in front of player
        pot += bbAmount;
        playerBets[bigBlindSeatIndex] = bbAmount; // Track blind bet

        // Play blind post sound
        if (blindPostSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(blindPostSound);
        }

        UnityEngine.Debug.Log($"{bbSeat.PlayerName} posts big blind ${bbAmount}");

        // Update chip displays (show "ALL-IN" if someone posted their last chips)
        // Only update players who are actually in the hand
        AllInDisplayHandler.UpdateAllChipDisplays(tableManager, handInProgress: true, activePlayers);

        // DON'T show pot chips yet - bets stay in front of players
        // Pot chips will show when we collect bets later

        yield return new WaitForSeconds(dealDelay);
        currentState = GameState.DealingCards;
    }

    IEnumerator DealHoleCards()
    {
        UnityEngine.Debug.Log("Dealing hole cards...");

        // Lock all seats during dealing - players can't leave mid-deal
        foreach (int seatIndex in activePlayers)
        {
            PlayerSeat seat = tableManager.seats[seatIndex];
            PlayerSeatStatus status = seat?.GetComponent<PlayerSeatStatus>();
            if (status != null)
            {
                status.LockSeat();
            }
        }

        // Deal 2 cards to each player, starting left of dealer
        int dealerIndex = activePlayers.IndexOf(dealerSeatIndex);

        // First card to each player
        for (int i = 1; i <= activePlayers.Count; i++)
        {
            int seatIndex = activePlayers[(dealerIndex + i) % activePlayers.Count];
            PlayerSeat seat = tableManager.seats[seatIndex];

            // Play sound once per player (not per card)
            if (cardDealSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(cardDealSound);
            }

            // Animate card flying from deck to player (if animator available)
            if (cardAnimator != null && deckPosition != null && seat != null)
            {
                yield return cardAnimator.AnimateCardDeal(deckPosition, seat.transform);
            }

            // Deal card (adds to hand)
            DealCardToPlayer(seatIndex);

            yield return new WaitForSeconds(dealDelay);
        }

        // Second card to each player
        for (int i = 1; i <= activePlayers.Count; i++)
        {
            int seatIndex = activePlayers[(dealerIndex + i) % activePlayers.Count];
            PlayerSeat seat = tableManager.seats[seatIndex];

            // Play sound once per player (not per card)
            if (cardDealSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(cardDealSound);
            }

            // Animate card flying from deck to player (if animator available)
            if (cardAnimator != null && deckPosition != null && seat != null)
            {
                yield return cardAnimator.AnimateCardDeal(deckPosition, seat.transform);
            }

            // Deal card (adds to hand)
            DealCardToPlayer(seatIndex);

            yield return new WaitForSeconds(dealDelay);
        }

        // Show cards on seats
        UpdatePlayerCardDisplays();

        // IMPORTANT: Give AI players their hole cards so they can make smart decisions!
        foreach (int seatIndex in activePlayers)
        {
            PlayerSeat seat = tableManager.seats[seatIndex];
            if (seat != null && !seat.IsLocalPlayer)
            {
                // This is an AI player - give them their cards
                PokerPlayerController aiController = seat.GetComponent<PokerPlayerController>();
                if (aiController != null && playerHands.ContainsKey(seatIndex))
                {
                    aiController.SetHoleCards(playerHands[seatIndex]);
                }
            }
        }

        UnityEngine.Debug.Log("Hole cards dealt!");
        currentState = GameState.PreFlop;
    }

    void DealCardToPlayer(int seatIndex)
    {
        Card card = deck.Deal();
        if (card == null) return;

        if (!playerHands.ContainsKey(seatIndex))
        {
            playerHands[seatIndex] = new List<Card>();
        }

        playerHands[seatIndex].Add(card);

        // Sound is now played in the dealing loop (once per player)
        // Animation will be added here for card flying from deck to player
    }

    void UpdatePlayerCardDisplays()
    {
        UnityEngine.Debug.Log($"UpdatePlayerCardDisplays - {playerHands.Count} hands to display");

        foreach (var kvp in playerHands)
        {
            int seatIndex = kvp.Key;
            List<Card> hand = kvp.Value;

            if (seatIndex < tableManager.seats.Count)
            {
                PlayerSeat seat = tableManager.seats[seatIndex];
                if (seat != null && hand.Count >= 2)
                {
                    // IMPORTANT: Re-enable cards if they were hidden by fold animation
                    seat.ResetCards();

                    // Get card sprites
                    Sprite card1Sprite = null;
                    Sprite card2Sprite = null;

                    UnityEngine.Debug.Log($"Seat {seatIndex} ({seat.PlayerName}): IsLocalPlayer={seat.IsLocalPlayer}, cardDatabase={(cardDatabase != null ? "assigned" : "NULL")}");

                    if (seat.IsLocalPlayer && cardDatabase != null)
                    {
                        // Show actual cards to local player
                        card1Sprite = cardDatabase.GetCardSprite(hand[0]);
                        card2Sprite = cardDatabase.GetCardSprite(hand[1]);
                        UnityEngine.Debug.Log($"  Showing cards: {hand[0]} ({(card1Sprite != null ? card1Sprite.name : "NULL")}), {hand[1]} ({(card2Sprite != null ? card2Sprite.name : "NULL")})");
                    }
                    else if (cardDatabase != null)
                    {
                        // Show card backs to other players
                        card1Sprite = cardDatabase.cardBackSprite;
                        card2Sprite = cardDatabase.cardBackSprite;
                        UnityEngine.Debug.Log($"  Showing card backs: {(card1Sprite != null ? "assigned" : "NULL")}");
                    }

                    seat.ShowCards(card1Sprite, card2Sprite);
                }
            }
        }
    }

    IEnumerator DealFlop()
    {
        UnityEngine.Debug.Log("Dealing flop...");

        // Burn one card
        deck.Deal();

        // Use longer delay if everyone is all-in (showdown mode)
        float cardDelay = IsEveryoneAllIn() ? allInShowdownDelay : dealDelay;

        if (IsEveryoneAllIn())
        {
            UnityEngine.Debug.Log("[Showdown] Dealing flop slowly for all-in showdown...");
        }

        // Deal 3 cards
        for (int i = 0; i < 3; i++)
        {
            Card card = deck.Deal();
            communityCards.Add(card);

            // Animate card flying from deck to community card position (if animator available)
            if (cardAnimator != null && deckPosition != null && i < communityCardImages.Count && communityCardImages[i] != null)
            {
                // Play card deal sound
                if (cardDealSound != null && audioSource != null)
                {
                    audioSource.PlayOneShot(cardDealSound);
                }

                yield return cardAnimator.AnimateCardDeal(deckPosition, communityCardImages[i].transform);
            }

            ShowCommunityCard(i, card);
            yield return new WaitForSeconds(cardDelay);
        }

        UnityEngine.Debug.Log($"Flop: {communityCards[0].GetShortName()} {communityCards[1].GetShortName()} {communityCards[2].GetShortName()}");
    }

    /// <summary>
    /// Deal flop instantly (all 3 cards at once) for all-in showdowns
    /// </summary>
    IEnumerator DealFlopInstant()
    {
        UnityEngine.Debug.Log("[Showdown] Dealing flop INSTANTLY for all-in showdown...");

        // Burn one card
        deck.Deal();

        // Deal all 3 cards at once
        for (int i = 0; i < 3; i++)
        {
            Card card = deck.Deal();
            communityCards.Add(card);
            ShowCommunityCard(i, card);
        }

        UnityEngine.Debug.Log($"Flop: {communityCards[0].GetShortName()} {communityCards[1].GetShortName()} {communityCards[2].GetShortName()}");

        // Pause to see the flop
        yield return new WaitForSeconds(showdownPauseDelay);
    }

    IEnumerator DealTurn()
    {
        UnityEngine.Debug.Log("Dealing turn...");

        // Burn one card
        deck.Deal();

        // Deal turn
        Card card = deck.Deal();
        communityCards.Add(card);

        // Animate card flying from deck to turn position (if animator available)
        if (cardAnimator != null && deckPosition != null && communityCardImages.Count > 3 && communityCardImages[3] != null)
        {
            // Play card deal sound
            if (cardDealSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(cardDealSound);
            }

            yield return cardAnimator.AnimateCardDeal(deckPosition, communityCardImages[3].transform);
        }

        ShowCommunityCard(3, card);

        UnityEngine.Debug.Log($"Turn: {card.GetShortName()}");

        // Use longer delay if everyone is all-in
        float cardDelay = IsEveryoneAllIn() ? allInShowdownDelay : 0.5f;
        yield return new WaitForSeconds(cardDelay);
    }

    IEnumerator DealRiver()
    {
        UnityEngine.Debug.Log("Dealing river...");

        // Burn one card
        deck.Deal();

        // Deal river
        Card card = deck.Deal();
        communityCards.Add(card);

        // Animate card flying from deck to river position (if animator available)
        if (cardAnimator != null && deckPosition != null && communityCardImages.Count > 4 && communityCardImages[4] != null)
        {
            // Play card deal sound
            if (cardDealSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(cardDealSound);
            }

            yield return cardAnimator.AnimateCardDeal(deckPosition, communityCardImages[4].transform);
        }

        ShowCommunityCard(4, card);

        UnityEngine.Debug.Log($"River: {card.GetShortName()}");

        // Use longer delay if everyone is all-in
        float cardDelay = IsEveryoneAllIn() ? allInShowdownDelay : 0.5f;
        yield return new WaitForSeconds(cardDelay);
    }

    void ShowCommunityCard(int index, Card card)
    {
        if (index < communityCardImages.Count && communityCardImages[index] != null)
        {
            communityCardImages[index].gameObject.SetActive(true);

            if (cardDatabase != null)
            {
                communityCardImages[index].sprite = cardDatabase.GetCardSprite(card);
            }

            // Sound now played by animation code - removed to prevent duplicate
            // (Animation plays sound when card starts flying from deck)
        }
    }

    IEnumerator DoShowdown()
    {
        UnityEngine.Debug.Log("=== SHOWDOWN ===");

        // Remove any away/disconnected players from activePlayers before showdown
        for (int i = activePlayers.Count - 1; i >= 0; i--)
        {
            int seatIndex = activePlayers[i];
            PlayerSeat seat = tableManager.seats[seatIndex];
            PlayerSeatStatus status = seat?.GetComponent<PlayerSeatStatus>();

            if (status != null && status.isAway)
            {
                UnityEngine.Debug.Log($"[Showdown] Removing away player at seat {seatIndex} from showdown");
                activePlayers.RemoveAt(i);
            }
        }

        // If only 1 player left after removing away players, award pot instead of showdown
        if (activePlayers.Count == 1)
        {
            UnityEngine.Debug.Log($"[Showdown] Only 1 player remains - awarding pot instead of showdown");
            yield return AwardPotToLastPlayer();
            currentState = GameState.HandComplete;
            yield break;
        }

        // If no players left (shouldn't happen but safety check)
        if (activePlayers.Count == 0)
        {
            UnityEngine.Debug.LogError("[Showdown] No players left for showdown!");
            currentState = GameState.HandComplete;
            yield break;
        }

        // Bets were already collected to pot after river betting
        // No need to clear them again here

        // Log community cards
        string communityStr = "BOARD: ";
        foreach (var card in communityCards)
        {
            communityStr += card.GetShortName() + " ";
        }
        UnityEngine.Debug.Log(communityStr);
        UnityEngine.Debug.Log("-------------------");

        // Evaluate all hands
        Dictionary<int, EvaluatedHand> handResults = new Dictionary<int, EvaluatedHand>();

        foreach (var kvp in playerHands)
        {
            int seatIndex = kvp.Key;
            List<Card> holeCards = kvp.Value;

            // Skip players who folded (not in activePlayers)
            if (!activePlayers.Contains(seatIndex))
            {
                UnityEngine.Debug.Log($"Seat {seatIndex} folded, skipping");
                continue;
            }

            PlayerSeat seat = tableManager.seats[seatIndex];
            if (seat == null) continue;

            // Log hole cards
            UnityEngine.Debug.Log($"[{seat.PlayerName}]");
            UnityEngine.Debug.Log($"  Hole Cards: {holeCards[0].GetShortName()} {holeCards[1].GetShortName()}");

            // Evaluate hand
            EvaluatedHand result = HandEvaluator.EvaluateBestHand(holeCards, communityCards);

            if (result != null)
            {
                handResults[seatIndex] = result;

                // Show which cards from hole vs community
                string fromHole = "";
                string fromCommunity = "";

                foreach (var card in result.BestFiveCards)
                {
                    bool isHoleCard = holeCards.Exists(h => h.rank == card.rank && h.suit == card.suit);
                    if (isHoleCard)
                        fromHole += card.GetShortName() + " ";
                    else
                        fromCommunity += card.GetShortName() + " ";
                }

                UnityEngine.Debug.Log($"  Best Hand: {result.Description}");
                UnityEngine.Debug.Log($"  Best 5 Cards: {string.Join(" ", result.BestFiveCards.Select(c => c.GetShortName()))}");
                UnityEngine.Debug.Log($"    From Hole: {(string.IsNullOrEmpty(fromHole.Trim()) ? "(none)" : fromHole.Trim())}");
                UnityEngine.Debug.Log($"    From Board: {(string.IsNullOrEmpty(fromCommunity.Trim()) ? "(none)" : fromCommunity.Trim())}");
            }
            else
            {
                UnityEngine.Debug.LogError($"  ERROR: Could not evaluate hand for {seat.PlayerName}!");
            }

            // Show cards visually with spread animation
            if (cardDatabase != null && holeCards.Count >= 2)
            {
                Sprite card1Sprite = cardDatabase.GetCardSprite(holeCards[0]);
                Sprite card2Sprite = cardDatabase.GetCardSprite(holeCards[1]);
                seat.RevealCards(card1Sprite, card2Sprite);  // ★ Spread animation at showdown
            }
        }

        UnityEngine.Debug.Log("-------------------");

        yield return new WaitForSeconds(showdownPauseDelay);

        // Determine winner(s)
        List<int> winners = HandEvaluator.DetermineWinners(handResults);

        if (winners.Count == 0)
        {
            UnityEngine.Debug.Log("No winners?!");
            yield break;
        }

        // Calculate winnings per winner (split pot if tied)
        int potPerWinner = pot / winners.Count;
        int remainder = pot % winners.Count;

        // Animate pot chips to winner(s)
        if (winners.Count == 1)
        {
            // Single winner - animate all pot chips to them
            int winnerIndex = winners[0];
            PlayerSeat winner = tableManager.seats[winnerIndex];
            EvaluatedHand winningHand = handResults[winnerIndex];
            List<Card> winnerHoleCards = playerHands[winnerIndex];

            int winAmount = pot;

            UnityEngine.Debug.Log($"*** WINNER: {winner.PlayerName} ***");
            UnityEngine.Debug.Log($"  Hole Cards: {winnerHoleCards[0].GetShortName()} {winnerHoleCards[1].GetShortName()}");
            UnityEngine.Debug.Log($"  Winning Hand: {winningHand.Description}");
            UnityEngine.Debug.Log($"  Wins: ${winAmount}");

            // Show "WINNER" text in green
            winner.ShowAction("WINNER", 3.0f, Color.green);

            // Animate pot chips sliding to winner
            if (chipSweepAnimator != null && potChipStack != null)
            {
                yield return chipSweepAnimator.AnimateChipsToWinner(
                    potChipStack,
                    winner.transform,
                    null
                );

                // Clear pot stack (chips were destroyed by animator)
                potChipStack.ClearChips();
            }

            // Update winner's chip count
            winner.UpdateChips(winner.ChipCount + winAmount);
        }
        else
        {
            // Split pot - for now, just show pot chips and award instantly
            // (Animating to multiple winners is more complex - we can add it later)
            UnityEngine.Debug.Log($"Split pot between {winners.Count} players!");

            foreach (int winnerIndex in winners)
            {
                PlayerSeat winner = tableManager.seats[winnerIndex];
                EvaluatedHand winningHand = handResults[winnerIndex];
                List<Card> winnerHoleCards = playerHands[winnerIndex];

                int winAmount = potPerWinner;
                if (remainder > 0)
                {
                    winAmount++;
                    remainder--;
                }

                UnityEngine.Debug.Log($"*** WINNER: {winner.PlayerName} ***");
                UnityEngine.Debug.Log($"  Hole Cards: {winnerHoleCards[0].GetShortName()} {winnerHoleCards[1].GetShortName()}");
                UnityEngine.Debug.Log($"  Winning Hand: {winningHand.Description}");
                UnityEngine.Debug.Log($"  Wins: ${winAmount}");

                // Show "WINNER" text in green
                winner.ShowAction("WINNER", 3.0f, Color.green);

                winner.UpdateChips(winner.ChipCount + winAmount);
            }

            // Clear pot after delay (no animation for split pots yet)
            yield return new WaitForSeconds(showdownPauseDelay);
            if (potChipStack != null)
            {
                potChipStack.ClearChips();
            }
        }

        pot = 0;
        // Don't call UpdatePotDisplay here - pot chips already cleared by animation

        yield return new WaitForSeconds(showdownPauseDelay);
    }

    IEnumerator EndHand()
    {
        UnityEngine.Debug.Log("=== Hand Complete ===");
        isHandInProgress = false;

        // Clear cards and bet chips
        foreach (var seat in tableManager.seats)
        {
            if (seat != null)
            {
                seat.HideCards();
                seat.ClearBet();
            }
        }

        foreach (var img in communityCardImages)
        {
            if (img != null)
            {
                img.gameObject.SetActive(false);
            }
        }

        // Clear pot chips
        if (potChipStack != null)
        {
            potChipStack.ClearChips();
        }

        // Unlock all seats - players can now leave
        foreach (var seat in tableManager.seats)
        {
            if (seat != null)
            {
                PlayerSeatStatus status = seat.GetComponent<PlayerSeatStatus>();
                if (status != null)
                {
                    status.UnlockSeat();

                    // If player was away during hand, actually remove them now
                    if (status.isAway && seat.IsSeated)
                    {
                        UnityEngine.Debug.Log($"[PokerGameManager] Removing {seat.PlayerName} who left during hand");

                        // Clear the seat completely (frees it up for new players)
                        seat.ClearSeat();

                        // Reset away status
                        status.isAway = false;

                        UnityEngine.Debug.Log($"[PokerGameManager] Seat cleared - available for new players");
                    }
                }
            }
        }

        // Remove players with no chips - mark them as sitting out
        for (int i = activePlayers.Count - 1; i >= 0; i--)
        {
            int seatIndex = activePlayers[i];
            PlayerSeat seat = tableManager.seats[seatIndex];
            if (seat != null && seat.ChipCount <= 0)
            {
                UnityEngine.Debug.Log($"{seat.PlayerName} is out of chips - setting to SITTING OUT");

                // Mark as not seated so they show "SITTING OUT" instead of chip count
                // This will prevent them from being included in next hand
                seat.UpdateChips(0);  // Ensure chips are 0
                // seat.IsSeated will be checked in StartNewHand to exclude them
            }
        }

        // ★ IMPORTANT: Wait for betweenHandsDelay BEFORE updating chip displays
        // This allows WINNER text to display for its full 3 seconds
        // If we update chip displays immediately, it overwrites the WINNER text!
        yield return new WaitForSeconds(betweenHandsDelay);

        // Update all chip displays - broke players will show "SITTING OUT"
        AllInDisplayHandler.UpdateAllChipDisplays(tableManager, handInProgress: false);

        // Check if we still have enough players
        if (GetSeatedPlayerCount() >= minPlayersToStart)
        {
            currentState = GameState.StartingHand;
        }
        else
        {
            currentState = GameState.WaitingForPlayers;
        }
    }

    void UpdatePotDisplay()
    {
        if (potText != null)
        {
            potText.text = pot > 0 ? $"Pot: ${pot}" : "";
        }

        if (mainPotText != null)
        {
            mainPotText.text = pot > 0 ? $"${pot}" : "";
        }

        // Update pot chips visually
        if (potChipStack != null)
        {
            potChipStack.ShowChips(pot);
        }
    }

    /// <summary>
    /// Public method for BettingRoundManager to update pot display during betting
    /// Only updates text, not pot chips (since chips are still in front of players)
    /// </summary>
    public void UpdatePotDisplayFromBetting(int newPotAmount)
    {
        pot = newPotAmount;  // Update internal pot value

        // Update text displays
        if (potText != null)
        {
            potText.text = pot > 0 ? $"Pot: ${pot}" : "";
        }

        if (mainPotText != null)
        {
            mainPotText.text = pot > 0 ? $"${pot}" : "";
        }

        // Don't update pot chips here - chips are still in front of players!
        // Pot chips will be shown when CollectBetsToPot() is called
    }


    /// <summary>
    /// Collect all player bet chips to the pot (Full Tilt style with animation)
    /// Chips slide from players to center, then pot chips form
    /// </summary>
    IEnumerator CollectBetsToPot()
    {
        UnityEngine.Debug.Log("Collecting bets to pot...");

        if (chipSweepAnimator != null && potChipStack != null)
        {
            // Animate chips sliding to pot
            bool animationComplete = false;

            yield return chipSweepAnimator.AnimateChipsToPot(
                tableManager.seats,
                potChipStack.transform,
                () => { animationComplete = true; }
            );

            // Wait a frame to ensure animation cleanup
            yield return null;

            // Clear all player bet chip stacks (the GameObjects were destroyed by animator)
            foreach (var seat in tableManager.seats)
            {
                if (seat != null && seat.betChipStack != null)
                {
                    seat.betChipStack.ClearChips();
                }
            }
        }
        else
        {
            // Fallback: instant collection if no animator
            UnityEngine.Debug.LogWarning("No ChipSweepAnimator - using instant collection");
            foreach (var seat in tableManager.seats)
            {
                if (seat != null)
                {
                    seat.ClearBet();
                }
            }
        }

        // NOW show the pot chips in the center
        UpdatePotDisplay();

        UnityEngine.Debug.Log("Bets collected - pot chips now showing");
    }

    /// <summary>
    /// Run a betting round using the BettingRoundManager
    /// </summary>
    IEnumerator RunBettingRound()
    {
        if (bettingManager == null)
        {
            UnityEngine.Debug.LogError("[PokerGameManager] No BettingRoundManager assigned! Skipping betting.");
            yield break;
        }

        if (activePlayers.Count == 0)
        {
            UnityEngine.Debug.LogWarning("[PokerGameManager] No active players for betting round");
            yield break;
        }


        // If only 1 or 0 players left, award pot immediately
        if (activePlayers.Count <= 1)
        {
            if (activePlayers.Count == 1)
            {
                UnityEngine.Debug.Log($"[BettingRound] Only 1 player remains - awarding pot");
                yield return AwardPotToLastPlayer();
                currentState = GameState.HandComplete;
            }
            else
            {
                UnityEngine.Debug.LogError("[BettingRound] No players remain!");
                currentState = GameState.HandComplete;
            }
            yield break;
        }

        // Determine starting player (first to act)
        int startPlayerIndex;

        if (currentState == GameState.PreFlop)
        {
            // PreFlop: first to act is CLOCKWISE from BB (increment with your array order)
            int bbIndex = activePlayers.IndexOf(bigBlindSeatIndex);
            startPlayerIndex = activePlayers[(bbIndex + 1) % activePlayers.Count];
        }
        else
        {
            // Postflop: first to act is CLOCKWISE from dealer (increment with your array order)
            int dealerIndex = activePlayers.IndexOf(dealerSeatIndex);
            startPlayerIndex = activePlayers[(dealerIndex + 1) % activePlayers.Count];
        }

        UnityEngine.Debug.Log($"[PokerGameManager] Starting betting round - State: {currentState}, Starting player: Seat {startPlayerIndex}");

        // Run the betting round
        IEnumerator bettingCoroutine;

        if (currentState == GameState.PreFlop)
        {
            // PreFlop: pass existing blind bets
            bettingCoroutine = bettingManager.RunBettingRound(
                activePlayers,
                startPlayerIndex,
                pot,
                tableManager.bigBlind,
                playerBets  // Pass blind bets
            );
        }
        else
        {
            // Postflop: reset bets (fresh betting round)
            playerBets.Clear();
            bettingCoroutine = bettingManager.RunBettingRound(
                activePlayers,
                startPlayerIndex,
                pot,
                tableManager.bigBlind,
                null  // No existing bets
            );
        }

        yield return bettingCoroutine;

        // Get result
        BettingRoundResult result = bettingCoroutine.Current as BettingRoundResult;
        if (result != null)
        {
            pot = result.pot;
            activePlayers = result.activePlayers;

            UnityEngine.Debug.Log($"[PokerGameManager] Betting round complete - Pot: ${pot}, Active players: {activePlayers.Count}");

            // Log who remains
            foreach (int seatIndex in activePlayers)
            {
                PlayerSeat seat = tableManager.seats[seatIndex];
                UnityEngine.Debug.Log($"  - {seat.PlayerName} (${seat.ChipCount} remaining)");
            }

            // Check for all-in showdown - everyone is all-in, reveal cards NOW
            if (result.allInShowdown && activePlayers.Count > 1)
            {
                UnityEngine.Debug.Log("[PokerGameManager] ALL-IN SHOWDOWN - Revealing all cards NOW!");
                UnityEngine.Debug.Log($"  Reason: {activePlayers.Count} players remain, all are all-in");
                yield return RevealAllHands();
                yield return new WaitForSeconds(2.0f); // Pause so players can see hands
            }

            // Check if only one player left (everyone else folded)
            if (activePlayers.Count == 1)
            {
                UnityEngine.Debug.Log($"[PokerGameManager] Only one player left - {tableManager.seats[activePlayers[0]].PlayerName} wins!");
                UnityEngine.Debug.Log($"  Reason: All other players folded");
                // Award pot to last player and end hand
                yield return AwardPotToLastPlayer();
                currentState = GameState.HandComplete;
            }
            else if (activePlayers.Count > 1)
            {
                UnityEngine.Debug.Log($"[PokerGameManager] {activePlayers.Count} players remain - continuing to next street");
            }
        }
        else
        {
            UnityEngine.Debug.LogError("[PokerGameManager] Failed to get betting round result!");
        }
    }

    /// <summary>
    /// Reveal all active players' cards (for all-in showdown)
    /// </summary>
    IEnumerator RevealAllHands()
    {
        UnityEngine.Debug.Log("=== Revealing All Hands ===");

        foreach (int seatIndex in activePlayers)
        {
            PlayerSeat seat = tableManager.seats[seatIndex];
            if (seat != null && playerHands.ContainsKey(seatIndex))
            {
                var hand = playerHands[seatIndex];
                if (hand.Count >= 2 && cardDatabase != null)
                {
                    // Show actual cards for everyone with spread animation
                    Sprite card1Sprite = cardDatabase.GetCardSprite(hand[0]);
                    Sprite card2Sprite = cardDatabase.GetCardSprite(hand[1]);
                    seat.RevealCards(card1Sprite, card2Sprite);  // ★ Spread animation at showdown

                    UnityEngine.Debug.Log($"[Showdown] Revealed {seat.PlayerName}'s cards: {hand[0]}, {hand[1]}");
                }
            }
        }

        yield return null;
    }

    /// <summary>
    /// Award pot to last remaining player (when everyone else folds)
    /// </summary>
    IEnumerator AwardPotToLastPlayer()
    {
        if (activePlayers.Count != 1)
        {
            UnityEngine.Debug.LogError("[PokerGameManager] AwardPotToLastPlayer called but multiple players remain!");
            yield break;
        }

        int winnerSeatIndex = activePlayers[0];
        PlayerSeat winner = tableManager.seats[winnerSeatIndex];

        UnityEngine.Debug.Log($"*** {winner.PlayerName} wins ${pot} (all others folded) ***");

        // Show "WINNER" text in green
        winner.ShowAction("WINNER", 3.0f, Color.green);

        // FIRST: Collect all bet chips to pot with animation (if any bets exist)
        // This ensures chips animate to center before going to winner
        yield return CollectBetsToPot();

        // Wait a moment for player to see the pot
        yield return new WaitForSeconds(0.5f);

        // NOW animate pot chips to winner
        if (chipSweepAnimator != null && potChipStack != null && pot > 0)
        {
            yield return chipSweepAnimator.AnimateChipsToWinner(
                potChipStack,
                winner.transform,
                null
            );

            potChipStack.ClearChips();
        }

        // Award chips
        winner.UpdateChips(winner.ChipCount + pot);
        pot = 0;

        yield return new WaitForSeconds(1.5f);
    }

    /// <summary>
    /// Check if everyone remaining is all-in (0-1 players with chips)
    /// </summary>
    bool IsEveryoneAllIn()
    {
        int playersWithChips = 0;

        foreach (int seatIndex in activePlayers)
        {
            PlayerSeat seat = tableManager.seats[seatIndex];
            if (seat != null && seat.ChipCount > 0)
            {
                playersWithChips++;
            }
        }

        // If 0-1 players have chips, everyone else is all-in
        bool allIn = playersWithChips <= 1;

        if (allIn)
        {
            UnityEngine.Debug.Log($"[All-In Check] Only {playersWithChips} player(s) with chips - everyone else all-in!");
        }

        return allIn;
    }

    /// <summary>
    /// Reveal all active players' cards simultaneously for all-in showdown
    /// </summary>
    IEnumerator RevealAllCards()
    {
        UnityEngine.Debug.Log("=== Revealing All Cards for All-In Showdown ===");

        if (cardDatabase == null)
        {
            UnityEngine.Debug.LogError("No card database assigned!");
            yield break;
        }

        // Reveal all cards at the same time
        foreach (int seatIndex in activePlayers)
        {
            PlayerSeat seat = tableManager.seats[seatIndex];

            if (seat != null && playerHands.ContainsKey(seatIndex))
            {
                List<Card> hand = playerHands[seatIndex];

                if (hand.Count >= 2)
                {
                    // Show actual cards for everyone with spread animation
                    Sprite card1Sprite = cardDatabase.GetCardSprite(hand[0]);
                    Sprite card2Sprite = cardDatabase.GetCardSprite(hand[1]);

                    seat.RevealCards(card1Sprite, card2Sprite);  // ★ Spread animation at showdown

                    UnityEngine.Debug.Log($"[Showdown] Revealed {seat.PlayerName}'s cards: {hand[0]} {hand[1]}");
                }
            }
        }

        yield return new WaitForSeconds(0.5f); // Brief pause after revealing
    }

    int GetSeatedPlayerCount()
    {
        int count = 0;
        foreach (var seat in tableManager.seats)
        {
            if (seat != null && seat.IsSeated)
            {
                count++;
            }
        }
        return count;
    }

    // Get player's hand (for UI or debugging)
    public List<Card> GetPlayerHand(int seatIndex)
    {
        if (playerHands.ContainsKey(seatIndex))
        {
            return playerHands[seatIndex];
        }
        return null;
    }

    // Public getters
    public GameState CurrentState => currentState;
    public int Pot => pot;
    public int DealerSeat => dealerSeatIndex;
    public bool IsHandInProgress => isHandInProgress;

    // ============================================================
    // STEP 2: SNAPSHOT SYSTEM - Take a "photograph" of table state
    // ============================================================

    void Update()
    {
        // Test snapshot - Press 'S' key
        TestSnapshot();

        // Test round-trip - Press 'T' key (Step 3)
        TestRoundTrip();

        registrySyncTimer += Time.deltaTime;
        if (registrySyncTimer >= registrySyncInterval)
        {
            SyncTableStateToRegistry();
            registrySyncTimer = 0f;
        }
    }

    /// <summary>
    /// Step 2: Take a snapshot of the current table state
    /// This is READ-ONLY - doesn't change the game at all
    /// </summary>
    public TableState Snapshot()
    {
        TableState state = new TableState();

        // === BASIC INFO ===
        state.tableId = !string.IsNullOrEmpty(tableManager?.tableId) ? tableManager.tableId : savedTableId;
        state.handNumber = handNumber;

        // === DEALER & BLINDS ===
        state.dealerButtonSeat = dealerSeatIndex;
        state.smallBlindSeat = smallBlindSeatIndex;
        state.bigBlindSeat = bigBlindSeatIndex;

        // === POT ===
        state.totalPot = pot;

        // === CURRENT STREET ===
        state.currentStreet = isHandInProgress ? currentState.ToString() : "BetweenHands";  // "PreFlop", "Flop", etc.
        state.bettingComplete = false;

        // === BOARD CARDS ===
        state.boardCards.Clear();
        foreach (Card card in communityCards)
        {
            state.boardCards.Add(card.GetShortName());  // e.g., "Ah", "Kd"
        }

        // === WHOSE TURN ===
        state.currentPlayerSeat = currentPlayerIndex;

        // Get current bet amount from betting manager if available
        if (bettingManager != null)
        {
            // We'll need to expose this from BettingRoundManager later
            state.currentBet = 0;  // TODO: Get from bettingManager
        }

        // === SEATS ===
        state.seats.Clear();
        if (tableManager != null && tableManager.seats != null)
        {
            for (int i = 0; i < tableManager.seats.Count; i++)
            {
                PlayerSeat seat = tableManager.seats[i];
                SeatSnapshot seatSnap = new SeatSnapshot();

                seatSnap.seatIndex = i;
                seatSnap.isOccupied = seat.IsSeated;
                seatSnap.playerName = seat.PlayerName;
                seatSnap.chipCount = seat.ChipCount;

                // ★ FIX: Determine if folded by checking if in activePlayers list
                seatSnap.hasFolded = seat.IsSeated && !activePlayers.Contains(i);

                // ★ FIX: Check all-in by chip count = 0 and was seated
                seatSnap.isAllIn = seat.IsSeated && seat.ChipCount == 0;

                seatSnap.isSittingOut = !seat.IsSeated;  // Simple for now

                // Get current bet for this seat
                if (playerBets.ContainsKey(i))
                {
                    seatSnap.currentBet = playerBets[i];
                }

                // Get hole cards if available
                seatSnap.holeCards.Clear();
                if (playerHands.ContainsKey(i) && playerHands[i] != null)
                {
                    foreach (Card card in playerHands[i])
                    {
                        seatSnap.holeCards.Add(card.GetShortName());
                    }
                }

                state.seats.Add(seatSnap);
            }
        }

        UnityEngine.Debug.Log($"[Snapshot] Captured table state: {state.seats.Count} seats, pot=${state.totalPot}, street={state.currentStreet}");

        return state;
    }

    void SyncTableStateToRegistry()
    {
        string tableId = !string.IsNullOrEmpty(tableManager?.tableId) ? tableManager.tableId : savedTableId;
        if (string.IsNullOrEmpty(tableId))
        {
            return;
        }

        TableRegistry.Instance.UpdateTableState(tableId, Snapshot());
    }

    /// <summary>
    /// Step 3: Apply a snapshot to rebuild the table visuals
    /// This ONLY updates the visuals - doesn't run game logic
    /// </summary>
    public void ApplySnapshot(TableState state)
    {
        UnityEngine.Debug.Log($"[ApplySnapshot] Rebuilding table from snapshot: {state.tableId}");

        handNumber = state.handNumber;

        // === PREPARE DECK FOR REMAINING CARDS ===
        // ★ CRITICAL: Shuffle deck and remove already-dealt cards!
        deck.Reset();
        deck.Shuffle();

        // Remove hole cards that were already dealt (2 per active player)
        int holeCardsDealt = 0;
        foreach (var seat in state.seats)
        {
            if (seat.isOccupied && !seat.hasFolded && seat.holeCards.Count == 2)
            {
                holeCardsDealt += 2;
            }
        }

        // Remove board cards that were already dealt (burn + community)
        int boardCardsDealt = state.boardCards.Count;
        int burnCards = 0;
        if (boardCardsDealt >= 3) burnCards++; // Flop burn
        if (boardCardsDealt >= 4) burnCards++; // Turn burn  
        if (boardCardsDealt >= 5) burnCards++; // River burn

        int totalCardsToRemove = holeCardsDealt + boardCardsDealt + burnCards;
        UnityEngine.Debug.Log($"[ApplySnapshot] Removing {totalCardsToRemove} dealt cards from deck ({holeCardsDealt} hole + {boardCardsDealt} board + {burnCards} burns)");

        // Remove cards from deck (we don't care which specific ones, just reduce deck size)
        for (int i = 0; i < totalCardsToRemove && deck.CardsRemaining > 0; i++)
        {
            deck.Deal();
        }

        UnityEngine.Debug.Log($"[ApplySnapshot] ✓ Deck prepared: {deck.CardsRemaining} cards remaining for Turn/River");

        // === UPDATE DEALER BUTTON POSITION ===
        dealerSeatIndex = state.dealerButtonSeat;
        smallBlindSeatIndex = state.smallBlindSeat;
        bigBlindSeatIndex = state.bigBlindSeat;

        // Update visual dealer button
        if (dealerButtonImage != null && dealerButtonPositions != null && dealerSeatIndex >= 0 && dealerSeatIndex < dealerButtonPositions.Count)
        {
            dealerButtonImage.gameObject.SetActive(true);
            dealerButtonImage.transform.position = dealerButtonPositions[dealerSeatIndex].position;
        }

        // === UPDATE POT ===
        pot = state.totalPot;
        if (potText != null)
        {
            potText.text = state.totalPot > 0 ? $"Pot: ${state.totalPot:#,0}" : "";
        }

        // Show pot chips if there's a pot
        if (potChipStack != null)
        {
            if (state.totalPot > 0)
            {
                potChipStack.ShowChips(state.totalPot);
            }
            else
            {
                potChipStack.ClearChips();
            }
        }

        // === UPDATE BOARD CARDS ===
        // ★ CRITICAL: Validate communityCardImages array first!
        if (communityCardImages == null || communityCardImages.Count == 0)
        {
            UnityEngine.Debug.LogError("[ApplySnapshot] ⚠️⚠️⚠️ CRITICAL: communityCardImages array is NULL or EMPTY!");
            UnityEngine.Debug.LogError("[ApplySnapshot] Cards cannot be displayed! Check Inspector - assign card images to communityCardImages array!");
        }
        else
        {
            UnityEngine.Debug.Log($"[ApplySnapshot] ✓ communityCardImages array has {communityCardImages.Count} slots");
        }

        // ★ CRITICAL: Validate cardDatabase!
        if (cardDatabase == null)
        {
            UnityEngine.Debug.LogError("[ApplySnapshot] ⚠️⚠️⚠️ CRITICAL: cardDatabase is NULL!");
            UnityEngine.Debug.LogError("[ApplySnapshot] Card sprites cannot be assigned! Check Inspector - assign CardSpriteDatabase!");
        }
        else
        {
            UnityEngine.Debug.Log($"[ApplySnapshot] ✓ cardDatabase is assigned");
        }

        communityCards.Clear();
        foreach (string cardStr in state.boardCards)
        {
            Card card = ParseCardFromString(cardStr);
            if (card != null)
            {
                communityCards.Add(card);
            }
        }

        // Display board cards - FORCE THEM VISIBLE!
        UnityEngine.Debug.Log($"[ApplySnapshot] ★★★ FORCING {communityCards.Count} BOARD CARDS VISIBLE ★★★");
        for (int i = 0; i < communityCards.Count; i++)
        {
            if (i < communityCardImages.Count && communityCardImages[i] != null)
            {
                // Force card visible - MULTIPLE ATTEMPTS
                GameObject cardObj = communityCardImages[i].gameObject;

                // Activate parent objects too if they exist
                Transform parent = cardObj.transform.parent;
                if (parent != null && !parent.gameObject.activeSelf)
                {
                    parent.gameObject.SetActive(true);
                    UnityEngine.Debug.Log($"[ApplySnapshot]   Activated parent of card {i}");
                }

                cardObj.SetActive(true);

                // Force again after small delay
                if (!cardObj.activeSelf)
                {
                    UnityEngine.Debug.LogError($"[ApplySnapshot] ⚠️ Card {i} REFUSING to activate!");
                    cardObj.SetActive(true);
                }

                // Set sprite
                if (cardDatabase != null)
                {
                    Sprite cardSprite = cardDatabase.GetCardSprite(communityCards[i]);
                    communityCardImages[i].sprite = cardSprite;
                    UnityEngine.Debug.Log($"[ApplySnapshot]   Card {i}: {communityCards[i]} - active={cardObj.activeSelf}, sprite={(cardSprite != null ? "SET" : "NULL")}, parent={(parent != null ? parent.gameObject.activeSelf.ToString() : "none")}");
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"[ApplySnapshot] No card database!");
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[ApplySnapshot] Card image {i} not available! (communityCardImages.Count={communityCardImages.Count})");
            }
        }

        UnityEngine.Debug.Log($"[ApplySnapshot] Loaded {communityCards.Count} board cards: {string.Join(", ", state.boardCards)}");

        // ★ Start aggressive card monitoring
        StartCoroutine(EnsureCardsVisible());

        // === UPDATE SEATS ===
        if (tableManager != null && tableManager.seats != null)
        {
            for (int i = 0; i < state.seats.Count && i < tableManager.seats.Count; i++)
            {
                SeatSnapshot seatSnap = state.seats[i];
                PlayerSeat seat = tableManager.seats[i];

                if (seatSnap.isOccupied && seat.IsSeated)
                {
                    // Seat has a player in both snapshot and actual table - update it
                    UnityEngine.Debug.Log($"[ApplySnapshot] Updating seat {i}: {seatSnap.playerName}");

                    // Update chip count
                    seat.UpdateChips(seatSnap.chipCount);

                    // Load hole cards if present (and player hasn't folded)
                    if (seatSnap.holeCards != null && seatSnap.holeCards.Count == 2 && !seatSnap.hasFolded)
                    {
                        List<Card> holeCards = new List<Card>();
                        foreach (string cardStr in seatSnap.holeCards)
                        {
                            Card card = ParseCardFromString(cardStr);
                            if (card != null)
                            {
                                holeCards.Add(card);
                            }
                        }

                        if (holeCards.Count == 2)
                        {
                            // Store hole cards in playerHands dictionary
                            playerHands[i] = holeCards;

                            // ★ Show card backs for AI players (face down)
                            if (cardDatabase != null && cardDatabase.cardBackSprite != null)
                            {
                                seat.ShowCardBacks(cardDatabase.cardBackSprite);
                                UnityEngine.Debug.Log($"[ApplySnapshot] Showing card backs for seat {i}: {seatSnap.playerName}");
                            }
                            else
                            {
                                UnityEngine.Debug.LogWarning($"[ApplySnapshot] No card back sprite for seat {i}!");
                            }
                        }
                    }
                    else if (seatSnap.hasFolded)
                    {
                        // Mark folded players (hide their cards)
                        seat.HideCards();
                        UnityEngine.Debug.Log($"[ApplySnapshot] Hiding cards for folded seat {i}: {seatSnap.playerName}");
                    }

                    // Mark all-in players
                    if (seatSnap.isAllIn)
                    {
                        seat.ShowAction("ALL-IN", 999f);
                    }

                    // Show bet chips if they have a current bet
                    if (seatSnap.currentBet > 0)
                    {
                        seat.ShowBet(seatSnap.currentBet);
                    }
                    else
                    {
                        seat.ClearBet();
                    }
                }
                else if (!seatSnap.isOccupied)
                {
                    // Seat is empty in snapshot - make sure it's clear visually
                    seat.HideCards();
                    seat.ClearBet();
                }
            }
        }

        // === UPDATE GAME STATE ===
        // Rebuild active players list
        activePlayers.Clear();
        for (int i = 0; i < state.seats.Count; i++)
        {
            if (state.seats[i].isOccupied && !state.seats[i].hasFolded)
            {
                activePlayers.Add(i);
            }
        }

        // Parse the street back into GameState enum
        GameState targetState = GameState.WaitingForPlayers;
        switch (state.currentStreet)
        {
            case "PreFlop": targetState = GameState.PreFlop; break;
            case "Flop": targetState = GameState.Flop; break;
            case "Turn": targetState = GameState.Turn; break;
            case "River": targetState = GameState.River; break;
            default: targetState = GameState.WaitingForPlayers; break;
        }

        // ★ Stay at the current street - GameLoop will handle advancement
        currentState = targetState;

        // ★ Store bettingComplete flag for GameLoop to check
        bettingCompleteFromSnapshot = state.bettingComplete;

        // ★ DON'T clear bets here - let CollectBetsToPot handle it naturally in GameLoop

        // Set hand in progress flag
        isHandInProgress = (activePlayers.Count >= 2 && state.currentStreet != "BetweenHands");

        // Update current player indicator
        currentPlayerIndex = state.currentPlayerSeat;

        UnityEngine.Debug.Log($"[ApplySnapshot] Set state to {currentState}, bettingComplete={state.bettingComplete}");

        UnityEngine.Debug.Log($"[ApplySnapshot] Table rebuilt: {state.seats.Count(s => s.isOccupied)} players, pot=${state.totalPot}");
    }

    /// <summary>
    /// Helper coroutine to ensure cards stay visible after snapshot load
    /// Runs multiple times to fight against anything trying to hide them!
    /// </summary>
    IEnumerator EnsureCardsVisible()
    {
        // Check multiple times over 2 seconds
        for (int attempt = 0; attempt < 10; attempt++)
        {
            yield return new WaitForSeconds(0.2f);

            UnityEngine.Debug.Log($"[EnsureCardsVisible] Check #{attempt + 1}: {communityCards.Count} cards should be visible");

            bool anyFixed = false;
            for (int i = 0; i < communityCards.Count; i++)
            {
                if (i < communityCardImages.Count && communityCardImages[i] != null)
                {
                    GameObject cardObj = communityCardImages[i].gameObject;

                    if (!cardObj.activeSelf)
                    {
                        UnityEngine.Debug.LogWarning($"[EnsureCardsVisible] ⚠️ Card {i} was HIDDEN! Reactivating... (attempt {attempt + 1})");
                        cardObj.SetActive(true);
                        anyFixed = true;
                    }

                    // Reapply sprite if needed
                    if (communityCardImages[i].sprite == null && cardDatabase != null)
                    {
                        communityCardImages[i].sprite = cardDatabase.GetCardSprite(communityCards[i]);
                        UnityEngine.Debug.LogWarning($"[EnsureCardsVisible] Card {i} sprite was null! Reapplied.");
                        anyFixed = true;
                    }
                }
            }

            if (anyFixed)
            {
                UnityEngine.Debug.Log($"[EnsureCardsVisible] ✓ Fixed hidden cards on attempt {attempt + 1}");
            }
            else if (attempt == 0)
            {
                UnityEngine.Debug.Log($"[EnsureCardsVisible] ✓ All cards still visible");
            }
        }

        UnityEngine.Debug.Log("[EnsureCardsVisible] ✓ Finished monitoring - cards should be stable now");
    }

    /// <summary>
    /// Parse a card from string format like "A♠" or "Ah" or "AS"
    /// </summary>
    private Card ParseCardFromString(string cardStr)
    {
        if (string.IsNullOrEmpty(cardStr) || cardStr.Length < 2)
        {
            return null;
        }

        // Get rank (first part)
        string rankStr = cardStr.Substring(0, cardStr.Length - 1);
        Rank rank = Rank.Two;

        switch (rankStr)
        {
            case "2": rank = Rank.Two; break;
            case "3": rank = Rank.Three; break;
            case "4": rank = Rank.Four; break;
            case "5": rank = Rank.Five; break;
            case "6": rank = Rank.Six; break;
            case "7": rank = Rank.Seven; break;
            case "8": rank = Rank.Eight; break;
            case "9": rank = Rank.Nine; break;
            case "10": rank = Rank.Ten; break;
            case "J": rank = Rank.Jack; break;
            case "Q": rank = Rank.Queen; break;
            case "K": rank = Rank.King; break;
            case "A": rank = Rank.Ace; break;
            default: return null;
        }

        // Get suit (last char)
        char suitChar = cardStr[cardStr.Length - 1];
        Suit suit = Suit.Hearts;

        switch (suitChar)
        {
            case '♠': case 's': case 'S': suit = Suit.Spades; break;
            case '♥': case 'h': case 'H': suit = Suit.Hearts; break;
            case '♦': case 'd': case 'D': suit = Suit.Diamonds; break;
            case '♣': case 'c': case 'C': suit = Suit.Clubs; break;
            default: return null;
        }

        // ★ FIXED: Card constructor is Card(Suit, Rank) not Card(Rank, Suit)
        return new Card(suit, rank);
    }

    /// <summary>
    /// Step 2: Debug helper - Press 'S' key to take a snapshot and print it
    /// </summary>
    void TestSnapshot()
    {
        if (Input.GetKeyDown(KeyCode.S))
        {
            UnityEngine.Debug.Log("=== TAKING SNAPSHOT ===");
            UnityEngine.Debug.Log(">>> S KEY DETECTED <<<");  // Extra debug
            TableState snapshot = Snapshot();

            // Print summary
            UnityEngine.Debug.Log($"Table: {snapshot.tableId}");
            UnityEngine.Debug.Log($"Street: {snapshot.currentStreet}");
            UnityEngine.Debug.Log($"Pot: ${snapshot.totalPot}");
            UnityEngine.Debug.Log($"Dealer Seat: {snapshot.dealerButtonSeat}");
            UnityEngine.Debug.Log($"Board Cards: {string.Join(", ", snapshot.boardCards)}");
            UnityEngine.Debug.Log($"Seats occupied: {snapshot.seats.Count(s => s.isOccupied)}/{snapshot.seats.Count}");

            // Print each seat
            foreach (var seat in snapshot.seats)
            {
                if (seat.isOccupied)
                {
                    string cards = seat.holeCards.Count > 0 ? string.Join("", seat.holeCards) : "??";
                    string status = seat.hasFolded ? " (FOLDED)" : seat.isAllIn ? " (ALL-IN)" : "";
                    UnityEngine.Debug.Log($"  Seat {seat.seatIndex}: {seat.playerName} - ${seat.chipCount} - {cards}{status}");
                }
            }

            UnityEngine.Debug.Log("=== SNAPSHOT COMPLETE ===");
        }
    }

    /// <summary>
    /// Step 3: Test round-trip - Press 'T' to test snapshot/apply cycle
    /// </summary>
    void TestRoundTrip()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            UnityEngine.Debug.Log("=== TESTING ROUND-TRIP ===");

            // Take snapshot of current state
            TableState snapshot = Snapshot();
            UnityEngine.Debug.Log($"[Test] Snapshot taken - Pot: ${snapshot.totalPot}, Street: {snapshot.currentStreet}");

            // Apply it back (should look exactly the same)
            ApplySnapshot(snapshot);
            UnityEngine.Debug.Log($"[Test] Snapshot applied - table should look identical");

            UnityEngine.Debug.Log("=== ROUND-TRIP TEST COMPLETE ===");
            UnityEngine.Debug.Log("If everything looks the same, Step 3 is working!");
        }
    }
}
