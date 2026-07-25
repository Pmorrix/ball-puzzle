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
/// Creates a continuous 180-degree classic-reference curve, its visual prefab,
/// its palette sprite, and a locked Level01 palette card.
/// </summary>
public static class Classic180CurveTrackBuilder
{
    private const string RootFolder = "Assets/Art/ClassicReferenceTrack/Curve180";
    private const string MeshFolder = RootFolder + "/Meshes";
    private const string PiecesScenePath = "Assets/Scenes/PiecesCircuit.unity";
    private const string LevelScenePath = "Assets/Scenes/Level01.unity";
    private const string PrefabPath = "Assets/Prefabs/CircuitEditor/Curve180Piece.prefab";
    private const string SpritePath = "Assets/Sprites/Curve180Piece.png";
    private const string WoodMaterialPath =
        "Assets/Art/ClassicReferenceTrack/Materials/ClassicReferenceWood.mat";
    private const string MetalMaterialPath =
        "Assets/Art/ClassicReferenceTrack/Materials/ClassicReferenceMetal.mat";
    private const string RailMountMeshPath =
        "Assets/Art/ClassicReferenceTrack/Curve90/Meshes/OuterStartRailMount.mesh";
    private const string TrackRootName = "180 Degree Curve Classic Reference";
    private const string PaletteCardName = "Curve 180 Piece Card";

    private const float CenterRadius = 5f;
    private const float ArcAngle = 180f;
    private const int ArcSegments = 128;
    private const int ConnectorArcSegments = 8;
    private const float BodyWidth = 2.35f;
    private const float LaneWidth = 1.88f;
    private const float RailOffset = 0.98f;
    private const float RailMountInset = 0.55f;
    private const float SocketEndHeight = 1.045f;
    private const float BendStartHeight = 1.14f;
    private const float RailBendRadius = 0.16f;
    private const float RailHeight = BendStartHeight + RailBendRadius;
    private const int PreviewLayer = 30;
    private const string AutomaticCreationSessionKey =
        "BallPuzzle.Classic180CurveTrackBuilder.AutomaticCreationQueued";

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
                CreateClassic180DegreeCurve();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetBool(AutomaticCreationSessionKey, false);
            }
        };
    }

    [MenuItem("Tools/Ball Puzzle/Create Classic 180 Degree Curve")]
    public static void CreateClassic180DegreeCurve()
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
                "La curva de 180 grados necesita los materiales clasicos y el soporte de la curva de 90 grados.");
        }

        if (!Application.isBatchMode)
        {
            EditorSceneManager.SaveOpenScenes();
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
        trackRoot.transform.localPosition = new Vector3(14f, 0f, 8f);
        trackRoot.transform.localRotation = Quaternion.identity;

        GameObject prefabSource = Object.Instantiate(trackRoot);
        prefabSource.name = "Curve180Piece";
        prefabSource.transform.SetParent(null, true);
        prefabSource.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
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
        Debug.Log("Created continuous classic 180-degree curve, prefab, sprite, and Level01 palette card.");
    }

    private static GameObject BuildHierarchy(
        Material woodMaterial,
        Material metalMaterial,
        Mesh railMountMesh)
    {
        GameObject root = new GameObject(TrackRootName);

        CreateMeshPart(root.transform, "Curved Wooden Body", GetBodyMesh(), woodMaterial, Vector3.zero);
        CreateMeshPart(
            root.transform,
            "Concave Curved Wooden Lane",
            GetLaneMesh(),
            woodMaterial,
            new Vector3(0f, 0.75f, 0f));
        CreateMeshPart(
            root.transform,
            "Inner Wooden Rim",
            GetRimMesh(-1f),
            woodMaterial,
            new Vector3(0f, 0.64f, 0f));
        CreateMeshPart(
            root.transform,
            "Outer Wooden Rim",
            GetRimMesh(1f),
            woodMaterial,
            new Vector3(0f, 0.64f, 0f));

        CreateMeshPart(
            root.transform,
            "Inner Curved Metal Guard",
            GetRailMesh(-1f),
            metalMaterial,
            Vector3.zero);
        CreateMeshPart(
            root.transform,
            "Outer Curved Metal Guard",
            GetRailMesh(1f),
            metalMaterial,
            Vector3.zero);

        float startAngle = GetMountInsetAngle();
        float endAngle = ArcAngle * Mathf.Deg2Rad - startAngle;
        CreateMount(root.transform, "Inner Start Rail Mount", -1f, startAngle, railMountMesh, metalMaterial);
        CreateMount(root.transform, "Inner End Rail Mount", -1f, endAngle, railMountMesh, metalMaterial);
        CreateMount(root.transform, "Outer Start Rail Mount", 1f, startAngle, railMountMesh, metalMaterial);
        CreateMount(root.transform, "Outer End Rail Mount", 1f, endAngle, railMountMesh, metalMaterial);

        CreateConnection(root.transform, "Start Connection", PointAt(CenterRadius, 0f, 0f), 180f);
        CreateConnection(
            root.transform,
            "End Connection",
            PointAt(CenterRadius, ArcAngle * Mathf.Deg2Rad, 0f),
            180f);
        return root;
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

    private static void CreateMount(
        Transform parent,
        string objectName,
        float side,
        float angle,
        Mesh mesh,
        Material material)
    {
        Vector3 position = PointAt(CenterRadius + side * RailOffset, angle, 0.90f);
        CreateMeshPart(parent, objectName, mesh, material, position);
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
        const float halfBottom = BodyWidth * 0.5f;
        const float halfTop = 1.10f;
        const int sectionSize = 6;

        List<Vector3> vertices = new List<Vector3>((ArcSegments + 1) * sectionSize + 2);
        List<Vector2> uvs = new List<Vector2>(vertices.Capacity);
        for (int segment = 0; segment <= ArcSegments; segment++)
        {
            float t = segment / (float)ArcSegments;
            float angle = t * ArcAngle * Mathf.Deg2Rad;
            AddSectionVertex(vertices, uvs, CenterRadius - halfBottom, angle, 0f, 0f, t);
            AddSectionVertex(vertices, uvs, CenterRadius + halfBottom, angle, 0f, 1f, t);
            AddSectionVertex(vertices, uvs, CenterRadius - halfBottom, angle, 0.10f, 0f, t);
            AddSectionVertex(vertices, uvs, CenterRadius + halfBottom, angle, 0.10f, 1f, t);
            AddSectionVertex(vertices, uvs, CenterRadius - halfTop, angle, 0.38f, 0.03f, t);
            AddSectionVertex(vertices, uvs, CenterRadius + halfTop, angle, 0.38f, 0.97f, t);
        }

        List<int> triangles = new List<int>();
        for (int segment = 0; segment < ArcSegments; segment++)
        {
            int current = segment * sectionSize;
            int next = (segment + 1) * sectionSize;
            AddQuad(triangles, current, current + 1, next + 1, next);
            AddQuad(triangles, current, next, next + 2, current + 2);
            AddQuad(triangles, current + 1, current + 3, next + 3, next + 1);
            AddQuad(triangles, current + 2, next + 2, next + 4, current + 4);
            AddQuad(triangles, current + 3, current + 5, next + 5, next + 3);
            AddQuad(triangles, current + 4, next + 4, next + 5, current + 5);
        }

        AddEndCaps(vertices, uvs, triangles, sectionSize, new[] { 0, 1, 3, 5, 4, 2 }, 0.19f);
        return SaveOrUpdateMesh(
            MeshFolder + "/CurvedWoodenBody.mesh",
            BuildMesh("CurvedWoodenBody", vertices, uvs, triangles));
    }

    private static Mesh GetLaneMesh()
    {
        const int crossSegments = 28;
        int rowSize = crossSegments + 1;
        float endAngle = ArcAngle * Mathf.Deg2Rad;

        List<Vector3> vertices = new List<Vector3>((ArcSegments + 3) * rowSize);
        List<Vector2> uvs = new List<Vector2>(vertices.Capacity);
        for (int segment = 0; segment <= ArcSegments; segment++)
        {
            float lengthT = segment / (float)ArcSegments;
            float angle = Mathf.Lerp(0f, endAngle, lengthT);
            for (int cross = 0; cross <= crossSegments; cross++)
            {
                float t = cross / (float)crossSegments;
                float offset = Mathf.Lerp(-LaneWidth * 0.5f, LaneWidth * 0.5f, t);
                float arch = 1f - Mathf.Pow(offset / (LaneWidth * 0.5f), 2f);
                vertices.Add(PointAt(CenterRadius + offset, angle, -0.28f * Mathf.Clamp01(arch)));
                uvs.Add(new Vector2(t, lengthT));
            }
        }

        List<int> triangles = new List<int>();
        for (int segment = 0; segment < ArcSegments; segment++)
        {
            for (int cross = 0; cross < crossSegments; cross++)
            {
                int current = segment * rowSize + cross;
                int nextRow = current + rowSize;
                triangles.Add(current);
                triangles.Add(nextRow);
                triangles.Add(nextRow + 1);
                triangles.Add(current);
                triangles.Add(nextRow + 1);
                triangles.Add(current + 1);
            }
        }

        int startBottom = vertices.Count;
        int endBottom = startBottom + rowSize;
        for (int cross = 0; cross <= crossSegments; cross++)
        {
            float t = cross / (float)crossSegments;
            float offset = Mathf.Lerp(-LaneWidth * 0.5f, LaneWidth * 0.5f, t);
            vertices.Add(PointAt(CenterRadius + offset, 0f, -0.38f));
            uvs.Add(new Vector2(t, 0f));
        }
        for (int cross = 0; cross <= crossSegments; cross++)
        {
            float t = cross / (float)crossSegments;
            float offset = Mathf.Lerp(-LaneWidth * 0.5f, LaneWidth * 0.5f, t);
            vertices.Add(PointAt(CenterRadius + offset, endAngle, -0.38f));
            uvs.Add(new Vector2(t, 1f));
        }

        int endTop = ArcSegments * rowSize;
        for (int cross = 0; cross < crossSegments; cross++)
        {
            triangles.Add(cross);
            triangles.Add(cross + 1);
            triangles.Add(startBottom + cross + 1);
            triangles.Add(cross);
            triangles.Add(startBottom + cross + 1);
            triangles.Add(startBottom + cross);

            triangles.Add(endTop + cross);
            triangles.Add(endBottom + cross + 1);
            triangles.Add(endTop + cross + 1);
            triangles.Add(endTop + cross);
            triangles.Add(endBottom + cross);
            triangles.Add(endBottom + cross + 1);
        }

        return SaveOrUpdateMesh(
            MeshFolder + "/ConcaveCurvedWoodenLane.mesh",
            BuildMesh("ConcaveCurvedWoodenLane", vertices, uvs, triangles));
    }

    private static Mesh GetRimMesh(float side)
    {
        string name = side < 0f ? "InnerWoodenRim" : "OuterWoodenRim";
        float center = CenterRadius + side * 1.02f;
        const float bottomHalfWidth = 0.15f;
        const float topHalfWidth = 0.135f;
        const int sectionSize = 4;

        List<Vector3> vertices = new List<Vector3>((ArcSegments + 1) * sectionSize + 2);
        List<Vector2> uvs = new List<Vector2>(vertices.Capacity);
        for (int segment = 0; segment <= ArcSegments; segment++)
        {
            float t = segment / (float)ArcSegments;
            float angle = t * ArcAngle * Mathf.Deg2Rad;
            AddSectionVertex(vertices, uvs, center - bottomHalfWidth, angle, -0.26f, 0f, t);
            AddSectionVertex(vertices, uvs, center + bottomHalfWidth, angle, -0.26f, 1f, t);
            AddSectionVertex(vertices, uvs, center - topHalfWidth, angle, 0.26f, 0.04f, t);
            AddSectionVertex(vertices, uvs, center + topHalfWidth, angle, 0.26f, 0.96f, t);
        }

        List<int> triangles = new List<int>();
        for (int segment = 0; segment < ArcSegments; segment++)
        {
            int current = segment * sectionSize;
            int next = (segment + 1) * sectionSize;
            AddQuad(triangles, current, current + 1, next + 1, next);
            AddQuad(triangles, current, next, next + 2, current + 2);
            AddQuad(triangles, current + 1, current + 3, next + 3, next + 1);
            AddQuad(triangles, current + 2, next + 2, next + 3, current + 3);
        }

        AddEndCaps(vertices, uvs, triangles, sectionSize, new[] { 0, 1, 3, 2 }, 0f);
        return SaveOrUpdateMesh(
            MeshFolder + "/" + name + ".mesh",
            BuildMesh(name, vertices, uvs, triangles));
    }

    private static Mesh GetRailMesh(float side)
    {
        string name = side < 0f ? "InnerCurvedMetalGuard" : "OuterCurvedMetalGuard";
        List<Vector3> path = CreateRailPath(side);
        Mesh mesh = CreateTubeMesh(path, 0.075f, 14, RadialAt(0f));
        mesh.name = name;
        return SaveOrUpdateMesh(MeshFolder + "/" + name + ".mesh", mesh);
    }

    private static List<Vector3> CreateRailPath(float side)
    {
        const int bendSegments = 12;
        float radius = CenterRadius + side * RailOffset;
        float supportStart = GetMountInsetAngle();
        float supportEnd = ArcAngle * Mathf.Deg2Rad - supportStart;
        float bendAngle = RailBendRadius / radius;
        List<Vector3> points = new List<Vector3>();

        for (int segment = 0; segment <= ConnectorArcSegments; segment++)
        {
            float t = segment / (float)ConnectorArcSegments;
            points.Add(PointAt(radius, Mathf.Lerp(0f, supportStart, t), SocketEndHeight));
        }
        points.Add(PointAt(radius, supportStart, BendStartHeight));
        for (int segment = 1; segment <= bendSegments; segment++)
        {
            float bend = segment / (float)bendSegments * Mathf.PI * 0.5f;
            float travelled = RailBendRadius * (1f - Mathf.Cos(bend));
            points.Add(PointAt(
                radius,
                supportStart + travelled / radius,
                BendStartHeight + RailBendRadius * Mathf.Sin(bend)));
        }

        float mainStart = supportStart + bendAngle;
        float mainEnd = supportEnd - bendAngle;
        for (int segment = 1; segment < ArcSegments; segment++)
        {
            float t = segment / (float)ArcSegments;
            points.Add(PointAt(radius, Mathf.Lerp(mainStart, mainEnd, t), RailHeight));
        }
        points.Add(PointAt(radius, mainEnd, RailHeight));

        for (int segment = 1; segment <= bendSegments; segment++)
        {
            float bend = segment / (float)bendSegments * Mathf.PI * 0.5f;
            float travelled = RailBendRadius * Mathf.Sin(bend);
            points.Add(PointAt(
                radius,
                mainEnd + travelled / radius,
                BendStartHeight + RailBendRadius * Mathf.Cos(bend)));
        }
        points.Add(PointAt(radius, supportEnd, SocketEndHeight));
        for (int segment = 1; segment <= ConnectorArcSegments; segment++)
        {
            float t = segment / (float)ConnectorArcSegments;
            points.Add(PointAt(
                radius,
                Mathf.Lerp(supportEnd, ArcAngle * Mathf.Deg2Rad, t),
                SocketEndHeight));
        }
        return points;
    }

    private static Mesh CreateTubeMesh(
        List<Vector3> path,
        float radius,
        int sides,
        Vector3 firstNormal)
    {
        List<Vector3> vertices = new List<Vector3>(path.Count * sides);
        List<Vector2> uvs = new List<Vector2>(vertices.Capacity);
        List<int> triangles = new List<int>((path.Count - 1) * sides * 6);

        Vector3 previousTangent = Vector3.zero;
        Vector3 normal = firstNormal.normalized;
        for (int index = 0; index < path.Count; index++)
        {
            Vector3 tangent = index == 0
                ? path[1] - path[0]
                : index == path.Count - 1
                    ? path[index] - path[index - 1]
                    : path[index + 1] - path[index - 1];
            tangent.Normalize();
            if (index > 0)
            {
                normal = Quaternion.FromToRotation(previousTangent, tangent) * normal;
                normal = Vector3.ProjectOnPlane(normal, tangent).normalized;
                if (normal.sqrMagnitude < 0.5f)
                {
                    normal = Vector3.Cross(tangent, Vector3.forward).normalized;
                }
            }

            Vector3 binormal = Vector3.Cross(tangent, normal).normalized;
            for (int side = 0; side < sides; side++)
            {
                float angle = side / (float)sides * Mathf.PI * 2f;
                vertices.Add(path[index] +
                    (normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle)) * radius);
                uvs.Add(new Vector2(side / (float)sides, index / (float)(path.Count - 1)));
            }
            previousTangent = tangent;
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
        return BuildMesh("CurvedTube", vertices, uvs, triangles);
    }

    private static void AddSectionVertex(
        List<Vector3> vertices,
        List<Vector2> uvs,
        float radius,
        float angle,
        float y,
        float u,
        float v)
    {
        vertices.Add(PointAt(radius, angle, y));
        uvs.Add(new Vector2(u, v));
    }

    private static void AddEndCaps(
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles,
        int sectionSize,
        int[] boundary,
        float centerHeight)
    {
        int startCenter = vertices.Count;
        vertices.Add(PointAt(CenterRadius, 0f, centerHeight));
        uvs.Add(new Vector2(0.5f, 0f));
        int endCenter = vertices.Count;
        vertices.Add(PointAt(CenterRadius, ArcAngle * Mathf.Deg2Rad, centerHeight));
        uvs.Add(new Vector2(0.5f, 1f));

        int endStart = ArcSegments * sectionSize;
        for (int index = 0; index < boundary.Length; index++)
        {
            int next = (index + 1) % boundary.Length;
            triangles.Add(startCenter);
            triangles.Add(boundary[next]);
            triangles.Add(boundary[index]);
            triangles.Add(endCenter);
            triangles.Add(endStart + boundary[index]);
            triangles.Add(endStart + boundary[next]);
        }
    }

    private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a);
        triangles.Add(b);
        triangles.Add(c);
        triangles.Add(a);
        triangles.Add(c);
        triangles.Add(d);
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

    private static Vector3 PointAt(float radius, float angle, float y)
    {
        return new Vector3(-CenterRadius + radius * Mathf.Cos(angle), y, radius * Mathf.Sin(angle));
    }

    private static Vector3 RadialAt(float angle)
    {
        return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
    }

    private static float GetMountInsetAngle()
    {
        return RailMountInset / CenterRadius;
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
            previewRoot.name = "Curve 180 Sprite Preview";
            previewRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, -18f, 0f));
            SetLayerRecursively(previewRoot, PreviewLayer);
            foreach (Collider collider in previewRoot.GetComponentsInChildren<Collider>())
            {
                collider.enabled = false;
            }

            Bounds bounds = GetRenderBounds(previewRoot);
            cameraObject = new GameObject("Curve 180 Sprite Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << PreviewLayer;
            camera.orthographic = true;
            camera.orthographicSize = bounds.extents.magnitude * 0.92f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            Vector3 viewDirection = new Vector3(1.20f, 1.05f, -1.35f).normalized;
            camera.transform.position = bounds.center + viewDirection * 24f;
            camera.transform.LookAt(bounds.center + Vector3.up * 0.15f);

            lightObject = new GameObject("Curve 180 Sprite Light");
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
            throw new System.InvalidOperationException("No se pudo importar el sprite de la curva de 180 grados.");
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
        if (cardsRoot == null || sourceCard == null)
        {
            throw new System.InvalidOperationException(
                "Level01 no contiene la paleta o la tarjeta de la curva de 90 grados.");
        }

        Transform cardTransform = FindTransform(levelScene, PaletteCardName);
        if (cardTransform == null)
        {
            GameObject cardObject = Object.Instantiate(sourceCard.gameObject, cardsRoot);
            cardObject.name = PaletteCardName;
            cardTransform = cardObject.transform;
        }
        cardTransform.SetSiblingIndex(sourceCard.GetSiblingIndex() + 1);

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
                label.text = "180\u00B0 CURVE";
            }
        }

        PieceSelectionCard card = cardTransform.GetComponent<PieceSelectionCard>();
        if (card == null)
        {
            throw new System.InvalidOperationException("La nueva tarjeta no contiene PieceSelectionCard.");
        }
        card.SetState(false, false, false, 0);

        BallPuzzleLevelController controller =
            Object.FindFirstObjectByType<BallPuzzleLevelController>();
        if (controller == null)
        {
            throw new System.InvalidOperationException("Level01 no contiene BallPuzzleLevelController.");
        }

        SerializedObject serializedController = new SerializedObject(controller);
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
            summary.text = "2 AVAILABLE  \u00B7  " + lockedCards.arraySize + " LOCKED";
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
