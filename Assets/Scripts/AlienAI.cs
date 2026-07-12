using UnityEngine;
using System.Collections;

public class AlienAI : MonoBehaviour
{
    public enum AIState { Spawning, Chasing, Attacking, Dying }

    [Header("State")]
    public AIState currentState = AIState.Spawning;

    [Header("Movement")]
    public float moveSpeed = 0.3f;
    public float walkSpeed = 0.15f;
    public float runSpeed = 0.5f;
    public float sprintSpeed = 1.0f;
    public float rotationSpeed = 5f;
    public float strafeAmount = 0.3f;
    public float strafeSpeed = 1.5f;
    public float zigzagFrequency = 0.8f;

    [Header("Attack")]
    public float attackRange = 2.0f;   // stop this far (horizontal) from the player and attack
    public float attackCooldown = 1.5f;
    public int attackDamage = 10;

    [Header("Spawn Animation")]
    public float spawnDuration = 1.2f;

    [Header("Audio")]
    public AudioClip attackSound;
    public AudioClip spawnSound;
    public AudioClip idleSound;

    // Runtime
    private Transform playerTarget;
    private float attackTimer;
    private float spawnTimer;
    private Vector3 originalScale;
    private float strafePhase;
    private AudioSource audioSource;
    private AudioSource idleAudioSource;
    private AlienHealth alienHealth;
    private AlienModelBuilder modelBuilder;
    private bool isInitialized = false;
    private float aggressionMultiplier = 1f;
    private float zigzagTimer;
    private int zigzagDir = 1;
    private float vocalTimer;
    private float floorY;
    private AudioClip approachSound;
    private AudioClip chargeSound;
    private bool hasPlayedCharge = false;
    private int waveNumber = 1;
    private float animSpeed = 0.5f;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
            audioSource.maxDistance = 15f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
        }

        alienHealth = GetComponent<AlienHealth>();
        modelBuilder = GetComponent<AlienModelBuilder>();
    }

    void Start()
    {
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            playerTarget = mainCam.transform;
        }

        originalScale = transform.localScale;
        strafePhase = Random.Range(0f, Mathf.PI * 2f);
        zigzagTimer = Random.Range(0f, 2f);
        vocalTimer = Random.Range(2f, 5f);
        attackTimer = 0f;

        // Auto-generate sounds
        if (spawnSound == null) spawnSound = ProceduralAudioGenerator.GenerateAlienSpawn();
        if (attackSound == null) attackSound = ProceduralAudioGenerator.GenerateAlienAttack();
        if (idleSound == null) idleSound = ProceduralAudioGenerator.GenerateAlienIdle();
        approachSound = ProceduralAudioGenerator.GenerateAlienApproach();
        chargeSound = ProceduralAudioGenerator.GenerateAlienCharge();

        // Store floor Y
        floorY = transform.position.y;

        // Determine speed based on wave number
        ConfigureSpeedForWave();

        // Spawn animation
        currentState = AIState.Spawning;
        spawnTimer = 0f;
        transform.localScale = Vector3.zero;

        if (spawnSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(spawnSound);
        }

        // Spawn portal VFX
        AlienSpawnVFX.PlayAt(transform.position);

        // Setup idle audio
        SetupIdleAudio();

        isInitialized = true;
    }

    void ConfigureSpeedForWave()
    {
        if (waveNumber <= 2)
        {
            // Early waves: slow walk
            moveSpeed = walkSpeed;
            animSpeed = 0.5f;
        }
        else if (waveNumber <= 4)
        {
            // Mid waves: normal walk to jog
            moveSpeed = Mathf.Lerp(walkSpeed, runSpeed, (waveNumber - 2f) / 2f);
            animSpeed = Mathf.Lerp(0.5f, 1.0f, (waveNumber - 2f) / 2f);
        }
        else if (waveNumber <= 7)
        {
            // Later waves: running
            moveSpeed = runSpeed;
            animSpeed = 1.0f;
        }
        else
        {
            // Very late waves: sprinting
            moveSpeed = Mathf.Lerp(runSpeed, sprintSpeed, Mathf.Min((waveNumber - 7f) / 5f, 1f));
            animSpeed = Mathf.Lerp(1.0f, 1.5f, Mathf.Min((waveNumber - 7f) / 5f, 1f));
        }

        // Set animation speed on model
        if (modelBuilder != null)
        {
            modelBuilder.SetAnimationSpeed(animSpeed);
        }
    }

    void SetupIdleAudio()
    {
        if (idleSound == null) return;

        GameObject idleAudioGO = new GameObject("IdleAudio");
        idleAudioGO.transform.SetParent(transform, false);
        idleAudioSource = idleAudioGO.AddComponent<AudioSource>();
        idleAudioSource.clip = idleSound;
        idleAudioSource.loop = true;
        idleAudioSource.volume = 0.15f;
        idleAudioSource.spatialBlend = 1f;
        idleAudioSource.maxDistance = 10f;
        idleAudioSource.rolloffMode = AudioRolloffMode.Linear;
        idleAudioSource.Play();
    }

    public void Initialize(float speedMultiplier, float healthMultiplier, bool isBoss)
    {
        if (alienHealth != null)
        {
            int newHealth = Mathf.RoundToInt(alienHealth.maxHealth * healthMultiplier);
            alienHealth.maxHealth = newHealth;
            alienHealth.ResetHealth();
        }

        if (isBoss)
        {
            transform.localScale *= 1.5f;
            originalScale = transform.localScale;
            moveSpeed *= 0.7f;
            if (alienHealth != null)
            {
                alienHealth.maxHealth *= 3;
                alienHealth.ResetHealth();
                alienHealth.scoreValue *= 5;
            }

            if (modelBuilder != null)
            {
                modelBuilder.isBossVariant = true;
            }
        }

        // Apply wave speed multiplier on top
        moveSpeed *= speedMultiplier;
    }

    /// <summary>
    /// Set the wave number so the AI knows whether to walk or run.
    /// </summary>
    public void SetWaveNumber(int wave)
    {
        waveNumber = wave;
        ConfigureSpeedForWave();
    }

    void Update()
    {
        if (playerTarget == null || !isInitialized)
            return;

        switch (currentState)
        {
            case AIState.Spawning:
                UpdateSpawning();
                break;
            case AIState.Chasing:
                UpdateChasing();
                break;
            case AIState.Attacking:
                UpdateAttacking();
                break;
            case AIState.Dying:
                break;
        }

        if (currentState != AIState.Dying)
        {
            UpdateAggression();
            UpdateVocalizations();
        }
    }

    void UpdateSpawning()
    {
        spawnTimer += Time.deltaTime;
        float t = Mathf.Clamp01(spawnTimer / spawnDuration);

        // Rise up from the ground
        float elastic = 1f - Mathf.Pow(2f, -10f * t) * Mathf.Cos(t * Mathf.PI * 3f);
        transform.localScale = originalScale * Mathf.Clamp01(elastic);

        RotateTowardPlayer();

        if (t >= 1f)
        {
            transform.localScale = originalScale;
            currentState = AIState.Chasing;
        }
    }

    void UpdateChasing()
    {
        // Use HORIZONTAL distance so the enemy stops in front of the player
        // (and stays shootable) instead of walking into/through the head.
        Vector3 flatDelta = playerTarget.position - transform.position;
        flatDelta.y = 0f;
        float distanceToPlayer = flatDelta.magnitude;

        if (distanceToPlayer <= attackRange)
        {
            currentState = AIState.Attacking;
            attackTimer = 0f;
            if (!hasPlayedCharge && chargeSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(chargeSound, 0.6f);
                hasPlayedCharge = true;
            }
            return;
        }

        hasPlayedCharge = false;

        // Direction to player (ground only)
        Vector3 directionToPlayer = (playerTarget.position - transform.position);
        directionToPlayer.y = 0;
        directionToPlayer.Normalize();

        // Walk STRAIGHT at the player (no strafe/zigzag) so aliens approach
        // head-on instead of drifting in at an angle to the side.
        Vector3 moveDir = directionToPlayer;
        float currentSpeed = moveSpeed * aggressionMultiplier;
        transform.position += moveDir * currentSpeed * Time.deltaTime;

        // GROUND WALKING: Keep Y at floor level, NO bobbing, NO flying
        Vector3 pos = transform.position;
        pos.y = floorY;
        transform.position = pos;

        // Proximity audio
        UpdateProximityAudio(distanceToPlayer);

        RotateTowardPlayer();
    }

    void UpdateAttacking()
    {
        Vector3 flatDelta = playerTarget.position - transform.position;
        flatDelta.y = 0f;
        float distanceToPlayer = flatDelta.magnitude;

        if (distanceToPlayer > attackRange * 1.3f)
        {
            currentState = AIState.Chasing;
            return;
        }

        RotateTowardPlayer();

        attackTimer += Time.deltaTime;
        if (attackTimer >= attackCooldown / aggressionMultiplier)
        {
            attackTimer = 0f;
            PerformAttack();
        }

        // Stay on ground while attacking
        Vector3 pos = transform.position;
        pos.y = floorY;
        transform.position = pos;
    }

    void UpdateProximityAudio(float distance)
    {
        if (idleAudioSource != null)
        {
            float proximityVolume = Mathf.Clamp01(1f - (distance / 8f)) * 0.5f;
            idleAudioSource.volume = 0.1f + proximityVolume;
        }
    }

    void PerformAttack()
    {
        PlayerHealth player = playerTarget.GetComponentInParent<PlayerHealth>();
        if (player == null)
        {
            player = Object.FindFirstObjectByType<PlayerHealth>();
        }

        if (player != null)
        {
            player.TakeDamage(attackDamage);
        }

        if (attackSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(attackSound);
        }

        StartCoroutine(AttackLunge());
    }

    IEnumerator AttackLunge()
    {
        if (playerTarget == null) yield break;

        Vector3 startPos = transform.position;
        Vector3 lungeDir = (playerTarget.position - transform.position).normalized;
        lungeDir.y = 0;
        Vector3 lungePos = startPos + lungeDir * 0.5f;
        lungePos.y = floorY;

        float t = 0f;
        while (t < 0.1f)
        {
            t += Time.deltaTime;
            Vector3 p = Vector3.Lerp(startPos, lungePos, t / 0.1f);
            p.y = floorY;
            transform.position = p;
            yield return null;
        }

        t = 0f;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            Vector3 p = Vector3.Lerp(lungePos, startPos, t / 0.3f);
            p.y = floorY;
            transform.position = p;
            yield return null;
        }

        transform.position = startPos;
    }

    void UpdateAggression()
    {
        if (alienHealth != null)
        {
            float healthPct = alienHealth.GetHealthPercentage();
            aggressionMultiplier = Mathf.Lerp(1.8f, 1f, healthPct);

            // Cloud LLM control seam: if an external enemy director is registered,
            // let it override aggression (Qualcomm AI Cloud integration).
            if (GameBridge.Instance != null && GameBridge.Instance.EnemyDirector != null && playerTarget != null)
            {
                aggressionMultiplier = GameBridge.Instance.EnemyDirector.GetAggression(
                    gameObject, playerTarget.position, healthPct);
            }

            // Speed up animation when aggressive
            if (modelBuilder != null)
            {
                modelBuilder.SetAnimationSpeed(animSpeed * aggressionMultiplier);
            }
        }
    }

    void UpdateVocalizations()
    {
        vocalTimer -= Time.deltaTime;
        if (vocalTimer <= 0f)
        {
            vocalTimer = Random.Range(3f, 7f) / aggressionMultiplier;

            if (audioSource != null && !audioSource.isPlaying)
            {
                audioSource.pitch = Random.Range(0.7f, 1.3f);
                audioSource.PlayOneShot(attackSound, 0.25f);
            }
        }
    }

    void RotateTowardPlayer()
    {
        if (playerTarget == null) return;

        Vector3 direction = playerTarget.position - transform.position;
        direction.y = 0;
        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    public void OnDying()
    {
        currentState = AIState.Dying;
        Collider[] cols = GetComponentsInChildren<Collider>();
        foreach (var col in cols) col.enabled = false;

        if (idleAudioSource != null) idleAudioSource.Stop();

        // Stop animation
        if (modelBuilder != null && modelBuilder.animator != null)
            modelBuilder.animator.enabled = false;

        Animation legacyAnim = GetComponentInChildren<Animation>();
        if (legacyAnim != null) legacyAnim.Stop();
    }
}
