using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RoosterRhythmMinigame : MonoBehaviour
{
    [Header("Testing")]
    [SerializeField] private bool startHeartbrokenForTesting = false;

    [Header("UI Panel & Ring References")]
    [SerializeField] private GameObject minigamePanel;
    [SerializeField] private RectTransform targetRing;
    [SerializeField] private RectTransform approachRing;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private TMP_Text progressText;

    [Header("Player & Dialogue References")]
    [SerializeField] private ChickenMovement playerMovement;
    [SerializeField] private DialogueBox dialogueBox;
    [SerializeField] private string roosterSpeakerName = "Rooster";
    [TextArea(2, 4)]
    [SerializeField] private string[] trainingCompleteDialogue = new[]
    {
        "*COCK-A-DOODLE-DOO!* That's the spirit, kid! Your heart is steady and your strength is back!",
        "Now go show Wingston Churchill what this farm is made of!"
    };

    [Header("Rhythm Timing Tuning")]
    [SerializeField] private int streakRequiredToWin = 5;
    [SerializeField] private float initialBeatDuration = 1.35f;
    [SerializeField] private float minBeatDuration = 0.80f;
    [SerializeField] private float startRingScale = 2.6f;
    [SerializeField] private float endRingScale = 0.65f;

    [Tooltip("How close the ring scale must be to 1.0 to count as a hit (0.22 = crisp & fair)")]
    [SerializeField] private float hitWindowTolerance = 0.22f;
    [SerializeField] private float perfectWindowTolerance = 0.10f;

    [Header("Feedback Colors & Optional Audio")]
    [SerializeField] private Image targetRingImage;
    [SerializeField] private Color neutralRingColor = Color.white;
    [SerializeField] private Color hitFlashColor = new Color(0.35f, 0.95f, 0.45f);
    [SerializeField] private Color missFlashColor = new Color(0.95f, 0.35f, 0.35f);
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip beatStartCue;
    [SerializeField] private AudioClip hitSuccessCue;
    [SerializeField] private AudioClip missCue;

    private bool isRunning;
    private int currentStreak;
    private float currentBeatDuration;
    private float beatElapsed;
    private bool beatResolved;
    private bool ignoreInputThisFrame;
    private Coroutine flashRoutine;

    private void Start()
    {
        if (minigamePanel != null)
            minigamePanel.SetActive(false);

        if (startHeartbrokenForTesting && StoryManager.Instance != null && !StoryManager.Instance.HasTriggeredMidpointTragedy)
        {
            SetupTestHeartbreakState();
        }
    }

    private void SetupTestHeartbreakState()
    {
        StoryManager.Instance.TriggerMidpointTragedy();

        if (playerMovement != null)
            playerMovement.ApplyPrideScaling();

        var prideUI = FindFirstObjectByType<PrideMeterUI>();
        if (prideUI != null)
            prideUI.RefreshMeter();

        foreach (var animal in FindObjectsByType<AnimalInteractable>(FindObjectsSortMode.None))
            animal.RefreshVisualState();
    }

    /// <summary>
    /// Wire this method to AnimalInteractable.onStartRoosterTraining in the Inspector!
    /// </summary>
    public void StartTraining()
    {
        if (dialogueBox != null && dialogueBox.IsOpen)
            dialogueBox.CloseDialogue();
            
        if (isRunning) return;

        isRunning = true;
        ignoreInputThisFrame = true; // Prevents the 'E' press that closed dialogue from firing immediately
        currentStreak = 0;
        currentBeatDuration = initialBeatDuration;

        if (playerMovement != null)
            playerMovement.canMove = false;

        if (minigamePanel != null)
            minigamePanel.SetActive(true);

        if (instructionText != null)
            instructionText.text = $"Hit {streakRequiredToWin} in a row! Press [SPACE] or [E] on the ring!";

        if (feedbackText != null)
            feedbackText.text = "Focus your breathing...";

        UpdateProgressUI();
        BeginNextBeat();
    }

    private void BeginNextBeat()
    {
        beatElapsed = 0f;
        beatResolved = false;

        float t = streakRequiredToWin > 1 ? (float)currentStreak / (streakRequiredToWin - 1) : 1f;
        currentBeatDuration = Mathf.Lerp(initialBeatDuration, minBeatDuration, t);

        if (approachRing != null)
        {
            approachRing.gameObject.SetActive(true);
            approachRing.localScale = Vector3.one * startRingScale;
        }

        if (audioSource != null && beatStartCue != null)
            audioSource.PlayOneShot(beatStartCue);
    }

    private void Update()
    {
        if (!isRunning || beatResolved) return;

        beatElapsed += Time.deltaTime;

        float totalTravel = startRingScale - endRingScale;
        float progress = beatElapsed / currentBeatDuration;
        float currentScale = startRingScale - (totalTravel * progress);

        if (approachRing != null)
            approachRing.localScale = Vector3.one * Mathf.Max(endRingScale, currentScale);

        if (!ignoreInputThisFrame && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E)))
        {
            EvaluateHit(currentScale);
            return;
        }

        if (currentScale <= endRingScale)
        {
            beatResolved = true;
            if (approachRing != null)
                approachRing.gameObject.SetActive(false);
            HandleMiss("Too late! Streak reset!");
        }
    }

    private void LateUpdate()
    {
        ignoreInputThisFrame = false;
    }

    private void EvaluateHit(float currentScale)
    {
        beatResolved = true;

        // Immediately hide the approaching ring on impact so it doesn't keep shrinking
        if (approachRing != null)
            approachRing.gameObject.SetActive(false);

        float error = Mathf.Abs(currentScale - 1.0f);

        if (error <= hitWindowTolerance)
        {
            currentStreak++;
            UpdateProgressUI();

            string rating = error <= perfectWindowTolerance
                ? $"PERFECT! ({currentStreak}/{streakRequiredToWin})"
                : $"GOOD! ({currentStreak}/{streakRequiredToWin})";

            if (feedbackText != null)
                feedbackText.text = rating;

            if (audioSource != null && hitSuccessCue != null)
                audioSource.PlayOneShot(hitSuccessCue);

            TriggerRingFlash(hitFlashColor);

            if (currentStreak >= streakRequiredToWin)
            {
                StartCoroutine(FinishTrainingRoutine());
            }
            else
            {
                StartCoroutine(NextBeatDelayRoutine(0.28f));
            }
        }
        else
        {
            string hint = currentScale > 1.0f
                ? "Too early! Streak reset!"
                : "Too late! Streak reset!";
            HandleMiss(hint);
        }
    }

    private void HandleMiss(string message)
    {
        currentStreak = 0;
        UpdateProgressUI();

        if (feedbackText != null)
            feedbackText.text = message;

        if (audioSource != null && missCue != null)
            audioSource.PlayOneShot(missCue);

        TriggerRingFlash(missFlashColor);
        StartCoroutine(NextBeatDelayRoutine(0.45f));
    }

    private IEnumerator NextBeatDelayRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (isRunning)
            BeginNextBeat();
    }

    private IEnumerator FinishTrainingRoutine()
    {
        if (feedbackText != null)
            feedbackText.text = "SPIRIT RESTORED!";

        yield return new WaitForSeconds(0.65f);

        isRunning = false;

        if (minigamePanel != null)
            minigamePanel.SetActive(false);

        if (StoryManager.Instance != null)
            StoryManager.Instance.CompleteRoosterTraining();

        if (playerMovement != null)
            playerMovement.canMove = true;

        if (dialogueBox != null && trainingCompleteDialogue != null && trainingCompleteDialogue.Length > 0)
        {
            dialogueBox.StartDialogue(roosterSpeakerName, trainingCompleteDialogue);
        }
    }

    private void UpdateProgressUI()
    {
        if (progressSlider != null)
        {
            progressSlider.minValue = 0;
            progressSlider.maxValue = streakRequiredToWin;
            progressSlider.value = currentStreak;
        }

        if (progressText != null)
            progressText.text = $"Streak: {currentStreak} / {streakRequiredToWin}";
    }

    private void TriggerRingFlash(Color flashColor)
    {
        if (targetRingImage == null) return;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRingRoutine(flashColor));
    }

    private IEnumerator FlashRingRoutine(Color flashColor)
    {
        targetRingImage.color = flashColor;
        yield return new WaitForSeconds(0.2f);
        targetRingImage.color = neutralRingColor;
        flashRoutine = null;
    }
}