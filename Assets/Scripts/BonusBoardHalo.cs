using UnityEngine;

[DisallowMultipleComponent]
public sealed class BonusBoardHalo : MonoBehaviour
{
    [Header("Board")]
    [SerializeField] private Renderer boardRenderer;

    [Tooltip("Cu�nto sobresale el halo respecto al borde del tablero.")]
    [SerializeField, Min(0f)] private float padding = 0.08f;

    [Tooltip("Altura sobre la superficie para evitar z-fighting.")]
    [SerializeField, Min(0f)] private float heightOffset = 0.03f;

    [Header("Halo")]
    [SerializeField]
    private Color haloColor =
        new Color(0.15f, 0.75f, 1f, 0.35f);

    [Tooltip("Grosor de la banda luminosa.")]
    [SerializeField, Range(0.01f, 0.5f)]
    private float perimeterThickness = 0.10f;

    [Tooltip("Suavizado de los bordes del halo.")]
    [SerializeField, Range(0.001f, 0.2f)]
    private float edgeFeather = 0.04f;

    [Header("Pulse")]
    [SerializeField, Min(0.01f)]
    private float pulseSpeed = 1.2f;

    [SerializeField, Range(0f, 1f)]
    private float minimumAlpha = 0.45f;

    private GameObject haloObject;
    private LineRenderer glowRenderer;
    private LineRenderer coreRenderer;
    private Material haloMaterial;

    private void Awake()
    {
        if (boardRenderer == null)
        {
            boardRenderer = GetComponentInChildren<Renderer>();
        }

        if (boardRenderer == null)
        {
            Debug.LogWarning(
                "BonusBoardHalo: no se ha encontrado el Renderer del tablero.",
                this);

            enabled = false;
            return;
        }

        CreateHalo();
        PositionHalo();
    }

    private void Update()
    {
        AnimateHalo();
    }

    private void CreateHalo()
    {
        haloObject = new GameObject("Bonus Board Halo");
        haloObject.transform.SetParent(transform, true);

        haloMaterial = new Material(Shader.Find("Sprites/Default"));
        glowRenderer = CreatePerimeterRenderer(
            "Soft Glow",
            perimeterThickness + edgeFeather * 2f);
        coreRenderer = CreatePerimeterRenderer(
            "Light Core",
            perimeterThickness);
    }

    private LineRenderer CreatePerimeterRenderer(
        string objectName,
        float width)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(haloObject.transform, true);

        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = true;
        lineRenderer.positionCount = 4;
        lineRenderer.widthMultiplier = width;
        lineRenderer.numCornerVertices = 4;
        lineRenderer.numCapVertices = 2;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.material = haloMaterial;
        lineRenderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.sortingOrder = 20;
        return lineRenderer;
    }

    private void PositionHalo()
    {
        Bounds bounds = boardRenderer.bounds;

        float y = bounds.max.y + heightOffset;
        Vector3[] corners =
        {
            new Vector3(bounds.min.x - padding, y, bounds.min.z - padding),
            new Vector3(bounds.min.x - padding, y, bounds.max.z + padding),
            new Vector3(bounds.max.x + padding, y, bounds.max.z + padding),
            new Vector3(bounds.max.x + padding, y, bounds.min.z - padding)
        };

        glowRenderer.SetPositions(corners);
        coreRenderer.SetPositions(corners);
    }

    private void AnimateHalo()
    {
        if (glowRenderer == null || coreRenderer == null)
        {
            return;
        }

        float wave =
            (Mathf.Sin(
                Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;

        float pulse = Mathf.SmoothStep(0f, 1f, wave);
        float alpha = haloColor.a * Mathf.Lerp(
            minimumAlpha,
            1f,
            pulse);

        Color coreColor = haloColor;
        coreColor.a = alpha;
        coreRenderer.startColor = coreColor;
        coreRenderer.endColor = coreColor;

        Color glowColor = haloColor;
        glowColor.a = alpha * 0.35f;
        glowRenderer.startColor = glowColor;
        glowRenderer.endColor = glowColor;
    }

    private void OnDestroy()
    {
        if (haloMaterial != null)
        {
            Destroy(haloMaterial);
        }

        if (haloObject != null)
        {
            Destroy(haloObject);
        }
    }
}
