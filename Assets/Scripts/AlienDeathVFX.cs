using UnityEngine;
using System.Collections;

public class AlienDeathVFX : MonoBehaviour
{
    [Header("Death Animation")]
    public float deathDuration = 0.6f;
    public float scaleDownDuration = 0.4f;

    [Header("Particles")]
    public int particleCount = 25;
    public float particleSpeed = 4f;
    public float particleLifetime = 1.0f;

    [Header("Colors")]
    public Color sparkColor1 = new Color(0.2f, 1f, 0.3f, 1f);
    public Color sparkColor2 = new Color(0.6f, 0.1f, 1f, 1f);

    [Header("Score Popup")]
    public bool showScorePopup = true;

    [Header("Body Part Explosion")]
    public float bodyPartForce = 3f;
    public float bodyPartTorque = 500f;

    public void PlayDeathEffect(int scoreValue)
    {
        StartCoroutine(DeathSequence(scoreValue));
    }

    IEnumerator DeathSequence(int scoreValue)
    {
        // Separate body parts and fling them
        ExplodeBodyParts();

        // Spawn particles (more dramatic)
        SpawnDeathParticles();

        // Energy burst (flash of light)
        SpawnEnergyBurst();

        // Score popup
        if (showScorePopup)
        {
            SpawnScorePopup(scoreValue);
        }

        // Scale down the root (remaining parts)
        Vector3 startScale = transform.localScale;
        float timer = 0f;

        while (timer < scaleDownDuration)
        {
            timer += Time.deltaTime;
            float t = timer / scaleDownDuration;

            transform.Rotate(Vector3.up, 720f * Time.deltaTime);
            float scale = 1f - (t * t);
            transform.localScale = startScale * scale;

            yield return null;
        }

        Destroy(gameObject);
    }

    void ExplodeBodyParts()
    {
        // Detach some child primitives and give them physics
        int detached = 0;
        for (int i = transform.childCount - 1; i >= 0 && detached < 5; i--)
        {
            Transform child = transform.GetChild(i);
            Renderer rend = child.GetComponent<Renderer>();
            if (rend == null) continue;

            // Detach from parent
            child.SetParent(null);

            // Add rigidbody for physics
            Rigidbody rb = child.gameObject.AddComponent<Rigidbody>();
            rb.mass = 0.1f;
            rb.useGravity = true;

            // Fling outward
            Vector3 explosionDir = (child.position - transform.position).normalized;
            if (explosionDir.sqrMagnitude < 0.01f)
                explosionDir = Random.onUnitSphere;
            explosionDir.y = Mathf.Abs(explosionDir.y) + 0.3f; // Bias upward

            rb.AddForce(explosionDir * bodyPartForce, ForceMode.Impulse);
            rb.AddTorque(Random.onUnitSphere * bodyPartTorque);

            // Shrink and destroy after a delay
            StartCoroutine(ShrinkAndDestroy(child.gameObject, 1.5f));

            detached++;
        }
    }

    IEnumerator ShrinkAndDestroy(GameObject go, float delay)
    {
        yield return new WaitForSeconds(delay * 0.7f);

        if (go == null) yield break;

        Vector3 startScale = go.transform.localScale;
        float timer = 0f;
        float shrinkDuration = delay * 0.3f;

        while (timer < shrinkDuration)
        {
            if (go == null) yield break;
            timer += Time.deltaTime;
            float t = timer / shrinkDuration;
            go.transform.localScale = startScale * (1f - t);
            yield return null;
        }

        if (go != null) Destroy(go);
    }

    void SpawnDeathParticles()
    {
        GameObject particleGO = new GameObject("DeathParticles");
        particleGO.transform.position = transform.position;

        ParticleSystem ps = particleGO.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.1f;
        main.startLifetime = particleLifetime;
        main.startSpeed = particleSpeed;
        main.startSize = 0.06f;
        main.maxParticles = particleCount;
        main.loop = false;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.4f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(sparkColor1, 0.0f),
                new GradientColorKey(sparkColor2, 0.5f),
                new GradientColorKey(sparkColor2, 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1.0f, 0.0f),
                new GradientAlphaKey(1.0f, 0.5f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        colorOverLifetime.color = gradient;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, particleCount)
        });

        ParticleSystemRenderer renderer = particleGO.GetComponent<ParticleSystemRenderer>();
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        if (shader != null)
        {
            renderer.material = new Material(shader);
            renderer.material.color = sparkColor1;
            if (renderer.material.HasProperty("_BaseColor"))
                renderer.material.SetColor("_BaseColor", sparkColor1);
        }

        ps.Play();
        Destroy(particleGO, particleLifetime + 0.5f);
    }

    void SpawnEnergyBurst()
    {
        // Quick expanding sphere of light
        GameObject burstGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        burstGO.name = "EnergyBurst";
        burstGO.transform.position = transform.position;
        burstGO.transform.localScale = Vector3.one * 0.1f;
        DestroyImmediate(burstGO.GetComponent<Collider>());

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        if (shader != null)
        {
            Material mat = new Material(shader);
            Color burstColor = new Color(0.3f, 1f, 0.5f, 0.8f);
            mat.SetColor("_BaseColor", burstColor);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", burstColor * 8f);
            }
            burstGO.GetComponent<Renderer>().material = mat;
        }

        StartCoroutine(AnimateEnergyBurst(burstGO));
    }

    IEnumerator AnimateEnergyBurst(GameObject burst)
    {
        float timer = 0f;
        float duration = 0.2f;

        while (timer < duration)
        {
            if (burst == null) yield break;
            timer += Time.deltaTime;
            float t = timer / duration;

            burst.transform.localScale = Vector3.one * Mathf.Lerp(0.1f, 1.5f, t);

            Renderer rend = burst.GetComponent<Renderer>();
            if (rend != null)
            {
                Color c = rend.material.color;
                c.a = 1f - t;
                rend.material.color = c;
            }

            yield return null;
        }

        if (burst != null) Destroy(burst);
    }

    void SpawnScorePopup(int scoreValue)
    {
        GameObject popup = new GameObject("ScorePopup");
        popup.transform.position = transform.position + Vector3.up * 0.5f;

        FloatingText floatingText = popup.AddComponent<FloatingText>();
        floatingText.Initialize("+" + scoreValue, sparkColor1);
    }
}
