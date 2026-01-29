using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;  // ★ For bet amount text

/// <summary>
/// Fixed version - spawns chips properly as UI children
/// </summary>
public class ChipStack : MonoBehaviour
{
    [Header("References")]
    public ChipSpriteDatabase chipDatabase;
    public Transform chipContainer;
    public TMP_Text betAmountText;  // ★ Shows "$20" next to chip stack

    [Header("Visual Settings")]
    [Tooltip("Vertical offset between stacked chips (pixels)")]
    public float stackOffset = 6f;  // ★ Tighter stacks

    [Tooltip("Horizontal spacing between different chip types")]
    public float chipTypeSpacing = 18f;  // ★ More compact grouping

    [Tooltip("Scale of chip sprites")]
    public float chipScale = 0.85f;  // ★ Slightly smaller for better grouping

    [Header("Bet Amount Text Positioning")]
    [Tooltip("Horizontal offset from right edge of chip stack (pixels)")]
    public float textOffsetX = 25f;  // ★ Closer to chips

    [Tooltip("Vertical offset from center of chip stack (pixels)")]
    public float textOffsetY = 0f;

    [Header("Debug")]
    public bool showDebugLogs = false;

    // Internal
    private List<GameObject> activeChips = new List<GameObject>();
    private int currentAmount = 0;

    void Start()
    {
        if (chipContainer == null)
            chipContainer = transform;
    }

    /// <summary>
    /// Display chips for a specific amount
    /// </summary>
    public void ShowChips(int amount)
    {
        if (showDebugLogs)
            UnityEngine.Debug.Log($"[ChipStack] ShowChips: ${amount:#,0} on {gameObject.name}");

        if (amount == currentAmount)
        {
            if (showDebugLogs)
                UnityEngine.Debug.Log($"[ChipStack] Already showing ${amount}, skipping");
            return;
        }

        ClearChips();
        currentAmount = amount;

        if (amount <= 0)
        {
            if (showDebugLogs)
                UnityEngine.Debug.Log($"[ChipStack] Amount is 0 or negative, nothing to show");
            return;
        }

        if (chipDatabase == null)
        {
            UnityEngine.Debug.LogError($"[ChipStack] No chipDatabase assigned on {gameObject.name}!");
            return;
        }

        // Get chip breakdown
        var breakdown = chipDatabase.GetChipBreakdown(amount);

        if (breakdown.Count == 0)
        {
            UnityEngine.Debug.LogWarning($"[ChipStack] Cannot break down ${amount} into available denominations!");
            return;
        }

        if (showDebugLogs)
            UnityEngine.Debug.Log($"[ChipStack] Breakdown: {breakdown.Count} different denominations");

        float xPos = 0f;
        int totalChipsCreated = 0;

        // Create chips for each denomination
        foreach (var (value, count) in breakdown)
        {
            Sprite chipSprite = chipDatabase.GetChipSprite(value);
            if (chipSprite == null)
            {
                UnityEngine.Debug.LogWarning($"[ChipStack] No sprite for ${value} chip!");
                continue;
            }

            // Stack chips of same denomination
            for (int i = 0; i < count; i++)
            {
                GameObject chipObj = CreateChipImage(chipSprite, xPos, i * stackOffset);
                if (chipObj != null)
                {
                    activeChips.Add(chipObj);
                    totalChipsCreated++;
                }
            }

            // Move to next chip type position
            xPos += chipTypeSpacing;
        }

        if (showDebugLogs)
            UnityEngine.Debug.Log($"[ChipStack] Created {totalChipsCreated} chip GameObjects");

        // ★ Update bet amount text label and position it next to chip stack
        if (betAmountText != null)
        {
            betAmountText.text = $"${amount:#,0}";
            betAmountText.gameObject.SetActive(true);

            // Position text to the right of the chip stack
            RectTransform textRect = betAmountText.rectTransform;
            if (textRect != null)
            {
                // Position relative to BetChips (xPos = total chip stack width)
                Vector2 textPosition = new Vector2(xPos + textOffsetX, textOffsetY);
                textRect.anchoredPosition = textPosition;

                // Ensure text is on top
                betAmountText.transform.SetAsLastSibling();

                if (showDebugLogs)
                    UnityEngine.Debug.Log($"[ChipStack] Text positioned at ({textPosition.x}, {textPosition.y})");
            }
        }
    }

    /// <summary>
    /// Create a single chip image as a UI element
    /// </summary>
    GameObject CreateChipImage(Sprite sprite, float xPos, float yPos)
    {
        // Create new GameObject
        GameObject chipObj = new GameObject("Chip");
        chipObj.transform.SetParent(chipContainer, false); // FALSE is important for UI!

        // Add Image component
        Image img = chipObj.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false; // Don't block clicks
        img.SetNativeSize(); // Use sprite's native size

        // Setup RectTransform for UI positioning
        RectTransform rt = chipObj.GetComponent<RectTransform>();
        if (rt != null)
        {
            // Center anchors
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            // Position relative to parent
            rt.anchoredPosition = new Vector2(xPos, yPos);
            rt.localScale = Vector3.one * chipScale;
        }

        if (showDebugLogs)
            UnityEngine.Debug.Log($"[ChipStack] Created chip at local pos ({xPos}, {yPos})");

        return chipObj;
    }

    /// <summary>
    /// Clear all displayed chips
    /// </summary>
    public void ClearChips()
    {
        if (showDebugLogs && activeChips.Count > 0)
            UnityEngine.Debug.Log($"[ChipStack] Clearing {activeChips.Count} chips from {gameObject.name}");

        foreach (var chip in activeChips)
        {
            if (chip != null)
                Destroy(chip);
        }
        activeChips.Clear();
        currentAmount = 0;

        // ★ Hide bet amount text
        if (betAmountText != null)
        {
            betAmountText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Update the chip display
    /// </summary>
    public void UpdateChips(int newAmount)
    {
        ShowChips(newAmount);
    }

    void OnDestroy()
    {
        ClearChips();
    }

    // Public getter
    public int CurrentAmount => currentAmount;

    /// <summary>
    /// Get all active chip GameObjects (for animation)
    /// </summary>
    public List<GameObject> GetActiveChipObjects()
    {
        return new List<GameObject>(activeChips);
    }
}
