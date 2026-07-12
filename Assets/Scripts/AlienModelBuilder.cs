using UnityEngine;

/// <summary>
/// Loads the Zombie Running FBX model and sets up animation.
/// Replaces the old procedural insectoid builder.
/// </summary>
public class AlienModelBuilder : MonoBehaviour
{
    [Header("Model")]
    public string zombieAssetPath = "Assets/Resources/Models/AlienMonster.glb";
    public float modelScale = 0.45f;

    [Header("Size Normalization")]
    [Tooltip("When on, the model is measured and scaled so it stands targetHeight meters tall.")]
    public bool normalizeToHeight = true;
    public float targetHeight = 1.7f;

    [Header("Optional Animator Controller (Resources path)")]
    public string animatorControllerResourcePath = "Models/AlienMonsterController";

    [Header("Boss")]
    public bool isBossVariant = false;

    // Runtime references
    [HideInInspector] public Transform headTransform;
    [HideInInspector] public Transform[] legs; // Not used for FBX, kept for API compat
    [HideInInspector] public Transform leftMandible;
    [HideInInspector] public Transform rightMandible;
    [HideInInspector] public Animator animator;

    private GameObject modelInstance;

    void Start()
    {
        if (modelInstance == null)
        {
            Build();
        }
    }

    public void Build()
    {
        // Clean existing model
        if (modelInstance != null)
        {
            DestroyImmediate(modelInstance);
        }

        // Also clean any old procedural children
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name != "IdleAudio")
            {
                DestroyImmediate(child.gameObject);
            }
        }

        // Load zombie GLB: Resources.Load first (build-safe), AssetDatabase fallback (editor)
        string resourcePath = zombieAssetPath.Replace("Assets/Resources/", "").Replace(".glb", "").Replace(".fbx", "").Replace(".gltf", "");
        GameObject prefab = Resources.Load<GameObject>(resourcePath);
        #if UNITY_EDITOR
        if (prefab == null)
        {
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(zombieAssetPath);
        }
        #endif
        if (prefab == null)
        {
            Debug.LogWarning($"AlienModelBuilder: Could not load '{zombieAssetPath}', building fallback");
            BuildFallbackModel();
            return;
        }

        // Instantiate UNPARENTED first: the root may be scaled to zero by the
        // spawn animation (AlienAI), which would corrupt bounds measurement.
        modelInstance = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        modelInstance.name = "ZombieModel";

        Bounds modelBounds;
        Vector3 modelLocalPos = Vector3.zero;
        if (normalizeToHeight)
        {
            // Measure and scale so the alien stands targetHeight meters tall
            float height = isBossVariant ? targetHeight * 1.2f : targetHeight;
            modelBounds = ModelUtil.NormalizeHeight(modelInstance, height);

            // Root the model so its feet rest at local Y=0
            float footOffset = modelInstance.transform.position.y - modelBounds.min.y;
            modelLocalPos = new Vector3(0f, footOffset, 0f);
            modelBounds = ModelUtil.GetRendererBounds(modelInstance);
            // Express bounds relative to feet-at-zero
            modelBounds.center += modelLocalPos;
        }
        else
        {
            float scale = isBossVariant ? modelScale * 1.2f : modelScale;
            modelInstance.transform.localScale = Vector3.one * scale;
            modelBounds = ModelUtil.GetRendererBounds(modelInstance);
        }

        // Now attach to the (possibly zero-scaled) root, keeping the computed local scale
        modelInstance.transform.SetParent(transform, false);
        modelInstance.transform.localPosition = modelLocalPos;
        modelInstance.transform.localRotation = Quaternion.identity;

        // Remove colliders from the mesh
        Collider[] colliders = modelInstance.GetComponentsInChildren<Collider>();
        foreach (var col in colliders) DestroyImmediate(col);

        // Two-zone hit detection: body capsule + head sphere
        BuildHitZones(modelBounds);

        // Legacy zombie textures only apply to the zombie model; the alien GLB keeps its own
        if (zombieAssetPath.ToLower().Contains("zombie"))
            ApplyZombieTextures(modelInstance);

        // Setup animation
        SetupAnimation();

        // Apply tinting for boss variant
        if (isBossVariant)
        {
            ApplyBossTint();
        }

        // Nullify unused references
        legs = new Transform[0];
        leftMandible = null;
        rightMandible = null;
    }

    /// <summary>
    /// Builds the two-zone hit detection: a large capsule over the body (bodyshot)
    /// and a smaller sphere at the head (headshot, kill-tier damage).
    /// Both are put on the 'Alien' layer and tagged with HitZone components.
    /// </summary>
    void BuildHitZones(Bounds modelBounds)
    {
        // Bounds are measured relative to this root, so express sizes in local units.
        // Guard against a zero/negative measurement (e.g. during spawn scale-in).
        float h = Mathf.Max(modelBounds.size.y, 0.5f);
        float w = Mathf.Max(Mathf.Max(modelBounds.size.x, modelBounds.size.z) * 0.5f, 0.15f);

        // Body capsule: covers feet to shoulders
        CapsuleCollider bodyCol = gameObject.GetComponent<CapsuleCollider>();
        if (bodyCol == null) bodyCol = gameObject.AddComponent<CapsuleCollider>();
        bodyCol.center = new Vector3(0f, h * 0.42f, 0f);
        bodyCol.height = h * 0.84f;
        bodyCol.radius = Mathf.Min(w * 0.7f, h * 0.25f);
        HitZone.Attach(gameObject, gameObject, false);

        // Head sphere: top ~15% of the model
        Transform existing = transform.Find("Head_Hitbox");
        GameObject headHitbox = existing != null ? existing.gameObject : new GameObject("Head_Hitbox");
        headHitbox.transform.SetParent(transform, false);
        headHitbox.transform.localPosition = new Vector3(0f, h * 0.88f, 0f);
        SphereCollider headCol = headHitbox.GetComponent<SphereCollider>();
        if (headCol == null) headCol = headHitbox.AddComponent<SphereCollider>();
        headCol.radius = Mathf.Clamp(h * 0.11f, 0.08f, 0.25f);
        HitZone.Attach(headHitbox, gameObject, true);

        // Remember an approximate head transform for other systems
        headTransform = headHitbox.transform;

        // Whole alien lives on the 'Alien' layer so the pistol raycast can filter to it
        int alienLayer = LayerMask.NameToLayer("Alien");
        if (alienLayer >= 0)
            ModelUtil.SetLayerRecursive(gameObject, alienLayer);
    }

    void SetupAnimation()
    {
        if (modelInstance == null) return;

        animator = modelInstance.GetComponent<Animator>();

        // Preferred: a Mecanim controller shipped in Resources (plays the GLB's own clips)
        if (animator != null && !string.IsNullOrEmpty(animatorControllerResourcePath))
        {
            RuntimeAnimatorController ctrl =
                Resources.Load<RuntimeAnimatorController>(animatorControllerResourcePath);
            if (ctrl != null)
            {
                animator.runtimeAnimatorController = ctrl;
                animator.enabled = true;
                return;
            }
        }

        // Otherwise disable the Animator (we use Legacy Animation below)
        if (animator != null) animator.enabled = false;

        // GLB models come with Animation component and clips already set up
        Animation legacyAnim = modelInstance.GetComponent<Animation>();

        if (legacyAnim != null && legacyAnim.GetClipCount() > 0)
        {
            // GLB clip is named 'mixamo.com' — rename for clarity
            AnimationClip existingClip = legacyAnim.clip;
            if (existingClip != null)
            {
                existingClip.wrapMode = WrapMode.Loop;
                legacyAnim.wrapMode = WrapMode.Loop;
                legacyAnim.AddClip(existingClip, "Run");
                legacyAnim.Play("Run");
                return;
            }
        }

        // Fallback: load clip from sub-assets
        string resourcePath = zombieAssetPath.Replace("Assets/Resources/", "").Replace(".glb", "").Replace(".fbx", "");
        Object[] allAssets = Resources.LoadAll(resourcePath);
        AnimationClip runClip = null;
        foreach (var asset in allAssets)
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__"))
            {
                runClip = clip;
                break;
            }
        }

        if (runClip != null)
        {
            runClip.wrapMode = WrapMode.Loop;
            runClip.legacy = true;

            if (legacyAnim == null)
            {
                legacyAnim = modelInstance.AddComponent<Animation>();
            }

            legacyAnim.AddClip(runClip, "Run");
            legacyAnim.clip = runClip;
            legacyAnim.wrapMode = WrapMode.Loop;
            legacyAnim.Play("Run");
        }
    }

    /// <summary>
    /// Set the animation speed (0.5 = walking, 1.0 = running, 1.5+ = sprinting)
    /// </summary>
    public void SetAnimationSpeed(float speed)
    {
        if (modelInstance == null) return;

        // Mecanim path
        if (animator != null && animator.enabled && animator.runtimeAnimatorController != null)
        {
            animator.speed = speed;
            return;
        }

        Animation legacyAnim = modelInstance.GetComponent<Animation>();
        if (legacyAnim != null && legacyAnim["Run"] != null)
        {
            legacyAnim["Run"].speed = speed;
        }
    }

    void ApplyBossTint()
    {
        if (modelInstance == null) return;

        // Use MaterialPropertyBlock to tint WITHOUT replacing materials
        // This preserves the glTFast textures
        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>();
        foreach (var rend in renderers)
        {
            rend.GetPropertyBlock(mpb);
            // Tint the existing material red
            mpb.SetColor("_Color", new Color(1f, 0.4f, 0.4f, 1f));
            mpb.SetColor("_BaseColor", new Color(1f, 0.4f, 0.4f, 1f));
            mpb.SetColor("baseColorFactor", new Color(1f, 0.3f, 0.3f, 1f));
            rend.SetPropertyBlock(mpb);
        }
    }

    void BuildFallbackModel()
    {
        // Simple capsule fallback if FBX loading fails
        modelInstance = new GameObject("FallbackModel");
        modelInstance.transform.SetParent(transform, false);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(modelInstance.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        body.transform.localScale = new Vector3(0.4f, 0.6f, 0.4f);
        DestroyImmediate(body.GetComponent<Collider>());

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader != null)
        {
            Material mat = new Material(shader);
            mat.SetColor("_BaseColor", new Color(0.3f, 0.5f, 0.2f));
            body.GetComponent<Renderer>().material = mat;
        }

        // Head
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(modelInstance.transform, false);
        head.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        head.transform.localScale = Vector3.one * 0.35f;
        headTransform = head.transform;
        DestroyImmediate(head.GetComponent<Collider>());

        // Collider
        CapsuleCollider hitCol = gameObject.GetComponent<CapsuleCollider>();
        if (hitCol == null) hitCol = gameObject.AddComponent<CapsuleCollider>();
        hitCol.center = new Vector3(0f, 0.7f, 0f);
        hitCol.height = 1.6f;
        hitCol.radius = 0.3f;

        legs = new Transform[0];
    }

    /// <summary>
    /// Loads extracted zombie textures from Resources and creates URP/Lit materials.
    /// Textures are pre-extracted from the GLB to Assets/Resources/Models/ZombieTextures/
    /// so they survive Instantiate() and work on Quest 3 builds.
    /// </summary>
    static void ApplyZombieTextures(GameObject go)
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) return;

        // Load extracted textures
        Texture2D bodyTex = Resources.Load<Texture2D>("Models/ZombieTextures/image_0");
        Texture2D normalTex = Resources.Load<Texture2D>("Models/ZombieTextures/image_1");
        Texture2D clothTex = Resources.Load<Texture2D>("Models/ZombieTextures/image_2");

        // Create URP materials with textures
        Material bodyMat = new Material(urpLit);
        bodyMat.name = "ZombieBody_URP";
        if (bodyTex != null) { bodyMat.SetTexture("_BaseMap", bodyTex); bodyMat.SetTexture("_MainTex", bodyTex); }
        if (normalTex != null) { bodyMat.SetTexture("_BumpMap", normalTex); bodyMat.EnableKeyword("_NORMALMAP"); }
        bodyMat.enableInstancing = true;

        Material clothMat = new Material(urpLit);
        clothMat.name = "ZombieCloth_URP";
        if (clothTex != null) { clothMat.SetTexture("_BaseMap", clothTex); clothMat.SetTexture("_MainTex", clothTex); }
        if (normalTex != null) { clothMat.SetTexture("_BumpMap", normalTex); clothMat.EnableKeyword("_NORMALMAP"); }
        clothMat.enableInstancing = true;

        // Apply to all renderers
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        foreach (var rend in renderers)
        {
            Material[] mats = rend.sharedMaterials;
            Material[] newMats = new Material[mats.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) { newMats[i] = bodyMat; continue; }
                string matName = mats[i].name.ToLower();
                // Default to the body/skin texture; only use the cloth map for clearly cloth materials.
                if (matName.Contains("cloth") || matName.Contains("shirt") ||
                    matName.Contains("pant") || matName.Contains("jean") || matName.Contains("jacket"))
                    newMats[i] = clothMat;
                else
                    newMats[i] = bodyMat;
            }
            rend.materials = newMats;
        }
    }
}
