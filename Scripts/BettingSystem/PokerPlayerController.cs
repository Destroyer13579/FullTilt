using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SMART AI POKER PLAYER - Actually Looks At Cards!
/// Evaluates hand strength, position, pot odds
/// Makes intelligent decisions based on actual cards
/// </summary>
public class PokerPlayerController : MonoBehaviour
{
    [Header("AI Settings")]
    [Tooltip("Minimum time AI takes to act (seconds)")]
    public float minThinkTime = 0.5f;

    [Tooltip("Maximum time AI takes to act (seconds)")]
    public float maxThinkTime = 3.0f;

    [Tooltip("Fast action percentage (check/fold quickly)")]
    [Range(0f, 1f)]
    public float fastActionChance = 0.3f;

    [Header("Audio - Action Sounds")]
    public AudioClip foldSound;
    public AudioClip checkSound;
    public AudioClip betCallSound;
    public AudioClip raiseSound;
    private AudioSource audioSource;

    private PlayerSeat seat;
    private AIPlayer aiPlayerData;

    // Store hole cards for decision-making
    private List<Card> holeCards = new List<Card>();

    void Start()
    {
        seat = GetComponent<PlayerSeat>();

        // Setup AudioSource for AI action sounds
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.volume = 1.0f;
        audioSource.spatialBlend = 0;
    }

    public void SetAIPlayerData(AIPlayer data)
    {
        aiPlayerData = data;
        UnityEngine.Debug.Log($"[PokerPlayerController] Linked to AIPlayer: {data.PlayerName} ({data.Playstyle})");
    }

    /// <summary>
    /// Set the AI's hole cards so it can evaluate hand strength
    /// Call this when dealing cards!
    /// </summary>
    public void SetHoleCards(List<Card> cards)
    {
        holeCards = new List<Card>(cards);
        UnityEngine.Debug.Log($"[AI] {seat?.PlayerName} received cards: {cards[0].GetShortName()} {cards[1].GetShortName()}");
    }

    public IEnumerator RequestAction(BettingState state, PlayerUIController playerUI, System.Action<PlayerActionData> callback)
    {
        if (seat == null)
        {
            seat = GetComponent<PlayerSeat>();
        }

        if (seat.IsLocalPlayer)
        {
            // HUMAN PLAYER - show UI
            yield return HandleHumanAction(state, playerUI, callback);
        }
        else
        {
            // AI PLAYER - make decision
            yield return HandleAIAction(state, callback);
        }
    }

    IEnumerator HandleHumanAction(BettingState state, PlayerUIController playerUI, System.Action<PlayerActionData> callback)
    {
        bool actionReceived = false;
        PlayerActionData action = null;

        playerUI.ShowUI(seat, state, (a) =>
        {
            action = a;
            actionReceived = true;
        });

        // Wait for player to act
        while (!actionReceived)
        {
            yield return null;
        }

        callback?.Invoke(action);
    }

    IEnumerator HandleAIAction(BettingState state, System.Action<PlayerActionData> callback)
    {
        // Check if player is away/disconnected
        PlayerSeatStatus seatStatus = seat.GetComponent<PlayerSeatStatus>();
        if (seatStatus != null && seatStatus.isAway)
        {
            PlayerActionData autoAction = seatStatus.GetAutoAction(state);
            callback?.Invoke(autoAction);
            yield break;
        }

        // Determine think time
        bool isFastAction = UnityEngine.Random.value < fastActionChance;
        float thinkTime = isFastAction ?
            UnityEngine.Random.Range(0.3f, 0.8f) :
            UnityEngine.Random.Range(minThinkTime, maxThinkTime);

        // Wait to simulate thinking
        yield return new WaitForSeconds(thinkTime);

        // Make SMART decision
        PlayerActionData action = DecideActionSmart(state);

        // Play sound
        PlayActionSound(action.action);

        // Return decision
        callback?.Invoke(action);
    }

    /// <summary>
    /// SMART AI DECISION ENGINE
    /// Actually evaluates hand strength and makes intelligent decisions
    /// </summary>
    PlayerActionData DecideActionSmart(BettingState state)
    {
        int chipCount = seat.ChipCount;
        int pot = state.pot;

        // Get personality traits
        AIPersonality personality = GetPersonality();

        // CRITICAL: Evaluate actual hand strength
        float handStrength = EvaluateHandStrength();

        UnityEngine.Debug.Log($"[AI] {seat.PlayerName} ({aiPlayerData?.Playstyle}) deciding...");
        UnityEngine.Debug.Log($"  Hand: {GetHandDescription()}");
        UnityEngine.Debug.Log($"  Strength: {handStrength:F2}, Chips: ${chipCount}, Pot: ${pot}, Bet: ${state.currentBet}");

        // === SPECIAL CASE: Can check for free ===
        if (state.canCheck)
        {
            return HandleFreeCheck(personality, state, pot, chipCount, handStrength);
        }

        // === SPECIAL CASE: Not enough chips to call ===
        if (state.amountToCall >= chipCount)
        {
            return HandleShortStacked(personality, chipCount, handStrength);
        }

        // === MAIN DECISION TREE ===
        // Calculate pot odds
        float potOdds = (float)state.amountToCall / (pot + state.amountToCall);

        // Decide main action category
        if (ShouldFold(handStrength, potOdds, personality, state))
        {
            return new PlayerActionData(PokerAction.Fold, 0, seat.PlayerName);
        }
        else if (ShouldRaise(handStrength, personality, state, chipCount))
        {
            return HandleRaise(personality, state, pot, chipCount, handStrength);
        }
        else
        {
            return HandleCall(state, chipCount);
        }
    }

    /// <summary>
    /// Evaluate actual hand strength based on hole cards
    /// Returns 0.0 (trash) to 1.0 (premium)
    /// </summary>
    float EvaluateHandStrength()
    {
        if (holeCards == null || holeCards.Count < 2)
        {
            // No cards - return random
            return UnityEngine.Random.Range(0.3f, 0.7f);
        }

        Card card1 = holeCards[0];
        Card card2 = holeCards[1];

        // Pocket pairs
        if (card1.rank == card2.rank)
        {
            return EvaluatePocketPair(card1.rank);
        }

        // High cards
        bool suited = (card1.suit == card2.suit);
        return EvaluateHighCards(card1.rank, card2.rank, suited);
    }

    /// <summary>
    /// Evaluate pocket pair strength
    /// </summary>
    float EvaluatePocketPair(Rank rank)
    {
        switch (rank)
        {
            case Rank.Ace: return 1.0f;  // AA - absolute premium
            case Rank.King: return 0.95f; // KK - premium
            case Rank.Queen: return 0.90f; // QQ - premium
            case Rank.Jack: return 0.85f; // JJ - strong
            case Rank.Ten: return 0.80f; // TT - strong
            case Rank.Nine: return 0.70f; // 99 - good
            case Rank.Eight: return 0.65f; // 88 - good
            case Rank.Seven: return 0.60f; // 77 - playable
            case Rank.Six: return 0.55f; // 66 - marginal
            case Rank.Five: return 0.50f; // 55 - marginal
            case Rank.Four: return 0.45f; // 44 - weak
            case Rank.Three: return 0.40f; // 33 - weak
            case Rank.Two: return 0.35f; // 22 - very weak
            default: return 0.50f;
        }
    }

    /// <summary>
    /// Evaluate high card hand strength
    /// </summary>
    float EvaluateHighCards(Rank rank1, Rank rank2, bool suited)
    {
        // Get rank values (2=2, 3=3, ..., J=11, Q=12, K=13, A=14)
        int val1 = (int)rank1 + 2;
        int val2 = (int)rank2 + 2;

        // Make sure val1 is higher
        if (val2 > val1)
        {
            int temp = val1;
            val1 = val2;
            val2 = temp;
        }

        float strength = 0f;

        // Premium hands
        if (val1 == 14 && val2 == 13) strength = 0.88f; // AK
        else if (val1 == 14 && val2 == 12) strength = 0.82f; // AQ
        else if (val1 == 14 && val2 == 11) strength = 0.76f; // AJ
        else if (val1 == 13 && val2 == 12) strength = 0.78f; // KQ
        else if (val1 == 13 && val2 == 11) strength = 0.72f; // KJ
        else if (val1 == 12 && val2 == 11) strength = 0.68f; // QJ

        // Strong aces
        else if (val1 == 14 && val2 >= 10) strength = 0.70f; // AT, A9
        else if (val1 == 14 && val2 >= 8) strength = 0.60f;  // A8, A7
        else if (val1 == 14) strength = 0.50f;               // A6 and below

        // Face cards
        else if (val1 >= 11 && val2 >= 10) strength = 0.65f; // JT, KT, QT
        else if (val1 >= 11 && val2 >= 9) strength = 0.55f;  // J9, K9, etc

        // Connected cards (straight potential)
        else if (Mathf.Abs(val1 - val2) == 1 && val1 >= 10) strength = 0.60f; // T9, 98, etc (high)
        else if (Mathf.Abs(val1 - val2) == 1) strength = 0.45f; // Connected but low

        // Everything else
        else
        {
            // Base strength on high card
            strength = (val1 / 14f) * 0.4f + (val2 / 14f) * 0.2f;
        }

        // Suited bonus (flush potential)
        if (suited)
        {
            strength += 0.05f;
        }

        return Mathf.Clamp01(strength);
    }

    string GetHandDescription()
    {
        if (holeCards == null || holeCards.Count < 2) return "Unknown";
        return $"{holeCards[0].GetShortName()}{holeCards[1].GetShortName()}";
    }

    /// <summary>
    /// Handle decision when check is free
    /// </summary>
    PlayerActionData HandleFreeCheck(AIPersonality personality, BettingState state, int pot, int chipCount, float handStrength)
    {
        // With strong hands, bet more often
        // With weak hands, check more often
        float betChance = personality.aggression * handStrength;

        if (UnityEngine.Random.value < betChance)
        {
            // Bet! Choose size based on personality and hand strength
            int betAmount = ChooseBetSize(personality, pot, chipCount, state.minimumRaise, handStrength);

            // ★ FIX: If there's already a bet (e.g., we're big blind), this is a RAISE not a BET
            PokerAction action = (state.currentBet > 0) ? PokerAction.Raise : PokerAction.Bet;

            UnityEngine.Debug.Log($"[AI] {seat.PlayerName} {(action == PokerAction.Raise ? "raising" : "betting")} ${betAmount} with {GetHandDescription()}");
            return new PlayerActionData(action, betAmount, seat.PlayerName);
        }
        else
        {
            // Check (free!)
            UnityEngine.Debug.Log($"[AI] {seat.PlayerName} checking with {GetHandDescription()}");
            return new PlayerActionData(PokerAction.Check, 0, seat.PlayerName);
        }
    }

    /// <summary>
    /// Handle decision when short-stacked
    /// </summary>
    PlayerActionData HandleShortStacked(AIPersonality personality, int chipCount, float handStrength)
    {
        // Strong hands = more likely to go all-in
        // Weak hands = more likely to fold
        float allInThreshold = 0.4f - (personality.looseness * 0.2f);

        if (handStrength > allInThreshold)
        {
            UnityEngine.Debug.Log($"[AI] {seat.PlayerName} going ALL-IN with {GetHandDescription()}! (strength: {handStrength:F2})");
            return new PlayerActionData(PokerAction.AllIn, chipCount, seat.PlayerName);
        }
        else
        {
            UnityEngine.Debug.Log($"[AI] {seat.PlayerName} folding {GetHandDescription()} (too weak for all-in)");
            return new PlayerActionData(PokerAction.Fold, 0, seat.PlayerName);
        }
    }

    /// <summary>
    /// Determine if should fold
    /// </summary>
    bool ShouldFold(float handStrength, float potOdds, AIPersonality personality, BettingState state)
    {
        // Very strong hands almost never fold
        if (handStrength > 0.85f) return false;

        // Calculate fold threshold based on personality
        // Tight players fold more, loose players fold less
        float baseFoldThreshold = 0.5f - (personality.looseness * 0.25f);

        // Adjust for bet size (big bets = more folds)
        float betSizeRatio = (float)state.amountToCall / seat.ChipCount;
        if (betSizeRatio > 0.3f)
        {
            baseFoldThreshold += 0.1f; // More likely to fold to big bets
        }

        // Very weak hands should fold
        if (handStrength < baseFoldThreshold)
        {
            UnityEngine.Debug.Log($"[AI] {seat.PlayerName} folding weak hand {GetHandDescription()} (strength: {handStrength:F2})");
            return true;
        }

        // Bad pot odds with mediocre hand = fold
        if (handStrength < 0.6f && potOdds > 0.4f)
        {
            UnityEngine.Debug.Log($"[AI] {seat.PlayerName} folding {GetHandDescription()} (bad pot odds)");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determine if should raise
    /// </summary>
    bool ShouldRaise(float handStrength, AIPersonality personality, BettingState state, int chipCount)
    {
        // Must have enough chips
        if (chipCount <= state.currentBet + state.minimumRaise)
            return false;

        // Strong hands raise more often
        // Weak hands rarely raise
        float raiseThreshold = 0.65f - (personality.looseness * 0.15f);

        if (handStrength < raiseThreshold)
        {
            // Hand too weak to raise
            return false;
        }

        // Calculate raise chance based on hand strength and aggression
        float raiseChance = (handStrength - raiseThreshold) * personality.aggression * 2f;

        // Maniacs raise more
        if (personality.maniacLevel > 0.5f)
            raiseChance += 0.3f;

        bool shouldRaise = UnityEngine.Random.value < raiseChance;

        if (shouldRaise)
        {
            UnityEngine.Debug.Log($"[AI] {seat.PlayerName} deciding to raise with {GetHandDescription()} (strength: {handStrength:F2})");
        }

        return shouldRaise;
    }

    /// <summary>
    /// Handle raising
    /// </summary>
    PlayerActionData HandleRaise(AIPersonality personality, BettingState state, int pot, int chipCount, float handStrength)
    {
        int raiseAmount = ChooseRaiseSize(personality, state, pot, chipCount, handStrength);

        // Check for all-in
        if (raiseAmount >= chipCount)
        {
            UnityEngine.Debug.Log($"[AI] {seat.PlayerName} going ALL-IN (raise) with {GetHandDescription()}! ${chipCount}");
            return new PlayerActionData(PokerAction.AllIn, chipCount, seat.PlayerName);
        }

        UnityEngine.Debug.Log($"[AI] {seat.PlayerName} raising to ${raiseAmount} with {GetHandDescription()}");
        return new PlayerActionData(PokerAction.Raise, raiseAmount, seat.PlayerName);
    }

    /// <summary>
    /// Handle calling
    /// </summary>
    PlayerActionData HandleCall(BettingState state, int chipCount)
    {
        int callAmount = Mathf.Min(state.amountToCall, chipCount);

        if (callAmount == chipCount)
        {
            UnityEngine.Debug.Log($"[AI] {seat.PlayerName} calling ALL-IN with {GetHandDescription()}! ${chipCount}");
            return new PlayerActionData(PokerAction.AllIn, callAmount, seat.PlayerName);
        }
        else
        {
            UnityEngine.Debug.Log($"[AI] {seat.PlayerName} calling ${callAmount} with {GetHandDescription()}");
            return new PlayerActionData(PokerAction.Call, callAmount, seat.PlayerName);
        }
    }

    /// <summary>
    /// Choose bet size based on hand strength and personality
    /// </summary>
    int ChooseBetSize(AIPersonality personality, int pot, int chipCount, int minimumBet, float handStrength)
    {
        int betAmount = minimumBet;

        // Strong hands bet bigger
        if (handStrength > 0.8f)
        {
            // Premium hands - pot to 2x pot
            float sizeMultiplier = UnityEngine.Random.Range(0.8f, 2.0f);
            betAmount = Mathf.Max((int)(pot * sizeMultiplier), minimumBet);
        }
        else if (handStrength > 0.6f)
        {
            // Good hands - half pot to pot
            float sizeMultiplier = UnityEngine.Random.Range(0.5f, 1.2f);
            betAmount = Mathf.Max((int)(pot * sizeMultiplier), minimumBet);
        }
        else
        {
            // Marginal hands - minimum to half pot
            float sizeMultiplier = UnityEngine.Random.Range(0.3f, 0.6f);
            betAmount = Mathf.Max((int)(pot * sizeMultiplier), minimumBet);
        }

        // Maniacs bet bigger
        if (personality.maniacLevel > 0.7f)
        {
            betAmount = (int)(betAmount * 1.5f);
        }

        // Clamp to chips available
        betAmount = Mathf.Min(betAmount, chipCount);
        betAmount = Mathf.Max(betAmount, minimumBet);

        return betAmount;
    }

    /// <summary>
    /// Choose raise size based on hand strength and personality
    /// </summary>
    int ChooseRaiseSize(AIPersonality personality, BettingState state, int pot, int chipCount, float handStrength)
    {
        int currentBet = state.currentBet;
        int minimumRaise = state.minimumRaise;
        int minimumTotal = currentBet + minimumRaise;

        int raiseAmount = minimumTotal;

        // Strong hands raise bigger
        if (handStrength > 0.85f)
        {
            // Premium hands - 3x to pot-sized or all-in
            if (personality.maniacLevel > 0.7f && UnityEngine.Random.value < 0.3f)
            {
                raiseAmount = chipCount; // All-in!
            }
            else
            {
                raiseAmount = Mathf.Max(pot, minimumTotal + minimumRaise * 2);
            }
        }
        else if (handStrength > 0.7f)
        {
            // Strong hands - 2x-3x or pot-sized
            raiseAmount = minimumTotal + minimumRaise * UnityEngine.Random.Range(1, 3);
        }
        else
        {
            // Decent hands - minimum to 2x
            raiseAmount = minimumTotal + (int)(minimumRaise * UnityEngine.Random.Range(0f, 1.5f));
        }

        // Aggressive players raise more
        if (personality.aggression > 0.7f)
        {
            raiseAmount = (int)(raiseAmount * UnityEngine.Random.Range(1.1f, 1.3f));
        }

        // Clamp to valid range
        raiseAmount = Mathf.Min(raiseAmount, chipCount);
        raiseAmount = Mathf.Max(raiseAmount, minimumTotal);

        return raiseAmount;
    }

    /// <summary>
    /// Adjust hand strength perception based on personality
    /// </summary>
    float AdjustHandStrengthByPersonality(float baseStrength, AIPersonality personality)
    {
        // Loose players overvalue weak hands
        float looseFactor = personality.looseness * 0.15f;
        float adjusted = baseStrength + looseFactor;

        // Maniacs play almost everything
        if (personality.maniacLevel > 0.7f)
        {
            adjusted += 0.15f;
        }

        return Mathf.Clamp01(adjusted);
    }

    /// <summary>
    /// Get personality traits from AIPlayer data
    /// </summary>
    AIPersonality GetPersonality()
    {
        if (aiPlayerData == null)
        {
            return new AIPersonality
            {
                aggression = 0.5f,
                looseness = 0.5f,
                maniacLevel = 0f,
                skillLevel = 0.5f
            };
        }

        AIPersonality p = new AIPersonality();
        p.skillLevel = aiPlayerData.SkillLevel;

        switch (aiPlayerData.Playstyle)
        {
            case AIPlaystyle.TightAggressive:
                p.aggression = 0.75f;
                p.looseness = 0.25f;
                p.maniacLevel = 0.1f;
                break;

            case AIPlaystyle.LooseAggressive:
                p.aggression = 0.85f;
                p.looseness = 0.75f;
                p.maniacLevel = 0.3f;
                break;

            case AIPlaystyle.TightPassive:
                p.aggression = 0.25f;
                p.looseness = 0.20f;
                p.maniacLevel = 0f;
                break;

            case AIPlaystyle.LoosePassive:
                p.aggression = 0.30f;
                p.looseness = 0.80f;
                p.maniacLevel = 0.05f;
                break;

            case AIPlaystyle.Maniac:
                p.aggression = 0.95f;
                p.looseness = 0.95f;
                p.maniacLevel = 1.0f;
                break;
        }

        // Add random variance (±10%)
        p.aggression *= UnityEngine.Random.Range(0.9f, 1.1f);
        p.looseness *= UnityEngine.Random.Range(0.9f, 1.1f);
        p.maniacLevel *= UnityEngine.Random.Range(0.9f, 1.1f);

        p.aggression = Mathf.Clamp01(p.aggression);
        p.looseness = Mathf.Clamp01(p.looseness);
        p.maniacLevel = Mathf.Clamp01(p.maniacLevel);

        return p;
    }

    void PlayActionSound(PokerAction action)
    {
        if (audioSource == null) return;

        AudioClip soundToPlay = null;

        switch (action)
        {
            case PokerAction.Fold:
                soundToPlay = foldSound;
                break;
            case PokerAction.Check:
                soundToPlay = checkSound;
                break;
            case PokerAction.Call:
            case PokerAction.Bet:
                soundToPlay = betCallSound;
                break;
            case PokerAction.Raise:
            case PokerAction.AllIn:
                soundToPlay = raiseSound;
                break;
        }

        if (soundToPlay != null)
        {
            audioSource.PlayOneShot(soundToPlay);
        }
    }
}

public struct AIPersonality
{
    public float aggression;
    public float looseness;
    public float maniacLevel;
    public float skillLevel;
}
