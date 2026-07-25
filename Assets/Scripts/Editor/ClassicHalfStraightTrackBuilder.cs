using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Creates a classic-reference straight with half the length of the existing
/// eight-unit straight, while preserving the original piece unchanged.
/// </summary>
public static class ClassicHalfStraightTrackBuilder
{
    private const string RootFolder = "Assets/Art/ClassicReferenceTrack/HalfStraight";
    private const string MeshFolder = RootFolder + "/Meshes";
    private const string PiecesScenePath = "Assets/Scenes/PiecesCircuit.unity";
    private const string LevelScenePath = "Assets/Scenes/Level01.unity";
    private const string PrefabPath = "Assets/Prefabs/CircuitEditor/HalfStraightPiece.prefab";
    private const string SpritePath = "Assets/Sprites/HalfStraightPiece.png";
    private const string WoodMaterialPath =
        "Assets/Art/ClassicReferenceTrack/Materials/ClassicReferenceWood.mat";
    private const string MetalMaterialPath =
        "Assets/Art/ClassicReferenceTrack/Materials/ClassicReferenceMetal.mat";
    private const string RailMountMeshPath =
        "Assets/Art/ClassicReferenceTrack/Curve90/Meshes/OuterStartRailMount.mesh";
    private const string TrackRootName = "Half Straight Classic Reference";
    private const string PaletteCardName = "Half Straight Piece Card";
    private const string AutomaticCreationSessionKey =
        "BallPuzzle.ClassicHalfStraightTrackBuilder.AutomaticCreationQueued";
    private const string AutomaticGameplaySetupSessionKey =
        "BallPuzzle.ClassicHalfStraightTrackBuilder.AutomaticGameplaySetupQueued";

    private const float Length = 4f;
    private const float HalfLength = Length * 0.5f;
    private const float BodyWidth = 2.35f;
    private const float LaneWidth = 1.88f;
    private const float RailOffset = 0.98f;
    private const float RailMountInset = 0.55f;
    private const float SocketEndHeight = 1.045f;
    private const float BendStartHeight = 1.14f;
    private const float RailBendRadius = 0.16f;
    private const int PreviewLayer = 30;

    [InitializeOnLoadMethod]
    private static void QueueAutomaticCreation()
    {
        if (Application.isBatchMode ||
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null ||
            SessionState.GetBool(AutomaticCreationSessionKey, false))
        {
            return;
        }

        SessionState.SetBool(AutomaticCreationSessionKey, true);
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                SessionState.SetBool(AutomaticCreationSessionKey, false);
                QueueAutomaticCreation();
                return;
            }

            try
            {
                CreateClassicHalfStraight();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetBool(AutomaticCreationSessionKey, false);
            }
        };
    }

    [InitializeOnLoadMethod]
    private static void QueueAutomaticGameplaySetup()
    {
        if (Application.isBatchMode ||
            SessionState.GetBool(AutomaticGameplaySetupSessionKey, false))
        {
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            return;
        }

        SessionState.SetBool(AutomaticGameplaySetupSessionKey, true);
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                SessionState.SetBool(AutomaticGameplaySetupSessionKey, false);
                QueueAutomaticGameplaySetup();
                return;
            }

            try
            {
                UpgradeGameplayPrefab();
                ConfigureOpenLevelForGameplay();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetBool(AutomaticGameplaySetupSessionKey, false);
            }
        };
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }

        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        QueueAutomaticGameplaySetup();
    }

    private static void UpgradeGameplayPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            return;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            ConfigureCircuitPiece(prefabRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void ConfigureOpenLevelForGameplay()
    {
        Scene levelScene = SceneManager.GetActiveScene();
        if (!levelScene.IsValid() || levelScene.path != LevelScenePath)
        {
            return;
        }

        BallPuzzleLevelController controller =
            Object.FindFirstObjectByType<BallPuzzleLevelController>();
        Transform cardTransform = FindTransform(levelScene, PaletteCardName);
        Transform summaryTransform = FindTransform(levelScene, "Palette Summary");
        CircuitPiece prefab = AssetDatabase.LoadAssetAtPath<CircuitPiece>(PrefabPath);
        PieceSelectionCard card =
            cardTransform != null ? cardTransform.GetComponent<PieceSelectionCard>() : null;
        TMP_Text summary =
            summaryTransform != null ? summaryTransform.GetComponent<TMP_Text>() : null;
        if (controller == null || card == null || summary == null || prefab == null)
        {
            throw new System.InvalidOperationException(
                "No se pudo configurar la media recta jugable en Level01.");
        }

        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("halfStraightPiecePrefab").objectReferenceValue = prefab;
        serializedController.FindProperty("availableHalfStraights").intValue = 2;
        serializedController.FindProperty("halfStraightPieceCard").objectReferenceValue = card;
        serializedController.FindProperty("paletteSummaryLabel").objectReferenceValue = summary;
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject serializedCard = new SerializedObject(card);
        serializedCard.FindProperty("activeInPalette").boolValue = true;
        serializedCard.FindProperty("lockedInPalette").boolValue = false;
        serializedCard.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(card);
        EditorSceneManager.MarkSceneDirty(levelScene);
        if (!EditorSceneManager.SaveScene(levelScene))
        {
            throw new System.InvalidOperationException("No se pudo guardar " + LevelScenePath);
        }
    }

    [MenuItem("Tools/Ball Puzzle/Create Classic Half Straight")]
    public static void CreateClassicHalfStraight()
    {
        EnsureFolder("Assets/Art");
        EnsureFolder("Assets/Art/ClassicReferenceTrack");
        EnsureFolder(RootFolder);
        EnsureFolder(MeshFolder);
        EnsureFolder("Assets/Prefabs");
        EnsureFolder("Assets/Prefabs/CircuitEditor");
        EnsureFolder("Assets/Sprites");

        Material woodMaterial = AssetDatabase.LoadAssetAtPath<Material>(WoodMaterialPath);
        Material metalMaterial = AssetDatabase.LoadAssetAtPath<Material>(MetalMaterialPath);
        Mesh railMountMesh = AssetDatabase.LoadAssetAtPath<Mesh>(RailMountMeshPath);
        if (woodMaterial == null || metalMaterial == null || railMountMesh == null)
        {
            throw new System.InvalidOperationException(
                "La recta corta necesita los materiales clasicos y un soporte de barandilla existente.");
        }

        Scene piecesScene = EditorSceneManager.OpenScene(PiecesScenePath, OpenSceneMode.Single);
        GameObject raceRoad = GameObject.Find("RaceRoad");
        if (raceRoad == null)
        {
            throw new System.InvalidOperationException("PiecesCircuit no contiene el objeto RaceRoad.");
        }

        Transform previous = raceRoad.transform.Find(TrackRootName);
        if (previous != null)
        {
            Object.DestroyImmediate(previous.gameObject);
        }

        GameObject trackRoot = BuildHierarchy(woodMaterial, metalMaterial, railMountMesh);
        trackRoot.transform.SetParent(raceRoad.transform, false);
        trackRoot.transform.localPosition = new Vector3(14f, 0f, -4f);
        trackRoot.transform.localRotation = Quaternion.identity;

        GameObject prefabSource = Object.Instantiate(trackRoot);
        prefabSource.name = "HalfStraightPiece";
        prefabSource.transform.SetParent(null, true);
        prefabSource.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        ConfigureCircuitPiece(prefabSource);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(prefabSource, PrefabPath);
        Object.DestroyImmediate(prefabSource);
        if (prefab == null)
        {
            throw new System.InvalidOperationException("No se pudo crear " + PrefabPath);
        }

        EditorSceneManager.MarkSceneDirty(piecesScene);
        if (!EditorSceneManager.SaveScene(piecesScene))
        {
            throw new System.InvalidOperationException("No se pudo guardar " + PiecesScenePath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        RenderPaletteSprite(prefab);
        InstallLockedPaletteCard();

        piecesScene = EditorSceneManager.OpenScene(PiecesScenePath, OpenSceneMode.Single);
        raceRoad = GameObject.Find("RaceRoad");
        Transform created = raceRoad != null ? raceRoad.transform.Find(TrackRootName) : null;
        Selection.activeGameObject = created != null ? created.gameObject : null;
        Debug.Log("Created classic half straight, prefab, sprite, and Level01 palette card.");
    }

    private static GameObject BuildHierarchy(
        Material woodMaterial,
        Material metalMaterial,
        Mesh railMountMesh)
    {
        GameObject root = new GameObject(TrackRootName);

        CreateMeshPart(root.transform, "Rounded Wooden Body", GetBodyMesh(), woodMaterial, Vector3.zero);
        CreateMeshPart(
            root.transform,
            "Concave Wooden Lane",
            GetLaneMesh(),
            woodMaterial,
            new Vector3(0f, 0.75f, 0f));
        CreateMeshPart(
            root.transform,
            "Left Wooden Rim",
            GetRimMesh(-1f),
            woodMaterial,
            new Vector3(-1.02f, 0.64f, 0f));
        CreateMeshPart(
            root.transform,
            "Right Wooden Rim",
            GetRimMesh(1f),
            woodMaterial,
            new Vector3(1.02f, 0.64f, 0f));

        CreateMeshPart(
            root.transform,
            "Left Curved Metal Guard",
            GetRailMesh(-1f),
            metalMaterial,
            Vector3.zero);
        CreateMeshPart(
            root.transform,
            "Right Curved Metal Guard",
            GetRailMesh(1f),
            metalMaterial,
            Vector3.zero);

        float mountPosition = HalfLength - RailMountInset;
        CreateMeshPart(
            root.transform,
            "Left Front Rail Mount",
            railMountMesh,
            metalMaterial,
            new Vector3(-RailOffset, 0.90f, -mountPosition));
        CreateMeshPart(
            root.transform,
            "Left Back Rail Mount",
            railMountMesh,
            metalMaterial,
            new Vector3(-RailOffset, 0.90f, mountPosition));
        CreateMeshPart(
            root.transform,
            "Right Front Rail Mount",
            railMountMesh,
            metalMaterial,
            new Vector3(RailOffset, 0.90f, -mountPosition));
        CreateMeshPart(
            root.transform,
            "Right Back Rail Mount",
            railMountMesh,
            metalMaterial,
            new Vector3(RailOffset, 0.90f, mountPosition));

        CreateConnection(root.transform, "Start Connection", new Vector3(0f, 0f, -HalfLength), 180f);
        CreateConnection(root.transform, "End Connection", new Vector3(0f, 0f, HalfLength), 0f);
        return root;
    }

    private static void ConfigureCircuitPiece(GameObject root)
    {
        Transform startConnection = root.transform.Find("Start Connection");
        Transform endConnection = root.transform.Find("End Connection");
        if (startConnection == null || endConnection == null)
        {
            throw new System.InvalidOperationException(
                "La media recta necesita sus conexiones Start y End.");
        }

        CircuitPiece piece = root.GetComponent<CircuitPiece>();
        if (piece == null)
        {
            piece = root.AddComponent<CircuitPiece>();
        }
        piece.ConfigureForEditor(
            CircuitPieceType.HalfStraight,
            "Media recta",
            new[] { startConnection, endConnection },
            0);
        EditorUtility.SetDirty(piece);
    }

    private static void CreateMeshPart(
        Transform parent,
        string objectName,
        Mesh mesh,
        Material material,
        Vector3 localPosition)
    {
        GameObject part = new GameObject(objectName);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;

        MeshFilter filter = part.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        MeshCollider collider = part.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
    }

    private static void CreateConnection(
        Transform parent,
        string objectName,
        Vector3 localPosition,
        float yaw)
    {
        GameObject connection = new GameObject(objectName);
        connection.transform.SetParent(parent, false);
        connection.transform.localPosition = localPosition;
        connection.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private static Mesh GetBodyMesh()
    {
        const int cornerSegments = 5;
        List<Vector3> bottom = CreateRoundedRectangleRing(
            BodyWidth,
            Length,
            0.18f,
            0f,
            cornerSegments);
        List<Vector3> middle = CreateRoundedRectangleRing(
            BodyWidth,
            Length,
            0.18f,
            0.10f,
            cornerSegments);
        List<Vector3> top = CreateRoundedRectangleRing(
            2.20f,
            Length - 0.15f,
            0.14f,
            0.38f,
            cornerSegments);
        int ringSize = bottom.Count;

        List<Vector3> vertices = new List<Vector3>(ringSize * 3 + 1);
        vertices.AddRange(bottom);
        vertices.AddRange(middle);
        vertices.AddRange(top);
        int topCenter = vertices.Count;
        vertices.Add(new Vector3(0f, 0.38f, 0f));

        List<Vector2> uvs = new List<Vector2>(vertices.Count);
        for (int index = 0; index < vertices.Count; index++)
        {
            uvs.Add(new Vector2(
                vertices[index].x * 0.5f + 0.5f,
                vertices[index].z / Length + 0.5f));
        }

        List<int> triangles = new List<int>();
        AddRingSides(triangles, 0, ringSize, ringSize);
        AddRingSides(triangles, ringSize, ringSize * 2, ringSize);
        for (int index = 0; index < ringSize; index++)
        {
            int next = (index + 1) % ringSize;
            triangles.Add(topCenter);
            triangles.Add(ringSize * 2 + next);
            triangles.Add(ringSize * 2 + index);
        }

        return SaveOrUpdateMesh(
            MeshFolder + "/RoundedWoodenBody.mesh",
            BuildMesh("RoundedWoodenBody", vertices, uvs, triangles));
    }

    private static List<Vector3> CreateRoundedRectangleRing(
        float width,
        float length,
        float radius,
        float y,
        int segmentsPerCorner)
    {
        List<Vector3> ring = new List<Vector3>(segmentsPerCorner * 4);
        float halfWidth = width * 0.5f - radius;
        float halfLength = length * 0.5f - radius;
        Vector2[] centers =
        {
            new Vector2(halfWidth, halfLength),
            new Vector2(-halfWidth, halfLength),
            new Vector2(-halfWidth, -halfLength),
            new Vector2(halfWidth, -halfLength)
        };

        for (int corner = 0; corner < 4; corner++)
        {
            float startAngle = corner * 90f;
            for (int step = 0; step < segmentsPerCorner; step++)
            {
                float angle =
                    (startAngle + step * 90f / segmentsPerCorner) * Mathf.Deg2Rad;
                Vector2 point = centers[corner] +
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                ring.Add(new Vector3(point.x, y, point.y));
            }
        }
        return ring;
    }

    private static Mesh GetLaneMesh()
    {
        const int crossSegments = 28;
        const int lengthSegments = 24;
        int rowSize = crossSegments + 1;
        List<Vector3> vertices =
            new List<Vector3>((crossSegments + 1) * (lengthSegments + 3));
        List<Vector2> uvs = new List<Vector2>(vertices.Capacity);

        for (int z = 0; z <= lengthSegments; z++)
        {
            float zT = z / (float)lengthSegments;
            float zPosition = Mathf.Lerp(-HalfLength, HalfLength, zT);
            for (int x = 0; x <= crossSegments; x++)
            {
                float t = x / (float)crossSegments;
                float xPosition = Mathf.Lerp(-LaneWidth * 0.5f, LaneWidth * 0.5f, t);
                float arch = 1f - Mathf.Pow(xPosition / (LaneWidth * 0.5f), 2f);
                vertices.Add(new Vector3(
                    xPosition,
                    -0.28f * Mathf.Clamp01(arch),
                    zPosition));
                uvs.Add(new Vector2(t, zT));
            }
        }

        List<int> triangles = new List<int>();
        for (int z = 0; z < lengthSegments; z++)
        {
            for (int x = 0; x < crossSegments; x++)
            {
                int front = z * rowSize + x;
                int back = front + rowSize;
                triangles.Add(front);
                triangles.Add(back);
                triangles.Add(back + 1);
                triangles.Add(front);
                triangles.Add(back + 1);
                triangles.Add(front + 1);
            }
        }

        int frontBottom = vertices.Count;
        int backBottom = frontBottom + rowSize;
        for (int x = 0; x <= crossSegments; x++)
        {
            float t = x / (float)crossSegments;
            float xPosition = Mathf.Lerp(-LaneWidth * 0.5f, LaneWidth * 0.5f, t);
            vertices.Add(new Vector3(xPosition, -0.38f, -HalfLength));
            uvs.Add(new Vector2(t, 0f));
        }
        for (int x = 0; x <= crossSegments; x++)
        {
            float t = x / (float)crossSegments;
            float xPosition = Mathf.Lerp(-LaneWidth * 0.5f, LaneWidth * 0.5f, t);
            vertices.Add(new Vector3(xPosition, -0.38f, HalfLength));
            uvs.Add(new Vector2(t, 1f));
        }

        int backTop = lengthSegments * rowSize;
        for (int x = 0; x < crossSegments; x++)
        {
            triangles.Add(x);
            triangles.Add(x + 1);
            triangles.Add(frontBottom + x + 1);
            triangles.Add(x);
            triangles.Add(frontBottom + x + 1);
            triangles.Add(frontBottom + x);

            triangles.Add(backTop + x);
            triangles.Add(backBottom + x + 1);
            triangles.Add(backTop + x + 1);
            triangles.Add(backTop + x);
            triangles.Add(backBottom + x);
            triangles.Add(backBottom + x + 1);
        }

        return SaveOrUpdateMesh(
            MeshFolder + "/ConcaveWoodenLane.mesh",
            BuildMesh("ConcaveWoodenLane", vertices, uvs, triangles));
    }

    private static Mesh GetRimMesh(float side)
    {
        string name = side < 0f ? "LeftWoodenRim" : "RightWoodenRim";
        Mesh mesh = CreateRoundedBoxMesh(0.30f, 0.52f, Length, 0.065f);
        mesh.name = name;
        return SaveOrUpdateMesh(MeshFolder + "/" + name + ".mesh", mesh);
    }

    private static Mesh CreateRoundedBoxMesh(
        float width,
        float height,
        float length,
        float radius)
    {
        const int segments = 4;
        List<Vector3> bottom = CreateRoundedRectangleRing(
            width,
            length,
            radius,
            -height * 0.5f,
            segments);
        List<Vector3> top = CreateRoundedRectangleRing(
            width - 0.03f,
            length - 0.03f,
            Mathf.Max(0.01f, radius - 0.015f),
            height * 0.5f,
            segments);
        int ringSize = bottom.Count;

        List<Vector3> vertices = new List<Vector3>(ringSize * 2 + 2);
        vertices.AddRange(bottom);
        vertices.AddRange(top);
        int bottomCenter = vertices.Count;
        vertices.Add(new Vector3(0f, -height * 0.5f, 0f));
        int topCenter = vertices.Count;
        vertices.Add(new Vector3(0f, height * 0.5f, 0f));

        List<Vector2> uvs = new List<Vector2>(vertices.Count);
        for (int index = 0; index < vertices.Count; index++)
        {
            uvs.Add(new Vector2(
                vertices[index].x / width + 0.5f,
                vertices[index].z / length + 0.5f));
        }

        List<int> triangles = new List<int>();
        AddRingSides(triangles, 0, ringSize, ringSize);
        for (int index = 0; index < ringSize; index++)
        {
            int next = (index + 1) % ringSize;
            triangles.Add(bottomCenter);
            triangles.Add(index);
            triangles.Add(next);
            triangles.Add(topCenter);
            triangles.Add(ringSize + next);
            triangles.Add(ringSize + index);
        }
        return BuildMesh("RoundedRim", vertices, uvs, triangles);
    }

    private static Mesh GetRailMesh(float side)
    {
        string name = side < 0f ? "LeftCurvedMetalGuard" : "RightCurvedMetalGuard";
        Mesh mesh = CreateTubeMesh(CreateRailPath(side), 0.075f, 14);
        mesh.name = name;
        return SaveOrUpdateMesh(MeshFolder + "/" + name + ".mesh", mesh);
    }

    private static List<Vector3> CreateRailPath(float side)
    {
        const int curveSegments = 12;
        float mountZ = HalfLength - RailMountInset;
        float straightHeight = BendStartHeight + RailBendRadius;
        List<Vector3> points = new List<Vector3>();

        points.Add(new Vector3(side * RailOffset, SocketEndHeight, -HalfLength));
        points.Add(new Vector3(side * RailOffset, SocketEndHeight, -mountZ));
        for (int index = 0; index <= curveSegments; index++)
        {
            float angle = index / (float)curveSegments * Mathf.PI * 0.5f;
            points.Add(new Vector3(
                side * RailOffset,
                BendStartHeight + RailBendRadius * Mathf.Sin(angle),
                -mountZ + RailBendRadius * (1f - Mathf.Cos(angle))));
        }

        points.Add(new Vector3(
            side * RailOffset,
            straightHeight,
            mountZ - RailBendRadius));
        for (int index = 1; index <= curveSegments; index++)
        {
            float angle = index / (float)curveSegments * Mathf.PI * 0.5f;
            points.Add(new Vector3(
                side * RailOffset,
                BendStartHeight + RailBendRadius * Mathf.Cos(angle),
                mountZ - RailBendRadius + RailBendRadius * Mathf.Sin(angle)));
        }
        points.Add(new Vector3(side * RailOffset, SocketEndHeight, mountZ));
        points.Add(new Vector3(side * RailOffset, SocketEndHeight, HalfLength));
        return points;
    }

    private static Mesh CreateTubeMesh(List<Vector3> path, float radius, int sides)
    {
        List<Vector3> vertices = new List<Vector3>(path.Count * sides);
        List<Vector2> uvs = new List<Vector2>(vertices.Capacity);
        List<int> triangles = new List<int>((path.Count - 1) * sides * 6);

        for (int index = 0; index < path.Count; index++)
        {
            Vector3 tangent = index == 0
                ? path[1] - path[0]
                : index == path.Count - 1
                    ? path[index] - path[index - 1]
                    : path[index + 1] - path[index - 1];
            tangent.Normalize();
            Vector3 sideways = Vector3.right;
            Vector3 upward = Vector3.Cross(tangent, sideways).normalized;

            for (int side = 0; side < sides; side++)
            {
                float angle = side / (float)sides * Mathf.PI * 2f;
                vertices.Add(path[index] +
                    (sideways * Mathf.Cos(angle) + upward * Mathf.Sin(angle)) * radius);
                uvs.Add(new Vector2(side / (float)sides, index / (float)(path.Count - 1)));
            }
        }

        for (int ring = 0; ring < path.Count - 1; ring++)
        {
            for (int side = 0; side < sides; side++)
            {
                int nextSide = (side + 1) % sides;
                int a = ring * sides + side;
                int b = ring * sides + nextSide;
                int c = (ring + 1) * sides + nextSide;
                int d = (ring + 1) * sides + side;
                triangles.Add(a);
                triangles.Add(d);
                triangles.Add(c);
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);
            }
        }
        return BuildMesh("StraightTube", vertices, uvs, triangles);
    }

    private static void AddRingSides(
        List<int> triangles,
        int lowerStart,
        int upperStart,
        int ringSize)
    {
        for (int index = 0; index < ringSize; index++)
        {
            int next = (index + 1) % ringSize;
            triangles.Add(lowerStart + index);
            triangles.Add(upperStart + index);
            triangles.Add(upperStart + next);
            triangles.Add(lowerStart + index);
            triangles.Add(upperStart + next);
            triangles.Add(lowerStart + next);
        }
    }

    private static Mesh BuildMesh(
        string name,
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles)
    {
        Mesh mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh SaveOrUpdateMesh(string path, Mesh generatedMesh)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(generatedMesh, path);
            return generatedMesh;
        }

        existing.Clear();
        existing.name = generatedMesh.name;
        existing.vertices = generatedMesh.vertices;
        existing.uv = generatedMesh.uv;
        existing.triangles = generatedMesh.triangles;
        existing.RecalculateNormals();
        existing.RecalculateBounds();
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(generatedMesh);
        return existing;
    }

    private static void RenderPaletteSprite(GameObject prefab)
    {
        GameObject previewRoot = null;
        GameObject cameraObject = null;
        GameObject lightObject = null;
        RenderTexture renderTexture = null;
        Texture2D texture = null;
        RenderTexture previousTarget = RenderTexture.active;
        AmbientMode previousAmbientMode = RenderSettings.ambientMode;
        Color previousAmbientLight = RenderSettings.ambientLight;

        try
        {
            previewRoot = Object.Instantiate(prefab);
            previewRoot.name = "Half Straight Sprite Preview";
            previewRoot.transform.SetPositionAndRotation(
                Vector3.zero,
                Quaternion.Euler(0f, -24f, 0f));
            SetLayerRecursively(previewRoot, PreviewLayer);
            foreach (Collider collider in previewRoot.GetComponentsInChildren<Collider>())
            {
                collider.enabled = false;
            }

            Bounds bounds = GetRenderBounds(previewRoot);
            cameraObject = new GameObject("Half Straight Sprite Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << PreviewLayer;
            camera.orthographic = true;
            camera.orthographicSize = bounds.extents.magnitude * 1.02f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            Vector3 viewDirection = new Vector3(1.20f, 1.05f, -1.35f).normalized;
            camera.transform.position = bounds.center + viewDirection * 18f;
            camera.transform.LookAt(bounds.center + Vector3.up * 0.12f);

            lightObject = new GameObject("Half Straight Sprite Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.color = new Color(1f, 0.94f, 0.86f);
            light.cullingMask = 1 << PreviewLayer;
            lightObject.transform.rotation = Quaternion.Euler(48f, -34f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.46f, 0.49f, 0.54f);

            renderTexture = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 8
            };
            renderTexture.Create();
            camera.targetTexture = renderTexture;
            camera.Render();

            RenderTexture.active = renderTexture;
            texture = new Texture2D(512, 512, TextureFormat.RGBA32, false, false);
            texture.ReadPixels(new Rect(0f, 0f, 512f, 512f), 0, 0);
            texture.Apply(false, false);
            MatchClassicThumbnailWoodTone(texture);

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string absolutePath = Path.Combine(
                projectRoot,
                SpritePath.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previousTarget;
            RenderSettings.ambientMode = previousAmbientMode;
            RenderSettings.ambientLight = previousAmbientLight;
            if (texture != null)
            {
                Object.DestroyImmediate(texture);
            }
            if (renderTexture != null)
            {
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
            }
            if (cameraObject != null)
            {
                Object.DestroyImmediate(cameraObject);
            }
            if (lightObject != null)
            {
                Object.DestroyImmediate(lightObject);
            }
            if (previewRoot != null)
            {
                Object.DestroyImmediate(previewRoot);
            }
        }

        AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
        if (importer == null)
        {
            throw new System.InvalidOperationException("No se pudo importar el sprite de la recta corta.");
        }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 512;
        importer.SaveAndReimport();
    }

    private static void MatchClassicThumbnailWoodTone(Texture2D texture)
    {
        Color32[] pixels = texture.GetPixels32();
        for (int index = 0; index < pixels.Length; index++)
        {
            Color32 pixel = pixels[index];
            if (pixel.a == 0 || pixel.r <= 55 || pixel.r <= pixel.g * 1.35f ||
                pixel.g <= pixel.b * 1.15f || pixel.b >= 135)
            {
                continue;
            }

            pixel.r = (byte)Mathf.Clamp(Mathf.RoundToInt(pixel.r * 0.20f + 100f), 0, 255);
            pixel.g = (byte)Mathf.Clamp(Mathf.RoundToInt(pixel.g * 0.25f + 50f), 0, 255);
            pixel.b = (byte)Mathf.Clamp(Mathf.RoundToInt(pixel.b * 0.25f + 33f), 0, 255);
            pixels[index] = pixel;
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
    }

    private static void InstallLockedPaletteCard()
    {
        Scene levelScene = EditorSceneManager.OpenScene(LevelScenePath, OpenSceneMode.Single);
        Transform cardsRoot = FindTransform(levelScene, "Piece Cards");
        Transform sourceCard = FindTransform(levelScene, "Curve 90 Piece Card");
        Transform orderReference = FindTransform(levelScene, "Straight Piece Card");
        if (cardsRoot == null || sourceCard == null || orderReference == null)
        {
            throw new System.InvalidOperationException(
                "Level01 no contiene la paleta o las tarjetas de referencia necesarias.");
        }

        Transform cardTransform = FindTransform(levelScene, PaletteCardName);
        if (cardTransform == null)
        {
            GameObject cardObject = Object.Instantiate(sourceCard.gameObject, cardsRoot);
            cardObject.name = PaletteCardName;
            cardTransform = cardObject.transform;
        }
        cardTransform.SetSiblingIndex(orderReference.GetSiblingIndex() + 1);

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (sprite == null)
        {
            throw new System.InvalidOperationException("No se encontro el sprite " + SpritePath);
        }

        foreach (Image image in cardTransform.GetComponentsInChildren<Image>(true))
        {
            if (image.gameObject.name == "Piece Thumbnail")
            {
                image.sprite = sprite;
                image.preserveAspect = true;
            }
        }
        foreach (TMP_Text label in cardTransform.GetComponentsInChildren<TMP_Text>(true))
        {
            if (label.gameObject.name == "Name")
            {
                label.text = "HALF STRAIGHT";
            }
        }

        PieceSelectionCard card = cardTransform.GetComponent<PieceSelectionCard>();
        if (card == null)
        {
            throw new System.InvalidOperationException("La nueva tarjeta no contiene PieceSelectionCard.");
        }
        SerializedObject serializedCard = new SerializedObject(card);
        serializedCard.FindProperty("activeInPalette").boolValue = true;
        serializedCard.FindProperty("lockedInPalette").boolValue = false;
        serializedCard.ApplyModifiedPropertiesWithoutUndo();
        card.SetState(true, true, false, 2);

        BallPuzzleLevelController controller =
            Object.FindFirstObjectByType<BallPuzzleLevelController>();
        if (controller == null)
        {
            throw new System.InvalidOperationException("Level01 no contiene BallPuzzleLevelController.");
        }

        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("halfStraightPiecePrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<CircuitPiece>(PrefabPath);
        serializedController.FindProperty("availableHalfStraights").intValue = 2;
        serializedController.FindProperty("halfStraightPieceCard").objectReferenceValue = card;
        SerializedProperty lockedCards = serializedController.FindProperty("lockedPieceCards");
        bool alreadyRegistered = false;
        for (int index = 0; index < lockedCards.arraySize; index++)
        {
            if (lockedCards.GetArrayElementAtIndex(index).objectReferenceValue == card)
            {
                alreadyRegistered = true;
                break;
            }
        }
        if (!alreadyRegistered)
        {
            int newIndex = lockedCards.arraySize;
            lockedCards.InsertArrayElementAtIndex(newIndex);
            lockedCards.GetArrayElementAtIndex(newIndex).objectReferenceValue = card;
        }
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        Transform summaryTransform = FindTransform(levelScene, "Palette Summary");
        TMP_Text summary = summaryTransform != null ? summaryTransform.GetComponent<TMP_Text>() : null;
        if (summary != null)
        {
            summary.text = "3 AVAILABLE  \u00B7  " +
                           Mathf.Max(0, lockedCards.arraySize - 1) + " LOCKED";
            serializedController.FindProperty("paletteSummaryLabel").objectReferenceValue = summary;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(levelScene);
        if (!EditorSceneManager.SaveScene(levelScene))
        {
            throw new System.InvalidOperationException("No se pudo guardar " + LevelScenePath);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static Bounds GetRenderBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return new Bounds(root.transform.position, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }
        return bounds;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private static Transform FindTransform(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform result = FindTransform(root.transform, objectName);
            if (result != null)
            {
                return result;
            }
        }
        return null;
    }

    private static Transform FindTransform(Transform current, string objectName)
    {
        if (current.name == objectName)
        {
            return current;
        }
        foreach (Transform child in current)
        {
            Transform result = FindTransform(child, objectName);
            if (result != null)
            {
                return result;
            }
        }
        return null;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        string folder = path.Substring(slash + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder);
    }
}
