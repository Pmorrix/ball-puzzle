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
    private const float ConnectionPositionTolerance = 0.01f;
    private const float ConnectionDirectionDot = -0.999f;
    private const float PieceCardSpawnMargin = 24f;
    private const int PlacementHaloTextureSize = 64;
    private const float PlacementHaloPadding = 2.4f;
    private const float PlacementHaloHeightOffset = 0.04f;
    private const float PlacementHaloPulseSpeed = 3.8f;
    private const float PlacementHaloPulseAmount = 0.06f;
    private const string LastPlayedLevelKey = "BallPuzzleLastPlayedLevel";
    private const string TotalCompletionTimeKey = "BallPuzzleTotalCompletionTime";
    private const string LevelCompletionTimeKeyPrefix = "BallPuzzleCompletionTime.";

    private static readonly Color InteractionIndicatorColor = new Color(0.23f, 0.78f, 0.91f, 0.82f);
    private static readonly Color ValidPlacementIndicatorColor = new Color(0.31f, 0.84f, 0.60f, 0.82f);
    private static readonly Color InvalidPlacementIndicatorColor = new Color(0.95f, 0.42f, 0.42f, 0.82f);

    public event Action<CircuitPiece> PlacementStarted;
    public event Action<CircuitPiece, bool> PlacementPositioned;
    public event Action<CircuitPiece> PieceRotated;
    public event Action<CircuitPiece> PiecePlaced;
    public event Action PlacementCancelled;
    public event Action TestStarted;
    public event Action LayoutReset;
    public event Action LevelCompleted;

    public int PlacedPieceCount => placedPieces.Count;
    public bool IsPendingPieceMoving =>
        pendingPiece != null &&
        (placementState == PlacementState.Selected ||
         placementState == PlacementState.Dragging);

    private enum LevelState
    {
        Build,
        Testing,
        Paused,
        Failure,
        Success
    }

    private enum PlacementState
    {
        None,
        Selected,
        Dragging,
        Positioned
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
    [SerializeField, Min(0.1f)] private float targetTestDuration = 6f;

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
    [SerializeField] private GameObject buildControlsPanel;
    [SerializeField] private GameObject buildActionsPanel;
    [SerializeField] private GameObject testingControlsPanel;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private GameObject rotationControlsPanel;
    [SerializeField] private GameObject playConfirmationDialog;
    [SerializeField] private Button confirmPlayButton;
    [SerializeField] private Button cancelPlayButton;
    [SerializeField] private Button straightButton;
    [SerializeField] private Button curveButton;
    [SerializeField] private Button rotateYButton;
    [SerializeField] private Button rotateYCounterClockwiseButton;
    [SerializeField] private Button placeButton;
    [SerializeField] private Button testButton;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button stopButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button editButton;
    [SerializeField] private Button resultResetButton;
    [SerializeField] private TMP_Text straightButtonLabel;
    [SerializeField] private TMP_Text curveButtonLabel;
    [SerializeField] private TMP_Text pauseButtonLabel;
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
    private GameObject placementIndicator;
    private SpriteRenderer placementIndicatorRenderer;
    private Texture2D placementIndicatorTexture;
    private Sprite placementIndicatorSprite;
    private Vector3 placementIndicatorBaseScale;
    private Color placementIndicatorBaseColor;
    private CircuitPiece pendingPiece;
    private PlacementState placementState = PlacementState.None;
    private int pendingRotationIndex;
    private Vector3 pendingDragOffset;
    private Vector3 pendingRotationPivotLocal;
    private bool pendingHasValidPosition;
    private int straightRemaining;
    private int curveRemaining;
    private int halfStraightRemaining;
    private LevelState state = LevelState.Build;
    private string status = "Choose a piece to start placing it.";
    private string resultMessage = string.Empty;
    private float testStartedAt;
    private float lastTestDuration;
    private float stoppedAt = -1f;
    private float pauseStartedAt = -1f;
    private bool hasTestDuration;
    private Vector3 pausedLinearVelocity;
    private Vector3 pausedAngularVelocity;
    private Vector3 prizeInitialScale;
    private Vector3 prizeInitialPosition;
    private CircuitPiece adjustedGoalPiece;
    private int adjustedGoalConnectorIndex = -1;
    private bool hasAdjustedGoal;

    private void Awake()
    {
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

        RememberCurrentLevel();
        ApplyPieceCardActiveStates();
        EnablePanelDragging(rotationControlsPanel);
        GameObject piecePalettePanel = paletteSummaryLabel.transform.parent.gameObject;
        Transform topBar = buildControlsPanel.transform.parent.Find("Top Bar");
        LinkDraggablePanels(
            piecePalettePanel,
            topBar != null ? topBar.gameObject : null);
        HidePlayConfirmationDialog();
        WireUiEvents();

        GameObject piecesRoot = new GameObject("Placed Puzzle Pieces");
        piecesRoot.transform.SetParent(transform, false);
        placedPiecesRoot = piecesRoot.transform;
        CreatePlacementIndicator();

        straightRemaining = availableStraights;
        curveRemaining = availableCurves;
        halfStraightRemaining = availableHalfStraights;
        prizeInitialScale = prize.localScale;
        prizeInitialPosition = prize.position;
        ResetBallForBuild();
        RefreshUi();
    }

    private static void RememberCurrentLevel()
    {
        string activeSceneName = SceneManager.GetActiveScene().name;
        if (string.IsNullOrWhiteSpace(activeSceneName))
        {
            return;
        }

        PlayerPrefs.SetString(LastPlayedLevelKey, activeSceneName);
        PlayerPrefs.Save();
    }

    private void OnDestroy()
    {
        UnwireUiEvents();
        if (placementIndicatorSprite != null)
        {
            Destroy(placementIndicatorSprite);
        }
        if (placementIndicatorTexture != null)
        {
            Destroy(placementIndicatorTexture);
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

    private static void LinkDraggablePanels(GameObject firstPanel, GameObject secondPanel)
    {
        DraggableUIPanel firstDraggable = EnablePanelDragging(firstPanel);
        DraggableUIPanel secondDraggable = EnablePanelDragging(secondPanel);
        if (firstDraggable == null || secondDraggable == null)
        {
            return;
        }

        firstDraggable.SetLinkedPanel(secondPanel.transform as RectTransform);
        secondDraggable.SetLinkedPanel(firstPanel.transform as RectTransform);
    }

    private void Update()
    {
        AnimatePrize();
        AnimatePlacementIndicator();

        if (state == LevelState.Build)
        {
            UpdateBuildMode();
        }
        else if (state == LevelState.Testing)
        {
            UpdateBallTest();
        }

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

        if (pointerOverUi ||
            !mouse.leftButton.wasPressedThisFrame ||
            !TryGetPendingPieceDragOffset(pointerPosition, out pendingDragOffset))
        {
            return;
        }

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
        RecalculateAdjustedGoalPosition();
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
                if (piece == adjustedGoalPiece &&
                    connector == adjustedGoalConnectorIndex)
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
                    HorizontalDistance(
                        piece.GetConnectorPosition(connector) + positionOffset,
                        anchor.position) <= ConnectionPositionTolerance)
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
                if (AreConnectorsAligned(
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
               HasValidAdjustedGoal();
    }

    private void ReevaluateAdjustedGoalBinding()
    {
        if (TryGetBoundGoalCenter(out Vector3 boundCenter))
        {
            ApplyAdjustedGoalPosition(boundCenter);
            hasAdjustedGoal = true;
            return;
        }

        adjustedGoalPiece = null;
        adjustedGoalConnectorIndex = -1;
        if (TryGetClosestFreeGoalCandidate(
                out CircuitPiece candidatePiece,
                out int candidateConnector,
                out Vector3 candidateCenter))
        {
            adjustedGoalPiece = candidatePiece;
            adjustedGoalConnectorIndex = candidateConnector;
            ApplyAdjustedGoalPosition(candidateCenter);
            hasAdjustedGoal = true;
            return;
        }

        RestoreInitialGoalPosition();
    }

    private void RecalculateAdjustedGoalPosition()
    {
        if (TryGetStoredGoalCenter(out Vector3 worldCenter))
        {
            ApplyAdjustedGoalPosition(worldCenter);
            return;
        }

        RestoreInitialGoalPosition();
    }

    private void ApplyAdjustedGoalPosition(Vector3 worldCenter)
    {
        if (prize == null)
        {
            return;
        }

        Vector3 position = prize.position;
        position.x = worldCenter.x;
        position.z = worldCenter.z;
        prize.position = position;
    }

    private bool HasValidAdjustedGoal()
    {
        if (!hasAdjustedGoal || !TryGetBoundGoalCenter(out Vector3 worldCenter))
        {
            return false;
        }

        return HorizontalDistance(prize.position, worldCenter) <=
               ConnectionPositionTolerance;
    }

    private bool TryGetBoundGoalCenter(out Vector3 worldCenter)
    {
        if (!TryGetStoredGoalCenter(out worldCenter) ||
            !IsPieceConnectedToStart(adjustedGoalPiece))
        {
            return false;
        }

        return true;
    }

    private bool TryGetStoredGoalCenter(out Vector3 worldCenter)
    {
        worldCenter = Vector3.zero;
        if (adjustedGoalPiece == null ||
            !adjustedGoalPiece.gameObject.activeSelf ||
            !placedPieces.Contains(adjustedGoalPiece) ||
            adjustedGoalConnectorIndex < 0 ||
            adjustedGoalConnectorIndex >= adjustedGoalPiece.ConnectorCount)
        {
            return false;
        }

        worldCenter = adjustedGoalPiece.GetConnectorPosition(
            adjustedGoalConnectorIndex);
        return true;
    }

    private bool TryGetClosestFreeGoalCandidate(
        out CircuitPiece candidatePiece,
        out int candidateConnector,
        out Vector3 candidateCenter)
    {
        candidatePiece = null;
        candidateConnector = -1;
        candidateCenter = Vector3.zero;
        float closestDistance = float.PositiveInfinity;
        float assistDistance = GetAnchorAlignmentAssistDistance();
        HashSet<CircuitPiece> startComponent = GetStartConnectedPieces();

        foreach (CircuitPiece piece in startComponent)
        {
            if (piece == null)
            {
                continue;
            }

            for (int connector = 0;
                 connector < piece.ConnectorCount;
                 connector++)
            {
                if (IsPlacedConnectorOccupied(piece, connector))
                {
                    continue;
                }

                Vector3 connectorPosition =
                    piece.GetConnectorPosition(connector);
                if (startAnchor != null &&
                    HorizontalDistance(
                        connectorPosition,
                        startAnchor.position) <= ConnectionPositionTolerance)
                {
                    continue;
                }

                float distance = HorizontalDistance(
                    connectorPosition,
                    prizeInitialPosition);
                if (distance > assistDistance ||
                    distance >= closestDistance)
                {
                    continue;
                }

                candidatePiece = piece;
                candidateConnector = connector;
                candidateCenter = connectorPosition;
                closestDistance = distance;
            }
        }

        return candidatePiece != null;
    }

    private bool IsPieceConnectedToStart(CircuitPiece piece)
    {
        return piece != null && GetStartConnectedPieces().Contains(piece);
    }

    private HashSet<CircuitPiece> GetStartConnectedPieces()
    {
        HashSet<CircuitPiece> visited = new HashSet<CircuitPiece>();
        Queue<CircuitPiece> pending = new Queue<CircuitPiece>();

        foreach (CircuitPiece piece in placedPieces)
        {
            if (piece == null || !HasOpenConnectorAtStart(piece))
            {
                continue;
            }

            visited.Add(piece);
            pending.Enqueue(piece);
        }

        while (pending.Count > 0)
        {
            CircuitPiece current = pending.Dequeue();
            foreach (CircuitPiece other in placedPieces)
            {
                if (other == null ||
                    visited.Contains(other) ||
                    !ArePiecesConnected(current, other))
                {
                    continue;
                }

                visited.Add(other);
                pending.Enqueue(other);
            }
        }

        return visited;
    }

    private void RestoreInitialGoalPosition()
    {
        hasAdjustedGoal = false;
        if (prize != null)
        {
            prize.position = prizeInitialPosition;
        }
    }

    private bool HasOpenConnectorAtStart(CircuitPiece piece)
    {
        if (piece == null || startAnchor == null)
        {
            return false;
        }

        for (int connector = 0;
             connector < piece.ConnectorCount;
             connector++)
        {
            if (!IsPlacedConnectorOccupied(piece, connector) &&
                HorizontalDistance(
                    piece.GetConnectorPosition(connector),
                    startAnchor.position) <= ConnectionPositionTolerance)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ArePiecesConnected(
        CircuitPiece firstPiece,
        CircuitPiece secondPiece)
    {
        for (int firstConnector = 0;
             firstConnector < firstPiece.ConnectorCount;
             firstConnector++)
        {
            for (int secondConnector = 0;
                 secondConnector < secondPiece.ConnectorCount;
                 secondConnector++)
            {
                if (AreConnectorsAligned(
                        firstPiece,
                        firstConnector,
                        secondPiece,
                        secondConnector))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private float GetAnchorAlignmentAssistDistance()
    {
        return Mathf.Max(
                   connectionReleaseAssistDistance * 2f,
                   placementGridSize * 2f) +
               ConnectionPositionTolerance;
    }

    private void BeginPlacement(
        CircuitPiece prefab,
        PieceSelectionCard sourceCard)
    {
        if (state != LevelState.Build || prefab == null || GetRemainingCount(prefab) <= 0)
        {
            return;
        }

        CancelPendingPiece(false);
        pendingPiece = Instantiate(prefab, placedPiecesRoot);
        pendingPiece.name = prefab.DisplayName + " (pendiente)";
        pendingPiece.ClearTint();

        placementState = PlacementState.Selected;
        pendingRotationIndex = 0;
        pendingDragOffset = Vector3.zero;
        pendingRotationPivotLocal = pendingPiece.transform.InverseTransformPoint(
            pendingPiece.GetRenderBounds().center);

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
        Vector3 stagingPosition = new Vector3(
            SnapToGrid(buildAreaCenter.x),
            buildSurfaceHeight,
            SnapToGrid(buildAreaCenter.y));
        pendingPiece.transform.SetPositionAndRotation(
            stagingPosition,
            GetPendingRotation());
        RestPendingPieceOnBuildSurface();
        EvaluatePendingPlacement();
    }

    private void UpdatePendingPiece(Vector2 mousePosition)
    {
        if (!TryGetBuildPoint(mousePosition, out Vector3 buildPoint))
        {
            SetPendingPlacementValidity(false);
            return;
        }

        buildPoint += pendingDragOffset;
        Vector3 snappedPosition = new Vector3(
            SnapToGrid(buildPoint.x),
            buildSurfaceHeight,
            SnapToGrid(buildPoint.z));
        pendingPiece.transform.SetPositionAndRotation(snappedPosition, GetPendingRotation());
        RestPendingPieceOnBuildSurface();
        EvaluatePendingPlacement();
    }

    private void MovePendingPieceFullyRightOf(Vector2 minimumScreenPosition)
    {
        const int MaximumCorrectionIterations = 3;
        Vector2 currentScreenPosition = minimumScreenPosition;

        for (int iteration = 0;
             iteration < MaximumCorrectionIterations;
             iteration++)
        {
            float pieceLeftEdge = GetPendingPieceLeftScreenEdge();
            float horizontalCorrection = minimumScreenPosition.x - pieceLeftEdge;
            if (horizontalCorrection <= 0.5f)
            {
                break;
            }

            Vector2 correctedScreenPosition =
                currentScreenPosition + Vector2.right * horizontalCorrection;
            if (!TryGetBuildPoint(
                    currentScreenPosition,
                    out Vector3 currentBuildPoint) ||
                !TryGetBuildPoint(
                    correctedScreenPosition,
                    out Vector3 correctedBuildPoint))
            {
                break;
            }

            Vector3 worldCorrection = correctedBuildPoint - currentBuildPoint;
            worldCorrection.y = 0f;
            pendingPiece.transform.position += worldCorrection;
            RestPendingPieceOnBuildSurface();
            currentScreenPosition = correctedScreenPosition;
        }

        EvaluatePendingPlacement();
    }

    private float GetPendingPieceLeftScreenEdge()
    {
        Bounds bounds = pendingPiece.GetRenderBounds();
        Vector3 minimum = bounds.min;
        Vector3 maximum = bounds.max;
        float leftEdge = float.PositiveInfinity;

        for (int x = 0; x < 2; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                for (int z = 0; z < 2; z++)
                {
                    Vector3 corner = new Vector3(
                        x == 0 ? minimum.x : maximum.x,
                        y == 0 ? minimum.y : maximum.y,
                        z == 0 ? minimum.z : maximum.z);
                    Vector3 screenCorner =
                        buildCamera.WorldToScreenPoint(corner);
                    if (screenCorner.z > 0f)
                    {
                        leftEdge = Mathf.Min(leftEdge, screenCorner.x);
                    }
                }
            }
        }

        return leftEdge;
    }

    private void EvaluatePendingPlacement()
    {
        if (pendingPiece == null || !pendingPiece.gameObject.activeSelf)
        {
            SetPendingPlacementValidity(false);
            return;
        }

        bool isInsideBuildArea = IsInsideBuildArea(pendingPiece);
        bool hasValidConnection = placedPieces.Count == 0 || HasValidConnection(pendingPiece);
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
            IsInsideBuildArea(pendingPiece) &&
            HasValidConnection(pendingPiece))
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
        dragOffset = Vector3.zero;
        Ray ray = buildCamera.ScreenPointToRay(mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            float.PositiveInfinity,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
        bool hitPendingPiece = false;
        float closestDistance = float.PositiveInfinity;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
            {
                continue;
            }

            Transform hitTransform = hit.collider.transform;
            bool belongsToPendingPiece =
                hitTransform == pendingPiece.transform ||
                hitTransform.IsChildOf(pendingPiece.transform);
            if (!belongsToPendingPiece || hit.distance >= closestDistance)
            {
                continue;
            }

            hitPendingPiece = true;
            closestDistance = hit.distance;
        }

        if (!hitPendingPiece ||
            !TryGetBuildPoint(mousePosition, out Vector3 buildPoint))
        {
            return false;
        }

        dragOffset = pendingPiece.transform.position - buildPoint;
        dragOffset.y = 0f;
        return true;
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

    private Quaternion GetPendingRotation()
    {
        return Quaternion.Euler(0f, pendingRotationIndex * rotationStep, 0f);
    }

    private float SnapToGrid(float value)
    {
        return Mathf.Round(value / placementGridSize) * placementGridSize;
    }

    private void RestPendingPieceOnBuildSurface()
    {
        Bounds bounds = pendingPiece.GetRenderBounds();
        Vector3 position = pendingPiece.transform.position;
        position.y += buildSurfaceHeight - bounds.min.y;
        pendingPiece.transform.position = position;
    }

    private void SetPendingPlacementValidity(bool isValid)
    {
        pendingHasValidPosition = isValid;
        UpdatePlacementIndicator();
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
        HidePlacementIndicator();
        placedPieces.Add(piece);
        ConsumePiece(piece);
        ReevaluateAdjustedGoalBinding();
        PiecePlaced?.Invoke(piece);

        pendingPiece = null;
        placementState = PlacementState.None;
        pendingRotationIndex = 0;
        pendingDragOffset = Vector3.zero;
        pendingRotationPivotLocal = Vector3.zero;
        pendingHasValidPosition = false;
        status = adjustedGoalPiece == piece && hasAdjustedGoal
            ? "Final piece placed. GOAL centered on the track."
            : "Piece placed. Continue or press PLAY.";
    }

    private void CancelPendingPiece(bool updateStatus = true)
    {
        if (pendingPiece != null)
        {
            Destroy(pendingPiece.gameObject);
        }

        HidePlacementIndicator();
        pendingPiece = null;
        placementState = PlacementState.None;
        pendingRotationIndex = 0;
        pendingDragOffset = Vector3.zero;
        pendingRotationPivotLocal = Vector3.zero;
        pendingHasValidPosition = false;
        PlacementCancelled?.Invoke();
        if (updateStatus)
        {
            status = "Placement cancelled.";
        }
    }

    private void CreatePlacementIndicator()
    {
        placementIndicator = new GameObject("Placement Halo");
        placementIndicator.transform.SetParent(transform, false);
        placementIndicator.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        placementIndicatorRenderer = placementIndicator.AddComponent<SpriteRenderer>();
        placementIndicatorRenderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        placementIndicatorRenderer.receiveShadows = false;
        placementIndicatorRenderer.sortingOrder = 20;

        placementIndicatorTexture = new Texture2D(
            PlacementHaloTextureSize,
            PlacementHaloTextureSize,
            TextureFormat.RGBA32,
            false)
        {
            name = "Placement Halo Texture (Runtime)",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        Color[] pixels = new Color[
            PlacementHaloTextureSize * PlacementHaloTextureSize];
        for (int y = 0; y < PlacementHaloTextureSize; y++)
        {
            for (int x = 0; x < PlacementHaloTextureSize; x++)
            {
                float normalizedX =
                    (x + 0.5f) / PlacementHaloTextureSize * 2f - 1f;
                float normalizedY =
                    (y + 0.5f) / PlacementHaloTextureSize * 2f - 1f;
                float distance = Mathf.Sqrt(
                    normalizedX * normalizedX +
                    normalizedY * normalizedY);
                float alpha = 1f - Mathf.SmoothStep(0.12f, 1f, distance);
                alpha = Mathf.Pow(alpha, 0.72f);
                pixels[y * PlacementHaloTextureSize + x] =
                    new Color(1f, 1f, 1f, alpha);
            }
        }
        placementIndicatorTexture.SetPixels(pixels);
        placementIndicatorTexture.Apply(false, false);

        placementIndicatorSprite = Sprite.Create(
            placementIndicatorTexture,
            new Rect(
                0f,
                0f,
                PlacementHaloTextureSize,
                PlacementHaloTextureSize),
            new Vector2(0.5f, 0.5f),
            PlacementHaloTextureSize);
        placementIndicatorSprite.name = "Placement Halo Sprite (Runtime)";
        placementIndicatorSprite.hideFlags = HideFlags.HideAndDontSave;
        placementIndicatorRenderer.sprite = placementIndicatorSprite;
        placementIndicator.SetActive(false);
    }

    private void UpdatePlacementIndicator()
    {
        if (placementIndicator == null ||
            placementIndicatorRenderer == null ||
            pendingPiece == null ||
            !pendingPiece.gameObject.activeSelf)
        {
            HidePlacementIndicator();
            return;
        }

        Bounds bounds = pendingPiece.GetRenderBounds();
        Vector3 center = bounds.center;
        center.y = bounds.min.y + PlacementHaloHeightOffset;
        placementIndicator.transform.position = center;
        placementIndicatorBaseScale = new Vector3(
            Mathf.Max(PlacementHaloPadding, bounds.size.x + PlacementHaloPadding * 2f),
            Mathf.Max(PlacementHaloPadding, bounds.size.z + PlacementHaloPadding * 2f),
            1f);

        bool showPlacementValidity =
            placementState == PlacementState.Dragging ||
            placementState == PlacementState.Positioned;
        placementIndicatorBaseColor = showPlacementValidity
            ? pendingHasValidPosition
                ? ValidPlacementIndicatorColor
                : InvalidPlacementIndicatorColor
            : InteractionIndicatorColor;
        placementIndicator.SetActive(true);
        AnimatePlacementIndicator();
    }

    private void AnimatePlacementIndicator()
    {
        if (placementIndicator == null ||
            placementIndicatorRenderer == null ||
            !placementIndicator.activeSelf)
        {
            return;
        }

        float wave = (Mathf.Sin(Time.unscaledTime * PlacementHaloPulseSpeed) + 1f) * 0.5f;
        float scale = 1f + Mathf.Lerp(
            -PlacementHaloPulseAmount,
            PlacementHaloPulseAmount,
            wave);
        placementIndicator.transform.localScale = new Vector3(
            placementIndicatorBaseScale.x * scale,
            placementIndicatorBaseScale.y * scale,
            1f);

        Color animatedColor = placementIndicatorBaseColor;
        animatedColor.a *= Mathf.Lerp(0.78f, 1f, wave);
        placementIndicatorRenderer.color = animatedColor;
    }

    private void HidePlacementIndicator()
    {
        if (placementIndicator != null && placementIndicator.activeSelf)
        {
            placementIndicator.SetActive(false);
        }
    }

    private void RequestStartBallTest()
    {
        if (state != LevelState.Build ||
            pendingPiece != null ||
            placedPieces.Count == 0)
        {
            StartBallTest();
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
        if (state != LevelState.Build || pendingPiece != null)
        {
            status = "Finish or cancel the pending piece before testing.";
            return;
        }
        if (placedPieces.Count == 0)
        {
            status = "Place at least one piece before testing.";
            return;
        }

        state = LevelState.Testing;
        testStartedAt = Time.time;
        lastTestDuration = 0f;
        hasTestDuration = true;
        stoppedAt = -1f;
        pauseStartedAt = -1f;
        resultMessage = string.Empty;
        prize.gameObject.SetActive(true);
        prize.localScale = prizeInitialScale;

        ball.gameObject.SetActive(true);
        ball.transform.SetPositionAndRotation(ballSpawnPoint.position, ballSpawnPoint.rotation);
        ball.isKinematic = false;
        ball.angularVelocity = Vector3.zero;
        ball.linearVelocity = GetBallLaunchDirection() * launchSpeed;
        ball.WakeUp();
        status = "Test running: the ball must collect the prize.";
        TestStarted?.Invoke();
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
                    if (IsPlacedConnectorOccupied(piece, connector) ||
                        HorizontalDistance(
                            piece.GetConnectorPosition(connector),
                            startAnchor.position) > ConnectionPositionTolerance)
                    {
                        continue;
                    }

                    Vector3 launchDirection =
                        -Flatten(piece.GetConnectorDirection(connector));
                    if (launchDirection.sqrMagnitude > Mathf.Epsilon)
                    {
                        return launchDirection;
                    }
                }
            }
        }

        return ballSpawnPoint.forward;
    }

    private void TogglePause()
    {
        if (state == LevelState.Testing)
        {
            // Conserva el movimiento para continuar desde el mismo punto.
            pausedLinearVelocity = ball.linearVelocity;
            pausedAngularVelocity = ball.angularVelocity;
            FreezeBall();
            pauseStartedAt = Time.time;
            state = LevelState.Paused;
            status = "PAUSED - press RESUME to continue.";
            return;
        }

        if (state != LevelState.Paused)
        {
            return;
        }

        // Descuenta del test todo el tiempo que la bola estuvo pausada.
        float pausedDuration = Time.time - pauseStartedAt;
        testStartedAt += pausedDuration;
        if (stoppedAt >= 0f)
        {
            stoppedAt += pausedDuration;
        }

        ball.isKinematic = false;
        ball.linearVelocity = pausedLinearVelocity;
        ball.angularVelocity = pausedAngularVelocity;
        ball.WakeUp();
        pauseStartedAt = -1f;
        state = LevelState.Testing;
        status = "Test running: the ball must collect the prize.";
    }

    private void UpdateBallTest()
    {
        if (Vector3.Distance(ball.position, prize.position) <= prizeCollectionDistance)
        {
            CompleteLevel();
            return;
        }
        if (ball.position.y < fallHeight)
        {
            FailTest("The ball fell off the track.");
            return;
        }
        if (Time.time - testStartedAt >= maximumTestDuration)
        {
            FailTest("The test timed out.");
            return;
        }

        if (Time.time - testStartedAt > 2f && ball.linearVelocity.sqrMagnitude < 0.04f)
        {
            if (stoppedAt < 0f)
            {
                stoppedAt = Time.time;
            }
            else if (Time.time - stoppedAt >= stoppedDuration)
            {
                FailTest("The ball has stopped.");
            }
        }
        else
        {
            stoppedAt = -1f;
        }
    }

    private void FailTest(string reason)
    {
        CaptureCurrentTestDuration();
        FreezeBall();
        state = LevelState.Failure;
        status = reason;
        resultMessage = GetResultMessage();
    }

    private void CompleteLevel()
    {
        CaptureCurrentTestDuration();
        RegisterCompletedLevelTime();
        FreezeBall();
        state = LevelState.Success;
        status = "Prize collected! Level complete.";
        resultMessage = GetResultMessage();
        prize.localScale = prizeInitialScale * 1.35f;
        LevelCompleted?.Invoke();
    }

    private void RetryTest()
    {
        state = LevelState.Build;
        ResetBallForBuild();
        StartBallTest();
    }

    private void ReturnToBuild()
    {
        CaptureCurrentTestDuration();
        state = LevelState.Build;
        ResetBallForBuild();
        prize.gameObject.SetActive(true);
        prize.localScale = prizeInitialScale;
        status = "Adjust the layout and press PLAY again.";
    }

    private void ExitToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    private void ResetLayout()
    {
        CancelPendingPiece(false);
        adjustedGoalPiece = null;
        adjustedGoalConnectorIndex = -1;
        hasAdjustedGoal = false;
        prize.position = prizeInitialPosition;
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
        hasTestDuration = false;
        lastTestDuration = 0f;
        resultMessage = string.Empty;
        ResetBallForBuild();
        prize.gameObject.SetActive(true);
        prize.localScale = prizeInitialScale;
        status = "Layout reset. Choose a piece to place it.";
        LayoutReset?.Invoke();
    }

    private void FreezeBall()
    {
        ball.linearVelocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;
        ball.isKinematic = true;
    }

    private void ResetBallForBuild()
    {
        if (!ball.isKinematic)
        {
            ball.linearVelocity = Vector3.zero;
            ball.angularVelocity = Vector3.zero;
        }

        ball.isKinematic = true;
        ball.transform.SetPositionAndRotation(ballSpawnPoint.position, ballSpawnPoint.rotation);
        ball.gameObject.SetActive(true);
        pauseStartedAt = -1f;
        pausedLinearVelocity = Vector3.zero;
        pausedAngularVelocity = Vector3.zero;
    }

    private void AnimatePrize()
    {
        if (prize == null || !prize.gameObject.activeSelf)
        {
            return;
        }
        prize.Rotate(0f, 65f * Time.deltaTime, 0f, Space.World);
    }

    private bool TryGetBuildPoint(Vector2 mousePosition, out Vector3 point)
    {
        Ray ray = buildCamera.ScreenPointToRay(mousePosition);
        Plane plane = new Plane(Vector3.up, new Vector3(0f, buildSurfaceHeight, 0f));
        if (plane.Raycast(ray, out float distance))
        {
            point = ray.GetPoint(distance);
            point.y = buildSurfaceHeight;
            return true;
        }

        point = Vector3.zero;
        return false;
    }

    private bool IsInsideBuildArea(CircuitPiece piece)
    {
        Bounds bounds = piece.GetRenderBounds();
        return bounds.min.x >= buildAreaCenter.x - buildHalfSize &&
               bounds.max.x <= buildAreaCenter.x + buildHalfSize &&
               bounds.min.z >= buildAreaCenter.y - buildHalfSize &&
               bounds.max.z <= buildAreaCenter.y + buildHalfSize;
    }

    private bool HasValidConnection(CircuitPiece candidate)
    {
        for (int candidateConnector = 0;
             candidateConnector < candidate.ConnectorCount;
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
                    if (!IsPlacedConnectorOccupied(placedPiece, placedConnector) &&
                        AreConnectorsAligned(
                            candidate,
                            candidateConnector,
                            placedPiece,
                            placedConnector))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
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
                    if (IsPlacedConnectorOccupied(placedPiece, placedConnector) ||
                        !AreConnectorDirectionsOpposite(
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
        RestPendingPieceOnBuildSurface();
    }

    private bool IsPlacedConnectorOccupied(CircuitPiece piece, int connector)
    {
        foreach (CircuitPiece otherPiece in placedPieces)
        {
            if (otherPiece == null || otherPiece == piece)
            {
                continue;
            }

            for (int otherConnector = 0;
                 otherConnector < otherPiece.ConnectorCount;
                 otherConnector++)
            {
                if (AreConnectorsAligned(piece, connector, otherPiece, otherConnector))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool AreConnectorsAligned(
        CircuitPiece firstPiece,
        int firstConnector,
        CircuitPiece secondPiece,
        int secondConnector)
    {
        Vector3 firstPosition = firstPiece.GetConnectorPosition(firstConnector);
        Vector3 secondPosition = secondPiece.GetConnectorPosition(secondConnector);
        if (HorizontalDistance(firstPosition, secondPosition) > ConnectionPositionTolerance)
        {
            return false;
        }

        return AreConnectorDirectionsOpposite(
            firstPiece,
            firstConnector,
            secondPiece,
            secondConnector);
    }

    private static bool AreConnectorDirectionsOpposite(
        CircuitPiece firstPiece,
        int firstConnector,
        CircuitPiece secondPiece,
        int secondConnector)
    {
        Vector3 firstDirection = Flatten(firstPiece.GetConnectorDirection(firstConnector));
        Vector3 secondDirection = Flatten(secondPiece.GetConnectorDirection(secondConnector));
        return firstDirection.sqrMagnitude > Mathf.Epsilon &&
               secondDirection.sqrMagnitude > Mathf.Epsilon &&
               Vector3.Dot(firstDirection, secondDirection) <= ConnectionDirectionDot;
    }

    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        return Vector2.Distance(
            new Vector2(first.x, first.z),
            new Vector2(second.x, second.z));
    }

    private static Vector3 Flatten(Vector3 direction)
    {
        direction.y = 0f;
        return direction.normalized;
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
               pauseButton != null && pauseButtonLabel != null &&
               rotateYButton != null && rotateYCounterClockwiseButton != null &&
               placeButton != null &&
               resetButton != null && stopButton != null && retryButton != null &&
               editButton != null && resultResetButton != null && straightButtonLabel != null &&
               curveButtonLabel != null && testingLabel != null && statusLabel != null &&
               inventoryStatusLabel != null && paletteSummaryLabel != null &&
               resultTitleLabel != null && resultMessageLabel != null &&
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
        pauseButton.onClick.AddListener(TogglePause);
        resetButton.onClick.AddListener(ResetLayout);
        stopButton.onClick.AddListener(ReturnToBuild);
        retryButton.onClick.AddListener(ReturnToBuild);
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
        if (pauseButton != null) pauseButton.onClick.RemoveListener(TogglePause);
        if (resetButton != null) resetButton.onClick.RemoveListener(ResetLayout);
        if (stopButton != null) stopButton.onClick.RemoveListener(ReturnToBuild);
        if (retryButton != null) retryButton.onClick.RemoveListener(ReturnToBuild);
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

        ApplyPendingRotationAroundPivot();
        RestPendingPieceOnBuildSurface();
        EvaluatePendingPlacement();
        PieceRotated?.Invoke(pendingPiece);
        float angle = direction * rotationStep;
        status = "Rotated " + angle.ToString("+0;-0;0") +
                 " degrees around Y. Position unchanged.";
    }

    private void ApplyPendingRotationAroundPivot()
    {
        Quaternion targetRotation = GetPendingRotation();
        Quaternion rotationDelta =
            targetRotation * Quaternion.Inverse(pendingPiece.transform.rotation);
        Vector3 pivotWorld =
            pendingPiece.transform.TransformPoint(pendingRotationPivotLocal);
        Vector3 rootOffset = pendingPiece.transform.position - pivotWorld;
        Vector3 targetPosition = pivotWorld + rotationDelta * rootOffset;

        pendingPiece.transform.SetPositionAndRotation(targetPosition, targetRotation);
    }

    private void RefreshUi()
    {
        ApplyPieceCardActiveStates();

        bool isBuilding = state == LevelState.Build;
        bool isTesting = state == LevelState.Testing;
        bool isPaused = state == LevelState.Paused;
        bool isTestActive = isTesting || isPaused;
        bool isShowingResult = state == LevelState.Failure || state == LevelState.Success;

        // El panel de piezas permanece visible mientras se prueba el recorrido.
        buildControlsPanel.SetActive(isBuilding || isTestActive);
        buildActionsPanel.SetActive(isBuilding || isTestActive);
        testingControlsPanel.SetActive(false);
        resultPanel.SetActive(isShowingResult);
        rotationControlsPanel.SetActive(isBuilding);
        buildControlsPanel.SetActive(isBuilding);
        RefreshPaletteSummary();

        bool straightSelected = pendingPiece != null && pendingPiece.PieceType == CircuitPieceType.Straight;
        bool curveSelected = pendingPiece != null && pendingPiece.PieceType == CircuitPieceType.Curve45Right;
        bool halfStraightSelected = pendingPiece != null &&
                                    pendingPiece.PieceType == CircuitPieceType.HalfStraight;
        bool straightAvailable = straightPieceCard.ActiveInPalette &&
                                 !straightPieceCard.LockedInPalette;
        bool curveAvailable = curve45PieceCard.ActiveInPalette &&
                              !curve45PieceCard.LockedInPalette;
        bool halfStraightAvailable = halfStraightPieceCard.ActiveInPalette &&
                                     !halfStraightPieceCard.LockedInPalette;
        straightPieceCard.SetState(
            straightAvailable,
            straightAvailable && !straightSelected && straightRemaining > 0,
            straightSelected,
            straightRemaining);
        curve45PieceCard.SetState(
            curveAvailable,
            curveAvailable && !curveSelected && curveRemaining > 0,
            curveSelected,
            curveRemaining);
        halfStraightPieceCard.SetState(
            halfStraightAvailable,
            halfStraightAvailable && !halfStraightSelected && halfStraightRemaining > 0,
            halfStraightSelected,
            halfStraightRemaining);

        foreach (PieceSelectionCard card in lockedPieceCards)
        {
            if (card == halfStraightPieceCard)
            {
                continue;
            }

            bool unlocked = !card.LockedInPalette;
            card.SetState(unlocked, false, false, unlocked ? 1 : 0);
        }

        testButton.interactable = isBuilding &&
                                  pendingPiece == null &&
                                  placedPieces.Count > 0;
        pauseButton.interactable = isTestActive;
        stopButton.interactable = isTestActive;
        resetButton.interactable = (isBuilding || isTestActive) &&
                                   (placedPieces.Count > 0 || pendingPiece != null);
        pauseButtonLabel.text = isPaused ? "RESUME" : "PAUSE";
        bool canManipulatePendingPiece =
            isBuilding &&
            pendingPiece != null &&
            pendingPiece.gameObject.activeSelf &&
            placementState == PlacementState.Positioned;
        rotateYButton.interactable = canManipulatePendingPiece;
        rotateYCounterClockwiseButton.interactable = canManipulatePendingPiece;
        placeButton.interactable = canManipulatePendingPiece && pendingHasValidPosition;

        if (isTestActive)
        {
            float currentTestTime = isPaused ? pauseStartedAt : Time.time;
            testingLabel.text = "TEST  " +
                                (currentTestTime - testStartedAt).ToString("0.0") + " s";
        }

        if (isShowingResult)
        {
            bool reachedGoal = state == LevelState.Success;
            resultTitleLabel.text = reachedGoal
                ? "GOAL"
                : "FAIL";
            resultMessageLabel.text = string.IsNullOrEmpty(resultMessage)
                ? status
                : resultMessage;
            editButton.gameObject.SetActive(!reachedGoal);
        }

        statusLabel.text = status;
        inventoryStatusLabel.text = isTestActive || isShowingResult
            ? GetElapsedTimeStatus()
            : string.Empty;
    }

    private void ApplyPieceCardActiveStates()
    {
        straightPieceCard.ApplyInspectorActiveState();
        curve45PieceCard.ApplyInspectorActiveState();
        halfStraightPieceCard.ApplyInspectorActiveState();

        foreach (PieceSelectionCard card in lockedPieceCards)
        {
            card.ApplyInspectorActiveState();
        }
    }

    private void RefreshPaletteSummary()
    {
        int available = 0;
        int locked = 0;
        CountPaletteCard(straightPieceCard, ref available, ref locked);
        CountPaletteCard(halfStraightPieceCard, ref available, ref locked);
        CountPaletteCard(curve45PieceCard, ref available, ref locked);

        foreach (PieceSelectionCard card in lockedPieceCards)
        {
            if (card != halfStraightPieceCard)
            {
                CountPaletteCard(card, ref available, ref locked);
            }
        }

        paletteSummaryLabel.text = available + " AVAILABLE  ·  " + locked + " LOCKED";
    }

    private static void CountPaletteCard(
        PieceSelectionCard card,
        ref int available,
        ref int locked)
    {
        if (card == null || !card.ActiveInPalette)
        {
            return;
        }

        if (card.LockedInPalette)
        {
            locked++;
        }
        else
        {
            available++;
        }
    }

    private string GetInventoryStatus(
        bool straightSelected,
        bool halfStraightSelected,
        bool curveSelected)
    {
        if (straightSelected)
        {
            return "SELECTED: STRAIGHT  -  x" + straightRemaining;
        }
        if (curveSelected)
        {
            return "SELECTED: 45° CURVE  -  x" + curveRemaining;
        }
        if (halfStraightSelected)
        {
            return "SELECTED: HALF STRAIGHT  -  x" + halfStraightRemaining;
        }

        bool straightActive = straightPieceCard.ActiveInPalette &&
                              !straightPieceCard.LockedInPalette;
        bool curveActive = curve45PieceCard.ActiveInPalette &&
                           !curve45PieceCard.LockedInPalette;
        bool halfStraightActive = halfStraightPieceCard.ActiveInPalette &&
                                  !halfStraightPieceCard.LockedInPalette;
        List<string> inventory = new List<string>();
        if (straightActive) inventory.Add("STRAIGHT x" + straightRemaining);
        if (halfStraightActive) inventory.Add("HALF STRAIGHT x" + halfStraightRemaining);
        if (curveActive) inventory.Add("45° CURVE x" + curveRemaining);
        return inventory.Count > 0 ? string.Join("  |  ", inventory) : "NO PIECES AVAILABLE";
    }

    private string GetElapsedTimeStatus()
    {
        return hasTestDuration
            ? "TIME ON ROAD: " +
              GetDisplayedTestDuration().ToString("0.0") + " s"
            : string.Empty;
    }

    private float GetDisplayedTestDuration()
    {
        if (!hasTestDuration)
        {
            return 0f;
        }

        if (state == LevelState.Testing)
        {
            return Mathf.Max(0f, Time.time - testStartedAt);
        }

        if (state == LevelState.Paused)
        {
            return Mathf.Max(0f, pauseStartedAt - testStartedAt);
        }

        return lastTestDuration;
    }

    private void CaptureCurrentTestDuration()
    {
        if (!hasTestDuration)
        {
            return;
        }

        if (state == LevelState.Testing || state == LevelState.Paused)
        {
            lastTestDuration = GetDisplayedTestDuration();
        }
    }

    private string GetResultMessage()
    {
        return "TIME  " + FormatTime(lastTestDuration) +
               "\nPIECES  " + placedPieces.Count + " / " + targetPieceCount;
    }

    private void RegisterCompletedLevelTime()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        string levelTimeKey = LevelCompletionTimeKeyPrefix + sceneName;
        float totalTime = GetTotalCompletionTime();

        if (!PlayerPrefs.HasKey(levelTimeKey))
        {
            PlayerPrefs.SetFloat(levelTimeKey, lastTestDuration);
            PlayerPrefs.SetFloat(TotalCompletionTimeKey, totalTime + lastTestDuration);
            PlayerPrefs.Save();
            return;
        }

        float previousLevelTime = Mathf.Max(
            0f,
            PlayerPrefs.GetFloat(levelTimeKey, lastTestDuration));
        if (lastTestDuration >= previousLevelTime)
        {
            return;
        }

        PlayerPrefs.SetFloat(levelTimeKey, lastTestDuration);
        PlayerPrefs.SetFloat(
            TotalCompletionTimeKey,
            Mathf.Max(0f, totalTime - previousLevelTime + lastTestDuration));
        PlayerPrefs.Save();
    }

    private static float GetTotalCompletionTime()
    {
        return Mathf.Max(0f, PlayerPrefs.GetFloat(TotalCompletionTimeKey, 0f));
    }

    private static string FormatTime(float seconds)
    {
        return Mathf.Max(0f, seconds).ToString("0.0") + " s";
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
        Button newStopButton,
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
        stopButton = newStopButton;
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
