using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Orchestrates the boss encounter after wave 1.
/// Spawns Vader, activates horror atmosphere, manages boss health bar.
/// </summary>
public class BossWaveManager : MonoBehaviour
{
    public static BossWaveManager Instance { get; private set; }

    [Header("Boss Settings")]
    public float bossSpawnDistance = 6f;
    public float bossSpawnDelay = 2f;

    // Runtime
    private BossEnemy currentBoss;
    private HorrorAtmosphere atmosphere;
    private Canvas bossHealthCanvas;
    private Image healthBarFill;
    private Text bossNameText;
    private bool isBossWaveActive = false;

    // Events
    public System.Action onBossWaveComplete;

    void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// Trigger a boss wave. Call this after wave 1 completes.
    /// </summary>
    public void StartBossWave()
    {
        if (isBossWaveActive) return;
        isBossWaveActive = true;
        StartCoroutine(BossWaveSequence());
    }

    IEnumerator BossWaveSequence()
    {
        // === PHASE 1: HORROR TRANSITION ===
        // Create atmosphere manager
        GameObject atmosGO = new GameObject("HorrorAtmosphere");
        atmosphere = atmosGO.AddComponent<HorrorAtmosphere>();
        atmosphere.Activate();

        // HUD announcement
        if (GameManager.Instance != null && GameManager.Instance.gameHUD != null)
        {
            GameManager.Instance.gameHUD.ShowWaveAnnouncement(-1); // -1 = boss wave
        }

        yield return new WaitForSeconds(bossSpawnDelay);

        // === PHASE 2: SPAWN BOSS ===
        Vector3 spawnPos = GetBossSpawnPosition();

        GameObject bossGO = new GameObject("Boss_DarthVader");
        bossGO.transform.position = spawnPos;

        // Face the player
        if (Camera.main != null)
        {
            Vector3 dir = Camera.main.transform.position - spawnPos;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                bossGO.transform.rotation = Quaternion.LookRotation(dir);
        }

        currentBoss = bossGO.AddComponent<BossEnemy>();
        currentBoss.onBossDefeated = OnBossDefeated;

        // Wait a frame for Start() to run
        yield return null;

        // Activate boss
        currentBoss.ActivateBoss();

        // === PHASE 3: BOSS HEALTH BAR ===
        CreateBossHealthBar();

        // Monitor boss health
        while (currentBoss != null && !currentBoss.Equals(null))
        {
            UpdateBossHealthBar();
            yield return null;
        }
    }

    Vector3 GetBossSpawnPosition()
    {
        // Spawn far in front of the player
        Vector3 spawnPos = Vector3.zero;
        if (Camera.main != null)
        {
            Vector3 forward = Camera.main.transform.forward;
            forward.y = 0f;
            forward.Normalize();
            spawnPos = Camera.main.transform.position + forward * bossSpawnDistance;
            spawnPos.y = Camera.main.transform.position.y - 1.5f; // Floor level
        }
        return spawnPos;
    }

    void CreateBossHealthBar()
    {
        // World-space health bar at the top of the player's view
        GameObject canvasGO = new GameObject("BossHealthBarCanvas");
        bossHealthCanvas = canvasGO.AddComponent<Canvas>();
        bossHealthCanvas.renderMode = RenderMode.WorldSpace;
        bossHealthCanvas.sortingOrder = 90;

        RectTransform canvasRect = bossHealthCanvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(400f, 50f);
        canvasRect.localScale = Vector3.one * 0.002f;

        // Background
        GameObject bgGO = new GameObject("HealthBG");
        bgGO.transform.SetParent(canvasGO.transform, false);
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.15f, 0f, 0f, 0.8f);
        RectTransform bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;

        // Health fill
        GameObject fillGO = new GameObject("HealthFill");
        fillGO.transform.SetParent(bgGO.transform, false);
        healthBarFill = fillGO.AddComponent<Image>();
        healthBarFill.color = new Color(0.9f, 0.1f, 0f);
        RectTransform fillRect = fillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0.01f, 0.15f);
        fillRect.anchorMax = new Vector2(0.99f, 0.85f);
        fillRect.offsetMin = Vector2.zero; fillRect.offsetMax = Vector2.zero;

        // Boss name
        GameObject nameGO = new GameObject("BossName");
        nameGO.transform.SetParent(bgGO.transform, false);
        bossNameText = nameGO.AddComponent<Text>();
        bossNameText.font = Font.CreateDynamicFontFromOSFont("Arial", 18);
        bossNameText.fontSize = 18;
        bossNameText.text = "◆ THE OVERLORD ◆";
        bossNameText.color = new Color(1f, 0.8f, 0.6f);
        bossNameText.alignment = TextAnchor.MiddleCenter;
        RectTransform nameRect = nameGO.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 1f); nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0.5f, 0f);
        nameRect.sizeDelta = new Vector2(0f, 25f);
        nameRect.anchoredPosition = new Vector2(0f, 2f);

        // Outline
        Outline outline = bgGO.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.2f, 0f, 0.6f);
        outline.effectDistance = new Vector2(2f, 2f);
    }

    void UpdateBossHealthBar()
    {
        if (bossHealthCanvas == null || Camera.main == null) return;

        // Position above player view
        Vector3 pos = Camera.main.transform.position
            + Camera.main.transform.forward * 2f
            + Camera.main.transform.up * 0.8f;
        bossHealthCanvas.transform.position = pos;
        bossHealthCanvas.transform.LookAt(
            bossHealthCanvas.transform.position + Camera.main.transform.forward
        );

        // Update fill
        if (healthBarFill != null && currentBoss != null)
        {
            float pct = currentBoss.GetHealthPercent();
            RectTransform fillRect = healthBarFill.GetComponent<RectTransform>();
            fillRect.anchorMax = new Vector2(0.01f + 0.98f * pct, 0.85f);

            // Color gradient: red → orange → green
            healthBarFill.color = pct > 0.5f
                ? Color.Lerp(new Color(1f, 0.5f, 0f), new Color(0.9f, 0.1f, 0f), (1f - pct) * 2f)
                : Color.Lerp(new Color(0.5f, 0f, 0f), new Color(1f, 0.5f, 0f), pct * 2f);
        }
    }

    void OnBossDefeated()
    {
        isBossWaveActive = false;

        // Deactivate horror atmosphere
        if (atmosphere != null)
        {
            atmosphere.Deactivate();
            Destroy(atmosphere.gameObject, 4f);
        }

        // Remove health bar
        if (bossHealthCanvas != null)
            Destroy(bossHealthCanvas.gameObject, 1f);

        // Notify game manager
        onBossWaveComplete?.Invoke();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
