using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the classic wooden Y junction used by the loose-pieces gallery.
/// The piece has one input and two 45-degree outputs.
/// </summary>
public static class ClassicYJunctionTrackBuilder
{
    private const string RootFolder = "Assets/Art/ClassicReferenceTrack";
    private const string JunctionFolder = RootFolder + "/YJunction";
    private const string MeshFolder = JunctionFolder + "/Meshes";
    private const string SymmetricJunctionFolder = RootFolder + "/YJunction120";
    private const string SymmetricMeshFolder = SymmetricJunctionFolder + "/Meshes";
    private const string RampFolder = RootFolder + "/Ramp";
    private const string ScenePath = "Assets/Scenes/PiecesCircuit.unity";
    private const string WoodMaterialPath = RootFolder + "/Materials/ClassicReferenceWood.mat";
    private const string MetalMaterialPath = RootFolder + "/Materials/ClassicReferenceMetal.mat";
    private const string SharedMountMeshPath = RootFolder + "/Meshes/LeftFrontRailMount.mesh";
    private const string RootName = "Y Junction Classic Reference";
    private const string SymmetricRootName = "120 Degree Y Junction Classic Reference";
    private const string RampRootName = "Straight Ramp Classic Reference";

    private const float BodyHalfWidth = 1.175f;
    private const float LaneHalfWidth = 0.94f;
    private const float RailOffset = 0.98f;
    private const float BodyTop = 0.38f;
    private const float LaneEdgeHeight = 0.75f;
    private const float LaneDepth = 0.28f;
    private const float RimBottom = 0.38f;
    private const float RimTop = 0.90f;
    private const float RimHalfWidth = 0.15f;
    private const float RailHeight = 1.30f;
    private const float RailSocketHeight = 1.045f;
    private const float RailBendStartHeight = 1.14f;
    private const float RailBendRadius = 0.16f;
    private const float RailMountInset = 0.55f;
    private const float BranchLength = 5f;
    private const float InputZ = -4f;
    private const float BranchLaneStartDistance = 1.05f;
    private const float SymmetricArmLength = 4f;
    private const float SymmetricLaneStartDistance = 1.35f;
    private const float SymmetricRailApproachDistance = 1.45f;

    private static readonly float Diagonal = 1f / Mathf.Sqrt(2f);
    private static readonly Vector2 LeftDirection = new Vector2(-Diagonal, Diagonal);
    private static readonly Vector2 RightDirection = new Vector2(Diagonal, Diagonal);
    private static readonly Vector2 LeftEnd = LeftDirection * BranchLength;
    private static readonly Vector2 RightEnd = RightDirection * BranchLength;
    private static readonly float SymmetricDiagonal = Mathf.Sqrt(3f) * 0.5f;
    private static readonly Vector2[] SymmetricDirections =
    {
        Vector2.down,
        new Vector2(SymmetricDiagonal, 0.5f),
        new Vector2(-SymmetricDiagonal, 0.5f)
    };

    [MenuItem("Tools/Ball Puzzle/Create Classic Y Junction In Pieces Circuit")]
    public static void CreateClassicYJunctionInPiecesCircuit()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (Application.isBatchMode)
        {
            activeScene = EditorSceneManager.OpenScene(ScenePath);
        }
        else if (activeScene.path != ScenePath)
        {
            Debug.LogError("Open PiecesCircuit before creating the classic Y junction.");
            return;
        }

        GameObject raceRoad = GameObject.Find("RaceRoad");
        if (raceRoad == null)
        {
            Debug.LogError("PiecesCircuit has no RaceRoad root.");
            return;
        }

        Transform existingJunction = raceRoad.transform.Find(RootName);
        if (existingJunction != null)
        {
            Object.DestroyImmediate(existingJunction.gameObject);
        }

        Material woodMaterial = AssetDatabase.LoadAssetAtPath<Material>(WoodMaterialPath);
        Material metalMaterial = AssetDatabase.LoadAssetAtPath<Material>(MetalMaterialPath);
        Mesh mountMesh = AssetDatabase.LoadAssetAtPath<Mesh>(SharedMountMeshPath);
        if (woodMaterial == null || metalMaterial == null || mountMesh == null)
        {
            Debug.LogError("The classic reference materials or shared rail mount are missing.");
            return;
        }

        EnsureFolder("Assets/Art");
        EnsureFolder(RootFolder);
        EnsureFolder(JunctionFolder);
        EnsureFolder(MeshFolder);

        GameObject root = BuildHierarchy(woodMaterial, metalMaterial, mountMesh);
        root.transform.SetParent(raceRoad.transform, false);
        root.transform.localPosition = new Vector3(1.5f, 0f, 0f);
        root.transform.localRotation = Quaternion.identity;

        RemoveRamp(raceRoad.transform);
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeGameObject = root;
        Debug.Log("Created modular classic Y junction in PiecesCircuit and removed the discarded ramp.");
    }

    [MenuItem("Tools/Ball Puzzle/Create Symmetric 120 Degree Y Junction In Pieces Circuit")]
    public static void CreateSymmetricYJunctionInPiecesCircuit()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (Application.isBatchMode)
        {
            activeScene = EditorSceneManager.OpenScene(ScenePath);
        }
        else if (activeScene.path != ScenePath)
        {
            Debug.LogError("Open PiecesCircuit before creating the symmetric 120 degree Y junction.");
            return;
        }

        GameObject raceRoad = GameObject.Find("RaceRoad");
        if (raceRoad == null)
        {
            Debug.LogError("PiecesCircuit has no RaceRoad root.");
            return;
        }

        Material woodMaterial = AssetDatabase.LoadAssetAtPath<Material>(WoodMaterialPath);
        Material metalMaterial = AssetDatabase.LoadAssetAtPath<Material>(MetalMaterialPath);
        Mesh mountMesh = AssetDatabase.LoadAssetAtPath<Mesh>(SharedMountMeshPath);
        if (woodMaterial == null || metalMaterial == null || mountMesh == null)
        {
            Debug.LogError("The classic reference materials or shared rail mount are missing.");
            return;
        }

        EnsureFolder("Assets/Art");
        EnsureFolder(RootFolder);
        EnsureFolder(SymmetricJunctionFolder);
        EnsureFolder(SymmetricMeshFolder);

        Transform existingSymmetricJunction = raceRoad.transform.Find(SymmetricRootName);
        if (existingSymmetricJunction != null)
        {
            Object.DestroyImmediate(existingSymmetricJunction.gameObject);
        }

        GameObject root = BuildSymmetricHierarchy(woodMaterial, metalMaterial, mountMesh);
        root.transform.SetParent(raceRoad.transform, false);
        root.transform.localPosition = new Vector3(1.5f, 0f, 5f);
        root.transform.localRotation = Quaternion.identity;

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeGameObject = root;
        Debug.Log("Created a separate symmetric 120 degree Y junction in PiecesCircuit.");
    }

    private static GameObject BuildHierarchy(Material woodMaterial, Material metalMaterial, Mesh mountMesh)
    {
        GameObject root = new GameObject(RootName);

        CreateMeshPart(root.transform, "Y Wooden Body", CreateBodyMesh(), woodMaterial, true);
        CreateMeshPart(root.transform, "Y Concave Lane", CreateLaneMesh(), woodMaterial, true);

        List<Vector2> leftOuterRim = GetLeftOuterPlanPath();
        List<Vector2> rightOuterRim = MirrorPath(leftOuterRim);
        List<Vector2> leftInnerRim = GetLeftInnerPlanPath();
        List<Vector2> rightInnerRim = MirrorPath(leftInnerRim);

        CreateMeshPart(root.transform, "Left Outer Wooden Rim", CreateRimMesh("LeftOuterWoodenRim", leftOuterRim), woodMaterial, true);
        CreateMeshPart(root.transform, "Right Outer Wooden Rim", CreateRimMesh("RightOuterWoodenRim", rightOuterRim), woodMaterial, true);
        CreateMeshPart(root.transform, "Left Inner Wooden Rim", CreateRimMesh("LeftInnerWoodenRim", leftInnerRim), woodMaterial, true);
        CreateMeshPart(root.transform, "Right Inner Wooden Rim", CreateRimMesh("RightInnerWoodenRim", rightInnerRim), woodMaterial, true);

        List<Vector3> leftOuterRail = BuildRailPath(leftOuterRim, true);
        List<Vector3> rightOuterRail = BuildRailPath(rightOuterRim, true);
        List<Vector3> leftInnerRail = BuildRailPath(leftInnerRim, false);
        List<Vector3> rightInnerRail = BuildRailPath(rightInnerRim, false);

        CreateMeshPart(root.transform, "Left Outer Metal Guard", CreateRailMesh("LeftOuterMetalGuard", leftOuterRail), metalMaterial, true);
        CreateMeshPart(root.transform, "Right Outer Metal Guard", CreateRailMesh("RightOuterMetalGuard", rightOuterRail), metalMaterial, true);
        CreateMeshPart(root.transform, "Left Inner Metal Guard", CreateRailMesh("LeftInnerMetalGuard", leftInnerRail), metalMaterial, true);
        CreateMeshPart(root.transform, "Right Inner Metal Guard", CreateRailMesh("RightInnerMetalGuard", rightInnerRail), metalMaterial, true);

        CreateMounts(root.transform, mountMesh, metalMaterial,
            leftOuterRim, rightOuterRim, leftInnerRim, rightInnerRim);

        CreateConnection(root.transform, "Input Connection", new Vector3(0f, 0f, InputZ), Quaternion.identity);
        CreateConnection(root.transform, "Left Output Connection", ToVector3(LeftEnd, 0f), Quaternion.Euler(0f, -45f, 0f));
        CreateConnection(root.transform, "Right Output Connection", ToVector3(RightEnd, 0f), Quaternion.Euler(0f, 45f, 0f));

        return root;
    }

    private static GameObject BuildSymmetricHierarchy(
        Material woodMaterial,
        Material metalMaterial,
        Mesh mountMesh)
    {
        GameObject root = new GameObject(SymmetricRootName);

        CreateMeshPart(root.transform, "120 Y Wooden Body",
            CreateSymmetricBodyMesh(), woodMaterial, true);
        CreateMeshPart(root.transform, "120 Y Concave Lane",
            CreateSymmetricLaneMesh(), woodMaterial, true);

        for (int sector = 0; sector < SymmetricDirections.Length; sector++)
        {
            Vector2 directionA = SymmetricDirections[sector];
            Vector2 directionB = SymmetricDirections[(sector + 1) % SymmetricDirections.Length];
            List<Vector2> plan = CreateSymmetricSectorPlan(directionA, directionB);
            string sectorName = "Sector " + (sector + 1);

            CreateMeshPart(root.transform, sectorName + " Wooden Rim",
                CreateRimMesh("Symmetric" + sectorName.Replace(" ", string.Empty) + "WoodenRim",
                    plan, SymmetricMeshFolder), woodMaterial, true);
            CreateMeshPart(root.transform, sectorName + " Metal Guard",
                CreateRailMesh("Symmetric" + sectorName.Replace(" ", string.Empty) + "MetalGuard",
                    BuildRailPath(plan, true), SymmetricMeshFolder), metalMaterial, true);

            CreateMount(root.transform, sectorName + " Start Rail Mount", mountMesh, metalMaterial,
                GetPointAtDistance(plan, RailMountInset));
            CreateMount(root.transform, sectorName + " End Rail Mount", mountMesh, metalMaterial,
                GetPointAtDistance(plan, GetPathLength(plan) - RailMountInset));
        }

        for (int arm = 0; arm < SymmetricDirections.Length; arm++)
        {
            Vector2 direction = SymmetricDirections[arm];
            float rotation = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            CreateConnection(root.transform, "Arm " + (arm + 1) + " Connection",
                ToVector3(direction * SymmetricArmLength, 0f), Quaternion.Euler(0f, rotation, 0f));
        }

        return root;
    }

    private static void RemoveRamp(Transform raceRoad)
    {
        Transform ramp = raceRoad.Find(RampRootName);
        if (ramp != null)
        {
            Object.DestroyImmediate(ramp.gameObject);
        }

        if (AssetDatabase.IsValidFolder(RampFolder))
        {
            AssetDatabase.DeleteAsset(RampFolder);
        }
    }

    private static Mesh CreateBodyMesh()
    {
        float branchOffset = BodyHalfWidth * Diagonal;
        float outerJoinZ = BodyHalfWidth - branchOffset * 2f;
        float notchZ = branchOffset * 2f;

        List<Vector2> outline = new List<Vector2>
        {
            new Vector2(-BodyHalfWidth, InputZ),
            new Vector2(BodyHalfWidth, InputZ),
            new Vector2(BodyHalfWidth, outerJoinZ),
            RightEnd + new Vector2(branchOffset, -branchOffset),
            RightEnd + new Vector2(-branchOffset, branchOffset),
            new Vector2(0f, notchZ),
            LeftEnd + new Vector2(branchOffset, branchOffset),
            LeftEnd + new Vector2(-branchOffset, -branchOffset),
            new Vector2(-BodyHalfWidth, outerJoinZ)
        };

        Mesh mesh = CreateExtrudedPolygon(outline, 0f, BodyTop);
        mesh.name = "YWoodenBody";
        return SaveOrUpdateMesh(MeshFolder + "/YWoodenBody.mesh", mesh);
    }

    private static Mesh CreateSymmetricBodyMesh()
    {
        List<Vector2> outline = CreateSymmetricOutline(BodyHalfWidth, SymmetricArmLength);
        Mesh mesh = CreateExtrudedPolygon(outline, 0f, BodyTop);
        mesh.name = "SymmetricYWoodenBody";
        return SaveOrUpdateMesh(SymmetricMeshFolder + "/SymmetricYWoodenBody.mesh", mesh);
    }

    private static List<Vector2> CreateSymmetricOutline(float halfWidth, float endDistance)
    {
        List<Vector2> outline = new List<Vector2>();
        for (int arm = 0; arm < SymmetricDirections.Length; arm++)
        {
            Vector2 direction = SymmetricDirections[arm];
            Vector2 nextDirection = SymmetricDirections[(arm + 1) % SymmetricDirections.Length];
            Vector2 normal = GetLeftNormal(direction);
            Vector2 end = direction * endDistance;

            outline.Add(end - normal * halfWidth);
            outline.Add(end + normal * halfWidth);
            outline.Add(GetSymmetricSectorCorner(direction, nextDirection, halfWidth));
        }
        return outline;
    }

    private static Mesh CreateLaneMesh()
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        Vector2 inputEnd = new Vector2(0f, -0.40f);
        Vector2 leftStart = LeftDirection * BranchLaneStartDistance;
        Vector2 rightStart = RightDirection * BranchLaneStartDistance;

        AddLaneStrip(vertices, uvs, triangles,
            new Vector2(0f, InputZ), inputEnd, 28, false, true);
        AddLaneStrip(vertices, uvs, triangles,
            leftStart, LeftEnd, 32, true, false);
        AddLaneStrip(vertices, uvs, triangles,
            rightStart, RightEnd, 32, true, false);
        AddFlatLaneHub(vertices, uvs, triangles, inputEnd, leftStart, rightStart);

        Mesh mesh = new Mesh { name = "YConcaveLane" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return SaveOrUpdateMesh(MeshFolder + "/YConcaveLane.mesh", mesh);
    }

    private static Mesh CreateSymmetricLaneMesh()
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        foreach (Vector2 direction in SymmetricDirections)
        {
            AddLaneStrip(vertices, uvs, triangles,
                direction * SymmetricLaneStartDistance,
                direction * SymmetricArmLength,
                28,
                true,
                false);
        }

        List<Vector2> hubOutline = CreateSymmetricOutline(LaneHalfWidth, SymmetricLaneStartDistance);
        AddFlatLaneSurface(vertices, uvs, triangles, hubOutline);

        Mesh mesh = new Mesh { name = "SymmetricYConcaveLane" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return SaveOrUpdateMesh(SymmetricMeshFolder + "/SymmetricYConcaveLane.mesh", mesh);
    }

    private static void AddLaneStrip(
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles,
        Vector2 start,
        Vector2 end,
        int lengthSegments,
        bool flattenAtStart,
        bool flattenAtEnd)
    {
        const int crossSegments = 24;
        Vector2 direction = (end - start).normalized;
        Vector2 normal = new Vector2(direction.y, -direction.x);
        int firstVertex = vertices.Count;

        for (int length = 0; length <= lengthSegments; length++)
        {
            float lengthT = length / (float)lengthSegments;
            Vector2 center = Vector2.Lerp(start, end, lengthT);
            float profileFactor = 1f;
            if (flattenAtStart)
            {
                profileFactor *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(lengthT / 0.22f));
            }
            if (flattenAtEnd)
            {
                profileFactor *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - lengthT) / 0.22f));
            }

            for (int cross = 0; cross <= crossSegments; cross++)
            {
                float crossT = cross / (float)crossSegments;
                float offset = Mathf.Lerp(-LaneHalfWidth, LaneHalfWidth, crossT);
                float normalizedOffset = offset / LaneHalfWidth;
                Vector2 point = center + normal * offset;
                float height = LaneEdgeHeight - LaneDepth +
                    LaneDepth * normalizedOffset * normalizedOffset * profileFactor;
                vertices.Add(new Vector3(point.x, height, point.y));
                uvs.Add(new Vector2(crossT, lengthT));
            }
        }

        int rowSize = crossSegments + 1;
        for (int length = 0; length < lengthSegments; length++)
        {
            for (int cross = 0; cross < crossSegments; cross++)
            {
                int a = firstVertex + length * rowSize + cross;
                int b = a + rowSize;
                int c = b + 1;
                int d = a + 1;
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(d);
            }
        }
    }

    private static void AddFlatLaneHub(
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles,
        Vector2 inputEnd,
        Vector2 leftStart,
        Vector2 rightStart)
    {
        Vector2 leftNormal = new Vector2(LeftDirection.y, -LeftDirection.x);
        Vector2 rightNormal = new Vector2(RightDirection.y, -RightDirection.x);
        List<Vector2> outline = new List<Vector2>
        {
            inputEnd + Vector2.left * LaneHalfWidth,
            inputEnd + Vector2.right * LaneHalfWidth,
            rightStart + rightNormal * LaneHalfWidth,
            rightStart - rightNormal * LaneHalfWidth,
            new Vector2(0f, 1.25f),
            leftStart + leftNormal * LaneHalfWidth,
            leftStart - leftNormal * LaneHalfWidth
        };

        AddFlatLaneSurface(vertices, uvs, triangles, outline);
    }

    private static void AddFlatLaneSurface(
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles,
        List<Vector2> outline)
    {
        EnsureCounterClockwise(outline);
        List<int> surfaceTriangles = Triangulate(outline);
        int firstVertex = vertices.Count;
        float laneFloor = LaneEdgeHeight - LaneDepth;
        foreach (Vector2 point in outline)
        {
            vertices.Add(ToVector3(point, laneFloor));
            uvs.Add(point * 0.12f + Vector2.one * 0.5f);
        }

        for (int i = 0; i < surfaceTriangles.Count; i += 3)
        {
            triangles.Add(firstVertex + surfaceTriangles[i + 2]);
            triangles.Add(firstVertex + surfaceTriangles[i + 1]);
            triangles.Add(firstVertex + surfaceTriangles[i]);
        }
    }

    private static Mesh CreateExtrudedPolygon(List<Vector2> outline, float bottom, float top)
    {
        EnsureCounterClockwise(outline);
        List<int> surfaceTriangles = Triangulate(outline);
        int count = outline.Count;
        List<Vector3> vertices = new List<Vector3>(count * 2);
        List<Vector2> uvs = new List<Vector2>(count * 2);

        for (int i = 0; i < count; i++)
        {
            vertices.Add(ToVector3(outline[i], bottom));
            uvs.Add(outline[i] * 0.12f + Vector2.one * 0.5f);
        }
        for (int i = 0; i < count; i++)
        {
            vertices.Add(ToVector3(outline[i], top));
            uvs.Add(outline[i] * 0.12f + Vector2.one * 0.5f);
        }

        List<int> triangles = new List<int>();
        for (int i = 0; i < surfaceTriangles.Count; i += 3)
        {
            int a = surfaceTriangles[i];
            int b = surfaceTriangles[i + 1];
            int c = surfaceTriangles[i + 2];
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
            triangles.Add(count + c);
            triangles.Add(count + b);
            triangles.Add(count + a);
        }

        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            int bottomA = i;
            int bottomB = next;
            int topA = count + i;
            int topB = count + next;
            triangles.Add(bottomA);
            triangles.Add(topA);
            triangles.Add(topB);
            triangles.Add(bottomA);
            triangles.Add(topB);
            triangles.Add(bottomB);
        }

        Mesh mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh CreateRimMesh(
        string meshName,
        List<Vector2> path,
        string targetMeshFolder = MeshFolder)
    {
        List<Vector3> vertices = new List<Vector3>(path.Count * 4);
        List<Vector2> uvs = new List<Vector2>(path.Count * 4);

        for (int i = 0; i < path.Count; i++)
        {
            Vector2 tangent = GetPathTangent(path, i);
            Vector2 normal = new Vector2(tangent.y, -tangent.x);
            Vector2 left = path[i] - normal * RimHalfWidth;
            Vector2 right = path[i] + normal * RimHalfWidth;

            vertices.Add(ToVector3(left, RimBottom));
            vertices.Add(ToVector3(right, RimBottom));
            vertices.Add(ToVector3(left, RimTop));
            vertices.Add(ToVector3(right, RimTop));
            float v = i / (float)(path.Count - 1);
            uvs.Add(new Vector2(0f, v));
            uvs.Add(new Vector2(1f, v));
            uvs.Add(new Vector2(0f, v));
            uvs.Add(new Vector2(1f, v));
        }

        List<int> triangles = new List<int>();
        for (int i = 0; i < path.Count - 1; i++)
        {
            int a = i * 4;
            int b = (i + 1) * 4;
            AddQuad(triangles, a + 2, b + 2, b + 3, a + 3);
            AddQuad(triangles, a, a + 1, b + 1, b);
            AddQuad(triangles, a, b, b + 2, a + 2);
            AddQuad(triangles, a + 1, a + 3, b + 3, b + 1);
        }
        AddQuad(triangles, 0, 2, 3, 1);
        int end = (path.Count - 1) * 4;
        AddQuad(triangles, end, end + 1, end + 3, end + 2);

        Mesh mesh = new Mesh { name = meshName };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return SaveOrUpdateMesh(targetMeshFolder + "/" + meshName + ".mesh", mesh);
    }

    private static Mesh CreateRailMesh(
        string meshName,
        List<Vector3> path,
        string targetMeshFolder = MeshFolder)
    {
        Mesh mesh = CreateTubeMesh(path, 0.075f, 14);
        mesh.name = meshName;
        return SaveOrUpdateMesh(targetMeshFolder + "/" + meshName + ".mesh", mesh);
    }

    private static Mesh CreateTubeMesh(List<Vector3> path, float radius, int sides)
    {
        List<Vector3> vertices = new List<Vector3>(path.Count * sides);
        List<Vector2> uvs = new List<Vector2>(path.Count * sides);
        List<int> triangles = new List<int>((path.Count - 1) * sides * 6);

        for (int i = 0; i < path.Count; i++)
        {
            Vector3 tangent = i == 0 ? path[1] - path[0] :
                i == path.Count - 1 ? path[i] - path[i - 1] : path[i + 1] - path[i - 1];
            tangent.Normalize();
            Vector3 sideways = Vector3.Cross(Vector3.up, tangent).normalized;
            if (sideways.sqrMagnitude < 0.01f)
            {
                sideways = Vector3.right;
            }
            Vector3 upward = Vector3.Cross(tangent, sideways).normalized;

            for (int side = 0; side < sides; side++)
            {
                float angle = side / (float)sides * Mathf.PI * 2f;
                vertices.Add(path[i] + (sideways * Mathf.Cos(angle) + upward * Mathf.Sin(angle)) * radius);
                uvs.Add(new Vector2(side / (float)sides, i / (float)(path.Count - 1)));
            }
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

        Mesh mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static List<Vector2> GetLeftOuterPlanPath()
    {
        Vector2 start = new Vector2(-RailOffset, InputZ);
        Vector2 curveStart = new Vector2(-RailOffset, -0.60f);
        Vector2 outerEnd = LeftEnd + new Vector2(-RailOffset * Diagonal, -RailOffset * Diagonal);
        List<Vector2> path = new List<Vector2>();

        AddLineSegment(path, start, curveStart, 18);
        AddCubicBezier(path,
            curveStart,
            new Vector2(-RailOffset, 0.40f),
            outerEnd - LeftDirection * 1.40f,
            outerEnd,
            32);
        return path;
    }

    private static List<Vector2> GetLeftInnerPlanPath()
    {
        Vector2 innerOffset = new Vector2(RailOffset * Diagonal, RailOffset * Diagonal);
        Vector2 start = LeftDirection * 1.25f + innerOffset;
        Vector2 end = LeftEnd + innerOffset;
        List<Vector2> path = new List<Vector2>();
        AddLineSegment(path, start, end, 28);
        return path;
    }

    private static List<Vector2> CreateSymmetricSectorPlan(Vector2 directionA, Vector2 directionB)
    {
        Vector2 normalA = GetLeftNormal(directionA);
        Vector2 normalB = GetLeftNormal(directionB);
        Vector2 start = directionA * SymmetricArmLength + normalA * RailOffset;
        Vector2 end = directionB * SymmetricArmLength - normalB * RailOffset;
        Vector2 approachA = directionA * SymmetricRailApproachDistance + normalA * RailOffset;
        Vector2 approachB = directionB * SymmetricRailApproachDistance - normalB * RailOffset;
        Vector2 corner = GetSymmetricSectorCorner(directionA, directionB, RailOffset);
        List<Vector2> path = new List<Vector2>();

        AddLineSegment(path, start, approachA, 20);
        AddQuadraticBezier(path, approachA, corner, approachB, 24);
        AddLineSegment(path, approachB, end, 20);
        return path;
    }

    private static List<Vector3> BuildRailPath(List<Vector2> plan, bool startIsConnector)
    {
        const int curveSegments = 12;
        float totalLength = GetPathLength(plan);
        float startHighDistance = startIsConnector
            ? RailMountInset + RailBendRadius
            : RailBendRadius;
        float endHighDistance = totalLength - RailMountInset - RailBendRadius;
        List<Vector3> points = new List<Vector3>();

        points.Add(ToVector3(GetPointAtDistance(plan, 0f), RailSocketHeight));
        float startBendOrigin = startIsConnector ? RailMountInset : 0f;
        if (startIsConnector)
        {
            points.Add(ToVector3(GetPointAtDistance(plan, RailMountInset), RailSocketHeight));
        }

        for (int i = 0; i <= curveSegments; i++)
        {
            float angle = i / (float)curveSegments * Mathf.PI * 0.5f;
            float distance = startBendOrigin + RailBendRadius * (1f - Mathf.Cos(angle));
            float height = RailBendStartHeight + RailBendRadius * Mathf.Sin(angle);
            points.Add(ToVector3(GetPointAtDistance(plan, distance), height));
        }

        float traversed = 0f;
        for (int i = 1; i < plan.Count; i++)
        {
            traversed += Vector2.Distance(plan[i - 1], plan[i]);
            if (traversed > startHighDistance + 0.0001f && traversed < endHighDistance - 0.0001f)
            {
                points.Add(ToVector3(plan[i], RailHeight));
            }
        }

        points.Add(ToVector3(GetPointAtDistance(plan, endHighDistance), RailHeight));
        for (int i = 1; i <= curveSegments; i++)
        {
            float angle = i / (float)curveSegments * Mathf.PI * 0.5f;
            float distance = endHighDistance + RailBendRadius * Mathf.Sin(angle);
            float height = RailBendStartHeight + RailBendRadius * Mathf.Cos(angle);
            points.Add(ToVector3(GetPointAtDistance(plan, distance), height));
        }
        points.Add(ToVector3(GetPointAtDistance(plan, totalLength - RailMountInset), RailSocketHeight));
        points.Add(ToVector3(GetPointAtDistance(plan, totalLength), RailSocketHeight));
        return points;
    }

    private static void CreateMounts(
        Transform root,
        Mesh mountMesh,
        Material material,
        List<Vector2> leftOuter,
        List<Vector2> rightOuter,
        List<Vector2> leftInner,
        List<Vector2> rightInner)
    {
        CreateMount(root, "Input Left Rail Mount", mountMesh, material,
            GetPointAtDistance(leftOuter, RailMountInset));
        CreateMount(root, "Input Right Rail Mount", mountMesh, material,
            GetPointAtDistance(rightOuter, RailMountInset));

        CreateMount(root, "Left Output Outer Rail Mount", mountMesh, material,
            GetPointAtDistance(leftOuter, GetPathLength(leftOuter) - RailMountInset));
        CreateMount(root, "Right Output Outer Rail Mount", mountMesh, material,
            GetPointAtDistance(rightOuter, GetPathLength(rightOuter) - RailMountInset));
        CreateMount(root, "Left Output Inner Rail Mount", mountMesh, material,
            GetPointAtDistance(leftInner, GetPathLength(leftInner) - RailMountInset));
        CreateMount(root, "Right Output Inner Rail Mount", mountMesh, material,
            GetPointAtDistance(rightInner, GetPathLength(rightInner) - RailMountInset));

        CreateMount(root, "Fork Left Rail Mount", mountMesh, material, leftInner[0]);
        CreateMount(root, "Fork Right Rail Mount", mountMesh, material, rightInner[0]);
    }

    private static void CreateMount(
        Transform root,
        string objectName,
        Mesh mountMesh,
        Material material,
        Vector2 position)
    {
        CreateMeshPart(root, objectName, mountMesh, material, true, ToVector3(position, 0.90f));
    }

    private static void AddLineSegment(List<Vector2> path, Vector2 start, Vector2 end, int segments)
    {
        if (path.Count == 0)
        {
            path.Add(start);
        }
        for (int i = 1; i <= segments; i++)
        {
            path.Add(Vector2.Lerp(start, end, i / (float)segments));
        }
    }

    private static void AddCubicBezier(
        List<Vector2> path,
        Vector2 start,
        Vector2 controlA,
        Vector2 controlB,
        Vector2 end,
        int segments)
    {
        if (path.Count == 0)
        {
            path.Add(start);
        }
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            float inverse = 1f - t;
            path.Add(
                inverse * inverse * inverse * start +
                3f * inverse * inverse * t * controlA +
                3f * inverse * t * t * controlB +
                t * t * t * end);
        }
    }

    private static void AddQuadraticBezier(
        List<Vector2> path,
        Vector2 start,
        Vector2 control,
        Vector2 end,
        int segments)
    {
        if (path.Count == 0)
        {
            path.Add(start);
        }
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            float inverse = 1f - t;
            path.Add(inverse * inverse * start + 2f * inverse * t * control + t * t * end);
        }
    }

    private static float GetPathLength(List<Vector2> path)
    {
        float length = 0f;
        for (int i = 1; i < path.Count; i++)
        {
            length += Vector2.Distance(path[i - 1], path[i]);
        }
        return length;
    }

    private static Vector2 GetPointAtDistance(List<Vector2> path, float distance)
    {
        float remaining = Mathf.Clamp(distance, 0f, GetPathLength(path));
        for (int i = 1; i < path.Count; i++)
        {
            float segmentLength = Vector2.Distance(path[i - 1], path[i]);
            if (remaining <= segmentLength || i == path.Count - 1)
            {
                float t = segmentLength > 0.0001f ? remaining / segmentLength : 0f;
                return Vector2.Lerp(path[i - 1], path[i], Mathf.Clamp01(t));
            }
            remaining -= segmentLength;
        }
        return path[path.Count - 1];
    }

    private static void CreateMeshPart(Transform parent, string objectName, Mesh mesh, Material material, bool addCollider)
    {
        CreateMeshPart(parent, objectName, mesh, material, addCollider, Vector3.zero);
    }

    private static void CreateMeshPart(
        Transform parent,
        string objectName,
        Mesh mesh,
        Material material,
        bool addCollider,
        Vector3 position)
    {
        GameObject part = new GameObject(objectName);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = Vector3.one;

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

    private static void CreateConnection(Transform parent, string objectName, Vector3 position, Quaternion rotation)
    {
        GameObject connection = new GameObject(objectName);
        connection.transform.SetParent(parent, false);
        connection.transform.localPosition = position;
        connection.transform.localRotation = rotation;
        connection.transform.localScale = Vector3.one;
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

    private static void EnsureCounterClockwise(List<Vector2> points)
    {
        float area = 0f;
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 current = points[i];
            Vector2 next = points[(i + 1) % points.Count];
            area += current.x * next.y - next.x * current.y;
        }
        if (area < 0f)
        {
            points.Reverse();
        }
    }

    private static List<int> Triangulate(List<Vector2> points)
    {
        List<int> remaining = new List<int>();
        List<int> triangles = new List<int>();
        for (int i = 0; i < points.Count; i++)
        {
            remaining.Add(i);
        }

        int safety = points.Count * points.Count;
        while (remaining.Count > 3 && safety-- > 0)
        {
            bool removedEar = false;
            for (int i = 0; i < remaining.Count; i++)
            {
                int previous = remaining[(i - 1 + remaining.Count) % remaining.Count];
                int current = remaining[i];
                int next = remaining[(i + 1) % remaining.Count];
                if (Cross(points[previous], points[current], points[next]) <= 0.0001f)
                {
                    continue;
                }

                bool containsPoint = false;
                for (int test = 0; test < remaining.Count; test++)
                {
                    int candidate = remaining[test];
                    if (candidate == previous || candidate == current || candidate == next)
                    {
                        continue;
                    }
                    if (IsPointInTriangle(points[candidate], points[previous], points[current], points[next]))
                    {
                        containsPoint = true;
                        break;
                    }
                }
                if (containsPoint)
                {
                    continue;
                }

                triangles.Add(previous);
                triangles.Add(current);
                triangles.Add(next);
                remaining.RemoveAt(i);
                removedEar = true;
                break;
            }

            if (!removedEar)
            {
                break;
            }
        }

        if (remaining.Count == 3)
        {
            triangles.Add(remaining[0]);
            triangles.Add(remaining[1]);
            triangles.Add(remaining[2]);
        }
        return triangles;
    }

    private static bool IsPointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
    {
        float ab = Cross(a, b, point);
        float bc = Cross(b, c, point);
        float ca = Cross(c, a, point);
        return ab >= -0.0001f && bc >= -0.0001f && ca >= -0.0001f;
    }

    private static float Cross(Vector2 a, Vector2 b, Vector2 c)
    {
        Vector2 ab = b - a;
        Vector2 ac = c - a;
        return ab.x * ac.y - ab.y * ac.x;
    }

    private static Vector2 GetPathTangent(List<Vector2> path, int index)
    {
        if (index == 0)
        {
            return (path[1] - path[0]).normalized;
        }
        if (index == path.Count - 1)
        {
            return (path[index] - path[index - 1]).normalized;
        }
        return (path[index + 1] - path[index - 1]).normalized;
    }

    private static List<Vector2> MirrorPath(List<Vector2> source)
    {
        List<Vector2> mirrored = new List<Vector2>(source.Count);
        foreach (Vector2 point in source)
        {
            mirrored.Add(Mirror(point));
        }
        return mirrored;
    }

    private static List<Vector3> MirrorPath(List<Vector3> source)
    {
        List<Vector3> mirrored = new List<Vector3>(source.Count);
        foreach (Vector3 point in source)
        {
            mirrored.Add(new Vector3(-point.x, point.y, point.z));
        }
        return mirrored;
    }

    private static Vector2 Mirror(Vector2 point)
    {
        return new Vector2(-point.x, point.y);
    }

    private static Vector2 GetLeftNormal(Vector2 direction)
    {
        return new Vector2(-direction.y, direction.x);
    }

    private static Vector2 GetSymmetricSectorCorner(
        Vector2 directionA,
        Vector2 directionB,
        float offset)
    {
        return (directionA + directionB).normalized * (offset / SymmetricDiagonal);
    }

    private static Vector3 ToVector3(Vector2 point, float y)
    {
        return new Vector3(point.x, y, point.y);
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

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string parent = System.IO.Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        string folderName = System.IO.Path.GetFileName(folderPath);
        if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folderName))
        {
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
