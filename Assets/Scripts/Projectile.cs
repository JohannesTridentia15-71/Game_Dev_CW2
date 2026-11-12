using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] private float damage = 25f;
    [SerializeField] private float lifetime = 4f;

    private Vector2 velocity;

    public void Initialize(Vector2 moveVelocity)
    {
        velocity = moveVelocity;
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        transform.Translate(velocity * Time.deltaTime, Space.World);
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
}