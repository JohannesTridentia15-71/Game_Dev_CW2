using UnityEngine;

public enum PatternShape
{
    AimedFan,        // Spread of bullets aimed at the egg
    RadialRing,      // 360-degree burst from the arena center
    WallWithGap,     // Line of bullets sweeping across the box with 1 guaranteed safe opening
    StampedeCross    // Two opposite walls firing inward simultaneously with offset safe gaps
}

[CreateAssetMenu(fileName = "NewAttackPattern", menuName = "Battle/Attack Pattern")]
public class AttackPatternSO : ScriptableObject
{
    [Header("Pattern Shape & Timing")]
    public PatternShape shape = PatternShape.AimedFan;
    public Projectile projectileOverride; // Leave empty to use spawner's default prefab
    public int bulletCount = 5;
    public float bulletSpeed = 4.5f;
    public float duration = 3.5f;         // How long this wave runs before switching to the next
    public float burstInterval = 1.1f;    // Time between volleys inside this wave

    [Header("Shape Tuning")]
    [Tooltip("Fan spread angle in degrees (AimedFan)")]
    public float angleParameter = 45f;
    [Tooltip("Width of the safe gap in world units (WallWithGap / StampedeCross)")]
    public float safeGapWidth = 1.35f;

    [Header("Bullet Flight Behavior")]
    public ProjectileMovementType movementType = ProjectileMovementType.Linear;
    public float behaviorParamA = 0f; // Frequency (Sine) / Homing Duration / Acceleration
    public float behaviorParamB = 0f; // Amplitude (Sine) / Turn Speed (Homing)

    [Header("Telegraph Warning")]
    [Tooltip("Seconds to display warning markers before firing (0 = no warning)")]
    public float telegraphDuration = 0.6f;
}