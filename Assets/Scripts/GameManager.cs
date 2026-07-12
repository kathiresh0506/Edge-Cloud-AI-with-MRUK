using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { WaitingToStart, Playing, WaveComplete, GameOver }

    [Header("Game State")]
    public GameState currentState = GameState.WaitingToStart;
    public int currentWave = 0;
    public int score = 0;
    public int totalKills = 0;
    public int killsThisWave = 0;
    public int aliensRemainingThisWave = 0;

    [Header("Wave Settings")]
    public int baseAliensPerWave = 3;
    public int aliensPerWaveIncrement = 2;
    public float timeBetweenWaves = 5f;
    public float alienSpeedMultiplierPerWave = 0.1f;
    public float alienHealthMultiplierPerWave = 0.15f;

    [Header("Game Mode")]
    [Tooltip("Gem Hunt: continuous waves + 4 hidden gems -> darkness -> wall boss -> victory. Off = classic endless waves.")]
    public bool gemHuntMode = true;
    public GemHuntDirector gemHuntDirector;

    [Header("References")]
    public AlienWaveManager waveManager;
    public PlayerHealth playerHealth;
    public GameHUD gameHUD;

    [Header("Events")]
    public UnityEvent onGameStart;
    public UnityEvent onWaveStart;
    public UnityEvent onWaveComplete;
    public UnityEvent onGameOver;
    public UnityEvent<int> onScoreChanged;
    public UnityEvent<int> onWaveChanged;
    public UnityEvent<int> onAlienKilled;

    private int aliensSpawnedThisWave = 0;
    private AudioSource audioSource;
    private AudioClip waveFanfare;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        currentState = GameState.WaitingToStart;

        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
        waveFanfare = ProceduralAudioGenerator.GenerateWaveFanfare();

        if (gameHUD != null)
        {
            gameHUD.ShowStartScreen(true);
        }
    }

    void Update()
    {
        // Auto-start after a short delay for MR (no start button needed in VR)
        if (currentState == GameState.WaitingToStart)
        {
            // Player can start by pressing trigger, handled by PlayerShooter
        }
    }

    private float lastStartTime = -10f;

    public void StartGame()
    {
        if (currentState != GameState.WaitingToStart && currentState != GameState.GameOver)
            return;

        // Debounce: ignore duplicate start requests within 3s (input noise / double triggers)
        if (Time.time - lastStartTime < 3f)
            return;
        lastStartTime = Time.time;

        currentState = GameState.Playing;
        currentWave = 0;
        score = 0;
        totalKills = 0;

        if (playerHealth != null)
            playerHealth.ResetHealth();

        if (gameHUD != null)
        {
            gameHUD.ShowStartScreen(false);
            gameHUD.ShowGameOverScreen(false);
        }

        onGameStart?.Invoke();
        onScoreChanged?.Invoke(score);

        // Gem Hunt mode: the director owns the whole flow (waves, gems, darkness, boss)
        if (gemHuntMode)
        {
            if (gemHuntDirector == null)
                gemHuntDirector = FindFirstObjectByType<GemHuntDirector>();
            if (gemHuntDirector != null)
            {
                gemHuntDirector.StartRun();
                return;
            }
            Debug.LogWarning("GameManager: gemHuntMode is on but no GemHuntDirector found — falling back to classic waves.");
        }

        StartCoroutine(StartNextWave());
    }

    /// <summary>Called by GemHuntDirector when the boss is defeated.</summary>
    public void OnGemHuntVictory()
    {
        currentState = GameState.GameOver;

        // Clear any remaining aliens so the celebration isn't interrupted
        if (waveManager != null)
            waveManager.DestroyAllAliens();

        onGameOver?.Invoke();
    }

    IEnumerator StartNextWave()
    {
        currentWave++;
        killsThisWave = 0;
        int alienCount = GetAlienCountForWave(currentWave);
        aliensRemainingThisWave = alienCount;
        aliensSpawnedThisWave = alienCount;

        onWaveChanged?.Invoke(currentWave);

        if (gameHUD != null)
        {
            gameHUD.ShowWaveAnnouncement(currentWave);
        }

        // Wait for announcement
        yield return new WaitForSeconds(2f);

        // Play wave fanfare
        if (waveFanfare != null && audioSource != null)
        {
            audioSource.PlayOneShot(waveFanfare, 0.6f);
        }

        if (currentState != GameState.Playing)
            yield break;

        // Spawn aliens
        if (waveManager != null)
        {
            float speedMult = 1f + (currentWave - 1) * alienSpeedMultiplierPerWave;
            float healthMult = 1f + (currentWave - 1) * alienHealthMultiplierPerWave;
            bool isBossWave = (currentWave % 5 == 0);

            waveManager.SpawnWave(alienCount, speedMult, healthMult, isBossWave, currentWave);
        }

        onWaveStart?.Invoke();
    }

    public int GetAlienCountForWave(int wave)
    {
        return baseAliensPerWave + (wave - 1) * aliensPerWaveIncrement;
    }

    public void OnAlienKilled(int scoreValue)
    {
        if (currentState != GameState.Playing)
            return;

        totalKills++;
        killsThisWave++;
        aliensRemainingThisWave--;
        score += scoreValue;

        onScoreChanged?.Invoke(score);
        onAlienKilled?.Invoke(totalKills);

        if (gameHUD != null)
        {
            gameHUD.UpdateKillCount(killsThisWave, aliensSpawnedThisWave);
        }

        // Gem Hunt: spawning is continuous (driven by the director) — no wave-complete flow
        if (gemHuntMode)
            return;

        // Check if wave is complete
        if (aliensRemainingThisWave <= 0)
        {
            StartCoroutine(WaveComplete());
        }
    }

    IEnumerator WaveComplete()
    {
        currentState = GameState.WaveComplete;
        onWaveComplete?.Invoke();

        if (gameHUD != null)
        {
            gameHUD.ShowWaveComplete(currentWave);
        }

        yield return new WaitForSeconds(timeBetweenWaves);

        if (currentState == GameState.GameOver)
            yield break;

        // BOSS WAVE: after wave 1 and every 5 waves
        if (currentWave == 1 || currentWave % 5 == 0)
        {
            currentState = GameState.Playing;
            StartBossWave();
            yield break;
        }

        currentState = GameState.Playing;
        StartCoroutine(StartNextWave());
    }

    void StartBossWave()
    {
        // Create boss wave manager if needed
        GameObject bossManagerGO = new GameObject("BossWaveManager");
        BossWaveManager bossManager = bossManagerGO.AddComponent<BossWaveManager>();
        bossManager.onBossWaveComplete = OnBossWaveComplete;
        bossManager.StartBossWave();
    }

    void OnBossWaveComplete()
    {
        // Resume normal waves after boss is defeated
        if (currentState == GameState.GameOver) return;
        currentState = GameState.Playing;
        StartCoroutine(StartNextWave());
    }

    public void OnPlayerDeath()
    {
        if (currentState == GameState.GameOver)
            return;

        currentState = GameState.GameOver;

        // Stop the gem hunt (clears gems/boss and lifts the darkness)
        if (gemHuntDirector != null)
            gemHuntDirector.ResetRun();

        // Destroy all remaining aliens
        if (waveManager != null)
            waveManager.DestroyAllAliens();

        if (gameHUD != null)
        {
            gameHUD.ShowGameOverScreen(true, score, currentWave, totalKills);
        }

        onGameOver?.Invoke();
    }

    public void RestartGame()
    {
        // Debounce restarts too (grip mashing / input noise)
        if (Time.time - lastStartTime < 3f)
            return;

        // Stop all running coroutines (wave spawning, announcements, etc.)
        StopAllCoroutines();

        // Gem Hunt cleanup: gems, boss, darkness, spawn loop
        if (gemHuntDirector == null)
            gemHuntDirector = FindFirstObjectByType<GemHuntDirector>();
        if (gemHuntDirector != null)
            gemHuntDirector.ResetRun();

        // Clean up all aliens
        if (waveManager != null)
            waveManager.DestroyAllAliens();

        // Clean up weapon pickups
        WeaponPickup[] pickups = Object.FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None);
        foreach (var p in pickups) Destroy(p.gameObject);

        // Clean up room cracks
        RoomCrackVFX[] cracks = Object.FindObjectsByType<RoomCrackVFX>(FindObjectsSortMode.None);
        foreach (var c in cracks) Destroy(c.gameObject);

        // Reset stats
        score = 0;
        currentWave = 0;
        totalKills = 0;
        aliensSpawnedThisWave = 0;

        // Reset player health
        PlayerHealth playerHealth = Object.FindFirstObjectByType<PlayerHealth>();
        if (playerHealth != null)
            playerHealth.ResetHealth();

        // Reset HUD
        if (gameHUD != null)
        {
            gameHUD.ShowGameOverScreen(false);
            gameHUD.ShowStartScreen(false);
            gameHUD.HideVictoryScreen();
            gameHUD.UpdateGemCount(0, 4);
            gameHUD.UpdateScore(0);
            gameHUD.UpdateWave(1);
            gameHUD.UpdateKillCount(0, 0);
            gameHUD.UpdateHealth(1f);
        }

        currentState = GameState.WaitingToStart;
        StartGame();
    }

    public float GetCurrentSpeedMultiplier()
    {
        return 1f + (currentWave - 1) * alienSpeedMultiplierPerWave;
    }

    public float GetCurrentHealthMultiplier()
    {
        return 1f + (currentWave - 1) * alienHealthMultiplierPerWave;
    }
}
