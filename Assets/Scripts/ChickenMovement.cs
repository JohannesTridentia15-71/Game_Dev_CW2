using UnityEngine;

public class ChickenMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;

    private Rigidbody2D rb;
    private Vector2 moveInput;

    public bool canMove = true;
    private static readonly int IsMovingHash = Animator.StringToHash("isMoving");

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (animator == null) animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (!canMove)
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
        rb.linearVelocity = moveInput * moveSpeed;
    }
}