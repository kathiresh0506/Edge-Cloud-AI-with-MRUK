using UnityEngine;
using System.Collections;

/// <summary>
/// Pulsing glow animation for the alien model parts.
/// Attach to the alien root and it will animate all emissive materials.
/// </summary>
public class AlienGlowPulse : MonoBehaviour
{
    [Header("Pulse Settings")]
    public float pulseSpeed = 2f;
    public float pulseMinIntensity = 0.5f;
    public float pulseMaxIntensity = 2f;

    [Header("Eye Pulse")]
    public float eyePulseSpeed = 3f;

    private Renderer[] allRenderers;
    private MaterialPropertyBlock propertyBlock;

    void Start()
    {
        allRenderers = GetComponentsInChildren<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
    }

    void Update()
    {
        float pulse = Mathf.Lerp(pulseMinIntensity, pulseMaxIntensity,
            (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f);

        float eyePulse = Mathf.Lerp(pulseMinIntensity, pulseMaxIntensity * 1.5f,
            (Mathf.Sin(Time.time * eyePulseSpeed) + 1f) * 0.5f);

        foreach (var rend in allRenderers)
        {
            if (rend == null) continue;

            foreach (var mat in rend.materials)
            {
                if (!mat.HasProperty("_EmissionColor")) continue;

                Color baseEmission = mat.GetColor("_BaseColor");

                if (rend.gameObject.name.Contains("Eye"))
                {
                    mat.SetColor("_EmissionColor", baseEmission * eyePulse);
                }
                else if (rend.gameObject.name.Contains("Tip"))
                {
                    mat.SetColor("_EmissionColor", baseEmission * pulse * 1.5f);
                }
            }
        }
    }
}
