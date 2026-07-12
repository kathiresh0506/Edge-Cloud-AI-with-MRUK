using UnityEngine;
using System.Collections;

/// <summary>
/// A diamond gem that is always visible, glowing and bobbing, with a tall beacon
/// column so it can be spotted from across the room. Walking within collectDistance
/// collects it: pickup effect, sound, counter.
/// </summary>
public class GemPickup : MonoBehaviour
{
    [Header("Model")]
    public string gemModelAssetPath = "Assets/Resources/Models/DiamondGem.glb";
    public float gemSize = 0.32f;

    [Header("Proximity")]
    [Tooltip("Player distance at which the gem is collected by walking into it.")]
    public float collectDistance = 0.7f;

    [Header("Bobbing")]
    public float bobAmplitude = 0.08f;
    public float bobFrequency = 1.6f;
    public float spinSpeed = 55f;

    [Header("Glow")]
    public Color glowColor = new Color(0.3f, 0.85f, 1f);
    public float glowLightRange = 6f;        // big radius so the gem lights up its area
    public float glowLightIntensity = 4f;    // bright beacon, visible across the room

    // Runtime
    private GameObject modelInstance;
    private Light glowLight;
    private Transform playerHead;
    private Vector3 basePosition;
    private float bobPhase;
    private bool revealed = false;
    private bool collected = false;
    private float revealScale = 0f;   // 0..1 fade-in by scale
    private float fullScale = 1f;
    private GemManager manager;

    void Start()
    {
        basePosition = transform.position;
        bobPhase = Random.Range(0f, Mathf.PI * 2f);
        playerHead = Camera.main != null ? Camera.main.transform : null;

        BuildModel();
        // Gems are ALWAYS visible now (they used to stay hidden until the player
        // was within 1m, which made them impossible to find). Show immediately.
        SetVisible(true);
        revealed = true;
        revealScale = 1f;
    }

    public void SetManager(GemManager m) => manager = m;

    void BuildModel()
    {
        GameObject prefab = ModelUtil.LoadPrefab(gemModelAssetPath);
        if (prefab != null)
        {
            modelInstance = Instantiate(prefab);
            modelInstance.name = "GemModel";
            ModelUtil.NormalizeLargestDimension(modelInstance, gemSize);
            modelInstance.transform.SetParent(transform, false);
            modelInstance.transform.localPosition = Vector3.zero;
            ModelUtil.StripColliders(modelInstance);
        }
        else
        {
            // Fallback: emissive octahedron-ish cube so the game still works
            modelInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            modelInstance.name = "GemModel_Fallback";
            Destroy(modelInstance.GetComponent<Collider>());
            modelInstance.transform.SetParent(transform, false);
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            modelInstance.transform.localScale = Vector3.one * gemSize * 0.7f;

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (lit != null)
            {
                Material m = new Material(lit);
                m.SetColor("_BaseColor", glowColor);
                if (m.HasProperty("_EmissionColor"))
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", glowColor * 2f);
                }
                modelInstance.GetComponent<Renderer>().material = m;
            }
        }

        fullScale = modelInstance.transform.localScale.x;

        // Glow light (the gem's own shine)
        GameObject lightGO = new GameObject("GemGlow");
        lightGO.transform.SetParent(transform, false);
        lightGO.transform.localPosition = Vector3.up * 0.1f;
        glowLight = lightGO.AddComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.color = glowColor;
        glowLight.range = glowLightRange;
        glowLight.intensity = glowLightIntensity;
        glowLight.shadows = LightShadows.None;

        // Tall glowing beacon column so the gem is spottable from across the room
        BuildBeacon();
    }

    /// <summary>A thin, bright vertical beam rising from the gem so it can be
    /// located from anywhere in the room (like a quest marker).</summary>
    void BuildBeacon()
    {
        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.name = "GemBeacon";
        Destroy(beam.GetComponent<Collider>());
        beam.transform.SetParent(transform, false);
        beam.transform.localPosition = new Vector3(0f, 1.4f, 0f);
        beam.transform.localScale = new Vector3(0.04f, 1.4f, 0.04f); // ~2.8m tall pillar

        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        if (unlit != null)
        {
            Material m = new Material(unlit);
            m.SetColor("_BaseColor", glowColor);
            m.color = glowColor;
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", glowColor * 3f);
            }
            beam.GetComponent<Renderer>().material = m;
            beam.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    void SetVisible(bool visible)
    {
        if (modelInstance != null) modelInstance.SetActive(visible);
        if (glowLight != null) glowLight.gameObject.SetActive(visible);
    }

    void Update()
    {
        if (collected) return;

        if (playerHead == null)
        {
            if (Camera.main != null) playerHead = Camera.main.transform;
            return;
        }

        float dist = Vector3.Distance(playerHead.position, transform.position);

        // Always-on: continuous bob + spin so the gem is lively and easy to notice
        float bob = Mathf.Sin(Time.time * bobFrequency * Mathf.PI + bobPhase) * bobAmplitude;
        transform.position = basePosition + Vector3.up * (bob + bobAmplitude);
        if (modelInstance != null)
            modelInstance.transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);

        // Pulse the glow
        if (glowLight != null)
            glowLight.intensity = glowLightIntensity * (0.8f + 0.35f * Mathf.Sin(Time.time * 3f + bobPhase));

        // Collection when the player walks into it
        if (dist <= collectDistance)
            Collect();
    }

    void Collect()
    {
        if (collected) return;
        collected = true;

        // Pickup sound + sparkle burst
        AudioSource.PlayClipAtPoint(ProceduralAudioGenerator.GenerateKillSound(),
            transform.position, 0.9f);
        SpawnPickupBurst();
        FloatingText.Spawn("+GEM", transform.position + Vector3.up * 0.15f, glowColor);

        if (manager != null)
            manager.OnGemCollected(this);

        Destroy(gameObject, 0.1f);
    }

    void SpawnPickupBurst()
    {
        GameObject go = new GameObject("GemPickupBurst");
        go.transform.position = transform.position;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 0.7f;
        main.startSpeed = 1.6f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        main.maxParticles = 40;
        main.loop = false;
        main.startColor = new ParticleSystem.MinMaxGradient(glowColor, Color.white);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 30) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.08f;

        var rend = go.GetComponent<ParticleSystemRenderer>();
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Particles/Standard Unlit");
        if (sh != null)
        {
            Material m = new Material(sh);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", glowColor);
            rend.material = m;
        }

        ps.Play();
        Destroy(go, 1.5f);
    }
}
