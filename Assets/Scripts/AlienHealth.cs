using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class AlienHealth : MonoBehaviour
{
    [Header("Health")]
    public int maxHealth = 3;
    public int scoreValue = 10;

    [Header("Hit Effect")]
    public float hitFlashDuration = 0.1f;
    public Color hitFlashColor = Color.white;

    [Header("Events")]
    public UnityEvent onDeath;
    public UnityEvent onHit;

    [Header("Audio")]
    public AudioClip hitSound;
    public AudioClip deathSound;

    private int currentHealth;
    private bool isDead = false;
    private bool wasLastHitHeadshot = false;
    private AudioSource audioSource;
    private List<Renderer> renderers = new List<Renderer>();
    private Dictionary<Renderer, Color[]> originalColors = new Dictionary<Renderer, Color[]>();

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
        }

        // Cache all renderers and their original colors
        GetComponentsInChildren<Renderer>(renderers);
        foreach (var rend in renderers)
        {
            Color[] colors = new Color[rend.materials.Length];
            for (int i = 0; i < rend.materials.Length; i++)
            {
                if (rend.materials[i].HasProperty("_BaseColor"))
                    colors[i] = rend.materials[i].GetColor("_BaseColor");
                else if (rend.materials[i].HasProperty("_Color"))
                    colors[i] = rend.materials[i].color;
                else
                    colors[i] = Color.white;
            }
            originalColors[rend] = colors;
        }
    }

    void Start()
    {
        currentHealth = maxHealth;

        // Auto-generate sounds if not assigned
        if (hitSound == null) hitSound = ProceduralAudioGenerator.GenerateHitImpact();
        if (deathSound == null) deathSound = ProceduralAudioGenerator.GenerateAlienDeath();
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        isDead = false;
    }

    public void TakeDamage(int amount)
    {
        TakeDamage(amount, false);
    }

    /// <summary>
    /// Location-based damage entry point. Headshots flash gold and award bonus score.
    /// </summary>
    public void TakeDamage(int amount, bool isHeadshot)
    {
        if (isDead) return;

        wasLastHitHeadshot = isHeadshot;
        currentHealth -= amount;
        onHit?.Invoke();

        // The model is built after Awake, so the renderer cache can be empty on
        // the first hit — refresh it lazily so hit flashes actually show.
        if (renderers.Count == 0)
        {
            GetComponentsInChildren<Renderer>(renderers);
            foreach (var rend in renderers)
            {
                if (originalColors.ContainsKey(rend)) continue;
                Color[] colors = new Color[rend.materials.Length];
                for (int i = 0; i < rend.materials.Length; i++)
                {
                    if (rend.materials[i].HasProperty("_BaseColor"))
                        colors[i] = rend.materials[i].GetColor("_BaseColor");
                    else if (rend.materials[i].HasProperty("_Color"))
                        colors[i] = rend.materials[i].color;
                    else
                        colors[i] = Color.white;
                }
                originalColors[rend] = colors;
            }
        }

        // Hit flash (gold for headshots, white for body hits)
        hitFlashColor = isHeadshot ? new Color(1f, 0.85f, 0.2f) : Color.white;
        StartCoroutine(HitFlash());

        // Hit sound
        if (hitSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(hitSound);
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    IEnumerator HitFlash()
    {
        // Flash all renderers white
        foreach (var rend in renderers)
        {
            if (rend == null) continue;
            foreach (var mat in rend.materials)
            {
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", hitFlashColor);
                else if (mat.HasProperty("_Color"))
                    mat.color = hitFlashColor;

                if (mat.HasProperty("_EmissionColor"))
                    mat.SetColor("_EmissionColor", hitFlashColor * 3f);
            }
        }

        yield return new WaitForSeconds(hitFlashDuration);

        // Restore original colors
        foreach (var rend in renderers)
        {
            if (rend == null) continue;
            if (!originalColors.ContainsKey(rend)) continue;

            Color[] colors = originalColors[rend];
            for (int i = 0; i < rend.materials.Length && i < colors.Length; i++)
            {
                if (rend.materials[i].HasProperty("_BaseColor"))
                    rend.materials[i].SetColor("_BaseColor", colors[i]);
                else if (rend.materials[i].HasProperty("_Color"))
                    rend.materials[i].color = colors[i];
            }
        }
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        // Play death sound
        if (deathSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(deathSound);
        }

        // Notify AI
        AlienAI ai = GetComponent<AlienAI>();
        if (ai != null)
        {
            ai.OnDying();
        }

        // Notify GameManager (headshot kills award 50% bonus score)
        int awardedScore = wasLastHitHeadshot ? Mathf.RoundToInt(scoreValue * 1.5f) : scoreValue;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnAlienKilled(awardedScore);
        }

        // Trigger death VFX
        AlienDeathVFX deathVFX = GetComponent<AlienDeathVFX>();
        if (deathVFX != null)
        {
            deathVFX.PlayDeathEffect(scoreValue);
        }
        else
        {
            // Fallback: just destroy
            Destroy(gameObject, 0.1f);
        }

        onDeath?.Invoke();
    }

    public float GetHealthPercentage()
    {
        return (float)currentHealth / maxHealth;
    }
}
