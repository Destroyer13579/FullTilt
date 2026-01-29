using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles Full Tilt Poker style card dealing animation
/// Cards fly from deck position to player seats
/// </summary>
public class CardDealAnimator : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("How long cards take to fly to players (seconds)")]
    public float dealDuration = 0.3f;

    [Tooltip("Animation curve for card movement (smooth ease)")]
    public AnimationCurve dealCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Card Visuals")]
    [Tooltip("Card back sprite (what players see flying)")]
    public Sprite cardBackSprite;

    [Tooltip("Parent canvas for temporary card objects")]
    public Canvas parentCanvas;

    [Header("Prefab")]
    [Tooltip("Simple Image prefab for animated cards")]
    public GameObject cardImagePrefab;

    /// <summary>
    /// Animate a card dealing from deck to player seat
    /// </summary>
    public IEnumerator AnimateCardDeal(Transform deckPosition, Transform playerSeatTransform, System.Action onComplete = null)
    {
        if (deckPosition == null || playerSeatTransform == null)
        {
            UnityEngine.Debug.LogWarning("[CardDeal] Missing deck or player position");
            onComplete?.Invoke();
            yield break;
        }

        if (cardBackSprite == null)
        {
            UnityEngine.Debug.LogWarning("[CardDeal] No card back sprite assigned - skipping animation");
            onComplete?.Invoke();
            yield break;
        }

        // Create temporary card image
        GameObject tempCard = null;
        
        if (cardImagePrefab != null)
        {
            // Use prefab if provided
            tempCard = Instantiate(cardImagePrefab, parentCanvas != null ? parentCanvas.transform : transform);
        }
        else
        {
            // Create simple image
            tempCard = new GameObject("TempDealCard");
            tempCard.transform.SetParent(parentCanvas != null ? parentCanvas.transform : transform);
            Image img = tempCard.AddComponent<Image>();
            img.sprite = cardBackSprite;
            
            // Set size (standard card size)
            RectTransform rt = tempCard.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(71, 96); // Standard poker card ratio
        }

        Image cardImage = tempCard.GetComponent<Image>();
        if (cardImage != null)
        {
            cardImage.sprite = cardBackSprite;
        }

        RectTransform cardRect = tempCard.GetComponent<RectTransform>();

        // Get start and end positions
        Vector3 startPos = deckPosition.position;
        Vector3 endPos = playerSeatTransform.position;

        // Animate card flying
        float elapsed = 0f;
        while (elapsed < dealDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dealDuration;

            // Apply curve for smooth movement
            float curvedT = dealCurve.Evaluate(t);

            // Move card
            cardRect.position = Vector3.Lerp(startPos, endPos, curvedT);

            // Optional: Add slight rotation for style
            float rotation = Mathf.Lerp(0f, 15f, t);
            cardRect.localRotation = Quaternion.Euler(0, 0, rotation);

            yield return null;
        }

        // Ensure final position
        cardRect.position = endPos;

        // Destroy temporary card
        Destroy(tempCard);

        // Callback
        onComplete?.Invoke();
    }
}
