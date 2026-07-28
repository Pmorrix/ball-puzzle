using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public sealed class DraggableUIPanel : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private RectTransform dragArea;
    [SerializeField] private RectTransform linkedPanel;

    private RectTransform panelRect;
    private Canvas rootCanvas;
    private bool isDragging;

    private void Awake()
    {
        panelRect = (RectTransform)transform;
        rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;

        if (dragArea == null)
        {
            dragArea = panelRect.parent as RectTransform;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Las tarjetas reservan el arrastre para seleccionar y mover piezas.
        if (eventData.button != PointerEventData.InputButton.Left ||
            IsPointerOverPieceCard(eventData))
        {
            isDragging = false;
            return;
        }

        isDragging = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging)
        {
            return;
        }

        float scaleFactor = rootCanvas != null ? rootCanvas.scaleFactor : 1f;
        MovePanels(eventData.delta / Mathf.Max(scaleFactor, 0.01f));
        KeepInsideDragArea();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
    }

    public void SetLinkedPanel(RectTransform panel)
    {
        linkedPanel = panel != panelRect ? panel : null;
    }

    private static bool IsPointerOverPieceCard(PointerEventData eventData)
    {
        GameObject pressedObject = eventData.pointerPressRaycast.gameObject;
        if (pressedObject == null)
        {
            pressedObject = eventData.pointerPress;
        }

        return pressedObject != null &&
               pressedObject.GetComponentInParent<PieceSelectionCard>() != null;
    }

    private void MovePanels(Vector2 delta)
    {
        panelRect.anchoredPosition += delta;

        if (linkedPanel != null)
        {
            linkedPanel.anchoredPosition += delta;
        }
    }

    private void KeepInsideDragArea()
    {
        if (dragArea == null)
        {
            return;
        }

        Bounds panelBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(dragArea, panelRect);
        if (linkedPanel != null)
        {
            Bounds linkedBounds =
                RectTransformUtility.CalculateRelativeRectTransformBounds(dragArea, linkedPanel);
            panelBounds.Encapsulate(linkedBounds.min);
            panelBounds.Encapsulate(linkedBounds.max);
        }

        Rect area = dragArea.rect;
        Vector2 correction = Vector2.zero;

        if (panelBounds.min.x < area.xMin)
        {
            correction.x = area.xMin - panelBounds.min.x;
        }
        else if (panelBounds.max.x > area.xMax)
        {
            correction.x = area.xMax - panelBounds.max.x;
        }

        if (panelBounds.min.y < area.yMin)
        {
            correction.y = area.yMin - panelBounds.min.y;
        }
        else if (panelBounds.max.y > area.yMax)
        {
            correction.y = area.yMax - panelBounds.max.y;
        }

        MovePanels(correction);
    }
}
