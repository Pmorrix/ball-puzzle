using System.Collections.Generic;
using UnityEngine;

internal sealed class BallPuzzleGoalBinding
{
    private readonly IList<CircuitPiece> placedPieces;
    private readonly BallPuzzlePlacementValidator placementValidator;
    private readonly Transform startAnchor;
    private readonly Transform goal;
    private readonly Vector3 initialGoalPosition;
    private readonly float alignmentAssistDistance;
    private readonly float goalReachDistance;

    private CircuitPiece adjustedGoalPiece;
    private int adjustedGoalConnectorIndex = -1;

    public BallPuzzleGoalBinding(
        IList<CircuitPiece> placedPieces,
        BallPuzzlePlacementValidator placementValidator,
        Transform startAnchor,
        Transform goal,
        Vector3 initialGoalPosition,
        float alignmentAssistDistance,
        float goalReachDistance)
    {
        this.placedPieces = placedPieces;
        this.placementValidator = placementValidator;
        this.startAnchor = startAnchor;
        this.goal = goal;
        this.initialGoalPosition = initialGoalPosition;
        this.alignmentAssistDistance = alignmentAssistDistance;
        this.goalReachDistance = goalReachDistance;
    }

    public bool IsBoundConnector(CircuitPiece piece, int connectorIndex)
    {
        return piece == adjustedGoalPiece &&
               connectorIndex == adjustedGoalConnectorIndex;
    }

    public bool IsAdjustedPiece(CircuitPiece piece)
    {
        return IsPieceConnectedToStart(piece) && IsGoalAlongPiece(piece);
    }

    public void ReevaluateBinding()
    {
        if (TryGetBoundGoalCenter(out _))
        {
            KeepGoalAtInitialPosition();
            return;
        }

        adjustedGoalPiece = null;
        adjustedGoalConnectorIndex = -1;
        if (TryGetClosestFreeGoalCandidate(
                out CircuitPiece candidatePiece,
                out int candidateConnector))
        {
            adjustedGoalPiece = candidatePiece;
            adjustedGoalConnectorIndex = candidateConnector;
            KeepGoalAtInitialPosition();
            return;
        }

        RestoreInitialGoalPosition();
    }

    public void RecalculatePosition()
    {
        if (TryGetStoredGoalCenter(out _))
        {
            KeepGoalAtInitialPosition();
            return;
        }

        RestoreInitialGoalPosition();
    }

    public bool HasValidGoal()
    {
        if (goal == null)
        {
            return false;
        }

        foreach (CircuitPiece piece in GetStartConnectedPieces())
        {
            if (IsGoalAlongPiece(piece))
            {
                return true;
            }
        }

        return false;
    }

    public void Reset()
    {
        adjustedGoalPiece = null;
        adjustedGoalConnectorIndex = -1;
        RestoreInitialGoalPosition();
    }

    private bool TryGetBoundGoalCenter(out Vector3 worldCenter)
    {
        return TryGetStoredGoalCenter(out worldCenter) &&
               IsPieceConnectedToStart(adjustedGoalPiece);
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
        out int candidateConnector)
    {
        candidatePiece = null;
        candidateConnector = -1;
        float closestDistance = float.PositiveInfinity;
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
                if (placementValidator.IsPlacedConnectorOccupied(piece, connector))
                {
                    continue;
                }

                Vector3 connectorPosition = piece.GetConnectorPosition(connector);
                if (startAnchor != null &&
                    BallPuzzlePlacementValidator.HorizontalDistance(
                        connectorPosition,
                        startAnchor.position) <=
                    BallPuzzlePlacementValidator.ConnectionPositionTolerance)
                {
                    continue;
                }

                float distance = BallPuzzlePlacementValidator.HorizontalDistance(
                    connectorPosition,
                    initialGoalPosition);
                if (distance > alignmentAssistDistance ||
                    distance >= closestDistance)
                {
                    continue;
                }

                candidatePiece = piece;
                candidateConnector = connector;
                closestDistance = distance;
            }
        }

        return candidatePiece != null;
    }

    private bool IsPieceConnectedToStart(CircuitPiece piece)
    {
        return piece != null && GetStartConnectedPieces().Contains(piece);
    }

    private bool IsGoalAlongPiece(CircuitPiece piece)
    {
        if (piece == null || goal == null || piece.BallPathPointCount < 2)
        {
            return false;
        }

        for (int point = 0; point < piece.BallPathPointCount - 1; point++)
        {
            Vector3 segmentStart = piece.GetBallPathPointPosition(point);
            Vector3 segmentEnd = piece.GetBallPathPointPosition(point + 1);
            if (DistanceToSegment(goal.position, segmentStart, segmentEnd) <=
                goalReachDistance)
            {
                return true;
            }
        }

        return false;
    }

    private static float DistanceToSegment(
        Vector3 point,
        Vector3 segmentStart,
        Vector3 segmentEnd)
    {
        Vector3 segment = segmentEnd - segmentStart;
        if (segment.sqrMagnitude <= Mathf.Epsilon)
        {
            return Vector3.Distance(point, segmentStart);
        }

        float position = Mathf.Clamp01(
            Vector3.Dot(point - segmentStart, segment) / segment.sqrMagnitude);
        return Vector3.Distance(point, segmentStart + segment * position);
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
            if (!placementValidator.IsPlacedConnectorOccupied(piece, connector) &&
                BallPuzzlePlacementValidator.HorizontalDistance(
                    piece.GetConnectorPosition(connector),
                    startAnchor.position) <=
                BallPuzzlePlacementValidator.ConnectionPositionTolerance)
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
                if (BallPuzzlePlacementValidator.AreConnectorsAligned(
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

    private void KeepGoalAtInitialPosition()
    {
        if (goal != null)
        {
            goal.position = initialGoalPosition;
        }
    }

    private void RestoreInitialGoalPosition()
    {
        KeepGoalAtInitialPosition();
    }
}
