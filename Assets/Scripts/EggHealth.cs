using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class EggHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHP = 100f;
    [SerializeField] private float hitInvulnerabilityDuration = 0.6f;

    [Header("Pride Scaling")]
    [SerializeField] private float bonusMaxHPPerPride = 25f; // At 5 Pride -> 175 Max HP

    [Header("References")]
    [SerializeField] private AbilityManager abilityManager;
    [SerializeField] private SpriteRenderer eggSpriteRenderer;

    [Header("Player HUD (Optional)")]
    [SerializeField] private Slider playerHPSlider;
    [SerializeField] private TMP_Text playerHPText;

    public event Action OnEggCracked;
    public event Action<float, float> OnHPChanged;

    private float baseMaxHP;
    private float currentHP;
    private float invulnerabilityTimer;
    private Color baseSpriteColor = Color.white;

    public float CurrentHP => currentHP;
    public float MaxHP => maxHP;

    private void Awake()
    {
        baseMaxHP = maxHP;

        if (eggSpriteRenderer == null)
            eggSpriteRenderer = GetComponent<SpriteRenderer>();

        if (eggSpriteRenderer != null)
            baseSpriteColor = eggSpriteRenderer.color;

        ResetHP();
    }

    private void OnEnable()
    {
        if (StoryManager.Instance != null)
            StoryManager.Instance.OnStatsChanged += HandleStatsChanged;
    }

    private void Start()
    {
        if (StoryManager.Instance != null)
        {
            StoryManager.Instance.OnStatsChanged -= HandleStatsChanged;
            StoryManager.Instance.OnStatsChanged += HandleStatsChanged;
        }

        ApplyPrideMaxHP(refillToFull: true);
    }

    private void OnDisable()
    {
        if (StoryManager.Instance != null)
            StoryManager.Instance.OnStatsChanged -= HandleStatsChanged;
    }

    private void HandleStatsChanged()
    {
        ApplyPrideMaxHP(refillToFull: false);
    }

    public void ApplyPrideMaxHP(bool refillToFull)
    {
        int pride = StoryManager.Instance != null ? StoryManager.Instance.CurrentPride : 0;
        float previousMax = maxHP;
        maxHP = baseMaxHP + (pride * bonusMaxHPPerPride);

        if (refillToFull)
        {
            currentHP = maxHP;
        }
        else if (maxHP > previousMax)
        {
            currentHP += (maxHP - previousMax);
        }

        currentHP = Mathf.Clamp(currentHP, 0f, maxHP);
        UpdateHealthUI();
    }

    private void Update()
    {
        if (invulnerabilityTimer > 0f)
        {
            invulnerabilityTimer = Mathf.Max(0f, invulnerabilityTimer - Time.deltaTime);

            // Subtle blink feedback while invulnerable
            if (eggSpriteRenderer != null)
            {
                float alpha = Mathf.PingPong(Time.time * 16f, 1f) > 0.5f ? 1f : 0.45f;
                eggSpriteRenderer.color = new Color(baseSpriteColor.r, baseSpriteColor.g, baseSpriteColor.b, alpha);
            }
        }
        else if (eggSpriteRenderer != null && eggSpriteRenderer.color.a < 1f)
        {
            eggSpriteRenderer.color = baseSpriteColor;
        }
    }

    public void ResetHP()
    {
        ApplyPrideMaxHP(refillToFull: true);
        invulnerabilityTimer = 0f;
        if (eggSpriteRenderer != null)
            eggSpriteRenderer.color = baseSpriteColor;
    }

    public void Heal(float amount)
    {
        if (amount <= 0f) return;
        currentHP = Mathf.Min(maxHP, currentHP + amount);
        UpdateHealthUI();
    }

    public void GrantInvulnerability(float duration)
    {
        invulnerabilityTimer = Mathf.Max(invulnerabilityTimer, duration);
    }

    public void TakeDamage(float damage)
    {
        // 1. Check Wool Barrier first (absorbs up to 2 projectiles)
        if (abilityManager != null && abilityManager.TryAbsorbProjectile())
            return;

        // 2. Ignore hits during active invulnerability frames (hit i-frames or Fresh Milk)
        if (invulnerabilityTimer > 0f)
            return;

        currentHP = Mathf.Max(0f, currentHP - damage);
        invulnerabilityTimer = hitInvulnerabilityDuration;
        UpdateHealthUI();

        if (currentHP <= 0f)
        {
            OnEggCracked?.Invoke();
        }
    }

    private void UpdateHealthUI()
    {
        if (playerHPSlider != null)
        {
            playerHPSlider.value = currentHP / maxHP;
            if (playerHPSlider.fillRect != null)
                playerHPSlider.fillRect.gameObject.SetActive(playerHPSlider.value > 0f);
        }

        if (playerHPText != null)
        {
            playerHPText.text = $"{Mathf.CeilToInt(currentHP)} / {Mathf.CeilToInt(maxHP)}";
        }

        OnHPChanged?.Invoke(currentHP, maxHP);
    }
}