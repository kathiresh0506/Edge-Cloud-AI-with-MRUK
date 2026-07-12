using UnityEngine;

/// <summary>
/// Loads GLB gun models and positions them as actual guns held by the VR controller.
/// The sniper alignment is the reference — all guns should feel the same way.
/// </summary>
public class GunModelBuilder : MonoBehaviour
{
    [Header("Default Model")]
    public string defaultModelAssetPath = "Assets/Resources/Models/thompson.glb";

    [Header("Aim")]
    [Tooltip("Extra downward pitch (degrees) applied to the whole gun+muzzle so the barrel lines up with how you naturally point the controller. Increase if shots land high, decrease if they land low.")]
    public float aimPitchOffset = 20f;

    // Runtime references
    [HideInInspector] public Transform muzzleTip;

    private GameObject currentGunInstance;
    private Transform gunRoot;

    struct GunConfig
    {
        public float scale;
        public Vector3 posOffset;
        public Vector3 rotOffset;
        public float muzzleZ;
    }

    void Start()
    {
        if (currentGunInstance == null) Build();
    }

    public void Build() { LoadGunFromAssetPath(defaultModelAssetPath); }

    GunConfig GetConfigForModel(string assetPath)
    {
        string lower = assetPath.ToLower();

        if (lower.Contains("pistolgun") || lower.Contains("gravity"))
        {
            // Gem-hunt pistol (gravity gun GLB). Negative scale = auto-normalize
            // by renderer bounds to |scale| meters — import scales vary wildly.
            // Model's barrel points along local +X, so yaw -90 aims the business
            // end forward (+Z / controller-forward) instead of 90 to the right.
            return new GunConfig {
                scale = -0.42f,
                posOffset = new Vector3(0f, -0.02f, 0.08f),  // grip at controller
                rotOffset = new Vector3(0f, -90f, 0f),        // barrel forward (was 90 sideways)
                muzzleZ = 0.35f
            };
        }
        else if (lower.Contains("thompson"))
        {
            // Thompson: user confirms this size is good — leave as-is
            return new GunConfig {
                scale = 0.25f,
                posOffset = new Vector3(0f, -0.02f, 0.08f),  // grip at controller
                rotOffset = new Vector3(0f, -90f, 0f),        // barrel forward (reverse-mount fix)
                muzzleZ = 0.55f
            };
        }
        else if (lower.Contains("remington"))
        {
            // Remington: BIG shotgun like First Encounter
            // Raw Z=7.72m, at 0.08 scale → 0.62m long gun
            return new GunConfig {
                scale = 0.08f,
                posOffset = new Vector3(0f, -0.02f, 0.08f),  // grip at controller
                rotOffset = new Vector3(0f, 180f, 0f),        // barrel forward (flipped 180 from prior reversed mount)
                muzzleZ = 0.55f
            };
        }
        else if (lower.Contains("sniper"))
        {
            // Sniper: user says this works perfectly — DON'T CHANGE
            return new GunConfig {
                scale = 0.45f,
                posOffset = new Vector3(0f, 0.1f, 0.4f),
                rotOffset = new Vector3(0f, 0f, 0f),
                muzzleZ = 0.25f
            };
        }
        else if (lower.Contains("ak47") || lower.Contains("ak_"))
        {
            // Assault rifle (AK47) — starting in-hand transform (verify & tweak in headset)
            return new GunConfig {
                scale = 0.3f,
                posOffset = new Vector3(0f, -0.02f, 0.06f),
                rotOffset = new Vector3(0f, 0f, 0f),
                muzzleZ = 0.45f
            };
        }
        else
        {
            return new GunConfig {
                scale = 0.15f,
                posOffset = new Vector3(0f, 0f, 0.1f),
                rotOffset = new Vector3(0f, 180f, 0f),        // barrel forward (flipped 180 from prior reversed mount)
                muzzleZ = 0.2f
            };
        }
    }

    public void LoadGunFromAssetPath(string assetPath)
    {
        // Clean everything
        if (currentGunInstance != null) DestroyImmediate(currentGunInstance);
        foreach (Transform child in transform)
        {
            if (child.name == "GunModel" || child.name == "GunRoot" || child.name == "MuzzleTip")
                DestroyImmediate(child.gameObject);
        }

        // Load model
        string resourcePath = assetPath.Replace("Assets/Resources/", "")
            .Replace(".glb", "").Replace(".fbx", "").Replace(".gltf", "");
        GameObject prefab = Resources.Load<GameObject>(resourcePath);
        #if UNITY_EDITOR
        if (prefab == null) prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        #endif

        if (prefab == null) { BuildFallbackGun(); return; }

        GunConfig cfg = GetConfigForModel(assetPath);

        // GunRoot pivot: the gun model AND muzzle both live under this, so one
        // pitch offset aims everything together and recoil can kick the whole gun.
        GameObject rootGO = new GameObject("GunRoot");
        rootGO.transform.SetParent(transform, false);
        rootGO.transform.localPosition = Vector3.zero;
        rootGO.transform.localRotation = Quaternion.Euler(aimPitchOffset, 0f, 0f);
        gunRoot = rootGO.transform;

        // CACHE prefab materials BEFORE instantiation (they have glTFast textures)
        var prefabRenderers = prefab.GetComponentsInChildren<Renderer>(true);
        var cachedMaterials = new System.Collections.Generic.Dictionary<string, Material[]>();
        foreach (var pr in prefabRenderers)
            cachedMaterials[pr.name] = pr.sharedMaterials;

        currentGunInstance = Instantiate(prefab);
        currentGunInstance.name = "GunModel";
        currentGunInstance.transform.SetParent(gunRoot, false);
        if (cfg.scale > 0f)
        {
            currentGunInstance.transform.localScale = Vector3.one * cfg.scale;
        }
        else
        {
            // Auto-normalize: measure renderer bounds, scale longest side to |cfg.scale| m
            currentGunInstance.transform.localScale = Vector3.one;
            ModelUtil.NormalizeLargestDimension(currentGunInstance, -cfg.scale);
        }
        currentGunInstance.transform.localRotation = Quaternion.Euler(cfg.rotOffset);
        currentGunInstance.transform.localPosition = cfg.posOffset;

        // Remove colliders
        foreach (var col in currentGunInstance.GetComponentsInChildren<Collider>())
            DestroyImmediate(col);

        // RE-APPLY cached prefab materials (these have the original textures)
        var instanceRenderers = currentGunInstance.GetComponentsInChildren<Renderer>(true);
        foreach (var rend in instanceRenderers)
        {
            if (cachedMaterials.ContainsKey(rend.name))
                rend.sharedMaterials = cachedMaterials[rend.name];
        }

        // Apply explicit URP textures (survives builds; guarantees the gun isn't untextured)
        ApplyGunTextures(currentGunInstance, assetPath);

        // Muzzle tip — under GunRoot so it inherits the same aim pitch as the barrel,
        // meaning the raycast, laser sight and tracers all agree with the visual gun.
        GameObject tipGO = new GameObject("MuzzleTip");
        tipGO.transform.SetParent(gunRoot, false);
        tipGO.transform.localPosition = new Vector3(0f, 0f, cfg.muzzleZ);
        muzzleTip = tipGO.transform;
    }

    public void SwapModel(string resourcesPath)
    {
        string assetPath = "Assets/Resources/" + resourcesPath;
        if (!assetPath.EndsWith(".glb") && !assetPath.EndsWith(".fbx"))
            assetPath += ".glb";
        LoadGunFromAssetPath(assetPath);
    }

    public void RevertToDefault() { LoadGunFromAssetPath(defaultModelAssetPath); }
    public void OnShoot() { }

    /// <summary>
    /// Textures the assault rifle (AK47), which ships with no textures, using generic
    /// PBR metal/wood maps so it isn't left flat/magenta. Public so weapon pickups can reuse it.
    /// </summary>
    public static void ApplyGunTextures(GameObject go, string assetPath)
    {
        if (go == null) return;
        string lower = assetPath.ToLower();
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) return;

        if (lower.Contains("ak47") || lower.Contains("ak_"))
        {
            Texture2D metalAlbedo = Resources.Load<Texture2D>("Models/ThompsonTextures/Metal_Albedo");
            Texture2D metalNormal = Resources.Load<Texture2D>("Models/ThompsonTextures/Metal_Normal");
            Texture2D woodAlbedo  = Resources.Load<Texture2D>("Models/ThompsonTextures/Wood_Albedo");
            Texture2D woodNormal  = Resources.Load<Texture2D>("Models/ThompsonTextures/Wood_Normal");

            Material metalMat = new Material(urpLit) { name = "AK_Metal" };
            if (metalAlbedo != null) { metalMat.SetTexture("_BaseMap", metalAlbedo); metalMat.SetTexture("_MainTex", metalAlbedo); }
            else metalMat.SetColor("_BaseColor", new Color(0.12f, 0.12f, 0.14f));
            if (metalNormal != null) { metalMat.SetTexture("_BumpMap", metalNormal); metalMat.EnableKeyword("_NORMALMAP"); }
            if (metalMat.HasProperty("_Metallic"))   metalMat.SetFloat("_Metallic", 0.85f);
            if (metalMat.HasProperty("_Smoothness")) metalMat.SetFloat("_Smoothness", 0.5f);
            metalMat.enableInstancing = true;

            Material woodMat = new Material(urpLit) { name = "AK_Wood" };
            if (woodAlbedo != null) { woodMat.SetTexture("_BaseMap", woodAlbedo); woodMat.SetTexture("_MainTex", woodAlbedo); }
            else woodMat.SetColor("_BaseColor", new Color(0.35f, 0.2f, 0.08f));
            if (woodNormal != null) { woodMat.SetTexture("_BumpMap", woodNormal); woodMat.EnableKeyword("_NORMALMAP"); }
            if (woodMat.HasProperty("_Smoothness")) woodMat.SetFloat("_Smoothness", 0.3f);
            woodMat.enableInstancing = true;

            foreach (var rend in go.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = rend.sharedMaterials;
                Material[] newMats = new Material[mats.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    string n = mats[i] != null ? mats[i].name.ToLower() : "";
                    bool isWood = n.Contains("wood") || n.Contains("stock") || n.Contains("grip")
                        || n.Contains("hand") || n.Contains("furniture");
                    newMats[i] = isWood ? woodMat : metalMat;
                }
                rend.sharedMaterials = newMats;
            }
        }
    }

    void BuildFallbackGun()
    {
        GameObject rootGO = new GameObject("GunRoot");
        rootGO.transform.SetParent(transform, false);
        rootGO.transform.localRotation = Quaternion.Euler(aimPitchOffset, 0f, 0f);
        gunRoot = rootGO.transform;

        currentGunInstance = new GameObject("GunModel");
        currentGunInstance.transform.SetParent(gunRoot, false);
        currentGunInstance.transform.localPosition = new Vector3(0f, -0.02f, 0.05f);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material mat = shader != null ? new Material(shader) : null;
        if (mat != null) { mat.SetColor("_BaseColor", new Color(0.15f, 0.15f, 0.2f)); mat.enableInstancing = true; }

        var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        barrel.transform.SetParent(currentGunInstance.transform, false);
        barrel.transform.localPosition = new Vector3(0f, 0f, 0.12f);
        barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        barrel.transform.localScale = new Vector3(0.018f, 0.1f, 0.018f);
        if (mat != null) barrel.GetComponent<Renderer>().material = mat;
        DestroyImmediate(barrel.GetComponent<Collider>());

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.transform.SetParent(currentGunInstance.transform, false);
        body.transform.localPosition = new Vector3(0f, 0f, 0.02f);
        body.transform.localScale = new Vector3(0.035f, 0.04f, 0.1f);
        if (mat != null) body.GetComponent<Renderer>().material = mat;
        DestroyImmediate(body.GetComponent<Collider>());

        var tipGO = new GameObject("MuzzleTip");
        tipGO.transform.SetParent(gunRoot, false);
        tipGO.transform.localPosition = new Vector3(0f, 0f, 0.22f);
        muzzleTip = tipGO.transform;
    }
}
