using UnityEngine;
using System.Collections;

/// <summary>
/// Creates a portal spawn effect when aliens appear.
/// Expanding ring of particles + vertical energy column.
/// </summary>
public class AlienSpawnVFX : MonoBehaviour
{
    [Header("Portal Settings")]
    public float portalDuration = 1.2f;
    public float portalRadius = 0.5f;
    public Color portalColor = new Color(0.5f, 0f, 1f, 1f);
    public Color energyColor = new Color(0.2f, 1f, 0.6f, 1f);

    /// <summary>
    /// Call this at the spawn position to create the portal effect.
    /// </summary>
    public static void PlayAt(Vector3 position)
    {
        GameObject vfxGO = new GameObject("SpawnPortal");
        vfxGO.transform.position = position;
        AlienSpawnVFX vfx = vfxGO.AddComponent<AlienSpawnVFX>();
        vfx.StartCoroutine(vfx.PortalSequence());
    }

    IEnumerator PortalSequence()
    {
        // Create the portal ring (torus approximated by a flattened cylinder)
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "PortalRing";
        ring.transform.SetParent(transform, false);
        ring.transform.localPosition = Vector3.zero;
        ring.transform.localScale = Vector3.zero;
        DestroyImmediate(ring.GetComponent<Collider>());

        // Ring material
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        if (shader != null)
        {
            Material ringMat = new Material(shader);
            ringMat.SetColor("_BaseColor", portalColor);
            if (ringMat.HasProperty("_EmissionColor"))
            {
                ringMat.EnableKeyword("_EMISSION");
                ringMat.SetColor("_EmissionColor", portalColor * 4f);
            }
            if (ringMat.HasProperty("_Surface"))
            {
                ringMat.SetFloat("_Surface", 1f); // Transparent
                ringMat.SetFloat("_Blend", 0f);
            }
            ring.GetComponent<Renderer>().material = ringMat;
        }

        // Create vertical energy particles
        GameObject particleGO = new GameObject("SpawnParticles");
        particleGO.transform.SetParent(transform, false);
        particleGO.transform.localPosition = Vector3.zero;

        ParticleSystem ps = particleGO.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = portalDuration * 0.8f;
        main.startLifetime = 0.6f;
        main.startSpeed = 2f;
        main.startSize = 0.04f;
        main.maxParticles = 40;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.5f; // Float upward

        var emission = ps.emission;
        emission.rateOverTime = 50f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = portalRadius * 0.3f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(energyColor, 0f),
                new GradientColorKey(portalColor, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        // Renderer
        ParticleSystemRenderer psRenderer = particleGO.GetComponent<ParticleSystemRenderer>();
        if (shader != null)
        {
            Material psMat = new Material(shader);
            psMat.SetColor("_BaseColor", energyColor);
            if (psMat.HasProperty("_EmissionColor"))
            {
                psMat.EnableKeyword("_EMISSION");
                psMat.SetColor("_EmissionColor", energyColor * 3f);
            }
            psRenderer.material = psMat;
        }

        ps.Play();

        // Animate the ring expanding
        float timer = 0f;
        while (timer < portalDuration)
        {
            timer += Time.deltaTime;
            float t = timer / portalDuration;

            // Ring expands then contracts
            float ringScale;
            if (t < 0.4f)
            {
                // Expand with overshoot
                float expandT = t / 0.4f;
                ringScale = portalRadius * (1f + 0.2f * Mathf.Sin(expandT * Mathf.PI)) * expandT;
            }
            else
            {
                // Slowly contract
                float contractT = (t - 0.4f) / 0.6f;
                ringScale = portalRadius * (1f - contractT * contractT);
            }

            ring.transform.localScale = new Vector3(ringScale, 0.005f, ringScale);

            // Rotate for visual effect
            ring.transform.Rotate(Vector3.up, 180f * Time.deltaTime);

            // Pulse emission
            if (ring.GetComponent<Renderer>() != null)
            {
                float pulse = (Mathf.Sin(timer * 15f) + 1f) * 0.5f;
                Material mat = ring.GetComponent<Renderer>().material;
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", portalColor * (2f + pulse * 4f));
                }
            }

            yield return null;
        }

        // Cleanup
        Destroy(gameObject, 0.5f);
    }
}
