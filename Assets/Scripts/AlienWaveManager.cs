using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections;
using System.Collections.Generic;

public class AlienWaveManager : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject alienPrefab;

    [Header("Spawn Settings")]
    public float minSpawnDistance = 4f;
    public float maxSpawnDistance = 8f;
    public float spawnHeight = 0f;
    public float delayBetweenSpawns = 0.8f;

    private int currentWaveNumber = 1;

    [Header("AR References")]
    public ARPlaneManager planeManager;
    public ARBoundingBoxManager boundingBoxManager;

    [Header("Fallback")]
    public bool useFallbackSpawning = true;

    private List<GameObject> activeAliens = new List<GameObject>();
    private Transform playerTransform;

    void Start()
    {
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            playerTransform = mainCam.transform;
        }

        // Try to find AR managers if not assigned
        if (planeManager == null)
            planeManager = FindFirstObjectByType<ARPlaneManager>();
        if (boundingBoxManager == null)
            boundingBoxManager = FindFirstObjectByType<ARBoundingBoxManager>();
    }

    public void SpawnWave(int count, float speedMultiplier, float healthMultiplier, bool isBossWave, int waveNumber = 1)
    {
        currentWaveNumber = waveNumber;
        StartCoroutine(SpawnWaveRoutine(count, speedMultiplier, healthMultiplier, isBossWave));
    }

    /// <summary>
    /// Spawn a single enemy at a world position (used by the phone top-view spawn control
    /// via GameBridge). Opens a rift there first, then spawns the alien.
    /// </summary>
    public void SpawnEnemyAt(Vector3 position, bool isBoss = false)
    {
        float speedMult = 1f + (currentWaveNumber - 1) * 0.1f;
        float healthMult = 1f + (currentWaveNumber - 1) * 0.15f;

        Vector3 normal = playerTransform != null
            ? (playerTransform.position - position).normalized
            : Vector3.forward;
        RoomCrackVFX.SpawnAt(position, normal);

        SpawnAlien(position, speedMult, healthMult, isBoss);
    }

    IEnumerator SpawnWaveRoutine(int count, float speedMultiplier, float healthMultiplier, bool isBossWave)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = GetSpawnPosition();

            // Spawn crack VFX 1 second before alien
            Vector3 crackNormal = playerTransform != null
                ? (playerTransform.position - spawnPos).normalized
                : Vector3.forward;
            RoomCrackVFX.SpawnAt(spawnPos, crackNormal);

            yield return new WaitForSeconds(Mathf.Min(delayBetweenSpawns, 1f));

            SpawnAlien(spawnPos, speedMultiplier, healthMultiplier, isBossWave && i == count - 1);

            if (delayBetweenSpawns > 1f)
                yield return new WaitForSeconds(delayBetweenSpawns - 1f);
        }

        // Spawn a weapon pickup after the wave (classic mode only —
        // the gem hunt is a single-pistol experience)
        if (GameManager.Instance == null || !GameManager.Instance.gemHuntMode)
            SpawnWeaponPickup();
    }

    Vector3 GetSpawnPosition()
    {
        // Try AR-based spawning first
        Vector3? arPosition = TryGetARSpawnPosition();
        if (arPosition.HasValue)
            return arPosition.Value;

        // Fallback: spawn around the player
        return GetFallbackSpawnPosition();
    }

    Vector3? TryGetARSpawnPosition()
    {
        // Try bounding boxes first (furniture surfaces)
        if (boundingBoxManager != null && boundingBoxManager.subsystem != null && boundingBoxManager.subsystem.running)
        {
            List<ARBoundingBox> boxes = new List<ARBoundingBox>();
            foreach (var box in boundingBoxManager.trackables)
            {
                boxes.Add(box);
            }

            if (boxes.Count > 0)
            {
                ARBoundingBox chosen = boxes[Random.Range(0, boxes.Count)];
                Vector3 pos = chosen.transform.position;
                // Spawn above the furniture
                pos.y += 0.4f;
                // Add some random offset so they don't all stack
                pos += new Vector3(
                    Random.Range(-0.5f, 0.5f),
                    0f,
                    Random.Range(-0.5f, 0.5f)
                );
                return pos;
            }
        }

        // Try AR planes (floor/walls)
        if (planeManager != null && planeManager.subsystem != null && planeManager.subsystem.running)
        {
            List<ARPlane> floors = new List<ARPlane>();
            foreach (var plane in planeManager.trackables)
            {
                if (plane.alignment == PlaneAlignment.HorizontalUp)
                {
                    floors.Add(plane);
                }
            }

            if (floors.Count > 0)
            {
                ARPlane chosen = floors[Random.Range(0, floors.Count)];
                Vector3 pos = chosen.transform.position;

                // Random point within the plane bounds
                Vector2 randomPoint = Random.insideUnitCircle * Mathf.Min(chosen.size.x, chosen.size.y) * 0.4f;
                pos += chosen.transform.right * randomPoint.x + chosen.transform.forward * randomPoint.y;
                pos.y += spawnHeight + 0.3f;

                // Ensure minimum distance from player
                if (playerTransform != null)
                {
                    float dist = Vector3.Distance(pos, playerTransform.position);
                    if (dist < minSpawnDistance)
                    {
                        Vector3 awayDir = (pos - playerTransform.position).normalized;
                        if (awayDir.sqrMagnitude < 0.01f)
                            awayDir = Random.onUnitSphere;
                        awayDir.y = 0;
                        pos = playerTransform.position + awayDir.normalized * minSpawnDistance;
                        pos.y = chosen.transform.position.y + spawnHeight + 0.3f;
                    }
                }

                return pos;
            }
        }

        return null;
    }

    Vector3 GetFallbackSpawnPosition()
    {
        if (playerTransform == null)
            return transform.position + Random.onUnitSphere * maxSpawnDistance;

        // Random angle around the player
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float distance = Random.Range(minSpawnDistance, maxSpawnDistance);

        Vector3 offset = new Vector3(
            Mathf.Cos(angle) * distance,
            spawnHeight,
            Mathf.Sin(angle) * distance
        );

        return playerTransform.position + offset;
    }

    void SpawnAlien(Vector3 position, float speedMultiplier, float healthMultiplier, bool isBoss)
    {
        if (alienPrefab == null)
        {
            Debug.LogError("AlienWaveManager: alienPrefab is not assigned!");
            return;
        }

        // Ensure spawn at ground level
        if (playerTransform != null)
        {
            position.y = playerTransform.position.y - 1.0f; // Rough ground level
        }

        GameObject alien = Instantiate(alienPrefab, position, Quaternion.identity);
        activeAliens.Add(alien);

        // Initialize AI with wave modifiers
        AlienAI ai = alien.GetComponent<AlienAI>();
        if (ai != null)
        {
            ai.SetWaveNumber(currentWaveNumber);
            ai.Initialize(speedMultiplier, healthMultiplier, isBoss);
        }

        // Register cleanup on death
        AlienHealth health = alien.GetComponent<AlienHealth>();
        if (health != null)
        {
            health.onDeath.AddListener(() => OnAlienDestroyed(alien));
        }
    }

    void OnAlienDestroyed(GameObject alien)
    {
        activeAliens.Remove(alien);
    }

    public void DestroyAllAliens()
    {
        for (int i = activeAliens.Count - 1; i >= 0; i--)
        {
            if (activeAliens[i] != null)
            {
                Destroy(activeAliens[i]);
            }
        }
        activeAliens.Clear();
    }

    void Update()
    {
        // Clean up null references
        activeAliens.RemoveAll(a => a == null);
    }

    public int GetActiveAlienCount()
    {
        return activeAliens.Count;
    }

    void SpawnWeaponPickup()
    {
        if (playerTransform == null) return;

        // Spawn weapon pickup 2-3m from the player
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float dist = Random.Range(1.5f, 3f);
        Vector3 offset = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
        Vector3 pos = playerTransform.position + offset;
        pos.y = playerTransform.position.y - 0.5f; // Ground level roughly

        WeaponPickup.SpawnRandom(pos);
    }
}
