using UnityEngine;
using TMPro;
using System.Collections;

public class FloatingText : MonoBehaviour
{
    [Header("Animation")]
    public float floatSpeed = 1f;
    public float lifetime = 1.2f;
    public float scaleUpDuration = 0.15f;
    public float maxScale = 1.2f;

    private TextMeshPro textMesh;
    private Color textColor;
    private Transform cameraTransform;

    /// <summary>
    /// Convenience one-liner: spawns a floating text popup at a world position.
    /// </summary>
    public static FloatingText Spawn(string text, Vector3 worldPosition, Color color)
    {
        GameObject go = new GameObject("FloatingText_" + text);
        go.transform.position = worldPosition;
        FloatingText ft = go.AddComponent<FloatingText>();
        ft.Initialize(text, color);
        return ft;
    }

    public void Initialize(string text, Color color)
    {
        textColor = color;

        // Create TextMeshPro component
        textMesh = gameObject.AddComponent<TextMeshPro>();
        textMesh.text = text;
        textMesh.fontSize = 4f;
        textMesh.color = color;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.fontStyle = FontStyles.Bold;
        textMesh.sortingOrder = 100;

        // Set rect transform size
        RectTransform rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(2f, 1f);
        }

        // Make it face the camera
        cameraTransform = Camera.main?.transform;

        transform.localScale = Vector3.zero;

        StartCoroutine(AnimatePopup());
    }

    IEnumerator AnimatePopup()
    {
        float timer = 0f;
        Vector3 startPos = transform.position;

        while (timer < lifetime)
        {
            timer += Time.deltaTime;
            float t = timer / lifetime;

            // Move up
            transform.position = startPos + Vector3.up * (floatSpeed * timer);

            // Scale - pop in then stay
            float scaleT = Mathf.Clamp01(timer / scaleUpDuration);
            // Elastic ease
            float elastic = scaleT < 1f ? 
                1f - Mathf.Pow(2f, -10f * scaleT) * Mathf.Cos(scaleT * Mathf.PI * 2f) : 1f;
            float currentScale = elastic * maxScale;

            // Fade out in last 30%
            if (t > 0.7f)
            {
                float fadeT = (t - 0.7f) / 0.3f;
                currentScale *= (1f - fadeT);
                if (textMesh != null)
                {
                    Color c = textColor;
                    c.a = 1f - fadeT;
                    textMesh.color = c;
                }
            }

            transform.localScale = Vector3.one * currentScale;

            // Billboard - face camera
            if (cameraTransform != null)
            {
                transform.rotation = Quaternion.LookRotation(
                    transform.position - cameraTransform.position
                );
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}
