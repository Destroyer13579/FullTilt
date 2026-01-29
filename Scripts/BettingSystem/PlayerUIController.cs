using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the local player's betting UI
/// Handles fold, check, call, bet, raise buttons and bet slider
/// </summary>
public class PlayerUIController : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject uiContainer;      // Shows/hides entire UI
    public Button foldButton;
    public Button checkButton;
    public Button callButton;
    public Button betButton;
    public Button raiseButton;

    [Header("Bet Slider")]
    public Slider betSlider;
    public TMP_Text betAmountText;      // Shows current slider value

    [Header("Quick Bet Buttons")]
    public Button halfPotButton;        // Bet 1/2 pot
    public Button potButton;            // Bet pot amount
    public Button maxButton;            // All-in (max bet)

    [Header("Call/Raise Text")]
    public TMP_Text callButtonText;     // "Call $50"
    public TMP_Text raiseButtonText;    // "Raise to $150"
    public TMP_Text betButtonText;      // "Bet $50"

    [Header("Audio - Action Sounds")]
    public AudioClip foldSound;         // Sound for folding
    public AudioClip checkSound;        // Sound for checking
    public AudioClip betCallSound;      // Sound for bet/call
    public AudioClip raiseSound;        // Sound for raising

    [Header("Audio - Your Turn Sound")]
    public AudioClip yourTurnSound;     // Sound when it's the local player's turn (Full Tilt style!)

    private AudioSource audioSource;

    // Internal
    private BettingState currentState;
    private PlayerSeat playerSeat;
    private Action<PlayerActionData> onActionCallback;
    private bool isPlayerTurn = false;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;

        // Setup button listeners
        if (foldButton != null) foldButton.onClick.AddListener(OnFoldClicked);
        if (checkButton != null) checkButton.onClick.AddListener(OnCheckClicked);
        if (callButton != null) callButton.onClick.AddListener(OnCallClicked);
        if (betButton != null) betButton.onClick.AddListener(OnBetClicked);
        if (raiseButton != null) raiseButton.onClick.AddListener(OnRaiseClicked);

        // Setup slider
        if (betSlider != null)
        {
            betSlider.onValueChanged.AddListener(OnSliderChanged);
        }

        // Setup quick bet buttons
        if (halfPotButton != null) halfPotButton.onClick.AddListener(OnHalfPotClicked);
        if (potButton != null) potButton.onClick.AddListener(OnPotClicked);
        if (maxButton != null) maxButton.onClick.AddListener(OnMaxClicked);

        HideUI();
    }

    /// <summary>
    /// Show UI and wait for player action
    /// </summary>
    public void ShowUI(PlayerSeat seat, BettingState state, Action<PlayerActionData> callback)
    {
        UnityEngine.Debug.Log($"[PlayerUI] === ShowUI CALLED ===");
        UnityEngine.Debug.Log($"[PlayerUI] Seat: {seat?.PlayerName ?? "NULL"}");
        UnityEngine.Debug.Log($"[PlayerUI] CanCheck: {state?.canCheck ?? false}");
        UnityEngine.Debug.Log($"[PlayerUI] CanBet: {state?.canBet ?? false}");
        UnityEngine.Debug.Log($"[PlayerUI] CanRaise: {state?.canRaise ?? false}");
        UnityEngine.Debug.Log($"[PlayerUI] AmountToCall: ${state?.amountToCall ?? 0}");
        UnityEngine.Debug.Log($"[PlayerUI] CurrentBet: ${state?.currentBet ?? 0}");
        UnityEngine.Debug.Log($"[PlayerUI] MinimumRaise: ${state?.minimumRaise ?? 0}");

        playerSeat = seat;
        currentState = state;
        onActionCallback = callback;
        isPlayerTurn = true;

        // Check if player is away/disconnected
        PlayerSeatStatus seatStatus = seat.GetComponent<PlayerSeatStatus>();

        // ★ CRITICAL: Don't show UI if player is waiting for next hand (joined mid-hand)
        if (seatStatus != null && seatStatus.isWaitingForNextHand)
        {
            UnityEngine.Debug.Log("[PlayerUI] Player is WAITING FOR NEXT HAND - not showing action buttons");
            isPlayerTurn = false;
            return;
        }

        if (seatStatus != null && seatStatus.isAway)
        {
            UnityEngine.Debug.Log("[PlayerUI] Player is AWAY - auto-playing");
            // Auto-play for away human player
            PlayerActionData autoAction = seatStatus.GetAutoAction(state);

            // Wait a moment so it's not instant
            StartCoroutine(AutoPlayAfterDelay(autoAction, 1.0f));
            return;
        }

        // Play "your turn" sound for local player (Full Tilt style!)
        if (seat.IsLocalPlayer && yourTurnSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(yourTurnSound);
            UnityEngine.Debug.Log("[PlayerUI] Playing 'Your Turn' sound!");
        }

        if (uiContainer != null)
            uiContainer.SetActive(true);

        UpdateButtonStates();
        UpdateSlider();

        UnityEngine.Debug.Log($"[PlayerUI] UI now showing - Check button interactable: {checkButton?.interactable ?? false}");
    }

    IEnumerator AutoPlayAfterDelay(PlayerActionData action, float delay)
    {
        yield return new WaitForSeconds(delay);
        PerformAction(action);
    }

    public void HideUI()
    {
        isPlayerTurn = false;

        if (uiContainer != null)
            uiContainer.SetActive(false);
    }

    void UpdateButtonStates()
    {
        if (currentState == null)
        {
            UnityEngine.Debug.LogWarning("[PlayerUI] UpdateButtonStates called but currentState is NULL!");
            // Hide all buttons if no state
            if (foldButton != null) foldButton.gameObject.SetActive(false);
            if (checkButton != null) checkButton.gameObject.SetActive(false);
            if (callButton != null) callButton.gameObject.SetActive(false);
            if (betButton != null) betButton.gameObject.SetActive(false);
            if (raiseButton != null) raiseButton.gameObject.SetActive(false);
            return;
        }

        if (playerSeat == null)
        {
            UnityEngine.Debug.LogWarning("[PlayerUI] UpdateButtonStates called but playerSeat is NULL!");
            return;
        }

        UnityEngine.Debug.Log($"[PlayerUI] UpdateButtonStates - canCheck: {currentState.canCheck}, amountToCall: ${currentState.amountToCall}");

        // Position 1: CHECK or FOLD (mutually exclusive - same position!)
        if (currentState.canCheck)
        {
            // Can check → show CHECK, hide FOLD
            if (checkButton != null)
            {
                checkButton.gameObject.SetActive(true);
                checkButton.interactable = true;
                UnityEngine.Debug.Log($"[PlayerUI] CHECK button active");
            }
            if (foldButton != null)
            {
                foldButton.gameObject.SetActive(false);
                UnityEngine.Debug.Log($"[PlayerUI] FOLD button hidden (can check)");
            }
        }
        else
        {
            // Facing bet → show FOLD, hide CHECK
            if (foldButton != null)
            {
                foldButton.gameObject.SetActive(true);
                foldButton.interactable = true;
                UnityEngine.Debug.Log($"[PlayerUI] FOLD button active");
            }
            if (checkButton != null)
            {
                checkButton.gameObject.SetActive(false);
                UnityEngine.Debug.Log($"[PlayerUI] CHECK button hidden (facing bet)");
            }
        }

        // Position 2: BET or CALL (mutually exclusive - same position!)
        // Position 3: RAISE (shows when you can raise)
        if (currentState.canCheck)
        {
            // You can check (no bet to call)
            // But check if there's already a bet on table (e.g., you're big blind)
            if (currentState.currentBet > 0)
            {
                // ★ Big blind scenario: There IS a bet (your blind), so show RAISE not BET
                UnityEngine.Debug.Log($"[PlayerUI] Big Blind Scenario - currentBet: ${currentState.currentBet}, showing RAISE button");

                if (betButton != null)
                    betButton.gameObject.SetActive(false);
                if (callButton != null)
                    callButton.gameObject.SetActive(false);
                if (raiseButton != null)
                {
                    bool canRaise = playerSeat.ChipCount > 0;
                    raiseButton.gameObject.SetActive(canRaise);
                    raiseButton.interactable = canRaise;

                    // Update button text to show "Raise to $X"
                    if (raiseButtonText != null && betSlider != null)
                    {
                        int raiseAmount = Mathf.RoundToInt(betSlider.value);
                        raiseButtonText.text = $"Raise to ${raiseAmount:#,0}";
                        UnityEngine.Debug.Log($"[PlayerUI] Set raise button text: Raise to ${raiseAmount:#,0}");
                    }

                    UnityEngine.Debug.Log($"[PlayerUI] RAISE button active (big blind): {canRaise}");
                }
            }
            else
            {
                // ★ Normal scenario: No bet yet, show BET
                UnityEngine.Debug.Log($"[PlayerUI] Normal betting - currentBet: ${currentState.currentBet}, showing BET button");

                if (betButton != null)
                {
                    bool canBet = playerSeat.ChipCount > 0;
                    betButton.gameObject.SetActive(canBet);
                    betButton.interactable = canBet;

                    // Update button text to show "Bet $X"
                    if (betButtonText != null && betSlider != null)
                    {
                        int betAmount = Mathf.RoundToInt(betSlider.value);
                        betButtonText.text = $"Bet ${betAmount:#,0}";
                        UnityEngine.Debug.Log($"[PlayerUI] Set bet button text: Bet ${betAmount:#,0}");
                    }

                    UnityEngine.Debug.Log($"[PlayerUI] BET button active: {canBet}");
                }
                if (callButton != null)
                {
                    callButton.gameObject.SetActive(false);
                    UnityEngine.Debug.Log($"[PlayerUI] CALL button hidden (no bet to call)");
                }
                if (raiseButton != null)
                {
                    raiseButton.gameObject.SetActive(false);
                    UnityEngine.Debug.Log($"[PlayerUI] RAISE button hidden (no bet yet)");
                }
            }
        }
        else
        {
            // Facing bet → show CALL and RAISE
            if (callButton != null)
            {
                // ★ FIX: Allow calling with less chips (all-in call)
                // Player can call as long as they have ANY chips and there's a bet to call
                bool canCall = currentState.amountToCall > 0 && playerSeat.ChipCount > 0;
                callButton.gameObject.SetActive(canCall);
                callButton.interactable = canCall;

                if (callButtonText != null && currentState.amountToCall > 0)
                {
                    int callAmount = Mathf.Min(currentState.amountToCall, playerSeat.ChipCount);
                    // Show "Call $X (All-In)" if calling with all remaining chips
                    if (callAmount == playerSeat.ChipCount)
                    {
                        callButtonText.text = $"Call ${callAmount} (All-In)";
                    }
                    else
                    {
                        callButtonText.text = $"Call ${callAmount}";
                    }
                }
                UnityEngine.Debug.Log($"[PlayerUI] CALL button active: {canCall}");
            }
            if (betButton != null)
            {
                betButton.gameObject.SetActive(false);
                UnityEngine.Debug.Log($"[PlayerUI] BET button hidden (facing bet)");
            }
            if (raiseButton != null)
            {
                bool canRaise = currentState.canRaise && playerSeat.ChipCount > currentState.minimumRaise;
                raiseButton.gameObject.SetActive(canRaise);
                raiseButton.interactable = canRaise;
                UnityEngine.Debug.Log($"[PlayerUI] RAISE button active: {canRaise}");
            }
        }
    }

    void UpdateSlider()
    {
        if (betSlider == null) return;

        int minBet = currentState.canRaise ?
            currentState.currentBet + currentState.minimumRaise :
            currentState.minimumRaise;

        int maxBet = playerSeat.ChipCount;

        betSlider.minValue = minBet;
        betSlider.maxValue = maxBet;
        betSlider.value = minBet;

        OnSliderChanged(betSlider.value);
    }

    void OnSliderChanged(float value)
    {
        int amount = Mathf.RoundToInt(value);

        if (betAmountText != null)
        {
            betAmountText.text = $"${amount:#,0}";
        }

        // Update button text based on what's showing
        if (currentState != null)
        {
            // If we can check but there's a bet (big blind scenario), show raise text
            if (currentState.canCheck && currentState.currentBet > 0)
            {
                if (raiseButtonText != null)
                {
                    raiseButtonText.text = $"Raise to ${amount:#,0}";
                    UnityEngine.Debug.Log($"[PlayerUI Slider] Updated RAISE text: Raise to ${amount:#,0}");
                }
            }
            // Normal betting scenario
            else if (currentState.canBet && betButtonText != null)
            {
                betButtonText.text = $"Bet ${amount:#,0}";
                UnityEngine.Debug.Log($"[PlayerUI Slider] Updated BET text: Bet ${amount:#,0}");
            }
            // Facing a bet, can raise
            else if (currentState.canRaise && raiseButtonText != null)
            {
                raiseButtonText.text = $"Raise to ${amount:#,0}";
                UnityEngine.Debug.Log($"[PlayerUI Slider] Updated RAISE text (facing bet): Raise to ${amount:#,0}");
            }
        }
    }

    // === QUICK BET BUTTONS ===

    void OnHalfPotClicked()
    {
        if (betSlider == null || playerSeat == null) return;

        // Calculate half of player's chip stack
        int halfStack = Mathf.RoundToInt(playerSeat.ChipCount / 2f);

        // Clamp to valid bet range
        int minBet = (int)betSlider.minValue;
        int maxBet = (int)betSlider.maxValue;
        int betAmount = Mathf.Clamp(halfStack, minBet, maxBet);

        // Set slider
        betSlider.value = betAmount;

        UnityEngine.Debug.Log($"[QuickBet] 1/2 Stack: ${betAmount} (chips: ${playerSeat.ChipCount})");
    }

    void OnPotClicked()
    {
        if (betSlider == null || currentState == null) return;

        // Calculate pot amount
        int potAmount = currentState.pot;

        // Clamp to valid bet range
        int minBet = (int)betSlider.minValue;
        int maxBet = (int)betSlider.maxValue;
        int betAmount = Mathf.Clamp(potAmount, minBet, maxBet);

        // Set slider
        betSlider.value = betAmount;

        UnityEngine.Debug.Log($"[QuickBet] Pot: ${betAmount} (pot: ${currentState.pot})");
    }

    void OnMaxClicked()
    {
        if (betSlider == null) return;

        // Set to maximum (all-in)
        betSlider.value = betSlider.maxValue;

        UnityEngine.Debug.Log($"[QuickBet] MAX (All-in): ${betSlider.maxValue}");
    }

    void OnFoldClicked()
    {
        // Silently ignore if UI not properly initialized
        if (playerSeat == null) return;

        PerformAction(new PlayerActionData(PokerAction.Fold, 0, playerSeat.PlayerName));
    }

    void OnCheckClicked()
    {
        // Silently ignore if UI not properly initialized
        if (playerSeat == null) return;

        PerformAction(new PlayerActionData(PokerAction.Check, 0, playerSeat.PlayerName));
    }

    void OnCallClicked()
    {
        // Silently ignore if UI not properly initialized
        if (playerSeat == null) return;

        int callAmount = Mathf.Min(currentState.amountToCall, playerSeat.ChipCount);
        PokerAction action = (callAmount == playerSeat.ChipCount) ? PokerAction.AllIn : PokerAction.Call;
        PerformAction(new PlayerActionData(action, callAmount, playerSeat.PlayerName));
    }

    void OnBetClicked()
    {
        // Silently ignore if UI not properly initialized
        if (playerSeat == null || currentState == null) return;

        int betAmount = Mathf.RoundToInt(betSlider.value);

        // Determine correct action type
        PokerAction action;
        if (betAmount == playerSeat.ChipCount)
        {
            // All chips → ALL IN
            action = PokerAction.AllIn;
        }
        else if (currentState.currentBet > 0)
        {
            // ★ FIX: There's already a bet (e.g., blinds) → This is a RAISE, not a BET
            action = PokerAction.Raise;
        }
        else
        {
            // No bet yet → This is a BET
            action = PokerAction.Bet;
        }

        PerformAction(new PlayerActionData(action, betAmount, playerSeat.PlayerName));
    }

    void OnRaiseClicked()
    {
        // Silently ignore if UI not properly initialized
        if (playerSeat == null) return;

        int raiseAmount = Mathf.RoundToInt(betSlider.value);
        PokerAction action = (raiseAmount == playerSeat.ChipCount) ? PokerAction.AllIn : PokerAction.Raise;
        PerformAction(new PlayerActionData(action, raiseAmount, playerSeat.PlayerName));
    }

    void PerformAction(PlayerActionData action)
    {
        // Play appropriate sound based on action type
        if (audioSource != null)
        {
            AudioClip soundToPlay = null;

            switch (action.action)
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

        HideUI();
        onActionCallback?.Invoke(action);
    }
}
