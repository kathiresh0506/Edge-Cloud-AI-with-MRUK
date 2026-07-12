using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

/// <summary>
/// Local AI bridge for:
///
/// Unity game
///     -> localhost Python server
///     -> local Qwen model
///     -> Piper TTS
///     -> commentary audio returned to Unity
///
/// Python endpoints expected:
///
/// GET  /health
/// POST /game-state
/// POST /commentary
///
/// /commentary should return JSON like:
///
/// {
///     "text": "That was an incredible headshot!",
///     "audio_url": "/audio/commentary_123.wav"
/// }
/// </summary>
public class GameBridge : MonoBehaviour
{
    public static GameBridge Instance { get; private set; }
    // ============================================================
    // Compatibility with existing game scripts
    // ============================================================

    // These remain false because Qualcomm Cloud and Arduino are not being used.
    [HideInInspector]
    public bool cloudOnline = false;

    [HideInInspector]
    public bool arduinoOnline = false;

    // AlienAI.cs expects this property.
    public IEnemyDirector EnemyDirector { get; set; }

    /// <summary>
    /// Existing StoryManager and other scripts call Narrate().
    /// The line is sent to the local Qwen + Piper server.
    /// </summary>
    public void Narrate(string line, string speaker = "THE VOICE")
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        commentatorName = speaker;

        ReportGameplayEvent(
            "narration",
            line
        );
    }

    /// <summary>
    /// Existing boss scripts may call BossSay().
    /// </summary>
    public void BossSay(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        commentatorName = "THE OVERLORD";

        ReportGameplayEvent(
            "boss_dialogue",
            line
        );
    }

    /// <summary>
    /// Existing conversation scripts may call this when the player speaks.
    /// </summary>
    public void OnPlayerSpeech(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        commentatorName = "AI COMMENTATOR";

        ReportGameplayEvent(
            "player_speech",
            "The player said: " + text
        );
    }

    // ============================================================
    // Inspector settings
    // ============================================================

    [Header("Local Python AI Server")]
    [Tooltip("Python FastAPI server running on the same computer.")]
    public string serverBaseUrl = "http://127.0.0.1:8000";

    [Tooltip("Candidate PC addresses probed at startup. 127.0.0.1 only works in the editor — on the Quest the game must reach the PC over Wi-Fi, so the PC's LAN IPs are listed too. The first host whose /health answers becomes serverBaseUrl.")]
    public string[] candidateServerHosts =
    {
        "127.0.0.1",       // editor / same-PC testing
        "172.20.10.2",     // PC's IP on the phone hotspot
        "192.168.137.1",   // PC's IP when using Windows Mobile Hotspot
    };
    public int serverPort = 8000;

    public string healthEndpoint = "/health";
    public string telemetryEndpoint = "/game-state";
    public string commentaryEndpoint = "/commentary";

    [Header("Live Data")]
    [Tooltip("How often Unity reads player and enemy information.")]
    [Range(0.1f, 5f)]
    public float stateSampleInterval = 0.25f;

    [Tooltip("How often Unity sends the current game state to Python.")]
    [Range(0.25f, 10f)]
    public float telemetrySendInterval = 1f;

    [Tooltip("How often Unity searches for newly spawned aliens and bosses.")]
    [Range(0.5f, 10f)]
    public float componentRefreshInterval = 1f;

    public bool sendLiveTelemetry = true;

    [Header("Commentary")]
    [Tooltip("Minimum gap between two Qwen commentary requests.")]
    [Range(0.5f, 15f)]
    public float commentaryCooldown = 2f;

    [Tooltip("Maximum waiting events. Old events are removed when full.")]
    [Range(1, 30)]
    public int maximumEventQueue = 10;

    public bool automaticallyCommentOnLowHealth = true;
    public bool automaticallyCommentOnBossLowHealth = true;

    [Header("Network")]
    public int requestTimeoutSeconds = 60;
    public bool showNetworkLogs = true;

    [Header("Audio")]
    [Range(0f, 1f)]
    public float commentatorVolume = 1f;

    public bool stopPreviousCommentary = true;

    [Header("Caption")]
    public bool showCaptions = true;
    public string commentatorName = "AI COMMENTATOR";
    public float minimumCaptionTime = 3f;
    public float captionTimePerCharacter = 0.045f;

    // ============================================================
    // Serializable request and response models
    // ============================================================

    [Serializable]
    public class VectorData
    {
        public float x;
        public float y;
        public float z;

        public VectorData()
        {
            x = 0;
            y = 0;
            z = 0;
        }

        public VectorData(Vector3 value)
        {
            x = Round(value.x);
            y = Round(value.y);
            z = Round(value.z);
        }

        private static float Round(float value)
        {
            return Mathf.Round(value * 100f) / 100f;
        }
    }

    [Serializable]
    public class GameStatePacket
    {
        public string timestamp;
        public string sceneName;

        public bool gameRunning;
        public bool playerAlive;

        public float playerHealth;
        public float playerMaxHealth;
        public float playerHealthPercent;

        public VectorData playerPosition;
        public float playerSpeed;

        public string weapon;
        public int currentAmmo;
        public int maximumAmmo;

        public int shotsFired;
        public int successfulHits;
        public int headshots;
        public int kills;
        public int score;
        public int wave;

        public int alienCount;
        public float nearestAlienHealth;
        public float nearestAlienMaxHealth;
        public float nearestAlienHealthPercent;
        public float nearestAlienDistance;

        public bool bossPresent;
        public float bossHealth;
        public float bossMaxHealth;
        public float bossHealthPercent;

        public string latestEvent;
        public string latestEventDetails;
    }

    [Serializable]
    public class CommentaryRequest
    {
        public string eventType;
        public string eventDetails;
        public GameStatePacket state;
    }

    [Serializable]
    private class AIResponse
    {
        public bool success;

        public string text;
        public string response;
        public string commentary;
        public string message;

        public string audio;
        public string audio_url;
        public string audioUrl;
        public string audio_path;
    }

    [Serializable]
    private class PendingCommentaryEvent
    {
        public string eventType;
        public string details;
        public float queuedAt;
    }

    // ============================================================
    // Runtime references
    // ============================================================

    private MonoBehaviour playerHealthComponent;
    private MonoBehaviour playerShooterComponent;
    private MonoBehaviour gameManagerComponent;
    private MonoBehaviour waveManagerComponent;
    private MonoBehaviour bossHealthComponent;

    private readonly List<MonoBehaviour> alienHealthComponents =
        new List<MonoBehaviour>();

    private Transform playerTransform;
    private Rigidbody playerRigidbody;

    private AudioSource commentaryAudioSource;

    // ============================================================
    // Runtime statistics
    // ============================================================

    private int trackedShots;
    private int trackedHits;
    private int trackedHeadshots;
    private int trackedKills;
    private int trackedScore;
    private int trackedWave;

    private string trackedWeapon = "Unknown";
    private int trackedCurrentAmmo = -1;
    private int trackedMaximumAmmo = -1;

    private string latestEvent = "game_started";
    private string latestEventDetails = "";

    private float previousPlayerHealthPercent = 100f;
    private float previousBossHealthPercent = 100f;

    private bool playerLowHealthEventSent;
    private bool bossLowHealthEventSent;

    // ============================================================
    // Networking state
    // ============================================================

    public bool ServerOnline { get; private set; }
    public GameStatePacket CurrentState { get; private set; }

    private bool telemetryRequestRunning;
    private bool commentaryQueueRunning;

    private float lastCommentaryRequestTime = -100f;
    private float nextNetworkErrorLogTime;

    private readonly Queue<PendingCommentaryEvent> commentaryEvents =
        new Queue<PendingCommentaryEvent>();

    // ============================================================
    // Caption UI
    // ============================================================

    private Canvas captionCanvas;
    private Image captionBackground;
    private Text captionSpeaker;
    private Text captionText;

    private Coroutine captionCoroutine;

    // ============================================================
    // Automatic creation
    // ============================================================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        if (Instance != null)
            return;

        GameObject bridgeObject = new GameObject("GameBridge_LocalAI");
        DontDestroyOnLoad(bridgeObject);
        bridgeObject.AddComponent<GameBridge>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        commentaryAudioSource = gameObject.GetComponent<AudioSource>();

        if (commentaryAudioSource == null)
            commentaryAudioSource = gameObject.AddComponent<AudioSource>();

        commentaryAudioSource.playOnAwake = false;
        commentaryAudioSource.loop = false;
        commentaryAudioSource.spatialBlend = 0f;
        commentaryAudioSource.volume = commentatorVolume;

        if (showCaptions)
            BuildCaptionUI();
    }

    private void Start()
    {
        DiscoverGameComponents();

        StartCoroutine(ComponentDiscoveryLoop());
        StartCoroutine(StateSamplingLoop());
        StartCoroutine(TelemetryLoop());
        StartCoroutine(ServerHealthLoop());

        ReportGameplayEvent(
            "game_started",
            "The game has started. Introduce the match briefly."
        );
    }

    // ============================================================
    // Main update loops
    // ============================================================

    private IEnumerator ComponentDiscoveryLoop()
    {
        while (true)
        {
            DiscoverGameComponents();

            yield return new WaitForSecondsRealtime(
                Mathf.Max(0.5f, componentRefreshInterval)
            );
        }
    }

    private IEnumerator StateSamplingLoop()
    {
        while (true)
        {
            CurrentState = CollectCurrentGameState();
            CheckAutomaticEvents(CurrentState);

            yield return new WaitForSecondsRealtime(
                Mathf.Max(0.1f, stateSampleInterval)
            );
        }
    }

    private IEnumerator TelemetryLoop()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(
                Mathf.Max(0.25f, telemetrySendInterval)
            );

            if (!sendLiveTelemetry)
                continue;

            if (telemetryRequestRunning)
                continue;

            if (CurrentState == null)
                CurrentState = CollectCurrentGameState();

            StartCoroutine(SendTelemetry(CurrentState));
        }
    }

    private IEnumerator ServerHealthLoop()
    {
        // First: find WHICH address the server is actually reachable on.
        // On the Quest, 127.0.0.1 is the headset itself — the PC must be
        // reached via its Wi-Fi/hotspot LAN IP.
        yield return StartCoroutine(ProbeForServer());

        while (true)
        {
            yield return StartCoroutine(CheckServerHealth());
            yield return new WaitForSecondsRealtime(5f);
        }
    }

    /// <summary>
    /// Tries /health on every candidate host until one answers, then locks
    /// serverBaseUrl onto it. Keeps retrying every 8s while nothing responds.
    /// </summary>
    private IEnumerator ProbeForServer()
    {
        while (true)
        {
            foreach (string host in candidateServerHosts)
            {
                string url = "http://" + host + ":" + serverPort;

                using (UnityWebRequest request =
                       UnityWebRequest.Get(url + healthEndpoint))
                {
                    request.timeout = 3;
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        serverBaseUrl = url;
                        ServerOnline = true;

                        if (showNetworkLogs)
                            Debug.Log(
                                "[GameBridge] AI server found at " + serverBaseUrl
                            );

                        yield break;
                    }
                }
            }

            if (showNetworkLogs)
                Debug.LogWarning(
                    "[GameBridge] AI server not reachable on any candidate host. " +
                    "Check: server running? Firewall allows port " + serverPort +
                    "? Quest and PC on the same Wi-Fi? Retrying in 8s."
                );

            yield return new WaitForSecondsRealtime(8f);
        }
    }

    // ============================================================
    // Component discovery
    // ============================================================

    private void DiscoverGameComponents()
    {
        alienHealthComponents.Clear();

#pragma warning disable CS0618
        MonoBehaviour[] allComponents =
            UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
#pragma warning restore CS0618

        foreach (MonoBehaviour component in allComponents)
        {
            if (component == null)
                continue;

            string typeName = component.GetType().Name;

            switch (typeName)
            {
                case "PlayerHealth":
                    playerHealthComponent = component;
                    playerTransform = component.transform;
                    break;

                case "PlayerShooter":
                case "PlayerShoot":
                    playerShooterComponent = component;

                    if (playerTransform == null)
                        playerTransform = component.transform;

                    break;

                case "AlienHealth":
                    alienHealthComponents.Add(component);
                    break;

                case "BossHealth":
                    bossHealthComponent = component;
                    break;

                case "GameManager":
                    gameManagerComponent = component;
                    break;

                case "AlienWaveManager":
                case "WaveManager":
                    waveManagerComponent = component;
                    break;
            }
        }

        if (playerTransform != null)
        {
            playerRigidbody =
                playerTransform.GetComponent<Rigidbody>();

            if (playerRigidbody == null)
            {
                playerRigidbody =
                    playerTransform.GetComponentInParent<Rigidbody>();
            }
        }
    }

    // ============================================================
    // Live state collection
    // ============================================================

    private GameStatePacket CollectCurrentGameState()
    {
        GameStatePacket state = new GameStatePacket();

        state.timestamp = DateTime.UtcNow.ToString("o");
        state.sceneName =
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        state.gameRunning = Time.timeScale > 0f;

        ReadPlayerState(state);
        ReadWeaponState(state);
        ReadStatistics(state);
        ReadAlienState(state);
        ReadBossState(state);

        state.latestEvent = latestEvent;
        state.latestEventDetails = latestEventDetails;

        return state;
    }

    private void ReadPlayerState(GameStatePacket state)
    {
        float currentHealth = ReadFloat(
            playerHealthComponent,
            -1f,
            "currentHealth",
            "CurrentHealth",
            "health",
            "Health",
            "currentHP",
            "CurrentHP",
            "hp",
            "HP"
        );

        float maximumHealth = ReadFloat(
            playerHealthComponent,
            -1f,
            "maxHealth",
            "MaxHealth",
            "maximumHealth",
            "MaximumHealth",
            "maxHP",
            "MaxHP"
        );

        if (maximumHealth <= 0f)
            maximumHealth = 100f;

        if (currentHealth < 0f)
            currentHealth = maximumHealth;

        state.playerHealth = Round(currentHealth);
        state.playerMaxHealth = Round(maximumHealth);

        state.playerHealthPercent =
            maximumHealth > 0f
                ? Round((currentHealth / maximumHealth) * 100f)
                : 0f;

        state.playerAlive = currentHealth > 0f;

        if (playerTransform != null)
            state.playerPosition = new VectorData(playerTransform.position);
        else
            state.playerPosition = new VectorData();

        if (playerRigidbody != null)
            state.playerSpeed = Round(playerRigidbody.linearVelocity.magnitude);
        else
            state.playerSpeed = 0f;
    }

    private void ReadWeaponState(GameStatePacket state)
    {
        string reflectedWeapon = ReadString(
            playerShooterComponent,
            "",
            "currentWeaponName",
            "CurrentWeaponName",
            "weaponName",
            "WeaponName",
            "currentWeapon",
            "CurrentWeapon",
            "equippedWeapon",
            "EquippedWeapon",
            "weapon",
            "Weapon"
        );

        if (!string.IsNullOrWhiteSpace(reflectedWeapon) &&
            reflectedWeapon != "Unknown")
        {
            trackedWeapon = reflectedWeapon;
        }

        int reflectedAmmo = ReadInt(
            playerShooterComponent,
            -1,
            "currentAmmo",
            "CurrentAmmo",
            "ammo",
            "Ammo",
            "bulletsRemaining",
            "BulletsRemaining",
            "magazineAmmo",
            "MagazineAmmo"
        );

        int reflectedMaximumAmmo = ReadInt(
            playerShooterComponent,
            -1,
            "maxAmmo",
            "MaxAmmo",
            "maximumAmmo",
            "MaximumAmmo",
            "magazineSize",
            "MagazineSize",
            "clipSize",
            "ClipSize"
        );

        if (reflectedAmmo >= 0)
            trackedCurrentAmmo = reflectedAmmo;

        if (reflectedMaximumAmmo >= 0)
            trackedMaximumAmmo = reflectedMaximumAmmo;

        state.weapon = string.IsNullOrWhiteSpace(trackedWeapon)
            ? "Unknown"
            : trackedWeapon;

        state.currentAmmo = trackedCurrentAmmo;
        state.maximumAmmo = trackedMaximumAmmo;
    }

    private void ReadStatistics(GameStatePacket state)
    {
        int reflectedShots = ReadInt(
            playerShooterComponent,
            -1,
            "shotsFired",
            "ShotsFired",
            "totalShots",
            "TotalShots"
        );

        int reflectedHits = ReadInt(
            playerShooterComponent,
            -1,
            "successfulHits",
            "SuccessfulHits",
            "totalHits",
            "TotalHits",
            "hits",
            "Hits"
        );

        int reflectedHeadshots = ReadInt(
            playerShooterComponent,
            -1,
            "headshots",
            "Headshots",
            "headshotCount",
            "HeadshotCount"
        );

        int reflectedKills = ReadInt(
            playerShooterComponent,
            -1,
            "kills",
            "Kills",
            "killCount",
            "KillCount",
            "totalKills",
            "TotalKills"
        );

        int reflectedScore = ReadInt(
            gameManagerComponent,
            -1,
            "score",
            "Score",
            "currentScore",
            "CurrentScore",
            "totalScore",
            "TotalScore"
        );

        int reflectedWave = ReadInt(
            waveManagerComponent,
            -1,
            "currentWave",
            "CurrentWave",
            "wave",
            "Wave",
            "waveNumber",
            "WaveNumber"
        );

        if (reflectedWave < 0)
        {
            reflectedWave = ReadInt(
                gameManagerComponent,
                -1,
                "currentWave",
                "CurrentWave",
                "wave",
                "Wave"
            );
        }

        if (reflectedShots >= 0)
            trackedShots = Mathf.Max(trackedShots, reflectedShots);

        if (reflectedHits >= 0)
            trackedHits = Mathf.Max(trackedHits, reflectedHits);

        if (reflectedHeadshots >= 0)
            trackedHeadshots = Mathf.Max(
                trackedHeadshots,
                reflectedHeadshots
            );

        if (reflectedKills >= 0)
            trackedKills = Mathf.Max(trackedKills, reflectedKills);

        if (reflectedScore >= 0)
            trackedScore = reflectedScore;

        if (reflectedWave >= 0)
            trackedWave = reflectedWave;

        state.shotsFired = trackedShots;
        state.successfulHits = trackedHits;
        state.headshots = trackedHeadshots;
        state.kills = trackedKills;
        state.score = trackedScore;
        state.wave = trackedWave;
    }

    private void ReadAlienState(GameStatePacket state)
    {
        state.alienCount = 0;
        state.nearestAlienHealth = 0f;
        state.nearestAlienMaxHealth = 0f;
        state.nearestAlienHealthPercent = 0f;
        state.nearestAlienDistance = -1f;

        MonoBehaviour nearestAlien = null;
        float nearestDistance = float.MaxValue;

        foreach (MonoBehaviour alien in alienHealthComponents)
        {
            if (alien == null ||
                !alien.gameObject.activeInHierarchy)
            {
                continue;
            }

            float alienHealth = ReadFloat(
                alien,
                -1f,
                "currentHealth",
                "CurrentHealth",
                "health",
                "Health",
                "currentHP",
                "CurrentHP",
                "hp",
                "HP"
            );

            if (alienHealth <= 0f)
                continue;

            state.alienCount++;

            float distance = 0f;

            if (playerTransform != null)
            {
                distance = Vector3.Distance(
                    playerTransform.position,
                    alien.transform.position
                );
            }

            if (nearestAlien == null || distance < nearestDistance)
            {
                nearestAlien = alien;
                nearestDistance = distance;
            }
        }

        if (nearestAlien == null)
            return;

        float nearestCurrentHealth = ReadFloat(
            nearestAlien,
            0f,
            "currentHealth",
            "CurrentHealth",
            "health",
            "Health",
            "currentHP",
            "CurrentHP",
            "hp",
            "HP"
        );

        float nearestMaximumHealth = ReadFloat(
            nearestAlien,
            100f,
            "maxHealth",
            "MaxHealth",
            "maximumHealth",
            "MaximumHealth",
            "maxHP",
            "MaxHP"
        );

        state.nearestAlienHealth = Round(nearestCurrentHealth);
        state.nearestAlienMaxHealth = Round(nearestMaximumHealth);

        state.nearestAlienHealthPercent =
            nearestMaximumHealth > 0f
                ? Round(
                    nearestCurrentHealth /
                    nearestMaximumHealth *
                    100f
                )
                : 0f;

        state.nearestAlienDistance =
            nearestDistance == float.MaxValue
                ? -1f
                : Round(nearestDistance);
    }

    private void ReadBossState(GameStatePacket state)
    {
        if (bossHealthComponent == null ||
            !bossHealthComponent.gameObject.activeInHierarchy)
        {
            state.bossPresent = false;
            state.bossHealth = 0f;
            state.bossMaxHealth = 0f;
            state.bossHealthPercent = 0f;
            return;
        }

        float currentBossHealth = ReadFloat(
            bossHealthComponent,
            -1f,
            "currentHealth",
            "CurrentHealth",
            "health",
            "Health",
            "currentHP",
            "CurrentHP",
            "hp",
            "HP"
        );

        float maximumBossHealth = ReadFloat(
            bossHealthComponent,
            100f,
            "maxHealth",
            "MaxHealth",
            "maximumHealth",
            "MaximumHealth",
            "maxHP",
            "MaxHP"
        );

        state.bossPresent = currentBossHealth > 0f;
        state.bossHealth = Mathf.Max(0f, Round(currentBossHealth));
        state.bossMaxHealth = Round(maximumBossHealth);

        state.bossHealthPercent =
            maximumBossHealth > 0f
                ? Round(
                    Mathf.Max(0f, currentBossHealth) /
                    maximumBossHealth *
                    100f
                )
                : 0f;
    }

    // ============================================================
    // Automatic commentary triggers
    // ============================================================

    private void CheckAutomaticEvents(GameStatePacket state)
    {
        if (state == null)
            return;

        if (automaticallyCommentOnLowHealth)
        {
            if (state.playerHealthPercent <= 25f &&
                previousPlayerHealthPercent > 25f &&
                !playerLowHealthEventSent)
            {
                playerLowHealthEventSent = true;

                ReportGameplayEvent(
                    "player_low_health",
                    "The player has fallen below 25 percent health."
                );
            }

            if (state.playerHealthPercent >= 45f)
                playerLowHealthEventSent = false;
        }

        if (automaticallyCommentOnBossLowHealth &&
            state.bossPresent)
        {
            if (state.bossHealthPercent <= 25f &&
                previousBossHealthPercent > 25f &&
                !bossLowHealthEventSent)
            {
                bossLowHealthEventSent = true;

                ReportGameplayEvent(
                    "boss_low_health",
                    "The boss has less than 25 percent health remaining."
                );
            }

            if (state.bossHealthPercent >= 45f)
                bossLowHealthEventSent = false;
        }

        previousPlayerHealthPercent =
            state.playerHealthPercent;

        previousBossHealthPercent =
            state.bossHealthPercent;
    }

    // ============================================================
    // Public functions for gameplay scripts
    // ============================================================

    /// <summary>
    /// Call whenever the player fires.
    /// </summary>
    public void ReportShot(string weaponName = "")
    {
        trackedShots++;

        if (!string.IsNullOrWhiteSpace(weaponName))
            trackedWeapon = weaponName;

        latestEvent = "shot_fired";
        latestEventDetails = "The player fired the current weapon.";
    }

    /// <summary>
    /// Call whenever a projectile or ray hits an alien.
    /// </summary>
    public void ReportHit(
        float damage = 0f,
        string targetName = "alien"
    )
    {
        trackedHits++;

        latestEvent = "enemy_hit";
        latestEventDetails =
            "The player hit " + targetName +
            " for " + damage + " damage.";
    }

    /// <summary>
    /// Call when a headshot occurs.
    /// </summary>
    public void ReportHeadshot(
        string weaponName = "",
        float distance = 0f,
        string targetName = "alien"
    )
    {
        trackedHeadshots++;
        trackedHits++;

        if (!string.IsNullOrWhiteSpace(weaponName))
            trackedWeapon = weaponName;

        string details =
            "The player landed a headshot on " +
            targetName + ".";

        if (distance > 0f)
        {
            details +=
                " The distance was " +
                Round(distance) +
                " metres.";
        }

        ReportGameplayEvent("headshot", details);
    }

    /// <summary>
    /// Call when a normal alien dies.
    /// </summary>
    public void ReportKill(
        string enemyName = "alien",
        string weaponName = ""
    )
    {
        trackedKills++;

        if (!string.IsNullOrWhiteSpace(weaponName))
            trackedWeapon = weaponName;

        ReportGameplayEvent(
            "enemy_killed",
            "The player eliminated " + enemyName + "."
        );
    }

    /// <summary>
    /// Call when multiple enemies are killed quickly.
    /// </summary>
    public void ReportMultiKill(int killAmount)
    {
        trackedKills += Mathf.Max(0, killAmount);

        ReportGameplayEvent(
            "multi_kill",
            "The player eliminated " +
            killAmount +
            " enemies in rapid succession."
        );
    }

    /// <summary>
    /// Call when the boss appears.
    /// </summary>
    public void ReportBossAppeared(string bossName = "the boss")
    {
        ReportGameplayEvent(
            "boss_appeared",
            bossName + " has entered the battle."
        );

        DiscoverGameComponents();
    }

    /// <summary>
    /// Call when the boss is defeated.
    /// </summary>
    public void ReportBossDefeated(string bossName = "the boss")
    {
        trackedKills++;

        ReportGameplayEvent(
            "boss_defeated",
            "The player defeated " + bossName + "."
        );
    }

    /// <summary>
    /// Call when the player takes damage.
    /// </summary>
    public void ReportPlayerDamaged(float damage)
    {
        latestEvent = "player_damaged";
        latestEventDetails =
            "The player received " +
            damage +
            " damage.";

        if (CurrentState != null &&
            CurrentState.playerHealthPercent <= 25f)
        {
            ReportGameplayEvent(
                "critical_damage",
                latestEventDetails +
                " The player is now critically low on health."
            );
        }
    }

    /// <summary>
    /// Call when the player dies.
    /// </summary>
    public void ReportPlayerDeath()
    {
        ReportGameplayEvent(
            "player_died",
            "The player has been defeated."
        );
    }

    /// <summary>
    /// Call when health is restored.
    /// </summary>
    public void ReportPlayerHealed(float amount)
    {
        ReportGameplayEvent(
            "player_healed",
            "The player restored " +
            amount +
            " health."
        );
    }

    /// <summary>
    /// Call when a new weapon is equipped.
    /// </summary>
    public void SetCurrentWeapon(string weaponName)
    {
        trackedWeapon = string.IsNullOrWhiteSpace(weaponName)
            ? "Unknown"
            : weaponName;

        ReportGameplayEvent(
            "weapon_changed",
            "The player equipped " + trackedWeapon + "."
        );
    }

    /// <summary>
    /// Manually update ammo if reflection cannot read it.
    /// </summary>
    public void SetAmmo(int currentAmmo, int maximumAmmo)
    {
        trackedCurrentAmmo = currentAmmo;
        trackedMaximumAmmo = maximumAmmo;
    }

    /// <summary>
    /// Manually update the current wave.
    /// </summary>
    public void SetWave(int waveNumber)
    {
        trackedWave = waveNumber;

        ReportGameplayEvent(
            "wave_started",
            "Wave " + waveNumber + " has started."
        );
    }

    /// <summary>
    /// Call after a wave is completed.
    /// </summary>
    public void ReportWaveCompleted(int waveNumber)
    {
        trackedWave = waveNumber;

        ReportGameplayEvent(
            "wave_completed",
            "The player completed wave " +
            waveNumber + "."
        );
    }

    public void SetScore(int score)
    {
        trackedScore = score;
    }

    public void AddScore(int amount)
    {
        trackedScore += amount;
    }

    /// <summary>
    /// General-purpose commentary event.
    /// </summary>
    public void ReportGameplayEvent(
        string eventType,
        string details = ""
    )
    {
        if (string.IsNullOrWhiteSpace(eventType))
            eventType = "game_event";

        latestEvent = eventType;
        latestEventDetails = details;

        PendingCommentaryEvent pending =
            new PendingCommentaryEvent
            {
                eventType = eventType,
                details = details,
                queuedAt = Time.realtimeSinceStartup
            };

        while (commentaryEvents.Count >= maximumEventQueue)
            commentaryEvents.Dequeue();

        commentaryEvents.Enqueue(pending);

        if (!commentaryQueueRunning)
            StartCoroutine(ProcessCommentaryQueue());
    }

    // ============================================================
    // Commentary queue
    // ============================================================

    private IEnumerator ProcessCommentaryQueue()
    {
        commentaryQueueRunning = true;

        while (commentaryEvents.Count > 0)
        {
            float elapsed =
                Time.realtimeSinceStartup -
                lastCommentaryRequestTime;

            if (elapsed < commentaryCooldown)
            {
                yield return new WaitForSecondsRealtime(
                    commentaryCooldown - elapsed
                );
            }

            PendingCommentaryEvent pending =
                commentaryEvents.Dequeue();

            CurrentState = CollectCurrentGameState();

            yield return StartCoroutine(
                RequestCommentary(
                    pending.eventType,
                    pending.details,
                    CurrentState
                )
            );

            lastCommentaryRequestTime =
                Time.realtimeSinceStartup;
        }

        commentaryQueueRunning = false;
    }

    // ============================================================
    // HTTP requests
    // ============================================================

    private IEnumerator CheckServerHealth()
    {
        string url =
            BuildUrl(healthEndpoint);

        using (UnityWebRequest request =
               UnityWebRequest.Get(url))
        {
            request.timeout = requestTimeoutSeconds;

            yield return request.SendWebRequest();

            bool success =
                request.result ==
                UnityWebRequest.Result.Success;

            if (success != ServerOnline && showNetworkLogs)
            {
                if (success)
                    Debug.Log(
                        "[GameBridge] Local AI server connected: " +
                        serverBaseUrl
                    );
                else
                    Debug.LogWarning(
                        "[GameBridge] Local AI server is not available."
                    );
            }

            ServerOnline = success;
        }
    }

    private IEnumerator SendTelemetry(GameStatePacket state)
    {
        telemetryRequestRunning = true;

        string json = JsonUtility.ToJson(state);

        using (UnityWebRequest request =
               CreateJsonPostRequest(
                   BuildUrl(telemetryEndpoint),
                   json
               ))
        {
            yield return request.SendWebRequest();

            bool success =
                request.result ==
                UnityWebRequest.Result.Success;

            ServerOnline = success;

            if (!success)
                LogNetworkError("Telemetry", request);
        }

        telemetryRequestRunning = false;
    }

    private IEnumerator RequestCommentary(
        string eventType,
        string details,
        GameStatePacket state
    )
    {
        CommentaryRequest payload =
            new CommentaryRequest
            {
                eventType = eventType,
                eventDetails = details,
                state = state
            };

        string json = JsonUtility.ToJson(payload);

        if (showNetworkLogs)
        {
            Debug.Log(
                "[GameBridge] Sending commentary event: " +
                eventType
            );
        }

        using (UnityWebRequest request =
               CreateJsonPostRequest(
                   BuildUrl(commentaryEndpoint),
                   json
               ))
        {
            yield return request.SendWebRequest();

            if (request.result !=
                UnityWebRequest.Result.Success)
            {
                ServerOnline = false;
                LogNetworkError("Commentary", request);
                yield break;
            }

            ServerOnline = true;

            string responseBody =
                request.downloadHandler.text;

            HandleCommentaryResponse(responseBody);
        }
    }

    private UnityWebRequest CreateJsonPostRequest(
        string url,
        string json
    )
    {
        UnityWebRequest request =
            new UnityWebRequest(
                url,
                UnityWebRequest.kHttpVerbPOST
            );

        byte[] body =
            Encoding.UTF8.GetBytes(json);

        request.uploadHandler =
            new UploadHandlerRaw(body);

        request.downloadHandler =
            new DownloadHandlerBuffer();

        request.SetRequestHeader(
            "Content-Type",
            "application/json"
        );

        request.SetRequestHeader(
            "Accept",
            "application/json"
        );

        request.timeout = requestTimeoutSeconds;

        return request;
    }

    private void HandleCommentaryResponse(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return;

        AIResponse response = null;

        try
        {
            response =
                JsonUtility.FromJson<AIResponse>(
                    responseBody
                );
        }
        catch
        {
            response = null;
        }

        string commentaryText = "";
        string audioLocation = "";

        if (response != null)
        {
            commentaryText = FirstNonEmpty(
                response.text,
                response.commentary,
                response.response,
                response.message
            );

            audioLocation = FirstNonEmpty(
                response.audio_url,
                response.audioUrl,
                response.audio,
                response.audio_path
            );
        }

        // Allow Python to return plain text during early testing.
        if (string.IsNullOrWhiteSpace(commentaryText))
            commentaryText = responseBody.Trim();

        if (showNetworkLogs)
        {
            Debug.Log(
                "[GameBridge] Commentary: " +
                commentaryText
            );
        }

        ShowCommentaryCaption(commentaryText);

        if (!string.IsNullOrWhiteSpace(audioLocation))
        {
            string audioUrl =
                MakeAbsoluteAudioUrl(audioLocation);

            StartCoroutine(
                DownloadAndPlayAudio(audioUrl)
            );
        }
    }

    // ============================================================
    // Piper audio playback
    // ============================================================

    private IEnumerator DownloadAndPlayAudio(string audioUrl)
    {
        AudioType audioType =
            DetermineAudioType(audioUrl);

        string separator =
            audioUrl.Contains("?") ? "&" : "?";

        // Prevent Unity from playing an old cached file.
        string noCacheUrl =
            audioUrl +
            separator +
            "t=" +
            DateTime.UtcNow.Ticks;

        using (UnityWebRequest request =
               UnityWebRequestMultimedia.GetAudioClip(
                   noCacheUrl,
                   audioType
               ))
        {
            request.timeout = requestTimeoutSeconds;

            yield return request.SendWebRequest();

            if (request.result !=
                UnityWebRequest.Result.Success)
            {
                LogNetworkError("Audio download", request);
                yield break;
            }

            AudioClip clip =
                DownloadHandlerAudioClip.GetContent(request);

            if (clip == null)
            {
                Debug.LogError(
                    "[GameBridge] Piper returned an invalid audio file."
                );
                yield break;
            }

            if (stopPreviousCommentary &&
                commentaryAudioSource.isPlaying)
            {
                commentaryAudioSource.Stop();
            }

            commentaryAudioSource.volume =
                commentatorVolume;

            commentaryAudioSource.clip = clip;
            commentaryAudioSource.Play();
        }
    }

    private AudioType DetermineAudioType(string url)
    {
        string lower = url.ToLowerInvariant();

        if (lower.Contains(".mp3"))
            return AudioType.MPEG;

        if (lower.Contains(".ogg"))
            return AudioType.OGGVORBIS;

        return AudioType.WAV;
    }

    // ============================================================
    // Caption UI
    // ============================================================

    private void BuildCaptionUI()
    {
        GameObject canvasObject =
            new GameObject("AICommentaryCanvas");

        canvasObject.transform.SetParent(
            transform,
            false
        );

        captionCanvas =
            canvasObject.AddComponent<Canvas>();

        captionCanvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        captionCanvas.sortingOrder = 500;

        CanvasScaler scaler =
            canvasObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution =
            new Vector2(1920f, 1080f);

        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject backgroundObject =
            new GameObject("Background");

        backgroundObject.transform.SetParent(
            canvasObject.transform,
            false
        );

        captionBackground =
            backgroundObject.AddComponent<Image>();

        captionBackground.color =
            new Color(0.015f, 0.02f, 0.04f, 0.88f);

        RectTransform backgroundRect =
            backgroundObject.GetComponent<RectTransform>();

        backgroundRect.anchorMin =
            new Vector2(0.15f, 0.04f);

        backgroundRect.anchorMax =
            new Vector2(0.85f, 0.22f);

        backgroundRect.offsetMin =
            Vector2.zero;

        backgroundRect.offsetMax =
            Vector2.zero;

        GameObject speakerObject =
            new GameObject("Speaker");

        speakerObject.transform.SetParent(
            backgroundObject.transform,
            false
        );

        captionSpeaker =
            speakerObject.AddComponent<Text>();

        captionSpeaker.font =
            Font.CreateDynamicFontFromOSFont(
                "Arial",
                30
            );

        captionSpeaker.fontSize = 26;
        captionSpeaker.fontStyle = FontStyle.Bold;
        captionSpeaker.alignment =
            TextAnchor.MiddleCenter;

        captionSpeaker.color =
            new Color(0.3f, 0.9f, 1f);

        captionSpeaker.text = commentatorName;

        RectTransform speakerRect =
            speakerObject.GetComponent<RectTransform>();

        speakerRect.anchorMin =
            new Vector2(0.04f, 0.66f);

        speakerRect.anchorMax =
            new Vector2(0.96f, 0.96f);

        speakerRect.offsetMin =
            Vector2.zero;

        speakerRect.offsetMax =
            Vector2.zero;

        GameObject textObject =
            new GameObject("CommentaryText");

        textObject.transform.SetParent(
            backgroundObject.transform,
            false
        );

        captionText =
            textObject.AddComponent<Text>();

        captionText.font =
            Font.CreateDynamicFontFromOSFont(
                "Arial",
                36
            );

        captionText.fontSize = 34;
        captionText.alignment =
            TextAnchor.MiddleCenter;

        captionText.color = Color.white;
        captionText.horizontalOverflow =
            HorizontalWrapMode.Wrap;

        captionText.verticalOverflow =
            VerticalWrapMode.Truncate;

        RectTransform textRect =
            textObject.GetComponent<RectTransform>();

        textRect.anchorMin =
            new Vector2(0.04f, 0.08f);

        textRect.anchorMax =
            new Vector2(0.96f, 0.7f);

        textRect.offsetMin =
            Vector2.zero;

        textRect.offsetMax =
            Vector2.zero;

        captionCanvas.gameObject.SetActive(false);
    }

    private void ShowCommentaryCaption(string line)
    {
        if (!showCaptions ||
            captionCanvas == null ||
            string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        if (captionCoroutine != null)
            StopCoroutine(captionCoroutine);

        captionCoroutine =
            StartCoroutine(DisplayCaption(line));
    }

    private IEnumerator DisplayCaption(string line)
    {
        captionCanvas.gameObject.SetActive(true);

        captionSpeaker.text = commentatorName;
        captionText.text = "";

        float characterDelay = 0.015f;

        for (int index = 0;
             index < line.Length;
             index++)
        {
            captionText.text =
                line.Substring(0, index + 1);

            yield return new WaitForSecondsRealtime(
                characterDelay
            );
        }

        float duration =
            Mathf.Max(
                minimumCaptionTime,
                line.Length * captionTimePerCharacter
            );

        yield return new WaitForSecondsRealtime(duration);

        captionCanvas.gameObject.SetActive(false);
        captionCoroutine = null;
    }

    // ============================================================
    // Reflection helpers
    // ============================================================

    private object ReadMember(
        object target,
        params string[] memberNames
    )
    {
        if (target == null)
            return null;

        Type type = target.GetType();

        BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.IgnoreCase;

        foreach (string memberName in memberNames)
        {
            FieldInfo field =
                type.GetField(memberName, flags);

            if (field != null)
            {
                try
                {
                    return field.GetValue(target);
                }
                catch
                {
                    // Try the next possible member.
                }
            }

            PropertyInfo property =
                type.GetProperty(memberName, flags);

            if (property != null &&
                property.CanRead &&
                property.GetIndexParameters().Length == 0)
            {
                try
                {
                    return property.GetValue(target, null);
                }
                catch
                {
                    // Try the next possible member.
                }
            }
        }

        return null;
    }

    private float ReadFloat(
        object target,
        float fallback,
        params string[] memberNames
    )
    {
        object value =
            ReadMember(target, memberNames);

        if (value == null)
            return fallback;

        try
        {
            return Convert.ToSingle(value);
        }
        catch
        {
            return fallback;
        }
    }

    private int ReadInt(
        object target,
        int fallback,
        params string[] memberNames
    )
    {
        object value =
            ReadMember(target, memberNames);

        if (value == null)
            return fallback;

        try
        {
            return Convert.ToInt32(value);
        }
        catch
        {
            return fallback;
        }
    }

    private string ReadString(
        object target,
        string fallback,
        params string[] memberNames
    )
    {
        object value =
            ReadMember(target, memberNames);

        if (value == null)
            return fallback;

        if (value is UnityEngine.Object unityObject)
            return unityObject.name;

        string result = value.ToString();

        return string.IsNullOrWhiteSpace(result)
            ? fallback
            : result;
    }

    // ============================================================
    // Utility methods
    // ============================================================

    private string BuildUrl(string endpoint)
    {
        string root =
            serverBaseUrl.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(endpoint))
            return root;

        if (!endpoint.StartsWith("/"))
            endpoint = "/" + endpoint;

        return root + endpoint;
    }

    private string MakeAbsoluteAudioUrl(string audioLocation)
    {
        if (audioLocation.StartsWith(
                "http://",
                StringComparison.OrdinalIgnoreCase
            ) ||
            audioLocation.StartsWith(
                "https://",
                StringComparison.OrdinalIgnoreCase
            ))
        {
            return audioLocation;
        }

        return BuildUrl(audioLocation);
    }

    private string FirstNonEmpty(
        params string[] values
    )
    {
        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return "";
    }

    private void LogNetworkError(
        string operation,
        UnityWebRequest request
    )
    {
        if (!showNetworkLogs)
            return;

        if (Time.realtimeSinceStartup <
            nextNetworkErrorLogTime)
        {
            return;
        }

        nextNetworkErrorLogTime =
            Time.realtimeSinceStartup + 3f;

        Debug.LogWarning(
            "[GameBridge] " +
            operation +
            " failed. URL: " +
            request.url +
            " | Error: " +
            request.error +
            " | Response: " +
            request.downloadHandler?.text
        );
    }

    private static float Round(float value)
    {
        return Mathf.Round(value * 100f) / 100f;
    }

    // ============================================================
    // Testing controls
    // ============================================================

    [ContextMenu("Test Local AI Connection")]
    public void TestLocalAIConnection()
    {
        StartCoroutine(CheckServerHealth());
    }

    [ContextMenu("Test Headshot Commentary")]
    public void TestHeadshotCommentary()
    {
        ReportHeadshot(
            trackedWeapon,
            75f,
            "alien"
        );
    }

    [ContextMenu("Test Boss Commentary")]
    public void TestBossCommentary()
    {
        ReportGameplayEvent(
            "boss_battle",
            "The player is currently fighting the boss."
        );
    }

    [ContextMenu("Print Current Game State")]
    public void PrintCurrentGameState()
    {
        CurrentState = CollectCurrentGameState();

        Debug.Log(
            "[GameBridge] Current state:\n" +
            JsonUtility.ToJson(CurrentState, true)
        );
    }
}