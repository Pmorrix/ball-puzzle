using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal readonly struct BallPuzzleLevelUiState
{
    public BallPuzzleLevelUiState(
        bool isBuilding,
        bool isTesting,
        bool isShowingResult,
        bool isSuccess,
        bool isChallengeIncomplete,
        CircuitPieceType? selectedPieceType,
        bool hasPendingPiece,
        bool pendingPieceIsActive,
        bool pendingPieceIsPositioned,
        bool pendingHasValidPosition,
        bool hasCompleteCircuit,
        int placedPieceCount,
        int straightRemaining,
        int curveRemaining,
        int halfStraightRemaining,
        string status,
        string resultMessage,
        bool hasTestDuration,
        float displayedTestDuration)
    {
        IsBuilding = isBuilding;
        IsTesting = isTesting;
        IsShowingResult = isShowingResult;
        IsSuccess = isSuccess;
        IsChallengeIncomplete = isChallengeIncomplete;
        SelectedPieceType = selectedPieceType;
        HasPendingPiece = hasPendingPiece;
        PendingPieceIsActive = pendingPieceIsActive;
        PendingPieceIsPositioned = pendingPieceIsPositioned;
        PendingHasValidPosition = pendingHasValidPosition;
        HasCompleteCircuit = hasCompleteCircuit;
        PlacedPieceCount = placedPieceCount;
        StraightRemaining = straightRemaining;
        CurveRemaining = curveRemaining;
        HalfStraightRemaining = halfStraightRemaining;
        Status = status;
        ResultMessage = resultMessage;
        HasTestDuration = hasTestDuration;
        DisplayedTestDuration = displayedTestDuration;
    }

    public bool IsBuilding { get; }
    public bool IsTesting { get; }
    public bool IsShowingResult { get; }
    public bool IsSuccess { get; }
    public bool IsChallengeIncomplete { get; }
    public CircuitPieceType? SelectedPieceType { get; }
    public bool HasPendingPiece { get; }
    public bool PendingPieceIsActive { get; }
    public bool PendingPieceIsPositioned { get; }
    public bool PendingHasValidPosition { get; }
    public bool HasCompleteCircuit { get; }
    public int PlacedPieceCount { get; }
    public int StraightRemaining { get; }
    public int CurveRemaining { get; }
    public int HalfStraightRemaining { get; }
    public string Status { get; }
    public string ResultMessage { get; }
    public bool HasTestDuration { get; }
    public float DisplayedTestDuration { get; }
}

internal sealed class BallPuzzleLevelUiPresenter
{
    private static readonly Color ActivePlayLabelColor =
        new Color32(97, 255, 158, 255);
    private static readonly Color DisabledPlayLabelColor =
        new Color32(29, 91, 53, 213);

    private readonly RectTransform levelTitlePanel;
    private readonly bool moveLevelTitleDuringTest;
    private readonly Vector2 testingLevelTitleAnchoredPosition;
    private readonly GameObject buildControlsPanel;
    private readonly GameObject buildActionsPanel;
    private readonly GameObject testingControlsPanel;
    private readonly GameObject resultPanel;
    private readonly GameObject rotationControlsPanel;
    private readonly Button testButton;
    private readonly Button resetButton;
    private readonly Button rotateYButton;
    private readonly Button rotateYCounterClockwiseButton;
    private readonly Button placeButton;
    private readonly Button editButton;
    private readonly TMP_Text testButtonLabel;
    private readonly TMP_Text testingLabel;
    private readonly TMP_Text statusLabel;
    private readonly TMP_Text inventoryStatusLabel;
    private readonly TMP_Text resultTitleLabel;
    private readonly TMP_Text resultMessageLabel;
    private readonly PieceSelectionCard straightPieceCard;
    private readonly PieceSelectionCard curve45PieceCard;
    private readonly PieceSelectionCard halfStraightPieceCard;
    private readonly PieceSelectionCard[] lockedPieceCards;

    private Vector2 buildTitleAnchorMin;
    private Vector2 buildTitleAnchorMax;
    private Vector2 buildTitleAnchoredPosition;
    private Vector2 buildTitleSizeDelta;
    private Vector2 buildTitlePivot;
    private bool isLevelTitleInTestingPosition;

    public BallPuzzleLevelUiPresenter(
        RectTransform levelTitlePanel,
        bool moveLevelTitleDuringTest,
        Vector2 testingLevelTitleAnchoredPosition,
        GameObject buildControlsPanel,
        GameObject buildActionsPanel,
        GameObject testingControlsPanel,
        GameObject resultPanel,
        GameObject rotationControlsPanel,
        Button testButton,
        Button resetButton,
        Button rotateYButton,
        Button rotateYCounterClockwiseButton,
        Button placeButton,
        Button editButton,
        TMP_Text testingLabel,
        TMP_Text statusLabel,
        TMP_Text inventoryStatusLabel,
        TMP_Text resultTitleLabel,
        TMP_Text resultMessageLabel,
        PieceSelectionCard straightPieceCard,
        PieceSelectionCard curve45PieceCard,
        PieceSelectionCard halfStraightPieceCard,
        PieceSelectionCard[] lockedPieceCards)
    {
        this.levelTitlePanel = levelTitlePanel;
        this.moveLevelTitleDuringTest = moveLevelTitleDuringTest;
        this.testingLevelTitleAnchoredPosition = testingLevelTitleAnchoredPosition;
        this.buildControlsPanel = buildControlsPanel;
        this.buildActionsPanel = buildActionsPanel;
        this.testingControlsPanel = testingControlsPanel;
        this.resultPanel = resultPanel;
        this.rotationControlsPanel = rotationControlsPanel;
        this.testButton = testButton;
        testButtonLabel = testButton != null
            ? testButton.GetComponentInChildren<TMP_Text>(true)
            : null;
        this.resetButton = resetButton;
        this.rotateYButton = rotateYButton;
        this.rotateYCounterClockwiseButton = rotateYCounterClockwiseButton;
        this.placeButton = placeButton;
        this.editButton = editButton;
        this.testingLabel = testingLabel;
        this.statusLabel = statusLabel;
        this.inventoryStatusLabel = inventoryStatusLabel;
        this.resultTitleLabel = resultTitleLabel;
        this.resultMessageLabel = resultMessageLabel;
        this.straightPieceCard = straightPieceCard;
        this.curve45PieceCard = curve45PieceCard;
        this.halfStraightPieceCard = halfStraightPieceCard;
        this.lockedPieceCards = lockedPieceCards;
    }

    public void Refresh(BallPuzzleLevelUiState state)
    {
        ApplyPieceCardActiveStates();
        UpdateLevelTitleLayout(!state.IsBuilding);

        if (levelTitlePanel != null)
        {
            levelTitlePanel.gameObject.SetActive(!state.IsShowingResult);
        }

        buildControlsPanel.SetActive(state.IsBuilding);
        buildActionsPanel.SetActive(state.IsBuilding);
        testingControlsPanel.SetActive(false);
        resultPanel.SetActive(state.IsShowingResult);
        rotationControlsPanel.SetActive(state.IsBuilding);

        bool straightSelected = IsSelected(state, CircuitPieceType.Straight);
        bool curveSelected = IsSelected(state, CircuitPieceType.Curve45Right);
        bool halfStraightSelected = IsSelected(state, CircuitPieceType.HalfStraight);

        RefreshPieceCards(
            state,
            straightSelected,
            curveSelected,
            halfStraightSelected);
        RefreshButtons(state);
        RefreshTestingStatus(state);
        RefreshResult(state);

        if (state.IsShowingResult)
        {
            statusLabel.text = string.Empty;
            inventoryStatusLabel.text = string.Empty;
            return;
        }

        statusLabel.text = state.Status;
        inventoryStatusLabel.text = state.IsTesting
            ? GetElapsedTimeStatus(state)
            : GetInventoryStatus(
                state,
                straightSelected,
                halfStraightSelected,
                curveSelected);
    }

    public void ApplyPieceCardActiveStates()
    {
        straightPieceCard.ApplyInspectorActiveState();
        curve45PieceCard.ApplyInspectorActiveState();
        halfStraightPieceCard.ApplyInspectorActiveState();

        foreach (PieceSelectionCard card in lockedPieceCards)
        {
            card.ApplyInspectorActiveState();
        }
    }

    public static string CreateResultMessage(
        float testDuration,
        int placedPieceCount,
        int targetPieceCount)
    {
        return "TIME  " + FormatTime(testDuration) +
               "\nPIECES  " + placedPieceCount + " / " + targetPieceCount;
    }

    private void RefreshPieceCards(
        BallPuzzleLevelUiState state,
        bool straightSelected,
        bool curveSelected,
        bool halfStraightSelected)
    {
        bool straightAvailable = straightPieceCard.ActiveInPalette &&
                                 !straightPieceCard.LockedInPalette;
        bool curveAvailable = curve45PieceCard.ActiveInPalette &&
                              !curve45PieceCard.LockedInPalette;
        bool halfStraightAvailable = halfStraightPieceCard.ActiveInPalette &&
                                     !halfStraightPieceCard.LockedInPalette;

        straightPieceCard.SetState(
            straightAvailable,
            straightAvailable && !straightSelected && state.StraightRemaining > 0,
            straightSelected,
            state.StraightRemaining);
        curve45PieceCard.SetState(
            curveAvailable,
            curveAvailable && !curveSelected && state.CurveRemaining > 0,
            curveSelected,
            state.CurveRemaining);
        halfStraightPieceCard.SetState(
            halfStraightAvailable,
            halfStraightAvailable && !halfStraightSelected && state.HalfStraightRemaining > 0,
            halfStraightSelected,
            state.HalfStraightRemaining);

        foreach (PieceSelectionCard card in lockedPieceCards)
        {
            if (card == halfStraightPieceCard)
            {
                continue;
            }

            bool unlocked = !card.LockedInPalette;
            card.SetState(unlocked, false, false, unlocked ? 1 : 0);
        }
    }

    private void RefreshButtons(BallPuzzleLevelUiState state)
    {
        bool canStartTest = state.IsBuilding &&
                            !state.HasPendingPiece &&
                            state.HasCompleteCircuit;
        testButton.interactable = canStartTest;
        if (testButtonLabel != null)
        {
            testButtonLabel.color = canStartTest
                ? ActivePlayLabelColor
                : DisabledPlayLabelColor;
        }
        resetButton.interactable = (state.IsBuilding || state.IsTesting) &&
                                   (state.PlacedPieceCount > 0 || state.HasPendingPiece);

        bool canManipulatePendingPiece =
            state.IsBuilding &&
            state.HasPendingPiece &&
            state.PendingPieceIsActive &&
            state.PendingPieceIsPositioned;
        rotateYButton.interactable = canManipulatePendingPiece;
        rotateYCounterClockwiseButton.interactable = canManipulatePendingPiece;
        placeButton.interactable = canManipulatePendingPiece && state.PendingHasValidPosition;
    }

    private void RefreshTestingStatus(BallPuzzleLevelUiState state)
    {
        if (!state.IsTesting)
        {
            return;
        }

        testingLabel.text = "TEST  " + state.DisplayedTestDuration.ToString("0.0") + " s";
    }

    private void RefreshResult(BallPuzzleLevelUiState state)
    {
        if (!state.IsShowingResult)
        {
            return;
        }

        resultTitleLabel.text = state.IsSuccess
            ? "GOAL"
            : state.IsChallengeIncomplete
                ? "TRY AGAIN"
                : "FAIL";
        resultMessageLabel.text = string.IsNullOrEmpty(state.ResultMessage)
            ? state.Status
            : state.ResultMessage;
        editButton.gameObject.SetActive(!state.IsSuccess);
    }

    private void UpdateLevelTitleLayout(bool useTestingPosition)
    {
        if (!moveLevelTitleDuringTest || levelTitlePanel == null)
        {
            return;
        }

        if (useTestingPosition)
        {
            if (isLevelTitleInTestingPosition)
            {
                return;
            }

            buildTitleAnchorMin = levelTitlePanel.anchorMin;
            buildTitleAnchorMax = levelTitlePanel.anchorMax;
            buildTitleAnchoredPosition = levelTitlePanel.anchoredPosition;
            buildTitleSizeDelta = levelTitlePanel.sizeDelta;
            buildTitlePivot = levelTitlePanel.pivot;
            Vector2 titleSize = levelTitlePanel.rect.size;

            levelTitlePanel.anchorMin = new Vector2(0.5f, 1f);
            levelTitlePanel.anchorMax = new Vector2(0.5f, 1f);
            levelTitlePanel.pivot = new Vector2(0.5f, 1f);
            levelTitlePanel.sizeDelta = titleSize;
            levelTitlePanel.anchoredPosition = testingLevelTitleAnchoredPosition;
            isLevelTitleInTestingPosition = true;
            return;
        }

        if (!isLevelTitleInTestingPosition)
        {
            return;
        }

        levelTitlePanel.anchorMin = buildTitleAnchorMin;
        levelTitlePanel.anchorMax = buildTitleAnchorMax;
        levelTitlePanel.pivot = buildTitlePivot;
        levelTitlePanel.sizeDelta = buildTitleSizeDelta;
        levelTitlePanel.anchoredPosition = buildTitleAnchoredPosition;
        isLevelTitleInTestingPosition = false;
    }

    private string GetInventoryStatus(
        BallPuzzleLevelUiState state,
        bool straightSelected,
        bool halfStraightSelected,
        bool curveSelected)
    {
        if (straightSelected)
        {
            return "SELECTED: STRAIGHT  -  x" + state.StraightRemaining;
        }
        if (curveSelected)
        {
            return "SELECTED: 45° CURVE  -  x" + state.CurveRemaining;
        }
        if (halfStraightSelected)
        {
            return "SELECTED: HALF STRAIGHT  -  x" + state.HalfStraightRemaining;
        }

        bool straightActive = straightPieceCard.ActiveInPalette &&
                              !straightPieceCard.LockedInPalette;
        bool curveActive = curve45PieceCard.ActiveInPalette &&
                           !curve45PieceCard.LockedInPalette;
        bool halfStraightActive = halfStraightPieceCard.ActiveInPalette &&
                                  !halfStraightPieceCard.LockedInPalette;
        List<string> inventory = new List<string>();
        if (straightActive) inventory.Add("STRAIGHT x" + state.StraightRemaining);
        if (halfStraightActive) inventory.Add("HALF STRAIGHT x" + state.HalfStraightRemaining);
        if (curveActive) inventory.Add("45° CURVE x" + state.CurveRemaining);
        return inventory.Count > 0 ? string.Join("  |  ", inventory) : "NO PIECES AVAILABLE";
    }

    private static string GetElapsedTimeStatus(BallPuzzleLevelUiState state)
    {
        return state.HasTestDuration
            ? "TIME ON ROAD: " + state.DisplayedTestDuration.ToString("0.0") + " s"
            : string.Empty;
    }

    private static bool IsSelected(
        BallPuzzleLevelUiState state,
        CircuitPieceType pieceType)
    {
        return state.SelectedPieceType.HasValue &&
               state.SelectedPieceType.Value == pieceType;
    }

    private static string FormatTime(float seconds)
    {
        return Mathf.Max(0f, seconds).ToString("0.0") + " s";
    }
}
