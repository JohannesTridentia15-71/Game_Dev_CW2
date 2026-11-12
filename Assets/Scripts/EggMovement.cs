using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EggMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 6f;

    public bool canMove = false;
    private Rigidbody2D rb;
    private Vector2 moveInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
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
        rb.linearVelocity = canMove ? moveInput * moveSpeed : Vector2.zero;
    }
}