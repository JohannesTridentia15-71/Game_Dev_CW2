using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerBattleHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EggHealth eggHealth;

    [Header("Lives UI ([Heart] x N)")]
    [SerializeField] private TMP_Text livesMultiplierText;

    [Header("Round Egg Health UI")]
    [SerializeField] private Slider eggHealthSlider;
    [SerializeField] private TMP_Text eggHealthText;

    private void OnEnable()
    {
        if (eggHealth != null)
            eggHealth.OnHPChanged += UpdateEggHealthUI;

        if (StoryManager.Instance != null)
            StoryManager.Instance.OnStatsChanged += UpdateLivesUI;
    }

    private void Start()
    {
        if (eggHealth != null)
            UpdateEggHealthUI(eggHealth.CurrentHP, eggHealth.MaxHP);

        UpdateLivesUI();
    }

    private void OnDisable()
    {
        if (eggHealth != null)
            eggHealth.OnHPChanged -= UpdateEggHealthUI;

        if (StoryManager.Instance != null)
            StoryManager.Instance.OnStatsChanged -= UpdateLivesUI;
    }

    private void UpdateEggHealthUI(float currentHP, float maxHP)
    {
        if (eggHealthSlider != null)
            eggHealthSlider.value = maxHP > 0f ? currentHP / maxHP : 0f;

        if (eggHealthText != null)
            eggHealthText.text = $"{Mathf.CeilToInt(currentHP)} / {Mathf.CeilToInt(maxHP)}";
    }

    private void UpdateLivesUI()
    {
        int currentLives = StoryManager.Instance != null
            ? StoryManager.Instance.CurrentLives
            : 3;

        if (livesMultiplierText != null)
            livesMultiplierText.text = $"x{currentLives}";
    }
}