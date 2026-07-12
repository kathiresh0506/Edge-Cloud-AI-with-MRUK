using UnityEngine;

/// <summary>
/// A glowing bullet that visibly flies from the muzzle to the hit point with a
/// fading trail, so every shot has a readable path. Purely visual — the damage
/// raycast in PlayerShooter has already resolved by the time this spawns.
/// </summary>
public class BulletTracer : MonoBehaviour
{
    public Vector3 target;
    public float speed = 55f;

    private static Material tracerMat;
    private TrailRenderer trail;

    public static void Spawn(Vector3 from, Vector3 to, Color color, float speed = 55f)
    {
        GameObject go = new GameObject("BulletTracer");
        go.transform.position = from;

        BulletTracer t = go.AddComponent<BulletTracer>();
        t.target = to;
        t.speed = speed;
        t.BuildVisual(color);
    }

    void BuildVisual(Color color)
    {
        if (tracerMat == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Standard");
            tracerMat = new Material(sh);
        }
        Material mat = new Material(tracerMat);
        mat.SetColor("_BaseColor", color);
        mat.color = color;
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 4f);
        }

        // Tiny bright core
        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(core.GetComponent<Collider>());
        core.transform.SetParent(transform, false);
        core.transform.localScale = Vector3.one * 0.02f;
        core.GetComponent<Renderer>().material = mat;

        // Fading trail behind it
        trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.07f;
        trail.startWidth = 0.014f;
        trail.endWidth = 0f;
        trail.material = mat;
        trail.startColor = color;
        trail.endColor = new Color(color.r, color.g, color.b, 0f);
        trail.generateLightingData = false;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
    }

    void Update()
    {
        Vector3 pos = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        transform.position = pos;

        if ((pos - target).sqrMagnitude < 0.0004f)
        {
            // Let the trail fade out before the object disappears
            if (trail != null) trail.autodestruct = true;
            Destroy(gameObject, trail != null ? trail.time + 0.02f : 0f);
            enabled = false;
        }
    }
}
