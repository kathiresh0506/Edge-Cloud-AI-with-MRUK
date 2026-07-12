using UnityEngine;

/// <summary>
/// Spawns clusters of wooden crates around the player as defensive cover when the
/// game starts. Auto-spawns at runtime; no scene wiring needed.
/// </summary>
public class CoverManager : MonoBehaviour
{
    public int clusters = 6;
    public float minRadius = 2.5f;
    public float maxRadius = 4.0f;

    private bool spawned = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSpawn()
    {
        GameObject go = new GameObject("CoverManager");
        go.AddComponent<CoverManager>();
    }

    void Update()
    {
        if (spawned) return;
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.currentState != GameManager.GameState.Playing) return;

        SpawnCover();
        spawned = true;
    }

    void SpawnCover()
    {
        Camera cam = Camera.main;
        if (cam == null) { return; } // try again next frame

        Vector3 center = cam.transform.position;
        float floorY = center.y - 1.5f;

        for (int i = 0; i < clusters; i++)
        {
            float ang = (360f / clusters) * i + Random.Range(-15f, 15f);
            float dist = Random.Range(minRadius, maxRadius);
            Vector3 basePos = center + Quaternion.Euler(0f, ang, 0f) * Vector3.forward * dist;
            basePos.y = floorY;
            BuildCluster(basePos);
        }

        spawned = true;
    }

    void BuildCluster(Vector3 basePos)
    {
        int count = Random.Range(1, 4); // 1-3 stacked crates
        for (int c = 0; c < count; c++)
        {
            float size = Random.Range(0.4f, 0.6f);
            Vector3 pos = basePos + new Vector3(
                Random.Range(-0.3f, 0.3f),
                size * 0.5f + c * size,
                Random.Range(-0.3f, 0.3f));
            Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            WoodenCrate.Create(pos, size, rot);
        }
    }
}
