using System;
using System.Collections;  // ★ For IEnumerator (coroutines)
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerSeat : MonoBehaviour
{
    [Header("Seat Info")]
    public int seatIndex;  // 0-8 for 9-max table

    [Header("UI Elements")]
    public Image emptySeatImage;      // The grey desk (shown when empty)
    public Image avatarImage;          // Player avatar (shown when occupied)
    public TMP_Text nameText;          // Player name or "RESERVED"
    public TMP_Text chipCountText;     // Chip count
    public Button seatButton;          // Click to sit down
    public GameObject seatUIContainer; // Container for name/chips (hide when empty)

    [Header("Cards")]
    public Image card1Image;
    public Image card2Image;
    public GameObject cardsContainer;  // Hide when no cards

    [Header("Card Spread Settings")]
    [Tooltip("How far apart cards spread horizontally when revealed (pixels)")]
    public float cardSpreadDistance = 30f;
    [Tooltip("How far up cards move when revealed (pixels)")]
    public float cardSpreadUpDistance = 15f;
    [Tooltip("How fast cards spread (seconds)")]
    public float cardSpreadDuration = 0.3f;

    // Store original card positions
    private Vector2 card1OriginalPos;
    private Vector2 card2OriginalPos;
    private bool cardPositionsStored = false;
    private bool cardsHaveBeenSpread = false;  // ★ Track if spread animation already played this hand

    [Header("Chips")]
    public ChipStack betChipStack;  // Displays bet chips in front of seat

    [Header("Turn Indicator")]
    public GameObject turnIndicator;  // Bright glow effect when it's this player's turn

    [Header("Sprites")]
    public Sprite emptySeatSprite;     // Grey desk sprite
    public Sprite occupiedSeatSprite;  // Optional different sprite when occupied

    [Header("State")]
    public SeatState currentState = SeatState.Empty;

    // Data
    private string playerId;
    private string playerName;
    private int chipCount;
    private int avatarId;
    private bool isLocalPlayer = false;
    private bool isAllIn = false;  // ★ Track if player is all-in

    // ★ Consistent color for ALL IN and actions
    private static readonly Color ACTION_COLOR = new Color(0f, 0.75f, 1f); // Cyan-blue (Full Tilt style)

    // Events
    public event Action<PlayerSeat> OnSeatClicked;

    // Reference to avatar database (set by TableManager)
    private AvatarDatabase avatarDatabase;

    void Start()
    {
        Debug.Log($"PlayerSeat {seatIndex} starting. Button: {seatButton != null}");

        if (seatButton != null)
        {
            seatButton.onClick.AddListener(OnClick);
        }

        // Turn indicator starts disabled
        if (turnIndicator != null)
        {
            turnIndicator.SetActive(false);
        }

        // IMPORTANT: Make sure cards are hidden initially (prevents white boxes)
        if (cardsContainer != null)
        {
            cardsContainer.SetActive(false);
        }

        // ★ Store original card positions for spread animation
        if (card1Image != null && card2Image != null && !cardPositionsStored)
        {
            card1OriginalPos = card1Image.rectTransform.anchoredPosition;
            card2OriginalPos = card2Image.rectTransform.anchoredPosition;
            cardPositionsStored = true;
        }

        // FIXED: Dont clear seats if already occupied
        if (currentState == SeatState.Empty || string.IsNullOrEmpty(playerName))
        {
            SetState(SeatState.Empty);
        }
        else
        {
            UnityEngine.Debug.Log($"PlayerSeat {seatIndex} already occupied by {playerName}");
            SetState(currentState);
        }
    }

    public void Initialize(AvatarDatabase database)
    {
        avatarDatabase = database;
    }

    // === STATE MANAGEMENT ===

    public void SetState(SeatState newState)
    {
        currentState = newState;

        switch (newState)
        {
            case SeatState.Empty:
                ShowEmptySeat();
                break;
            case SeatState.Reserved:
                ShowReservedSeat();
                break;
            case SeatState.Seated:
                ShowSeatedPlayer();
                break;
        }
    }

    void ShowEmptySeat()
    {
        // Empty = just invisible clickable area (grey desk is HIDDEN)
        if (emptySeatImage != null)
            emptySeatImage.gameObject.SetActive(false);

        // Hide avatar and player info
        if (avatarImage != null)
            avatarImage.gameObject.SetActive(false);

        if (seatUIContainer != null)
            seatUIContainer.SetActive(false);

        if (cardsContainer != null)
            cardsContainer.SetActive(false);

        // Enable clicking
        if (seatButton != null)
            seatButton.interactable = true;
    }

    void ShowReservedSeat()
    {
        // Show grey desk
        if (emptySeatImage != null)
        {
            emptySeatImage.gameObject.SetActive(true);
            if (emptySeatSprite != null)
                emptySeatImage.sprite = emptySeatSprite;
        }

        // Show avatar
        if (avatarImage != null)
            avatarImage.gameObject.SetActive(true);

        // Show UI container with "RESERVED"
        if (seatUIContainer != null)
            seatUIContainer.SetActive(true);

        if (nameText != null)
        {
            nameText.text = "RESERVED";
            nameText.fontSize = 20;
        }

        if (chipCountText != null)
            chipCountText.text = "";

        // Hide cards
        if (cardsContainer != null)
            cardsContainer.SetActive(false);

        // Disable clicking while reserved
        if (seatButton != null)
            seatButton.interactable = false;
    }

    void ShowSeatedPlayer()
    {
        // Show grey desk
        if (emptySeatImage != null)
        {
            emptySeatImage.gameObject.SetActive(true);
            if (emptySeatSprite != null)
                emptySeatImage.sprite = emptySeatSprite;
        }

        // Show avatar
        if (avatarImage != null)
            avatarImage.gameObject.SetActive(true);

        // Show player info
        if (seatUIContainer != null)
            seatUIContainer.SetActive(true);

        if (nameText != null)
        {
            nameText.text = playerName;
            nameText.fontSize = 25;
            UnityEngine.Debug.Log($"[PlayerSeat {seatIndex}] Set {playerName} font size to 18 (was {nameText.fontSize})");
        }

        if (chipCountText != null)
            chipCountText.text = $"${chipCount:#,0}";

        // IMPORTANT: Hide cards until they're dealt!
        if (cardsContainer != null)
            cardsContainer.SetActive(false);

        // Disable clicking (seat is taken)
        if (seatButton != null)
            seatButton.interactable = false;
    }

    // === PLAYER MANAGEMENT ===

    public void ReserveSeat(string id, string name, int avatar)
    {
        playerId = id;
        playerName = name;
        avatarId = avatar;

        // Set avatar image
        UpdateAvatarDisplay();

        SetState(SeatState.Reserved);
    }

    public void SeatPlayer(string id, string name, int chips, int avatar, bool isLocal = false)
    {
        playerId = id;
        playerName = name;
        chipCount = chips;
        avatarId = avatar;
        isLocalPlayer = isLocal;

        // Set avatar image
        UpdateAvatarDisplay();

        // FORCE font size immediately
        if (nameText != null)
        {
            nameText.fontSize = 18;
            nameText.enableAutoSizing = false;  // Disable auto-sizing
            UnityEngine.Debug.Log($"[PlayerSeat {seatIndex}] SeatPlayer: Forced {name} font to 18, isLocal={isLocal}");
        }

        SetState(SeatState.Seated);

        // ★★★ CHECK IF JOINING MID-HAND ★★★
        if (isLocal)
        {
            PokerGameManager gameManager = FindObjectOfType<PokerGameManager>();
            if (gameManager != null && gameManager.IsHandInProgress)
            {
                // Hand is in progress - mark as waiting
                PlayerSeatStatus status = GetComponent<PlayerSeatStatus>();
                if (status != null)
                {
                    status.isWaitingForNextHand = true;
                    // ★ Removed "WAITING" text - it's ugly and unnecessary
                    // Player will just sit quietly until next hand
                    UnityEngine.Debug.Log($"[PlayerSeat] ⏸ {name} joining MID-HAND - will be dealt in next hand");
                }
            }
        }
    }

    public void UpdateChips(int newAmount)
    {
        int previousChipCount = chipCount;
        chipCount = newAmount;

        // ★ FIX: Detect all-in - when chips go from >0 to 0, player went all-in
        if (previousChipCount > 0 && chipCount == 0)
        {
            isAllIn = true;
        }

        if (chipCountText != null && currentState == SeatState.Seated)
        {
            // ★ CRITICAL: Don't update text if there's an active action display (e.g., WINNER)
            // The action display coroutine will restore chip count when it's done
            if (actionDisplayCoroutine != null)
            {
                // Action is displaying (WINNER, FOLD, etc.) - don't override it!
                // Just update the chipCount variable, text will update when action finishes
                return;
            }

            // ★ Show ALL IN if player is all-in with 0 chips
            if (isAllIn && chipCount == 0)
            {
                chipCountText.text = "ALL IN";
                chipCountText.color = ACTION_COLOR;  // Consistent cyan-blue
            }
            else
            {
                chipCountText.text = $"${chipCount:#,0}";
                chipCountText.color = Color.white; // Normal color

                // ★ Clear all-in flag if player gets chips again
                if (chipCount > 0 && isAllIn)
                {
                    isAllIn = false;
                }
            }
        }
    }

    /// <summary>
    /// Update chip count value WITHOUT changing the display
    /// Used when ShowAction is displaying something (like "WINNER")
    /// </summary>
    public void UpdateChipsValue(int newAmount)
    {
        chipCount = newAmount;

        // Clear all-in flag if player gets chips
        if (chipCount > 0 && isAllIn)
        {
            isAllIn = false;
        }
    }

    public void AddChips(int amount)
    {
        UpdateChips(chipCount + amount);
    }

    public void RemoveChips(int amount)
    {
        UpdateChips(Mathf.Max(0, chipCount - amount));
    }

    // Store the action display coroutine reference
    private Coroutine actionDisplayCoroutine;

    /// <summary>
    /// Show an action (CALL, BET, RAISE, FOLD, CHECK) in the chip count area
    /// Full Tilt Poker style - action shows in light blue, then reverts to chip count
    /// </summary>
    public void ShowAction(string actionText, float displayDuration = 2.0f)
    {
        ShowAction(actionText, displayDuration, ACTION_COLOR);
    }

    /// <summary>
    /// Show an action with custom color (e.g., WINNER in green)
    /// </summary>
    public void ShowAction(string actionText, float displayDuration, Color customColor)
    {
        if (chipCountText == null) return;

        // ★ Track if player is all-in
        if (actionText.ToUpper().Contains("ALL IN"))
        {
            isAllIn = true;
        }

        // Stop ONLY the previous action display coroutine (not ALL coroutines!)
        if (actionDisplayCoroutine != null)
        {
            StopCoroutine(actionDisplayCoroutine);
        }

        // Show action in specified color
        actionDisplayCoroutine = StartCoroutine(DisplayActionCoroutine(actionText, displayDuration, customColor));
    }

    private System.Collections.IEnumerator DisplayActionCoroutine(string actionText, float duration, Color actionColor)
    {
        // Show action text in specified color
        chipCountText.text = actionText.ToUpper();
        chipCountText.color = actionColor;

        // Wait
        yield return new WaitForSeconds(duration);

        // Restore chip count display
        if (currentState == SeatState.Seated)
        {
            // ★ FIX: Keep showing ALL IN if player is all-in, otherwise show chip count
            if (isAllIn && chipCount == 0)
            {
                chipCountText.text = "ALL IN";
                chipCountText.color = ACTION_COLOR;  // Consistent cyan-blue
            }
            else
            {
                chipCountText.text = $"${chipCount:#,0}";
                chipCountText.color = Color.white;  // Always restore to white
            }
        }

        // Clear the coroutine reference
        actionDisplayCoroutine = null;
    }

    public void ClearSeat()
    {
        playerId = null;
        playerName = null;
        chipCount = 0;
        avatarId = 0;
        isLocalPlayer = false;
        isAllIn = false;  // ★ Reset all-in flag

        // FIXED: Dont clear seats if already occupied
        if (currentState == SeatState.Empty || string.IsNullOrEmpty(playerName))
        {
            SetState(SeatState.Empty);
        }
        else
        {
            UnityEngine.Debug.Log($"PlayerSeat {seatIndex} already occupied by {playerName}");
            SetState(currentState);
        }
    }

    void UpdateAvatarDisplay()
    {
        if (avatarImage == null || avatarDatabase == null)
            return;

        var avatarData = avatarDatabase.GetAvatar(avatarId);
        if (avatarData != null)
        {
            avatarImage.sprite = avatarData.AvatarSprite;
            avatarImage.color = avatarData.AvatarColor;
        }
    }

    // === CARDS ===

    public void ShowCards(Sprite card1, Sprite card2)
    {
        if (cardsContainer != null)
            cardsContainer.SetActive(true);

        if (card1Image != null)
            card1Image.sprite = card1;

        if (card2Image != null)
            card2Image.sprite = card2;

        // Cards show in normal position (overlapped)
        ResetCardPositions();
    }

    /// <summary>
    /// Reveal cards with spread animation (for showdowns only)
    /// </summary>
    public void RevealCards(Sprite card1, Sprite card2)
    {
        if (cardsContainer != null)
            cardsContainer.SetActive(true);

        if (card1Image != null)
            card1Image.sprite = card1;

        if (card2Image != null)
            card2Image.sprite = card2;

        // ★ Only animate spread ONCE per hand (don't repeat on each community card)
        if (!cardsHaveBeenSpread)
        {
            cardsHaveBeenSpread = true;
            StartCoroutine(SpreadCardsAnimation());
        }
    }

    public void HideCards()
    {
        if (cardsContainer != null)
            cardsContainer.SetActive(false);

        // ★ Reset card positions to original
        ResetCardPositions();

        // ★ Reset spread flag for next hand
        cardsHaveBeenSpread = false;
    }

    /// <summary>
    /// Animates cards spreading apart when revealed (Full Tilt style!)
    /// </summary>
    private IEnumerator SpreadCardsAnimation()
    {
        if (card1Image == null || card2Image == null) yield break;

        RectTransform card1Rect = card1Image.rectTransform;
        RectTransform card2Rect = card2Image.rectTransform;

        // Start from original positions
        card1Rect.anchoredPosition = card1OriginalPos;
        card2Rect.anchoredPosition = card2OriginalPos;

        // Calculate target positions (spread apart and up)
        Vector2 card1Target = card1OriginalPos + new Vector2(-cardSpreadDistance, cardSpreadUpDistance);
        Vector2 card2Target = card2OriginalPos + new Vector2(cardSpreadDistance, cardSpreadUpDistance);

        float elapsed = 0f;

        while (elapsed < cardSpreadDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / cardSpreadDuration);

            // Smooth ease out curve
            float smoothT = 1f - (1f - t) * (1f - t);

            // Interpolate positions
            card1Rect.anchoredPosition = Vector2.Lerp(card1OriginalPos, card1Target, smoothT);
            card2Rect.anchoredPosition = Vector2.Lerp(card2OriginalPos, card2Target, smoothT);

            yield return null;
        }

        // Ensure final positions
        card1Rect.anchoredPosition = card1Target;
        card2Rect.anchoredPosition = card2Target;
    }

    /// <summary>
    /// Resets card positions back to overlapped state
    /// </summary>
    private void ResetCardPositions()
    {
        if (card1Image != null)
            card1Image.rectTransform.anchoredPosition = card1OriginalPos;

        if (card2Image != null)
            card2Image.rectTransform.anchoredPosition = card2OriginalPos;
    }

    // === TURN INDICATOR ===

    public void ShowTurnIndicator()
    {
        if (turnIndicator != null)
        {
            turnIndicator.SetActive(true);
            UnityEngine.Debug.Log($"[TurnIndicator] {PlayerName} turn indicator ON");
        }
    }

    public void HideTurnIndicator()
    {
        if (turnIndicator != null)
        {
            turnIndicator.SetActive(false);
            UnityEngine.Debug.Log($"[TurnIndicator] {PlayerName} turn indicator OFF");
        }
    }

    public void ShowCardBacks(Sprite cardBack)
    {
        if (cardsContainer != null)
            cardsContainer.SetActive(true);

        if (card1Image != null)
            card1Image.sprite = cardBack;

        if (card2Image != null)
            card2Image.sprite = cardBack;
    }

    // ★ Reset all-in state (called at start of new hand)
    public void ResetAllInState()
    {
        isAllIn = false;
    }

    // === BET CHIPS ===

    public void ShowBet(int amount)
    {
        if (betChipStack != null)
        {
            betChipStack.ShowChips(amount);
        }
    }

    public void ClearBet()
    {
        if (betChipStack != null)
        {
            betChipStack.ClearChips();
        }

        // ★ FIX: Reset all-in flag when clearing bets (hand ended)
        isAllIn = false;
    }

    public void UpdateBet(int newAmount)
    {
        if (betChipStack != null)
        {
            betChipStack.UpdateChips(newAmount);
        }
    }

    // === INTERACTION ===

    void OnClick()
    {
        Debug.Log($"Seat {seatIndex} clicked! State: {currentState}");
        if (currentState == SeatState.Empty)
        {
            OnSeatClicked?.Invoke(this);
        }
    }

    // === GETTERS ===

    public bool IsEmpty => currentState == SeatState.Empty;
    public bool IsOccupied => currentState == SeatState.Seated;
    public bool IsSeated => currentState == SeatState.Seated;
    public bool IsReserved => currentState == SeatState.Reserved;
    public string PlayerId => playerId;
    public string PlayerName => playerName;
    public int ChipCount => chipCount;
    public bool IsLocalPlayer => isLocalPlayer;
}

public enum SeatState
{
    Empty,
    Reserved,
    Seated
}
