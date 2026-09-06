using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class UIButtonHoverFeedback : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [SerializeField, Min(1f)] private float hoverScale = 1.055f;
    [SerializeField, Range(0.8f, 1f)] private float pressedScale = 0.975f;
    [SerializeField, Min(1f)] private float transitionSpeed = 18f;
    [SerializeField] private Color hoverTint = new Color(0.78f, 1f, 1f, 1f);
    [SerializeField] private Color pressedTint = new Color(0.62f, 0.82f, 0.95f, 1f);

    private Button button;
    private RectTransform visualRoot;
    private Graphic targetGraphic;
    private Vector3 normalScale;
    private Color normalColor;
    private bool hovered;
    private bool pressed;
    private bool initialized;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        button = GetComponent<Button>();
        visualRoot = transform as RectTransform;
        targetGraphic = button != null ? button.targetGraphic : null;
        if (button == null || visualRoot == null || targetGraphic == null)
        {
            return;
        }

        normalScale = visualRoot.localScale;
        normalColor = targetGraphic.color;
        button.transition = Selectable.Transition.None;
        initialized = true;
        ApplyVisualsInstantly();
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        bool interactive = button.IsInteractable();
        float scale = interactive
            ? pressed
                ? pressedScale
                : hovered
                    ? hoverScale
                    : 1f
            : 1f;
        Color color = interactive
            ? pressed
                ? pressedTint
                : hovered
                    ? hoverTint
                    : normalColor
            : normalColor;
        float blend = 1f - Mathf.Exp(-transitionSpeed * Time.unscaledDeltaTime);
        visualRoot.localScale = Vector3.Lerp(
            visualRoot.localScale,
            normalScale * scale,
            blend);
        targetGraphic.color = Color.Lerp(targetGraphic.color, color, blend);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = eventData.button == PointerEventData.InputButton.Left &&
                  button.IsInteractable();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
    }

    private void OnDisable()
    {
        hovered = false;
        pressed = false;
        ApplyVisualsInstantly();
    }

    private void ApplyVisualsInstantly()
    {
        if (!initialized)
        {
            return;
        }

        visualRoot.localScale = normalScale;
        targetGraphic.color = normalColor;
    }
}
