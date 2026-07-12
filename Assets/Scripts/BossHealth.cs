using UnityEngine;
using UnityEngine.Events;
using System.Collections;

/// <summary>
/// Health for the final boss. Same TakeDamage(int, bool isHeadshot) API as AlienHealth
/// but with much higher max health (~10x a regular alien's headshot-kill-equivalent).
/// </summary>
public class BossHealth : MonoBehaviour
{
    [Header("Health")]
    [Tooltip("Regular alien headshot-kill = 3 HP, so 30 = roughly 10x that.")]
    public int maxHealth = 30;
    public int scoreValue = 500;

    [Header("Hit Effect")]
    public float hitFlashDuration = 0.1f;

    [Header("Events")]
    public UnityEvent onDeath;
    public UnityEvent onHit;

    private int currentHealth;
    private bool isDead = false;
    private AudioSource audioSource;
    private Renderer[] renderers;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0.4f; // audible boss hits
        }
        currentHealth = maxHealth;
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        isDead = false;
    }

    /// <summary>Refresh the cached renderer list (call after the model is built).</summary>
    public void CacheRenderers()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    public void TakeDamage(int amount) => TakeDamage(amount, false);

    public void TakeDamage(int amount, bool isHeadshot)
    {
        if (isDead) return;

        currentHealth -= amount;
        onHit?.Invoke();

        StartCoroutine(HitFlash(isHeadshot));

        AudioClip hitClip = ProceduralAudioGenerator.GenerateHitImpact();
        if (hitClip != null && audioSource != null)
            audioSource.PlayOneShot(hitClip, isHeadshot ? 0.9f : 0.5f);

        if (currentHealth <= 0)
            Die();
    }

    IEnumerator HitFlash(bool isHeadshot)
    {
        if (renderers == null || renderers.Length == 0) CacheRenderers();
        if (renderers == null) yield break;

        Color flash = isHeadshot ? new Color(1f, 0.9f, 0.2f) : Color.white;
        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_EmissionColor", flash * 4f);
            r.SetPropertyBlock(mpb);
        }

        yield return new WaitForSeconds(hitFlashDuration);

        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.SetPropertyBlock(null); // restore original material state
        }
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        if (GameManager.Instance != null)
            GameManager.Instance.OnAlienKilled(scoreValue);

        AudioClip deathClip = ProceduralAudioGenerator.GenerateAlienDeath();
        if (deathClip != null)
            AudioSource.PlayClipAtPoint(deathClip, transform.position, 1f);

        // Death VFX (reuses the alien death effect at boss scale)
        AlienDeathVFX deathVFX = GetComponent<AlienDeathVFX>();
        if (deathVFX != null)
        {
            deathVFX.PlayDeathEffect(scoreValue);
        }
        else
        {
            Destroy(gameObject, 0.4f);
        }

        onDeath?.Invoke();
    }

    public float GetHealthPercentage() => Mathf.Clamp01((float)currentHealth / maxHealth);
    public bool IsDead => isDead;
}
