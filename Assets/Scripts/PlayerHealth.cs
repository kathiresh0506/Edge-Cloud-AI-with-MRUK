using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    public int maxHealth = 100;
    public float regenRate = 2f;
    public float regenDelay = 3f;

    [Header("Events")]
    public UnityEvent<float> onHealthChanged;
    public UnityEvent onDeath;
    public UnityEvent onDamaged;

    [Header("Audio")]
    public AudioClip damageSound;
    public AudioClip deathSound;

    private int currentHealth;
    private float lastDamageTime;
    private bool isDead = false;
    private AudioSource audioSource;
    private DamageVignette damageVignette;

    void Start()
    {
        currentHealth = maxHealth;
        lastDamageTime = -regenDelay;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        damageVignette = FindFirstObjectByType<DamageVignette>();

        // Auto-generate sounds if not assigned
        if (damageSound == null) damageSound = ProceduralAudioGenerator.GeneratePlayerDamage();
        if (deathSound == null) deathSound = ProceduralAudioGenerator.GeneratePlayerDeath();
    }

    void Update()
    {
        if (isDead) return;

        // Health regeneration
        if (Time.time - lastDamageTime > regenDelay && currentHealth < maxHealth)
        {
            currentHealth = Mathf.Min(maxHealth, currentHealth + Mathf.RoundToInt(regenRate * Time.deltaTime));
            onHealthChanged?.Invoke(GetHealthPercentage());
        }
    }

    public void TakeDamage(int amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        lastDamageTime = Time.time;

        // Damage vignette
        if (damageVignette != null)
        {
            float intensity = Mathf.Clamp01((float)amount / (maxHealth * 0.3f));
            damageVignette.TriggerDamage(intensity);
        }

        // Sound
        if (damageSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(damageSound, 0.6f);
        }

        onDamaged?.Invoke();
        onHealthChanged?.Invoke(GetHealthPercentage());

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        if (deathSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(deathSound);
        }

        onDeath?.Invoke();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerDeath();
        }
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        isDead = false;
        lastDamageTime = -regenDelay;
        onHealthChanged?.Invoke(1f);
    }

    public float GetHealthPercentage()
    {
        return (float)currentHealth / maxHealth;
    }

    public bool IsDead()
    {
        return isDead;
    }
}
