using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class BallPuzzleLevelController : MonoBehaviour
{
    private const float CoincidentPositionTolerance = 0.025f;
    private const float CoincidentDirectionDot = -0.995f;

    private enum LevelState
    {
        Build,
        Testing,
        Failure,
        Success
    }

    [Header("Scene")]
    [SerializeField] private Camera buildCamera;
    [SerializeField] private CircuitPiece straightPiecePrefab;
    [SerializeField] private CircuitPiece curve45RightPiecePrefab;
    [SerializeField] private Rigidbody ball;
    [SerializeField] private Transform ballSpawnPoint;
    [SerializeField] private Transform prize;
    [SerializeField] private Transform startAnchor;

    [Header("Level 01")]
    [SerializeField, Min(0)] private int availableStraights = 2;
    [SerializeField, Min(0)] private int availableCurves = 1;
    [SerializeField, Min(0.25f)] private float connectorSnapDistance = 2.25f;
    [SerializeField, Min(1f)] private float buildHalfSize = 16f;
    [SerializeField] private Vector2 buildAreaCenter = new Vector2(0f, 4f);

    [Header("Ball test")]
    [SerializeField, Min(0.1f)] private float launchSpeed = 7.5f;
    [SerializeField, Min(0.25f)] private float prizeCollectionDistance = 1.05f;
    [SerializeField] private float fallHeight = -0.55f;
    [SerializeField, Min(1f)] private float maximumTestDuration = 18f;
    [SerializeField, Min(0.1f)] private float stoppedDuration = 2f;

    private readonly List<CircuitPiece> placedPieces = new List<CircuitPiece>();
    private readonly Dictionary<CircuitPiece, bool[]> connectedConnectors =
        new Dictionary<CircuitPiece, bool[]>();

    private Transform placedPiecesRoot;
    private CircuitPiece pendingPiece;
    private CircuitPiece pendingTargetPiece;
    private int pendingTargetConnector = -1;
    private bool pendingUsesStartAnchor;
    private bool pendingHasValidSnap;
    private int straightRemaining;
    private int curveRemaining;
    private LevelState state = LevelState.Build;
    private string status = "Elige una pieza y conéctala al punto azul.";
    private float testStartedAt;
    private float stoppedAt = -1f;
    private Vector3 prizeInitialScale;

    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private GUIStyle statusStyle;
    private GUIStyle buttonStyle;
    private GUIStyle overlayStyle;

    private void Awake()
    {
        if (buildCamera == null)
        {
            buildCamera = Camera.main;
        }

        if (buildCamera == null || straightPiecePrefab == null || curve45RightPiecePrefab == null ||
            ball == null || ballSpawnPoint == null || prize == null || startAnchor == null)
        {
            Debug.LogError("Level01: faltan referencias de escena o prefabs.", this);
            enabled = false;
            return;
        }

        GameObject piecesRoot = new GameObject("Placed Puzzle Pieces");
        piecesRoot.transform.SetParent(transform, false);
        placedPiecesRoot = piecesRoot.transform;

        straightRemaining = availableStraights;
        curveRemaining = availableCurves;
        prizeInitialScale = prize.localScale;
        ResetBallForBuild();
    }

    private void Update()
    {
        AnimatePrize();

        if (state == LevelState.Build)
        {
            UpdateBuildMode();
        }
        else if (state == LevelState.Testing)
        {
            UpdateBallTest();
        }
    }

    private void UpdateBuildMode()
    {
        if (pendingPiece == null)
        {
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        if (mouse.rightButton.wasPressedThisFrame)
        {
            CancelPendingPiece();
            return;
        }

        UpdatePendingPiece(mouse.position.ReadValue());
        if (mouse.leftButton.wasPressedThisFrame)
        {
            PlacePendingPiece();
        }
    }

    private void BeginPlacement(CircuitPiece prefab)
    {
        if (state != LevelState.Build || prefab == null || GetRemainingCount(prefab) <= 0)
        {
            return;
        }

        CancelPendingPiece(false);
        pendingPiece = Instantiate(prefab, placedPiecesRoot);
        pendingPiece.name = prefab.DisplayName + " (pendiente)";
        pendingPiece.SetTint(new Color(0.92f, 0.34f, 0.25f, 1f));
        status = placedPieces.Count == 0
            ? "Encaja la primera pieza en el punto azul y haz clic."
            : "Acerca la pieza a un conector libre y haz clic. Botón derecho cancela.";
    }

    private void UpdatePendingPiece(Vector2 mousePosition)
    {
        if (!TryGetBuildPoint(mousePosition, out Vector3 buildPoint))
        {
            pendingHasValidSnap = false;
            return;
        }

        pendingPiece.transform.SetPositionAndRotation(buildPoint, Quaternion.identity);
        pendingTargetPiece = null;
        pendingTargetConnector = -1;
        pendingUsesStartAnchor = false;

        Vector3 position;
        Quaternion rotation;
        bool foundSnap;
        if (placedPieces.Count == 0)
        {
            foundSnap = TrySnapToStartAnchor(buildPoint, out position, out rotation);
        }
        else
        {
            foundSnap = TrySnapToOpenConnector(
                buildPoint,
                out pendingTargetPiece,
                out pendingTargetConnector,
                out position,
                out rotation);
        }

        if (!foundSnap)
        {
            pendingHasValidSnap = false;
            pendingPiece.SetTint(new Color(0.92f, 0.34f, 0.25f, 1f));
            return;
        }

        pendingPiece.transform.SetPositionAndRotation(position, rotation);
        pendingHasValidSnap = IsInsideBuildArea(pendingPiece) && !DuplicatesExistingOrigin(pendingPiece);
        pendingPiece.SetTint(pendingHasValidSnap
            ? new Color(0.30f, 0.90f, 0.48f, 1f)
            : new Color(0.92f, 0.34f, 0.25f, 1f));
    }

    private bool TrySnapToStartAnchor(
        Vector3 buildPoint,
        out Vector3 snappedPosition,
        out Quaternion snappedRotation)
    {
        pendingUsesStartAnchor = true;
        return TryBuildSnap(
            buildPoint,
            startAnchor.position,
            Flatten(startAnchor.forward),
            out snappedPosition,
            out snappedRotation);
    }

    private bool TrySnapToOpenConnector(
        Vector3 buildPoint,
        out CircuitPiece targetPiece,
        out int targetConnector,
        out Vector3 snappedPosition,
        out Quaternion snappedRotation)
    {
        targetPiece = null;
        targetConnector = -1;
        snappedPosition = Vector3.zero;
        snappedRotation = Quaternion.identity;
        float closestDistance = connectorSnapDistance;

        foreach (CircuitPiece piece in placedPieces)
        {
            for (int connector = 0; connector < piece.ConnectorCount; connector++)
            {
                if (IsConnected(piece, connector) ||
                    !TryBuildSnap(
                        buildPoint,
                        piece.GetConnectorPosition(connector),
                        Flatten(piece.GetConnectorDirection(connector)),
                        out Vector3 candidatePosition,
                        out Quaternion candidateRotation))
                {
                    continue;
                }

                float distance = HorizontalDistance(buildPoint, candidatePosition);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    targetPiece = piece;
                    targetConnector = connector;
                    snappedPosition = candidatePosition;
                    snappedRotation = candidateRotation;
                }
            }
        }

        return targetPiece != null;
    }

    private bool TryBuildSnap(
        Vector3 buildPoint,
        Vector3 targetPosition,
        Vector3 targetDirection,
        out Vector3 snappedPosition,
        out Quaternion snappedRotation)
    {
        int sourceConnector = pendingPiece.IncomingConnectorIndex;
        Vector3 sourceLocalPosition = pendingPiece.transform.InverseTransformPoint(
            pendingPiece.GetConnectorPosition(sourceConnector));
        Vector3 sourceLocalDirection = Flatten(pendingPiece.transform.InverseTransformDirection(
            pendingPiece.GetConnectorDirection(sourceConnector)));

        float yaw = Vector3.SignedAngle(sourceLocalDirection, -targetDirection, Vector3.up);
        yaw = Mathf.Round(yaw / 45f) * 45f;
        snappedRotation = Quaternion.Euler(0f, yaw, 0f);
        snappedPosition = targetPosition - snappedRotation * sourceLocalPosition;
        return HorizontalDistance(buildPoint, snappedPosition) <= connectorSnapDistance;
    }

    private void PlacePendingPiece()
    {
        if (!pendingHasValidSnap || (!pendingUsesStartAnchor && pendingTargetPiece == null))
        {
            status = "La pieza debe aparecer en verde antes de colocarla.";
            return;
        }

        CircuitPiece piece = pendingPiece;
        piece.name = piece.DisplayName + " " + (placedPieces.Count + 1);
        piece.ClearTint();
        RegisterPlacedPiece(piece);

        if (pendingUsesStartAnchor)
        {
            connectedConnectors[piece][piece.IncomingConnectorIndex] = true;
        }
        else
        {
            Connect(piece, piece.IncomingConnectorIndex, pendingTargetPiece, pendingTargetConnector);
        }
        ConnectOtherCoincidentConnectors(piece);
        ConsumePiece(piece);

        pendingPiece = null;
        pendingTargetPiece = null;
        pendingTargetConnector = -1;
        pendingUsesStartAnchor = false;
        pendingHasValidSnap = false;
        status = "Pieza colocada. Puedes continuar o pulsar PROBAR.";
    }

    private void CancelPendingPiece(bool updateStatus = true)
    {
        if (pendingPiece != null)
        {
            Destroy(pendingPiece.gameObject);
        }

        pendingPiece = null;
        pendingTargetPiece = null;
        pendingTargetConnector = -1;
        pendingUsesStartAnchor = false;
        pendingHasValidSnap = false;
        if (updateStatus)
        {
            status = "Colocación cancelada.";
        }
    }

    private void StartBallTest()
    {
        if (state != LevelState.Build || pendingPiece != null)
        {
            status = "Termina o cancela la pieza pendiente antes de probar.";
            return;
        }
        if (placedPieces.Count == 0)
        {
            status = "Coloca al menos una pieza antes de probar.";
            return;
        }

        state = LevelState.Testing;
        testStartedAt = Time.time;
        stoppedAt = -1f;
        prize.gameObject.SetActive(true);
        prize.localScale = prizeInitialScale;

        ball.gameObject.SetActive(true);
        ball.isKinematic = true;
        ball.transform.SetPositionAndRotation(ballSpawnPoint.position, ballSpawnPoint.rotation);
        ball.linearVelocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;
        ball.isKinematic = false;
        ball.linearVelocity = ballSpawnPoint.forward * launchSpeed;
        ball.WakeUp();
        status = "Prueba en marcha: la bola debe recoger el premio.";
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
            FailTest("La bola cayó fuera del recorrido.");
            return;
        }
        if (Time.time - testStartedAt >= maximumTestDuration)
        {
            FailTest("Se agotó el tiempo de la prueba.");
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
                FailTest("La bola se ha quedado detenida.");
            }
        }
        else
        {
            stoppedAt = -1f;
        }
    }

    private void FailTest(string reason)
    {
        FreezeBall();
        state = LevelState.Failure;
        status = reason;
    }

    private void CompleteLevel()
    {
        FreezeBall();
        state = LevelState.Success;
        status = "¡Premio recogido! Nivel completado.";
        prize.localScale = prizeInitialScale * 1.35f;
    }

    private void RetryTest()
    {
        state = LevelState.Build;
        ResetBallForBuild();
        StartBallTest();
    }

    private void ReturnToBuild()
    {
        state = LevelState.Build;
        ResetBallForBuild();
        prize.gameObject.SetActive(true);
        prize.localScale = prizeInitialScale;
        status = "Ajusta el montaje y vuelve a pulsar PROBAR.";
    }

    private void ResetLayout()
    {
        CancelPendingPiece(false);
        foreach (CircuitPiece piece in placedPieces)
        {
            if (piece != null)
            {
                Destroy(piece.gameObject);
            }
        }
        placedPieces.Clear();
        connectedConnectors.Clear();
        straightRemaining = availableStraights;
        curveRemaining = availableCurves;
        state = LevelState.Build;
        ResetBallForBuild();
        prize.gameObject.SetActive(true);
        prize.localScale = prizeInitialScale;
        status = "Montaje reiniciado. Conecta una pieza al punto azul.";
    }

    private void FreezeBall()
    {
        ball.linearVelocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;
        ball.isKinematic = true;
    }

    private void ResetBallForBuild()
    {
        ball.isKinematic = true;
        ball.linearVelocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;
        ball.transform.SetPositionAndRotation(ballSpawnPoint.position, ballSpawnPoint.rotation);
        ball.gameObject.SetActive(true);
    }

    private void AnimatePrize()
    {
        if (prize == null || !prize.gameObject.activeSelf)
        {
            return;
        }
        prize.Rotate(0f, 65f * Time.deltaTime, 0f, Space.World);
    }

    private void RegisterPlacedPiece(CircuitPiece piece)
    {
        placedPieces.Add(piece);
        connectedConnectors[piece] = new bool[piece.ConnectorCount];
    }

    private bool IsConnected(CircuitPiece piece, int connector)
    {
        return connectedConnectors.TryGetValue(piece, out bool[] states) && states[connector];
    }

    private void Connect(CircuitPiece first, int firstConnector, CircuitPiece second, int secondConnector)
    {
        connectedConnectors[first][firstConnector] = true;
        connectedConnectors[second][secondConnector] = true;
    }

    private void ConnectOtherCoincidentConnectors(CircuitPiece newPiece)
    {
        for (int newConnector = 0; newConnector < newPiece.ConnectorCount; newConnector++)
        {
            if (IsConnected(newPiece, newConnector))
            {
                continue;
            }

            foreach (CircuitPiece placedPiece in placedPieces)
            {
                if (IsConnected(newPiece, newConnector))
                {
                    break;
                }
                if (placedPiece == newPiece)
                {
                    continue;
                }

                for (int placedConnector = 0; placedConnector < placedPiece.ConnectorCount; placedConnector++)
                {
                    if (IsConnected(placedPiece, placedConnector) ||
                        Vector3.Distance(newPiece.GetConnectorPosition(newConnector),
                            placedPiece.GetConnectorPosition(placedConnector)) > CoincidentPositionTolerance)
                    {
                        continue;
                    }

                    float dot = Vector3.Dot(
                        Flatten(newPiece.GetConnectorDirection(newConnector)),
                        Flatten(placedPiece.GetConnectorDirection(placedConnector)));
                    if (dot <= CoincidentDirectionDot)
                    {
                        Connect(newPiece, newConnector, placedPiece, placedConnector);
                        break;
                    }
                }
            }
        }
    }

    private bool TryGetBuildPoint(Vector2 mousePosition, out Vector3 point)
    {
        Ray ray = buildCamera.ScreenPointToRay(mousePosition);
        Plane plane = new Plane(Vector3.up, Vector3.zero);
        if (plane.Raycast(ray, out float distance))
        {
            point = ray.GetPoint(distance);
            point.y = 0f;
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

    private bool DuplicatesExistingOrigin(CircuitPiece piece)
    {
        foreach (CircuitPiece placedPiece in placedPieces)
        {
            Vector2 difference = new Vector2(
                piece.transform.position.x - placedPiece.transform.position.x,
                piece.transform.position.z - placedPiece.transform.position.z);
            if (difference.sqrMagnitude < 0.04f)
            {
                return true;
            }
        }
        return false;
    }

    private int GetRemainingCount(CircuitPiece prefab)
    {
        return prefab.PieceType == CircuitPieceType.Straight ? straightRemaining : curveRemaining;
    }

    private void ConsumePiece(CircuitPiece piece)
    {
        if (piece.PieceType == CircuitPieceType.Straight)
        {
            straightRemaining = Mathf.Max(0, straightRemaining - 1);
        }
        else
        {
            curveRemaining = Mathf.Max(0, curveRemaining - 1);
        }
    }

    private static Vector3 Flatten(Vector3 direction)
    {
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
    }

    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        return Vector2.Distance(new Vector2(first.x, first.z), new Vector2(second.x, second.z));
    }

    private void OnGUI()
    {
        EnsureGuiStyles();

        GUI.Box(new Rect(12f, 12f, Screen.width - 24f, 76f), GUIContent.none);
        GUI.Label(new Rect(28f, 20f, 330f, 30f), "BALL PUZZLE — NIVEL 01", titleStyle);
        GUI.Label(new Rect(28f, 50f, 500f, 26f),
            "AZUL: caída de la bola  |  NARANJA: objetivo. Conecta ambos puntos.", labelStyle);

        if (state == LevelState.Build)
        {
            DrawBuildControls();
        }
        else if (state == LevelState.Testing)
        {
            DrawTestingControls();
        }
        else
        {
            DrawResultOverlay();
        }

        GUI.Box(new Rect(12f, Screen.height - 58f, Screen.width - 24f, 46f), status, statusStyle);
    }

    private void DrawBuildControls()
    {
        float buttonY = 25f;
        float right = Screen.width - 28f;

        GUI.enabled = pendingPiece == null && straightRemaining > 0;
        if (GUI.Button(new Rect(right - 620f, buttonY, 145f, 42f),
                "RECTA  ×" + straightRemaining, buttonStyle))
        {
            BeginPlacement(straightPiecePrefab);
        }

        GUI.enabled = pendingPiece == null && curveRemaining > 0;
        if (GUI.Button(new Rect(right - 465f, buttonY, 155f, 42f),
                "CURVA 45°  ×" + curveRemaining, buttonStyle))
        {
            BeginPlacement(curve45RightPiecePrefab);
        }

        GUI.enabled = pendingPiece == null && placedPieces.Count > 0;
        if (GUI.Button(new Rect(right - 300f, buttonY, 130f, 42f), "PROBAR", buttonStyle))
        {
            StartBallTest();
        }

        GUI.enabled = placedPieces.Count > 0 || pendingPiece != null;
        if (GUI.Button(new Rect(right - 160f, buttonY, 160f, 42f), "REINICIAR", buttonStyle))
        {
            ResetLayout();
        }
        GUI.enabled = true;
    }

    private void DrawTestingControls()
    {
        float elapsed = Time.time - testStartedAt;
        GUI.Label(new Rect(Screen.width - 410f, 31f, 190f, 30f),
            "PRUEBA  " + elapsed.ToString("0.0") + " s", titleStyle);
        if (GUI.Button(new Rect(Screen.width - 205f, 25f, 175f, 42f),
                "DETENER Y EDITAR", buttonStyle))
        {
            ReturnToBuild();
        }
    }

    private void DrawResultOverlay()
    {
        float width = 480f;
        float height = 230f;
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        GUI.Box(panel, GUIContent.none, overlayStyle);
        GUI.Label(new Rect(panel.x + 30f, panel.y + 28f, width - 60f, 42f),
            state == LevelState.Success ? "¡NIVEL COMPLETADO!" : "PRUEBA FALLIDA", titleStyle);
        GUI.Label(new Rect(panel.x + 30f, panel.y + 80f, width - 60f, 40f), status, labelStyle);

        if (GUI.Button(new Rect(panel.x + 30f, panel.y + 145f, 130f, 48f), "REINTENTAR", buttonStyle))
        {
            RetryTest();
        }
        if (GUI.Button(new Rect(panel.x + 175f, panel.y + 145f, 130f, 48f), "EDITAR", buttonStyle))
        {
            ReturnToBuild();
        }
        if (GUI.Button(new Rect(panel.x + 320f, panel.y + 145f, 130f, 48f), "REINICIAR", buttonStyle))
        {
            ResetLayout();
        }
    }

    private void EnsureGuiStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            normal = { textColor = new Color(0.88f, 0.92f, 0.96f) }
        };
        statusStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 15,
            padding = new RectOffset(16, 16, 8, 8),
            normal = { textColor = Color.white }
        };
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold
        };
        overlayStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = GUI.skin.box.normal.background }
        };
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
#endif
}
