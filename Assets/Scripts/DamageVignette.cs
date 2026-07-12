using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DamageVignette : MonoBehaviour
{
    [Header("Vignette Settings")]
    public Color damageColor = new Color(0.8f, 0f, 0f, 0.6f);
    public float fadeOutSpeed = 2f;

    private Image vignetteImage;
    private Canvas vignetteCanvas;
    private float currentAlpha = 0f;
    private float targetAlpha = 0f;

    void Start()
    {
        CreateVignetteUI();
    }

    void CreateVignetteUI()
    {
        // Create a world-space canvas attached to the camera
        GameObject canvasGO = new GameObject("DamageVignetteCanvas");
        canvasGO.transform.SetParent(transform);

        vignetteCanvas = canvasGO.AddComponent<Canvas>();
        vignetteCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        vignetteCanvas.sortingOrder = 999;

        // Add canvas scaler
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Create vignette image
        GameObject imageGO = new GameObject("VignetteImage");
        imageGO.transform.SetParent(canvasGO.transform, false);

        vignetteImage = imageGO.AddComponent<Image>();
        vignetteImage.color = new Color(damageColor.r, damageColor.g, damageColor.b, 0f);
        vignetteImage.raycastTarget = false;

        // Create a radial gradient texture for vignette effect
        Texture2D vignetteTex = CreateVignetteTexture(256, 256);
        Sprite vignetteSprite = Sprite.Create(vignetteTex,
            new Rect(0, 0, vignetteTex.width, vignetteTex.height),
            new Vector2(0.5f, 0.5f));
        vignetteImage.sprite = vignetteSprite;
        vignetteImage.type = Image.Type.Simple;
        vignetteImage.preserveAspect = false;

        // Fill the screen
        RectTransform rect = imageGO.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    Texture2D CreateVignetteTexture(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];

        Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
        float maxDist = Mathf.Sqrt(center.x * center.x + center.y * center.y);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = x - center.x;
                float dy = y - center.y;
                float dist = Mathf.Sqrt(dx * dx + dy * dy) / maxDist;

                // Vignette: transparent in center, opaque at edges
                float alpha = Mathf.Clamp01(Mathf.Pow(dist, 2f));

                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    void Update()
    {
        if (currentAlpha > 0f)
        {
            currentAlpha = Mathf.MoveTowards(currentAlpha, 0f, fadeOutSpeed * Time.deltaTime);
            UpdateVignetteAlpha();
        }
    }

    public void TriggerDamage(float intensity)
    {
        currentAlpha = Mathf.Clamp01(intensity);
        UpdateVignetteAlpha();
    }

    void UpdateVignetteAlpha()
    {
        if (vignetteImage != null)
        {
            vignetteImage.color = new Color(damageColor.r, damageColor.g, damageColor.b, currentAlpha * damageColor.a);
        }
    }
}
