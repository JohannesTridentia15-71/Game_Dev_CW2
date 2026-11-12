using System;
using UnityEngine;

public class EggHealth : MonoBehaviour
{
    [SerializeField] private float maxHP = 100f;
    [SerializeField] private float invulnerabilityTime = 0.6f;

    public float CurrentHP { get; private set; }
    public float MaxHP => maxHP;

    private SpriteRenderer spriteRenderer;
    private float invulnTimer;

    public event Action<float, float> OnHPChanged;
    public event Action OnEggCracked;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        CurrentHP = maxHP;
    }

    public void ResetHP()
    {
        CurrentHP = maxHP;
        invulnTimer = 0f;
        spriteRenderer.enabled = true;
        OnHPChanged?.Invoke(CurrentHP, maxHP);
    }

    private void Update()
    {
        if (invulnTimer > 0f)
        {
            invulnTimer -= Time.deltaTime;
            spriteRenderer.enabled = Mathf.FloorToInt(invulnTimer * 15f) % 2 == 0;
        }
        else
        {
            spriteRenderer.enabled = true;
        }
    }

    public void TakeDamage(float amount)
    {
        if (invulnTimer > 0f) return;

        CurrentHP = Mathf.Max(0f, CurrentHP - amount);
        invulnTimer = invulnerabilityTime;
        OnHPChanged?.Invoke(CurrentHP, maxHP);

        if (CurrentHP <= 0f)
        {
            Debug.Log("Egg cracked!");
            OnEggCracked?.Invoke();
        }
    }
}