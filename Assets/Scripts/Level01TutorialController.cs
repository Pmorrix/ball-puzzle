using UnityEngine;

[DisallowMultipleComponent]
public sealed class Level01TutorialController : MonoBehaviour
{
    private const string CompletionKey = "BallPuzzleTutorialCompleted";
    private const float ArrowHeightOffset = 0.04f;

    [Header("Gameplay")]
    [SerializeField] private BallPuzzleLevelController levelController;
    [SerializeField] private Camera buildCamera;
    [SerializeField] private GameObject rotationControlsPanel;

    [Header("Tutorial hints")]
    [SerializeField] private GameObject stepOneHint;
    [SerializeField] private GameObject stepTwoTarget;
    [SerializeField] private SpriteRenderer stepTwoArrow;
    [SerializeField] private GameObject rotationHighlight;
    [SerializeField] private GameObject placeHighlight;
    [SerializeField] private GameObject connectionTarget;
    [SerializeField] private Vector2 rotationPanelScreenOffset =
        new Vector2(360f, -240f);

    private CircuitPiece pendingPiece;
    private CircuitPiece connectionSourcePiece;
    private GameObject activeHint;
    private bool subscribed;
    private bool tutorialCompleted;
    private bool firstConnectionCompleted;

    private void Awake()
    {
        tutorialCompleted = PlayerPrefs.GetInt(CompletionKey, 0) != 0;
        if (tutorialCompleted)
        {
            HideAllHints();
            enabled = false;
            return;
        }

        if (levelController == null || buildCamera == null ||
            rotationControlsPanel == null || connectionTarget == null)
        {
            Debug.LogError(
                "Level01 tutorial: missing a required scene reference.",
                this);
            HideAllHints();
            enabled = false;
        }
    }

    private void OnEnable()
    {
        Subscribe();
        ShowHint(stepOneHint);
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        UpdateStepTwoArrow();
    }

    private void Subscribe()
    {
        if (subscribed || levelController == null)
        {
            return;
        }

        levelController.PlacementStarted += HandlePlacementStarted;
        levelController.PlacementPositioned += HandlePlacementPositioned;
        levelController.PieceRotated += HandlePieceRotated;
        levelController.PiecePlaced += HandlePiecePlaced;
        levelController.PlacementCancelled += HandlePlacementCancelled;
        levelController.TestStarted += HandleTestStarted;
        levelController.LayoutReset += HandleLayoutReset;
        levelController.LevelCompleted += HandleLevelCompleted;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || levelController == null)
        {
            return;
        }

        levelController.PlacementStarted -= HandlePlacementStarted;
        levelController.PlacementPositioned -= HandlePlacementPositioned;
        levelController.PieceRotated -= HandlePieceRotated;
        levelController.PiecePlaced -= HandlePiecePlaced;
        levelController.PlacementCancelled -= HandlePlacementCancelled;
        levelController.TestStarted -= HandleTestStarted;
        levelController.LayoutReset -= HandleLayoutReset;
        levelController.LevelCompleted -= HandleLevelCompleted;
        subscribed = false;
    }

    private void HandlePlacementStarted(CircuitPiece piece)
    {
        pendingPiece = piece;

        if (connectionSourcePiece != null && !firstConnectionCompleted)
        {
            ShowHint(connectionTarget);
            return;
        }

        ShowHint(levelController.PlacedPieceCount == 0 ? stepTwoTarget : null);
    }

    private void HandlePlacementPositioned(CircuitPiece piece, bool isValid)
    {
        pendingPiece = piece;

        // La segunda pieza debe conectarse con el extremo libre de la primera.
        if (connectionSourcePiece != null && !firstConnectionCompleted)
        {
            ShowHint(isValid ? null : connectionTarget);
            return;
        }

        if (levelController.PlacedPieceCount > 0)
        {
            ShowHint(null);
            return;
        }

        if (!isValid)
        {
            ShowHint(stepTwoTarget);
            return;
        }

        MoveRotationControlsNextToPiece(piece);
        ShowHint(rotationHighlight);
    }

    private void HandlePieceRotated(CircuitPiece piece)
    {
        pendingPiece = piece;
        ShowHint(levelController.PlacedPieceCount == 0 ? placeHighlight : null);
    }

    private void HandlePiecePlaced(CircuitPiece piece)
    {
        pendingPiece = null;

        if (levelController.PlacedPieceCount == 1 &&
            TryPositionConnectionTarget(piece))
        {
            connectionSourcePiece = piece;
            ShowHint(connectionTarget);
            return;
        }

        if (levelController.PlacedPieceCount >= 2)
        {
            firstConnectionCompleted = true;
            connectionSourcePiece = null;
        }

        ShowHint(null);
    }

    private void HandlePlacementCancelled()
    {
        pendingPiece = null;

        if (connectionSourcePiece != null && !firstConnectionCompleted)
        {
            ShowHint(connectionTarget);
            return;
        }

        ShowHint(levelController.PlacedPieceCount == 0 ? stepOneHint : null);
    }

    private void HandleTestStarted()
    {
        pendingPiece = null;
        ShowHint(null);
    }

    private void HandleLayoutReset()
    {
        pendingPiece = null;
        connectionSourcePiece = null;
        firstConnectionCompleted = false;
        ShowHint(stepOneHint);
    }

    private void HandleLevelCompleted()
    {
        tutorialCompleted = true;
        PlayerPrefs.SetInt(CompletionKey, 1);
        PlayerPrefs.Save();
        pendingPiece = null;
        connectionSourcePiece = null;
        HideAllHints();
        enabled = false;
    }

    private void ShowHint(GameObject hint)
    {
        activeHint = hint;

        if (stepOneHint != null)
        {
            stepOneHint.SetActive(hint == stepOneHint);
        }

        if (stepTwoTarget != null)
        {
            stepTwoTarget.SetActive(hint == stepTwoTarget);
        }

        if (rotationHighlight != null)
        {
            rotationHighlight.SetActive(hint == rotationHighlight);
        }

        if (placeHighlight != null)
        {
            placeHighlight.SetActive(hint == placeHighlight);
        }

        if (connectionTarget != null)
        {
            connectionTarget.SetActive(hint == connectionTarget);
        }

        if (stepTwoArrow != null && GetActiveWorldTarget() == null)
        {
            stepTwoArrow.gameObject.SetActive(false);
        }
    }

    private void HideAllHints()
    {
        ShowHint(null);
        if (stepTwoArrow != null)
        {
            stepTwoArrow.gameObject.SetActive(false);
        }
    }

    private void UpdateStepTwoArrow()
    {
        if (stepTwoArrow == null)
        {
            return;
        }

        Transform target = GetActiveWorldTarget();
        bool shouldShow =
            pendingPiece != null &&
            target != null &&
            levelController != null &&
            levelController.IsPendingPieceMoving;
        if (stepTwoArrow.gameObject.activeSelf != shouldShow)
        {
            stepTwoArrow.gameObject.SetActive(shouldShow);
        }

        if (!shouldShow)
        {
            return;
        }

        Vector3 start = pendingPiece.GetRenderBounds().center;
        Vector3 end = target.position;
        float arrowHeight = Mathf.Max(start.y, end.y) + ArrowHeightOffset;
        start.y = arrowHeight;
        end.y = arrowHeight;

        Vector3 direction = end - start;
        float distance = direction.magnitude;
        if (distance <= Mathf.Epsilon)
        {
            stepTwoArrow.gameObject.SetActive(false);
            return;
        }

        direction /= distance;
        Vector3 perpendicular = Vector3.Cross(Vector3.up, direction);
        Vector3 spriteUp = (perpendicular - direction).normalized;
        stepTwoArrow.transform.SetPositionAndRotation(
            Vector3.Lerp(start, end, 0.5f),
            Quaternion.LookRotation(Vector3.up, spriteUp));

        float scale = Mathf.Clamp(distance / 3.6f, 0.35f, 1.3f);
        stepTwoArrow.transform.localScale = Vector3.one * scale;
    }

    private Transform GetActiveWorldTarget()
    {
        if (activeHint == stepTwoTarget && stepTwoTarget != null)
        {
            return stepTwoTarget.transform;
        }

        if (activeHint == connectionTarget && connectionTarget != null)
        {
            return connectionTarget.transform;
        }

        return null;
    }

    private bool TryPositionConnectionTarget(CircuitPiece piece)
    {
        if (connectionTarget == null || piece == null ||
            piece.ConnectorCount == 0)
        {
            return false;
        }

        int targetConnector = 0;
        if (piece.ConnectorCount > 1 &&
            targetConnector == piece.IncomingConnectorIndex)
        {
            targetConnector = 1;
        }

        // Solo cambia la posición; conserva rotación y escala del Inspector.
        Vector3 targetPosition = piece.GetConnectorPosition(targetConnector);
        targetPosition.y += ArrowHeightOffset;
        connectionTarget.transform.position = targetPosition;
        return true;
    }

    private void MoveRotationControlsNextToPiece(CircuitPiece piece)
    {
        if (piece == null ||
            !rotationControlsPanel.TryGetComponent(
                out DraggableUIPanel draggablePanel))
        {
            return;
        }

        Vector3 pieceScreenPosition = buildCamera.WorldToScreenPoint(
            piece.GetRenderBounds().center);
        if (pieceScreenPosition.z <= 0f)
        {
            return;
        }

        Canvas rootCanvas =
            rotationControlsPanel.GetComponentInParent<Canvas>()?.rootCanvas;
        float scaleFactor = rootCanvas != null
            ? Mathf.Max(rootCanvas.scaleFactor, 0.01f)
            : 1f;
        Vector2 targetScreenPosition =
            (Vector2)pieceScreenPosition +
            rotationPanelScreenOffset * scaleFactor;
        draggablePanel.MoveToScreenPosition(targetScreenPosition);
    }
}
