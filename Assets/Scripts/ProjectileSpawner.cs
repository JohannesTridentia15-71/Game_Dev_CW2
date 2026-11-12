using UnityEngine;

public class ProjectileSpawner : MonoBehaviour
{
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private Transform targetEgg;
    [SerializeField] private float spawnInterval = 0.8f;
    [SerializeField] private float projectileSpeed = 4f;
    [SerializeField] private float boxHalfSize = 2.4f;

    public bool isSpawning = true;
    private float timer;

    private void Update()
    {
        if (!isSpawning || projectilePrefab == null) return;

        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            timer = 0f;
            SpawnEdgeProjectile();
        }
    }

    private void SpawnEdgeProjectile()
    {
        // Pick a random point along one of the 4 edges of the arena box
        int side = Random.Range(0, 4);
        Vector2 localOffset = side switch
        {
            0 => new Vector2(Random.Range(-boxHalfSize, boxHalfSize), boxHalfSize),  // Top
            1 => new Vector2(Random.Range(-boxHalfSize, boxHalfSize), -boxHalfSize), // Bottom
            2 => new Vector2(-boxHalfSize, Random.Range(-boxHalfSize, boxHalfSize)), // Left
            _ => new Vector2(boxHalfSize, Random.Range(-boxHalfSize, boxHalfSize))   // Right
        };

        Vector3 spawnPos = transform.position + (Vector3)localOffset;
        Projectile proj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);

        // Aim roughly toward the egg's current position
        Vector2 direction = targetEgg != null
            ? ((Vector2)targetEgg.position - (Vector2)spawnPos).normalized
            : -localOffset.normalized;

        proj.Initialize(direction * projectileSpeed);
    }
}