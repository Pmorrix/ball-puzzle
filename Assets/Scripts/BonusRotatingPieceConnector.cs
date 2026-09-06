using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CircuitPiece))]
public sealed class BonusRotatingPieceConnector : MonoBehaviour
{
    [Header("Rotation")]
    [SerializeField, Min(0f)] private float rotationSpeed = 10f;

    [Header("Connection")]
    [SerializeField, Min(0.01f)] private float connectionDistance = 0.08f;
    [SerializeField, Range(-1f, 0f)]
    private float directionDotThreshold = -0.999f;

    private readonly List<CircuitPiece> playerPieces =
        new List<CircuitPiece>();

    private BallPuzzleLevelController levelController;
    private CircuitPiece rotatingPiece;
    private Vector3 rotationCenter;
    private Vector3 baseWorldPosition;
    private Quaternion baseWorldRotation;
    private float rotationAngle;
    private bool isConnected;

    public bool IsConnected => isConnected;
    public CircuitPiece ConnectedPiece { get; private set; }

    private void Awake()
    {
        rotatingPiece = GetComponent<CircuitPiece>();
        levelController = GetComponentInParent<BallPuzzleLevelController>();

        if (rotatingPiece == null || levelController == null)
        {
            Debug.LogError(
                "BonusRotatingPieceConnector: faltan la pieza o el controlador del nivel.",
                this);
            enabled = false;
            return;
        }

        baseWorldPosition = transform.position;
        baseWorldRotation = transform.rotation;
        rotationCenter = GetVisualCenter(transform);
    }

    private void OnEnable()
    {
        if (levelController == null)
        {
            return;
        }

        levelController.PiecePlaced -= HandlePiecePlaced;
        levelController.PiecePlaced += HandlePiecePlaced;
        levelController.PieceRemoved -= HandlePieceRemoved;
        levelController.PieceRemoved += HandlePieceRemoved;
        levelController.LayoutReset -= HandleLayoutReset;
        levelController.LayoutReset += HandleLayoutReset;
    }

    private void OnDisable()
    {
        if (levelController == null)
        {
            return;
        }

        levelController.PiecePlaced -= HandlePiecePlaced;
        levelController.PieceRemoved -= HandlePieceRemoved;
        levelController.LayoutReset -= HandleLayoutReset;
    }

    private void Update()
    {
        if (isConnected)
        {
            return;
        }

        RotatePiece();
        if (levelController.IsBuilding)
        {
            TryConnectToPlayerPiece();
        }
    }

    private void RotatePiece()
    {
        rotationAngle += rotationSpeed * Time.unscaledDeltaTime;
        Quaternion rotation = Quaternion.AngleAxis(
            rotationAngle,
            Vector3.up);
        transform.SetPositionAndRotation(
            rotationCenter +
            rotation * (baseWorldPosition - rotationCenter),
            rotation * baseWorldRotation);
    }

    private void TryConnectToPlayerPiece()
    {
        CircuitPiece bestPiece = null;
        int bestRotatingConnector = -1;
        int bestPlayerConnector = -1;
        float bestDistance = float.PositiveInfinity;

        for (int pieceIndex = playerPieces.Count - 1;
             pieceIndex >= 0;
             pieceIndex--)
        {
            CircuitPiece playerPiece = playerPieces[pieceIndex];
            if (playerPiece == null)
            {
                playerPieces.RemoveAt(pieceIndex);
                continue;
            }

            for (int playerConnector = 0;
                 playerConnector < playerPiece.ConnectorCount;
                 playerConnector++)
            {
                if (IsPlayerConnectorOccupied(
                        playerPiece,
                        playerConnector))
                {
                    continue;
                }

                for (int rotatingConnector = 0;
                     rotatingConnector < rotatingPiece.ConnectorCount;
                     rotatingConnector++)
                {
                    float distance = BallPuzzlePlacementValidator.HorizontalDistance(
                        rotatingPiece.GetConnectorPosition(rotatingConnector),
                        playerPiece.GetConnectorPosition(playerConnector));
                    if (distance > connectionDistance ||
                        distance >= bestDistance ||
                        !AreDirectionsOpposite(
                            rotatingConnector,
                            playerPiece,
                            playerConnector))
                    {
                        continue;
                    }

                    bestPiece = playerPiece;
                    bestRotatingConnector = rotatingConnector;
                    bestPlayerConnector = playerConnector;
                    bestDistance = distance;
                }
            }
        }

        if (bestPiece == null)
        {
            return;
        }

        if (levelController.IsPieceAnchoredToStart(bestPiece) || HasOtherRouteConnection(
                bestPiece,
                bestPlayerConnector))
        {
            AlignRotatingPiece(
                bestRotatingConnector,
                bestPiece,
                bestPlayerConnector);
        }
        else
        {
            AlignPlayerPiece(
                bestPiece,
                bestPlayerConnector,
                bestRotatingConnector);
        }

        if (!levelController.RegisterConnectedScenePiece(rotatingPiece))
        {
            return;
        }

        isConnected = true;
        ConnectedPiece = bestPiece;
    }

    private bool IsPlayerConnectorOccupied(
        CircuitPiece playerPiece,
        int playerConnector)
    {
        if (levelController != null &&
            levelController.IsRouteConnectorOccupied(playerPiece, playerConnector))
        {
            return true;
        }
        foreach (CircuitPiece otherPiece in playerPieces)
        {
            if (otherPiece == null || otherPiece == playerPiece)
            {
                continue;
            }

            for (int otherConnector = 0;
                 otherConnector < otherPiece.ConnectorCount;
                 otherConnector++)
            {
                if (BallPuzzlePlacementValidator.AreConnectorsAligned(
                        playerPiece,
                        playerConnector,
                        otherPiece,
                        otherConnector))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void AlignRotatingPiece(
        int rotatingConnector,
        CircuitPiece playerPiece,
        int playerConnector)
    {
        Vector3 playerPosition =
            playerPiece.GetConnectorPosition(playerConnector);
        Vector3 rotatingRadius =
            rotatingPiece.GetConnectorPosition(rotatingConnector) -
            rotationCenter;
        Vector3 playerRadius = playerPosition - rotationCenter;
        rotatingRadius.y = 0f;
        playerRadius.y = 0f;
        float positionYawCorrection = 0f;
        if (rotatingRadius.sqrMagnitude > Mathf.Epsilon &&
            playerRadius.sqrMagnitude > Mathf.Epsilon)
        {
            positionYawCorrection = Vector3.SignedAngle(
                rotatingRadius,
                playerRadius,
                Vector3.up);
            Quaternion positionRotation = Quaternion.AngleAxis(
                positionYawCorrection,
                Vector3.up);
            transform.SetPositionAndRotation(
                rotationCenter +
                positionRotation * (transform.position - rotationCenter),
                positionRotation * transform.rotation);
        }

        Vector3 rotatingDirection = BallPuzzlePlacementValidator.Flatten(
            rotatingPiece.GetConnectorDirection(rotatingConnector));
        Vector3 playerDirection = BallPuzzlePlacementValidator.Flatten(
            playerPiece.GetConnectorDirection(playerConnector));
        if (rotatingDirection.sqrMagnitude <= Mathf.Epsilon ||
            playerDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        float directionYawCorrection = Vector3.SignedAngle(
            rotatingDirection,
            -playerDirection,
            Vector3.up);
        Quaternion directionRotation = Quaternion.AngleAxis(
            directionYawCorrection,
            Vector3.up);
        Vector3 connectorPosition =
            rotatingPiece.GetConnectorPosition(rotatingConnector);
        Vector3 rootOffset = transform.position - connectorPosition;
        transform.SetPositionAndRotation(
            connectorPosition + directionRotation * rootOffset,
            directionRotation * transform.rotation);

        Vector3 positionCorrection =
            playerPosition -
            rotatingPiece.GetConnectorPosition(rotatingConnector);
        positionCorrection.y = 0f;
        transform.position += positionCorrection;
        rotationAngle = Mathf.Repeat(
            rotationAngle +
            positionYawCorrection +
            directionYawCorrection,
            360f);
        Physics.SyncTransforms();
    }

    private bool HasOtherRouteConnection(
        CircuitPiece playerPiece,
        int detectedConnector)
    {
        for (int connector = 0;
             connector < playerPiece.ConnectorCount;
             connector++)
        {
            if (connector != detectedConnector &&
                levelController.IsRouteConnectorOccupied(
                    playerPiece,
                    connector))
            {
                return true;
            }
        }

        return false;
    }

    private bool AreDirectionsOpposite(
        int rotatingConnector,
        CircuitPiece playerPiece,
        int playerConnector)
    {
        Vector3 rotatingDirection = BallPuzzlePlacementValidator.Flatten(
            rotatingPiece.GetConnectorDirection(rotatingConnector));
        Vector3 playerDirection = BallPuzzlePlacementValidator.Flatten(
            playerPiece.GetConnectorDirection(playerConnector));
        return rotatingDirection.sqrMagnitude > Mathf.Epsilon &&
               playerDirection.sqrMagnitude > Mathf.Epsilon &&
               Vector3.Dot(rotatingDirection, playerDirection) <=
               directionDotThreshold;
    }

    private void AlignPlayerPiece(
        CircuitPiece playerPiece,
        int playerConnector,
        int rotatingConnector)
    {
        Vector3 rotatingDirection = BallPuzzlePlacementValidator.Flatten(
            rotatingPiece.GetConnectorDirection(rotatingConnector));
        Vector3 playerDirection = BallPuzzlePlacementValidator.Flatten(
            playerPiece.GetConnectorDirection(playerConnector));
        float yawCorrection = Vector3.SignedAngle(
            playerDirection,
            -rotatingDirection,
            Vector3.up);

        Vector3 connectorPosition =
            playerPiece.GetConnectorPosition(playerConnector);
        Quaternion rotationCorrection = Quaternion.AngleAxis(
            yawCorrection,
            Vector3.up);
        Vector3 rootOffset =
            playerPiece.transform.position - connectorPosition;
        playerPiece.transform.SetPositionAndRotation(
            connectorPosition + rotationCorrection * rootOffset,
            rotationCorrection * playerPiece.transform.rotation);

        Vector3 positionCorrection =
            rotatingPiece.GetConnectorPosition(rotatingConnector) -
            playerPiece.GetConnectorPosition(playerConnector);
        playerPiece.transform.position += positionCorrection;
        Physics.SyncTransforms();
    }

    private void HandlePiecePlaced(CircuitPiece piece)
    {
        if (piece != null && !playerPieces.Contains(piece))
        {
            playerPieces.Add(piece);
        }
    }

    private void HandlePieceRemoved(CircuitPiece piece)
    {
        playerPieces.Remove(piece);
        if (ConnectedPiece != piece)
        {
            return;
        }

        levelController.UnregisterConnectedScenePiece(rotatingPiece);
        ConnectedPiece = null;
        isConnected = false;
    }

    private void HandleLayoutReset()
    {
        playerPieces.Clear();
        ConnectedPiece = null;
        isConnected = false;
        rotationAngle = 0f;
        transform.SetPositionAndRotation(baseWorldPosition, baseWorldRotation);
        Physics.SyncTransforms();
    }

    private static Vector3 GetVisualCenter(Transform targetTransform)
    {
        Renderer[] renderers =
            targetTransform.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return targetTransform.position;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds.center;
    }
}
