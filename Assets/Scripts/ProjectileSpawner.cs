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
        float ringAngle = Random.Range(0f, 360f);

        BattleRelationshipModifiers relMods = StoryManager.Instance != null
            ? StoryManager.Instance.GetBattleModifiersFor(StoryManager.Instance.CurrentOpponent)
            : BattleRelationshipModifiers.Default;

        float speedMult = relMods.bulletSpeedMultiplier;
        float scaledBurstInterval = Mathf.Max(0.1f, pattern.burstInterval * relMods.burstIntervalMultiplier);

        while (isSpawning && waveElapsed < pattern.duration)
        {
            List<PlannedShot> shots = BuildVolleyShots(pattern, ref ringAngle);

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
                CreateBullet(pattern, shot.position, shot.direction, speedMult);
            }

            yield return new WaitForSeconds(scaledBurstInterval);
            waveElapsed += scaledBurstInterval;
        }
    }

    private List<PlannedShot> BuildVolleyShots(AttackPatternSO pattern, ref float ringAngle)
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
                    float angle = ringAngle + (ringStep * i);
                    Vector2 dir = AngleToVector(angle);
                    shots.Add(new PlannedShot
                    {
                        position = (Vector2)transform.position + dir * 0.4f,
                        direction = dir
                    });
                }
                ringAngle += 18f;
                break;

            case PatternShape.WallWithGap:
                bool horizontalWall = Random.value > 0.5f;
                float sign = Random.value > 0.5f ? 1f : -1f;
                float gapCenter = Random.Range(-boxHalfSize + pattern.safeGapWidth, boxHalfSize - pattern.safeGapWidth);
                AddWallShots(shots, horizontalWall, sign, gapCenter, pattern.safeGapWidth, pattern.bulletCount);
                break;

            case PatternShape.StampedeCross:
                bool horizontalPair = Random.value > 0.5f;
                float minGap = -boxHalfSize + pattern.safeGapWidth;
                float maxGap = boxHalfSize - pattern.safeGapWidth;

                // Pick two offset gaps so the player weaves through one opening, then shifts for the second
                float gapA = Random.Range(minGap, 0f);
                float gapB = Random.Range(0f, maxGap);
                if (Random.value > 0.5f)
                {
                    (gapA, gapB) = (gapB, gapA);
                }

                AddWallShots(shots, horizontalPair, 1f, gapA, pattern.safeGapWidth, pattern.bulletCount);
                AddWallShots(shots, horizontalPair, -1f, gapB, pattern.safeGapWidth, pattern.bulletCount);
                break;
        }

        return shots;
    }

    private void AddWallShots(List<PlannedShot> shots, bool horizontalWall, float sign, float gapCenter, float safeGapWidth, int bulletCount)
    {
        float halfGap = safeGapWidth * 0.5f;
        int wallSteps = Mathf.Max(4, bulletCount);
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
    }

    private void SpawnTelegraphMarkers(PatternShape shape, List<PlannedShot> shots)
    {
        ClearActiveMarkers();
        if (shots == null || shots.Count == 0 || telegraphMarkerPrefab == null) return;

        switch (shape)
        {
            case PatternShape.AimedFan:
                PlannedShot centerShot = shots[shots.Count / 2];
                activeMarkers.Add(Instantiate(telegraphMarkerPrefab, centerShot.position, Quaternion.identity));
                break;

            case PatternShape.RadialRing:
                activeMarkers.Add(Instantiate(telegraphMarkerPrefab, transform.position, Quaternion.identity));
                break;

            case PatternShape.WallWithGap:
                Vector2 avgPos = Vector2.zero;
                for (int i = 0; i < shots.Count; i++) avgPos += shots[i].position;
                avgPos /= shots.Count;
                activeMarkers.Add(Instantiate(telegraphMarkerPrefab, avgPos, Quaternion.identity));
                break;

            case PatternShape.StampedeCross:
                // Spawn one marker on each of the two opposite walls
                Vector2 sumPosA = Vector2.zero;
                Vector2 sumPosB = Vector2.zero;
                int countA = 0;
                int countB = 0;
                Vector2 firstDir = shots[0].direction;

                for (int i = 0; i < shots.Count; i++)
                {
                    if (Vector2.Dot(shots[i].direction, firstDir) > 0f)
                    {
                        sumPosA += shots[i].position;
                        countA++;
                    }
                    else
                    {
                        sumPosB += shots[i].position;
                        countB++;
                    }
                }

                if (countA > 0)
                    activeMarkers.Add(Instantiate(telegraphMarkerPrefab, sumPosA / countA, Quaternion.identity));
                if (countB > 0)
                    activeMarkers.Add(Instantiate(telegraphMarkerPrefab, sumPosB / countB, Quaternion.identity));
                break;

            default:
                activeMarkers.Add(Instantiate(telegraphMarkerPrefab, shots[0].position, Quaternion.identity));
                break;
        }
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

    private void CreateBullet(AttackPatternSO pattern, Vector2 worldPos, Vector2 direction, float speedMult = 1f)
    {
        Projectile prefab = pattern.projectileOverride != null
            ? pattern.projectileOverride
            : defaultProjectilePrefab;

        if (prefab == null) return;

        Projectile bullet = Instantiate(prefab, worldPos, Quaternion.identity);
        bullet.Initialize(
            direction,
            pattern.bulletSpeed * speedMult,
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