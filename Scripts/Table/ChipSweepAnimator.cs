using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles the Full Tilt style chip sliding animation
/// ★ FIXED: Chips slide as solid stacks, all seats move simultaneously
/// </summary>
public class ChipSweepAnimator : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("How long chips take to slide to pot (seconds)")]
    public float sweepDuration = 0.6f;

    [Tooltip("Animation curve for chip movement (smooth ease)")]
    public AnimationCurve sweepCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Audio")]
    [Tooltip("Sound effect when chips slide to pot")]
    public AudioClip chipSweepSound;

    [Tooltip("Sound effect when winner collects pot")]
    public AudioClip chipWinSound;

    [Tooltip("Volume for sweep sound")]
    [Range(0f, 1f)]
    public float soundVolume = 0.7f;

    private AudioSource audioSource;

    void Start()
    {
        // Get or create AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
    }

    /// <summary>
    /// Animates all bet chips sliding to pot, then updates pot display
    /// ★ FIX: Moves entire chip stacks as solid units, all simultaneously
    /// </summary>
    public IEnumerator AnimateChipsToPot(List<PlayerSeat> seats, Transform potPosition, System.Action onComplete)
    {
        // Collect all seat chip stacks that have bets
        List<(Transform chipStackTransform, Vector3 startPos, List<GameObject> chipsToDestroy)> chipStacks = new List<(Transform, Vector3, List<GameObject>)>();

        foreach (var seat in seats)
        {
            if (seat == null || seat.betChipStack == null) continue;

            // Check if this seat has any chips to animate
            var chips = seat.betChipStack.GetActiveChipObjects();
            if (chips.Count > 0)
            {
                // Get the chip container (the parent holding all chips)
                Transform container = seat.betChipStack.chipContainer != null
                    ? seat.betChipStack.chipContainer
                    : seat.betChipStack.transform;

                // Store container, start position, AND the chip GameObjects to destroy later
                chipStacks.Add((container, container.position, new List<GameObject>(chips)));
            }
        }

        if (chipStacks.Count == 0)
        {
            UnityEngine.Debug.Log("[ChipSweep] No chips to animate - skipping sound");
            onComplete?.Invoke();
            yield break;
        }

        // ONLY play sound if there are chips to collect
        if (chipSweepSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(chipSweepSound, soundVolume);
        }

        UnityEngine.Debug.Log($"[ChipSweep] Animating {chipStacks.Count} chip stacks to pot (all at once)");

        // ★ Start all chip stack animations SIMULTANEOUSLY (no stagger)
        List<Coroutine> animCoroutines = new List<Coroutine>();

        foreach (var (container, startPos, _) in chipStacks)
        {
            animCoroutines.Add(StartCoroutine(AnimateStackTransform(container, startPos, potPosition.position)));
        }

        // Wait for animation to finish
        yield return new WaitForSeconds(sweepDuration);

        // ★ FIX: Only destroy the chip GameObjects, NOT the containers
        foreach (var (container, startPos, chipsToDestroy) in chipStacks)
        {
            foreach (var chip in chipsToDestroy)
            {
                if (chip != null)
                    Destroy(chip);
            }

            // Reset container position back to original (it will be reused)
            if (container != null)
                container.position = startPos;
        }

        UnityEngine.Debug.Log("[ChipSweep] Animation complete");

        // Callback to update pot display
        onComplete?.Invoke();
    }

    /// <summary>
    /// Animates a transform (chip stack container) to target position
    /// ★ Moves the entire stack as one solid unit (no snake effect)
    /// </summary>
    IEnumerator AnimateStackTransform(Transform stackTransform, Vector3 startPos, Vector3 endPos)
    {
        if (stackTransform == null) yield break;

        float elapsed = 0f;

        while (elapsed < sweepDuration)
        {
            if (stackTransform == null) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / sweepDuration);
            float curvedT = sweepCurve.Evaluate(t);

            // Move entire stack as one solid unit
            stackTransform.position = Vector3.Lerp(startPos, endPos, curvedT);

            yield return null;
        }

        // Ensure final position
        if (stackTransform != null)
            stackTransform.position = endPos;
    }

    /// <summary>
    /// Animates pot chips sliding to winner's position
    /// ★ FIX: Moves entire pot stack as one solid unit (no snake effect)
    /// </summary>
    public IEnumerator AnimateChipsToWinner(ChipStack potChipStack, Transform winnerPosition, System.Action onComplete)
    {
        // Get all pot chip GameObjects
        var potChips = potChipStack.GetActiveChipObjects();

        if (potChips.Count == 0)
        {
            UnityEngine.Debug.Log("[ChipSweep] No pot chips to animate to winner - skipping sound");
            onComplete?.Invoke();
            yield break;
        }

        // ONLY play win sound if there are chips to collect
        if (chipWinSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(chipWinSound, soundVolume);
        }

        UnityEngine.Debug.Log($"[ChipSweep] Animating entire pot stack to winner as solid unit");

        // Get the pot's chip container
        Transform potContainer = potChipStack.chipContainer != null
            ? potChipStack.chipContainer
            : potChipStack.transform;

        Vector3 startPos = potContainer.position;

        // ★ Animate entire pot stack as one solid unit
        yield return StartCoroutine(AnimateStackTransform(potContainer, startPos, winnerPosition.position));

        // ★ FIX: Only destroy the chip GameObjects, not the container
        foreach (var chip in potChips)
        {
            if (chip != null)
                Destroy(chip);
        }

        // Reset container position (it will be reused)
        if (potContainer != null)
            potContainer.position = startPos;

        UnityEngine.Debug.Log("[ChipSweep] Winner collection complete");

        // Callback
        onComplete?.Invoke();
    }
}
