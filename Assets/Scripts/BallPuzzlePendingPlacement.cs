using UnityEngine;

internal enum PlacementState
{
    None,
    Selected,
    Dragging,
    Positioned
}

internal sealed class BallPuzzlePendingPlacement
{
    public CircuitPiece Piece { get; private set; }
    public PlacementState State { get; set; } = PlacementState.None;
    public int RotationIndex { get; set; }
    public Vector3 DragOffset { get; set; }
    public Vector3 RotationPivotLocal { get; private set; }
    public bool HasValidPosition { get; set; }

    public bool IsMoving =>
        Piece != null &&
        (State == PlacementState.Selected || State == PlacementState.Dragging);

    public void Begin(CircuitPiece piece)
    {
        Piece = piece;
        State = PlacementState.Selected;
        RotationIndex = 0;
        DragOffset = Vector3.zero;
        RotationPivotLocal = piece.transform.InverseTransformPoint(
            piece.GetRenderBounds().center);
        HasValidPosition = false;
    }

    public void Reset()
    {
        Piece = null;
        State = PlacementState.None;
        RotationIndex = 0;
        DragOffset = Vector3.zero;
        RotationPivotLocal = Vector3.zero;
        HasValidPosition = false;
    }
}
