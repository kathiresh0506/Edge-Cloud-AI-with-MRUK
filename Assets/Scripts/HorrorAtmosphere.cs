using UnityEngine;
using System.Collections;

/// <summary>
/// Creates a terrifying horror atmosphere when the boss appears.
/// Red fog, pulsing lights, falling embers, deep rumble.
/// Call Activate() to transition in, Deactivate() to fade out.
/// </summary>
public class HorrorAtmosphere : MonoBehaviour
{
    public static HorrorAtmosphere Instance { get; private set; }

    [Header("Transition")]
    public float transitionDuration = 3f;

    // Runtime objects
    private GameObject fogQuad;
    private Light[] horrorLights;
    private ParticleSystem embersPS;
    private AudioSource rumbleAudio;
    private Material fogMaterial;
    private GameObject horrorGround;
    private Material groundMaterial;
    private GameObject skydome;
    private Material skyMaterial;
    private Light lightningLight;
    private float nextLightning;
    private static Texture2D _hellSky;
    private bool isActive = false;
    private float intensity = 0f;

    void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// Smoothly transition the room into horror mode
    /// </summary>
    public void Activate()
    {
        if (isActive) return;
        isActive = true;
        CreateEffects();
        StartCoroutine(TransitionIn());
    }

    /// <summary>
    /// Smoothly fade out the horror atmosphere
    /// </summary>
    public void Deactivate()
    {
        if (!isActive) return;
        isActive = false;
        StartCoroutine(TransitionOut());
    }

    void CreateEffects()
    {
        // === RED FOG OVERLAY ===
        // Large sphere around the player that tints everything red
        fogQuad = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fogQuad.name = "HorrorFogSphere";
        fogQuad.transform.SetParent(transform, false);
        fogQuad.transform.localScale = Vector3.one * 20f;
        DestroyImmediate(fogQuad.GetComponent<Collider>());

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        fogMaterial = new Material(shader);
        fogMaterial.SetColor("_BaseColor", new Color(0.4f, 0f, 0f, 0f));
        fogMaterial.SetFloat("_Surface", 1f); // Transparent
        fogMaterial.SetFloat("_Blend", 0f);
        fogMaterial.SetFloat("_SrcBlend", 5f); // SrcAlpha
        fogMaterial.SetFloat("_DstBlend", 10f); // OneMinusSrcAlpha
        fogMaterial.SetFloat("_ZWrite", 0f);
        fogMaterial.SetFloat("_Cull", 1f); // Front face cull = render inside
        fogMaterial.renderQueue = 3000;
        fogMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        fogQuad.GetComponent<Renderer>().material = fogMaterial;
        fogQuad.GetComponent<Renderer>().enabled = false;

        // === HORRIFIC GROUND (terrain change) ===
        // Opaque hellish floor that overlays the real room floor during the boss wave.
        horrorGround = GameObject.CreatePrimitive(PrimitiveType.Plane); // 10x10m at scale 1
        horrorGround.name = "HorrorGround";
        horrorGround.transform.SetParent(transform, false);
        horrorGround.transform.localPosition = new Vector3(0f, -1.6f, 0f); // ~floor below the head
        horrorGround.transform.localScale = new Vector3(4f, 1f, 4f);       // 40x40m
        DestroyImmediate(horrorGround.GetComponent<Collider>());           // don't block gunshots

        Shader groundShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        groundMaterial = new Material(groundShader);
        groundMaterial.SetColor("_BaseColor", new Color(0.04f, 0.01f, 0.01f, 1f)); // charred black-red
        if (groundMaterial.HasProperty("_Smoothness")) groundMaterial.SetFloat("_Smoothness", 0.15f);
        if (groundMaterial.HasProperty("_EmissionColor"))
        {
            groundMaterial.EnableKeyword("_EMISSION");
            groundMaterial.SetColor("_EmissionColor", Color.black);
        }
        horrorGround.GetComponent<Renderer>().material = groundMaterial;
        horrorGround.GetComponent<Renderer>().enabled = false;

        // Glowing lava cracks across the ground
        Shader crackShader = Shader.Find("Universal Render Pipeline/Unlit") ?? groundShader;
        for (int c = 0; c < 14; c++)
        {
            GameObject vein = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vein.name = $"LavaVein_{c}";
            DestroyImmediate(vein.GetComponent<Collider>());
            vein.transform.SetParent(horrorGround.transform, false);
            // Plane local space is 10 units wide; scatter cracks across it
            vein.transform.localPosition = new Vector3(
                Random.Range(-4.5f, 4.5f), 0.02f, Random.Range(-4.5f, 4.5f));
            vein.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            vein.transform.localScale = new Vector3(
                Random.Range(0.05f, 0.12f), 0.02f, Random.Range(1.5f, 4f));
            Material veinMat = new Material(crackShader);
            Color lava = new Color(1f, 0.35f, 0.05f, 1f);
            veinMat.SetColor("_BaseColor", lava);
            if (veinMat.HasProperty("_EmissionColor"))
            {
                veinMat.EnableKeyword("_EMISSION");
                veinMat.SetColor("_EmissionColor", lava * 4f);
            }
            vein.GetComponent<Renderer>().material = veinMat;
        }

        // === ENCLOSING HELLSKY DOME (replaces the room — full VR scenery) ===
        EnsureHellSky();
        skydome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        skydome.name = "HellskyDome";
        skydome.transform.SetParent(transform, false);
        skydome.transform.localScale = Vector3.one * 56f; // ~28m radius around the player
        DestroyImmediate(skydome.GetComponent<Collider>());

        Shader skyShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
        skyMaterial = new Material(skyShader);
        if (_hellSky != null) { skyMaterial.SetTexture("_BaseMap", _hellSky); skyMaterial.SetTexture("_MainTex", _hellSky); }
        skyMaterial.SetColor("_BaseColor", Color.black); // faded in via intensity
        if (skyMaterial.HasProperty("_Cull")) skyMaterial.SetFloat("_Cull", 1f); // render inside faces
        skyMaterial.renderQueue = 1000; // draw as background
        skydome.GetComponent<Renderer>().material = skyMaterial;
        skydome.GetComponent<Renderer>().enabled = false;

        // === DISTANT JAGGED SPIRES (silhouette horizon) ===
        for (int i = 0; i < 12; i++)
        {
            float ang = i * 30f * Mathf.Deg2Rad + Random.Range(-0.1f, 0.1f);
            float dist = Random.Range(9f, 15f);
            float height = Random.Range(3f, 8f);

            GameObject spire = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spire.name = $"Spire_{i}";
            DestroyImmediate(spire.GetComponent<Collider>());
            spire.transform.SetParent(transform, false);
            spire.transform.localPosition = new Vector3(
                Mathf.Cos(ang) * dist, height * 0.5f - 1.6f, Mathf.Sin(ang) * dist);
            spire.transform.localRotation = Quaternion.Euler(
                Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f));
            spire.transform.localScale = new Vector3(Random.Range(0.6f, 1.6f), height, Random.Range(0.6f, 1.6f));

            Material spMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            spMat.SetColor("_BaseColor", new Color(0.03f, 0.01f, 0.02f, 1f));
            if (spMat.HasProperty("_EmissionColor"))
            {
                spMat.EnableKeyword("_EMISSION");
                spMat.SetColor("_EmissionColor", new Color(0.15f, 0.0f, 0.0f)); // faint red rim
            }
            spire.GetComponent<Renderer>().material = spMat;
        }

        // === LIGHTNING ===
        GameObject lgo = new GameObject("Lightning");
        lgo.transform.SetParent(transform, false);
        lgo.transform.localPosition = new Vector3(0f, 8f, 0f);
        lightningLight = lgo.AddComponent<Light>();
        lightningLight.type = LightType.Directional;
        lightningLight.color = new Color(1f, 0.6f, 0.5f);
        lightningLight.intensity = 0f;
        lightningLight.shadows = LightShadows.None;
        nextLightning = Time.time + Random.Range(2f, 5f);

        // === PULSING RED LIGHTS ===
        horrorLights = new Light[4];
        for (int i = 0; i < 4; i++)
        {
            GameObject lightGO = new GameObject($"HorrorLight_{i}");
            lightGO.transform.SetParent(transform, false);
            float angle = i * 90f * Mathf.Deg2Rad;
            lightGO.transform.localPosition = new Vector3(
                Mathf.Cos(angle) * 3f,
                2f,
                Mathf.Sin(angle) * 3f
            );
            Light light = lightGO.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.1f, 0f);
            light.intensity = 0f;
            light.range = 8f;
            light.shadows = LightShadows.None;
            horrorLights[i] = light;
        }

        // === FALLING EMBERS ===
        GameObject embersGO = new GameObject("HorrorEmbers");
        embersGO.transform.SetParent(transform, false);
        embersGO.transform.localPosition = new Vector3(0f, 3f, 0f);
        embersPS = embersGO.AddComponent<ParticleSystem>();

        var main = embersPS.main;
        main.startLifetime = 4f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.04f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.3f, 0f, 0.9f),
            new Color(1f, 0.1f, 0f, 0.7f)
        );
        main.maxParticles = 200;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.1f;

        var emission = embersPS.emission;
        emission.rateOverTime = 0f; // Start off

        var shape = embersPS.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(8f, 0.1f, 8f);

        // Embers glow
        var renderer = embersGO.GetComponent<ParticleSystemRenderer>();
        Material emberMat = new Material(Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        if (emberMat != null)
        {
            emberMat.SetColor("_BaseColor", new Color(1f, 0.4f, 0f, 1f));
            renderer.material = emberMat;
        }

        embersPS.Stop();

        // === DEEP RUMBLE AUDIO ===
        GameObject audioGO = new GameObject("HorrorRumble");
        audioGO.transform.SetParent(transform, false);
        rumbleAudio = audioGO.AddComponent<AudioSource>();
        rumbleAudio.clip = ProceduralAudioGenerator.GenerateDeepRumble();
        rumbleAudio.loop = true;
        rumbleAudio.volume = 0f;
        rumbleAudio.spatialBlend = 0f;
        rumbleAudio.playOnAwake = false;
    }

    IEnumerator TransitionIn()
    {
        fogQuad.GetComponent<Renderer>().enabled = true;
        if (horrorGround != null) horrorGround.GetComponent<Renderer>().enabled = true;
        if (skydome != null) skydome.GetComponent<Renderer>().enabled = true;
        embersPS.Play();
        rumbleAudio.Play();

        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            intensity = Mathf.SmoothStep(0f, 1f, elapsed / transitionDuration);
            ApplyIntensity();
            yield return null;
        }

        intensity = 1f;
        ApplyIntensity();
    }

    IEnumerator TransitionOut()
    {
        float elapsed = 0f;
        float startIntensity = intensity;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            intensity = Mathf.Lerp(startIntensity, 0f, elapsed / transitionDuration);
            ApplyIntensity();
            yield return null;
        }

        intensity = 0f;
        ApplyIntensity();

        // Cleanup
        if (embersPS != null) embersPS.Stop();
        if (rumbleAudio != null) rumbleAudio.Stop();
        if (fogQuad != null) fogQuad.GetComponent<Renderer>().enabled = false;

        // Destroy effects
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
    }

    void ApplyIntensity()
    {
        // Fog opacity (denser than before for a heavier atmosphere)
        if (fogMaterial != null)
        {
            Color c = fogMaterial.GetColor("_BaseColor");
            c.a = intensity * 0.28f;
            fogMaterial.SetColor("_BaseColor", c);
        }

        // Hellsky dome fades in from black → full scenery
        if (skyMaterial != null)
        {
            skyMaterial.SetColor("_BaseColor", Color.white * intensity);
        }

        // Lights pulse
        if (horrorLights != null)
        {
            for (int i = 0; i < horrorLights.Length; i++)
            {
                if (horrorLights[i] == null) continue;
                float pulse = 1f + 0.3f * Mathf.Sin(Time.time * 2f + i * 1.57f);
                horrorLights[i].intensity = intensity * 3f * pulse;
            }
        }

        // Embers rate
        if (embersPS != null)
        {
            var emission = embersPS.emission;
            emission.rateOverTime = intensity * 40f;
        }

        // Rumble volume
        if (rumbleAudio != null)
        {
            rumbleAudio.volume = intensity * 0.4f;
        }

        // Hellish ground glow pulses with the horror
        if (groundMaterial != null && groundMaterial.HasProperty("_EmissionColor"))
        {
            float glow = intensity * (0.4f + 0.25f * Mathf.Sin(Time.time * 2.5f));
            groundMaterial.SetColor("_EmissionColor", new Color(0.5f, 0.06f, 0f) * glow);
        }
    }

    void Update()
    {
        if (isActive && intensity > 0f)
        {
            ApplyIntensity(); // Keep pulsing
            UpdateLightning();
        }

        // Follow camera (player stays inside the dome)
        if (Camera.main != null)
        {
            transform.position = Camera.main.transform.position;
        }
    }

    void UpdateLightning()
    {
        if (lightningLight == null) return;
        if (Time.time >= nextLightning && intensity > 0.4f)
        {
            StartCoroutine(LightningFlash());
            nextLightning = Time.time + Random.Range(3f, 8f);
        }
    }

    IEnumerator LightningFlash()
    {
        lightningLight.intensity = 2.5f;
        yield return new WaitForSeconds(0.06f);
        lightningLight.intensity = 0.3f;
        yield return new WaitForSeconds(0.05f);
        lightningLight.intensity = 2f;
        yield return new WaitForSeconds(0.06f);
        lightningLight.intensity = 0f;
    }

    static void EnsureHellSky()
    {
        if (_hellSky != null) return;

        int size = 256;
        _hellSky = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
        float seed = Random.Range(0f, 100f);
        Color[] px = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            float v = (float)y / size;
            // Brightest glow around the horizon, dark toward the top
            float horizon = Mathf.Clamp01(1f - Mathf.Abs(v - 0.35f) * 2.2f);

            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size;

                float n = 0f, amp = 1f, freq = 4f, norm = 0f;
                for (int o = 0; o < 4; o++)
                {
                    n += amp * Mathf.PerlinNoise(seed + u * freq, seed + v * freq * 2f);
                    norm += amp; amp *= 0.5f; freq *= 2f;
                }
                n /= norm;
                float clouds = Mathf.Pow(n, 1.8f);

                Color col = new Color(0.12f, 0.02f, 0.01f) * (0.3f + clouds * 0.9f);
                col += new Color(0.6f, 0.12f, 0.03f) * horizon * (0.4f + clouds * 0.8f);
                col *= Mathf.Lerp(1f, 0.22f, Mathf.Clamp01((v - 0.4f) / 0.6f)); // darker overhead
                col.a = 1f;
                px[y * size + x] = col;
            }
        }

        // Embers near the horizon
        int dots = size * size / 200;
        for (int s = 0; s < dots; s++)
        {
            int dx = Random.Range(0, size), dy = Random.Range(0, size / 2);
            px[dy * size + dx] = new Color(1f, 0.5f, 0.1f, 1f);
        }

        _hellSky.SetPixels(px);
        _hellSky.Apply();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
