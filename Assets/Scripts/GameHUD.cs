using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class GameHUD : MonoBehaviour
{
    [Header("Score")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI waveText;
    public TextMeshProUGUI killText;

    [Header("Health Bar")]
    public Image healthBarFill;
    public Image healthBarBackground;
    public Image healthBarGlow;

    [Header("Screens")]
    public GameObject startScreen;
    public TextMeshProUGUI startScreenText;
    public GameObject gameOverScreen;
    public TextMeshProUGUI gameOverText;

    [Header("Wave Announcement")]
    public TextMeshProUGUI waveAnnouncementText;
    public float announcementDuration = 2.5f;

    [Header("Weapon Info")]
    public TextMeshProUGUI weaponText;
    public TextMeshProUGUI ammoText;

    [Header("Gem Hunt")]
    public TextMeshProUGUI gemText;
    public GameObject victoryScreen;
    public TextMeshProUGUI victoryText;

    [Header("Follow Camera")]
    public float followDistance = 2.5f;
    public float followHeight = 0.3f;
    public float followSmoothing = 3f;

    private Canvas hudCanvas;
    private Transform cameraTransform;
    private bool isInitialized = false;
    private int displayScore = 0;
    private int targetScore = 0;
    private float scoreAnimTimer = 0f;

    // Color palette
    private Color cyanGlow = new Color(0f, 0.95f, 1f);
    private Color goldGlow = new Color(1f, 0.85f, 0.2f);
    private Color redGlow = new Color(1f, 0.25f, 0.15f);
    private Color greenGlow = new Color(0.15f, 1f, 0.5f);
    private Color purpleGlow = new Color(0.7f, 0.2f, 1f);
    private Color orangeGlow = new Color(1f, 0.55f, 0.1f);

    void Start()
    {
        cameraTransform = Camera.main?.transform;

        if (scoreText == null)
        {
            CreateHUDElements();
        }

        isInitialized = true;
    }

    void CreateHUDElements()
    {
        // Create world-space canvas
        hudCanvas = GetComponent<Canvas>();
        if (hudCanvas == null)
        {
            hudCanvas = gameObject.AddComponent<Canvas>();
        }
        hudCanvas.renderMode = RenderMode.WorldSpace;

        RectTransform canvasRect = hudCanvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(900, 550);
        canvasRect.localScale = Vector3.one * 0.002f;

        gameObject.AddComponent<GraphicRaycaster>();

        // ===== BACKGROUND PANEL (dark glass) =====
        GameObject panelGO = new GameObject("BackgroundPanel");
        panelGO.transform.SetParent(transform, false);
        RectTransform panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.anchoredPosition = new Vector2(0, 210);
        panelRect.sizeDelta = new Vector2(860, 80);
        Image panelImg = panelGO.AddComponent<Image>();
        panelImg.color = new Color(0.02f, 0.02f, 0.08f, 0.6f);
        panelImg.raycastTarget = false;

        // ===== SCORE TEXT (Top Center — Cyan Glow) =====
        GameObject scoreGO = CreateTextElement("ScoreText", "SCORE: 0",
            new Vector2(0, 220), 42, TextAlignmentOptions.Center, cyanGlow);
        scoreText = scoreGO.GetComponent<TextMeshProUGUI>();
        // Add glow outline
        scoreText.outlineWidth = 0.15f;
        scoreText.outlineColor = new Color(0f, 0.4f, 0.5f, 0.6f);

        // ===== WAVE TEXT (Top Left — Gold) =====
        GameObject waveGO = CreateTextElement("WaveText", "WAVE 1",
            new Vector2(-340, 220), 32, TextAlignmentOptions.Left, goldGlow);
        waveText = waveGO.GetComponent<TextMeshProUGUI>();
        waveText.outlineWidth = 0.12f;
        waveText.outlineColor = new Color(0.5f, 0.35f, 0f, 0.5f);

        // ===== KILL COUNT (Top Right — Red/Orange) =====
        GameObject killGO = CreateTextElement("KillText", "💀 KILLS: 0",
            new Vector2(340, 220), 28, TextAlignmentOptions.Right, orangeGlow);
        killText = killGO.GetComponent<TextMeshProUGUI>();
        killText.outlineWidth = 0.1f;
        killText.outlineColor = new Color(0.5f, 0.15f, 0f, 0.5f);

        // ===== HEALTH BAR (Bottom — Gradient) =====
        CreateHealthBar();

        // ===== WEAPON INFO (Bottom Right) =====
        GameObject weaponGO = CreateTextElement("WeaponText", "",
            new Vector2(280, -170), 22, TextAlignmentOptions.Right, purpleGlow);
        weaponText = weaponGO.GetComponent<TextMeshProUGUI>();

        GameObject ammoGO = CreateTextElement("AmmoText", "",
            new Vector2(280, -195), 20, TextAlignmentOptions.Right, goldGlow);
        ammoText = ammoGO.GetComponent<TextMeshProUGUI>();

        // ===== WAVE ANNOUNCEMENT (Center — Large) =====
        GameObject announceGO = CreateTextElement("WaveAnnouncement", "",
            new Vector2(0, 50), 60, TextAlignmentOptions.Center, goldGlow);
        waveAnnouncementText = announceGO.GetComponent<TextMeshProUGUI>();
        waveAnnouncementText.outlineWidth = 0.2f;
        waveAnnouncementText.outlineColor = new Color(0.5f, 0.3f, 0f, 0.7f);
        announceGO.SetActive(false);

        // ===== GEM COUNTER (Below wave text — Cyan diamond) =====
        GameObject gemGO = CreateTextElement("GemText", "💎 GEMS: 0/4",
            new Vector2(-340, 185), 26, TextAlignmentOptions.Left, new Color(0.3f, 0.85f, 1f));
        gemText = gemGO.GetComponent<TextMeshProUGUI>();
        gemText.outlineWidth = 0.1f;
        gemText.outlineColor = new Color(0f, 0.3f, 0.45f, 0.6f);

        // ===== START SCREEN =====
        CreateStartScreen();

        // ===== GAME OVER SCREEN =====
        CreateGameOverScreen();

        // ===== VICTORY SCREEN =====
        CreateVictoryScreen();

        // ===== CROSSHAIR =====
        CreateCrosshair();
    }

    void CreateStartScreen()
    {
        startScreen = new GameObject("StartScreen");
        startScreen.transform.SetParent(transform, false);
        RectTransform startRect = startScreen.AddComponent<RectTransform>();
        startRect.anchoredPosition = Vector2.zero;
        startRect.sizeDelta = new Vector2(700, 400);

        // Dark gradient background
        Image startBG = startScreen.AddComponent<Image>();
        startBG.color = new Color(0.01f, 0.01f, 0.05f, 0.85f);
        startBG.raycastTarget = false;

        // Border glow
        GameObject borderGO = new GameObject("Border");
        borderGO.transform.SetParent(startScreen.transform, false);
        RectTransform borderRect = borderGO.AddComponent<RectTransform>();
        borderRect.anchoredPosition = Vector2.zero;
        borderRect.sizeDelta = new Vector2(704, 404);
        Image borderImg = borderGO.AddComponent<Image>();
        borderImg.color = new Color(0f, 0.8f, 1f, 0.4f);
        borderImg.raycastTarget = false;
        borderGO.transform.SetAsFirstSibling();

        // Title
        string titleText = "⚡ ALIEN SHOOTER ⚡\n\n" +
            "<size=26><color=#FFD700>Press Trigger to Start</color></size>\n" +
            "<size=20><color=#888888>Grip to Reset at any time</color></size>";
        GameObject startTextGO = CreateTextElement("StartText", titleText,
            Vector2.zero, 52, TextAlignmentOptions.Center, cyanGlow);
        startTextGO.transform.SetParent(startScreen.transform, false);
        startScreenText = startTextGO.GetComponent<TextMeshProUGUI>();
        startScreenText.outlineWidth = 0.15f;
        startScreenText.outlineColor = new Color(0f, 0.3f, 0.5f, 0.5f);
    }

    void CreateGameOverScreen()
    {
        gameOverScreen = new GameObject("GameOverScreen");
        gameOverScreen.transform.SetParent(transform, false);
        RectTransform goRect = gameOverScreen.AddComponent<RectTransform>();
        goRect.anchoredPosition = Vector2.zero;
        goRect.sizeDelta = new Vector2(700, 450);

        // Dark red gradient
        Image goBG = gameOverScreen.AddComponent<Image>();
        goBG.color = new Color(0.15f, 0.01f, 0.01f, 0.9f);
        goBG.raycastTarget = false;

        // Red border
        GameObject borderGO = new GameObject("Border");
        borderGO.transform.SetParent(gameOverScreen.transform, false);
        RectTransform borderRect = borderGO.AddComponent<RectTransform>();
        borderRect.anchoredPosition = Vector2.zero;
        borderRect.sizeDelta = new Vector2(704, 454);
        Image borderImg = borderGO.AddComponent<Image>();
        borderImg.color = new Color(1f, 0.1f, 0.1f, 0.5f);
        borderImg.raycastTarget = false;
        borderGO.transform.SetAsFirstSibling();

        GameObject goTextGO = CreateTextElement("GameOverText", "GAME OVER",
            Vector2.zero, 56, TextAlignmentOptions.Center, redGlow);
        goTextGO.transform.SetParent(gameOverScreen.transform, false);
        gameOverText = goTextGO.GetComponent<TextMeshProUGUI>();
        gameOverText.outlineWidth = 0.2f;
        gameOverText.outlineColor = new Color(0.5f, 0f, 0f, 0.7f);

        gameOverScreen.SetActive(false);
    }

    void CreateVictoryScreen()
    {
        victoryScreen = new GameObject("VictoryScreen");
        victoryScreen.transform.SetParent(transform, false);
        RectTransform vRect = victoryScreen.AddComponent<RectTransform>();
        vRect.anchoredPosition = Vector2.zero;
        vRect.sizeDelta = new Vector2(700, 450);

        // Deep green-gold celebratory background
        Image vBG = victoryScreen.AddComponent<Image>();
        vBG.color = new Color(0.01f, 0.08f, 0.03f, 0.92f);
        vBG.raycastTarget = false;

        // Gold border
        GameObject borderGO = new GameObject("Border");
        borderGO.transform.SetParent(victoryScreen.transform, false);
        RectTransform borderRect = borderGO.AddComponent<RectTransform>();
        borderRect.anchoredPosition = Vector2.zero;
        borderRect.sizeDelta = new Vector2(704, 454);
        Image borderImg = borderGO.AddComponent<Image>();
        borderImg.color = new Color(1f, 0.85f, 0.2f, 0.5f);
        borderImg.raycastTarget = false;
        borderGO.transform.SetAsFirstSibling();

        GameObject vTextGO = CreateTextElement("VictoryText", "VICTORY",
            Vector2.zero, 52, TextAlignmentOptions.Center, goldGlow);
        vTextGO.transform.SetParent(victoryScreen.transform, false);
        victoryText = vTextGO.GetComponent<TextMeshProUGUI>();
        victoryText.outlineWidth = 0.2f;
        victoryText.outlineColor = new Color(0.5f, 0.4f, 0f, 0.7f);

        victoryScreen.SetActive(false);
    }

    GameObject CreateTextElement(string name, string text, Vector2 position, float fontSize,
        TextAlignmentOptions alignment, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(700, 100);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.fontStyle = FontStyles.Bold;
        tmp.raycastTarget = false;
        tmp.enableAutoSizing = false;
        tmp.overflowMode = TextOverflowModes.Overflow;

        return go;
    }

    void CreateHealthBar()
    {
        // Bottom panel background
        GameObject bottomPanel = new GameObject("BottomPanel");
        bottomPanel.transform.SetParent(transform, false);
        RectTransform bpRect = bottomPanel.AddComponent<RectTransform>();
        bpRect.anchoredPosition = new Vector2(0, -210);
        bpRect.sizeDelta = new Vector2(500, 40);
        Image bpImg = bottomPanel.AddComponent<Image>();
        bpImg.color = new Color(0.02f, 0.02f, 0.06f, 0.6f);
        bpImg.raycastTarget = false;

        // Health bar background
        GameObject bgGO = new GameObject("HealthBarBG");
        bgGO.transform.SetParent(bottomPanel.transform, false);
        RectTransform bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchoredPosition = new Vector2(20, 0);
        bgRect.sizeDelta = new Vector2(400, 22);

        healthBarBackground = bgGO.AddComponent<Image>();
        healthBarBackground.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);
        healthBarBackground.raycastTarget = false;

        // Health bar outer glow
        GameObject glowGO = new GameObject("HealthBarGlow");
        glowGO.transform.SetParent(bgGO.transform, false);
        RectTransform glowRect = glowGO.AddComponent<RectTransform>();
        glowRect.anchorMin = new Vector2(0, 0);
        glowRect.anchorMax = new Vector2(1, 1);
        glowRect.offsetMin = new Vector2(-3, -3);
        glowRect.offsetMax = new Vector2(3, 3);
        healthBarGlow = glowGO.AddComponent<Image>();
        healthBarGlow.color = new Color(0f, 1f, 0.4f, 0.2f);
        healthBarGlow.raycastTarget = false;
        glowGO.transform.SetAsFirstSibling();

        // Health bar fill
        GameObject fillGO = new GameObject("HealthBarFill");
        fillGO.transform.SetParent(bgGO.transform, false);
        RectTransform fillRect = fillGO.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0, 0);
        fillRect.anchorMax = new Vector2(1, 1);
        fillRect.offsetMin = new Vector2(2, 2);
        fillRect.offsetMax = new Vector2(-2, -2);

        healthBarFill = fillGO.AddComponent<Image>();
        healthBarFill.color = greenGlow;
        healthBarFill.type = Image.Type.Filled;
        healthBarFill.fillMethod = Image.FillMethod.Horizontal;
        healthBarFill.fillAmount = 1f;
        healthBarFill.raycastTarget = false;

        // Health label
        CreateTextElement("HealthLabel", "❤ HP",
            new Vector2(-230, -210), 20, TextAlignmentOptions.Right, redGlow);
    }

    void CreateCrosshair()
    {
        GameObject crosshairGO = new GameObject("Crosshair");
        crosshairGO.transform.SetParent(transform, false);

        RectTransform crossRect = crosshairGO.AddComponent<RectTransform>();
        crossRect.anchoredPosition = Vector2.zero;
        crossRect.sizeDelta = new Vector2(16, 16);

        Image crosshairImage = crosshairGO.AddComponent<Image>();
        crosshairImage.color = new Color(0f, 1f, 1f, 0.7f);
        crosshairImage.raycastTarget = false;

        Texture2D dotTex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[32 * 32];
        Vector2 center = new Vector2(16, 16);

        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist < 4f)
                    pixels[y * 32 + x] = Color.white;
                else if (dist < 6f)
                    pixels[y * 32 + x] = new Color(0f, 1f, 1f, 0.8f);
                else if (dist < 14f && dist > 12f)
                    pixels[y * 32 + x] = new Color(0f, 1f, 1f, 0.3f);
                else
                    pixels[y * 32 + x] = Color.clear;
            }
        }

        dotTex.SetPixels(pixels);
        dotTex.Apply();

        Sprite dotSprite = Sprite.Create(dotTex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
        crosshairImage.sprite = dotSprite;
    }

    void LateUpdate()
    {
        if (!isInitialized || cameraTransform == null) return;

        Vector3 targetPos = cameraTransform.position + cameraTransform.forward * followDistance;
        targetPos.y = cameraTransform.position.y + followHeight;

        transform.position = Vector3.Lerp(transform.position, targetPos, followSmoothing * Time.deltaTime);
        transform.rotation = Quaternion.LookRotation(transform.position - cameraTransform.position);

        // Keep the scoreboard continuously in sync with live game state
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.score != targetScore)
                UpdateScore(GameManager.Instance.score);

            UpdateKillCount(GameManager.Instance.killsThisWave, GameManager.Instance.totalKills);

            int liveWave = Mathf.Max(1, GameManager.Instance.currentWave);
            string waveStr = "⚔ WAVE " + liveWave;
            if (waveText != null && waveText.text != waveStr)
                waveText.text = waveStr;
        }

        // Animate score counting
        if (displayScore != targetScore)
        {
            scoreAnimTimer += Time.deltaTime * 5f;
            displayScore = (int)Mathf.Lerp(displayScore, targetScore, Mathf.Clamp01(scoreAnimTimer));
            if (Mathf.Abs(displayScore - targetScore) <= 1)
                displayScore = targetScore;
            if (scoreText != null)
                scoreText.text = "SCORE: " + displayScore;
        }

        // Pulse health bar glow
        if (healthBarGlow != null && healthBarFill != null)
        {
            float glowPulse = (Mathf.Sin(Time.time * 3f) + 1f) * 0.5f;
            Color glowColor = healthBarFill.color;
            glowColor.a = 0.15f + glowPulse * 0.15f;
            healthBarGlow.color = glowColor;
        }
    }

    // ===== Public API =====

    public void UpdateScore(int newScore)
    {
        targetScore = newScore;
        scoreAnimTimer = 0f;
    }

    public void UpdateWave(int wave)
    {
        if (waveText != null)
        {
            waveText.text = "⚔ WAVE " + wave;
            // Flash gold on wave change
            StartCoroutine(FlashText(waveText, goldGlow));
        }
    }

    public void UpdateKillCount(int kills, int total)
    {
        if (killText != null)
            killText.text = $"💀 KILLS: {kills}/{total}";
    }

    public void UpdateHealth(float percentage)
    {
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = percentage;

            if (percentage > 0.6f)
                healthBarFill.color = Color.Lerp(greenGlow, cyanGlow, (percentage - 0.6f) * 2.5f);
            else if (percentage > 0.3f)
                healthBarFill.color = Color.Lerp(orangeGlow, greenGlow, (percentage - 0.3f) * 3.3f);
            else
                healthBarFill.color = Color.Lerp(redGlow, orangeGlow, percentage * 3.3f);

            // Shake health bar when low
            if (percentage < 0.25f && healthBarFill.transform.parent != null)
            {
                float shake = Mathf.Sin(Time.time * 20f) * 2f * (0.25f - percentage) * 4f;
                healthBarFill.transform.parent.GetComponent<RectTransform>().anchoredPosition =
                    new Vector2(20 + shake, 0);
            }
        }
    }

    public void UpdateWeaponInfo(string weaponName, int ammoCount)
    {
        if (weaponText != null)
        {
            weaponText.text = string.IsNullOrEmpty(weaponName) ? "" : $"🔫 {weaponName}";
        }
        if (ammoText != null)
        {
            ammoText.text = ammoCount > 0 ? $"AMMO: {ammoCount}" : "";
        }
    }

    public void ShowWaveAnnouncement(int wave)
    {
        if (waveAnnouncementText != null)
        {
            StartCoroutine(WaveAnnouncementRoutine(wave));
        }
    }

    IEnumerator WaveAnnouncementRoutine(int wave)
    {
        if (waveAnnouncementText == null) yield break;

        bool isBoss = (wave == -1 || wave % 5 == 0);
        string waveLabel = wave == -1 ? "⚠ BOSS INCOMING ⚠" : (isBoss ? $"⚠ BOSS WAVE {wave} ⚠" : $"WAVE {wave}");
        Color waveColor = isBoss ? redGlow : goldGlow;

        waveAnnouncementText.gameObject.SetActive(true);
        waveAnnouncementText.text = waveLabel;
        waveAnnouncementText.color = waveColor;
        waveAnnouncementText.outlineColor = isBoss
            ? new Color(0.5f, 0f, 0f, 0.7f)
            : new Color(0.5f, 0.3f, 0f, 0.7f);

        // Scale in with elastic bounce
        RectTransform rect = waveAnnouncementText.GetComponent<RectTransform>();
        float timer = 0f;
        float scaleInDuration = 0.4f;

        while (timer < scaleInDuration)
        {
            timer += Time.deltaTime;
            float t = timer / scaleInDuration;
            float elastic = 1f - Mathf.Pow(2f, -10f * t) * Mathf.Cos(t * Mathf.PI * 2.5f);
            rect.localScale = Vector3.one * Mathf.Clamp01(elastic) * 1.1f;

            // Color cycle during entrance
            if (!isBoss)
            {
                float hue = Mathf.Repeat(Time.time * 0.5f, 1f);
                waveAnnouncementText.color = Color.HSVToRGB(hue, 0.7f, 1f);
            }

            yield return null;
        }

        rect.localScale = Vector3.one;
        waveAnnouncementText.color = waveColor;

        yield return new WaitForSeconds(announcementDuration - scaleInDuration - 0.4f);

        // Fade out
        timer = 0f;
        Color startColor = waveAnnouncementText.color;
        while (timer < 0.4f)
        {
            timer += Time.deltaTime;
            float t = timer / 0.4f;
            waveAnnouncementText.color = new Color(startColor.r, startColor.g, startColor.b, 1f - t);
            rect.localScale = Vector3.one * (1f + t * 0.4f);
            yield return null;
        }

        waveAnnouncementText.gameObject.SetActive(false);
        rect.localScale = Vector3.one;
    }

    public void ShowWaveComplete(int wave)
    {
        if (waveAnnouncementText != null)
        {
            StartCoroutine(ShowTemporaryMessage($"✅ WAVE {wave} COMPLETE!", greenGlow, 2f));
        }
    }

    IEnumerator ShowTemporaryMessage(string message, Color color, float duration)
    {
        if (waveAnnouncementText == null) yield break;

        waveAnnouncementText.gameObject.SetActive(true);
        waveAnnouncementText.text = message;
        waveAnnouncementText.color = color;

        RectTransform rect = waveAnnouncementText.GetComponent<RectTransform>();
        rect.localScale = Vector3.one;

        yield return new WaitForSeconds(duration);

        waveAnnouncementText.gameObject.SetActive(false);
    }

    IEnumerator FlashText(TextMeshProUGUI text, Color baseColor)
    {
        if (text == null) yield break;

        text.color = Color.white;
        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            text.color = Color.Lerp(Color.white, baseColor, t / 0.3f);
            yield return null;
        }
        text.color = baseColor;
    }

    // ===== Gem Hunt API =====

    /// <summary>Updates the world-space 'Gems: X/4' counter.</summary>
    public void UpdateGemCount(int collected, int total)
    {
        if (gemText != null)
        {
            gemText.text = $"💎 GEMS: {collected}/{total}";
            StartCoroutine(FlashText(gemText, new Color(0.3f, 0.85f, 1f)));
        }
    }

    /// <summary>Brief center-screen message after collecting a gem (darkness warning).</summary>
    public void ShowGemMessage(int collected, int total)
    {
        if (waveAnnouncementText != null)
        {
            StartCoroutine(ShowTemporaryMessage(
                $"💎 GEM {collected}/{total} — THE DARKNESS GROWS…",
                new Color(0.4f, 0.8f, 1f), 2.2f));
        }
    }

    /// <summary>Victory screen with run time and shots fired. Grip restarts.</summary>
    public void ShowVictoryScreen(float timeTakenSeconds, int shotsFired)
    {
        if (victoryScreen == null) return;

        int minutes = Mathf.FloorToInt(timeTakenSeconds / 60f);
        int seconds = Mathf.FloorToInt(timeTakenSeconds % 60f);

        victoryScreen.SetActive(true);
        if (victoryText != null)
        {
            victoryText.text = "🏆 VICTORY! 🏆\n\n" +
                "<size=30><color=#FFD700>Boss Defeated</color></size>\n\n" +
                $"<size=28><color=#00EEFF>Time: {minutes:00}:{seconds:00}</color></size>\n" +
                $"<size=28><color=#FF9944>Shots Fired: {shotsFired}</color></size>\n\n" +
                "<size=20><color=#AAAAAA>Press Grip to Play Again</color></size>";
        }

        if (gameOverScreen != null) gameOverScreen.SetActive(false);
        if (startScreen != null) startScreen.SetActive(false);
    }

    public void HideVictoryScreen()
    {
        if (victoryScreen != null) victoryScreen.SetActive(false);
    }

    public void ShowStartScreen(bool show)
    {
        if (startScreen != null)
            startScreen.SetActive(show);
    }

    public void ShowGameOverScreen(bool show, int finalScore = 0, int wave = 0, int kills = 0)
    {
        if (gameOverScreen != null)
        {
            gameOverScreen.SetActive(show);
            if (show && gameOverText != null)
            {
                gameOverText.text = $"💀 GAME OVER 💀\n\n" +
                    $"<size=32><color=#00EEFF>Score: {finalScore}</color></size>\n" +
                    $"<size=28><color=#FFD700>Wave: {wave}</color></size>\n" +
                    $"<size=28><color=#FF6644>Kills: {kills}</color></size>\n\n" +
                    $"<size=22><color=#AAAAAA>Press Grip to Restart</color></size>";
            }
        }
    }
}
