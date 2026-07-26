using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public sealed class PieceSelectionCard : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    ISelectHandler,
    IDeselectHandler
{
    [Header("Palette")]
    [Tooltip("Show this card in the piece palette. Disabled cards cannot be selected.")]
    [SerializeField] private bool activeInPalette = true;
    [Tooltip("Show this card as locked and prevent its selection.")]
    [SerializeField] private bool lockedInPalette;

    [Header("Interaction")]
    [SerializeField] private Button button;
    [SerializeField] private RectTransform visualRoot;

    [Header("Visuals")]
    [SerializeField] private Image background;
    [SerializeField] private Outline outline;
    [SerializeField] private Image accentBar;
    [SerializeField] private Image pieceThumbnail;
    [SerializeField] private TMP_Text countLabel;
    [SerializeField] private TMP_Text stateLabel;
    [SerializeField] private GameObject lockBadge;
    [SerializeField, Min(1f)] private float transitionSpeed = 16f;

    private static readonly Color NormalBackground = new Color(0.025f, 0.055f, 0.085f, 0.18f);
    private static readonly Color HoverBackground = new Color(0.035f, 0.13f, 0.19f, 0.30f);
    private static readonly Color SelectedBackground = new Color(0.025f, 0.14f, 0.20f, 0.34f);
    private static readonly Color DisabledBackground = new Color(0.035f, 0.04f, 0.05f, 0.38f);
    private static readonly Color NormalOutline = new Color(0.05f, 0.42f, 0.68f, 0.48f);
    private static readonly Color HoverOutline = new Color(0.10f, 0.72f, 1f, 1f);
    private static readonly Color SelectedOutline = new Color(0.10f, 0.82f, 1f, 1f);
    private static readonly Color DisabledOutline = new Color(0.28f, 0.31f, 0.35f, 0.6f);
    private static readonly Color CyanAccent = new Color(0.06f, 0.62f, 0.95f, 1f);
    private static readonly Color DisabledAccent = new Color(0.31f, 0.34f, 0.38f, 0.75f);
    private static readonly Color EnabledThumbnail = Color.white;
    private static readonly Color DisabledThumbnail = new Color(0.34f, 0.37f, 0.41f, 0.48f);

    private bool availableInLevel;
    private bool selected;
    private bool hovered;
    private bool pressed;
    private bool focused;
    private int remaining;

    public bool ActiveInPalette => activeInPalette;
    public bool LockedInPalette => lockedInPalette;
    public Button Button => button;
    public event Action PointerPressed;

    public Vector2 GetRightSideScreenPosition(float screenY, float margin)
    {
        RectTransform cardRect = transform as RectTransform;
        if (cardRect == null)
        {
            return new Vector2(0f, screenY);
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        Camera canvasCamera =
            canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
        Vector3[] corners = new Vector3[4];
        cardRect.GetWorldCorners(corners);
        Vector2 topRight =
            RectTransformUtility.WorldToScreenPoint(canvasCamera, corners[2]);
        Vector2 bottomRight =
            RectTransformUtility.WorldToScreenPoint(canvasCamera, corners[3]);
        float rightEdge = Mathf.Max(topRight.x, bottomRight.x);
        float verticalPosition = Mathf.Clamp(screenY, bottomRight.y, topRight.y);
        return new Vector2(rightEdge + margin, verticalPosition);
    }

    private void Awake()
    {
        ApplyVisualsInstantly();
    }

    private void OnEnable()
    {
        pressed = false;
        ApplyVisualsInstantly();
    }

    private void Update()
    {
        if (visualRoot == null || background == null || outline == null ||
            accentBar == null || pieceThumbnail == null)
        {
            return;
        }

        GetVisualTargets(
            out Vector3 targetScale,
            out Color targetBackground,
            out Color targetOutline,
            out Color targetAccent,
            out Color targetThumbnail);

        float blend = 1f - Mathf.Exp(-transitionSpeed * Time.unscaledDeltaTime);
        visualRoot.localScale = Vector3.Lerp(visualRoot.localScale, targetScale, blend);
        background.color = Color.Lerp(background.color, targetBackground, blend);
        outline.effectColor = Color.Lerp(outline.effectColor, targetOutline, blend);
        accentBar.color = Color.Lerp(accentBar.color, targetAccent, blend);
        pieceThumbnail.color = Color.Lerp(pieceThumbnail.color, targetThumbnail, blend);
    }

    public void SetState(
        bool newAvailableInLevel,
        bool canInteract,
        bool newSelected,
        int newRemaining)
    {
        availableInLevel = newAvailableInLevel;
        remaining = Mathf.Max(0, newRemaining);
        selected = newSelected && availableInLevel && remaining > 0;

        if (button != null)
        {
            button.interactable = canInteract && availableInLevel && remaining > 0;
        }

        if (countLabel != null)
        {
            countLabel.text = availableInLevel ? "×" + remaining : "—";
        }

        if (stateLabel != null)
        {
            stateLabel.text = !availableInLevel
                ? string.Empty
                : remaining <= 0
                    ? "USED"
                    : selected
                        ? "SELECTED"
                        : string.Empty;
        }

        if (lockBadge != null)
        {
            lockBadge.SetActive(!availableInLevel);
        }
    }

    public void ApplyInspectorActiveState()
    {
        if (gameObject.activeSelf != activeInPalette)
        {
            gameObject.SetActive(activeInPalette);
        }
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
        pressed =
            eventData.button == PointerEventData.InputButton.Left &&
            IsInteractive();
        if (pressed)
        {
            PointerPressed?.Invoke();
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
    }

    public void OnSelect(BaseEventData eventData)
    {
        focused = true;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        focused = false;
        pressed = false;
    }

    private bool IsInteractive()
    {
        return button != null && button.interactable;
    }

    private void ApplyVisualsInstantly()
    {
        if (visualRoot == null || background == null || outline == null ||
            accentBar == null || pieceThumbnail == null)
        {
            return;
        }

        GetVisualTargets(
            out Vector3 targetScale,
            out Color targetBackground,
            out Color targetOutline,
            out Color targetAccent,
            out Color targetThumbnail);
        visualRoot.localScale = targetScale;
        background.color = targetBackground;
        outline.effectColor = targetOutline;
        accentBar.color = targetAccent;
        pieceThumbnail.color = targetThumbnail;
    }

    private void GetVisualTargets(
        out Vector3 targetScale,
        out Color targetBackground,
        out Color targetOutline,
        out Color targetAccent,
        out Color targetThumbnail)
    {
        bool unavailable = !availableInLevel || remaining <= 0;
        bool highlighted = IsInteractive() && (hovered || focused);

        if (selected)
        {
            targetScale = Vector3.one * 1.025f;
            targetBackground = SelectedBackground;
            targetOutline = SelectedOutline;
            targetAccent = CyanAccent;
            targetThumbnail = EnabledThumbnail;
            return;
        }

        if (unavailable)
        {
            targetScale = Vector3.one;
            targetBackground = DisabledBackground;
            targetOutline = DisabledOutline;
            targetAccent = DisabledAccent;
            targetThumbnail = DisabledThumbnail;
            return;
        }

        targetScale = pressed
            ? Vector3.one * 0.975f
            : highlighted
                ? Vector3.one * 1.035f
                : Vector3.one;
        targetBackground = highlighted ? HoverBackground : NormalBackground;
        targetOutline = highlighted ? HoverOutline : NormalOutline;
        targetAccent = CyanAccent;
        targetThumbnail = EnabledThumbnail;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        Button newButton,
        RectTransform newVisualRoot,
        Image newBackground,
        Outline newOutline,
        Image newAccentBar,
        Image newPieceThumbnail,
        TMP_Text newCountLabel,
        TMP_Text newStateLabel,
        GameObject newLockBadge,
        bool isAvailableInLevel,
        int initialRemaining)
    {
        button = newButton;
        visualRoot = newVisualRoot;
        background = newBackground;
        outline = newOutline;
        accentBar = newAccentBar;
        pieceThumbnail = newPieceThumbnail;
        countLabel = newCountLabel;
        stateLabel = newStateLabel;
        lockBadge = newLockBadge;
        SetState(isAvailableInLevel, isAvailableInLevel && initialRemaining > 0, false, initialRemaining);
        ApplyVisualsInstantly();
    }
#endif
}
