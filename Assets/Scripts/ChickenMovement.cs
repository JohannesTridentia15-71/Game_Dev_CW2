using System.Collections;
using UnityEngine;

public class ChickenMovement : MonoBehaviour
{
    public float moveSpeed = 5f;

    [Header("Pride Scaling")]
    [SerializeField] private float scaleIncreasePerPride = 0.12f; // At 5 Pride -> 1.6x size
    [SerializeField] private float speedPenaltyPerPride = 0.08f;  // At 5 Pride -> 0.6x speed
    [SerializeField] private float minSpeedMultiplier = 0.45f;
    [SerializeField] private float heartbrokenSpeedMultiplier = 0.72f; // Sluggish when heartbroken (-1 Pride)

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;
    [SerializeField] private DialogueBox dialogueBox;
    [SerializeField] private GameObject interactPrompt;

    private int nearbyInteractablesCount;
    private Rigidbody2D rb;
    private Vector2 moveInput;
    private Vector3 baseScale;
    private float baseMoveSpeed;

    public bool canMove = true;
    private static readonly int IsMovingHash = Animator.StringToHash("isMoving");

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (animator == null) animator = GetComponent<Animator>();

        baseScale = transform.localScale;
        baseMoveSpeed = moveSpeed;
    }

    private void OnEnable()
    {
        if (StoryManager.Instance != null)
            StoryManager.Instance.OnStatsChanged += ApplyPrideScaling;
    }

    private void Start()
    {
        if (StoryManager.Instance != null)
        {
            StoryManager.Instance.OnStatsChanged -= ApplyPrideScaling;
            StoryManager.Instance.OnStatsChanged += ApplyPrideScaling;
        }

        if (StoryManager.Instance != null && StoryManager.Instance.PendingOverworldThought != null)
        {
            string[] thoughtLines = StoryManager.Instance.PendingOverworldThought;
            StoryManager.Instance.PendingOverworldThought = null;
            StartCoroutine(ShowPendingThoughtNextFrame(thoughtLines));
        }

        nearbyInteractablesCount = 0;
        UpdateInteractPrompt();
        ApplyPrideScaling();
    }

    private void OnDisable()
    {
        if (StoryManager.Instance != null)
            StoryManager.Instance.OnStatsChanged -= ApplyPrideScaling;
    }

    public void ApplyPrideScaling()
    {
        int pride = StoryManager.Instance != null ? StoryManager.Instance.CurrentPride : 0;

        if (pride < 0)
        {
            transform.localScale = baseScale;
            moveSpeed = baseMoveSpeed * heartbrokenSpeedMultiplier;
            return;
        }

        float scaleMult = 1f + (pride * scaleIncreasePerPride);
        transform.localScale = baseScale * scaleMult;

        float speedMult = Mathf.Max(minSpeedMultiplier, 1f - (pride * speedPenaltyPerPride));
        moveSpeed = baseMoveSpeed * speedMult;
    }

    private void Update()
    {
        UpdateInteractPrompt();

        bool isTalking = dialogueBox != null && dialogueBox.IsOpen;
        if (!canMove || isTalking)
        {
            moveInput = Vector2.zero;
            if (animator != null) animator.SetBool(IsMovingHash, false);
            return;
        }

        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");

        moveInput = new Vector2(moveX, moveY).normalized;

        if (animator != null)
        {
            animator.SetBool(IsMovingHash, moveInput.sqrMagnitude > 0.01f);
        }

        if (moveX < 0)
        {
            spriteRenderer.flipX = false;
        }
        else if (moveX > 0)
        {
            spriteRenderer.flipX = true;
        }
    }

    private void FixedUpdate()
    {
        bool isTalking = dialogueBox != null && dialogueBox.IsOpen;
        if (!canMove || isTalking)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        rb.linearVelocity = moveInput * moveSpeed;
    }

    public void RegisterInteractableInRange(bool inRange)
    {
        nearbyInteractablesCount = Mathf.Max(0, nearbyInteractablesCount + (inRange ? 1 : -1));
        UpdateInteractPrompt();
    }

    private void UpdateInteractPrompt()
    {
        if (interactPrompt == null) return;

        bool isTalking = dialogueBox != null && dialogueBox.IsOpen;
        bool inMinigame = RoosterRhythmMinigame.IsMinigameOpen;
        interactPrompt.SetActive(nearbyInteractablesCount > 0 && canMove && !isTalking && !inMinigame);
    }

    private IEnumerator ShowPendingThoughtNextFrame(string[] lines)
    {
        yield return null;
        if (dialogueBox != null)
            dialogueBox.StartDialogue("Inner Voice", lines);
    }
}