using UnityEngine;

public enum ProjectileMovementType
{
    Linear,
    SineWave,
    TimedHoming,
    Accelerating
}

public class Projectile : MonoBehaviour
{
    [SerializeField] private float damage = 25f;
    [SerializeField] private float lifetime = 5f;

    private ProjectileMovementType movementType = ProjectileMovementType.Linear;
    private Vector2 direction;
    private float speed;
    private float elapsed;

    // Sine-wave parameters
    private float waveFrequency = 6f;
    private float waveAmplitude = 2f;

    // Timed-homing parameters
    private Transform target;
    private float homingDuration = 0.8f;
    private float turnSpeed = 240f; // degrees per second

    // Acceleration parameter
    private float acceleration = 4f;

    private Vector2 arenaCenter;
    private float maxDistanceFromCenter;
    private bool hasEnteredArena;

    public void Initialize(
        Vector2 moveDirection,
        float moveSpeed,
        ProjectileMovementType type = ProjectileMovementType.Linear,
        Transform homingTarget = null,
        float paramA = 0f,
        float paramB = 0f)
    {
        direction = moveDirection.normalized;
        speed = moveSpeed;
        movementType = type;
        target = homingTarget;
        elapsed = 0f;

        if (type == ProjectileMovementType.SineWave)
        {
            if (paramA > 0f) waveFrequency = paramA;
            if (paramB > 0f) waveAmplitude = paramB;
        }
        else if (type == ProjectileMovementType.TimedHoming)
        {
            if (paramA > 0f) homingDuration = paramA;
            if (paramB > 0f) turnSpeed = paramB;
        }
        else if (type == ProjectileMovementType.Accelerating)
        {
            if (paramA != 0f) acceleration = paramA;
        }

        RotateTowardDirection();
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        elapsed += dt;

        switch (movementType)
        {
            case ProjectileMovementType.Linear:
                transform.Translate(direction * speed * dt, Space.World);
                break;

            case ProjectileMovementType.SineWave:
                Vector2 perp = new Vector2(-direction.y, direction.x);
                float waveOffset = Mathf.Cos(elapsed * waveFrequency) * waveAmplitude;
                Vector2 velocity = (direction * speed) + (perp * waveOffset);
                transform.Translate(velocity * dt, Space.World);
                break;

            case ProjectileMovementType.TimedHoming:
                if (target != null && elapsed <= homingDuration)
                {
                    Vector2 toTarget = ((Vector2)target.position - (Vector2)transform.position).normalized;
                    float currentAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
                    float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, turnSpeed * dt);
                    float rad = newAngle * Mathf.Deg2Rad;
                    direction = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                    RotateTowardDirection();
                }
                transform.Translate(direction * speed * dt, Space.World);
                break;

            case ProjectileMovementType.Accelerating:
                speed = Mathf.Max(0.5f, speed + acceleration * dt);
                transform.Translate(direction * speed * dt, Space.World);
                break;
        }
        if (maxDistanceFromCenter > 0f)
        {
            Vector2 offset = (Vector2)transform.position - arenaCenter;
            bool insideBox = Mathf.Abs(offset.x) <= maxDistanceFromCenter
                          && Mathf.Abs(offset.y) <= maxDistanceFromCenter;

            if (insideBox)
            {
                hasEnteredArena = true;
            }
            else if (hasEnteredArena)
            {
                Destroy(gameObject);
                return;
            }
        }
    }

    private void RotateTowardDirection()
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            EggHealth eggHealth = other.GetComponent<EggHealth>();
            if (eggHealth != null)
            {
                eggHealth.TakeDamage(damage);
            }
            Destroy(gameObject);
        }
    }

    public void SetArenaBounds(Vector2 center, float boxHalfSize, float exitMargin = 0.25f)
    {
        arenaCenter = center;
        maxDistanceFromCenter = boxHalfSize + exitMargin;
        hasEnteredArena = false;
    }
}