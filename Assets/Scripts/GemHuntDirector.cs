using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Drives the gem-hunt game mode:
///  1. Aliens spawn continuously in waves of 3 ("Wave X" banner each wave).
///  2. Four diamond gems are hidden around the room. Each collected gem
///     increases the spawn rate.
///  3. After the 4th gem: spawning stops and the boss spawns at the nearest real wall.
///  4. Defeating the boss shows a victory screen with time taken + shots fired.
/// </summary>
public class GemHuntDirector : MonoBehaviour
{
    public static GemHuntDirector Instance { get; private set; }

    [Header("References (auto-found if empty)")]
    public AlienWaveManager waveManager;
    public GemManager gemManager;

    [Header("Wave Pacing")]
    public int aliensPerWave = 3;
    [Tooltip("Waves never count past this — the run ends at the boss, not at a wave number.")]
    public int maxWaves = 8;
    [Tooltip("Seconds between waves before any gems are collected.")]
    public float baseWaveInterval = 22f;
    [Tooltip("Seconds removed from the interval per gem collected.")]
    public float intervalReductionPerGem = 4.5f;
    public float minWaveInterval = 8f;

    [Header("Boss")]
    public float bossSpawnDelay = 3f;
    [Tooltip("The dragon boss ALWAYS appears once this wave is reached, even if not all gems were collected — so the fight is guaranteed.")]
    public int bossAfterWave = 4;
    [Tooltip("Distance in front of the player the boss spawns at, so it's always visible.")]
    public float bossSpawnDistance = 3.5f;

    // Runtime
    private bool running = false;
    private bool bossPhase = false;
    private int waveNumber = 0;
    private float runStartTime;
    private Coroutine spawnLoop;
    private GemBossController boss;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (waveManager == null) waveManager = FindFirstObjectByType<AlienWaveManager>();
        if (gemManager == null) gemManager = FindFirstObjectByType<GemManager>();

        if (gemManager != null)
        {
            gemManager.onGemCollected.AddListener(OnGemCollected);
            gemManager.onAllGemsCollected.AddListener(OnAllGemsCollected);
        }
    }

    /// <summary>Begins a fresh run. Called from GameManager.StartGame in gem-hunt mode.</summary>
    public void StartRun()
    {
        if (running) return;

        ResetRun();

        running = true;
        runStartTime = Time.time;
        PlayerShooter.TotalShotsFired = 0;

        if (gemManager != null) gemManager.PlaceGems();

        

        spawnLoop = StartCoroutine(SpawnLoop());
    }

    /// <summary>Stops everything and cleans up gems/boss (restart or game over).</summary>
    public void ResetRun()
    {
        running = false;
        bossPhase = false;
        waveNumber = 0;

        if (spawnLoop != null)
        {
            StopCoroutine(spawnLoop);
            spawnLoop = null;
        }
        StopAllCoroutines();

        if (boss != null)
        {
            Destroy(boss.gameObject);
            boss = null;
        }
        if (gemManager != null) gemManager.ClearGems();
        
    }

    IEnumerator SpawnLoop()
    {
        // Continuous waves of 3 until all gems are collected
        while (running && !bossPhase)
        {
            // Count up but never past maxWaves — the run is meant to end at the boss
            // (all 4 gems), so the on-screen wave number must not run away forever.
            bool advanced = waveNumber < maxWaves;
            if (advanced) waveNumber++;

            // Keep GameManager + HUD in sync with the director's wave number
            if (GameManager.Instance != null) GameManager.Instance.currentWave = waveNumber;

            GameHUD hud = GetHud();
            if (hud != null)
            {
                hud.UpdateWave(waveNumber);
                if (advanced) hud.ShowWaveAnnouncement(waveNumber);
            }

            yield return new WaitForSeconds(1.2f);

            if (!running || bossPhase) yield break;

            if (waveManager != null)
            {
                float speedMult = 1f + (waveNumber - 1) * 0.06f
                    + (gemManager != null ? gemManager.CollectedCount * 0.08f : 0f);
                waveManager.SpawnWave(aliensPerWave, speedMult, 1f, false, waveNumber);
            }

            // GUARANTEED BOSS: once the player reaches bossAfterWave, summon the dragon
            // even if they never found all 4 gems, so the fight always happens.
            if (waveNumber >= bossAfterWave && !bossPhase)
            {
                TriggerBoss();
                yield break;
            }

            float interval = baseWaveInterval;
            if (gemManager != null)
                interval -= gemManager.CollectedCount * intervalReductionPerGem;
            interval = Mathf.Max(minWaveInterval, interval);

            float elapsed = 0f;
            while (elapsed < interval && running && !bossPhase)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }

    void OnGemCollected(int count)
    {
        if (!running) return;

        GameHUD hud = GetHud();
        if (hud != null && count < (gemManager != null ? gemManager.TotalGems : 4))
        {
            hud.ShowGemMessage(count, gemManager != null ? gemManager.TotalGems : 4);
        }
    }

    void OnAllGemsCollected()
    {
        TriggerBoss();
    }

    /// <summary>Enters the boss phase once (whether via 4 gems or the wave fallback).</summary>
    void TriggerBoss()
    {
        if (!running || bossPhase) return;
        bossPhase = true;

        // Stop the wave loop
        if (spawnLoop != null)
        {
            StopCoroutine(spawnLoop);
            spawnLoop = null;
        }

        StartCoroutine(BossSequence());
    }

    IEnumerator BossSequence()
    {
        GameHUD hud = GetHud();
        if (hud != null) hud.ShowWaveAnnouncement(-1); // "BOSS INCOMING"

        yield return new WaitForSeconds(bossSpawnDelay);

        if (!running) yield break;

        SpawnBoss();
    }

    void SpawnBoss()
    {
        Vector3 spawnPos = FindWallSpawnPosition();

        GameObject bossGO = new GameObject("GemHuntBoss");
        bossGO.transform.position = spawnPos;

        BossHealth bh = bossGO.AddComponent<BossHealth>();
        // ~10x a regular alien's headshot-kill-equivalent (3 HP): 30 HP
        bh.maxHealth = 30;
        bh.ResetHealth();

        boss = bossGO.AddComponent<GemBossController>();
        boss.onDefeated = OnBossDefeated;

        // Face the player from the wall
        if (Camera.main != null)
        {
            Vector3 look = Camera.main.transform.position - spawnPos;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
                bossGO.transform.rotation = Quaternion.LookRotation(look.normalized);
        }

        boss.ActivateBoss();
    }

    /// <summary>
    /// Spawns the boss directly in FRONT of the player at floor level so the dragon
    /// is always immediately visible (spawning at a random wall risked it appearing
    /// behind the player, where they'd never see it).
    /// </summary>
    Vector3 FindWallSpawnPosition()
    {
        Transform head = Camera.main != null ? Camera.main.transform : null;
        Vector3 headPos = head != null ? head.position : Vector3.zero;
        float floorY = headPos.y - 1.5f;

        // Use the AR floor height when available for an accurate ground level
        ARPlaneManager planeManager = waveManager != null && waveManager.planeManager != null
            ? waveManager.planeManager
            : FindFirstObjectByType<ARPlaneManager>();
        if (planeManager != null && planeManager.subsystem != null && planeManager.subsystem.running)
        {
            foreach (var plane in planeManager.trackables)
            {
                if (plane.alignment == PlaneAlignment.HorizontalUp
                    && plane.transform.position.y < headPos.y - 0.5f)
                {
                    floorY = Mathf.Max(floorY, plane.transform.position.y);
                }
            }
        }

        // In front of the player, on their horizontal facing
        Vector3 fwd = head != null ? head.forward : Vector3.forward;
        fwd.y = 0f;
        fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
        Vector3 pos = headPos + fwd * bossSpawnDistance;
        pos.y = floorY;
        return pos;
    }

    void OnBossDefeated()
    {
        if (!running) return;
        running = false;

        float timeTaken = Time.time - runStartTime;
        int shotsFired = PlayerShooter.TotalShotsFired;

        GameHUD hud = GetHud();
        if (hud != null) hud.ShowVictoryScreen(timeTaken, shotsFired);

        if (GameManager.Instance != null)
            GameManager.Instance.OnGemHuntVictory();
    }

    GameHUD GetHud()
    {
        if (GameManager.Instance != null && GameManager.Instance.gameHUD != null)
            return GameManager.Instance.gameHUD;
        return FindFirstObjectByType<GameHUD>();
    }

    public bool IsRunning => running;
    public bool IsBossPhase => bossPhase;
}