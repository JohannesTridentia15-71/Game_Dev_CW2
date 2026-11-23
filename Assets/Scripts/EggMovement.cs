using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EggMovement : MonoBehaviour
{
    public float moveSpeed = 6f;

    [Header("Pride Scaling")]
    [SerializeField] private float scaleIncreasePerPride = 0.12f; // At 5 Pride -> 1.6x size (larger hitbox)
    [SerializeField] private float speedPenaltyPerPride = 0.07f;  // At 5 Pride -> 0.65x speed
    [SerializeField] private float minSpeedMultiplier = 0.5f;

    public bool canMove = false;
    private Rigidbody2D rb;
    private Vector2 moveInput;
    private Vector3 baseScale;
    private float baseMoveSpeed;
    private float speedMultiplier = 1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
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

        float scaleMult = 1f + (pride * scaleIncreasePerPride);
        transform.localScale = baseScale * scaleMult;

        float speedMult = Mathf.Max(minSpeedMultiplier, 1f - (pride * speedPenaltyPerPride));
        moveSpeed = baseMoveSpeed * speedMult;
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = multiplier;
    }

    private void Update()
    {
        if (!canMove)
        {
            moveInput = Vector2.zero;
            return;
        }

        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");
        moveInput = new Vector2(x, y).normalized;
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = canMove ? moveInput * (moveSpeed * speedMultiplier) : Vector2.zero;
    }
}