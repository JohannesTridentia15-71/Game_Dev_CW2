using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AnimalAttackSet
{
    public AnimalType animal;
    public List<AttackPatternSO> patterns = new List<AttackPatternSO>();
}

public class ProjectileSpawner : MonoBehaviour
{
    private struct PlannedShot
    {
        public Vector2 position;
        public Vector2 direction;
    }

    [Header("Core References")]
    [SerializeField] private Projectile defaultProjectilePrefab;
    [SerializeField] private GameObject telegraphMarkerPrefab;
    [SerializeField] private Transform targetEgg;
    [SerializeField] private float boxHalfSize = 2.4f;

    [Header("Per-Animal Attack Sets")]
    [SerializeField] private List<AnimalAttackSet> animalAttackSets = new List<AnimalAttackSet>();
    [SerializeField] private List<AttackPatternSO> fallbackPatterns = new List<AttackPatternSO>();

    public bool isSpawning;
    private List<AttackPatternSO> activePatterns;
    private Coroutine waveRoutine;
    private int patternIndex;
    private readonly List<GameObject> activeMarkers = new List<GameObject>();

    private void Start()
    {
        AnimalType opponent = StoryManager.Instance != null
            ? StoryManager.Instance.CurrentOpponent
            : AnimalType.Sheep;

        LoadPatternsForAnimal(opponent);
    }

    public void LoadPatternsForAnimal(AnimalType animal)
    {
        patternIndex = 0;

        foreach (var set in animalAttackSets)
        {
            if (set.animal == animal && set.patterns != null && set.patterns.Count > 0)
            {
                activePatterns = set.patterns;
                return;
            }
        }

        activePatterns = fallbackPatterns;
    }

    private void Update()
    {
        if (isSpawning && waveRoutine == null)
        {
            waveRoutine = StartCoroutine(RunPatternLoop());
        }
        else if (!isSpawning && waveRoutine != null)
        {
            StopCoroutine(waveRoutine);
            waveRoutine = null;
            ClearActiveMarkers();
        }
    }

    private IEnumerator RunPatternLoop()
    {
        if (activePatterns == null || activePatterns.Count == 0)
        {
            Debug.LogWarning("ProjectileSpawner has no patterns assigned for the current opponent!");
            yield break;
        }

        while (isSpawning)
        {
            AttackPatternSO pattern = activePatterns[patternIndex % activePatterns.Count];
            patternIndex++;

            yield return StartCoroutine(ExecutePattern(pattern));
            yield return new WaitForSeconds(0.4f);
        }

        waveRoutine = null;
    }

    private IEnumerator ExecutePattern(AttackPatternSO pattern)
    {
        float waveElapsed = 0f;
        float spiralAngle = Random.Range(0f, 360f);

        while (isSpawning && waveElapsed < pattern.duration)
        {
            List<PlannedShot> shots = BuildVolleyShots(pattern, ref spiralAngle);

            if (pattern.telegraphDuration > 0f && telegraphMarkerPrefab != null)
            {
                SpawnTelegraphMarkers(pattern.shape, shots);
                yield return new WaitForSeconds(pattern.telegraphDuration);
                ClearActiveMarkers();
                waveElapsed += pattern.telegraphDuration;

                if (!isSpawning) yield break;
            }

            foreach (var shot in shots)
            {
                CreateBullet(pattern, shot.position, shot.direction);
            }

            yield return new WaitForSeconds(pattern.burstInterval);
            waveElapsed += pattern.burstInterval;
        }
    }

    private List<PlannedShot> BuildVolleyShots(AttackPatternSO pattern, ref float spiralAngle)
    {
        var shots = new List<PlannedShot>();

        switch (pattern.shape)
        {
            case PatternShape.AimedFan:
                Vector2 origin = GetRandomEdgePosition();
                Vector2 baseDir = targetEgg != null
                    ? ((Vector2)targetEgg.position - origin).normalized
                    : -origin.normalized;

                float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;
                int fanCount = Mathf.Max(1, pattern.bulletCount);
                float startAngle = baseAngle - (pattern.angleParameter * 0.5f);
                float fanStep = fanCount > 1 ? pattern.angleParameter / (fanCount - 1) : 0f;

                for (int i = 0; i < fanCount; i++)
                {
                    float angle = fanCount > 1 ? startAngle + (fanStep * i) : baseAngle;
                    shots.Add(new PlannedShot { position = origin, direction = AngleToVector(angle) });
                }
                break;

            case PatternShape.RadialRing:
                int ringCount = Mathf.Max(3, pattern.bulletCount);
                float ringStep = 360f / ringCount;

                for (int i = 0; i < ringCount; i++)
                {
                    float angle = spiralAngle + (ringStep * i);
                    Vector2 dir = AngleToVector(angle);
                    shots.Add(new PlannedShot
                    {
                        position = (Vector2)transform.position + dir * 0.4f,
                        direction = dir
                    });
                }
                spiralAngle += 18f;
                break;

            case PatternShape.WallWithGap:
                bool horizontalWall = Random.value > 0.5f;
                float sign = Random.value > 0.5f ? 1f : -1f;
                float gapCenter = Random.Range(-boxHalfSize + pattern.safeGapWidth, boxHalfSize - pattern.safeGapWidth);
                float halfGap = pattern.safeGapWidth * 0.5f;
                int wallSteps = Mathf.Max(4, pattern.bulletCount);
                float span = boxHalfSize * 2f;

                for (int i = 0; i <= wallSteps; i++)
                {
                    float offset = -boxHalfSize + (span * i / wallSteps);
                    if (Mathf.Abs(offset - gapCenter) < halfGap) continue;

                    Vector2 localPos = horizontalWall
                        ? new Vector2(offset, sign * boxHalfSize)
                        : new Vector2(sign * boxHalfSize, offset);

                    Vector2 moveDir = horizontalWall
                        ? new Vector2(0f, -sign)
                        : new Vector2(-sign, 0f);

                    shots.Add(new PlannedShot
                    {
                        position = (Vector2)transform.position + localPos,
                        direction = moveDir
                    });
                }
                break;

            case PatternShape.SpiralStream:
                shots.Add(new PlannedShot
                {
                    position = transform.position,
                    direction = AngleToVector(spiralAngle)
                });
                spiralAngle += pattern.angleParameter;
                break;
        }

        return shots;
    }

    private void SpawnTelegraphMarkers(PatternShape shape, List<PlannedShot> shots)
    {
        ClearActiveMarkers();
        if (shots == null || shots.Count == 0 || telegraphMarkerPrefab == null) return;

        Vector2 markerPos;
        float markerAngle;

        switch (shape)
        {
            case PatternShape.AimedFan:
                // Single marker at the edge origin pointing along the middle bullet
                PlannedShot centerShot = shots[shots.Count / 2];
                markerPos = centerShot.position;
                markerAngle = Mathf.Atan2(centerShot.direction.y, centerShot.direction.x) * Mathf.Rad2Deg;
                break;

            case PatternShape.RadialRing:
                // Single marker at the arena center
                markerPos = transform.position;
                markerAngle = 0f;
                break;

            case PatternShape.WallWithGap:
                // Single marker centered on the spawning wall edge, pointing inward
                Vector2 avgPos = Vector2.zero;
                for (int i = 0; i < shots.Count; i++) avgPos += shots[i].position;
                markerPos = avgPos / shots.Count;
                Vector2 dir = shots[0].direction;
                markerAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                break;

            default:
                markerPos = shots[0].position;
                markerAngle = Mathf.Atan2(shots[0].direction.y, shots[0].direction.x) * Mathf.Rad2Deg;
                break;
        }

        GameObject marker = Instantiate(
            telegraphMarkerPrefab,
            markerPos,
            Quaternion.identity
        );
        activeMarkers.Add(marker);
    }

    private void ClearActiveMarkers()
    {
        for (int i = 0; i < activeMarkers.Count; i++)
        {
            if (activeMarkers[i] != null)
                Destroy(activeMarkers[i]);
        }
        activeMarkers.Clear();
    }

    private void CreateBullet(AttackPatternSO pattern, Vector2 worldPos, Vector2 direction)
    {
        Projectile prefab = pattern.projectileOverride != null
            ? pattern.projectileOverride
            : defaultProjectilePrefab;

        if (prefab == null) return;

        Projectile bullet = Instantiate(prefab, worldPos, Quaternion.identity);
        bullet.Initialize(
            direction,
            pattern.bulletSpeed,
            pattern.movementType,
            targetEgg,
            pattern.behaviorParamA,
            pattern.behaviorParamB
        );
        bullet.SetArenaBounds(transform.position, boxHalfSize);
    }

    private Vector2 GetRandomEdgePosition()
    {
        int side = Random.Range(0, 4);
        Vector2 offset = side switch
        {
            0 => new Vector2(Random.Range(-boxHalfSize, boxHalfSize), boxHalfSize),
            1 => new Vector2(Random.Range(-boxHalfSize, boxHalfSize), -boxHalfSize),
            2 => new Vector2(-boxHalfSize, Random.Range(-boxHalfSize, boxHalfSize)),
            _ => new Vector2(boxHalfSize, Random.Range(-boxHalfSize, boxHalfSize))
        };
        return (Vector2)transform.position + offset;
    }

    private static Vector2 AngleToVector(float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }
}