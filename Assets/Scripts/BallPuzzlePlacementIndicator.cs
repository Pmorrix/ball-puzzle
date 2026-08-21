using UnityEngine;
using UnityEngine.Rendering;

internal sealed class BallPuzzlePlacementIndicator
{
    private const int TextureSize = 64;
    private const float Padding = 2.4f;
    private const float HeightOffset = 0.04f;
    private const float PulseSpeed = 3.8f;
    private const float PulseAmount = 0.06f;

    private static readonly Color InteractionColor =
        new Color(0.23f, 0.78f, 0.91f, 0.82f);
    private static readonly Color ValidColor =
        new Color(0.31f, 0.84f, 0.60f, 0.82f);
    private static readonly Color InvalidColor =
        new Color(0.95f, 0.42f, 0.42f, 0.82f);

    private readonly GameObject indicator;
    private readonly SpriteRenderer indicatorRenderer;
    private readonly Texture2D indicatorTexture;
    private readonly Sprite indicatorSprite;

    private Vector3 baseScale;
    private Color baseColor;

    public BallPuzzlePlacementIndicator(Transform parent)
    {
        indicator = new GameObject("Placement Halo");
        indicator.transform.SetParent(parent, false);
        indicator.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        indicatorRenderer = indicator.AddComponent<SpriteRenderer>();
        indicatorRenderer.shadowCastingMode = ShadowCastingMode.Off;
        indicatorRenderer.receiveShadows = false;
        indicatorRenderer.sortingOrder = 20;

        indicatorTexture = CreateTexture();
        indicatorSprite = Sprite.Create(
            indicatorTexture,
            new Rect(0f, 0f, TextureSize, TextureSize),
            new Vector2(0.5f, 0.5f),
            TextureSize);
        indicatorSprite.name = "Placement Halo Sprite (Runtime)";
        indicatorSprite.hideFlags = HideFlags.HideAndDontSave;
        indicatorRenderer.sprite = indicatorSprite;
        indicator.SetActive(false);
    }

    public void Refresh(
        CircuitPiece pendingPiece,
        bool showPlacementValidity,
        bool hasValidPosition)
    {
        if (pendingPiece == null || !pendingPiece.gameObject.activeSelf)
        {
            Hide();
            return;
        }

        Bounds bounds = pendingPiece.GetRenderBounds();
        Vector3 center = bounds.center;
        center.y = bounds.min.y + HeightOffset;
        indicator.transform.position = center;
        baseScale = new Vector3(
            Mathf.Max(Padding, bounds.size.x + Padding * 2f),
            Mathf.Max(Padding, bounds.size.z + Padding * 2f),
            1f);
        baseColor = showPlacementValidity
            ? hasValidPosition ? ValidColor : InvalidColor
            : InteractionColor;

        indicator.SetActive(true);
        Animate();
    }

    public void Animate()
    {
        if (!indicator.activeSelf)
        {
            return;
        }

        float wave = (Mathf.Sin(Time.unscaledTime * PulseSpeed) + 1f) * 0.5f;
        float scale = 1f + Mathf.Lerp(-PulseAmount, PulseAmount, wave);
        indicator.transform.localScale = new Vector3(
            baseScale.x * scale,
            baseScale.y * scale,
            1f);

        Color animatedColor = baseColor;
        animatedColor.a *= Mathf.Lerp(0.78f, 1f, wave);
        indicatorRenderer.color = animatedColor;
    }

    public void Hide()
    {
        if (indicator.activeSelf)
        {
            indicator.SetActive(false);
        }
    }

    public void Dispose()
    {
        if (indicatorSprite != null)
        {
            Object.Destroy(indicatorSprite);
        }
        if (indicatorTexture != null)
        {
            Object.Destroy(indicatorTexture);
        }
    }

    private static Texture2D CreateTexture()
    {
        Texture2D texture = new Texture2D(
            TextureSize,
            TextureSize,
            TextureFormat.RGBA32,
            false)
        {
            name = "Placement Halo Texture (Runtime)",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[TextureSize * TextureSize];
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float normalizedX = (x + 0.5f) / TextureSize * 2f - 1f;
                float normalizedY = (y + 0.5f) / TextureSize * 2f - 1f;
                float distance = Mathf.Sqrt(
                    normalizedX * normalizedX +
                    normalizedY * normalizedY);
                float alpha = 1f - Mathf.SmoothStep(0.12f, 1f, distance);
                alpha = Mathf.Pow(alpha, 0.72f);
                pixels[y * TextureSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
        return texture;
    }
}
