using UnityEngine;

/// <summary>
/// Marks a collider as a damage zone (head or body) for location-based hit detection.
/// PlayerShooter reads this off the collider the raycast struck to decide
/// headshot vs bodyshot damage.
/// </summary>
public class HitZone : MonoBehaviour
{
    public bool isHead = false;

    /// <summary>Root object that owns the health component (AlienHealth / BossHealth).</summary>
    public GameObject owner;

    /// <summary>
    /// Convenience helper: adds a HitZone to a collider object and points it at its owner.
    /// </summary>
    public static HitZone Attach(GameObject colliderObject, GameObject healthOwner, bool head)
    {
        HitZone zone = colliderObject.GetComponent<HitZone>();
        if (zone == null) zone = colliderObject.AddComponent<HitZone>();
        zone.isHead = head;
        zone.owner = healthOwner;
        return zone;
    }
}
