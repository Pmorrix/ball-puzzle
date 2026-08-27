using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public sealed class BallPuzzleLevelController : MonoBehaviour
{
    private const float PieceCardSpawnMargin = 24f;

    public event Action<CircuitPiece> PlacementStarted;
    public event Action<CircuitPiece, bool> PlacementPositioned;
    public event Action<CircuitPiece> PieceRotated;
    public event Action<CircuitPiece> PiecePlaced;
    public event Action PlacementCancelled;
    public event Action TestStarted;
    public event Action LayoutReset;
    public event Action LevelCompleted;

    public int PlacedPieceCount => placedPieces.Count;
    public bool IsPendingPieceMoving => pendingPlacement.IsMoving;

    private enum LevelState
    {
        Build,
        Testing,
        Failure,
        ChallengeIncomplete,
        Success
    }

    [Header("Scene")]
    [SerializeField] private Camera buildCamera;
    [SerializeField] private CircuitPiece straightPiecePrefab;
    [SerializeField] private CircuitPiece curve45RightPiecePrefab;
    [SerializeField] private CircuitPiece halfStraightPiecePrefab;
    [SerializeField] private Rigidbody ball;
    [SerializeField] private Transform ballSpawnPoint;
    [SerializeField] private Transform prize;
    [SerializeField] private Transform startAnchor;

    [Header("Level 01")]
    [SerializeField, Min(0)] private int availableStraights = 2;
    [SerializeField, Min(0)] private int availableCurves = 2;
    [SerializeField, Min(0)] private int availableHalfStraights = 2;
    [SerializeField, Min(1f)] private float buildHalfSize = 16f;
    [SerializeField] private Vector2 buildAreaCenter = new Vector2(0f, 4f);

    [Header("Level goals")]
    [SerializeField, Min(1)] private int targetPieceCount = 3;
    [SerializeField] private bool requireAllPiecesForCompletion;
    [SerializeField] private string nextLevelScene;
    [SerializeField, Min(0.1f)] private float targetTestDuration = 6f;

    [Header("Countdown")]
    [SerializeField, Min(1f)] private float initialCountdownSeconds = 180f;

    [Header("Piece placement")]
    [SerializeField, Min(0.01f)] private float placementGridSize = 0.05f;
    [SerializeField, Min(0f)] private float connectionReleaseAssistDistance = 0.75f;
    [SerializeField] private float buildSurfaceHeight;
    [SerializeField, Min(1f)] private float rotationStep = 45f;

    [Header("Ball test")]
    [SerializeField, Min(0.1f)] private float launchSpeed = 7.5f;
    [SerializeField, Min(0.25f)] private float prizeCollectionDistance = 1.05f;
    [SerializeField] private float fallHeight = -0.55f;
    [SerializeField, Min(1f)] private float maximumTestDuration = 18f;
    [SerializeField, Min(0.1f)] private float stoppedDuration = 2f;

    [Header("UI")]
    [SerializeField] private RectTransform levelTitlePanel;
    [SerializeField] private bool moveLevelTitleDuringTest;
    [SerializeField] private Vector2 testingLevelTitleAnchoredPosition =
        new Vector2(0f, -24f);
    [SerializeField] private GameObject buildControlsPanel;
    [SerializeField] private GameObject buildActionsPanel;
    [SerializeField] private GameObject testingControlsPanel;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private GameObject rotationControlsPanel;
    [SerializeField] private GameObject introPresenter;
    [SerializeField] private GameObject playConfirmationDialog;
    [SerializeField] private Button confirmPlayButton;
    [SerializeField] private Button cancelPlayButton;
    [SerializeField] private Button straightButton;
    [SerializeField] private Button curveButton;
    [SerializeField] private Button rotateYButton;
    [SerializeField] private Button rotateYCounterClockwiseButton;
    [SerializeField] private Button placeButton;
    [SerializeField] private Button testButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button editButton;
    [SerializeField] private Button resultResetButton;
    [SerializeField] private TMP_Text straightButtonLabel;
    [SerializeField] private TMP_Text curveButtonLabel;
    [SerializeField] private TMP_Text testingLabel;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private TMP_Text inventoryStatusLabel;
    [SerializeField] private TMP_Text paletteSummaryLabel;
    [SerializeField] private TMP_Text resultTitleLabel;
    [SerializeField] private TMP_Text resultMessageLabel;
    [SerializeField] private PieceSelectionCard straightPieceCard;
    [SerializeField] private PieceSelectionCard curve45PieceCard;
    [SerializeField] private PieceSelectionCard halfStraightPieceCard;
    [SerializeField] private PieceSelectionCard[] lockedPieceCards;

    private readonly List<CircuitPiece> placedPieces = new List<CircuitPiece>();

    private Transform placedPiecesRoot;
    private BallPuzzlePlacementIndicator placementIndicator;
    private readonly BallPuzzlePendingPlacement pendingPlacement =
        new BallPuzzlePendingPlacement();
    private CircuitPiece pendingPiece => pendingPlacement.Piece;
    private PlacementState placementState
    {
        get => pendingPlacement.State;
        set => pendingPlacement.State = value;
    }
    private int pendingRotationIndex
    {
        get => pendingPlacement.RotationIndex;
        set => pendingPlacement.RotationIndex = value;
    }
    private Vector3 pendingDragOffset
    {
        get => pendingPlacement.DragOffset;
        set => pendingPlacement.DragOffset = value;
    }
    private Vector3 pendingRotationPivotLocal =>
        pendingPlacement.RotationPivotLocal;
    private bool pendingHasValidPosition
    {
        get => pendingPlacement.HasValidPosition;
        set => pendingPlacement.HasValidPosition = value;
    }
    private int straightRemaining;
    private int curveRemaining;
    private int halfStraightRemaining;
    private LevelState state = LevelState.Build;
    private BallPuzzleBallTestController ballTestController;
    private BallPuzzlePlacementValidator placementValidator;
    private BallPuzzlePendingPiecePositioner pendingPiecePositioner;
    private BallPuzzleLevelUiPresenter uiPresenter;
    private string status = "Choose a piece to start placing it.";
    private string resultMessage = string.Empty;
    private Vector3 prizeInitialScale;
    private Vector3 prizeInitialPosition;
    private BallPuzzleGoalBinding goalBinding;
    private float levelEntryCountdownSeconds;
    private float countdownRemaining;
    private bool countdownRunning;
    private bool countdownExpired;

    public static void ResetCountdownSession()
    {
        BallPuzzleCountdownSession.Reset();
    }

    private void Awake()
    {
        if (introPresenter != null && introPresenter.activeSelf)
        {
            introPresenter.SetActive(false);
        }

        if (targetPieceCount <= 0)
        {
            targetPieceCount = 3;
        }
        if (targetTestDuration <= 0f)
        {
            targetTestDuration = 6f;
        }

        if (buildCamera == null)
        {
            buildCamera = Camera.main;
        }

        if (buildCamera == null || straightPiecePrefab == null || curve45RightPiecePrefab == null ||
            halfStraightPiecePrefab == null ||
            ball == null || ballSpawnPoint == null || prize == null ||
            !HasRequiredUi())
        {
            Debug.LogError("Level01: faltan referencias de escena, prefabs o Canvas UI.", this);
            enabled = false;
            return;
        }

        InitializeCountdown();
        BallPuzzleProgressStore.RememberCurrentLevel(
            SceneManager.GetActiveScene().name);
        uiPresenter = CreateUiPresenter();
        uiPresenter.ApplyPieceCardActiveStates();
        EnablePanelDragging(rotationControlsPanel);
        HidePlayConfirmationDialog();
        WireUiEvents();

        GameObject piecesRoot = new GameObject("Placed Puzzle Pieces");
        piecesRoot.transform.SetParent(transform, false);
        placedPiecesRoot = piecesRoot.transform;
        placementIndicator = new BallPuzzlePlacementIndicator(transform);
        placementValidator = new BallPuzzlePlacementValidator(
            placedPieces,
            buildAreaCenter,
            buildHalfSize);
        pendingPiecePositioner = new BallPuzzlePendingPiecePositioner(
            buildCamera,
            placementGridSize,
            buildSurfaceHeight,
            rotationStep);

        straightRemaining = availableStraights;
        curveRemaining = availableCurves;
        halfStraightRemaining = availableHalfStraights;
        prizeInitialScale = prize.localScale;
        prizeInitialPosition = prize.position;
        goalBinding = new BallPuzzleGoalBinding(
            placedPieces,
            placementValidator,
            startAnchor,
            prize,
            prizeInitialPosition,
            prizeCollectionDistance);
        ballTestController = new BallPuzzleBallTestController(
            ball,
            ballSpawnPoint,
            prize,
            prizeCollectionDistance,
            fallHeight,
            maximumTestDuration,
            stoppedDuration);
        ballTestController.ResetBallForBuild();
        RefreshUi();

        if (introPresenter != null)
        {
            introPresenter.SetActive(true);
        }
    }

    private void OnDestroy()
    {
        UnwireUiEvents();
        placementIndicator?.Dispose();
    }

    private static DraggableUIPanel EnablePanelDragging(GameObject panel)
    {
        if (panel == null)
        {
            return null;
        }

        Image panelImage = panel.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.raycastTarget = true;
        }

        if (panel.TryGetComponent(out DraggableUIPanel draggable))
        {
            return draggable;
        }

        return panel.AddComponent<DraggableUIPanel>();
    }

    private void Update()
    {
        AnimatePrize();
        placementIndicator?.Animate();
        UpdateCountdown();

        if (state == LevelState.Build)
        {
            UpdateBuildMode();
        }
        else if (state == LevelState.Testing)
        {
            UpdateBallTest();
        }

        StopCountdownWhenPlayBecomesAvailable();
        RefreshUi();
    }

    private void UpdateBuildMode()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        bool pointerOverUi =
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        Vector2 pointerPosition = mouse.position.ReadValue();

        if (pendingPiece != null && mouse.rightButton.wasPressedThisFrame)
        {
            CancelPendingPiece();
            return;
        }

        if (pendingPiece == null)
        {
            return;
        }

        if (placementState == PlacementState.Dragging)
        {
            if (mouse.leftButton.wasReleasedThisFrame)
            {
                if (pointerOverUi)
                {
                    // Si se suelta sobre el panel, la pieza sigue vinculada
                    // al cursor y no queda fijada todavía.
                    placementState = PlacementState.Selected;
                    status = "Move the cursor onto the board and click to set the piece.";
                    return;
                }

                UpdatePendingPiece(pointerPosition);
                FinishPendingPieceDrag();
                return;
            }

            if (mouse.leftButton.isPressed && !pointerOverUi)
            {
                UpdatePendingPiece(pointerPosition);
            }

            return;
        }

        // Tras soltar la tarjeta, la pieza sigue al cursor sin otro clic.
        if (placementState == PlacementState.Selected && !pointerOverUi)
        {
            pendingDragOffset = Vector3.zero;
            UpdatePendingPiece(pointerPosition);

            if (mouse.leftButton.wasPressedThisFrame)
            {
                FinishPendingPieceDrag();
            }
            else
            {
                status = "Move the piece and click to set its position.";
            }

            return;
        }

        if (pointerOverUi || !mouse.leftButton.wasPressedThisFrame)
        {
            return;
        }

        if (!TryGetPendingPieceDragOffset(
                pointerPosition,
                out Vector3 dragOffset))
        {
            return;
        }

        pendingDragOffset = dragOffset;
        placementState = PlacementState.Dragging;
        UpdatePendingPiece(pointerPosition);
        status = "Drag the piece and release the left mouse button to set its position.";
    }

    private Vector3 ClampStructureTranslation(Vector3 translation)
    {
        translation.y = 0f;
        if (!TryGetStructureBounds(out Bounds bounds))
        {
            return translation;
        }

        float minimumX = buildAreaCenter.x - buildHalfSize - bounds.min.x;
        float maximumX = buildAreaCenter.x + buildHalfSize - bounds.max.x;
        float minimumZ = buildAreaCenter.y - buildHalfSize - bounds.min.z;
        float maximumZ = buildAreaCenter.y + buildHalfSize - bounds.max.z;

        translation.x = minimumX <= maximumX
            ? Mathf.Clamp(translation.x, minimumX, maximumX)
            : 0f;
        translation.z = minimumZ <= maximumZ
            ? Mathf.Clamp(translation.z, minimumZ, maximumZ)
            : 0f;
        return translation;
    }

    private bool TryGetStructureBounds(out Bounds structureBounds)
    {
        structureBounds = new Bounds();
        bool hasBounds = false;

        foreach (CircuitPiece piece in placedPieces)
        {
            EncapsulatePieceBounds(piece, ref structureBounds, ref hasBounds);
        }

        EncapsulatePieceBounds(pendingPiece, ref structureBounds, ref hasBounds);
        return hasBounds;
    }

    private static void EncapsulatePieceBounds(
        CircuitPiece piece,
        ref Bounds structureBounds,
        ref bool hasBounds)
    {
        if (piece == null || !piece.gameObject.activeSelf)
        {
            return;
        }

        Bounds pieceBounds = piece.GetRenderBounds();
        if (!hasBounds)
        {
            structureBounds = pieceBounds;
            hasBounds = true;
            return;
        }

        structureBounds.Encapsulate(pieceBounds);
    }

    private bool TryAlignPlacedStructureToAnchors(bool includePendingPiece = false)
    {
        if (placedPiecesRoot == null)
        {
            return false;
        }

        bool foundAlignment = false;
        int bestAnchorMatchCount = 0;
        float closestDistance = float.PositiveInfinity;
        Vector3 bestOffset = Vector3.zero;

        EvaluateStructureAnchorOffsets(
            startAnchor,
            includePendingPiece,
            ref foundAlignment,
            ref bestAnchorMatchCount,
            ref closestDistance,
            ref bestOffset);
        if (!foundAlignment)
        {
            return false;
        }

        Vector3 clampedOffset = ClampStructureTranslation(bestOffset);
        if ((clampedOffset - bestOffset).sqrMagnitude > 0.000001f)
        {
            return false;
        }

        placedPiecesRoot.position += clampedOffset;
        goalBinding.RecalculatePosition();
        return true;
    }

    private void EvaluateStructureAnchorOffsets(
        Transform anchor,
        bool includePendingPiece,
        ref bool foundAlignment,
        ref int bestAnchorMatchCount,
        ref float closestDistance,
        ref Vector3 bestOffset)
    {
        if (anchor == null)
        {
            return;
        }

        int pieceCount = GetStructurePieceCount(includePendingPiece);
        for (int pieceIndex = 0; pieceIndex < pieceCount; pieceIndex++)
        {
            CircuitPiece piece = GetStructurePiece(pieceIndex);
            if (piece == null || !piece.gameObject.activeSelf)
            {
                continue;
            }

            for (int connector = 0;
                 connector < piece.ConnectorCount;
                 connector++)
            {
                if (goalBinding.IsBoundConnector(piece, connector))
                {
                    continue;
                }

                if (IsStructureConnectorOccupied(
                        piece,
                        connector,
                        includePendingPiece))
                {
                    continue;
                }

                Vector3 offset =
                    anchor.position - piece.GetConnectorPosition(connector);
                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance > GetAnchorAlignmentAssistDistance())
                {
                    continue;
                }

                int anchorMatchCount = CountAlignedStructureAnchors(
                    offset,
                    includePendingPiece);
                if (foundAlignment &&
                    (anchorMatchCount < bestAnchorMatchCount ||
                     anchorMatchCount == bestAnchorMatchCount &&
                     distance >= closestDistance))
                {
                    continue;
                }

                foundAlignment = true;
                bestAnchorMatchCount = anchorMatchCount;
                closestDistance = distance;
                bestOffset = offset;
            }
        }
    }

    private int CountAlignedStructureAnchors(
        Vector3 positionOffset,
        bool includePendingPiece)
    {
        return HasOpenStructureConnectorAt(
            startAnchor,
            positionOffset,
            includePendingPiece)
            ? 1
            : 0;
    }

    private bool HasOpenStructureConnectorAt(
        Transform anchor,
        Vector3 positionOffset,
        bool includePendingPiece = false)
    {
        if (anchor == null)
        {
            return false;
        }

        int pieceCount = GetStructurePieceCount(includePendingPiece);
        for (int pieceIndex = 0; pieceIndex < pieceCount; pieceIndex++)
        {
            CircuitPiece piece = GetStructurePiece(pieceIndex);
            if (piece == null || !piece.gameObject.activeSelf)
            {
                continue;
            }

            for (int connector = 0;
                 connector < piece.ConnectorCount;
                 connector++)
            {
                if (!IsStructureConnectorOccupied(
                        piece,
                        connector,
                        includePendingPiece) &&
                    BallPuzzlePlacementValidator.HorizontalDistance(
                        piece.GetConnectorPosition(connector) + positionOffset,
                        anchor.position) <=
                    BallPuzzlePlacementValidator.ConnectionPositionTolerance)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsStructureConnectorOccupied(
        CircuitPiece piece,
        int connector,
        bool includePendingPiece = false)
    {
        int pieceCount = GetStructurePieceCount(includePendingPiece);
        for (int pieceIndex = 0; pieceIndex < pieceCount; pieceIndex++)
        {
            CircuitPiece otherPiece = GetStructurePiece(pieceIndex);
            if (otherPiece == null ||
                !otherPiece.gameObject.activeSelf ||
                otherPiece == piece)
            {
                continue;
            }

            for (int otherConnector = 0;
                 otherConnector < otherPiece.ConnectorCount;
                 otherConnector++)
            {
                if (BallPuzzlePlacementValidator.AreConnectorsAligned(
                        piece,
                        connector,
                        otherPiece,
                        otherConnector))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private int GetStructurePieceCount(bool includePendingPiece = false)
    {
        bool hasIncludedPendingPiece = pendingPiece != null &&
                                       pendingPiece.gameObject.activeSelf &&
                                       (includePendingPiece || pendingHasValidPosition);
        return placedPieces.Count + (hasIncludedPendingPiece ? 1 : 0);
    }

    private CircuitPiece GetStructurePiece(int index)
    {
        return index < placedPieces.Count
            ? placedPieces[index]
            : pendingPiece;
    }

    private bool HasRequiredAnchorAlignment(bool includePendingPiece)
    {
        return HasOpenStructureConnectorAt(
            startAnchor,
            Vector3.zero,
            includePendingPiece);
    }

    private bool HasCompleteAnchorAlignment()
    {
        return HasOpenStructureConnectorAt(startAnchor, Vector3.zero) &&
               goalBinding.HasValidGoal();
    }

    private float GetAnchorAlignmentAssistDistance()
    {
        return Mathf.Max(
                   connectionReleaseAssistDistance * 2f,
                   placementGridSize * 2f) +
               BallPuzzlePlacementValidator.ConnectionPositionTolerance;
    }

    private void BeginPlacement(
        CircuitPiece prefab,
        PieceSelectionCard sourceCard)
    {
        if (state != LevelState.Build || prefab == null || GetRemainingCount(prefab) <= 0)
        {
            return;
        }

        StartCountdown();
        CancelPendingPiece(false);
        CircuitPiece piece = Instantiate(prefab, placedPiecesRoot);
        pendingPlacement.Begin(piece);
        pendingPiece.name = prefab.DisplayName + " (pendiente)";
        pendingPiece.ClearTint();

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed)
        {
            placementState = PlacementState.Dragging;
            Vector2 pointerPosition = mouse.position.ReadValue();
            Vector2 spawnPosition = sourceCard != null
                ? sourceCard.GetRightSideScreenPosition(
                    pointerPosition.y,
                    PieceCardSpawnMargin)
                : pointerPosition;
            UpdatePendingPiece(spawnPosition);
            if (sourceCard != null)
            {
                MovePendingPieceFullyRightOf(spawnPosition);
            }
            status = "Drag the piece and release the left mouse button to set its position.";
        }
        else
        {
            PositionPendingPieceAtStagingPoint();
            status = "Move the cursor onto the board and click to set the piece.";
        }

        PlacementStarted?.Invoke(pendingPiece);
    }

    private void PositionPendingPieceAtStagingPoint()
    {
        pendingPiecePositioner.PositionAtStagingPoint(
            pendingPiece,
            buildAreaCenter,
            pendingRotationIndex);
        EvaluatePendingPlacement();
    }

    private void UpdatePendingPiece(Vector2 mousePosition)
    {
        if (!pendingPiecePositioner.TryPositionAtPointer(
                pendingPiece,
                mousePosition,
                pendingDragOffset,
                pendingRotationIndex))
        {
            SetPendingPlacementValidity(false);
            return;
        }

        TryAlignPendingPieceToConnectionTarget();
        EvaluatePendingPlacement();
    }

    private void MovePendingPieceFullyRightOf(Vector2 minimumScreenPosition)
    {
        pendingPiecePositioner.MoveFullyRightOf(
            pendingPiece,
            minimumScreenPosition);
        EvaluatePendingPlacement();
    }

    private void EvaluatePendingPlacement()
    {
        if (pendingPiece == null || !pendingPiece.gameObject.activeSelf)
        {
            SetPendingPlacementValidity(false);
            return;
        }

        bool isInsideBuildArea = placementValidator.IsInsideBuildArea(pendingPiece);
        bool hasValidConnection = placedPieces.Count == 0 ||
                                  placementValidator.HasValidConnection(pendingPiece);
        bool hasRequiredAnchorAlignment = HasRequiredAnchorAlignment(true);
        SetPendingPlacementValidity(
            isInsideBuildArea &&
            hasValidConnection &&
            hasRequiredAnchorAlignment);
    }

    private void FinishPendingPieceDrag()
    {
        if (pendingPiece == null || !pendingPiece.gameObject.activeSelf)
        {
            placementState = PlacementState.Selected;
            return;
        }

        TryAlignPendingPieceToConnectionTarget();
        if (placedPieces.Count > 0 &&
            placementValidator.IsInsideBuildArea(pendingPiece) &&
            placementValidator.HasValidConnection(pendingPiece))
        {
            TryAlignPlacedStructureToAnchors(true);
        }

        placementState = PlacementState.Positioned;
        EvaluatePendingPlacement();
        PlacementPositioned?.Invoke(pendingPiece, pendingHasValidPosition);

        status = pendingHasValidPosition
            ? "Valid position. You can rotate, drag again, or press PLACE."
            : GetInvalidPlacementMessage();
    }

    private bool TryGetPendingPieceDragOffset(
        Vector2 mousePosition,
        out Vector3 dragOffset)
    {
        return pendingPiecePositioner.TryGetDragOffset(
            pendingPiece,
            mousePosition,
            out dragOffset);
    }

    private string GetInvalidPlacementMessage()
    {
        if (!HasOpenStructureConnectorAt(
                startAnchor,
                Vector3.zero,
                true))
        {
            return placedPieces.Count == 0
                ? "The first piece must be centered on START."
                : "The track must remain centered on START.";
        }

        return placedPieces.Count == 0
            ? "The first piece must remain completely inside the board."
            : "The piece must connect correctly to an open endpoint.";
    }

    private void SetPendingPlacementValidity(bool isValid)
    {
        pendingHasValidPosition = isValid;
        placementIndicator?.Refresh(
            pendingPiece,
            placementState != PlacementState.None,
            pendingHasValidPosition);
    }

    private void PlacePendingPiece()
    {
        if (placementState != PlacementState.Positioned ||
            pendingPiece == null ||
            !pendingPiece.gameObject.activeSelf)
        {
            status = "Drag and release the piece before placing it.";
            return;
        }

        EvaluatePendingPlacement();
        if (!pendingHasValidPosition)
        {
            status = GetInvalidPlacementMessage();
            return;
        }

        CircuitPiece piece = pendingPiece;
        piece.name = piece.DisplayName + " " + (placedPieces.Count + 1);
        piece.ClearTint();
        placementIndicator?.Hide();
        placedPieces.Add(piece);
        ConsumePiece(piece);
        goalBinding.ReevaluateBinding();
        PiecePlaced?.Invoke(piece);

        pendingPlacement.Reset();
        status = goalBinding.IsAdjustedPiece(piece)
            ? "Final piece placed. GOAL connection detected."
            : "Piece placed. Continue or press PLAY.";
    }

    private void CancelPendingPiece(bool updateStatus = true)
    {
        if (pendingPiece != null)
        {
            Destroy(pendingPiece.gameObject);
        }

        placementIndicator?.Hide();
        pendingPlacement.Reset();
        PlacementCancelled?.Invoke();
        if (updateStatus)
        {
            status = "Placement cancelled.";
        }
    }

    private void RequestStartBallTest()
    {
        if (!CanStartBallTest())
        {
            return;
        }

        if (playConfirmationDialog == null ||
            confirmPlayButton == null ||
            cancelPlayButton == null)
        {
            StartBallTest();
            return;
        }

        playConfirmationDialog.SetActive(true);
        playConfirmationDialog.transform.SetAsLastSibling();
    }

    private void ConfirmStartBallTest()
    {
        HidePlayConfirmationDialog();
        StartBallTest();
    }

    private void HidePlayConfirmationDialog()
    {
        if (playConfirmationDialog != null)
        {
            playConfirmationDialog.SetActive(false);
        }
    }

    private void StartBallTest()
    {
        if (!CanStartBallTest())
        {
            return;
        }

        state = LevelState.Testing;
        resultMessage = string.Empty;
        prize.gameObject.SetActive(true);
        prize.localScale = prizeInitialScale;

        ballTestController.Start(GetBallLaunchDirection(), launchSpeed);
        status = "Test running: the ball must collect the prize.";
        TestStarted?.Invoke();
    }

    private bool CanStartBallTest()
    {
        if (state != LevelState.Build || pendingPiece != null)
        {
            status = "Finish or cancel the pending piece before testing.";
            return false;
        }

        if (placedPieces.Count == 0)
        {
            status = "Place at least one piece before testing.";
            return false;
        }

        if (!HasCompleteAnchorAlignment())
        {
            status = "Circuit incomplete: GOAL must be on the final piece.";
            return false;
        }

        return true;
    }

    private Vector3 GetBallLaunchDirection()
    {
        if (startAnchor != null)
        {
            foreach (CircuitPiece piece in placedPieces)
            {
                if (piece == null || !piece.gameObject.activeSelf)
                {
                    continue;
                }

                for (int connector = 0;
                     connector < piece.ConnectorCount;
                     connector++)
                {
                    if (placementValidator.IsPlacedConnectorOccupied(piece, connector) ||
                        BallPuzzlePlacementValidator.HorizontalDistance(
                            piece.GetConnectorPosition(connector),
                            startAnchor.position) >
                        BallPuzzlePlacementValidator.ConnectionPositionTolerance)
                    {
                        continue;
                    }

                    Vector3 launchDirection =
                        -BallPuzzlePlacementValidator.Flatten(
                            piece.GetConnectorDirection(connector));
                    if (launchDirection.sqrMagnitude > Mathf.Epsilon)
                    {
                        return launchDirection;
                    }
                }
            }
        }

        return ballSpawnPoint.forward;
    }

    private void UpdateBallTest()
    {
        switch (ballTestController.Evaluate())
        {
            case BallPuzzleBallTestResult.PrizeCollected:
                CompleteCircuitTest();
                break;
            case BallPuzzleBallTestResult.Fell:
                FailTest("The ball fell off the track.");
                break;
            case BallPuzzleBallTestResult.TimedOut:
                FailTest("The test timed out.");
                break;
            case BallPuzzleBallTestResult.Stopped:
                FailTest("The ball has stopped.");
                break;
        }
    }

    private void FailTest(string reason)
    {
        ballTestController.Finish();
        state = LevelState.Failure;
        status = reason;
        resultMessage = BallPuzzleLevelUiPresenter.CreateResultMessage(
            countdownRemaining,
            placedPieces.Count,
            targetPieceCount);
    }

    private void CompleteLevel()
    {
        ballTestController.Finish();
        countdownRunning = false;
        BallPuzzleCountdownSession.SetRemaining(countdownRemaining);
        state = LevelState.Success;
        status = "Prize collected! Level complete.";
        resultMessage = BallPuzzleLevelUiPresenter.CreateResultMessage(
            countdownRemaining,
            placedPieces.Count,
            targetPieceCount);
        prize.localScale = prizeInitialScale * 1.35f;
        LevelCompleted?.Invoke();
    }

    private void CompleteCircuitTest()
    {
        if (!requireAllPiecesForCompletion || AreAllPiecesUsed())
        {
            CompleteLevel();
            return;
        }

        ballTestController.Finish();
        state = LevelState.ChallengeIncomplete;
        status = "Circuit complete, but use all " + targetPieceCount +
                 " pieces to finish the level.";
        resultMessage = "CIRCUIT WORKS\nPIECES USED  " + placedPieces.Count +
                        " / " + targetPieceCount +
                        "\nTIME LEFT  " + countdownRemaining.ToString("0.0") + " s" +
                        "\nUSE ALL PIECES TO WIN";
    }

    private bool AreAllPiecesUsed()
    {
        return straightRemaining == 0 &&
               curveRemaining == 0 &&
               halfStraightRemaining == 0;
    }

    private void RetryTest()
    {
        state = LevelState.Build;
        ballTestController.ResetBallForBuild();
        StartBallTest();
    }

    private void ReturnToBuild()
    {
        state = LevelState.Build;
        ballTestController.ResetBallForBuild();
        prize.gameObject.SetActive(true);
        prize.localScale = prizeInitialScale;
        status = "Adjust the layout and press PLAY again.";
    }

    private void HandleResultAction()
    {
        if (countdownExpired)
        {
            RestartCurrentLevel();
            return;
        }

        if (state != LevelState.Success || string.IsNullOrWhiteSpace(nextLevelScene))
        {
            ReturnToBuild();
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(nextLevelScene))
        {
            Debug.LogError(
                "Level transition: scene '" + nextLevelScene +
                "' is not available in Build Settings.",
                this);
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(nextLevelScene);
    }

    private void RestartCurrentLevel()
    {
        BallPuzzleCountdownSession.SetRemaining(levelEntryCountdownSeconds);
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void ExitToMainMenu()
    {
        if (countdownExpired)
        {
            BallPuzzleCountdownSession.SetRemaining(
                levelEntryCountdownSeconds);
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    private void ResetLayout()
    {
        CancelPendingPiece(false);
        goalBinding.Reset();
        foreach (CircuitPiece piece in placedPieces)
        {
            if (piece != null)
            {
                Destroy(piece.gameObject);
            }
        }
        placedPieces.Clear();
        if (placedPiecesRoot != null)
        {
            placedPiecesRoot.localPosition = Vector3.zero;
        }
        straightRemaining = availableStraights;
        curveRemaining = availableCurves;
        halfStraightRemaining = availableHalfStraights;
        state = LevelState.Build;
        ballTestController.ClearDuration();
        resultMessage = string.Empty;
        ballTestController.ResetBallForBuild();
        prize.gameObject.SetActive(true);
        prize.localScale = prizeInitialScale;
        status = "Layout reset. Choose a piece to place it.";
        LayoutReset?.Invoke();
    }

    private void AnimatePrize()
    {
        if (prize == null || !prize.gameObject.activeSelf)
        {
            return;
        }
        prize.Rotate(0f, 65f * Time.deltaTime, 0f, Space.World);
    }

    private bool TryAlignPendingPieceToConnectionTarget()
    {
        if (pendingPiece == null)
        {
            return false;
        }

        if (placedPieces.Count == 0 &&
            TryGetClosestAnchorOffset(startAnchor, out Vector3 startOffset))
        {
            ApplyPendingPieceOffset(startOffset);
            return true;
        }

        bool foundConnection = false;
        float closestDistance = float.PositiveInfinity;
        Vector3 bestOffset = Vector3.zero;

        for (int candidateConnector = 0;
             candidateConnector < pendingPiece.ConnectorCount;
             candidateConnector++)
        {
            foreach (CircuitPiece placedPiece in placedPieces)
            {
                if (placedPiece == null)
                {
                    continue;
                }

                for (int placedConnector = 0;
                     placedConnector < placedPiece.ConnectorCount;
                     placedConnector++)
                {
                    if (placementValidator.IsPlacedConnectorOccupied(
                            placedPiece,
                            placedConnector) ||
                        !BallPuzzlePlacementValidator.AreConnectorDirectionsOpposite(
                            pendingPiece,
                            candidateConnector,
                            placedPiece,
                            placedConnector))
                    {
                        continue;
                    }

                    Vector3 candidatePosition =
                        pendingPiece.GetConnectorPosition(candidateConnector);
                    Vector3 placedPosition =
                        placedPiece.GetConnectorPosition(placedConnector);
                    Vector3 offset = placedPosition - candidatePosition;
                    offset.y = 0f;
                    float distance = offset.magnitude;
                    if (distance > connectionReleaseAssistDistance ||
                        distance >= closestDistance)
                    {
                        continue;
                    }

                    foundConnection = true;
                    closestDistance = distance;
                    bestOffset = offset;
                }
            }
        }

        if (!foundConnection)
        {
            return false;
        }

        ApplyPendingPieceOffset(bestOffset);
        return true;
    }

    private bool TryGetClosestAnchorOffset(
        Transform anchor,
        out Vector3 bestOffset)
    {
        bestOffset = Vector3.zero;
        if (anchor == null)
        {
            return false;
        }

        bool foundAnchor = false;
        float closestDistance = float.PositiveInfinity;
        for (int connector = 0;
             connector < pendingPiece.ConnectorCount;
             connector++)
        {
            Vector3 offset =
                anchor.position - pendingPiece.GetConnectorPosition(connector);
            offset.y = 0f;
            float distance = offset.magnitude;
            if (distance > GetAnchorAlignmentAssistDistance() ||
                distance >= closestDistance)
            {
                continue;
            }

            foundAnchor = true;
            closestDistance = distance;
            bestOffset = offset;
        }

        return foundAnchor;
    }

    private void ApplyPendingPieceOffset(Vector3 offset)
    {
        pendingPiece.transform.position += offset;
        pendingPiecePositioner.RestOnBuildSurface(pendingPiece);
    }

    private int GetRemainingCount(CircuitPiece prefab)
    {
        switch (prefab.PieceType)
        {
            case CircuitPieceType.Straight:
                return straightRemaining;
            case CircuitPieceType.Curve45Right:
                return curveRemaining;
            case CircuitPieceType.HalfStraight:
                return halfStraightRemaining;
            default:
                return 0;
        }
    }

    private void ConsumePiece(CircuitPiece piece)
    {
        switch (piece.PieceType)
        {
            case CircuitPieceType.Straight:
                straightRemaining = Mathf.Max(0, straightRemaining - 1);
                break;
            case CircuitPieceType.Curve45Right:
                curveRemaining = Mathf.Max(0, curveRemaining - 1);
                break;
            case CircuitPieceType.HalfStraight:
                halfStraightRemaining = Mathf.Max(0, halfStraightRemaining - 1);
                break;
        }
    }

    private bool HasRequiredUi()
    {
        return buildControlsPanel != null && buildActionsPanel != null &&
               testingControlsPanel != null && resultPanel != null &&
               rotationControlsPanel != null &&
               straightButton != null && curveButton != null && testButton != null &&
               rotateYButton != null && rotateYCounterClockwiseButton != null &&
               placeButton != null &&
               resetButton != null && retryButton != null &&
               editButton != null && straightButtonLabel != null &&
               curveButtonLabel != null && testingLabel != null && statusLabel != null &&
               inventoryStatusLabel != null && resultTitleLabel != null && resultMessageLabel != null &&
               straightPieceCard != null && curve45PieceCard != null &&
               halfStraightPieceCard != null && halfStraightPieceCard.Button != null &&
               HasLockedPieceCards();
    }

    private bool HasLockedPieceCards()
    {
        if (lockedPieceCards == null || lockedPieceCards.Length == 0)
        {
            return false;
        }

        foreach (PieceSelectionCard card in lockedPieceCards)
        {
            if (card == null)
            {
                return false;
            }
        }

        return true;
    }

    private void WireUiEvents()
    {
        straightPieceCard.PointerPressed += SelectStraightPiece;
        curve45PieceCard.PointerPressed += SelectCurvePiece;
        halfStraightPieceCard.PointerPressed += SelectHalfStraightPiece;
        rotateYButton.onClick.AddListener(RotatePendingPieceY);
        rotateYCounterClockwiseButton.onClick.AddListener(RotatePendingPieceYCounterClockwise);
        placeButton.onClick.AddListener(PlacePendingPiece);
        testButton.onClick.AddListener(RequestStartBallTest);
        if (confirmPlayButton != null)
        {
            confirmPlayButton.onClick.AddListener(ConfirmStartBallTest);
        }
        if (cancelPlayButton != null)
        {
            cancelPlayButton.onClick.AddListener(HidePlayConfirmationDialog);
        }
        resetButton.onClick.AddListener(ResetLayout);
        retryButton.onClick.AddListener(HandleResultAction);
        editButton.onClick.AddListener(ExitToMainMenu);
    }

    private void UnwireUiEvents()
    {
        if (straightPieceCard != null) straightPieceCard.PointerPressed -= SelectStraightPiece;
        if (curve45PieceCard != null) curve45PieceCard.PointerPressed -= SelectCurvePiece;
        if (halfStraightPieceCard != null) halfStraightPieceCard.PointerPressed -= SelectHalfStraightPiece;
        if (rotateYButton != null) rotateYButton.onClick.RemoveListener(RotatePendingPieceY);
        if (rotateYCounterClockwiseButton != null)
        {
            rotateYCounterClockwiseButton.onClick.RemoveListener(RotatePendingPieceYCounterClockwise);
        }
        if (placeButton != null) placeButton.onClick.RemoveListener(PlacePendingPiece);
        if (testButton != null) testButton.onClick.RemoveListener(RequestStartBallTest);
        if (confirmPlayButton != null)
        {
            confirmPlayButton.onClick.RemoveListener(ConfirmStartBallTest);
        }
        if (cancelPlayButton != null)
        {
            cancelPlayButton.onClick.RemoveListener(HidePlayConfirmationDialog);
        }
        if (resetButton != null) resetButton.onClick.RemoveListener(ResetLayout);
        if (retryButton != null) retryButton.onClick.RemoveListener(HandleResultAction);
        if (editButton != null) editButton.onClick.RemoveListener(ExitToMainMenu);
    }

    private void SelectStraightPiece()
    {
        if (straightPieceCard.ActiveInPalette && !straightPieceCard.LockedInPalette)
        {
            BeginPlacement(straightPiecePrefab, straightPieceCard);
        }
    }

    private void SelectCurvePiece()
    {
        if (curve45PieceCard.ActiveInPalette && !curve45PieceCard.LockedInPalette)
        {
            BeginPlacement(curve45RightPiecePrefab, curve45PieceCard);
        }
    }

    private void SelectHalfStraightPiece()
    {
        if (halfStraightPieceCard.ActiveInPalette && !halfStraightPieceCard.LockedInPalette)
        {
            BeginPlacement(halfStraightPiecePrefab, halfStraightPieceCard);
        }
    }

    private void RotatePendingPieceY()
    {
        RotatePendingPieceY(1);
    }

    private void RotatePendingPieceYCounterClockwise()
    {
        RotatePendingPieceY(-1);
    }

    private void RotatePendingPieceY(int direction)
    {
        if (state != LevelState.Build ||
            pendingPiece == null ||
            placementState != PlacementState.Positioned)
        {
            return;
        }

        int orientationCount = Mathf.Max(1, Mathf.RoundToInt(360f / rotationStep));
        pendingRotationIndex =
            (pendingRotationIndex + direction + orientationCount) %
            orientationCount;

        pendingPiecePositioner.RotateAroundPivot(
            pendingPiece,
            pendingRotationPivotLocal,
            pendingRotationIndex);
        pendingPiecePositioner.RestOnBuildSurface(pendingPiece);
        EvaluatePendingPlacement();
        PieceRotated?.Invoke(pendingPiece);
        float angle = direction * rotationStep;
        status = "Rotated " + angle.ToString("+0;-0;0") +
                 " degrees around Y. Position unchanged.";
    }

    private BallPuzzleLevelUiPresenter CreateUiPresenter()
    {
        return new BallPuzzleLevelUiPresenter(
            levelTitlePanel,
            moveLevelTitleDuringTest,
            testingLevelTitleAnchoredPosition,
            buildControlsPanel,
            buildActionsPanel,
            testingControlsPanel,
            resultPanel,
            rotationControlsPanel,
            testButton,
            resetButton,
            rotateYButton,
            rotateYCounterClockwiseButton,
            placeButton,
            editButton,
            retryButton,
            testingLabel,
            statusLabel,
            inventoryStatusLabel,
            paletteSummaryLabel,
            resultTitleLabel,
            resultMessageLabel,
            straightPieceCard,
            curve45PieceCard,
            halfStraightPieceCard,
            lockedPieceCards);
    }

    private void RefreshUi()
    {
        bool isBuilding = state == LevelState.Build;
        bool isTesting = state == LevelState.Testing;
        bool isShowingResult = state == LevelState.Failure ||
                               state == LevelState.ChallengeIncomplete ||
                               state == LevelState.Success;
        CircuitPieceType? selectedPieceType = pendingPiece != null
            ? pendingPiece.PieceType
            : null;
        float displayedTestDuration = ballTestController != null
            ? ballTestController.GetDisplayedDuration()
            : 0f;

        uiPresenter.Refresh(new BallPuzzleLevelUiState(
            isBuilding,
            isTesting,
            isShowingResult,
            state == LevelState.Success,
            state == LevelState.ChallengeIncomplete,
            countdownExpired,
            !string.IsNullOrWhiteSpace(nextLevelScene),
            selectedPieceType,
            pendingPiece != null,
            pendingPiece != null && pendingPiece.gameObject.activeSelf,
            placementState == PlacementState.Positioned,
            pendingHasValidPosition,
            HasCompleteAnchorAlignment(),
            placedPieces.Count,
            straightRemaining,
            curveRemaining,
            halfStraightRemaining,
            status,
            resultMessage,
            countdownRemaining,
            ballTestController != null && ballTestController.HasTestDuration,
            displayedTestDuration));
    }

    private void InitializeCountdown()
    {
        if (!BallPuzzleCountdownSession.HasRemaining)
        {
            BallPuzzleCountdownSession.SetRemaining(initialCountdownSeconds);
        }

        countdownRemaining = BallPuzzleCountdownSession.Remaining;
        levelEntryCountdownSeconds = countdownRemaining;
        countdownRunning = false;
        countdownExpired = false;
    }

    private void StartCountdown()
    {
        if (!countdownExpired && countdownRemaining > 0f)
        {
            countdownRunning = true;
        }
    }

    private void StopCountdownWhenPlayBecomesAvailable()
    {
        if (state != LevelState.Build ||
            pendingPiece != null ||
            !HasCompleteAnchorAlignment())
        {
            return;
        }

        countdownRunning = false;
        BallPuzzleCountdownSession.SetRemaining(countdownRemaining);
    }

    private void UpdateCountdown()
    {
        if (!countdownRunning || countdownExpired || state == LevelState.Success)
        {
            return;
        }

        countdownRemaining = Mathf.Max(
            0f,
            countdownRemaining - Time.deltaTime);
        BallPuzzleCountdownSession.SetRemaining(countdownRemaining);
        if (countdownRemaining <= 0f)
        {
            ExpireCountdown();
        }
    }

    private void ExpireCountdown()
    {
        countdownRunning = false;
        countdownExpired = true;
        countdownRemaining = 0f;
        BallPuzzleCountdownSession.SetRemaining(0f);
        HidePlayConfirmationDialog();
        ballTestController.Finish();
        state = LevelState.Failure;
        status = "Time is up.";
        resultMessage = "TIME LEFT  0.0 s\nRETRY THE LEVEL";
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        Camera newBuildCamera,
        CircuitPiece newStraightPiecePrefab,
        CircuitPiece newCurve45RightPiecePrefab,
        Rigidbody newBall,
        Transform newBallSpawnPoint,
        Transform newPrize,
        Transform newStartAnchor)
    {
        buildCamera = newBuildCamera;
        straightPiecePrefab = newStraightPiecePrefab;
        curve45RightPiecePrefab = newCurve45RightPiecePrefab;
        ball = newBall;
        ballSpawnPoint = newBallSpawnPoint;
        prize = newPrize;
        startAnchor = newStartAnchor;
    }

    public void ConfigureUiForEditor(
        GameObject newBuildControlsPanel,
        GameObject newTestingControlsPanel,
        GameObject newResultPanel,
        Button newStraightButton,
        Button newCurveButton,
        Button newTestButton,
        Button newResetButton,
        Button newRetryButton,
        Button newEditButton,
        Button newResultResetButton,
        TMP_Text newStraightButtonLabel,
        TMP_Text newCurveButtonLabel,
        TMP_Text newTestingLabel,
        TMP_Text newStatusLabel,
        TMP_Text newInventoryStatusLabel,
        TMP_Text newResultTitleLabel,
        TMP_Text newResultMessageLabel,
        PieceSelectionCard newStraightPieceCard,
        PieceSelectionCard newCurve45PieceCard,
        PieceSelectionCard[] newLockedPieceCards)
    {
        buildControlsPanel = newBuildControlsPanel;
        buildActionsPanel = newBuildControlsPanel != null
            ? newBuildControlsPanel.transform.Find("Build Actions")?.gameObject
            : null;
        testingControlsPanel = newTestingControlsPanel;
        resultPanel = newResultPanel;
        straightButton = newStraightButton;
        curveButton = newCurveButton;
        testButton = newTestButton;
        resetButton = newResetButton;
        retryButton = newRetryButton;
        editButton = newEditButton;
        resultResetButton = newResultResetButton;
        straightButtonLabel = newStraightButtonLabel;
        curveButtonLabel = newCurveButtonLabel;
        testingLabel = newTestingLabel;
        statusLabel = newStatusLabel;
        inventoryStatusLabel = newInventoryStatusLabel;
        resultTitleLabel = newResultTitleLabel;
        resultMessageLabel = newResultMessageLabel;
        straightPieceCard = newStraightPieceCard;
        curve45PieceCard = newCurve45PieceCard;
        lockedPieceCards = newLockedPieceCards;
    }

    public void ConfigureRotationUiForEditor(
        GameObject newRotationControlsPanel,
        Button newRotateYButton,
        Button newRotateYCounterClockwiseButton,
        Button newPlaceButton)
    {
        rotationControlsPanel = newRotationControlsPanel;
        rotateYButton = newRotateYButton;
        rotateYCounterClockwiseButton = newRotateYCounterClockwiseButton;
        placeButton = newPlaceButton;
    }
#endif
}

internal static class BallPuzzleCountdownSession
{
    private static bool hasRemaining;
    private static float remaining;

    public static bool HasRemaining => hasRemaining;
    public static float Remaining => Mathf.Max(0f, remaining);

    public static void SetRemaining(float seconds)
    {
        remaining = Mathf.Max(0f, seconds);
        hasRemaining = true;
    }

    public static void Reset()
    {
        remaining = 0f;
        hasRemaining = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlaySessionStart()
    {
        Reset();
    }
}
