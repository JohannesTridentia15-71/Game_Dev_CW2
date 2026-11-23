using UnityEngine;

public enum ProjectileMovementType
{
    Linear,
    SineWave,
    TimedHoming,
    Accelerating,
    Bouncing,
    Boomerang
}

public class Projectile : MonoBehaviour
{
    public float damage = 25f;
    [SerializeField] private float lifetime = 6f;

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

    // Bouncing parameters
    private int remainingBounces = 2;

    // Boomerang parameters
    private float boomerangDeceleration = 5f;
    private float boomerangInitialSpeed;
    private bool boomerangReversed;

    private Vector2 arenaCenter;
    private float arenaBoxHalfSize;
    private float maxDistanceFromCenter;
    private bool hasEnteredArena;
    private bool hasHit;

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
        else if (type == ProjectileMovementType.Bouncing)
        {
            remainingBounces = paramA > 0f ? Mathf.RoundToInt(paramA) : 2;
        }
        else if (type == ProjectileMovementType.Boomerang)
        {
            boomerangDeceleration = paramA > 0f ? paramA : 5f;
            boomerangInitialSpeed = moveSpeed;
            boomerangReversed = false;
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

            case ProjectileMovementType.Bouncing:
                transform.Translate(direction * speed * dt, Space.World);
                HandleArenaBounce();
                break;

            case ProjectileMovementType.Boomerang:
                if (!boomerangReversed)
                {
                    speed -= boomerangDeceleration * dt;
                    if (speed <= 0f)
                    {
                        speed = 0f;
                        boomerangReversed = true;
                        direction = -direction;
                        RotateTowardDirection();
                    }
                }
                else
                {
                    speed = Mathf.Min(boomerangInitialSpeed * 1.2f, speed + boomerangDeceleration * dt);
                }
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
            else if (hasEnteredArena && (movementType != ProjectileMovementType.Bouncing || remainingBounces <= 0))
            {
                Destroy(gameObject);
                return;
            }
        }
    }

    private void HandleArenaBounce()
    {
        if (arenaBoxHalfSize <= 0f || remainingBounces <= 0) return;

        Vector2 pos = transform.position;
        Vector2 offset = pos - arenaCenter;
        bool bounced = false;

        if (offset.x > arenaBoxHalfSize && direction.x > 0f)
        {
            pos.x = arenaCenter.x + arenaBoxHalfSize;
            direction.x = -direction.x;
            bounced = true;
        }
        else if (offset.x < -arenaBoxHalfSize && direction.x < 0f)
        {
            pos.x = arenaCenter.x - arenaBoxHalfSize;
            direction.x = -direction.x;
            bounced = true;
        }

        if (offset.y > arenaBoxHalfSize && direction.y > 0f)
        {
            pos.y = arenaCenter.y + arenaBoxHalfSize;
            direction.y = -direction.y;
            bounced = true;
        }
        else if (offset.y < -arenaBoxHalfSize && direction.y < 0f)
        {
            pos.y = arenaCenter.y - arenaBoxHalfSize;
            direction.y = -direction.y;
            bounced = true;
        }

        if (bounced)
        {
            remainingBounces--;
            transform.position = pos;
            RotateTowardDirection();
        }
    }

    private void RotateTowardDirection()
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit) return;

        if (other.CompareTag("Player"))
        {
            hasHit = true;
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
        arenaBoxHalfSize = boxHalfSize;
        maxDistanceFromCenter = boxHalfSize + exitMargin;
        hasEnteredArena = false;
    }
}