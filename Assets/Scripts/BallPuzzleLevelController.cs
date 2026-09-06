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
    public event Action<CircuitPiece> PieceRemoved;
    public event Action PlacementCancelled;
    public event Action TestStarted;
    public event Action LayoutReset;
    public event Action LevelCompleted;

    public int PlacedPieceCount => placedPieces.Count;
    public bool IsPendingPieceMoving => pendingPlacement.IsMoving;
    public bool IsBuilding => state == LevelState.Build;

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

    [Header("Bonus challenge")]
    [SerializeField] private BonusPieceChallengeController bonusChallenge;
    [SerializeField, Min(0.1f)] private float bonusStartReturnDistance = 1.05f;

    [Header("Bonus loan")]
    [SerializeField] private CircuitPiece bonusLoanPiecePrefab;
    [SerializeField] private PieceSelectionCard bonusLoanPieceCard;
    [SerializeField, Min(0)] private int availableBonusLoanPieces = 1;

    [Header("Countdown")]
    [SerializeField] private bool useCountdown = true;
    [SerializeField, Min(1f)] private float initialCountdownSeconds = 180f;

    [Header("Piece placement")]
    [SerializeField, Min(0.01f)] private float placementGridSize = 0.05f;
    [SerializeField, Min(0f)] private float connectionReleaseAssistDistance = 0.75f;
    [SerializeField] private float buildSurfaceHeight;
    [SerializeField, Min(1f)] private float rotationStep = 45f;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Ball test")]
    [SerializeField, Min(0.1f)] private float launchSpeed = 7.5f;
    [SerializeField] private bool continuousBallImpulse;
    [SerializeField, Min(0f)] private float ballDriveAcceleration = 6f;
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
    private readonly List<CircuitPiece> routePieces = new List<CircuitPiece>();

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
    private int bonusLoanRemaining;
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
    private InputActionMap gameplayInputMap;
    private InputAction pointerPositionAction;
    private InputAction primaryAction;
    private InputAction cancelAction;
    private InputAction undoAction;
    private bool inputMapEnabledByController;
    private bool ownsGameplayInputMap;
    private bool bonusBallLeftStart;

    public static void ResetCountdownSession()
    {
        BallPuzzleCountdownSession.Reset();
    }

    public bool RegisterConnectedScenePiece(CircuitPiece piece)
    {
        if (!IsBuilding || piece == null || routePieces.Contains(piece))
        {
            return false;
        }

        routePieces.Add(piece);
        goalBinding?.ReevaluateBinding();
        bonusChallenge?.RegisterConnectedTargetPiece(piece);
        status = HasCompleteAnchorAlignment()
            ? "Circuit closed at START. You can press PLAY."
            : "Bonus piece connected. Continue building the closed circuit.";
        RefreshUi();
        return true;
    }

    public bool UnregisterConnectedScenePiece(CircuitPiece piece)
    {
        if (!IsBuilding || piece == null || placedPieces.Contains(piece) ||
            !routePieces.Remove(piece))
        {
            return false;
        }

        bonusChallenge?.UnregisterPiece(piece);
        goalBinding?.ReevaluateBinding();
        return true;
    }

    public bool IsRouteConnectorOccupied(
        CircuitPiece piece,
        int connector)
    {
        return piece != null &&
               connector >= 0 &&
               connector < piece.ConnectorCount &&
               IsStructureConnectorOccupied(piece, connector);
    }

    public bool IsPieceAnchoredToStart(CircuitPiece piece)
    {
        if (piece == null || startAnchor == null) return false;
        for (int connector = 0; connector < piece.ConnectorCount; connector++)
            if (BallPuzzlePlacementValidator.HorizontalDistance(
                    piece.GetConnectorPosition(connector), startAnchor.position) <=
                BallPuzzlePlacementValidator.ConnectionPositionTolerance)
                return true;
        return false;
    }

    private void ConfigureInputActions()
    {
        if (gameplayInputMap != null &&
            pointerPositionAction != null &&
            primaryAction != null &&
            cancelAction != null &&
            undoAction != null)
        {
            return;
        }

        gameplayInputMap = inputActions != null
            ? inputActions.FindActionMap("Gameplay", false)
            : null;
        pointerPositionAction = gameplayInputMap?.FindAction("Point", false);
        primaryAction = gameplayInputMap?.FindAction("PrimaryAction", false);
        cancelAction = gameplayInputMap?.FindAction("Cancel", false);
        undoAction = gameplayInputMap?.FindAction("Undo", false);

        if (pointerPositionAction != null &&
            primaryAction != null &&
            cancelAction != null &&
            undoAction != null)
        {
            return;
        }

        gameplayInputMap = new InputActionMap("Gameplay");
        pointerPositionAction = gameplayInputMap.AddAction(
            "Point",
            InputActionType.PassThrough,
            "<Pointer>/position",
            expectedControlLayout: "Vector2");
        primaryAction = gameplayInputMap.AddAction(
            "PrimaryAction",
            InputActionType.Button,
            "<Pointer>/press",
            expectedControlLayout: "Button");
        cancelAction = gameplayInputMap.AddAction(
            "Cancel",
            InputActionType.Button,
            "<Mouse>/rightButton",
            expectedControlLayout: "Button");
        undoAction = gameplayInputMap.AddAction(
            "Undo",
            InputActionType.Button,
            "<Keyboard>/backspace",
            expectedControlLayout: "Button");
        ownsGameplayInputMap = true;
    }

    private bool TryGetPointerPosition(out Vector2 pointerPosition)
    {
        pointerPosition = Vector2.zero;
        if (pointerPositionAction == null ||
            !pointerPositionAction.enabled ||
            pointerPositionAction.controls.Count == 0)
        {
            return false;
        }

        pointerPosition = pointerPositionAction.ReadValue<Vector2>();
        return true;
    }

    private bool IsPrimaryActionPressed()
    {
        return primaryAction != null &&
               primaryAction.enabled &&
               primaryAction.IsPressed();
    }

    private bool WasPrimaryActionPressedThisFrame()
    {
        return primaryAction != null &&
               primaryAction.enabled &&
               primaryAction.WasPressedThisFrame();
    }

    private bool WasPrimaryActionReleasedThisFrame()
    {
        return primaryAction != null &&
               primaryAction.enabled &&
               primaryAction.WasReleasedThisFrame();
    }

    private bool WasCancelPressedThisFrame()
    {
        return cancelAction != null &&
               cancelAction.enabled &&
               cancelAction.WasPressedThisFrame();
    }

    private bool WasUndoPressedThisFrame()
    {
        return undoAction != null &&
               undoAction.enabled &&
               undoAction.WasPressedThisFrame();
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
            routePieces,
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
        bonusLoanRemaining = availableBonusLoanPieces;
        prizeInitialScale = prize.localScale;
        prizeInitialPosition = prize.position;
        goalBinding = new BallPuzzleGoalBinding(
            routePieces,
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
            stoppedDuration,
            bonusChallenge == null);
        ballTestController.ResetBallForBuild();
        if (bonusChallenge != null)
        {
            status = "Build a closed route back to START.";
        }
        RefreshUi();

        if (introPresenter != null)
        {
            introPresenter.SetActive(true);
        }
    }

    private void OnEnable()
    {
        ConfigureInputActions();
        inputMapEnabledByController = !gameplayInputMap.enabled;
        if (inputMapEnabledByController)
        {
            gameplayInputMap.Enable();
        }
    }

    private void OnDisable()
    {
        if (inputMapEnabledByController)
        {
            gameplayInputMap?.Disable();
        }

        inputMapEnabledByController = false;
    }

    private void OnDestroy()
    {
        UnwireUiEvents();
        placementIndicator?.Dispose();
        if (ownsGameplayInputMap)
        {
            gameplayInputMap?.Dispose();
        }
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

    private void FixedUpdate()
    {
        if (state == LevelState.Testing)
            ballTestController?.StepPhysics();
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
        bool cancelPressed = WasCancelPressedThisFrame();
        if (pendingPiece != null && cancelPressed)
        {
            CancelPendingPiece();
            return;
        }

        if (pendingPiece == null &&
            (cancelPressed || WasUndoPressedThisFrame()))
        {
            UndoLastPlacedPiece();
            return;
        }

        if (!TryGetPointerPosition(out Vector2 pointerPosition))
        {
            return;
        }

        bool pointerOverUi =
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        if (pendingPiece == null)
        {
            return;
        }

        if (placementState == PlacementState.Dragging)
        {
            if (WasPrimaryActionReleasedThisFrame())
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

            if (IsPrimaryActionPressed() && !pointerOverUi)
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

            if (WasPrimaryActionPressedThisFrame())
            {
                FinishPendingPieceDrag();
            }
            else
            {
                status = "Move the piece and click to set its position.";
            }

            return;
        }

        if (pointerOverUi || !WasPrimaryActionPressedThisFrame())
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
        status = "Drag the piece and release the primary button to set its position.";
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

        foreach (CircuitPiece piece in routePieces)
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
        return routePieces.Count + (hasIncludedPendingPiece ? 1 : 0);
    }

    private CircuitPiece GetStructurePiece(int index)
    {
        return index < routePieces.Count
            ? routePieces[index]
            : pendingPiece;
    }

    private bool HasRequiredAnchorAlignment(bool includePendingPiece)
    {
        if (bonusChallenge != null &&
            HasClosedBonusCircuit(includePendingPiece))
        {
            return true;
        }

        return HasConnectedScenePiece() ||
               HasOpenStructureConnectorAt(
                   startAnchor,
                   Vector3.zero,
                   includePendingPiece);
    }

    private bool HasCompleteAnchorAlignment()
    {
        if (bonusChallenge != null)
        {
            return bonusChallenge.AreAllTargetsConnected && HasClosedBonusCircuit();
        }

        return (HasConnectedScenePiece() ||
                HasOpenStructureConnectorAt(startAnchor, Vector3.zero)) &&
               goalBinding.HasValidGoal();
    }

    private bool HasClosedBonusCircuit(bool includePendingPiece = false)
    {
        int pieceCount = GetStructurePieceCount(includePendingPiece);
        if (startAnchor == null || pieceCount < 2)
        {
            return false;
        }

        CircuitPiece startPiece = null;
        int activePieceCount = 0;
        for (int pieceIndex = 0; pieceIndex < pieceCount; pieceIndex++)
        {
            CircuitPiece piece = GetStructurePiece(pieceIndex);
            if (piece == null || !piece.gameObject.activeSelf)
            {
                continue;
            }

            activePieceCount++;
            if (piece.ConnectorCount != 2 ||
                (placementValidator != null && !placementValidator.IsInsideBuildArea(piece)))
            {
                return false;
            }

            for (int connector = 0;
                 connector < piece.ConnectorCount;
                 connector++)
            {
                if (GetConnectedStructurePiece(
                        piece,
                        connector,
                        includePendingPiece) == null)
                {
                    return false;
                }

                if (BallPuzzlePlacementValidator.HorizontalDistance(
                        piece.GetConnectorPosition(connector),
                        startAnchor.position) <=
                    BallPuzzlePlacementValidator.ConnectionPositionTolerance)
                {
                    startPiece = piece;
                }
            }
        }

        if (startPiece == null || activePieceCount < 2)
        {
            return false;
        }

        HashSet<CircuitPiece> visited = new HashSet<CircuitPiece>();
        Stack<CircuitPiece> pending = new Stack<CircuitPiece>();
        pending.Push(startPiece);
        while (pending.Count > 0)
        {
            CircuitPiece piece = pending.Pop();
            if (!visited.Add(piece))
            {
                continue;
            }

            for (int connector = 0;
                 connector < piece.ConnectorCount;
                 connector++)
            {
                CircuitPiece connectedPiece = GetConnectedStructurePiece(
                    piece,
                    connector,
                    includePendingPiece);
                if (connectedPiece != null && !visited.Contains(connectedPiece))
                {
                    pending.Push(connectedPiece);
                }
            }
        }

        return visited.Count == activePieceCount;
    }

    private CircuitPiece GetConnectedStructurePiece(
        CircuitPiece piece,
        int connector,
        bool includePendingPiece)
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
                    return otherPiece;
                }
            }
        }

        return null;
    }

    private bool HasConnectedScenePiece()
    {
        foreach (CircuitPiece piece in routePieces)
        {
            if (IsConnectedScenePiece(piece))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsConnectedScenePiece(CircuitPiece piece)
    {
        return piece != null &&
               routePieces.Contains(piece) &&
               !placedPieces.Contains(piece);
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

        if (IsPrimaryActionPressed() &&
            TryGetPointerPosition(out Vector2 pointerPosition))
        {
            placementState = PlacementState.Dragging;
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
            status = "Drag the piece and release the primary button to set its position.";
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

    private void UpdatePendingPiece(Vector2 pointerPosition)
    {
        if (!pendingPiecePositioner.TryPositionAtPointer(
                pendingPiece,
                pointerPosition,
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

        bool isInsideBuildArea =
            placementValidator.IsInsideBuildArea(pendingPiece);
        bool hasValidConnection = routePieces.Count == 0 ||
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
        if (routePieces.Count > 0 &&
            !HasConnectedScenePiece() &&
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
        Vector2 pointerPosition,
        out Vector3 dragOffset)
    {
        return pendingPiecePositioner.TryGetDragOffset(
            pendingPiece,
            pointerPosition,
            out dragOffset);
    }

    private string GetInvalidPlacementMessage()
    {
        if (!HasRequiredAnchorAlignment(true))
        {
            return routePieces.Count == 0
                ? "The first piece must be centered on START."
                : "The track must remain centered on START.";
        }

        return routePieces.Count == 0
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
        routePieces.Add(piece);
        ConsumePiece(piece);
        goalBinding.ReevaluateBinding();
        PiecePlaced?.Invoke(piece);
        bonusChallenge?.RegisterPlacedPiece(piece);

        pendingPlacement.Reset();
        if (bonusChallenge == null)
        {
            status = goalBinding.IsAdjustedPiece(piece)
                ? "Final piece placed. GOAL connection detected."
                : "Piece placed. Continue or press PLAY.";
        }
        else
        {
            status = HasCompleteAnchorAlignment()
                ? "Circuit closed at START. You can press PLAY."
                : "Piece placed. Continue building the closed circuit.";
        }
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
        if (bonusChallenge == null)
        {
            prize.gameObject.SetActive(true);
            prize.localScale = prizeInitialScale;
        }
        else
        {
            prize.gameObject.SetActive(false);
            bonusChallenge.BeginRun();
            bonusBallLeftStart = false;
        }

        ballTestController.Start(GetBallLaunchDirection(), launchSpeed,
            continuousBallImpulse ? ballDriveAcceleration : 0f);
        status = bonusChallenge == null
            ? "Test running: the ball must collect the prize."
            : "One lap: return to START through the bonus pieces.";
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
            status = bonusChallenge != null
                ? "Connect all bonus pieces and close the circuit at START before launching."
                : "Circuit incomplete: GOAL must be on the final piece.";
            return false;
        }

        return true;
    }

    private Vector3 GetBallLaunchDirection()
    {
        if (startAnchor != null)
        {
            float startTolerance = bonusChallenge == null && HasConnectedScenePiece()
                ? GetAnchorAlignmentAssistDistance()
                : BallPuzzlePlacementValidator.ConnectionPositionTolerance;
            foreach (CircuitPiece piece in routePieces)
            {
                if (piece == null || !piece.gameObject.activeSelf)
                {
                    continue;
                }

                for (int connector = 0;
                     connector < piece.ConnectorCount;
                     connector++)
                {
                    // Both START connectors are occupied on a closed bonus loop.
                    // Route order chooses the first placed piece as the launch side.
                    if ((bonusChallenge == null &&
                         placementValidator.IsPlacedConnectorOccupied(piece, connector)) ||
                        BallPuzzlePlacementValidator.HorizontalDistance(
                            piece.GetConnectorPosition(connector),
                            startAnchor.position) >
                        startTolerance)
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
        BallPuzzleBallTestResult result = ballTestController.Evaluate();
        switch (result)
        {
            case BallPuzzleBallTestResult.PrizeCollected:
                CompleteCircuitTest();
                return;
            case BallPuzzleBallTestResult.Fell:
                FinishTest("The ball fell off the track.");
                return;
            case BallPuzzleBallTestResult.TimedOut:
                FinishTest("The run ended.");
                return;
            case BallPuzzleBallTestResult.Stopped:
                FinishTest("The ball has stopped.");
                return;
        }

        bonusChallenge?.TrackBall();
        if (HasBallReturnedToBonusStart())
        {
            CompleteBonusTest();
        }
    }

    private bool HasBallReturnedToBonusStart()
    {
        if (bonusChallenge == null || startAnchor == null || ball == null)
        {
            return false;
        }

        float returnDistance = Mathf.Max(0.1f, bonusStartReturnDistance);
        float distance = BallPuzzlePlacementValidator.HorizontalDistance(
            ball.position,
            startAnchor.position);
        if (!bonusBallLeftStart)
        {
            if (distance > returnDistance * 1.5f)
            {
                bonusBallLeftStart = true;
            }

            return false;
        }

        return distance <= returnDistance &&
               ballSpawnPoint != null &&
               Mathf.Abs(ball.position.y - ballSpawnPoint.position.y) <=
                   returnDistance &&
               bonusChallenge.TargetCount > 0 &&
               bonusChallenge.SecuredCount == bonusChallenge.TargetCount;
    }

    private void UndoLastPlacedPiece()
    {
        if (placedPieces.Count == 0)
        {
            status = "There is no placed piece to undo.";
            return;
        }

        int lastIndex = placedPieces.Count - 1;
        CircuitPiece piece = placedPieces[lastIndex];
        placedPieces.RemoveAt(lastIndex);
        routePieces.Remove(piece);
        RestorePiece(piece);
        bonusChallenge?.UnregisterPiece(piece);
        PieceRemoved?.Invoke(piece);
        goalBinding.ReevaluateBinding();

        if (piece != null)
        {
            Destroy(piece.gameObject);
        }

        status = "Last placed piece undone.";
    }

    private void FinishTest(string reason)
    {
        FailTest(reason);
    }

    private void CompleteBonusTest()
    {
        ballTestController.Finish();
        bonusChallenge.CompleteRun();
        state = LevelState.Success;
        status = "Lap complete. All bonus pieces secured.";
        resultMessage = bonusChallenge.CreateResultMessage();
        LevelCompleted?.Invoke();
    }

    private void FailTest(string reason)
    {
        ballTestController.Finish();
        bonusChallenge?.AbortRun();
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
        bonusChallenge?.AbortRun();
        ballTestController.ResetBallForBuild();
        if (bonusChallenge == null)
        {
            prize.gameObject.SetActive(true);
            prize.localScale = prizeInitialScale;
        }
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
        routePieces.Clear();
        if (placedPiecesRoot != null)
        {
            placedPiecesRoot.localPosition = Vector3.zero;
        }
        straightRemaining = availableStraights;
        curveRemaining = availableCurves;
        halfStraightRemaining = availableHalfStraights;
        bonusLoanRemaining = availableBonusLoanPieces;
        state = LevelState.Build;
        ballTestController.ClearDuration();
        resultMessage = string.Empty;
        ballTestController.ResetBallForBuild();
        if (bonusChallenge == null)
        {
            prize.gameObject.SetActive(true);
            prize.localScale = prizeInitialScale;
        }
        else
        {
            prize.gameObject.SetActive(false);
            bonusChallenge.ResetChallenge();
        }
        status = bonusChallenge == null
            ? "Layout reset. Choose a piece to place it."
            : "Layout reset. Build a closed route back to START.";
        LayoutReset?.Invoke();
    }

    private void AnimatePrize()
    {
        if (bonusChallenge != null ||
            prize == null ||
            !prize.gameObject.activeSelf)
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

        if (routePieces.Count == 0 &&
            TryGetClosestAnchorOffset(startAnchor, out Vector3 startOffset))
        {
            ApplyPendingPieceOffset(startOffset);
            return true;
        }

        bool foundConnection = false;
        float closestDistance = float.PositiveInfinity;
        Vector3 bestOffset = Vector3.zero;
        CircuitPiece bestPlacedPiece = null;
        int bestCandidateConnector = -1;
        int bestPlacedConnector = -1;
        bool canFineAlignToRoute = HasConnectedScenePiece();

        for (int candidateConnector = 0;
             candidateConnector < pendingPiece.ConnectorCount;
             candidateConnector++)
        {
            foreach (CircuitPiece placedPiece in routePieces)
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
                        !canFineAlignToRoute &&
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
                    bestPlacedPiece = placedPiece;
                    bestCandidateConnector = candidateConnector;
                    bestPlacedConnector = placedConnector;
                }
            }
        }

        if (!foundConnection)
        {
            return false;
        }

        if (canFineAlignToRoute)
        {
            AlignPendingPieceToConnector(
                bestCandidateConnector,
                bestPlacedPiece,
                bestPlacedConnector);
            return true;
        }

        ApplyPendingPieceOffset(bestOffset);
        return true;
    }

    private void AlignPendingPieceToConnector(
        int pendingConnector,
        CircuitPiece targetPiece,
        int targetConnector)
    {
        Vector3 pendingDirection = BallPuzzlePlacementValidator.Flatten(
            pendingPiece.GetConnectorDirection(pendingConnector));
        Vector3 targetDirection = BallPuzzlePlacementValidator.Flatten(
            targetPiece.GetConnectorDirection(targetConnector));
        float yawCorrection = Vector3.SignedAngle(
            pendingDirection,
            -targetDirection,
            Vector3.up);

        Vector3 connectorPosition =
            pendingPiece.GetConnectorPosition(pendingConnector);
        Quaternion rotationCorrection = Quaternion.AngleAxis(
            yawCorrection,
            Vector3.up);
        Vector3 rootOffset =
            pendingPiece.transform.position - connectorPosition;
        pendingPiece.transform.SetPositionAndRotation(
            connectorPosition + rotationCorrection * rootOffset,
            rotationCorrection * pendingPiece.transform.rotation);

        Vector3 positionCorrection =
            targetPiece.GetConnectorPosition(targetConnector) -
            pendingPiece.GetConnectorPosition(pendingConnector);
        ApplyPendingPieceOffset(positionCorrection);
        Physics.SyncTransforms();
    }

    private bool TryGetClosestAnchorOffset(
        Transform anchor,
        out Vector3 bestOffset,
        bool requireAssistDistance = true)
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
            if ((requireAssistDistance &&
                 distance > GetAnchorAlignmentAssistDistance()) ||
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
        if (prefab == bonusLoanPiecePrefab)
        {
            return bonusLoanRemaining;
        }

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
        if (IsBonusLoanPiece(piece))
        {
            bonusLoanRemaining = Mathf.Max(0, bonusLoanRemaining - 1);
            return;
        }

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
               HasValidBonusLoan() &&
               HasLockedPieceCards();
    }

    private bool HasValidBonusLoan()
    {
        return bonusChallenge == null ||
               (bonusLoanPiecePrefab != null &&
                bonusLoanPieceCard != null &&
                bonusLoanPieceCard.Button != null);
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
        if (bonusLoanPieceCard != null)
        {
            bonusLoanPieceCard.PointerPressed += SelectBonusLoanPiece;
        }
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
        if (bonusLoanPieceCard != null)
        {
            bonusLoanPieceCard.PointerPressed -= SelectBonusLoanPiece;
        }
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

    private void RestorePiece(CircuitPiece piece)
    {
        if (piece == null)
        {
            return;
        }

        if (IsBonusLoanPiece(piece))
        {
            bonusLoanRemaining++;
            return;
        }

        switch (piece.PieceType)
        {
            case CircuitPieceType.Straight:
                straightRemaining++;
                break;
            case CircuitPieceType.Curve45Right:
                curveRemaining++;
                break;
            case CircuitPieceType.HalfStraight:
                halfStraightRemaining++;
                break;
        }
    }

    private void SelectBonusLoanPiece()
    {
        if (bonusChallenge != null &&
            bonusLoanPiecePrefab != null &&
            bonusLoanPieceCard != null &&
            bonusLoanPieceCard.ActiveInPalette &&
            !bonusLoanPieceCard.LockedInPalette)
        {
            BeginPlacement(bonusLoanPiecePrefab, bonusLoanPieceCard);
        }
    }

    private bool IsBonusLoanPiece(CircuitPiece piece)
    {
        return bonusChallenge != null &&
               bonusLoanPiecePrefab != null &&
               piece != null &&
               piece.PieceType == bonusLoanPiecePrefab.PieceType;
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

        bool keepFirstBonusPieceOnStart =
            bonusChallenge != null &&
            routePieces.Count == 0 &&
            HasOpenStructureConnectorAt(
                startAnchor,
                Vector3.zero,
                true);
        int orientationCount = Mathf.Max(1, Mathf.RoundToInt(360f / rotationStep));
        pendingRotationIndex =
            (pendingRotationIndex + direction + orientationCount) %
            orientationCount;

        pendingPiecePositioner.RotateAroundPivot(
            pendingPiece,
            pendingRotationPivotLocal,
            pendingRotationIndex);
        pendingPiecePositioner.RestOnBuildSurface(pendingPiece);
        if (keepFirstBonusPieceOnStart &&
            TryGetClosestAnchorOffset(
                startAnchor,
                out Vector3 startOffset,
                false))
        {
            ApplyPendingPieceOffset(startOffset);
        }
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

        bool hasLoadableNextLevel =
            !string.IsNullOrWhiteSpace(nextLevelScene) &&
            Application.CanStreamedLevelBeLoaded(nextLevelScene);
        uiPresenter.Refresh(new BallPuzzleLevelUiState(
            isBuilding,
            isTesting,
            isShowingResult,
            state == LevelState.Success,
            state == LevelState.ChallengeIncomplete,
            countdownExpired,
            hasLoadableNextLevel,
            bonusChallenge != null,
            bonusChallenge != null ? bonusChallenge.ProgressText : string.Empty,
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
        RefreshBonusLoanCard(selectedPieceType);
    }

    private void RefreshBonusLoanCard(CircuitPieceType? selectedPieceType)
    {
        if (bonusChallenge == null || bonusLoanPieceCard == null ||
            bonusLoanPiecePrefab == null)
        {
            return;
        }

        bonusLoanPieceCard.ApplyInspectorActiveState();
        bool selected = selectedPieceType.HasValue &&
                        selectedPieceType.Value == bonusLoanPiecePrefab.PieceType;
        bool canSelect = state == LevelState.Build &&
                         pendingPiece == null &&
                         bonusLoanRemaining > 0;
        bonusLoanPieceCard.SetState(
            true,
            canSelect,
            selected,
            bonusLoanRemaining);
    }

    private void InitializeCountdown()
    {
        if (!useCountdown)
        {
            countdownRemaining = initialCountdownSeconds;
            levelEntryCountdownSeconds = initialCountdownSeconds;
            countdownRunning = false;
            countdownExpired = false;
            return;
        }

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
        if (useCountdown && !countdownExpired && countdownRemaining > 0f)
        {
            countdownRunning = true;
        }
    }

    private void StopCountdownWhenPlayBecomesAvailable()
    {
        if (!useCountdown ||
            state != LevelState.Build ||
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
        if (!useCountdown ||
            !countdownRunning ||
            countdownExpired ||
            state == LevelState.Success)
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
