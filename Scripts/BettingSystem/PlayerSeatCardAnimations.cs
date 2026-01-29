using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Extension methods for PlayerSeat to add card folding animation
/// Add this script to your project, and the FoldCards() method will be available
/// </summary>
public static class PlayerSeatCardAnimations
{
    /// <summary>
    /// Animate cards folding (Full Tilt style - cards fold into each other and sink)
    /// Call this as an extension method: seat.FoldCards();
    /// </summary>
    public static void FoldCards(this PlayerSeat seat)
    {
        if (seat == null)
        {
            UnityEngine.Debug.LogError("[FoldCards] SEAT IS NULL!");
            return;
        }

        UnityEngine.Debug.Log($"[FoldCards] FoldCards() called for {seat.PlayerName}");

        // Start the animation coroutine
        seat.StartCoroutine(AnimateFoldCards(seat));
    }

    private static IEnumerator AnimateFoldCards(PlayerSeat seat)
    {
        UnityEngine.Debug.Log($"[FoldCards] ===== STARTING FOLD FOR {seat.PlayerName} =====");

        // Get card images
        Image card1 = seat.card1Image;
        Image card2 = seat.card2Image;

        UnityEngine.Debug.Log($"[FoldCards] card1Image: {(card1 != null ? "EXISTS" : "NULL")}");
        UnityEngine.Debug.Log($"[FoldCards] card2Image: {(card2 != null ? "EXISTS" : "NULL")}");

        if (card1 == null || card2 == null)
        {
            UnityEngine.Debug.LogError($"[FoldCards] {seat.PlayerName} has NULL card images! card1={card1}, card2={card2}");
            yield break;
        }

        // Check card GameObject state
        UnityEngine.Debug.Log($"[FoldCards] card1.gameObject: {card1.gameObject.name}, active: {card1.gameObject.activeInHierarchy}");
        UnityEngine.Debug.Log($"[FoldCards] card2.gameObject: {card2.gameObject.name}, active: {card2.gameObject.activeInHierarchy}");

        // Check cardsContainer state
        if (seat.cardsContainer != null)
        {
            UnityEngine.Debug.Log($"[FoldCards] cardsContainer: {seat.cardsContainer.name}, active: {seat.cardsContainer.activeInHierarchy}");
        }
        else
        {
            UnityEngine.Debug.LogWarning($"[FoldCards] cardsContainer is NULL");
        }

        // Only skip animation if BOTH cards are already hidden
        if (!card1.gameObject.activeInHierarchy && !card2.gameObject.activeInHierarchy)
        {
            UnityEngine.Debug.LogWarning($"[FoldCards] {seat.PlayerName} BOTH cards already hidden - skipping animation");
            yield break;
        }

        UnityEngine.Debug.Log($"[FoldCards] ✓ Starting fold animation for {seat.PlayerName}");

        float foldDuration = 0.3f;
        float sinkDuration = 0.2f;

        // Store original positions and scales
        Vector3 card1StartPos = card1.transform.localPosition;
        Vector3 card2StartPos = card2.transform.localPosition;
        Vector3 originalScale = card1.transform.localScale;

        // Calculate midpoint between cards
        Vector3 midPoint = (card1StartPos + card2StartPos) / 2f;

        // Phase 1: Fold cards into each other (move to midpoint)
        UnityEngine.Debug.Log($"[FoldCards] PHASE 1 START: Folding cards to midpoint");
        float elapsed = 0f;
        while (elapsed < foldDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / foldDuration;

            // Ease out curve for smooth movement
            float smoothT = 1f - Mathf.Pow(1f - t, 3f);

            card1.transform.localPosition = Vector3.Lerp(card1StartPos, midPoint, smoothT);
            card2.transform.localPosition = Vector3.Lerp(card2StartPos, midPoint, smoothT);

            yield return null;
        }

        // Ensure cards are at midpoint
        card1.transform.localPosition = midPoint;
        card2.transform.localPosition = midPoint;

        UnityEngine.Debug.Log($"[FoldCards] PHASE 1 COMPLETE: Cards at midpoint");

        // Phase 2: Sink down and fade out
        UnityEngine.Debug.Log($"[FoldCards] PHASE 2 START: Sinking and fading");
        elapsed = 0f;
        Vector3 sinkTarget = midPoint + new Vector3(0, -30f, 0); // Sink down 30 pixels
        Color startColor = card1.color;

        while (elapsed < sinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / sinkDuration;

            // Move down
            Vector3 currentPos = Vector3.Lerp(midPoint, sinkTarget, t);
            card1.transform.localPosition = currentPos;
            card2.transform.localPosition = currentPos;

            // Shrink slightly
            float scale = Mathf.Lerp(1f, 0.7f, t);
            card1.transform.localScale = originalScale * scale;
            card2.transform.localScale = originalScale * scale;

            // Fade out
            Color fadeColor = startColor;
            fadeColor.a = Mathf.Lerp(1f, 0f, t);
            card1.color = fadeColor;
            card2.color = fadeColor;

            yield return null;
        }

        UnityEngine.Debug.Log($"[FoldCards] PHASE 2 COMPLETE: Animation finished, hiding cards");

        // Hide cards completely
        card1.gameObject.SetActive(false);
        card2.gameObject.SetActive(false);

        // Reset properties for next time
        card1.transform.localPosition = card1StartPos;
        card2.transform.localPosition = card2StartPos;
        card1.transform.localScale = originalScale;
        card2.transform.localScale = originalScale;
        card1.color = startColor;
        card2.color = startColor;

        UnityEngine.Debug.Log($"[FoldCards] {seat.PlayerName} cards folded and hidden");
    }

    /// <summary>
    /// Force cards to be shown - call this after fold animation to re-enable cards
    /// </summary>
    public static void ResetCards(this PlayerSeat seat)
    {
        if (seat == null) return;

        Image card1 = seat.card1Image;
        Image card2 = seat.card2Image;

        if (card1 != null)
        {
            card1.gameObject.SetActive(true);
            Color c = card1.color;
            c.a = 1f;
            card1.color = c;
        }

        if (card2 != null)
        {
            card2.gameObject.SetActive(true);
            Color c = card2.color;
            c.a = 1f;
            card2.color = c;
        }
    }
}
