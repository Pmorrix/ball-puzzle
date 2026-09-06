using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class CircuitEditorController : MonoBehaviour
{
    private const float PaletteHeight = 0.20f;
    private const int PaletteLayer = 31;
    private const float CoincidentPositionTolerance = 0.025f;
    private const float CoincidentDirectionDot = -0.995f;

    [Header("Scene")]
    [SerializeField] private Camera buildCamera;
    [SerializeField] private CircuitPiece startPiecePrefab;
    [SerializeField] private CircuitPiece straightPiecePrefab;
    [SerializeField] private CircuitPiece curve45RightPiecePrefab;

    [Header("Grid and snap")]
    [SerializeField, Min(0.25f)] private float gridSpacing = 1f;
    [SerializeField, Min(4)] private int gridHalfSize = 14;
    [SerializeField, Min(0.25f)] private float connectorSnapDistance = 2.25f;

    private readonly List<CircuitPiece> placedPieces = new List<CircuitPiece>();
    private readonly Dictionary<CircuitPiece, bool[]> connectedConnectors = new Dictionary<CircuitPiece, bool[]>();

    private Transform placedPiecesRoot;
    private Camera paletteCamera;
    private CircuitPiece straightPreview;
    private CircuitPiece curvePreview;
    private CircuitPiece pendingPiece;
    private CircuitPiece pendingTargetPiece;
    private int pendingTargetConnector = -1;
    private bool pendingHasValidSnap;
    private bool awaitingPieceValidation;
    private string status = "Drag a piece from the top area.";

    private GUIStyle titleStyle;
    private GUIStyle paletteLabelStyle;
    private GUIStyle statusStyle;
    private GUIStyle buttonStyle;

    private void Awake()
    {
        if (buildCamera == null)
        {
            buildCamera = Camera.main;
        }

        if (buildCamera == null || startPiecePrefab == null || straightPiecePrefab == null || curve45RightPiecePrefab == null)
        {
            Debug.LogError("Circuit Editor: faltan referencias de cámara o prefabs.", this);
            enabled = false;
            return;
        }

        CreateRuntimeHierarchy();
        CreateBuildSurface();
        CreatePalette();
        PlaceStartPiece();
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        Vector2 mousePosition = mouse.position.ReadValue();

        if (!awaitingPieceValidation && pendingPiece == null && mouse.leftButton.wasPressedThisFrame && IsInsidePalette(mousePosition))
        {
            BeginDrag(mousePosition.x < Screen.width * 0.5f ? straightPiecePrefab : curve45RightPiecePrefab);
        }

        if (pendingPiece == null || awaitingPieceValidation)
        {
            return;
        }

        UpdateDraggedPiece(mousePosition);

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            FinishDrag();
        }
    }

    private void CreateRuntimeHierarchy()
    {
        GameObject root = new GameObject("Placed Circuit Pieces");
        root.transform.SetParent(transform, false);
        placedPiecesRoot = root.transform;

        buildCamera.rect = new Rect(0f, 0f, 1f, 1f - PaletteHeight);
        buildCamera.cullingMask &= ~(1 << PaletteLayer);
    }

    private void CreateBuildSurface()
    {
        float size = gridHalfSize * 2f;

        GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
        board.name = "Circuit Build Area";
        board.transform.SetParent(transform, false);
        board.transform.position = new Vector3(0f, -0.10f, 0f);
        board.transform.localScale = new Vector3(size, 0.15f, size);

        Collider boardCollider = board.GetComponent<Collider>();
        if (boardCollider != null)
        {
            Destroy(boardCollider);
        }

        Material boardMaterial = CreateRuntimeMaterial(
            "Circuit Board Material",
            new Color(0.075f, 0.10f, 0.13f, 1f),
            false);
        board.GetComponent<Renderer>().sharedMaterial = boardMaterial;

        GameObject grid = new GameObject("Snap Grid");
        grid.transform.SetParent(transform, false);
        MeshFilter filter = grid.AddComponent<MeshFilter>();
        MeshRenderer renderer = grid.AddComponent<MeshRenderer>();

        List<Vector3> vertices = new List<Vector3>();
        List<int> indices = new List<int>();
        for (float coordinate = -gridHalfSize; coordinate <= gridHalfSize + 0.001f; coordinate += gridSpacing)
        {
            int start = vertices.Count;
            vertices.Add(new Vector3(coordinate, 0.01f, -gridHalfSize));
            vertices.Add(new Vector3(coordinate, 0.01f, gridHalfSize));
            vertices.Add(new Vector3(-gridHalfSize, 0.01f, coordinate));
            vertices.Add(new Vector3(gridHalfSize, 0.01f, coordinate));
            indices.Add(start);
            indices.Add(start + 1);
            indices.Add(start + 2);
            indices.Add(start + 3);
        }

        Mesh mesh = new Mesh { name = "Circuit Snap Grid" };
        mesh.SetVertices(vertices);
        mesh.SetIndices(indices, MeshTopology.Lines, 0);
        mesh.RecalculateBounds();
        filter.sharedMesh = mesh;
        renderer.sharedMaterial = CreateRuntimeMaterial(
            "Circuit Grid Material",
            new Color(0.25f, 0.47f, 0.58f, 0.62f),
            true);
    }

    private void CreatePalette()
    {
        GameObject cameraObject = new GameObject("Available Pieces Camera");
        cameraObject.transform.SetParent(transform, false);
        paletteCamera = cameraObject.AddComponent<Camera>();
        paletteCamera.clearFlags = CameraClearFlags.SolidColor;
        paletteCamera.backgroundColor = new Color(0.035f, 0.045f, 0.06f, 1f);
        paletteCamera.cullingMask = 1 << PaletteLayer;
        paletteCamera.orthographic = true;
        paletteCamera.orthographicSize = 3.25f;
        paletteCamera.nearClipPlane = 0.1f;
        paletteCamera.farClipPlane = 50f;
        paletteCamera.depth = buildCamera.depth + 1f;
        paletteCamera.rect = new Rect(0f, 1f - PaletteHeight, 1f, PaletteHeight);
        paletteCamera.transform.position = new Vector3(0f, 15f, 0f);
        paletteCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        GameObject previewRoot = new GameObject("Available Piece Previews");
        previewRoot.transform.SetParent(transform, false);
        previewRoot.transform.position = Vector3.zero;

        straightPreview = Instantiate(straightPiecePrefab, previewRoot.transform);
        straightPreview.name = "Straight Preview";
        straightPreview.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        straightPreview.transform.localScale = Vector3.one * 0.62f;

        curvePreview = Instantiate(curve45RightPiecePrefab, previewRoot.transform);
        curvePreview.name = "Curve 45 Right Preview";
        curvePreview.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
        curvePreview.transform.localScale = Vector3.one * 0.78f;

        DisablePreviewColliders(straightPreview);
        DisablePreviewColliders(curvePreview);
        SetLayerRecursively(previewRoot, PaletteLayer);
        LayoutPalettePreviews();
    }

    private void LayoutPalettePreviews()
    {
        if (paletteCamera == null || straightPreview == null || curvePreview == null)
        {
            return;
        }

        float halfWidth = paletteCamera.orthographicSize * paletteCamera.aspect;
        float offset = halfWidth * 0.25f;
        CenterPreviewAt(straightPreview, new Vector3(-offset, 0f, 0f));
        CenterPreviewAt(curvePreview, new Vector3(offset, 0f, 0f));
    }

    private static void CenterPreviewAt(CircuitPiece preview, Vector3 desiredCenter)
    {
        Bounds bounds = preview.GetRenderBounds();
        Vector3 correction = desiredCenter - bounds.center;
        correction.y = 0f;
        preview.transform.position += correction;
    }

    private static void DisablePreviewColliders(CircuitPiece preview)
    {
        foreach (Collider collider in preview.GetComponentsInChildren<Collider>())
        {
            collider.enabled = false;
        }
    }

    private void PlaceStartPiece()
    {
        CircuitPiece startPiece = Instantiate(startPiecePrefab, Vector3.zero, Quaternion.identity, placedPiecesRoot);
        startPiece.name = "Start Piece";
        RegisterPlacedPiece(startPiece);
        status = "Exit placed. Drag the first piece from above.";
    }

    private void BeginDrag(CircuitPiece prefab)
    {
        if (prefab == null)
        {
            return;
        }

        pendingPiece = Instantiate(prefab, placedPiecesRoot);
        pendingPiece.name = prefab.DisplayName + " (pendiente)";
        pendingPiece.SetTint(new Color(1f, 0.67f, 0.18f, 1f));
        pendingTargetPiece = null;
        pendingTargetConnector = -1;
        pendingHasValidSnap = false;
        status = "Move the piece near an open connector and release the mouse.";
    }

    private void UpdateDraggedPiece(Vector2 mousePosition)
    {
        if (!TryGetBuildPoint(mousePosition, out Vector3 buildPoint))
        {
            pendingHasValidSnap = false;
            return;
        }

        Vector3 gridPoint = new Vector3(
            Mathf.Round(buildPoint.x / gridSpacing) * gridSpacing,
            0f,
            Mathf.Round(buildPoint.z / gridSpacing) * gridSpacing);
        pendingPiece.transform.SetPositionAndRotation(gridPoint, Quaternion.identity);

        if (!TryFindClosestOpenConnector(
                buildPoint,
                out CircuitPiece targetPiece,
                out int targetConnector,
                out Vector3 snappedPosition,
                out Quaternion snappedRotation))
        {
            pendingTargetPiece = null;
            pendingTargetConnector = -1;
            pendingHasValidSnap = false;
            pendingPiece.SetTint(new Color(0.92f, 0.34f, 0.25f, 1f));
            return;
        }

        pendingPiece.transform.SetPositionAndRotation(snappedPosition, snappedRotation);
        pendingTargetPiece = targetPiece;
        pendingTargetConnector = targetConnector;
        pendingHasValidSnap = IsInsideBuildArea(pendingPiece) && !DuplicatesExistingOrigin(pendingPiece);
        pendingPiece.SetTint(pendingHasValidSnap
            ? new Color(0.30f, 0.90f, 0.48f, 1f)
            : new Color(0.92f, 0.34f, 0.25f, 1f));
    }

    private bool TryFindClosestOpenConnector(
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

        int sourceConnector = pendingPiece.IncomingConnectorIndex;
        Vector3 sourceLocalPosition = pendingPiece.transform.InverseTransformPoint(
            pendingPiece.GetConnectorPosition(sourceConnector));
        Vector3 sourceLocalDirection = Flatten(pendingPiece.transform.InverseTransformDirection(
            pendingPiece.GetConnectorDirection(sourceConnector)));

        foreach (CircuitPiece piece in placedPieces)
        {
            for (int connector = 0; connector < piece.ConnectorCount; connector++)
            {
                if (IsConnected(piece, connector))
                {
                    continue;
                }

                Vector3 targetDirection = Flatten(piece.GetConnectorDirection(connector));
                float yaw = Vector3.SignedAngle(sourceLocalDirection, -targetDirection, Vector3.up);
                yaw = Mathf.Round(yaw / 45f) * 45f;
                Quaternion candidateRotation = Quaternion.Euler(0f, yaw, 0f);
                Vector3 candidatePosition = piece.GetConnectorPosition(connector) - candidateRotation * sourceLocalPosition;
                float distance = Vector2.Distance(
                    new Vector2(buildPoint.x, buildPoint.z),
                    new Vector2(candidatePosition.x, candidatePosition.z));
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

    private bool TryGetBuildPoint(Vector2 mousePosition, out Vector3 point)
    {
        Ray ray = buildCamera.ScreenPointToRay(mousePosition);
        Plane buildPlane = new Plane(Vector3.up, Vector3.zero);
        if (buildPlane.Raycast(ray, out float distance))
        {
            point = ray.GetPoint(distance);
            return true;
        }

        point = Vector3.zero;
        return false;
    }

    private void FinishDrag()
    {
        if (!pendingHasValidSnap || pendingTargetPiece == null || pendingTargetConnector < 0)
        {
            Destroy(pendingPiece.gameObject);
            pendingPiece = null;
            status = "The piece did not fit. Drag it again until it turns green.";
            return;
        }

        awaitingPieceValidation = true;
        status = "Good fit. Press CONFIRM PIECE to lock it.";
    }

    private void ConfirmPendingPiece()
    {
        if (!awaitingPieceValidation || pendingPiece == null || pendingTargetPiece == null)
        {
            return;
        }

        CircuitPiece confirmedPiece = pendingPiece;
        confirmedPiece.name = confirmedPiece.DisplayName + " " + placedPieces.Count;
        confirmedPiece.ClearTint();
        RegisterPlacedPiece(confirmedPiece);
        Connect(
            confirmedPiece,
            confirmedPiece.IncomingConnectorIndex,
            pendingTargetPiece,
            pendingTargetConnector);
        ConnectOtherCoincidentConnectors(confirmedPiece);

        pendingPiece = null;
        pendingTargetPiece = null;
        pendingTargetConnector = -1;
        pendingHasValidSnap = false;
        awaitingPieceValidation = false;
        status = "Piece confirmed. Drag the next one or validate the circuit.";
    }

    private void CancelPendingPiece()
    {
        if (pendingPiece != null)
        {
            Destroy(pendingPiece.gameObject);
        }

        pendingPiece = null;
        pendingTargetPiece = null;
        pendingTargetConnector = -1;
        pendingHasValidSnap = false;
        awaitingPieceValidation = false;
        status = "Placement cancelled. Drag another piece from above.";
    }

    private void ValidateCircuit()
    {
        if (awaitingPieceValidation)
        {
            status = "First validate or cancel the pending piece.";
            return;
        }

        int openConnectors = CountOpenConnectors();
        if (placedPieces.Count < 3)
        {
            status = "The circuit still needs more pieces.";
        }
        else if (openConnectors > 0)
        {
            status = "Circuito incompleto: quedan " + openConnectors + " conexiones abiertas.";
        }
        else
        {
            status = "Circuit validated: all tracks are connected.";
        }
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
                if (placedPiece == newPiece)
                {
                    continue;
                }

                for (int placedConnector = 0; placedConnector < placedPiece.ConnectorCount; placedConnector++)
                {
                    if (IsConnected(placedPiece, placedConnector) ||
                        Vector3.Distance(newPiece.GetConnectorPosition(newConnector), placedPiece.GetConnectorPosition(placedConnector)) > CoincidentPositionTolerance)
                    {
                        continue;
                    }

                    float directionDot = Vector3.Dot(
                        Flatten(newPiece.GetConnectorDirection(newConnector)),
                        Flatten(placedPiece.GetConnectorDirection(placedConnector)));
                    if (directionDot <= CoincidentDirectionDot)
                    {
                        Connect(newPiece, newConnector, placedPiece, placedConnector);
                        break;
                    }
                }

                if (IsConnected(newPiece, newConnector))
                {
                    break;
                }
            }
        }
    }

    private int CountOpenConnectors()
    {
        int count = 0;
        foreach (CircuitPiece piece in placedPieces)
        {
            for (int connector = 0; connector < piece.ConnectorCount; connector++)
            {
                if (!IsConnected(piece, connector))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private bool IsInsideBuildArea(CircuitPiece piece)
    {
        Bounds bounds = piece.GetRenderBounds();
        return bounds.min.x >= -gridHalfSize && bounds.max.x <= gridHalfSize &&
               bounds.min.z >= -gridHalfSize && bounds.max.z <= gridHalfSize;
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

    private static Vector3 Flatten(Vector3 direction)
    {
        direction.y = 0f;
        return direction.normalized;
    }

    private static bool IsInsidePalette(Vector2 screenPosition)
    {
        return screenPosition.y >= Screen.height * (1f - PaletteHeight);
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private static Material CreateRuntimeMaterial(string materialName, Color color, bool unlit)
    {
        Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find(unlit ? "Unlit/Color" : "Standard");
        }

        Material material = new Material(shader) { name = materialName, color = color };
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        return material;
    }

    private void OnGUI()
    {
        EnsureGuiStyles();

        float palettePixels = Screen.height * PaletteHeight;
        GUI.Label(new Rect(18f, 7f, 360f, 34f), "AVAILABLE PIECES", titleStyle);
        GUI.Label(new Rect(0f, palettePixels - 34f, Screen.width * 0.5f, 28f), "STRAIGHT", paletteLabelStyle);
        GUI.Label(new Rect(Screen.width * 0.5f, palettePixels - 34f, Screen.width * 0.5f, 28f), "RIGHT 45° CURVE", paletteLabelStyle);

        GUI.Box(new Rect(12f, Screen.height - 58f, Screen.width - 24f, 46f), status, statusStyle);

        float buttonY = Screen.height - 112f;
        if (awaitingPieceValidation)
        {
            if (GUI.Button(new Rect(Screen.width - 360f, buttonY, 170f, 44f), "CONFIRM PIECE", buttonStyle))
            {
                ConfirmPendingPiece();
            }

            if (GUI.Button(new Rect(Screen.width - 180f, buttonY, 150f, 44f), "CANCELAR", buttonStyle))
            {
                CancelPendingPiece();
            }
        }
        else if (GUI.Button(new Rect(Screen.width - 230f, buttonY, 200f, 44f), "VALIDATE CIRCUIT", buttonStyle))
        {
            ValidateCircuit();
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
        paletteLabelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 15,
            fontStyle = FontStyle.Bold,
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
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        Camera newBuildCamera,
        CircuitPiece newStartPiecePrefab,
        CircuitPiece newStraightPiecePrefab,
        CircuitPiece newCurve45RightPiecePrefab)
    {
        buildCamera = newBuildCamera;
        startPiecePrefab = newStartPiecePrefab;
        straightPiecePrefab = newStraightPiecePrefab;
        curve45RightPiecePrefab = newCurve45RightPiecePrefab;
    }
#endif
}
