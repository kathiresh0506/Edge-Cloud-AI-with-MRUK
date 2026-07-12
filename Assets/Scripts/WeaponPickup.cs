using UnityEngine;
using System.Collections;

/// <summary>
/// Weapon pickup that spawns on the ground with an FBX gun model.
/// Walk near it to equip. Different weapon types with unique stats.
/// </summary>
public class WeaponPickup : MonoBehaviour
{
    public enum WeaponType { Shotgun, Sniper, AK47 }

    [Header("Weapon Config")]
    public WeaponType weaponType = WeaponType.Shotgun;
    public int ammo = 15;

    [HideInInspector] public int damage;
    [HideInInspector] public float fireRate;
    [HideInInspector] public float laserWidth;
    [HideInInspector] public Color laserColor;
    [HideInInspector] public AudioClip shootSound;
    [HideInInspector] public int pellets = 1;
    [HideInInspector] public float spreadAngle = 0f;
    [HideInInspector] public string gunModelPath;

    private bool isPickedUp = false;
    private AudioSource audioSource;
    private AudioClip pickupSound;
    private GameObject modelInstance;

    void Start()
    {
        ConfigureWeapon();
        BuildVisual();
        SetupTrigger();

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f;
        audioSource.playOnAwake = false;

        pickupSound = ProceduralAudioGenerator.GenerateWeaponPickup();
    }

    void ConfigureWeapon()
    {
        switch (weaponType)
        {
            case WeaponType.Shotgun:
                damage = 2;
                fireRate = 0.6f;
                laserWidth = 0.04f;
                laserColor = new Color(1f, 0.5f, 0.1f, 1f);
                shootSound = ProceduralAudioGenerator.GenerateShotgunBlast();
                pellets = 5;
                spreadAngle = 8f;
                ammo = 12;
                gunModelPath = "Assets/Resources/Models/Remington_model_M31.glb";
                break;

            case WeaponType.Sniper:
                damage = 8;
                fireRate = 1.2f;
                laserWidth = 0.05f;
                laserColor = new Color(0.2f, 0.4f, 1f, 1f);
                shootSound = ProceduralAudioGenerator.GenerateRailgunShot();
                pellets = 1;
                spreadAngle = 0f;
                ammo = 6;
                gunModelPath = "Assets/Resources/Models/Sniper_glb.glb";
                break;

            case WeaponType.AK47:
                damage = 1;
                fireRate = 0.08f;
                laserWidth = 0.015f;
                laserColor = new Color(0.1f, 1f, 0.2f, 1f);
                shootSound = ProceduralAudioGenerator.GenerateGunshot();
                pellets = 1;
                spreadAngle = 3f;
                ammo = 30;
                gunModelPath = "Assets/Resources/Models/Ak47.fbx"; // real assault-rifle model
                break;
        }
    }

    void BuildVisual()
    {
        // Load GLB model: Resources.Load first (build-safe), AssetDatabase fallback
        string resourcePath = gunModelPath.Replace("Assets/Resources/", "").Replace(".glb", "").Replace(".fbx", "").Replace(".gltf", "");
        GameObject prefab = Resources.Load<GameObject>(resourcePath);
        #if UNITY_EDITOR
        if (prefab == null)
        {
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(gunModelPath);
        }
        #endif
        if (prefab != null)
        {
            modelInstance = Instantiate(prefab);
            modelInstance.name = "PickupModel";
            modelInstance.transform.SetParent(transform, false);
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localScale = Vector3.one * 0.12f; // drastically smaller pickup

            // Remove colliders from model
            Collider[] cols = modelInstance.GetComponentsInChildren<Collider>();
            foreach (var c in cols) DestroyImmediate(c);

            // Convert glTFast materials to URP/Lit for proper textures
            ConvertMaterialsToURP(modelInstance);

            // Texture the AK47 (assault rifle ships untextured)
            GunModelBuilder.ApplyGunTextures(modelInstance, gunModelPath);
        }
        else
        {
            // Fallback primitive
            BuildFallbackVisual();
        }

        // Beacon glow sphere above
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        if (shader != null)
        {
            GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            beacon.name = "Beacon";
            beacon.transform.SetParent(transform, false);
            beacon.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            beacon.transform.localScale = Vector3.one * 0.1f;
            DestroyImmediate(beacon.GetComponent<Collider>());

            Material glowMat = new Material(shader);
            glowMat.SetColor("_BaseColor", laserColor);
            if (glowMat.HasProperty("_EmissionColor"))
            {
                glowMat.EnableKeyword("_EMISSION");
                glowMat.SetColor("_EmissionColor", laserColor * 4f);
            }
            beacon.GetComponent<Renderer>().material = glowMat;
        }
    }

    void BuildFallbackVisual()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material bodyMat = null;
        if (shader != null)
        {
            bodyMat = new Material(shader);
            bodyMat.SetColor("_BaseColor", new Color(0.2f, 0.2f, 0.25f));
        }

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "WeaponBody";
        body.transform.SetParent(transform, false);
        body.transform.localScale = new Vector3(0.06f, 0.05f, 0.15f);
        if (bodyMat != null) body.GetComponent<Renderer>().material = bodyMat;
        DestroyImmediate(body.GetComponent<Collider>());
    }

    void SetupTrigger()
    {
        SphereCollider trigger = gameObject.AddComponent<SphereCollider>();
        trigger.radius = 0.6f;
        trigger.isTrigger = true;

        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    void Update()
    {
        if (isPickedUp) return;

        // Floating rotation
        transform.Rotate(Vector3.up, 45f * Time.deltaTime);

        // Check proximity to controllers
        PlayerShooter[] shooters = Object.FindObjectsByType<PlayerShooter>(FindObjectsSortMode.None);
        foreach (var shooter in shooters)
        {
            float dist = Vector3.Distance(transform.position, shooter.transform.position);
            if (dist < 0.7f)
            {
                EquipTo(shooter);
                return;
            }
        }
    }

    void EquipTo(PlayerShooter shooter)
    {
        if (isPickedUp) return;
        isPickedUp = true;

        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, 0.8f);
        }

        shooter.EquipWeapon(damage, fireRate, laserWidth, laserColor, shootSound,
            ammo, pellets, spreadAngle, GetWeaponName(), gunModelPath);

        Destroy(gameObject);
    }

    string GetWeaponName()
    {
        switch (weaponType)
        {
            case WeaponType.Shotgun: return "SHOTGUN";
            case WeaponType.Sniper: return "SNIPER";
            case WeaponType.AK47: return "AK47";
            default: return "WEAPON";
        }
    }

    public static void SpawnRandom(Vector3 position)
    {
        GameObject pickupGO = new GameObject("WeaponPickup");
        pickupGO.transform.position = position + Vector3.up * 0.4f;

        WeaponPickup pickup = pickupGO.AddComponent<WeaponPickup>();
        int type = Random.Range(0, 3);
        pickup.weaponType = (WeaponType)type;
    }

    static void ConvertMaterialsToURP(GameObject go)
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) return;

        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        foreach (var rend in renderers)
        {
            Material[] mats = rend.sharedMaterials;
            Material[] newMats = new Material[mats.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                Material src = mats[i];
                if (src == null) { newMats[i] = null; continue; }
                if (src.shader == urpLit) { newMats[i] = src; continue; }

                Material dst = new Material(urpLit);
                dst.name = src.name + "_URP";

                Color baseColor = Color.white;
                if (src.HasColor("baseColorFactor")) baseColor = src.GetColor("baseColorFactor");
                else if (src.HasColor("_BaseColor")) baseColor = src.GetColor("_BaseColor");
                dst.SetColor("_BaseColor", baseColor);

                Texture baseTex = null;
                if (src.HasTexture("baseColorTexture")) baseTex = src.GetTexture("baseColorTexture");
                else if (src.HasTexture("_BaseMap")) baseTex = src.GetTexture("_BaseMap");
                if (baseTex != null) { dst.SetTexture("_BaseMap", baseTex); dst.SetTexture("_MainTex", baseTex); }

                Texture normalTex = null;
                if (src.HasTexture("normalTexture")) normalTex = src.GetTexture("normalTexture");
                if (normalTex != null) { dst.SetTexture("_BumpMap", normalTex); dst.EnableKeyword("_NORMALMAP"); }

                if (src.HasFloat("metallicFactor")) dst.SetFloat("_Metallic", src.GetFloat("metallicFactor"));
                if (src.HasFloat("roughnessFactor")) dst.SetFloat("_Smoothness", 1f - src.GetFloat("roughnessFactor"));

                dst.enableInstancing = true;
                newMats[i] = dst;
            }
            rend.materials = newMats;
        }
    }
}
