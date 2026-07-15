using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds a separate straight track that follows the classic visual reference:
/// rounded wooden body, concave lane, and continuous metal guard tubes.
/// </summary>
public static class ClassicReferenceTrackBuilder
{
    private const string RootFolder = "Assets/Art/ClassicReferenceTrack";
    private const string MeshFolder = RootFolder + "/Meshes";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string ReferenceMaterialsFolder = RootFolder + "/Materials";
    private const string ReferenceWoodMaterialPath = ReferenceMaterialsFolder + "/ClassicReferenceWood.mat";
    private const string ReferenceMetalMaterialPath = ReferenceMaterialsFolder + "/ClassicReferenceMetal.mat";
    private const string ReferenceWoodTexturePath = RootFolder + "/ClassicReferenceWoodGrain.asset";
    private const string TrackRootName = "Straight Track Classic Reference";
    private static string activeMeshFolder = MeshFolder;
    [MenuItem("Tools/Ball Puzzle/Create Classic Reference Track In Scene")]
    public static void CreateClassicReferenceTrack()
    {
        EnsureFolder("Assets/Art");
        EnsureFolder(RootFolder);
        EnsureFolder(MeshFolder);
        EnsureFolder(ReferenceMaterialsFolder);
        EnsureFolder("Assets/Prefabs");

        Material woodMaterial = GetOrCreateReferenceWoodMaterial();
        Material metalMaterial = GetOrCreateReferenceMetalMaterial();
        if (woodMaterial == null || metalMaterial == null)
        {
            Debug.LogError("Classic track materials are missing. Create the original classic track first.");
            return;
        }

        CreateSceneHierarchy(woodMaterial, metalMaterial);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Ball Puzzle/Refresh Selected Classic Reference Track Meshes")]
    public static void RefreshSelectedClassicReferenceTrackMeshes()
    {
        GameObject root = GetSelectedRoot();
        if (root == null || !root.name.StartsWith(TrackRootName, System.StringComparison.Ordinal))
        {
            Debug.LogError("Select a classic reference straight track before refreshing its meshes.");
            return;
        }

        MeshFilter lane = root.transform.Find("Concave Wooden Lane")?.GetComponent<MeshFilter>();
        string meshPath = lane != null ? AssetDatabase.GetAssetPath(lane.sharedMesh) : string.Empty;
        string meshFolder = string.IsNullOrEmpty(meshPath) ? null : System.IO.Path.GetDirectoryName(meshPath)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(meshFolder))
        {
            Debug.LogError("The selected classic reference straight track has no generated lane mesh.");
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
        Debug.Log("Refreshed selected classic reference straight track meshes.");
    }

    private static GameObject BuildTrackHierarchy(string rootName, Material woodMaterial, Material metalMaterial)
    {
        GameObject root = new GameObject(rootName);

        CreateMeshPart(root.transform, "Rounded Wooden Body", GetRoundedBodyMesh(), woodMaterial, Vector3.zero, Quaternion.identity, true);
        CreateMeshPart(root.transform, "Concave Wooden Lane", GetLaneMesh(), woodMaterial, new Vector3(0f, 0.75f, 0f), Quaternion.identity, true);

        CreateMeshPart(root.transform, "Left Wooden Rim", GetRimMesh(-1f), woodMaterial,
            new Vector3(-1.02f, 0.64f, 0f), Quaternion.identity, true);
        CreateMeshPart(root.transform, "Right Wooden Rim", GetRimMesh(1f), woodMaterial,
            new Vector3(1.02f, 0.64f, 0f), Quaternion.identity, true);

        CreateMeshPart(root.transform, "Left Curved Metal Guard", GetRailMesh(-1f), metalMaterial, Vector3.zero, Quaternion.identity, true);
        CreateMeshPart(root.transform, "Right Curved Metal Guard", GetRailMesh(1f), metalMaterial, Vector3.zero, Quaternion.identity, true);

        CreateMeshPart(root.transform, "Left Front Rail Mount", GetRailMountMesh("Left Front Rail Mount"), metalMaterial,
            new Vector3(-0.98f, 0.90f, -3.45f), Quaternion.identity, true);
        CreateMeshPart(root.transform, "Left Back Rail Mount", GetRailMountMesh("Left Back Rail Mount"), metalMaterial,
            new Vector3(-0.98f, 0.90f, 3.45f), Quaternion.identity, true);
        CreateMeshPart(root.transform, "Right Front Rail Mount", GetRailMountMesh("Right Front Rail Mount"), metalMaterial,
            new Vector3(0.98f, 0.90f, -3.45f), Quaternion.identity, true);
        CreateMeshPart(root.transform, "Right Back Rail Mount", GetRailMountMesh("Right Back Rail Mount"), metalMaterial,
            new Vector3(0.98f, 0.90f, 3.45f), Quaternion.identity, true);

        CreateConnection(root.transform, "Start Connection", new Vector3(0f, 0f, -4f));
        CreateConnection(root.transform, "End Connection", new Vector3(0f, 0f, 4f));

        return root;
    }

    private static void CreateConnection(Transform parent, string objectName, Vector3 position)
    {
        GameObject connection = new GameObject(objectName);
        connection.transform.SetParent(parent, false);
        connection.transform.localPosition = position;
        connection.transform.localRotation = Quaternion.identity;
        connection.transform.localScale = Vector3.one;
    }

    private static void CreateSceneHierarchy(Material woodMaterial, Material metalMaterial)
    {
        if (!Application.isBatchMode)
        {
            EditorSceneManager.SaveOpenScenes();
        }
        EditorSceneManager.OpenScene(ScenePath);

        int nextTrackNumber = 1;
        foreach (GameObject sceneRoot in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (sceneRoot.name == TrackRootName)
            {
                nextTrackNumber = Mathf.Max(nextTrackNumber, 2);
                continue;
            }

            string numberedPrefix = TrackRootName + " ";
            if (sceneRoot.name.StartsWith(numberedPrefix, System.StringComparison.Ordinal) &&
                int.TryParse(sceneRoot.name.Substring(numberedPrefix.Length), out int existingNumber))
            {
                nextTrackNumber = Mathf.Max(nextTrackNumber, existingNumber + 1);
            }
        }

        string rootName = nextTrackNumber == 1 ? TrackRootName : TrackRootName + " " + nextTrackNumber;
        string meshFolderName = rootName.Replace(" ", "_").Replace("(", string.Empty).Replace(")", string.Empty);
        activeMeshFolder = AssetDatabase.GenerateUniqueAssetPath(MeshFolder + "/" + meshFolderName);
        EnsureFolder(activeMeshFolder);

        GameObject trackRoot = BuildTrackHierarchy(rootName, woodMaterial, metalMaterial);
        trackRoot.transform.position = new Vector3(-3.3f - (nextTrackNumber - 1) * 3f, 0.75f, 0f);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
    }

    private static void CreateMeshPart(Transform parent, string objectName, Mesh mesh, Material material, Vector3 position, Quaternion rotation, bool addMeshCollider)
    {
        GameObject part = new GameObject(objectName);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localRotation = rotation;

        MeshFilter filter = part.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;

        if (addMeshCollider)
        {
            MeshCollider collider = part.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }
    }

    private static void CreatePrimitivePart(Transform parent, string objectName, PrimitiveType primitiveType, Material material, Vector3 position, Quaternion rotation, Vector3 scale, bool addCollider)
    {
        GameObject part = new GameObject(objectName);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localRotation = rotation;
        part.transform.localScale = scale;

        MeshFilter filter = part.AddComponent<MeshFilter>();
        filter.sharedMesh = GetIndependentPrimitiveMesh(primitiveType, objectName);
        MeshRenderer renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;

        if (addCollider)
        {
            if (primitiveType == PrimitiveType.Cube)
            {
                part.AddComponent<BoxCollider>();
            }
            else
            {
                part.AddComponent<CapsuleCollider>();
            }
        }
    }

    private static Mesh GetRoundedBodyMesh()
    {
        string path = activeMeshFolder + "/RoundedWoodenBody.mesh";

        const int cornerSegments = 5;
        List<Vector3> bottom = CreateRoundedRectangleRing(2.35f, 8f, 0.18f, 0f, cornerSegments);
        List<Vector3> middle = CreateRoundedRectangleRing(2.35f, 8f, 0.18f, 0.1f, cornerSegments);
        List<Vector3> top = CreateRoundedRectangleRing(2.2f, 7.85f, 0.14f, 0.38f, cornerSegments);
        int ringSize = bottom.Count;

        List<Vector3> vertices = new List<Vector3>(ringSize * 3 + 1);
        vertices.AddRange(bottom);
        vertices.AddRange(middle);
        vertices.AddRange(top);
        int topCenter = vertices.Count;
        vertices.Add(new Vector3(0f, 0.38f, 0f));

        List<Vector2> uvs = new List<Vector2>(vertices.Count);
        for (int i = 0; i < vertices.Count; i++)
        {
            uvs.Add(new Vector2(vertices[i].x * 0.5f + 0.5f, vertices[i].z / 8f + 0.5f));
        }

        List<int> triangles = new List<int>();
        AddRingSides(triangles, 0, ringSize, ringSize);
        AddRingSides(triangles, ringSize, ringSize * 2, ringSize);
        for (int i = 0; i < ringSize; i++)
        {
            int next = (i + 1) % ringSize;
            triangles.Add(topCenter);
            triangles.Add(ringSize * 2 + next);
            triangles.Add(ringSize * 2 + i);
        }

        Mesh mesh = new Mesh { name = "RoundedWoodenBody" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return SaveOrUpdateMesh(path, mesh);
    }

    private static List<Vector3> CreateRoundedRectangleRing(float width, float length, float radius, float y, int segmentsPerCorner)
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
                float angle = (startAngle + step * 90f / segmentsPerCorner) * Mathf.Deg2Rad;
                Vector2 point = centers[corner] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                ring.Add(new Vector3(point.x, y, point.y));
            }
        }

        return ring;
    }

    private static void AddRingSides(List<int> triangles, int lowerStart, int upperStart, int ringSize)
    {
        for (int i = 0; i < ringSize; i++)
        {
            int next = (i + 1) % ringSize;
            triangles.Add(lowerStart + i);
            triangles.Add(upperStart + i);
            triangles.Add(upperStart + next);
            triangles.Add(lowerStart + i);
            triangles.Add(upperStart + next);
            triangles.Add(lowerStart + next);
        }
    }

    private static Mesh GetLaneMesh()
    {
        string path = activeMeshFolder + "/ConcaveWoodenLane.mesh";
        const int crossSegments = 28;
        const int lengthSegments = 48;
        const float width = 1.88f;
        const float length = 8f;
        List<Vector3> vertices = new List<Vector3>((crossSegments + 1) * (lengthSegments + 3));
        List<Vector2> uvs = new List<Vector2>((crossSegments + 1) * (lengthSegments + 3));
        for (int z = 0; z <= lengthSegments; z++)
        {
            float zT = z / (float)lengthSegments;
            float zPosition = Mathf.Lerp(-length * 0.5f, length * 0.5f, zT);
            for (int x = 0; x <= crossSegments; x++)
            {
                float t = x / (float)crossSegments;
                float xPosition = Mathf.Lerp(-width * 0.5f, width * 0.5f, t);
                float arch = 1f - (xPosition / (width * 0.5f)) * (xPosition / (width * 0.5f));
                vertices.Add(new Vector3(xPosition, -0.28f * Mathf.Clamp01(arch), zPosition));
                uvs.Add(new Vector2(t, zT));
            }
        }

        List<int> triangles = new List<int>(crossSegments * lengthSegments * 6 + crossSegments * 12);
        for (int z = 0; z < lengthSegments; z++)
        {
            for (int x = 0; x < crossSegments; x++)
            {
                int front = z * (crossSegments + 1) + x;
                int back = front + crossSegments + 1;
                triangles.Add(front);
                triangles.Add(back);
                triangles.Add(back + 1);
                triangles.Add(front);
                triangles.Add(back + 1);
                triangles.Add(front + 1);
            }
        }

        int frontBottomStart = vertices.Count;
        int backBottomStart = frontBottomStart + crossSegments + 1;
        for (int x = 0; x <= crossSegments; x++)
        {
            float t = x / (float)crossSegments;
            float xPosition = Mathf.Lerp(-width * 0.5f, width * 0.5f, t);
            vertices.Add(new Vector3(xPosition, -0.38f, -length * 0.5f));
            uvs.Add(new Vector2(t, 0f));
        }
        for (int x = 0; x <= crossSegments; x++)
        {
            float t = x / (float)crossSegments;
            float xPosition = Mathf.Lerp(-width * 0.5f, width * 0.5f, t);
            vertices.Add(new Vector3(xPosition, -0.38f, length * 0.5f));
            uvs.Add(new Vector2(t, 1f));
        }

        int backTopStart = lengthSegments * (crossSegments + 1);
        for (int x = 0; x < crossSegments; x++)
        {
            triangles.Add(x);
            triangles.Add(x + 1);
            triangles.Add(frontBottomStart + x + 1);
            triangles.Add(x);
            triangles.Add(frontBottomStart + x + 1);
            triangles.Add(frontBottomStart + x);

            triangles.Add(backTopStart + x);
            triangles.Add(backBottomStart + x + 1);
            triangles.Add(backTopStart + x + 1);
            triangles.Add(backTopStart + x);
            triangles.Add(backBottomStart + x);
            triangles.Add(backBottomStart + x + 1);
        }

        Mesh mesh = new Mesh { name = "ConcaveWoodenLane" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return SaveOrUpdateMesh(path, mesh);
    }

    private static Mesh GetRailMesh(float side)
    {
        string path = activeMeshFolder + (side < 0f ? "/LeftCurvedMetalGuard.mesh" : "/RightCurvedMetalGuard.mesh");
        List<Vector3> pathPoints = CreateRailPath(side);

        Mesh mesh = CreateTubeMesh(pathPoints, 0.075f, 14);
        mesh.name = side < 0f ? "LeftCurvedMetalGuard" : "RightCurvedMetalGuard";
        return SaveOrUpdateMesh(path, mesh);
    }

    private static List<Vector3> CreateRailPath(float side)
    {
        List<Vector3> points = new List<Vector3>();
        const int curveSegments = 12;
        const float curveRadius = 0.16f;
        const float socketEndHeight = 1.045f;
        const float bendStartHeight = 1.14f;
        const float straightHeight = bendStartHeight + curveRadius;

        points.Add(new Vector3(side * 0.98f, socketEndHeight, -4f));
        points.Add(new Vector3(side * 0.98f, socketEndHeight, -3.45f));
        for (int i = 0; i <= curveSegments; i++)
        {
            float angle = i / (float)curveSegments * Mathf.PI * 0.5f;
            points.Add(new Vector3(
                side * 0.98f,
                bendStartHeight + curveRadius * Mathf.Sin(angle),
                -3.45f + curveRadius * (1f - Mathf.Cos(angle))));
        }

        points.Add(new Vector3(side * 0.98f, straightHeight, 3.29f));
        for (int i = 1; i <= curveSegments; i++)
        {
            float angle = i / (float)curveSegments * Mathf.PI * 0.5f;
            points.Add(new Vector3(
                side * 0.98f,
                bendStartHeight + curveRadius * Mathf.Cos(angle),
                3.29f + curveRadius * Mathf.Sin(angle)));
        }
        points.Add(new Vector3(side * 0.98f, socketEndHeight, 3.45f));
        points.Add(new Vector3(side * 0.98f, socketEndHeight, 4f));

        return points;
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

        List<Vector3> vertices = new List<Vector3>((outerProfile.Length + 2) * radialSegments + 2);
        List<Vector2> uvs = new List<Vector2>((outerProfile.Length + 2) * radialSegments + 2);
        for (int ring = 0; ring < outerProfile.Length; ring++)
        {
            for (int segment = 0; segment < radialSegments; segment++)
            {
                float angle = segment / (float)radialSegments * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * outerProfile[ring].x, outerProfile[ring].y, Mathf.Sin(angle) * outerProfile[ring].x));
                uvs.Add(new Vector2(segment / (float)radialSegments, ring / (float)(outerProfile.Length - 1)));
            }
        }

        int innerTopStart = vertices.Count;
        for (int segment = 0; segment < radialSegments; segment++)
        {
            float angle = segment / (float)radialSegments * Mathf.PI * 2f;
            vertices.Add(new Vector3(Mathf.Cos(angle) * socketRadius, socketTop, Mathf.Sin(angle) * socketRadius));
            uvs.Add(new Vector2(segment / (float)radialSegments, 1f));
        }
        int innerBottomStart = vertices.Count;
        for (int segment = 0; segment < radialSegments; segment++)
        {
            float angle = segment / (float)radialSegments * Mathf.PI * 2f;
            vertices.Add(new Vector3(Mathf.Cos(angle) * socketRadius, socketBottom, Mathf.Sin(angle) * socketRadius));
            uvs.Add(new Vector2(segment / (float)radialSegments, 0.6f));
        }

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
        int outerTopStart = (outerProfile.Length - 1) * radialSegments;
        for (int segment = 0; segment < radialSegments; segment++)
        {
            int next = (segment + 1) % radialSegments;
            triangles.Add(bottomCenter);
            triangles.Add(segment);
            triangles.Add(next);

            triangles.Add(outerTopStart + segment);
            triangles.Add(innerTopStart + segment);
            triangles.Add(outerTopStart + next);
            triangles.Add(outerTopStart + next);
            triangles.Add(innerTopStart + segment);
            triangles.Add(innerTopStart + next);

            triangles.Add(innerTopStart + segment);
            triangles.Add(innerBottomStart + segment);
            triangles.Add(innerBottomStart + next);
            triangles.Add(innerTopStart + segment);
            triangles.Add(innerBottomStart + next);
            triangles.Add(innerTopStart + next);

            triangles.Add(socketCenter);
            triangles.Add(innerBottomStart + next);
            triangles.Add(innerBottomStart + segment);
        }

        Mesh mesh = new Mesh { name = objectName.Replace(" ", string.Empty) };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return SaveOrUpdateMesh(path, mesh);
    }

    private static Mesh CreateTubeMesh(List<Vector3> path, float radius, int sides)
    {
        List<Vector3> vertices = new List<Vector3>(path.Count * sides);
        List<Vector2> uvs = new List<Vector2>(path.Count * sides);
        List<int> triangles = new List<int>((path.Count - 1) * sides * 6);

        for (int i = 0; i < path.Count; i++)
        {
            Vector3 tangent = i == 0 ? path[1] - path[0] : i == path.Count - 1 ? path[i] - path[i - 1] : path[i + 1] - path[i - 1];
            tangent.Normalize();
            // The rail path is planar on YZ. Keeping X as the fixed side axis
            // prevents the tube frame from collapsing when an endpoint is vertical.
            Vector3 sideways = Vector3.right;
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

        // Rails intentionally have open ends on the shared connector plane.
        // The adjacent modular piece supplies the matching ring, avoiding a
        // visible circular cap at the joint.

        Mesh mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh GetRimMesh(float side)
    {
        string path = activeMeshFolder + (side < 0f ? "/LeftWoodenRim.mesh" : "/RightWoodenRim.mesh");
        Mesh mesh = CreateRoundedBoxMesh(0.30f, 0.52f, 8f, 0.065f);
        mesh.name = side < 0f ? "LeftWoodenRim" : "RightWoodenRim";
        return SaveOrUpdateMesh(path, mesh);
    }

    private static Mesh CreateRoundedBoxMesh(float width, float height, float length, float radius)
    {
        const int segments = 4;
        List<Vector3> bottom = CreateRoundedRectangleRing(width, length, radius, -height * 0.5f, segments);
        List<Vector3> top = CreateRoundedRectangleRing(width - 0.03f, length - 0.03f, Mathf.Max(0.01f, radius - 0.015f), height * 0.5f, segments);
        int ringSize = bottom.Count;
        List<Vector3> vertices = new List<Vector3>(ringSize * 2 + 2);
        vertices.AddRange(bottom);
        vertices.AddRange(top);
        int bottomCenter = vertices.Count;
        vertices.Add(new Vector3(0f, -height * 0.5f, 0f));
        int topCenter = vertices.Count;
        vertices.Add(new Vector3(0f, height * 0.5f, 0f));

        List<Vector2> uvs = new List<Vector2>(vertices.Count);
        for (int i = 0; i < vertices.Count; i++)
        {
            uvs.Add(new Vector2(vertices[i].x / width + 0.5f, vertices[i].z / length + 0.5f));
        }

        List<int> triangles = new List<int>();
        AddRingSides(triangles, 0, ringSize, ringSize);
        for (int i = 0; i < ringSize; i++)
        {
            int next = (i + 1) % ringSize;
            triangles.Add(bottomCenter);
            triangles.Add(i);
            triangles.Add(next);
            triangles.Add(topCenter);
            triangles.Add(ringSize + next);
            triangles.Add(ringSize + i);
        }

        Mesh mesh = new Mesh();
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

    private static Material GetOrCreateReferenceWoodMaterial()
    {
        Texture2D texture = GetOrCreateReferenceWoodTexture();
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ReferenceWoodMaterialPath);
        if (material != null)
        {
            return material;
        }

        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "ClassicReferenceWood" };
        material.SetColor("_BaseColor", new Color(0.86f, 0.55f, 0.24f));
        material.SetTexture("_BaseMap", texture);
        material.SetFloat("_Smoothness", 0.28f);
        AssetDatabase.CreateAsset(material, ReferenceWoodMaterialPath);
        return material;
    }

    private static Material GetOrCreateReferenceMetalMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ReferenceMetalMaterialPath);
        if (material != null)
        {
            return material;
        }

        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "ClassicReferenceMetal" };
        material.SetColor("_BaseColor", new Color(0.82f, 0.85f, 0.88f));
        material.SetFloat("_Metallic", 0.72f);
        material.SetFloat("_Smoothness", 0.78f);
        AssetDatabase.CreateAsset(material, ReferenceMetalMaterialPath);
        return material;
    }

    private static Texture2D GetOrCreateReferenceWoodTexture()
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ReferenceWoodTexturePath);
        if (texture != null)
        {
            return texture;
        }

        const int size = 512;
        texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "ClassicReferenceWoodGrain" };

        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        Color dark = new Color(0.55f, 0.32f, 0.12f);
        Color light = new Color(0.95f, 0.74f, 0.43f);
        for (int y = 0; y < size; y++)
        {
            float v = y / (float)(size - 1);
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)(size - 1);
                float wave = Mathf.Sin((u * 5.5f + Mathf.PerlinNoise(v * 1.2f, u * 2f) * 0.10f) * Mathf.PI * 2f) * 0.5f + 0.5f;
                float fineGrain = Mathf.PerlinNoise(u * 26f, v * 3f) * 0.12f;
                float grain = Mathf.Clamp01(0.58f + (wave - 0.5f) * 0.16f + fineGrain);
                texture.SetPixel(x, y, Color.Lerp(dark, light, grain));
            }
        }

        texture.Apply(true, false);
        AssetDatabase.CreateAsset(texture, ReferenceWoodTexturePath);
        return texture;
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

    private static Mesh GetIndependentPrimitiveMesh(PrimitiveType primitiveType, string objectName)
    {
        string path = activeMeshFolder + "/" + objectName.Replace(" ", string.Empty) + ".mesh";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null)
        {
            return mesh;
        }

        GameObject temporary = GameObject.CreatePrimitive(primitiveType);
        mesh = Object.Instantiate(temporary.GetComponent<MeshFilter>().sharedMesh);
        mesh.name = objectName.Replace(" ", string.Empty);
        Object.DestroyImmediate(temporary);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
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
