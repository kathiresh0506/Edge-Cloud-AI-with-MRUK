using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections;

public class PlayerShooter : MonoBehaviour
{
    [Header("Input")]
    public UnityEngine.XR.XRNode controllerNode = UnityEngine.XR.XRNode.RightHand;

    [Header("Shooting")]
    public float fireRate = 0.15f;
    public float range = 50f;
    public int damage = 1;
    public Transform muzzlePoint;

    [Header("Hit Filtering (Gem Hunt)")]
    [Tooltip("Raycast hits are filtered to these layers only. Auto-set to Alien|Boss when left empty.")]
    public LayerMask hitLayers = 0;
    [Tooltip("Semi-auto: one shot per trigger pull (pistol style). Off = hold to auto-fire.")]
    public bool semiAuto = true;

    [Header("Location Damage")]
    [Tooltip("Damage on a head collider hit — kills a regular alien (3 HP) instantly.")]
    public int headshotDamage = 3;
    [Tooltip("Damage on a body collider hit — roughly a third of a headshot.")]
    public int bodyshotDamage = 1;

    /// <summary>Total trigger pulls this run — shown on the victory screen.</summary>
    public static int TotalShotsFired = 0;

    [Header("Laser Visual")]
    public LineRenderer laserLine;
    public float laserDuration = 0.08f;
    public float laserWidth = 0.05f;
    public Color laserColor = new Color(0f, 0.9f, 1f, 1f);

    [Header("Aim Laser Sight (always on while playing)")]
    public bool showAimLaser = true;
    public Color aimLaserColor = new Color(1f, 0.15f, 0.1f, 0.85f);
    public float aimLaserMaxDistance = 25f;
    private LineRenderer aimLaser;

    [Header("Bullet Tracer")]
    public bool showTracers = true;
    public Color tracerColor = new Color(1f, 0.85f, 0.35f, 1f);
    public float tracerSpeed = 55f;

    [Header("Haptics")]
    public UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor controllerInteractor;
    public float shootHapticAmplitude = 0.3f;
    public float shootHapticDuration = 0.05f;
    public float killHapticAmplitude = 0.7f;
    public float killHapticDuration = 0.15f;

    [Header("Audio")]
    public AudioClip shootSound;
    public AudioClip hitSound;
    public AudioClip killSound;

    [Header("VFX")]
    public ParticleSystem muzzleFlash;
    public ParticleSystem hitEffect;

    [Header("Gun Model")]
    public bool autoCreateGun = true;
    public string gunModelAssetPath = "Assets/Resources/Models/PistolGun.glb";

    [Header("Recoil")]
    public float recoilKickback = 0.02f;
    public float recoilRecoverSpeed = 10f;

    [Header("ADS (Aim Down Sights)")]
    public float adsDistanceThreshold = 0.35f;
    public float adsDamageMultiplier = 1.5f;
    public float adsAccuracyBonus = 0.5f; // Reduces spread

    [Header("Reload")]
    public int magazineSize = 30;
    public float reloadTime = 1.6f;

    [Header("Melee Punch")]
    public float punchVelocityThreshold = 1.6f; // m/s swing to register a punch
    public float punchRange = 0.9f;
    public int punchDamage = 3;
    public float punchCooldown = 0.5f;

    // Default weapon stats (saved for revert)
    private int defaultDamage;
    private float defaultFireRate;
    private float defaultLaserWidth;
    private Color defaultLaserColor;
    private AudioClip defaultShootSound;

    // Weapon pickup state
    private int weaponAmmo = -1; // -1 = infinite (default weapon)
    private int weaponPellets = 1;
    private float weaponSpread = 0f;
    private string equippedWeaponName = "";
    private bool hasSpecialWeapon = false;

    private float nextFireTime;
    private AudioSource audioSource;
    private bool gameStarted = false;
    private bool previousTriggerState = false;
    private bool previousGripState = false;
    private GunModelBuilder gunModel;
    private Vector3 gunRestPosition;
    private float currentRecoil = 0f;
    private Transform cameraTransform;
    private bool isADS = false;

    // Reload state
    private int currentMag;
    private bool isReloading = false;
    private AudioClip reloadSound;
    private bool previousReloadState = false;

    // Melee state
    private AudioClip punchSound;
    private float nextPunchTime = 0f;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        // Auto-generate sounds if not assigned — realistic firearm crack, not the laser pew
        if (shootSound == null) shootSound = ProceduralAudioGenerator.GenerateGunshot();
        if (hitSound == null) hitSound = ProceduralAudioGenerator.GenerateHitImpact();
        if (killSound == null) killSound = ProceduralAudioGenerator.GenerateKillSound();
        reloadSound = ProceduralAudioGenerator.GenerateReload();
        punchSound = ProceduralAudioGenerator.GeneratePunch();
        currentMag = magazineSize;

        // Save defaults for weapon revert
        defaultDamage = damage;
        defaultFireRate = fireRate;
        defaultLaserWidth = laserWidth;
        defaultLaserColor = laserColor;
        defaultShootSound = shootSound;

        // Get camera for ADS
        cameraTransform = Camera.main?.transform;

        // Filter hits to enemy layers only (spec: 'Alien' and 'Boss')
        if (hitLayers.value == 0)
            hitLayers = LayerMask.GetMask("Alien", "Boss");

        // Build gun model
        if (autoCreateGun)
        {
            SetupGunModel();
        }

        SetupLaser();
        SetupAimLaser();
        SetupMuzzleFlash();
        SetupHitEffect();

        if (muzzlePoint == null)
        {
            if (gunModel != null && gunModel.muzzleTip != null)
                muzzlePoint = gunModel.muzzleTip;
            else
                muzzlePoint = transform;
        }
    }

    void SetupGunModel()
    {
        gunModel = GetComponent<GunModelBuilder>();
        if (gunModel == null)
        {
            gunModel = gameObject.AddComponent<GunModelBuilder>();
        }
        if (!string.IsNullOrEmpty(gunModelAssetPath))
            gunModel.defaultModelAssetPath = gunModelAssetPath;
        gunModel.Build();

        if (gunModel.muzzleTip != null)
        {
            muzzlePoint = gunModel.muzzleTip;
        }

        Transform gunRoot = transform.Find("GunRoot");
        if (gunRoot != null)
        {
            gunRestPosition = gunRoot.localPosition;
        }
    }

    void SetupLaser()
    {
        if (laserLine == null)
        {
            laserLine = gameObject.AddComponent<LineRenderer>();
        }

        laserLine.positionCount = 2;
        laserLine.startWidth = laserWidth;
        laserLine.endWidth = laserWidth * 0.3f;

        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        if (shader != null)
        {
            laserLine.material = new Material(shader);
            laserLine.material.color = laserColor;
            laserLine.material.SetColor("_BaseColor", laserColor);
            if (laserLine.material.HasProperty("_EmissionColor"))
            {
                laserLine.material.EnableKeyword("_EMISSION");
                laserLine.material.SetColor("_EmissionColor", laserColor * 5f);
            }
        }

        laserLine.startColor = laserColor;
        laserLine.endColor = new Color(laserColor.r, laserColor.g, laserColor.b, 0.3f);
        laserLine.enabled = false;
    }

    /// <summary>
    /// Thin always-on laser sight from the muzzle so the player can see EXACTLY
    /// where the gun is pointing before pulling the trigger.
    /// </summary>
    void SetupAimLaser()
    {
        GameObject go = new GameObject("AimLaser");
        go.transform.SetParent(transform, false);
        aimLaser = go.AddComponent<LineRenderer>();
        aimLaser.positionCount = 2;
        aimLaser.startWidth = 0.0035f;
        aimLaser.endWidth = 0.0012f;

        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader != null)
        {
            Material mat = new Material(shader);
            mat.color = aimLaserColor;
            mat.SetColor("_BaseColor", aimLaserColor);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", aimLaserColor * 3f);
            }
            aimLaser.material = mat;
        }
        aimLaser.startColor = aimLaserColor;
        aimLaser.endColor = new Color(aimLaserColor.r, aimLaserColor.g, aimLaserColor.b, 0f);
        aimLaser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        aimLaser.receiveShadows = false;
        aimLaser.enabled = false;
    }

    void UpdateAimLaser()
    {
        if (aimLaser == null) return;

        bool active = showAimLaser && gameStarted && muzzlePoint != null && !isReloading;
        aimLaser.enabled = active;
        if (!active) return;

        Vector3 origin = muzzlePoint.position;
        Vector3 dir = muzzlePoint.forward;

        // Stop the beam at whatever a real shot would hit
        Vector3 end = origin + dir * aimLaserMaxDistance;
        if (Physics.Raycast(origin, dir, out RaycastHit hit, aimLaserMaxDistance, hitLayers))
            end = hit.point;

        aimLaser.SetPosition(0, origin);
        aimLaser.SetPosition(1, end);
    }

    void SetupMuzzleFlash()
    {
        if (muzzleFlash != null) return;

        GameObject flashGO = new GameObject("MuzzleFlash");
        flashGO.transform.SetParent(muzzlePoint != null ? muzzlePoint : transform, false);
        flashGO.transform.localPosition = Vector3.zero;

        muzzleFlash = flashGO.AddComponent<ParticleSystem>();
        var main = muzzleFlash.main;
        main.duration = 0.05f;
        main.startLifetime = 0.08f;
        main.startSpeed = 3f;
        main.startSize = 0.06f;
        main.maxParticles = 8;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = muzzleFlash.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, 6)
        });

        var shape = muzzleFlash.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 15f;
        shape.radius = 0.01f;

        var colorOverLifetime = muzzleFlash.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f),  // hot white-yellow core
                new GradientColorKey(new Color(1f, 0.45f, 0.05f), 1f)  // orange fade
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        ParticleSystemRenderer psRenderer = flashGO.GetComponent<ParticleSystemRenderer>();
        var matShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (matShader == null) matShader = Shader.Find("Standard");
        if (matShader != null)
        {
            Color flashColor = new Color(1f, 0.7f, 0.2f);   // warm muzzle flash
            Material mat = new Material(matShader);
            mat.SetColor("_BaseColor", flashColor);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", flashColor * 6f);
            }
            psRenderer.material = mat;
        }
    }

    void SetupHitEffect()
    {
        if (hitEffect != null) return;

        GameObject hitGO = new GameObject("HitEffect");
        hitGO.transform.SetParent(transform, false);

        hitEffect = hitGO.AddComponent<ParticleSystem>();
        var main = hitEffect.main;
        main.duration = 0.1f;
        main.startLifetime = 0.3f;
        main.startSpeed = 2f;
        main.startSize = 0.04f;
        main.maxParticles = 12;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.5f;

        var emission = hitEffect.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, 10)
        });

        var shape = hitEffect.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.05f;

        ParticleSystemRenderer psRenderer = hitGO.GetComponent<ParticleSystemRenderer>();
        var matShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (matShader == null) matShader = Shader.Find("Standard");
        if (matShader != null)
        {
            Material mat = new Material(matShader);
            mat.SetColor("_BaseColor", new Color(1f, 0.6f, 0.2f));
            psRenderer.material = mat;
        }
    }

    void Update()
    {
        // Keep the aim laser tracking the muzzle every frame (self-disables when idle)
        UpdateAimLaser();

        // Read low-level XR input
        bool triggerPressed = false;
        bool gripPressed = false;

        bool reloadPressed = false;
        UnityEngine.XR.InputDevice device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(controllerNode);
        if (device.isValid)
        {
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool tVal))
                triggerPressed = tVal;
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out bool gVal))
                gripPressed = gVal;
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool pVal))
                reloadPressed = pVal;
        }

        bool reloadWasPressedThisFrame = reloadPressed && !previousReloadState;
        previousReloadState = reloadPressed;

        bool triggerWasPressedThisFrame = triggerPressed && !previousTriggerState;
        bool gripWasPressedThisFrame = gripPressed && !previousGripState;

        previousTriggerState = triggerPressed;
        previousGripState = gripPressed;

        // ===== GRIP = RESET FROM ANY STATE =====
        if (gripWasPressedThisFrame && GameManager.Instance != null)
        {
            if (GameManager.Instance.currentState == GameManager.GameState.GameOver)
            {
                GameManager.Instance.RestartGame();
                gameStarted = false;
            }
            else if (GameManager.Instance.currentState == GameManager.GameState.Playing
                  || GameManager.Instance.currentState == GameManager.GameState.WaveComplete)
            {
                GameManager.Instance.RestartGame();
                gameStarted = false;
            }
        }

        // Check for game start
        if (!gameStarted)
        {
            if (triggerWasPressedThisFrame)
            {
                if (GameManager.Instance != null && GameManager.Instance.currentState == GameManager.GameState.WaitingToStart)
                {
                    GameManager.Instance.StartGame();
                    gameStarted = true;
                    return;
                }
                else if (GameManager.Instance != null && GameManager.Instance.currentState == GameManager.GameState.GameOver)
                {
                    GameManager.Instance.RestartGame();
                    gameStarted = false;
                    return;
                }
                gameStarted = true;
            }
            return;
        }

        // ADS detection
        UpdateADS();

        // Manual reload (default weapon only)
        if (reloadWasPressedThisFrame && !hasSpecialWeapon && !isReloading && currentMag < magazineSize)
        {
            StartReload();
        }

        // Melee: swing the controller fast near an enemy to punch
        HandleMeleePunch(device);

        // Shoot on trigger press (semi-auto = one shot per pull, else hold to fire)
        bool wantsToShoot = semiAuto ? triggerWasPressedThisFrame : triggerPressed;
        if (wantsToShoot && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireRate;
            Shoot();
        }

        // Gun recoil recovery
        UpdateRecoil();
    }

    void UpdateADS()
    {
        if (cameraTransform == null) return;

        float distToHead = Vector3.Distance(transform.position, cameraTransform.position);
        bool wasADS = isADS;
        isADS = distToHead < adsDistanceThreshold;

        // Visual feedback: ADS active (simplified, no glow renderer needed)
    }

    void Shoot()
    {
        if (isReloading) return;

        // Default weapon uses a magazine + reload; special pickups use their own ammo pool.
        if (!hasSpecialWeapon && currentMag <= 0)
        {
            StartReload();
            return;
        }

        Vector3 origin = muzzlePoint.position;
        Vector3 direction = muzzlePoint.forward;

        TotalShotsFired++;

        int shotsToFire = hasSpecialWeapon ? weaponPellets : 1;
        float currentSpread = hasSpecialWeapon ? weaponSpread : 0f;

        // ADS reduces spread and increases damage
        float dmgMultiplier = isADS ? adsDamageMultiplier : 1f;
        if (isADS) currentSpread *= adsAccuracyBonus;

        // Play shoot sound (wider pitch spread = each shot sounds a little different)
        if (shootSound != null && audioSource != null)
        {
            audioSource.pitch = Random.Range(0.85f, 1.18f);
            audioSource.PlayOneShot(shootSound, 0.85f);
        }

        // Muzzle flash
        if (muzzleFlash != null)
        {
            muzzleFlash.transform.position = origin;
            muzzleFlash.transform.rotation = Quaternion.LookRotation(direction);
            muzzleFlash.Play();
        }

        if (gunModel != null) gunModel.OnShoot();
        currentRecoil = recoilKickback;
        TriggerHaptic(shootHapticAmplitude, shootHapticDuration);

        // Fire pellets (1 for normal, multiple for shotgun)
        Vector3 lastEndPoint = origin + direction * range;
        for (int p = 0; p < shotsToFire; p++)
        {
            Vector3 shotDir = direction;
            if (currentSpread > 0f)
            {
                shotDir = Quaternion.Euler(
                    Random.Range(-currentSpread, currentSpread),
                    Random.Range(-currentSpread, currentSpread),
                    0f
                ) * direction;
            }

            RaycastHit hit;
            bool hitSomething = Physics.Raycast(origin, shotDir, out hit, range, hitLayers);

            if (hitSomething)
            {
                lastEndPoint = hit.point;

                // Location-based damage: which collider did we strike?
                HitZone zone = hit.collider.GetComponent<HitZone>();
                bool isHeadshot = zone != null
                    ? zone.isHead
                    : hit.collider.transform.name.Contains("Head");

                int locationDamage = isHeadshot ? headshotDamage : bodyshotDamage;
                int actualDamage = Mathf.Max(1, Mathf.RoundToInt(locationDamage * dmgMultiplier));

                AlienHealth alien = hit.collider.GetComponent<AlienHealth>();
                if (alien == null) alien = hit.collider.GetComponentInParent<AlienHealth>();
                if (alien == null && zone != null && zone.owner != null)
                    alien = zone.owner.GetComponent<AlienHealth>();

                if (alien != null)
                {
                    float healthBefore = alien.GetHealthPercentage();
                    alien.TakeDamage(actualDamage, isHeadshot);
                    float healthAfter = alien.GetHealthPercentage();

                    if (hitSound != null) audioSource.PlayOneShot(hitSound, isHeadshot ? 0.6f : 0.4f);

                    if (isHeadshot)
                        FloatingText.Spawn("HEADSHOT!", hit.point, new Color(1f, 0.85f, 0.2f));

                    if (healthAfter <= 0f && healthBefore > 0f)
                    {
                        TriggerHaptic(killHapticAmplitude, killHapticDuration);
                        if (killSound != null) audioSource.PlayOneShot(killSound, 0.7f);
                    }
                }

                // Gem-hunt boss hit detection (BossHealth)
                BossHealth bossHealth = hit.collider.GetComponent<BossHealth>();
                if (bossHealth == null) bossHealth = hit.collider.GetComponentInParent<BossHealth>();
                if (bossHealth == null && zone != null && zone.owner != null)
                    bossHealth = zone.owner.GetComponent<BossHealth>();

                if (bossHealth != null)
                {
                    bossHealth.TakeDamage(actualDamage, isHeadshot);
                    if (hitSound != null) audioSource.PlayOneShot(hitSound, isHeadshot ? 0.6f : 0.4f);
                    if (isHeadshot)
                        FloatingText.Spawn("HEADSHOT!", hit.point, new Color(1f, 0.85f, 0.2f));
                    TriggerHaptic(shootHapticAmplitude * 1.5f, shootHapticDuration);
                }

                // Legacy boss hit detection (kept for compatibility)
                BossEnemy boss = hit.collider.GetComponent<BossEnemy>();
                if (boss == null) boss = hit.collider.GetComponentInParent<BossEnemy>();
                if (boss != null)
                {
                    boss.TakeDamage(actualDamage);
                    if (hitSound != null) audioSource.PlayOneShot(hitSound, 0.4f);
                    TriggerHaptic(shootHapticAmplitude * 1.5f, shootHapticDuration);
                }

                if (hitEffect != null)
                {
                    hitEffect.transform.position = hit.point;
                    hitEffect.transform.rotation = Quaternion.LookRotation(hit.normal);
                    hitEffect.Play();
                }
            }
            else
            {
                lastEndPoint = origin + shotDir * range;
            }

            // Visible bullet path: a glowing tracer flies from muzzle to impact
            if (showTracers)
            {
                Vector3 pelletEnd = hitSomething ? hit.point : origin + shotDir * range;
                BulletTracer.Spawn(origin, pelletEnd, tracerColor, tracerSpeed);
            }

            // Show laser for each pellet
            if (shotsToFire > 1)
            {
                StartCoroutine(ShowLaser(origin, hitSomething ? hit.point : origin + shotDir * range));
            }
        }

        // Single laser for non-spread weapons
        if (shotsToFire == 1)
        {
            StartCoroutine(ShowLaser(origin, lastEndPoint));
        }

        // Consume ammo for special weapons
        if (hasSpecialWeapon && weaponAmmo > 0)
        {
            weaponAmmo--;
            if (weaponAmmo <= 0)
            {
                RevertToDefault();
            }
        }

        // Consume magazine for the default weapon (auto-reload when empty)
        if (!hasSpecialWeapon)
        {
            currentMag--;
            UpdateAmmoHUD();
            if (currentMag <= 0) StartReload();
        }
    }

    void StartReload()
    {
        if (isReloading) return;
        StartCoroutine(ReloadRoutine());
    }

    IEnumerator ReloadRoutine()
    {
        isReloading = true;
        if (reloadSound != null && audioSource != null)
            audioSource.PlayOneShot(reloadSound, 0.8f);
        UpdateAmmoHUD();

        yield return new WaitForSeconds(reloadTime);

        currentMag = magazineSize;
        isReloading = false;
        UpdateAmmoHUD();
    }

    void UpdateAmmoHUD()
    {
        if (GameManager.Instance != null && GameManager.Instance.gameHUD != null)
        {
            string label = isReloading ? "RELOADING…" : "BLASTER";
            GameManager.Instance.gameHUD.UpdateWeaponInfo(label, currentMag);
        }
    }

    /// <summary>
    /// Velocity-based melee: swing the controller fast while an enemy is in range to punch it.
    /// </summary>
    void HandleMeleePunch(UnityEngine.XR.InputDevice device)
    {
        if (Time.time < nextPunchTime) return;
        if (!device.isValid) return;

        if (!device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceVelocity, out Vector3 vel))
            return;
        if (vel.magnitude < punchVelocityThreshold) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, punchRange);
        foreach (var h in hits)
        {
            AlienHealth alien = h.GetComponentInParent<AlienHealth>();
            if (alien != null)
            {
                alien.TakeDamage(punchDamage);
                DoMeleeFeedback(h.transform.position);
                return;
            }
            BossEnemy boss = h.GetComponentInParent<BossEnemy>();
            if (boss != null)
            {
                boss.TakeDamage(punchDamage);
                DoMeleeFeedback(h.transform.position);
                return;
            }
        }
    }

    void DoMeleeFeedback(Vector3 pos)
    {
        nextPunchTime = Time.time + punchCooldown;
        if (punchSound != null && audioSource != null)
            audioSource.PlayOneShot(punchSound, 0.9f);
        TriggerHaptic(killHapticAmplitude, killHapticDuration);
        if (hitEffect != null)
        {
            hitEffect.transform.position = pos;
            hitEffect.Play();
        }
    }

    // ===== WEAPON EQUIP SYSTEM =====

    public void EquipWeapon(int newDamage, float newFireRate, float newLaserWidth, Color newLaserColor,
        AudioClip newShootSound, int ammo, int pellets, float spread, string weaponName, string modelPath = "")
    {
        damage = newDamage;
        fireRate = newFireRate;
        laserWidth = newLaserWidth;
        laserColor = newLaserColor;
        shootSound = newShootSound;
        weaponAmmo = ammo;
        weaponPellets = pellets;
        weaponSpread = spread;
        equippedWeaponName = weaponName;
        hasSpecialWeapon = true;

        // Swap gun model if path provided
        if (!string.IsNullOrEmpty(modelPath) && gunModel != null)
        {
            gunModel.LoadGunFromAssetPath(modelPath);
            muzzlePoint = gunModel.muzzleTip;
        }

        UpdateLaserColor();

        Debug.Log($"Equipped {weaponName}! Ammo: {ammo}");
    }

    void RevertToDefault()
    {
        damage = defaultDamage;
        fireRate = defaultFireRate;
        laserWidth = defaultLaserWidth;
        laserColor = defaultLaserColor;
        shootSound = defaultShootSound;
        weaponAmmo = -1;
        weaponPellets = 1;
        weaponSpread = 0f;
        equippedWeaponName = "";
        hasSpecialWeapon = false;

        // Revert gun model back to default
        if (gunModel != null)
        {
            gunModel.RevertToDefault();
            muzzlePoint = gunModel.muzzleTip;
        }

        currentMag = magazineSize;
        isReloading = false;
        UpdateAmmoHUD();

        UpdateLaserColor();

        Debug.Log("Reverted to default blaster.");
    }

    void UpdateLaserColor()
    {
        if (laserLine != null && laserLine.material != null)
        {
            laserLine.material.color = laserColor;
            if (laserLine.material.HasProperty("_BaseColor"))
                laserLine.material.SetColor("_BaseColor", laserColor);
            if (laserLine.material.HasProperty("_EmissionColor"))
                laserLine.material.SetColor("_EmissionColor", laserColor * 5f);
        }
    }

    void UpdateRecoil()
    {
        if (currentRecoil > 0f)
        {
            currentRecoil = Mathf.MoveTowards(currentRecoil, 0f, recoilRecoverSpeed * Time.deltaTime);

            Transform gunRoot = transform.Find("GunRoot");
            if (gunRoot != null)
            {
                gunRoot.localPosition = gunRestPosition - Vector3.forward * currentRecoil;
            }
        }
    }

    IEnumerator ShowLaser(Vector3 start, Vector3 end)
    {
        if (laserLine == null) yield break;

        laserLine.enabled = true;
        laserLine.SetPosition(0, start);
        laserLine.SetPosition(1, end);

        float timer = 0f;
        float startWidth = laserWidth;

        while (timer < laserDuration)
        {
            timer += Time.deltaTime;
            float t = timer / laserDuration;

            float width = Mathf.Lerp(startWidth, 0f, t);
            laserLine.startWidth = width;
            laserLine.endWidth = width * 0.3f;

            Color fadeColor = laserColor;
            fadeColor.a = 1f - t;
            laserLine.startColor = fadeColor;
            laserLine.endColor = new Color(fadeColor.r, fadeColor.g, fadeColor.b, fadeColor.a * 0.3f);

            yield return null;
        }

        laserLine.enabled = false;
        laserLine.startWidth = startWidth;
        laserLine.endWidth = startWidth * 0.3f;
    }

    void TriggerHaptic(float amplitude, float duration)
    {
        if (controllerInteractor != null)
        {
            controllerInteractor.SendHapticImpulse(amplitude, duration);
        }
    }
}
