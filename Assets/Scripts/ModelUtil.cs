using UnityEngine;

/// <summary>
/// Shared helpers for loading GLB models from Resources and normalizing their size.
/// Import scales of downloaded models vary wildly, so we always measure renderer
/// bounds and scale to a desired real-world height.
/// </summary>
public static class ModelUtil
{
    /// <summary>
    /// Loads a model prefab. Accepts "Assets/Resources/..." paths or plain Resources paths.
    /// </summary>
    public static GameObject LoadPrefab(string assetPath)
    {
        string resourcePath = assetPath.Replace("Assets/Resources/", "")
            .Replace(".glb", "").Replace(".fbx", "").Replace(".gltf", "");
        GameObject prefab = Resources.Load<GameObject>(resourcePath);
#if UNITY_EDITOR
        if (prefab == null)
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
#endif
        return prefab;
    }

    /// <summary>
    /// World-space renderer bounds of an instantiated object (zero-size if no renderers).
    /// </summary>
    public static Bounds GetRendererBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(go.transform.position, Vector3.zero);

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        return b;
    }

    /// <summary>
    /// Uniformly scales an instance so its renderer-bounds height equals targetHeight.
    /// Returns the resulting bounds after scaling.
    /// </summary>
    public static Bounds NormalizeHeight(GameObject instance, float targetHeight)
    {
        Bounds b = GetRendererBounds(instance);
        if (b.size.y > 0.0001f)
        {
            float factor = targetHeight / b.size.y;
            instance.transform.localScale *= factor;
        }
        return GetRendererBounds(instance);
    }

    /// <summary>
    /// Uniformly scales an instance so its largest bounds dimension equals targetSize.
    /// Returns the resulting bounds after scaling.
    /// </summary>
    public static Bounds NormalizeLargestDimension(GameObject instance, float targetSize)
    {
        Bounds b = GetRendererBounds(instance);
        float maxDim = Mathf.Max(b.size.x, b.size.y, b.size.z);
        if (maxDim > 0.0001f)
        {
            float factor = targetSize / maxDim;
            instance.transform.localScale *= factor;
        }
        return GetRendererBounds(instance);
    }

    /// <summary>
    /// Removes every collider that shipped inside an imported model.
    /// </summary>
    public static void StripColliders(GameObject go)
    {
        foreach (var col in go.GetComponentsInChildren<Collider>(true))
            Object.Destroy(col);
    }

    /// <summary>
    /// Sets the layer on a GameObject and all of its children.
    /// </summary>
    public static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }

    /// <summary>
    /// Finds a plausible head bone in a rigged model, or null.
    /// </summary>
    public static Transform FindHeadBone(GameObject go)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLower();
            if (n.Contains("head") && !n.Contains("top") && !n.Contains("hitbox"))
                return t;
        }
        return null;
    }
}
