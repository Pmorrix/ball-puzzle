using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds one modular 45-degree curve with the same section and materials as
/// the classic reference straight. The root origin is the start connection.
/// </summary>
public static class Classic45CurveTrackBuilder
{
    private const string RootFolder = "Assets/Art/ClassicReferenceTrack/Curve45";
    private const string MeshFolder = RootFolder + "/Meshes";
    private const string Curve90RootFolder = "Assets/Art/ClassicReferenceTrack/Curve90";
    private const string Curve90MeshFolder = Curve90RootFolder + "/Meshes";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string PiecesScenePath = "Assets/Scenes/PiecesCircuit.unity";
    private const string WoodMaterialPath = "Assets/Art/ClassicReferenceTrack/Materials/ClassicReferenceWood.mat";
    private const string MetalMaterialPath = "Assets/Art/ClassicReferenceTrack/Materials/ClassicReferenceMetal.mat";
    private const string TrackRootName = "45 Degree Curve Classic Reference";
    private const string Curve90RootName = "90 Degree Curve Classic Reference";

    private const float CenterRadius = 5f;
    private const float Curve45Angle = 45f;
    private const int Curve45Segments = 32;
    private const float Curve90Angle = 90f;
    private const int Curve90Segments = 64;
    private const int ConnectorArcSegments = 8;
    private const float BodyWidth = 2.35f;
    private const float LaneWidth = 1.88f;
    private const float RailOffset = 0.98f;
    private const float RailMountInset = 0.55f;
    private const float SocketEndHeight = 1.045f;
    private const float BendStartHeight = 1.14f;
    private const float RailBendRadius = 0.16f;
    private const float RailHeight = BendStartHeight + RailBendRadius;

    private static string activeMeshFolder = MeshFolder;
    private static float activeArcAngle = Curve45Angle;
    private static int activeArcSegments = Curve45Segments;

    [MenuItem("Tools/Ball Puzzle/Create Classic 45 Degree Curve In Scene")]
    public static void CreateClassic45DegreeCurve()
    {
        activeArcAngle = Curve45Angle;
        activeArcSegments = Curve45Segments;
        EnsureFolder("Assets/Art");
        EnsureFolder("Assets/Art/ClassicReferenceTrack");
        EnsureFolder(RootFolder);
        EnsureFolder(MeshFolder);

        Material woodMaterial = AssetDatabase.LoadAssetAtPath<Material>(WoodMaterialPath);
        Material metalMaterial = AssetDatabase.LoadAssetAtPath<Material>(MetalMaterialPath);
        if (woodMaterial == null || metalMaterial == null)
        {
            Debug.LogError("The classic reference materials are missing. Create the classic straight track first.");
            return;
        }

        if (!Application.isBatchMode)
        {
            EditorSceneManager.SaveOpenScenes();
        }
        EditorSceneManager.OpenScene(ScenePath);

        int nextNumber = GetNextTrackNumber();
        string rootName = nextNumber == 1 ? TrackRootName : TrackRootName + " " + nextNumber;
        activeMeshFolder = AssetDatabase.GenerateUniqueAssetPath(MeshFolder + "/" + rootName.Replace(" ", "_"));
        EnsureFolder(activeMeshFolder);

        GameObject root = BuildHierarchy(rootName, woodMaterial, metalMaterial);
        root.transform.position = new Vector3(2.8f + (nextNumber - 1) * 4.5f, 0.75f, -2.5f);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeGameObject = root;
        Debug.Log("Created modular classic 45-degree curve: " + rootName);
    }

    [MenuItem("Tools/Ball Puzzle/Create Classic 90 Degree Curve In Pieces Circuit")]
    public static void CreateClassic90DegreeCurveInPiecesCircuit()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path != PiecesScenePath)
        {
            Debug.LogError("Open PiecesCircuit before creating the classic 90-degree curve.");
            return;
        }

        GameObject raceRoad = GameObject.Find("RaceRoad");
        if (raceRoad == null)
        {
            Debug.LogError("PiecesCircuit has no RaceRoad root.");
            return;
        }

        Transform existingCurve = raceRoad.transform.Find(Curve90RootName);
        if (existingCurve != null)
        {
            Selection.activeGameObject = existingCurve.gameObject;
            Debug.Log("The classic 90-degree curve already exists in PiecesCircuit.");
            return;
        }

        Material woodMaterial = AssetDatabase.LoadAssetAtPath<Material>(WoodMaterialPath);
        Material metalMaterial = AssetDatabase.LoadAssetAtPath<Material>(MetalMaterialPath);
        if (woodMaterial == null || metalMaterial == null)
        {
            Debug.LogError("The classic reference materials are missing.");
            return;
        }

        EnsureFolder("Assets/Art");
        EnsureFolder("Assets/Art/ClassicReferenceTrack");
        EnsureFolder(Curve90RootFolder);
        EnsureFolder(Curve90MeshFolder);

        activeArcAngle = Curve90Angle;
        activeArcSegments = Curve90Segments;
        activeMeshFolder = Curve90MeshFolder;

        try
        {
            GameObject root = BuildHierarchy(Curve90RootName, woodMaterial, metalMaterial);
            root.transform.SetParent(raceRoad.transform, false);
            root.transform.localPosition = new Vector3(-8.8f, 0f, -2.5f);
            root.transform.localRotation = Quaternion.identity;

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = root;
            Debug.Log("Created modular classic 90-degree curve in PiecesCircuit.");
        }
        finally
        {
            activeArcAngle = Curve45Angle;
            activeArcSegments = Curve45Segments;
            activeMeshFolder = MeshFolder;
        }
    }

    [MenuItem("Tools/Ball Puzzle/Refresh Selected Classic 45 Degree Curve Meshes")]
    public static void RefreshSelectedClassic45DegreeCurveMeshes()
    {
        activeArcAngle = Curve45Angle;
        activeArcSegments = Curve45Segments;
        GameObject root = GetSelectedRoot();
        if (root == null || !root.name.StartsWith(TrackRootName, System.StringComparison.Ordinal))
        {
            Debug.LogError("Select a classic 45-degree curve before refreshing its meshes.");
            return;
        }

        MeshFilter lane = root.transform.Find("Concave Curved Wooden Lane")?.GetComponent<MeshFilter>();
        string meshPath = lane != null ? AssetDatabase.GetAssetPath(lane.sharedMesh) : string.Empty;
        string meshFolder = string.IsNullOrEmpty(meshPath) ? null : System.IO.Path.GetDirectoryName(meshPath)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(meshFolder))
        {
            Debug.LogError("The selected classic 45-degree curve has no generated lane mesh.");
            return;
        }

        activeMeshFolder = meshFolder;
        GetLaneMesh();
        GetRimMesh(-1f);
        GetRimMesh(1f);
        GetRailMesh(-1f);
        GetRailMesh(1f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Refreshed selected classic 45-degree curve meshes.");
    }

    private static int GetNextTrackNumber()
    {
        int nextNumber = 1;
        string numberedPrefix = TrackRootName + " ";
        foreach (GameObject sceneRoot in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (sceneRoot.name == TrackRootName)
            {
                nextNumber = Mathf.Max(nextNumber, 2);
            }
            else if (sceneRoot.name.StartsWith(numberedPrefix, System.StringComparison.Ordinal) &&
                     int.TryParse(sceneRoot.name.Substring(numberedPrefix.Length), out int existingNumber))
            {
                nextNumber = Mathf.Max(nextNumber, existingNumber + 1);
            }
        }
        return nextNumber;
    }

    private static GameObject BuildHierarchy(string rootName, Material woodMaterial, Material metalMaterial)
    {
        GameObject root = new GameObject(rootName);

        CreateMeshPart(root.transform, "Curved Wooden Body", GetBodyMesh(), woodMaterial, Vector3.zero, true);
        CreateMeshPart(root.transform, "Concave Curved Wooden Lane", GetLaneMesh(), woodMaterial, new Vector3(0f, 0.75f, 0f), true);
        CreateMeshPart(root.transform, "Inner Wooden Rim", GetRimMesh(-1f), woodMaterial, new Vector3(0f, 0.64f, 0f), true);
        CreateMeshPart(root.transform, "Outer Wooden Rim", GetRimMesh(1f), woodMaterial, new Vector3(0f, 0.64f, 0f), true);

        CreateMeshPart(root.transform, "Inner Curved Metal Guard", GetRailMesh(-1f), metalMaterial, Vector3.zero, true);
        CreateMeshPart(root.transform, "Outer Curved Metal Guard", GetRailMesh(1f), metalMaterial, Vector3.zero, true);

        float startAngle = GetMountInsetAngle();
        float endAngle = activeArcAngle * Mathf.Deg2Rad - startAngle;
        CreateMount(root.transform, "Inner Start Rail Mount", -1f, startAngle, metalMaterial);
        CreateMount(root.transform, "Inner End Rail Mount", -1f, endAngle, metalMaterial);
        CreateMount(root.transform, "Outer Start Rail Mount", 1f, startAngle, metalMaterial);
        CreateMount(root.transform, "Outer End Rail Mount", 1f, endAngle, metalMaterial);

        CreateConnection(root.transform, "Start Connection", PointAt(CenterRadius, 0f, 0f), Quaternion.identity);
        float endRadians = activeArcAngle * Mathf.Deg2Rad;
        CreateConnection(root.transform, "End Connection", PointAt(CenterRadius, endRadians, 0f), Quaternion.Euler(0f, -activeArcAngle, 0f));

        return root;
    }

    private static void CreateMeshPart(Transform parent, string objectName, Mesh mesh, Material material, Vector3 localPosition, bool addCollider)
    {
        GameObject part = new GameObject(objectName);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;

        MeshFilter filter = part.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        if (addCollider)
        {
            MeshCollider collider = part.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }
    }

    private static void CreateMount(Transform parent, string objectName, float side, float angle, Material material)
    {
        Vector3 position = PointAt(CenterRadius + side * RailOffset, angle, 0.90f);
        CreateMeshPart(parent, objectName, GetRailMountMesh(objectName), material, position, true);
    }

    private static void CreateConnection(Transform parent, string objectName, Vector3 position, Quaternion rotation)
    {
        GameObject connection = new GameObject(objectName);
        connection.transform.SetParent(parent, false);
        connection.transform.localPosition = position;
        connection.transform.localRotation = rotation;
    }

    private static Mesh GetBodyMesh()
    {
        string path = activeMeshFolder + "/CurvedWoodenBody.mesh";
        const float halfBottom = BodyWidth * 0.5f;
        const float halfTop = 1.10f;
        const int sectionSize = 6;

        List<Vector3> vertices = new List<Vector3>((activeArcSegments + 1) * sectionSize + 2);
        List<Vector2> uvs = new List<Vector2>(vertices.Capacity);
        for (int segment = 0; segment <= activeArcSegments; segment++)
        {
            float t = segment / (float)activeArcSegments;
            float angle = t * activeArcAngle * Mathf.Deg2Rad;
            AddSectionVertex(vertices, uvs, CenterRadius - halfBottom, angle, 0f, 0f, t);
            AddSectionVertex(vertices, uvs, CenterRadius + halfBottom, angle, 0f, 1f, t);
            AddSectionVertex(vertices, uvs, CenterRadius - halfBottom, angle, 0.10f, 0f, t);
            AddSectionVertex(vertices, uvs, CenterRadius + halfBottom, angle, 0.10f, 1f, t);
            AddSectionVertex(vertices, uvs, CenterRadius - halfTop, angle, 0.38f, 0.03f, t);
            AddSectionVertex(vertices, uvs, CenterRadius + halfTop, angle, 0.38f, 0.97f, t);
        }

        List<int> triangles = new List<int>();
        for (int segment = 0; segment < activeArcSegments; segment++)
        {
            int a = segment * sectionSize;
            int b = (segment + 1) * sectionSize;
            AddQuad(triangles, a, a + 1, b + 1, b, true);
            AddQuad(triangles, a, b, b + 2, a + 2, true);
            AddQuad(triangles, a + 1, a + 3, b + 3, b + 1, true);
            AddQuad(triangles, a + 2, b + 2, b + 4, a + 4, true);
            AddQuad(triangles, a + 3, a + 5, b + 5, b + 3, true);
            AddQuad(triangles, a + 4, b + 4, b + 5, a + 5, true);
        }

        int startCenter = vertices.Count;
        vertices.Add(PointAt(CenterRadius, 0f, 0.19f));
        uvs.Add(new Vector2(0.5f, 0f));
        int endCenter = vertices.Count;
        vertices.Add(PointAt(CenterRadius, activeArcAngle * Mathf.Deg2Rad, 0.19f));
        uvs.Add(new Vector2(0.5f, 1f));
        int[] boundary = { 0, 1, 3, 5, 4, 2 };
        int endStart = activeArcSegments * sectionSize;
        for (int i = 0; i < boundary.Length; i++)
        {
            int next = (i + 1) % boundary.Length;
            triangles.Add(startCenter);
            triangles.Add(boundary[next]);
            triangles.Add(boundary[i]);
            triangles.Add(endCenter);
            triangles.Add(endStart + boundary[i]);
            triangles.Add(endStart + boundary[next]);
        }

        Mesh mesh = BuildMesh("CurvedWoodenBody", vertices, uvs, triangles);
        return SaveOrUpdateMesh(path, mesh);
    }

    private static void AddSectionVertex(List<Vector3> vertices, List<Vector2> uvs, float radius, float angle, float y, float u, float v)
    {
        vertices.Add(PointAt(radius, angle, y));
        uvs.Add(new Vector2(u, v));
    }

    private static Mesh GetLaneMesh()
    {
        string path = activeMeshFolder + "/ConcaveCurvedWoodenLane.mesh";
        const int crossSegments = 28;
        float startAngle = 0f;
        float endAngle = activeArcAngle * Mathf.Deg2Rad;

        List<Vector3> vertices = new List<Vector3>((activeArcSegments + 3) * (crossSegments + 1));
        List<Vector2> uvs = new List<Vector2>(vertices.Capacity);
        for (int segment = 0; segment <= activeArcSegments; segment++)
        {
            float lengthT = segment / (float)activeArcSegments;
            float angle = Mathf.Lerp(startAngle, endAngle, lengthT);
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
        int rowSize = crossSegments + 1;
        for (int segment = 0; segment < activeArcSegments; segment++)
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
            vertices.Add(PointAt(CenterRadius + offset, startAngle, -0.38f));
            uvs.Add(new Vector2(t, 0f));
        }
        for (int cross = 0; cross <= crossSegments; cross++)
        {
            float t = cross / (float)crossSegments;
            float offset = Mathf.Lerp(-LaneWidth * 0.5f, LaneWidth * 0.5f, t);
            vertices.Add(PointAt(CenterRadius + offset, endAngle, -0.38f));
            uvs.Add(new Vector2(t, 1f));
        }

        int endTop = activeArcSegments * rowSize;
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

        Mesh mesh = BuildMesh("ConcaveCurvedWoodenLane", vertices, uvs, triangles);
        return SaveOrUpdateMesh(path, mesh);
    }

    private static Mesh GetRimMesh(float side)
    {
        string name = side < 0f ? "InnerWoodenRim" : "OuterWoodenRim";
        string path = activeMeshFolder + "/" + name + ".mesh";
        float center = CenterRadius + side * 1.02f;
        float startAngle = 0f;
        float endAngle = activeArcAngle * Mathf.Deg2Rad;
        const float bottomHalfWidth = 0.15f;
        const float topHalfWidth = 0.135f;
        const int sectionSize = 4;

        List<Vector3> vertices = new List<Vector3>((activeArcSegments + 1) * sectionSize + 2);
        List<Vector2> uvs = new List<Vector2>(vertices.Capacity);
        for (int segment = 0; segment <= activeArcSegments; segment++)
        {
            float t = segment / (float)activeArcSegments;
            float angle = Mathf.Lerp(startAngle, endAngle, t);
            AddSectionVertex(vertices, uvs, center - bottomHalfWidth, angle, -0.26f, 0f, t);
            AddSectionVertex(vertices, uvs, center + bottomHalfWidth, angle, -0.26f, 1f, t);
            AddSectionVertex(vertices, uvs, center - topHalfWidth, angle, 0.26f, 0.04f, t);
            AddSectionVertex(vertices, uvs, center + topHalfWidth, angle, 0.26f, 0.96f, t);
        }

        List<int> triangles = new List<int>();
        for (int segment = 0; segment < activeArcSegments; segment++)
        {
            int a = segment * sectionSize;
            int b = (segment + 1) * sectionSize;
            AddQuad(triangles, a, a + 1, b + 1, b, true);
            AddQuad(triangles, a, b, b + 2, a + 2, true);
            AddQuad(triangles, a + 1, a + 3, b + 3, b + 1, true);
            AddQuad(triangles, a + 2, b + 2, b + 3, a + 3, true);
        }

        int startCenter = vertices.Count;
        vertices.Add(PointAt(center, startAngle, 0f));
        uvs.Add(new Vector2(0.5f, 0f));
        int endCenter = vertices.Count;
        vertices.Add(PointAt(center, endAngle, 0f));
        uvs.Add(new Vector2(0.5f, 1f));
        int[] boundary = { 0, 1, 3, 2 };
        int endStart = activeArcSegments * sectionSize;
        for (int i = 0; i < boundary.Length; i++)
        {
            int next = (i + 1) % boundary.Length;
            triangles.Add(startCenter);
            triangles.Add(boundary[next]);
            triangles.Add(boundary[i]);
            triangles.Add(endCenter);
            triangles.Add(endStart + boundary[i]);
            triangles.Add(endStart + boundary[next]);
        }

        Mesh mesh = BuildMesh(name, vertices, uvs, triangles);
        return SaveOrUpdateMesh(path, mesh);
    }

    private static Mesh GetRailMesh(float side)
    {
        string name = side < 0f ? "InnerCurvedMetalGuard" : "OuterCurvedMetalGuard";
        string path = activeMeshFolder + "/" + name + ".mesh";
        List<Vector3> railPath = CreateRailPath(side);
        Vector3 firstNormal = RadialAt(0f);
        Mesh mesh = CreateTubeMesh(railPath, 0.075f, 14, firstNormal);
        mesh.name = name;
        return SaveOrUpdateMesh(path, mesh);
    }

    private static List<Vector3> CreateRailPath(float side)
    {
        List<Vector3> points = new List<Vector3>();
        const int bendSegments = 12;
        float radius = CenterRadius + side * RailOffset;
        float supportStart = GetMountInsetAngle();
        float supportEnd = activeArcAngle * Mathf.Deg2Rad - supportStart;
        float bendAngle = RailBendRadius / radius;

        for (int segment = 0; segment <= ConnectorArcSegments; segment++)
        {
            float t = segment / (float)ConnectorArcSegments;
            points.Add(PointAt(radius, Mathf.Lerp(0f, supportStart, t), SocketEndHeight));
        }
        points.Add(PointAt(radius, supportStart, BendStartHeight));
        for (int i = 1; i <= bendSegments; i++)
        {
            float bendT = i / (float)bendSegments;
            float bend = bendT * Mathf.PI * 0.5f;
            float traveled = RailBendRadius * (1f - Mathf.Cos(bend));
            points.Add(PointAt(radius, supportStart + traveled / radius,
                BendStartHeight + RailBendRadius * Mathf.Sin(bend)));
        }

        float mainStart = supportStart + bendAngle;
        float mainEnd = supportEnd - bendAngle;
        for (int segment = 1; segment < activeArcSegments; segment++)
        {
            float t = segment / (float)activeArcSegments;
            points.Add(PointAt(radius, Mathf.Lerp(mainStart, mainEnd, t), RailHeight));
        }
        points.Add(PointAt(radius, mainEnd, RailHeight));

        for (int i = 1; i <= bendSegments; i++)
        {
            float bendT = i / (float)bendSegments;
            float bend = bendT * Mathf.PI * 0.5f;
            float traveled = RailBendRadius * Mathf.Sin(bend);
            points.Add(PointAt(radius, mainEnd + traveled / radius,
                BendStartHeight + RailBendRadius * Mathf.Cos(bend)));
        }
        points.Add(PointAt(radius, supportEnd, SocketEndHeight));
        for (int segment = 1; segment <= ConnectorArcSegments; segment++)
        {
            float t = segment / (float)ConnectorArcSegments;
            points.Add(PointAt(radius, Mathf.Lerp(supportEnd, activeArcAngle * Mathf.Deg2Rad, t), SocketEndHeight));
        }
        return points;
    }

    private static Mesh CreateTubeMesh(List<Vector3> path, float radius, int sides, Vector3 firstNormal)
    {
        List<Vector3> vertices = new List<Vector3>(path.Count * sides);
        List<Vector2> uvs = new List<Vector2>(vertices.Capacity);
        List<int> triangles = new List<int>((path.Count - 1) * sides * 6);

        Vector3 previousTangent = Vector3.zero;
        Vector3 normal = firstNormal.normalized;
        for (int i = 0; i < path.Count; i++)
        {
            Vector3 tangent = i == 0 ? path[1] - path[0] :
                i == path.Count - 1 ? path[i] - path[i - 1] : path[i + 1] - path[i - 1];
            tangent.Normalize();
            if (i > 0)
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
                vertices.Add(path[i] + (normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle)) * radius);
                uvs.Add(new Vector2(side / (float)sides, i / (float)(path.Count - 1)));
            }
            previousTangent = tangent;
        }

        for (int ring = 0; ring < path.Count - 1; ring++)
        {
            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                int a = ring * sides + side;
                int b = ring * sides + next;
                int c = (ring + 1) * sides + next;
                int d = (ring + 1) * sides + side;
                triangles.Add(a);
                triangles.Add(d);
                triangles.Add(c);
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);
            }
        }

        // Rails intentionally have open ends on the shared connector plane.
        // The adjacent modular piece supplies the matching ring, avoiding a
        // visible circular cap at the joint.

        return BuildMesh("CurvedTube", vertices, uvs, triangles);
    }

    private static Mesh GetRailMountMesh(string objectName)
    {
        string path = activeMeshFolder + "/" + objectName.Replace(" ", string.Empty) + ".mesh";
        const int radialSegments = 20;
        Vector2[] outerProfile =
        {
            new Vector2(0.17f, 0f),
            new Vector2(0.17f, 0.035f),
            new Vector2(0.15f, 0.065f),
            new Vector2(0.115f, 0.12f),
            new Vector2(0.115f, 0.16f),
            new Vector2(0.14f, 0.175f),
            new Vector2(0.14f, 0.215f)
        };
        const float socketRadius = 0.083f;
        const float socketBottom = 0.125f;
        const float socketTop = 0.215f;

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        foreach (Vector2 profile in outerProfile)
        {
            AddCircleRing(vertices, uvs, profile.x, profile.y, radialSegments);
        }
        int innerTop = vertices.Count;
        AddCircleRing(vertices, uvs, socketRadius, socketTop, radialSegments);
        int innerBottom = vertices.Count;
        AddCircleRing(vertices, uvs, socketRadius, socketBottom, radialSegments);

        List<int> triangles = new List<int>();
        for (int ring = 0; ring < outerProfile.Length - 1; ring++)
        {
            AddRingSides(triangles, ring * radialSegments, (ring + 1) * radialSegments, radialSegments);
        }

        int bottomCenter = vertices.Count;
        vertices.Add(Vector3.zero);
        uvs.Add(new Vector2(0.5f, 0.5f));
        int socketCenter = vertices.Count;
        vertices.Add(new Vector3(0f, socketBottom, 0f));
        uvs.Add(new Vector2(0.5f, 0.5f));
        int outerTop = (outerProfile.Length - 1) * radialSegments;
        for (int segment = 0; segment < radialSegments; segment++)
        {
            int next = (segment + 1) % radialSegments;
            triangles.Add(bottomCenter);
            triangles.Add(segment);
            triangles.Add(next);

            triangles.Add(outerTop + segment);
            triangles.Add(innerTop + segment);
            triangles.Add(outerTop + next);
            triangles.Add(outerTop + next);
            triangles.Add(innerTop + segment);
            triangles.Add(innerTop + next);

            triangles.Add(innerTop + segment);
            triangles.Add(innerBottom + segment);
            triangles.Add(innerBottom + next);
            triangles.Add(innerTop + segment);
            triangles.Add(innerBottom + next);
            triangles.Add(innerTop + next);

            triangles.Add(socketCenter);
            triangles.Add(innerBottom + next);
            triangles.Add(innerBottom + segment);
        }

        Mesh mesh = BuildMesh(objectName.Replace(" ", string.Empty), vertices, uvs, triangles);
        return SaveOrUpdateMesh(path, mesh);
    }

    private static void AddCircleRing(List<Vector3> vertices, List<Vector2> uvs, float radius, float y, int segments)
    {
        for (int segment = 0; segment < segments; segment++)
        {
            float angle = segment / (float)segments * Mathf.PI * 2f;
            vertices.Add(new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius));
            uvs.Add(new Vector2(segment / (float)segments, y));
        }
    }

    private static void AddRingSides(List<int> triangles, int lower, int upper, int ringSize)
    {
        for (int i = 0; i < ringSize; i++)
        {
            int next = (i + 1) % ringSize;
            triangles.Add(lower + i);
            triangles.Add(upper + i);
            triangles.Add(upper + next);
            triangles.Add(lower + i);
            triangles.Add(upper + next);
            triangles.Add(lower + next);
        }
    }

    private static void AddQuad(List<int> triangles, int a, int b, int c, int d, bool forward)
    {
        if (forward)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(d);
        }
        else
        {
            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(b);
            triangles.Add(a);
            triangles.Add(d);
            triangles.Add(c);
        }
    }

    private static Mesh BuildMesh(string name, List<Vector3> vertices, List<Vector2> uvs, List<int> triangles)
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
        Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existingMesh == null)
        {
            AssetDatabase.CreateAsset(generatedMesh, path);
            return generatedMesh;
        }

        existingMesh.Clear();
        existingMesh.name = generatedMesh.name;
        existingMesh.vertices = generatedMesh.vertices;
        existingMesh.uv = generatedMesh.uv;
        existingMesh.triangles = generatedMesh.triangles;
        existingMesh.RecalculateNormals();
        existingMesh.RecalculateBounds();
        EditorUtility.SetDirty(existingMesh);
        Object.DestroyImmediate(generatedMesh);
        return existingMesh;
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

    private static GameObject GetSelectedRoot()
    {
        Transform selected = Selection.activeTransform;
        while (selected != null && selected.parent != null)
        {
            selected = selected.parent;
        }

        return selected != null ? selected.gameObject : null;
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
