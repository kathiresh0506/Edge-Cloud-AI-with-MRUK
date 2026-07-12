using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

/// <summary>
/// Places 4 diamond gems "hidden in plain sight" around the player's real room
/// (AR bounding boxes / planes when available, ring fallback otherwise), tracks
/// collection, and notifies the game director: each gem raises spawn pressure
/// and darkens the world.
/// </summary>
public class GemManager : MonoBehaviour
{
    [Header("Gems")]
    public int totalGems = 4;
    [Tooltip("Min distance between gems so they spread around the room.")]
    public float minGemSeparation = 1.4f;

    [Header("Placement fallback (no AR data)")]
    public float minPlaceDistance = 1.6f;
    public float maxPlaceDistance = 3.2f;
    public float minHeight = 0.4f;
    public float maxHeight = 1.3f;

    [Header("AR References (auto-found)")]
    public ARPlaneManager planeManager;
    public ARBoundingBoxManager boundingBoxManager;

    [Header("Events")]
    public UnityEvent<int> onGemCollected;   // gems collected so far
    public UnityEvent onAllGemsCollected;

    // Runtime
    private readonly List<GemPickup> activeGems = new List<GemPickup>();
    private int collectedCount = 0;
    private Transform playerHead;

    public int CollectedCount => collectedCount;
    public int TotalGems => totalGems;
    public bool AllCollected => collectedCount >= totalGems;

    void Start()
    {
        if (planeManager == null) planeManager = FindFirstObjectByType<ARPlaneManager>();
        if (boundingBoxManager == null) boundingBoxManager = FindFirstObjectByType<ARBoundingBoxManager>();
        playerHead = Camera.main != null ? Camera.main.transform : null;
    }

    /// <summary>Spawns all gems around the room. Called by the director at game start.</summary>
    public void PlaceGems()
    {
        ClearGems();
        collectedCount = 0;

        List<Vector3> spots = new List<Vector3>();
        for (int i = 0; i < totalGems; i++)
        {
            Vector3 pos = FindGemSpot(spots, i);
            spots.Add(pos);

            GameObject gemGO = new GameObject($"Gem_{i + 1}");
            gemGO.transform.position = pos;
            GemPickup gem = gemGO.AddComponent<GemPickup>();
            gem.SetManager(this);
            activeGems.Add(gem);
        }

        UpdateHud();
    }

    Vector3 FindGemSpot(List<Vector3> taken, int index)
    {
        // Try several candidates, keep the first that's far enough from other gems
        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector3 candidate = TryGetArSpot(index) ?? GetFallbackSpot(index);
            bool farEnough = true;
            foreach (var t in taken)
            {
                if (Vector3.Distance(candidate, t) < minGemSeparation) { farEnough = false; break; }
            }
            if (farEnough) return candidate;
        }
        return GetFallbackSpot(index);
    }

    Vector3? TryGetArSpot(int index)
    {
        // Furniture surfaces first: gems sitting on real tables/shelves feel magical
        if (boundingBoxManager != null && boundingBoxManager.subsystem != null
            && boundingBoxManager.subsystem.running)
        {
            List<ARBoundingBox> boxes = new List<ARBoundingBox>();
            foreach (var b in boundingBoxManager.trackables) boxes.Add(b);
            if (boxes.Count > 0)
            {
                var box = boxes[Random.Range(0, boxes.Count)];
                Vector3 top = box.transform.position;
                top.y += box.size.y * 0.5f + 0.12f;
                top += new Vector3(Random.Range(-0.25f, 0.25f), 0f, Random.Range(-0.25f, 0.25f));
                return top;
            }
        }

        // Horizontal planes (floor, tables)
        if (planeManager != null && planeManager.subsystem != null && planeManager.subsystem.running)
        {
            List<ARPlane> planes = new List<ARPlane>();
            foreach (var p in planeManager.trackables)
            {
                if (p.alignment == PlaneAlignment.HorizontalUp) planes.Add(p);
            }
            if (planes.Count > 0)
            {
                var plane = planes[Random.Range(0, planes.Count)];
                Vector2 r = Random.insideUnitCircle * Mathf.Min(plane.size.x, plane.size.y) * 0.35f;
                Vector3 pos = plane.transform.position
                    + plane.transform.right * r.x + plane.transform.forward * r.y;
                pos.y += 0.25f; // float a hand-width above the surface
                return pos;
            }
        }

        return null;
    }

    Vector3 GetFallbackSpot(int index)
    {
        Vector3 center = playerHead != null ? playerHead.position : transform.position;

        // Spread gems roughly into four quadrants around the player
        float baseAngle = (360f / totalGems) * index + Random.Range(-25f, 25f);
        float rad = baseAngle * Mathf.Deg2Rad;
        float dist = Random.Range(minPlaceDistance, maxPlaceDistance);

        Vector3 pos = center + new Vector3(Mathf.Cos(rad) * dist, 0f, Mathf.Sin(rad) * dist);
        float floorY = center.y - 1.4f; // approximate floor from head height
        pos.y = floorY + Random.Range(minHeight, maxHeight);
        return pos;
    }

    public void OnGemCollected(GemPickup gem)
    {
        activeGems.Remove(gem);
        collectedCount = Mathf.Min(collectedCount + 1, totalGems);

        UpdateHud();
        onGemCollected?.Invoke(collectedCount);

        if (collectedCount >= totalGems)
            onAllGemsCollected?.Invoke();
    }

    void UpdateHud()
    {
        GameHUD hud = GameManager.Instance != null ? GameManager.Instance.gameHUD : null;
        if (hud == null) hud = FindFirstObjectByType<GameHUD>();
        if (hud != null) hud.UpdateGemCount(collectedCount, totalGems);
    }

    public void ClearGems()
    {
        foreach (var g in activeGems)
        {
            if (g != null) Destroy(g.gameObject);
        }
        activeGems.Clear();
        collectedCount = 0;
    }
}
