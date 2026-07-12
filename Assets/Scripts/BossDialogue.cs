using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Boss dialogue system — speech bubbles above the boss.
/// Placeholder for Qualcomm AI Cloud LLM integration.
/// Shows typewriter-style text based on player stats.
/// </summary>
public class BossDialogue : MonoBehaviour
{
    private Canvas dialogueCanvas;
    private Text dialogueText;
    private GameObject dialoguePanel;
    private Coroutine typewriterCoroutine;

    // Camera-locked subtitle caption (always readable, even if the boss is off-screen)
    private Canvas captionCanvas;
    private Text captionText;

    [Header("Settings")]
    public float typewriterSpeed = 0.04f;
    public float dialogueHeight = 3f;

    void Start()
    {
        CreateDialogueUI();
        CreateCaptionUI();
    }

    void CreateCaptionUI()
    {
        GameObject cGO = new GameObject("BossCaptionCanvas");
        captionCanvas = cGO.AddComponent<Canvas>();
        captionCanvas.renderMode = RenderMode.WorldSpace;
        captionCanvas.sortingOrder = 110;

        RectTransform cRect = captionCanvas.GetComponent<RectTransform>();
        cRect.sizeDelta = new Vector2(1200f, 220f);
        captionCanvas.transform.localScale = Vector3.one * 0.0016f;

        // Semi-transparent letterbox background
        GameObject bg = new GameObject("CaptionBG");
        bg.transform.SetParent(cGO.transform, false);
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.55f);
        RectTransform bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;

        // Speaker label
        GameObject spkGO = new GameObject("CaptionSpeaker");
        spkGO.transform.SetParent(bg.transform, false);
        Text spk = spkGO.AddComponent<Text>();
        spk.font = Font.CreateDynamicFontFromOSFont("Arial", 22);
        spk.fontSize = 22;
        spk.color = new Color(1f, 0.3f, 0.2f);
        spk.text = "THE OVERLORD";
        spk.alignment = TextAnchor.UpperCenter;
        RectTransform spkRect = spk.GetComponent<RectTransform>();
        spkRect.anchorMin = new Vector2(0f, 1f); spkRect.anchorMax = new Vector2(1f, 1f);
        spkRect.pivot = new Vector2(0.5f, 0f);
        spkRect.sizeDelta = new Vector2(0f, 30f);
        spkRect.anchoredPosition = new Vector2(0f, 4f);

        // Caption text
        GameObject tGO = new GameObject("CaptionText");
        tGO.transform.SetParent(bg.transform, false);
        captionText = tGO.AddComponent<Text>();
        captionText.font = Font.CreateDynamicFontFromOSFont("Arial", 40);
        captionText.fontSize = 40;
        captionText.color = Color.white;
        captionText.alignment = TextAnchor.MiddleCenter;
        captionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        captionText.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform tRect = captionText.GetComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0.03f, 0.05f); tRect.anchorMax = new Vector2(0.97f, 0.9f);
        tRect.offsetMin = Vector2.zero; tRect.offsetMax = Vector2.zero;

        captionCanvas.gameObject.SetActive(false);
    }

    void CreateDialogueUI()
    {
        // World-space canvas above boss
        GameObject canvasGO = new GameObject("BossDialogueCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvasGO.transform.localPosition = new Vector3(0f, dialogueHeight, 0f);

        dialogueCanvas = canvasGO.AddComponent<Canvas>();
        dialogueCanvas.renderMode = RenderMode.WorldSpace;
        dialogueCanvas.sortingOrder = 100;

        RectTransform canvasRect = dialogueCanvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(2f, 0.6f);
        canvasRect.localScale = Vector3.one * 0.01f;

        // Dark panel background
        dialoguePanel = new GameObject("DialoguePanel");
        dialoguePanel.transform.SetParent(canvasGO.transform, false);
        Image panelImg = dialoguePanel.AddComponent<Image>();
        panelImg.color = new Color(0.1f, 0f, 0f, 0.85f);

        RectTransform panelRect = dialoguePanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // Red border glow (via outline)
        Outline outline = dialoguePanel.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.2f, 0f, 0.8f);
        outline.effectDistance = new Vector2(2f, 2f);

        // Text
        GameObject textGO = new GameObject("DialogueText");
        textGO.transform.SetParent(dialoguePanel.transform, false);
        dialogueText = textGO.AddComponent<Text>();
        dialogueText.font = Font.CreateDynamicFontFromOSFont("Arial", 24);
        dialogueText.fontSize = 24;
        dialogueText.color = new Color(1f, 0.85f, 0.85f);
        dialogueText.alignment = TextAnchor.MiddleCenter;
        dialogueText.horizontalOverflow = HorizontalWrapMode.Wrap;
        dialogueText.verticalOverflow = VerticalWrapMode.Overflow;
        dialogueText.text = "";

        RectTransform textRect = dialogueText.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.05f, 0.05f);
        textRect.anchorMax = new Vector2(0.95f, 0.95f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        // "AI" label
        GameObject labelGO = new GameObject("AILabel");
        labelGO.transform.SetParent(dialoguePanel.transform, false);
        Text labelText = labelGO.AddComponent<Text>();
        labelText.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        labelText.fontSize = 14;
        labelText.color = new Color(1f, 0.3f, 0f, 0.6f);
        labelText.text = "◆ QUALCOMM AI CLOUD ◆";
        labelText.alignment = TextAnchor.UpperCenter;

        RectTransform labelRect = labelText.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.sizeDelta = new Vector2(0f, 20f);
        labelRect.anchoredPosition = new Vector2(0f, 5f);

        dialoguePanel.SetActive(false);
    }

    /// <summary>
    /// Show a dialogue line with typewriter effect
    /// </summary>
    public void ShowLine(string text)
    {
        if (dialoguePanel != null) dialoguePanel.SetActive(true);
        if (captionCanvas != null) captionCanvas.gameObject.SetActive(true);

        if (typewriterCoroutine != null)
            StopCoroutine(typewriterCoroutine);
        typewriterCoroutine = StartCoroutine(TypewriterEffect(text));
    }

    /// <summary>
    /// Generate a dialogue line based on player stats.
    /// This is the LLM placeholder — in production, this calls Qualcomm AI Cloud.
    /// </summary>
    public string GenerateDialogueLine(int kills, int score, int wave, string context = "")
    {
        // === QUALCOMM AI CLOUD LLM PLACEHOLDER ===
        // In production, this would call:
        // POST https://api.qualcomm.com/ai-cloud/v1/chat
        // with player stats as context for the LLM
        //
        // Request body:
        // {
        //   "model": "qualcomm-llama-3",
        //   "messages": [{
        //     "role": "system",
        //     "content": "You are a boss villain in a VR shooter game. Taunt the player based on their stats."
        //   }, {
        //     "role": "user", 
        //     "content": "Player has {kills} kills, score {score}, wave {wave}. {context}"
        //   }]
        // }

        // Demo fallback lines
        if (kills > 10) return $"Impressive, {kills} kills. But the dark side is eternal.";
        if (kills > 5) return $"{kills} kills? You're getting warmer, but still cold as ice.";
        return $"Only {kills}? Pathetic. I expected more from a Snapdragon-powered warrior.";
    }

    public void HideDialogue()
    {
        if (typewriterCoroutine != null)
            StopCoroutine(typewriterCoroutine);
        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
        if (captionCanvas != null)
            captionCanvas.gameObject.SetActive(false);
    }

    IEnumerator TypewriterEffect(string fullText)
    {
        if (dialogueText != null) dialogueText.text = "";
        if (captionText != null) captionText.text = "";
        for (int i = 0; i < fullText.Length; i++)
        {
            string shown = fullText.Substring(0, i + 1);
            if (dialogueText != null) dialogueText.text = shown;
            if (captionText != null) captionText.text = shown;
            yield return new WaitForSeconds(typewriterSpeed);
        }
    }

    void Update()
    {
        // Billboard the boss bubble to face the camera
        if (dialogueCanvas != null && Camera.main != null)
        {
            dialogueCanvas.transform.LookAt(
                dialogueCanvas.transform.position + Camera.main.transform.forward
            );
        }

        // Lock the caption to the lower-center of the player's view
        if (captionCanvas != null && captionCanvas.gameObject.activeSelf && Camera.main != null)
        {
            Transform cam = Camera.main.transform;
            captionCanvas.transform.position = cam.position + cam.forward * 2.2f - cam.up * 0.75f;
            captionCanvas.transform.rotation = Quaternion.LookRotation(
                captionCanvas.transform.position - cam.position);
        }
    }
}
