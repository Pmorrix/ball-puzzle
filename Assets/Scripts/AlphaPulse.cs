using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class AlphaPulse : MonoBehaviour
{
    [Header("Pulso de opacidad")]
    [Tooltip("Número de pulsos completos por segundo.")]
    [SerializeField, Min(0.01f)] private float pulseSpeed = 0.65f;

    [Tooltip("Opacidad mínima del pulso. La máxima conserva la opacidad original.")]
    [SerializeField, Range(0f, 1f)] private float minimumAlpha = 0.25f;

    private Graphic graphic;
    private SpriteRenderer spriteRenderer;
    private float originalAlpha = 1f;
    private float phase;
    private bool hasTarget;

    private void Awake()
    {
        FindVisualTarget();
    }

    private void OnEnable()
    {
        if (!hasTarget)
        {
            FindVisualTarget();
        }

        phase = 0f;
        if (hasTarget)
        {
            ApplyAlpha(Mathf.Min(minimumAlpha, originalAlpha));
        }
    }

    private void Update()
    {
        if (!hasTarget)
        {
            return;
        }

        phase = Mathf.Repeat(
            phase + Time.deltaTime * pulseSpeed,
            1f);

        // El pulso empieza tenue y alcanza su máxima opacidad al avanzar.
        float wave =
            (1f - Mathf.Cos(phase * Mathf.PI * 2f)) * 0.5f;
        float lowestAlpha =
            Mathf.Min(minimumAlpha, originalAlpha);
        ApplyAlpha(Mathf.Lerp(lowestAlpha, originalAlpha, wave));
    }

    private void OnDisable()
    {
        // Restaurar la opacidad evita dejar el gráfico semitransparente.
        if (hasTarget)
        {
            ApplyAlpha(originalAlpha);
        }
    }

    private void FindVisualTarget()
    {
        graphic = GetComponent<Graphic>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        hasTarget = graphic != null || spriteRenderer != null;

        if (graphic != null)
        {
            originalAlpha = graphic.color.a;
        }
        else if (spriteRenderer != null)
        {
            originalAlpha = spriteRenderer.color.a;
        }

        if (!hasTarget)
        {
            Debug.LogWarning(
                "AlphaPulse necesita un Graphic de UI o un SpriteRenderer.",
                this);
            enabled = false;
        }
    }

    private void ApplyAlpha(float alpha)
    {
        if (graphic != null)
        {
            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
            return;
        }

        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = alpha;
            spriteRenderer.color = color;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        pulseSpeed = Mathf.Max(0.01f, pulseSpeed);
    }
#endif
}
