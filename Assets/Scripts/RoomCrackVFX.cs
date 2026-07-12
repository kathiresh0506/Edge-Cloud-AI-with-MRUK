using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Multiverse crack VFX — a wide, jagged crack tears open facing the player and
/// you can see straight through it into ANOTHER UNIVERSE: a glowing nebula, a
/// drifting alien planet, swirling color, and stars. "First Encounter" style wall
/// rift. Aliens climb out through the rift (the wave manager spawns the alien a
/// moment after the crack opens).
/// </summary>
public class RoomCrackVFX : MonoBehaviour
{
    [Header("Rift")]
    public float openWidth = 1.6f;
    public float openHeight = 2.0f;
    public float lifetime = 6f;
    public int edgeShards = 16;

    // Shared, generated once and reused by every rift (cheap per-spawn)
    private static Texture2D _nebulaTex;

    private Material nebulaMat;
    private Transform nebula;
    private Transform planet;
    private Light riftLight;
    private ParticleSystem swirlPS;
    private AudioSource audioSrc;
    private readonly List<Transform> shards = new List<Transform>();
    private readonly List<Vector3> shardRest = new List<Vector3>();
    private float hueSeed;

    public static void SpawnAt(Vector3 position, Vector3 normal)
    {
        GameObject go = new GameObject("MultiverseRift");
        go.transform.position = position;

        // Face the player (normal points from spawn toward the player)
        if (normal.sqrMagnitude > 0.001f)
            go.transform.rotation = Quaternion.LookRotation(normal, Vector3.up);

        RoomCrackVFX rift = go.AddComponent<RoomCrackVFX>();
        rift.StartCoroutine(rift.RiftSequence());
    }

    IEnumerator RiftSequence()
    {
        hueSeed = Random.value;
        BuildUniverseWindow();
        BuildJaggedFrame();
        CreateRiftLight();
        CreateSwirl();
        CreateSound();

        // Start sealed
        transform.localScale = new Vector3(0f, 0f, 1f);

        // Phase 1: rift tears open
        float openTime = 1.2f;
        float timer = 0f;
        while (timer < openTime)
        {
            timer += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, timer / openTime);
            transform.localScale = new Vector3(t, t, 1f);

            for (int i = 0; i < shards.Count; i++)
            {
                if (shards[i] == null) continue;
                shards[i].localPosition = Vector3.Lerp(Vector3.zero, shardRest[i], t);
            }
            if (riftLight != null) riftLight.intensity = t * 4f;
            yield return null;
        }
        transform.localScale = Vector3.one;

        // Phase 2: the universe churns while aliens come through
        float activeTime = lifetime - openTime - 1.2f;
        timer = 0f;
        while (timer < activeTime)
        {
            timer += Time.deltaTime;
            AnimateUniverse();
            yield return null;
        }

        // Phase 3: rift collapses shut
        float closeTime = 1.2f;
        timer = 0f;
        Vector3 start = transform.localScale;
        while (timer < closeTime)
        {
            timer += Time.deltaTime;
            float t = timer / closeTime;
            transform.localScale = new Vector3(Mathf.Lerp(start.x, 0f, t), Mathf.Lerp(start.y, 0f, t), 1f);
            for (int i = 0; i < shards.Count; i++)
            {
                if (shards[i] == null) continue;
                shards[i].localPosition = Vector3.Lerp(shardRest[i], Vector3.zero, t);
            }
            if (riftLight != null) riftLight.intensity = Mathf.Lerp(4f, 0f, t);
            yield return null;
        }

        Destroy(gameObject);
    }

    // ---- the view into another universe ----

    void BuildUniverseWindow()
    {
        EnsureNebulaTexture();

        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");

        // Nebula backdrop, recessed behind the opening so you look "through" the wall
        GameObject neb = GameObject.CreatePrimitive(PrimitiveType.Quad);
        neb.name = "Nebula";
        Destroy(neb.GetComponent<Collider>());
        neb.transform.SetParent(transform, false);
        neb.transform.localPosition = new Vector3(0f, 0f, -0.7f);
        neb.transform.localScale = new Vector3(openWidth * 2.2f, openHeight * 2.2f, 1f);
        nebulaMat = new Material(unlit);
        // Per-rift color tint so each universe looks a bit different
        Color tint = Color.HSVToRGB(hueSeed, 0.35f, 1f);
        if (_nebulaTex != null) { nebulaMat.SetTexture("_BaseMap", _nebulaTex); nebulaMat.SetTexture("_MainTex", _nebulaTex); }
        nebulaMat.SetColor("_BaseColor", tint);
        nebulaMat.SetColor("_Color", tint);
        neb.GetComponent<Renderer>().material = nebulaMat;
        nebula = neb.transform;

        // A drifting alien planet floating in that universe
        GameObject pl = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pl.name = "AlienPlanet";
        Destroy(pl.GetComponent<Collider>());
        pl.transform.SetParent(transform, false);
        pl.transform.localPosition = new Vector3(openWidth * 0.28f, openHeight * 0.22f, -0.45f);
        pl.transform.localScale = Vector3.one * (openWidth * 0.32f);
        Material plMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        Color planetCol = Color.HSVToRGB(Mathf.Repeat(hueSeed + 0.4f, 1f), 0.7f, 0.9f);
        plMat.SetColor("_BaseColor", planetCol * 0.4f);
        if (plMat.HasProperty("_EmissionColor"))
        {
            plMat.EnableKeyword("_EMISSION");
            plMat.SetColor("_EmissionColor", planetCol * 1.5f);
        }
        pl.GetComponent<Renderer>().material = plMat;
        planet = pl.transform;
    }

    static void EnsureNebulaTexture()
    {
        if (_nebulaTex != null) return;

        int size = 160;
        _nebulaTex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float seed = Random.Range(0f, 100f);
        Color[] px = new Color[size * size];

        Color purple = new Color(0.35f, 0.05f, 0.6f);
        Color magenta = new Color(0.9f, 0.15f, 0.7f);
        Color cyan = new Color(0.1f, 0.6f, 0.95f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size, v = (float)y / size;

                // Multi-octave value noise for cloud density
                float n = 0f, amp = 1f, freq = 3f, norm = 0f;
                for (int o = 0; o < 4; o++)
                {
                    n += amp * Mathf.PerlinNoise(seed + u * freq, seed + v * freq);
                    norm += amp; amp *= 0.5f; freq *= 2f;
                }
                n /= norm;
                float cloud = Mathf.Pow(n, 2.2f);

                float hueN = Mathf.PerlinNoise(seed * 2f + u * 2.3f, seed * 2f + v * 2.3f);
                Color baseCol = hueN < 0.5f
                    ? Color.Lerp(purple, magenta, hueN * 2f)
                    : Color.Lerp(magenta, cyan, (hueN - 0.5f) * 2f);

                Color col = baseCol * cloud * 1.7f;
                col.b += 0.06f; // deep-space ambient
                col.a = 1f;
                px[y * size + x] = col;
            }
        }

        // Sprinkle stars
        int stars = size * size / 90;
        for (int s = 0; s < stars; s++)
        {
            int sx = Random.Range(0, size), sy = Random.Range(0, size);
            float b = Random.Range(0.6f, 1f);
            px[sy * size + sx] = new Color(b, b, b, 1f);
        }

        _nebulaTex.SetPixels(px);
        _nebulaTex.Apply();
    }

    void AnimateUniverse()
    {
        // Slow parallax drift of the nebula texture
        if (nebulaMat != null)
        {
            Vector2 off = new Vector2(Time.time * 0.01f, Time.time * 0.004f);
            if (nebulaMat.HasProperty("_BaseMap")) nebulaMat.SetTextureOffset("_BaseMap", off);
            if (nebulaMat.HasProperty("_MainTex")) nebulaMat.SetTextureOffset("_MainTex", off);
        }
        if (planet != null)
            planet.localRotation = Quaternion.Euler(0f, Time.time * 8f, 0f);

        // Colorful, cycling rift light
        if (riftLight != null)
        {
            float hue = Mathf.Repeat(hueSeed + Time.time * 0.15f, 1f);
            riftLight.color = Color.HSVToRGB(hue, 0.8f, 1f);
            riftLight.intensity = 3.5f + Mathf.Sin(Time.time * 5f) * 1.2f;
        }
    }

    // ---- the torn wall frame ----

    void BuildJaggedFrame()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material shardMat = new Material(lit);
        shardMat.SetColor("_BaseColor", new Color(0.06f, 0.06f, 0.08f, 1f));
        if (shardMat.HasProperty("_EmissionColor"))
        {
            shardMat.EnableKeyword("_EMISSION");
            shardMat.SetColor("_EmissionColor", Color.HSVToRGB(hueSeed, 0.8f, 1f) * 0.6f);
        }
        shardMat.enableInstancing = true;

        // Jagged shards around an elliptical opening — leaves the center open
        // so the universe shows through.
        for (int i = 0; i < edgeShards; i++)
        {
            float ang = (360f / edgeShards) * i + Random.Range(-8f, 8f);
            float rad = 1f + Random.Range(-0.05f, 0.12f);
            Vector3 rest = new Vector3(
                Mathf.Cos(ang * Mathf.Deg2Rad) * openWidth * 0.55f * rad,
                Mathf.Sin(ang * Mathf.Deg2Rad) * openHeight * 0.55f * rad,
                0f);

            GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = $"EdgeShard_{i}";
            Destroy(shard.GetComponent<Collider>());
            shard.transform.SetParent(transform, false);
            shard.transform.localPosition = Vector3.zero; // grows out from center
            shard.transform.localRotation = Quaternion.Euler(
                Random.Range(-25f, 25f), Random.Range(-25f, 25f), ang + Random.Range(-20f, 20f));
            shard.transform.localScale = new Vector3(
                Random.Range(0.12f, 0.28f), Random.Range(0.05f, 0.12f), Random.Range(0.04f, 0.08f));
            shard.GetComponent<Renderer>().material = shardMat;

            shards.Add(shard.transform);
            shardRest.Add(rest);
        }
    }

    void CreateRiftLight()
    {
        GameObject lgo = new GameObject("RiftLight");
        lgo.transform.SetParent(transform, false);
        lgo.transform.localPosition = new Vector3(0f, 0f, 0.4f); // spills toward the room
        riftLight = lgo.AddComponent<Light>();
        riftLight.type = LightType.Point;
        riftLight.color = Color.HSVToRGB(hueSeed, 0.8f, 1f);
        riftLight.intensity = 0f;
        riftLight.range = 5f;
        riftLight.shadows = LightShadows.None;
    }

    void CreateSwirl()
    {
        GameObject go = new GameObject("RiftSwirl");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, -0.2f);

        swirlPS = go.AddComponent<ParticleSystem>();
        var main = swirlPS.main;
        main.duration = lifetime;
        main.startLifetime = 1.6f;
        main.startSpeed = 0.4f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.maxParticles = 120;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startColor = new ParticleSystem.MinMaxGradient(
            Color.HSVToRGB(hueSeed, 0.8f, 1f), Color.HSVToRGB(Mathf.Repeat(hueSeed + 0.3f, 1f), 0.8f, 1f));

        var emission = swirlPS.emission;
        emission.rateOverTime = 30f;

        var shape = swirlPS.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = openWidth * 0.5f;
        shape.arc = 360f;

        var vel = swirlPS.velocityOverLifetime;
        vel.enabled = true;
        vel.orbitalZ = 2.5f; // spin around the rift face

        ParticleSystemRenderer rend = go.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Particles/Standard Unlit")
            ?? Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null)
        {
            Material m = new Material(shader);
            m.SetColor("_BaseColor", Color.HSVToRGB(hueSeed, 0.7f, 1f));
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.HSVToRGB(hueSeed, 0.7f, 1f) * 3f);
            }
            rend.material = m;
        }
        swirlPS.Play();
    }

    void CreateSound()
    {
        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.spatialBlend = 1f;
        audioSrc.maxDistance = 14f;
        audioSrc.rolloffMode = AudioRolloffMode.Linear;
        audioSrc.playOnAwake = false;

        AudioClip clip = ProceduralAudioGenerator.GenerateAlienApproach();
        if (clip != null)
        {
            audioSrc.pitch = 0.7f;
            audioSrc.PlayOneShot(clip, 0.55f);
        }
    }
}
