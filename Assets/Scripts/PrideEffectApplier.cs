using UnityEngine;

public class PrideEffectApplier : MonoBehaviour
{
    [Header("Scale Per Pride Point")]
    [SerializeField] private float scaleIncreasePerPride = 0.12f; // At 5 Pride -> 1.6x size

    [Header("Speed Penalty Per Pride Point")]
    [SerializeField] private float speedPenaltyPerPride = 0.07f;  // At 5 Pride -> 0.65x speed
    [SerializeField] private float minSpeedMultiplier = 0.5f;

    [Header("Optional Movement Reference")]
    [SerializeField] private EggMovement eggMovement;

    private Vector3 baseScale;
    private float baseSpeed;
    private bool initialized;

    private void Awake()
    {
        baseScale = transform.localScale;
        if (eggMovement != null)
            baseSpeed = eggMovement.moveSpeed;
        initialized = true;
    }

    private void OnEnable()
    {
        if (StoryManager.Instance != null)
            StoryManager.Instance.OnStatsChanged += ApplyPrideEffects;
    }

    private void Start()
    {
        ApplyPrideEffects();
    }

    private void OnDisable()
    {
        if (StoryManager.Instance != null)
            StoryManager.Instance.OnStatsChanged -= ApplyPrideEffects;
    }

    public void ApplyPrideEffects()
    {
        if (!initialized) return;

        int pride = StoryManager.Instance != null ? StoryManager.Instance.CurrentPride : 0;

        // 1. Grow larger (visual hubris + larger bullet-hell hitbox)
        float scaleMult = 1f + (pride * scaleIncreasePerPride);
        transform.localScale = baseScale * scaleMult;

        // 2. Move slower
        float speedMult = Mathf.Max(minSpeedMultiplier, 1f - (pride * speedPenaltyPerPride));
        if (eggMovement != null && baseSpeed > 0f)
            eggMovement.moveSpeed = baseSpeed * speedMult;
    }
}