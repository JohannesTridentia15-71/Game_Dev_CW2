using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PrideMeterUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Slider prideSlider;
    [SerializeField] private Image fillImage;
    [SerializeField] private TMP_Text prideValueText;

    [Header("Tier Colors")]
    [SerializeField] private Color heartbrokenColor = new Color(0.45f, 0.55f, 0.78f); // Heartbroken (-1)
    [SerializeField] private Color lowPrideColor = new Color(0.35f, 0.78f, 0.45f);    // Humble (0-1)
    [SerializeField] private Color mediumPrideColor = new Color(0.95f, 0.72f, 0.22f); // Proud (2-3)
    [SerializeField] private Color highPrideColor = new Color(0.88f, 0.25f, 0.25f);   // Arrogant (4-5)

    private void OnEnable()
    {
        if (StoryManager.Instance != null)
            StoryManager.Instance.OnStatsChanged += RefreshMeter;
    }

    private void Start()
    {
        RefreshMeter();
    }

    private void OnDisable()
    {
        if (StoryManager.Instance != null)
            StoryManager.Instance.OnStatsChanged -= RefreshMeter;
    }

    public void RefreshMeter()
    {
        int pride = StoryManager.Instance != null ? StoryManager.Instance.CurrentPride : 0;
        int maxPride = 5;
        PrideTier tier = StoryManager.Instance != null ? StoryManager.Instance.GetPrideTier() : PrideTier.Low;

        if (prideSlider != null)
        {
            prideSlider.minValue = 0;
            prideSlider.maxValue = maxPride;
            prideSlider.value = Mathf.Clamp(pride, 0, maxPride);
        }

        Color activeColor = tier switch
        {
            PrideTier.Heartbroken => heartbrokenColor,
            PrideTier.Low => lowPrideColor,
            PrideTier.Medium => mediumPrideColor,
            _ => highPrideColor
        };

        if (fillImage != null)
            fillImage.color = activeColor;

        if (prideValueText != null)
        {
            string tierLabel = tier switch
            {
                PrideTier.Heartbroken => "Heartbroken",
                PrideTier.Low => "Humble",
                PrideTier.Medium => "Proud",
                _ => "Arrogant"
            };
            prideValueText.text = $"Pride: {pride}/{maxPride} ({tierLabel})";
        }
    }
}