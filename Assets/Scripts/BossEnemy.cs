using UnityEngine;
using System.Collections;

/// <summary>
/// Boss enemy controller — Darth Vader.
/// Walks slowly toward player, high HP, dialogue system.
/// </summary>
public class BossEnemy : MonoBehaviour
{
    [Header("Model")]
    // Rigged + animated humanoid so the boss actually walks (limbs move), not a fixed pose.
    // Drop any rigged GLB here to replace the figure.
    public string bossModelAssetPath = "Assets/Resources/Models/Zombie Running.glb";
    public float modelScale = 1.25f;   // towering boss
    public float walkClipSpeed = 0.6f; // slow the run into a heavy, menacing walk

    [Header("Stats")]
    public int maxHealth = 50;
    public float moveSpeed = 0.3f;
    public float attackRange = 2f;
    public int attackDamage = 15;
    public float attackCooldown = 3f;

    [Header("Score")]
    public int scoreValue = 500;

    [Header("Walk Animation")]
    public float walkCadence = 7f;      // steps per second feel
    public float bobHeight = 0.06f;     // vertical body bob per step
    public float swayAmount = 0.04f;    // side-to-side shift
    public float leanAngle = 4f;        // body roll while striding

    // Runtime
    private int currentHealth;
    private GameObject modelInstance;
    private Transform playerTarget;
    private float lastAttackTime;
    private bool isDead = false;
    private bool isActive = false;
    private float floorY;
    private AudioSource audioSource;   // spatial hit/attack sounds
    private AudioSource voiceSource;   // 2D dialogue voice stinger
    private AudioSource breathSource;  // 2D looping breathing
    private BossDialogue dialogue;
    private Renderer[] bossRenderers;

    // Walk state
    private float walkPhase = 0f;
    private Vector3 modelBaseLocalPos = Vector3.zero;
    private Animation legacyWalkAnim;
    private Transform auraRing;

    // Events
    public System.Action onBossDefeated;

    void Start()
    {
        currentHealth = maxHealth;
        floorY = transform.position.y;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.35f;  // mostly 2D so it's clearly audible
        audioSource.volume = 1f;

        // 2D voice stinger for spoken lines
        voiceSource = gameObject.AddComponent<AudioSource>();
        voiceSource.spatialBlend = 0f;
        voiceSource.volume = 0.9f;
        voiceSource.playOnAwake = false;

        // 2D looping mechanical breathing
        breathSource = gameObject.AddComponent<AudioSource>();
        breathSource.spatialBlend = 0f;
        breathSource.volume = 0.5f;
        breathSource.loop = true;
        breathSource.playOnAwake = false;
        breathSource.clip = ProceduralAudioGenerator.GenerateVaderBreath();

        BuildModel();
        SetupDialogue();

        // Find player
        if (Camera.main != null)
            playerTarget = Camera.main.transform;
    }

    void BuildModel()
    {
        // Load Vader GLB
        string resourcePath = bossModelAssetPath.Replace("Assets/Resources/", "")
            .Replace(".glb", "").Replace(".fbx", "");
        GameObject prefab = Resources.Load<GameObject>(resourcePath);
        #if UNITY_EDITOR
        if (prefab == null) prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(bossModelAssetPath);
        #endif

        if (prefab == null)
        {
            Debug.LogWarning("BossEnemy: Vader model not found, building fallback");
            BuildFallbackModel();
            return;
        }

        modelInstance = Instantiate(prefab);
        modelInstance.name = "BossModel_Vader";
        modelInstance.transform.SetParent(transform, false);
        modelInstance.transform.localPosition = Vector3.zero;
        modelInstance.transform.localRotation = Quaternion.identity;
        modelInstance.transform.localScale = Vector3.one * modelScale;

        // Remove colliders
        foreach (var col in modelInstance.GetComponentsInChildren<Collider>())
            DestroyImmediate(col);

        // Textured, dark, demonic figure
        ApplyBossFigureMaterials(modelInstance);

        // Add collider for hit detection
        CapsuleCollider hitCol = gameObject.AddComponent<CapsuleCollider>();
        hitCol.center = new Vector3(0f, 1.0f * modelScale, 0f);
        hitCol.height = 2.1f * modelScale;
        hitCol.radius = 0.4f * modelScale;

        bossRenderers = modelInstance.GetComponentsInChildren<Renderer>();

        modelBaseLocalPos = modelInstance.transform.localPosition;
        SetupWalkClip();
        BuildBossRegalia();

        // Mount point for a phone-streamed OBJ face (see GameBridge.SetBossFace)
        var faceCtrl = gameObject.AddComponent<BossFaceController>();
        faceCtrl.head = FindHeadTransform();
    }

    /// <summary>
    /// Turns the rigged figure into an unmistakable boss: horns, glowing eyes,
    /// a crown of spikes, an ember aura, orbiting shards, and a menacing light.
    /// Head-mounted pieces animate with the walk.
    /// </summary>
    void BuildBossRegalia()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        Material metal = new Material(lit) { name = "BossRegalia" };
        metal.SetColor("_BaseColor", new Color(0.05f, 0.05f, 0.06f));
        if (metal.HasProperty("_Metallic")) metal.SetFloat("_Metallic", 0.9f);
        if (metal.HasProperty("_Smoothness")) metal.SetFloat("_Smoothness", 0.6f);
        if (metal.HasProperty("_EmissionColor"))
        {
            metal.EnableKeyword("_EMISSION");
            metal.SetColor("_EmissionColor", new Color(0.5f, 0.02f, 0f));
        }

        Material eyeMat = new Material(lit) { name = "BossEyes" };
        eyeMat.SetColor("_BaseColor", Color.red);
        if (eyeMat.HasProperty("_EmissionColor"))
        {
            eyeMat.EnableKeyword("_EMISSION");
            eyeMat.SetColor("_EmissionColor", new Color(3f, 0.1f, 0f));
        }

        Transform head = FindHeadTransform();
        if (head != null)
        {
            CreateHorn(head, metal, -1f);
            CreateHorn(head, metal, 1f);
            CreateEye(head, eyeMat, -0.055f);
            CreateEye(head, eyeMat, 0.055f);

            int spikes = 6;
            for (int i = 0; i < spikes; i++)
            {
                float a = (360f / spikes) * i;
                GameObject spike = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyImmediate(spike.GetComponent<Collider>());
                spike.transform.SetParent(head, false);
                spike.transform.localPosition = new Vector3(
                    Mathf.Cos(a * Mathf.Deg2Rad) * 0.09f, 0.12f, Mathf.Sin(a * Mathf.Deg2Rad) * 0.09f);
                spike.transform.localRotation = Quaternion.Euler(Random.Range(-15f, 15f), a, 0f);
                spike.transform.localScale = new Vector3(0.02f, 0.11f, 0.02f);
                spike.GetComponent<Renderer>().sharedMaterial = metal;
            }
        }

        // Menacing aura light
        GameObject lgo = new GameObject("BossAuraLight");
        lgo.transform.SetParent(transform, false);
        lgo.transform.localPosition = new Vector3(0f, 1.2f * modelScale, 0f);
        Light l = lgo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.2f, 0.05f);
        l.range = 6f;
        l.intensity = 3.5f;
        l.shadows = LightShadows.None;

        CreateAuraEmbers();

        // Orbiting shards
        auraRing = new GameObject("AuraRing").transform;
        auraRing.SetParent(transform, false);
        auraRing.localPosition = new Vector3(0f, 1f * modelScale, 0f);
        for (int i = 0; i < 5; i++)
        {
            float a = (360f / 5) * i;
            GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(shard.GetComponent<Collider>());
            shard.transform.SetParent(auraRing, false);
            shard.transform.localPosition = new Vector3(
                Mathf.Cos(a * Mathf.Deg2Rad) * 0.9f, Random.Range(-0.2f, 0.2f), Mathf.Sin(a * Mathf.Deg2Rad) * 0.9f);
            shard.transform.localRotation = Random.rotation;
            shard.transform.localScale = new Vector3(0.09f, 0.22f, 0.05f);
            shard.GetComponent<Renderer>().sharedMaterial = metal;
        }
    }

    Transform FindHeadTransform()
    {
        if (modelInstance == null) return null;
        foreach (var t in modelInstance.GetComponentsInChildren<Transform>())
        {
            string n = t.name.ToLower();
            if (n.Contains("head") && !n.Contains("top")) return t;
        }
        return null;
    }

    void CreateHorn(Transform head, Material mat, float side)
    {
        GameObject horn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        DestroyImmediate(horn.GetComponent<Collider>());
        horn.transform.SetParent(head, false);
        horn.transform.localPosition = new Vector3(side * 0.08f, 0.08f, 0f);
        horn.transform.localRotation = Quaternion.Euler(0f, 0f, side * 40f);
        horn.transform.localScale = new Vector3(0.03f, 0.13f, 0.03f);
        horn.GetComponent<Renderer>().sharedMaterial = mat;
    }

    void CreateEye(Transform head, Material mat, float x)
    {
        GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        DestroyImmediate(eye.GetComponent<Collider>());
        eye.transform.SetParent(head, false);
        eye.transform.localPosition = new Vector3(x, 0.02f, 0.1f);
        eye.transform.localScale = Vector3.one * 0.035f;
        eye.GetComponent<Renderer>().sharedMaterial = mat;
    }

    void CreateAuraEmbers()
    {
        GameObject go = new GameObject("BossEmbers");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.5f, 0f);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 1.5f;
        main.startSpeed = 0.5f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
        main.maxParticles = 60;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.05f;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.4f, 0.05f), new Color(1f, 0.1f, 0f));

        var em = ps.emission;
        em.rateOverTime = 22f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone; // rising ember column
        sh.angle = 5f;
        sh.radius = 0.4f;

        var rend = go.GetComponent<ParticleSystemRenderer>();
        Shader psSh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Particles/Standard Unlit")
            ?? Shader.Find("Universal Render Pipeline/Unlit");
        if (psSh != null)
        {
            Material m = new Material(psSh);
            m.SetColor("_BaseColor", new Color(1f, 0.4f, 0f));
            if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(2f, 0.6f, 0f)); }
            rend.material = m;
        }
        ps.Play();
    }

    /// <summary>
    /// If the GLB ships a legacy walk clip, loop it. Otherwise we drive a
    /// procedural stride in AnimateWalk().
    /// </summary>
    void SetupWalkClip()
    {
        if (modelInstance == null) return;

        Animator anim = modelInstance.GetComponent<Animator>();
        Animation legacy = modelInstance.GetComponent<Animation>();

        if (legacy != null && legacy.GetClipCount() > 0 && legacy.clip != null)
        {
            if (anim != null) anim.enabled = false;
            legacy.clip.wrapMode = WrapMode.Loop;
            legacy.wrapMode = WrapMode.Loop;
            legacy.Play();
            legacy[legacy.clip.name].speed = walkClipSpeed; // heavy, menacing pace
            legacyWalkAnim = legacy;
        }
    }

    /// <summary>
    /// Dark, textured, demonic material for the boss figure (glowing red).
    /// </summary>
    static void ApplyBossFigureMaterials(GameObject go)
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) return;

        Texture2D bodyTex = Resources.Load<Texture2D>("Models/ZombieTextures/image_0");
        Texture2D normalTex = Resources.Load<Texture2D>("Models/ZombieTextures/image_1");

        Material mat = new Material(urpLit) { name = "BossFigure" };
        if (bodyTex != null) { mat.SetTexture("_BaseMap", bodyTex); mat.SetTexture("_MainTex", bodyTex); }
        if (normalTex != null) { mat.SetTexture("_BumpMap", normalTex); mat.EnableKeyword("_NORMALMAP"); }
        mat.SetColor("_BaseColor", new Color(0.5f, 0.16f, 0.16f, 1f)); // dark blood tint
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.4f, 0.02f, 0.02f)); // demonic red glow
        }
        mat.enableInstancing = true;

        foreach (var rend in go.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = rend.sharedMaterials;
            Material[] newMats = new Material[mats.Length];
            for (int i = 0; i < mats.Length; i++) newMats[i] = mat;
            rend.sharedMaterials = newMats;
        }
    }

    void BuildFallbackModel()
    {
        modelInstance = new GameObject("BossModel_Fallback");
        modelInstance.transform.SetParent(transform, false);

        // Dark body
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.transform.SetParent(modelInstance.transform, false);
        body.transform.localPosition = new Vector3(0f, 1f, 0f);
        body.transform.localScale = new Vector3(0.6f, 1f, 0.4f);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", new Color(0.05f, 0.05f, 0.08f));
        mat.enableInstancing = true;
        body.GetComponent<Renderer>().material = mat;
        DestroyImmediate(body.GetComponent<Collider>());

        // Head
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.transform.SetParent(modelInstance.transform, false);
        head.transform.localPosition = new Vector3(0f, 2.1f, 0f);
        head.transform.localScale = Vector3.one * 0.35f;
        head.GetComponent<Renderer>().material = mat;
        DestroyImmediate(head.GetComponent<Collider>());

        CapsuleCollider hitCol = gameObject.AddComponent<CapsuleCollider>();
        hitCol.center = new Vector3(0f, 1f, 0f);
        hitCol.height = 2.2f;
        hitCol.radius = 0.4f;

        bossRenderers = modelInstance.GetComponentsInChildren<Renderer>();
    }

    void SetupDialogue()
    {
        dialogue = gameObject.AddComponent<BossDialogue>();
    }

    public void ActivateBoss()
    {
        isActive = true;

        // Start the looping mechanical breathing
        if (breathSource != null && breathSource.clip != null)
            breathSource.Play();

        // Play entrance sound
        AudioClip entranceClip = ProceduralAudioGenerator.GenerateDeepRumble();
        if (entranceClip != null)
            audioSource.PlayOneShot(entranceClip, 0.8f);

        // Start dialogue after a moment
        StartCoroutine(StartDialogueSequence());
    }

    IEnumerator StartDialogueSequence()
    {
        yield return new WaitForSeconds(2f);

        if (dialogue != null && GameManager.Instance != null)
        {
            int kills = GameManager.Instance.totalKills;
            int score = GameManager.Instance.score;

            // Boss speaks based on player stats
            string line1 = kills > 5
                ? $"Impressive... {kills} of my soldiers fallen. You think that makes you strong?"
                : $"Only {kills} kills? You're hardly worth my time, warrior.";

            SpeakLine(line1);

            yield return new WaitForSeconds(4f);

            string line2 = score > 100
                ? "Your score of " + score + " means nothing against the dark side."
                : "Pathetic score. The dark side will consume you.";

            SpeakLine(line2);

            yield return new WaitForSeconds(4f);

            SpeakLine("This game was built with Unity and Qualcomm AI. But no technology can save you now.");

            yield return new WaitForSeconds(4f);

            SpeakLine("The Qualcomm Snapdragon NPU renders my power in real-time. Face your doom!");

            yield return new WaitForSeconds(3f);
            dialogue.HideDialogue();
        }
    }

    /// <summary>
    /// Speak a line: play the robotic voice stinger AND show it as caption/dialogue.
    /// </summary>
    void SpeakLine(string line)
    {
        if (voiceSource != null)
        {
            voiceSource.pitch = Random.Range(0.95f, 1.05f);
            voiceSource.PlayOneShot(ProceduralAudioGenerator.GenerateVaderVoice(), 0.9f);
        }
        if (dialogue != null) dialogue.ShowLine(line);
    }

    void Update()
    {
        if (isDead || !isActive) return;

        // Spin the orbiting shard aura
        if (auraRing != null) auraRing.Rotate(Vector3.up, 45f * Time.deltaTime);

        if (playerTarget == null)
        {
            if (Camera.main != null) playerTarget = Camera.main.transform;
            return;
        }

        // Lock to floor
        Vector3 pos = transform.position;
        pos.y = floorY;
        transform.position = pos;

        // Face player
        Vector3 dirToPlayer = playerTarget.position - transform.position;
        dirToPlayer.y = 0f;
        if (dirToPlayer.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dirToPlayer);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 2f);
        }

        float distToPlayer = dirToPlayer.magnitude;

        // Walk toward player slowly
        if (distToPlayer > attackRange)
        {
            transform.position += dirToPlayer.normalized * moveSpeed * Time.deltaTime;
            AnimateWalk(true);
        }
        else
        {
            AnimateWalk(false);

            // Attack
            if (Time.time - lastAttackTime > attackCooldown)
            {
                lastAttackTime = Time.time;
                Attack();
            }
        }
    }

    /// <summary>
    /// Procedural walk: heavy stride bob + side sway + body roll so Vader
    /// reads as walking, not sliding. Skipped if a real walk clip is playing.
    /// </summary>
    void AnimateWalk(bool moving)
    {
        if (modelInstance == null) return;

        // A real clip already animates the body — just gate its playback.
        if (legacyWalkAnim != null)
        {
            if (legacyWalkAnim.clip != null)
                legacyWalkAnim[legacyWalkAnim.clip.name].speed = moving ? walkClipSpeed : 0f;
            return;
        }

        if (moving)
        {
            walkPhase += Time.deltaTime * walkCadence;
            float bob = Mathf.Abs(Mathf.Sin(walkPhase)) * bobHeight;   // rises on each footfall
            float sway = Mathf.Sin(walkPhase) * swayAmount;            // shifts weight side to side
            float roll = Mathf.Sin(walkPhase) * leanAngle;

            modelInstance.transform.localPosition = modelBaseLocalPos + new Vector3(sway, bob, 0f);
            modelInstance.transform.localRotation = Quaternion.Euler(3f, 0f, roll); // slight forward lean + roll
        }
        else
        {
            // Settle back to rest when attacking / idle
            modelInstance.transform.localPosition = Vector3.Lerp(
                modelInstance.transform.localPosition, modelBaseLocalPos, Time.deltaTime * 5f);
            modelInstance.transform.localRotation = Quaternion.Slerp(
                modelInstance.transform.localRotation, Quaternion.identity, Time.deltaTime * 5f);
        }
    }

    void Attack()
    {
        // Damage player
        if (GameManager.Instance != null && GameManager.Instance.playerHealth != null)
        {
            GameManager.Instance.playerHealth.TakeDamage(attackDamage);
        }

        // Flash red
        StartCoroutine(AttackFlash());

        // Attack sound
        AudioClip attackClip = ProceduralAudioGenerator.GenerateAlienDeath();
        if (attackClip != null)
            audioSource.PlayOneShot(attackClip, 0.6f);
    }

    IEnumerator AttackFlash()
    {
        if (bossRenderers == null) yield break;

        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        foreach (var r in bossRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_EmissionColor", Color.red * 3f);
            r.SetPropertyBlock(mpb);
        }

        yield return new WaitForSeconds(0.2f);

        mpb = new MaterialPropertyBlock();
        foreach (var r in bossRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_EmissionColor", new Color(0.4f, 0.02f, 0.02f)); // restore demonic glow
            r.SetPropertyBlock(mpb);
        }
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;
        currentHealth -= damage;

        // Hit flash
        StartCoroutine(HitFlash());

        // Hit sound
        AudioClip hitClip = ProceduralAudioGenerator.GenerateHitImpact();
        if (hitClip != null)
            audioSource.PlayOneShot(hitClip, 0.5f);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    IEnumerator HitFlash()
    {
        if (bossRenderers == null) yield break;
        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        foreach (var r in bossRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_EmissionColor", Color.white * 5f);
            r.SetPropertyBlock(mpb);
        }
        yield return new WaitForSeconds(0.1f);
        mpb = new MaterialPropertyBlock();
        foreach (var r in bossRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_EmissionColor", new Color(0.4f, 0.02f, 0.02f)); // restore demonic glow
            r.SetPropertyBlock(mpb);
        }
    }

    void Die()
    {
        isDead = true;

        // Score
        if (GameManager.Instance != null)
            GameManager.Instance.OnAlienKilled(scoreValue);

        // Death VFX
        var deathVfx = gameObject.AddComponent<AlienDeathVFX>();
        deathVfx.PlayDeathEffect(scoreValue);

        // Big explosion sound
        AudioClip deathClip = ProceduralAudioGenerator.GenerateAlienDeath();
        AudioSource.PlayClipAtPoint(deathClip, transform.position, 1f);

        // Notify
        onBossDefeated?.Invoke();

        // Destroy after brief delay
        Destroy(gameObject, 0.5f);
    }

    public float GetHealthPercent()
    {
        return (float)currentHealth / maxHealth;
    }

    /// <summary>
    /// Converts glTFast materials to URP/Lit preserving textures
    /// </summary>
    static void ConvertMaterialsToURP(GameObject go)
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) return;

        // Build texture lookup from GLB sub-assets
        var texLookup = new System.Collections.Generic.Dictionary<string, Texture>();
        var normLookup = new System.Collections.Generic.Dictionary<string, Texture>();

        #if UNITY_EDITOR
        string[] glbPaths = { "Assets/Resources/Models/darth_vader.glb" };
        foreach (var path in glbPaths)
        {
            var allAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
            if (allAssets == null) continue;
            foreach (var asset in allAssets)
            {
                if (asset is Material m)
                {
                    Texture baseTex = null, normTex = null;
                    if (m.HasTexture("baseColorTexture")) baseTex = m.GetTexture("baseColorTexture");
                    if (m.HasTexture("normalTexture")) normTex = m.GetTexture("normalTexture");
                    string key = m.name.Replace(" (Instance)", "");
                    if (baseTex != null && !texLookup.ContainsKey(key)) texLookup[key] = baseTex;
                    if (normTex != null && !normLookup.ContainsKey(key)) normLookup[key] = normTex;
                }
            }
        }
        #endif

        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        foreach (var rend in renderers)
        {
            Material[] mats = rend.sharedMaterials;
            Material[] newMats = new Material[mats.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                Material src = mats[i];
                if (src == null) { newMats[i] = null; continue; }

                if (src.shader.name.Contains("glTF"))
                {
                    Material dst = new Material(urpLit);
                    dst.name = src.name + "_URP";
                    Color bc = Color.white;
                    if (src.HasColor("baseColorFactor")) bc = src.GetColor("baseColorFactor");
                    dst.SetColor("_BaseColor", bc);
                    Texture bt = null;
                    if (src.HasTexture("baseColorTexture")) bt = src.GetTexture("baseColorTexture");
                    if (bt != null) { dst.SetTexture("_BaseMap", bt); dst.SetTexture("_MainTex", bt); }
                    Texture nt = null;
                    if (src.HasTexture("normalTexture")) nt = src.GetTexture("normalTexture");
                    if (nt != null) { dst.SetTexture("_BumpMap", nt); dst.EnableKeyword("_NORMALMAP"); }
                    dst.enableInstancing = true;
                    newMats[i] = dst;
                }
                else if (src.shader == urpLit && src.GetTexture("_BaseMap") == null)
                {
                    string matName = src.name.Replace(" (Instance)", "");
                    Texture baseTex = null;
                    texLookup.TryGetValue(matName, out baseTex);
                    if (baseTex != null)
                    {
                        Material dst = new Material(src);
                        dst.SetTexture("_BaseMap", baseTex);
                        dst.SetTexture("_MainTex", baseTex);
                        Texture normTex = null;
                        normLookup.TryGetValue(matName, out normTex);
                        if (normTex != null) { dst.SetTexture("_BumpMap", normTex); dst.EnableKeyword("_NORMALMAP"); }
                        dst.enableInstancing = true;
                        newMats[i] = dst;
                    }
                    else newMats[i] = src;
                }
                else newMats[i] = src;
            }
            rend.materials = newMats;
        }
    }
}
