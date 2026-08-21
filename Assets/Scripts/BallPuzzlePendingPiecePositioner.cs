using UnityEngine;

internal sealed class BallPuzzlePendingPiecePositioner
{
    private readonly Camera buildCamera;
    private readonly float placementGridSize;
    private readonly float buildSurfaceHeight;
    private readonly float rotationStep;

    public BallPuzzlePendingPiecePositioner(
        Camera buildCamera,
        float placementGridSize,
        float buildSurfaceHeight,
        float rotationStep)
    {
        this.buildCamera = buildCamera;
        this.placementGridSize = placementGridSize;
        this.buildSurfaceHeight = buildSurfaceHeight;
        this.rotationStep = rotationStep;
    }

    public void PositionAtStagingPoint(
        CircuitPiece piece,
        Vector2 buildAreaCenter,
        int rotationIndex)
    {
        Vector3 stagingPosition = new Vector3(
            SnapToGrid(buildAreaCenter.x),
            buildSurfaceHeight,
            SnapToGrid(buildAreaCenter.y));
        piece.transform.SetPositionAndRotation(
            stagingPosition,
            GetRotation(rotationIndex));
        RestOnBuildSurface(piece);
    }

    public bool TryPositionAtPointer(
        CircuitPiece piece,
        Vector2 mousePosition,
        Vector3 dragOffset,
        int rotationIndex)
    {
        if (!TryGetBuildPoint(mousePosition, out Vector3 buildPoint))
        {
            return false;
        }

        buildPoint += dragOffset;
        Vector3 snappedPosition = new Vector3(
            SnapToGrid(buildPoint.x),
            buildSurfaceHeight,
            SnapToGrid(buildPoint.z));
        piece.transform.SetPositionAndRotation(
            snappedPosition,
            GetRotation(rotationIndex));
        RestOnBuildSurface(piece);
        return true;
    }

    public void MoveFullyRightOf(
        CircuitPiece piece,
        Vector2 minimumScreenPosition)
    {
        const int MaximumCorrectionIterations = 3;
        Vector2 currentScreenPosition = minimumScreenPosition;

        for (int iteration = 0;
             iteration < MaximumCorrectionIterations;
             iteration++)
        {
            float pieceLeftEdge = GetLeftScreenEdge(piece);
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
            piece.transform.position += worldCorrection;
            RestOnBuildSurface(piece);
            currentScreenPosition = correctedScreenPosition;
        }
    }

    public bool TryGetDragOffset(
        CircuitPiece piece,
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
        bool hitPiece = false;
        float closestDistance = float.PositiveInfinity;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
            {
                continue;
            }

            Transform hitTransform = hit.collider.transform;
            bool belongsToPiece =
                hitTransform == piece.transform ||
                hitTransform.IsChildOf(piece.transform);
            if (!belongsToPiece || hit.distance >= closestDistance)
            {
                continue;
            }

            hitPiece = true;
            closestDistance = hit.distance;
        }

        if (!hitPiece ||
            !TryGetBuildPoint(mousePosition, out Vector3 buildPoint))
        {
            return false;
        }

        dragOffset = piece.transform.position - buildPoint;
        dragOffset.y = 0f;
        return true;
    }

    public void RotateAroundPivot(
        CircuitPiece piece,
        Vector3 pivotLocal,
        int rotationIndex)
    {
        Quaternion targetRotation = GetRotation(rotationIndex);
        Quaternion rotationDelta =
            targetRotation * Quaternion.Inverse(piece.transform.rotation);
        Vector3 pivotWorld = piece.transform.TransformPoint(pivotLocal);
        Vector3 rootOffset = piece.transform.position - pivotWorld;
        Vector3 targetPosition = pivotWorld + rotationDelta * rootOffset;

        piece.transform.SetPositionAndRotation(targetPosition, targetRotation);
    }

    public void RestOnBuildSurface(CircuitPiece piece)
    {
        Bounds bounds = piece.GetRenderBounds();
        Vector3 position = piece.transform.position;
        position.y += buildSurfaceHeight - bounds.min.y;
        piece.transform.position = position;
    }

    public bool TryGetBuildPoint(Vector2 mousePosition, out Vector3 point)
    {
        Ray ray = buildCamera.ScreenPointToRay(mousePosition);
        Plane plane = new Plane(
            Vector3.up,
            new Vector3(0f, buildSurfaceHeight, 0f));
        if (plane.Raycast(ray, out float distance))
        {
            point = ray.GetPoint(distance);
            point.y = buildSurfaceHeight;
            return true;
        }

        point = Vector3.zero;
        return false;
    }

    private Quaternion GetRotation(int rotationIndex)
    {
        return Quaternion.Euler(0f, rotationIndex * rotationStep, 0f);
    }

    private float SnapToGrid(float value)
    {
        return Mathf.Round(value / placementGridSize) * placementGridSize;
    }

    private float GetLeftScreenEdge(CircuitPiece piece)
    {
        Bounds bounds = piece.GetRenderBounds();
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
                    Vector3 screenCorner = buildCamera.WorldToScreenPoint(corner);
                    if (screenCorner.z > 0f)
                    {
                        leftEdge = Mathf.Min(leftEdge, screenCorner.x);
                    }
                }
            }
        }

        return leftEdge;
    }
}
