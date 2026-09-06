using System.Collections.Generic;
using UnityEngine;

internal sealed class BallPuzzlePlacementValidator
{
    public const float ConnectionPositionTolerance = 0.01f;

    private const float ConnectionDirectionDot = -0.999f;

    private readonly IReadOnlyList<CircuitPiece> placedPieces;
    private readonly Vector2 buildAreaCenter;
    private readonly float buildHalfSize;

    public BallPuzzlePlacementValidator(
        IReadOnlyList<CircuitPiece> placedPieces,
        Vector2 buildAreaCenter,
        float buildHalfSize)
    {
        this.placedPieces = placedPieces;
        this.buildAreaCenter = buildAreaCenter;
        this.buildHalfSize = buildHalfSize;
    }

    public bool IsInsideBuildArea(CircuitPiece piece)
    {
        Bounds bounds = piece.GetRenderBounds();
        return bounds.min.x >= buildAreaCenter.x - buildHalfSize &&
               bounds.max.x <= buildAreaCenter.x + buildHalfSize &&
               bounds.min.z >= buildAreaCenter.y - buildHalfSize &&
               bounds.max.z <= buildAreaCenter.y + buildHalfSize;
    }

    public bool HasValidConnection(CircuitPiece candidate)
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

    public bool IsPlacedConnectorOccupied(CircuitPiece piece, int connector)
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

    public static bool AreConnectorsAligned(
        CircuitPiece firstPiece,
        int firstConnector,
        CircuitPiece secondPiece,
        int secondConnector)
    {
        Vector3 firstPosition = firstPiece.GetConnectorPosition(firstConnector);
        Vector3 secondPosition = secondPiece.GetConnectorPosition(secondConnector);
        if (HorizontalDistance(firstPosition, secondPosition) >
            ConnectionPositionTolerance)
        {
            return false;
        }

        return AreConnectorDirectionsOpposite(
            firstPiece,
            firstConnector,
            secondPiece,
            secondConnector);
    }

    public static bool AreConnectorDirectionsOpposite(
        CircuitPiece firstPiece,
        int firstConnector,
        CircuitPiece secondPiece,
        int secondConnector)
    {
        Vector3 firstDirection = Flatten(
            firstPiece.GetConnectorDirection(firstConnector));
        Vector3 secondDirection = Flatten(
            secondPiece.GetConnectorDirection(secondConnector));
        return firstDirection.sqrMagnitude > Mathf.Epsilon &&
               secondDirection.sqrMagnitude > Mathf.Epsilon &&
               Vector3.Dot(firstDirection, secondDirection) <=
               ConnectionDirectionDot;
    }

    public static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        first.y = 0f;
        second.y = 0f;
        return Vector3.Distance(first, second);
    }

    public static Vector3 Flatten(Vector3 direction)
    {
        direction.y = 0f;
        return direction.normalized;
    }
}
