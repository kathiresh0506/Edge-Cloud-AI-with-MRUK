using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// The final boss of the gem hunt. Spawns at the nearest real wall after all 4 gems
/// are collected, stalks the player through the darkness, and uses the shared
/// headshot/bodyshot damage system via BossHealth + HitZone colliders.
/// </summary>
[RequireComponent(typeof(BossHealth))]
public class GemBossController : MonoBehaviour
{
    [Header("Model")]
    public string bossModelAssetPath = "Assets/Resources/Models/BossMonster.glb";
    public float bossHeight = 2.4f;

    [Header("Movement")]
    public float moveSpeed = 0.35f;
    public float attackRange = 1.8f;
    public int attackDamage = 20;
    public float attackCooldown = 2.5f;

    [Header("Presence")]
    public Color eyeGlowColor = new Color(1f, 0.15f, 0.05f);

    [Header("Animation (Resources path)")]
    public string animatorControllerResourcePath = "Models/BossMonsterController";

    // Runtime
    private BossHealth health;
    private GameObject modelInstance;
    private Transform playerTarget;
    private float lastAttackTime;
    private float floorY;
    private bool isActive = false;
    private AudioSource audioSource;
    private Image healthFill;
    private Transform healthBarRoot;
    private Bounds modelBounds;
    private Animator animator;

    public System.Action onDefeated;

    void Awake()
    {
        health = GetComponent<BossHealth>();
    }

    void Start()
    {
        floorY = transform.position.y;
        playerTarget = Camera.main != null ? Camera.main.transform : null;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.4f;

        BuildModel();
        BuildHealthBar();

        health.onDeath.AddListener(OnDeath);
        health.CacheRenderers();
    }

    void BuildModel()
    {
        GameObject prefab = ModelUtil.LoadPrefab(bossModelAssetPath);
        if (prefab != null)
        {
            // Instantiate unparented, normalize to bossHeight, then attach
            modelInstance = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            modelInstance.name = "BossModel";
            modelBounds = ModelUtil.NormalizeHeight(modelInstance, bossHeight);
            float footOffset = modelInstance.transform.position.y - modelBounds.min.y;

            modelInstance.transform.SetParent(transform, false);
            modelInstance.transform.localPosition = new Vector3(0f, footOffset, 0f);
            modelInstance.transform.localRotation = Quaternion.identity;

            ModelUtil.StripColliders(modelInstance);
            // Only bounds.size is used from here on (hit zones, glow, health bar height)
            modelBounds = ModelUtil.GetRendererBounds(modelInstance);
        }
        else
        {
            // Fallback: big menacing capsule
            modelInstance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            modelInstance.name = "BossModel_Fallback";
            Destroy(modelInstance.GetComponent<Collider>());
            modelInstance.transform.SetParent(transform, false);
            modelInstance.transform.localPosition = new Vector3(0f, bossHeight * 0.5f, 0f);
            modelInstance.transform.localScale = new Vector3(bossHeight * 0.45f, bossHeight * 0.5f, bossHeight * 0.45f);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (lit != null)
            {
                Material m = new Material(lit);
                m.SetColor("_BaseColor", new Color(0.1f, 0.02f, 0.02f));
                modelInstance.GetComponent<Renderer>().material = m;
            }
            modelBounds = new Bounds(new Vector3(0f, bossHeight * 0.5f, 0f),
                new Vector3(bossHeight * 0.5f, bossHeight, bossHeight * 0.5f));
        }

        BuildHitZones();
        AddEyeGlow();
        SetupAnimator();
    }

    void SetupAnimator()
    {
        if (modelInstance == null) return;
        animator = modelInstance.GetComponentInChildren<Animator>(true);
        if (animator == null) return;

        RuntimeAnimatorController ctrl =
            Resources.Load<RuntimeAnimatorController>(animatorControllerResourcePath);
        if (ctrl != null)
        {
            animator.runtimeAnimatorController = ctrl;
            animator.enabled = true;
            animator.applyRootMotion = false; // GemBossController drives position itself
        }
        else
        {
            animator.enabled = false;
        }
    }

    /// <summary>
    /// Two-zone hit detection like the aliens: big body capsule + smaller head sphere,
    /// both on the 'Boss' layer so the pistol raycast reaches them.
    /// </summary>
    void BuildHitZones()
    {
        float h = Mathf.Max(modelBounds.size.y, bossHeight);
        float w = Mathf.Max(Mathf.Max(modelBounds.size.x, modelBounds.size.z) * 0.5f, 0.3f);

        CapsuleCollider bodyCol = gameObject.GetComponent<CapsuleCollider>();
        if (bodyCol == null) bodyCol = gameObject.AddComponent<CapsuleCollider>();
        bodyCol.center = new Vector3(0f, h * 0.42f, 0f);
        bodyCol.height = h * 0.84f;
        bodyCol.radius = Mathf.Min(w * 0.7f, h * 0.22f);
        HitZone.Attach(gameObject, gameObject, false);

        GameObject headHitbox = new GameObject("Head_Hitbox");
        headHitbox.transform.SetParent(transform, false);
        headHitbox.transform.localPosition = new Vector3(0f, h * 0.88f, 0f);
        SphereCollider headCol = headHitbox.AddComponent<SphereCollider>();
        headCol.radius = Mathf.Clamp(h * 0.12f, 0.12f, 0.35f);
        HitZone.Attach(headHitbox, gameObject, true);

        int bossLayer = LayerMask.NameToLayer("Boss");
        if (bossLayer >= 0)
            ModelUtil.SetLayerRecursive(gameObject, bossLayer);
    }

    void AddEyeGlow()
    {
        // Menacing glow visible even in full darkness
        GameObject lightGO = new GameObject("BossGlow");
        lightGO.transform.SetParent(transform, false);
        lightGO.transform.localPosition = new Vector3(0f, modelBounds.size.y * 0.8f, 0.1f);
        Light l = lightGO.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = eyeGlowColor;
        l.range = 3.5f;
        l.intensity = 2.5f;
        l.shadows = LightShadows.None;
    }

    void BuildHealthBar()
    {
        // Small world-space bar floating above the boss
        GameObject canvasGO = new GameObject("BossHealthBar");
        canvasGO.transform.SetParent(transform, false);
        canvasGO.transform.localPosition = new Vector3(0f, Mathf.Max(modelBounds.size.y, bossHeight) + 0.35f, 0f);
        healthBarRoot = canvasGO.transform;

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(200, 24);
        canvasRect.localScale = Vector3.one * 0.004f;

        GameObject bgGO = new GameObject("BG");
        bgGO.transform.SetParent(canvasGO.transform, false);
        RectTransform bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;
        Image bg = bgGO.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.02f, 0.02f, 0.85f);
        bg.raycastTarget = false;

        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(bgGO.transform, false);
        RectTransform fillRect = fillGO.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2, 2); fillRect.offsetMax = new Vector2(-2, -2);
        healthFill = fillGO.AddComponent<Image>();
        healthFill.color = new Color(1f, 0.2f, 0.1f);
        healthFill.type = Image.Type.Filled;
        healthFill.fillMethod = Image.FillMethod.Horizontal;
        healthFill.fillAmount = 1f;
        healthFill.raycastTarget = false;
    }

    public void ActivateBoss()
    {
        isActive = true;

        // Entrance roar + rumble
        if (audioSource != null)
        {
            audioSource.PlayOneShot(ProceduralAudioGenerator.GenerateDeepRumble(), 0.9f);
            audioSource.PlayOneShot(ProceduralAudioGenerator.GenerateAlienCharge(), 0.8f);
        }

        // Dramatic entrance animation
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.CrossFade("Roar", 0.1f);
    }

    void Update()
    {
        if (!isActive || health == null || health.IsDead) return;

        if (playerTarget == null)
        {
            if (Camera.main != null) playerTarget = Camera.main.transform;
            return;
        }

        // Stay on the floor
        Vector3 pos = transform.position;
        pos.y = floorY;
        transform.position = pos;

        // Face the player
        Vector3 dirToPlayer = playerTarget.position - transform.position;
        dirToPlayer.y = 0f;
        float distToPlayer = dirToPlayer.magnitude;
        if (dirToPlayer.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dirToPlayer.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 2.5f);
        }

        if (distToPlayer > attackRange)
        {
            // Heavy stalk toward the player
            transform.position += dirToPlayer.normalized * moveSpeed * Time.deltaTime;
        }
        else if (Time.time - lastAttackTime > attackCooldown)
        {
            lastAttackTime = Time.time;
            Attack();
        }

        // Health bar faces the player
        if (healthBarRoot != null && playerTarget != null)
        {
            healthBarRoot.rotation = Quaternion.LookRotation(healthBarRoot.position - playerTarget.position);
            if (healthFill != null) healthFill.fillAmount = health.GetHealthPercentage();
        }
    }

    void Attack()
    {
        if (GameManager.Instance != null && GameManager.Instance.playerHealth != null)
            GameManager.Instance.playerHealth.TakeDamage(attackDamage);

        if (audioSource != null)
            audioSource.PlayOneShot(ProceduralAudioGenerator.GenerateAlienAttack(), 0.9f);

        if (animator != null && animator.runtimeAnimatorController != null)
            animator.CrossFade("Attack", 0.15f);

        StartCoroutine(AttackLunge());
    }

    IEnumerator AttackLunge()
    {
        if (playerTarget == null) yield break;

        Vector3 startPos = transform.position;
        Vector3 lungeDir = (playerTarget.position - transform.position).normalized;
        lungeDir.y = 0f;
        Vector3 lungePos = startPos + lungeDir * 0.4f;

        float t = 0f;
        while (t < 0.12f)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, lungePos, t / 0.12f);
            yield return null;
        }
        t = 0f;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(lungePos, startPos, t / 0.3f);
            yield return null;
        }
    }

    void OnDeath()
    {
        isActive = false;
        if (healthBarRoot != null) healthBarRoot.gameObject.SetActive(false);

        // Disable colliders so dying boss can't be shot
        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        onDefeated?.Invoke();
    }
}
