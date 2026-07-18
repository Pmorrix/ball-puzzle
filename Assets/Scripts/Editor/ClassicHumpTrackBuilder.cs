using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds a classic straight track that rises smoothly at the centre and
/// returns to the standard connector height at both ends.
/// </summary>
public static class ClassicHumpTrackBuilder
{
    private const string RootFolder = "Assets/Art/ClassicReferenceTrack";
    private const string HumpFolder = RootFolder + "/HumpStraight";
    private const string MeshFolder = HumpFolder + "/Meshes";
    private const string ScenePath = "Assets/Scenes/PiecesCircuit.unity";
    private const string WoodMaterialPath = RootFolder + "/Materials/ClassicReferenceWood.mat";
    private const string MetalMaterialPath = RootFolder + "/Materials/ClassicReferenceMetal.mat";
    private const string SharedMountMeshPath = RootFolder + "/Meshes/LeftFrontRailMount.mesh";
    private const string RootName = "Hump Straight Classic Reference";

    private const float Length = 8f;
    private const float HalfLength = Length * 0.5f;
    private const float HumpHeight = 1.35f;
    private const float BodyHalfWidth = 1.175f;
    private const float BodyTopHalfWidth = 1.10f;
    private const float BodyTop = 0.38f;
    private const float LaneHalfWidth = 0.94f;
    private const float LaneEdgeHeight = 0.75f;
    private const float LaneDepth = 0.28f;
    private const float RimHalfWidth = 0.15f;
    private const float RimBottom = 0.38f;
    private const float RimTop = 0.90f;
    private const float RailOffset = 0.98f;
    private const float RailSocketHeight = 1.045f;
    private const float RailBendStartHeight = 1.14f;
    private const float RailHeight = 1.30f;
    private const float RailBendRadius = 0.16f;
    private const float RailMountInset = 0.55f;
    private const int LengthSegments = 64;

    [MenuItem("Tools/Ball Puzzle/Create Classic Hump Straight In Pieces Circuit")]
    public static void CreateClassicHumpStraightInPiecesCircuit()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (Application.isBatchMode)
        {
            activeScene = EditorSceneManager.OpenScene(ScenePath);
        }
        else if (activeScene.path != ScenePath)
        {
            Debug.LogError("Open PiecesCircuit before creating the classic hump straight.");
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
        EnsureFolder(HumpFolder);
        EnsureFolder(MeshFolder);

        Transform existing = raceRoad.transform.Find(RootName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        GameObject root = BuildHierarchy(woodMaterial, metalMaterial, mountMesh);
        root.transform.SetParent(raceRoad.transform, false);
        root.transform.localPosition = new Vector3(8.5f, 0f, 5f);
        root.transform.localRotation = Quaternion.identity;

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeGameObject = root;
        Debug.Log("Created the classic hump straight in PiecesCircuit.");
    }

    private static GameObject BuildHierarchy(Material woodMaterial, Material metalMaterial, Mesh mountMesh)
    {
        GameObject root = new GameObject(RootName);

        CreateMeshPart(root.transform, "Hump Wooden Body", CreateBodyMesh(), woodMaterial, true, Vector3.zero);
        CreateMeshPart(root.transform, "Hump Concave Lane", CreateLaneMesh(), woodMaterial, true, Vector3.zero);
        CreateMeshPart(root.transform, "Left Hump Wooden Rim", CreateRimMesh(-1f), woodMaterial, true, Vector3.zero);
        CreateMeshPart(root.transform, "Right Hump Wooden Rim", CreateRimMesh(1f), woodMaterial, true, Vector3.zero);
        CreateMeshPart(root.transform, "Left Hump Metal Guard", CreateRailMesh(-1f), metalMaterial, true, Vector3.zero);
        CreateMeshPart(root.transform, "Right Hump Metal Guard", CreateRailMesh(1f), metalMaterial, true, Vector3.zero);

        CreateRailMount(root.transform, "Left Start Rail Mount", mountMesh, metalMaterial, -1f, -HalfLength + RailMountInset);
        CreateRailMount(root.transform, "Left End Rail Mount", mountMesh, metalMaterial, -1f, HalfLength - RailMountInset);
        CreateRailMount(root.transform, "Right Start Rail Mount", mountMesh, metalMaterial, 1f, -HalfLength + RailMountInset);
        CreateRailMount(root.transform, "Right End Rail Mount", mountMesh, metalMaterial, 1f, HalfLength - RailMountInset);

        CreateConnection(root.transform, "Start Connection", new Vector3(0f, 0f, -HalfLength));
        CreateConnection(root.transform, "End Connection", new Vector3(0f, 0f, HalfLength));
        return root;
    }

    private static Mesh CreateBodyMesh()
    {
        Vector2[] profile =
        {
            new Vector2(-BodyHalfWidth + 0.18f, 0f),
            new Vector2(BodyHalfWidth - 0.18f, 0f),
            new Vector2(BodyHalfWidth, 0.10f),
            new Vector2(BodyTopHalfWidth, BodyTop),
            new Vector2(-BodyTopHalfWidth, BodyTop),
            new Vector2(-BodyHalfWidth, 0.10f)
        };

        Mesh mesh = CreateBentPrismMesh("HumpWoodenBody", profile);
        return SaveOrUpdateMesh(MeshFolder + "/HumpWoodenBody.mesh", mesh);
    }

    private static Mesh CreateBentPrismMesh(string meshName, Vector2[] profile)
    {
        int ringSize = profile.Length;
        List<Vector3> vertices = new List<Vector3>((LengthSegments + 1) * ringSize + 2);
        List<Vector2> uvs = new List<Vector2>((LengthSegments + 1) * ringSize + 2);
        List<int> triangles = new List<int>();

        for (int length = 0; length <= LengthSegments; length++)
        {
            float t = length / (float)LengthSegments;
            float z = Mathf.Lerp(-HalfLength, HalfLength, t);
            float elevation = EvaluateHump(t);
            for (int point = 0; point < ringSize; point++)
            {
                vertices.Add(new Vector3(profile[point].x, elevation + profile[point].y, z));
                uvs.Add(new Vector2(point / (float)ringSize, t));
            }
        }

        for (int length = 0; length < LengthSegments; length++)
        {
            int current = length * ringSize;
            int next = (length + 1) * ringSize;
            for (int point = 0; point < ringSize; point++)
            {
                int profileNext = (point + 1) % ringSize;
                AddQuad(triangles,
                    current + point,
                    current + profileNext,
                    next + profileNext,
                    next + point);
            }
        }

        Vector2 profileCenter = Vector2.zero;
        foreach (Vector2 point in profile)
        {
            profileCenter += point;
        }
        profileCenter /= ringSize;

        int startCenter = vertices.Count;
        vertices.Add(new Vector3(profileCenter.x, profileCenter.y, -HalfLength));
        uvs.Add(new Vector2(0.5f, 0.5f));
        int endCenter = vertices.Count;
        vertices.Add(new Vector3(profileCenter.x, profileCenter.y, HalfLength));
        uvs.Add(new Vector2(0.5f, 0.5f));
        int endRing = LengthSegments * ringSize;
        for (int point = 0; point < ringSize; point++)
        {
            int next = (point + 1) % ringSize;
            triangles.Add(startCenter);
            triangles.Add(next);
            triangles.Add(point);
            triangles.Add(endCenter);
            triangles.Add(endRing + point);
            triangles.Add(endRing + next);
        }

        Mesh mesh = new Mesh { name = meshName };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh CreateLaneMesh()
    {
        const int crossSegments = 28;
        int rowSize = crossSegments + 1;
        List<Vector3> vertices = new List<Vector3>((LengthSegments + 1) * rowSize);
        List<Vector2> uvs = new List<Vector2>((LengthSegments + 1) * rowSize);
        List<int> triangles = new List<int>(LengthSegments * crossSegments * 6);

        for (int length = 0; length <= LengthSegments; length++)
        {
            float lengthT = length / (float)LengthSegments;
            float z = Mathf.Lerp(-HalfLength, HalfLength, lengthT);
            float elevation = EvaluateHump(lengthT);
            for (int cross = 0; cross <= crossSegments; cross++)
            {
                float crossT = cross / (float)crossSegments;
                float x = Mathf.Lerp(-LaneHalfWidth, LaneHalfWidth, crossT);
                float normalized = x / LaneHalfWidth;
                float y = elevation + LaneEdgeHeight - LaneDepth + LaneDepth * normalized * normalized;
                vertices.Add(new Vector3(x, y, z));
                uvs.Add(new Vector2(crossT, lengthT));
            }
        }

        for (int length = 0; length < LengthSegments; length++)
        {
            for (int cross = 0; cross < crossSegments; cross++)
            {
                int a = length * rowSize + cross;
                int b = a + rowSize;
                int c = b + 1;
                int d = a + 1;
                AddQuad(triangles, a, b, c, d);
            }
        }

        Mesh mesh = new Mesh { name = "HumpConcaveLane" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return SaveOrUpdateMesh(MeshFolder + "/HumpConcaveLane.mesh", mesh);
    }

    private static Mesh CreateRimMesh(float side)
    {
        const float bevel = 0.04f;
        float center = side * RailOffset;
        Vector2[] profile =
        {
            new Vector2(center - RimHalfWidth + bevel, RimBottom),
            new Vector2(center + RimHalfWidth - bevel, RimBottom),
            new Vector2(center + RimHalfWidth, RimBottom + bevel),
            new Vector2(center + RimHalfWidth, RimTop - bevel),
            new Vector2(center + RimHalfWidth - bevel, RimTop),
            new Vector2(center - RimHalfWidth + bevel, RimTop),
            new Vector2(center - RimHalfWidth, RimTop - bevel),
            new Vector2(center - RimHalfWidth, RimBottom + bevel)
        };

        string meshName = side < 0f ? "LeftHumpWoodenRim" : "RightHumpWoodenRim";
        Mesh mesh = CreateBentPrismMesh(meshName, profile);
        return SaveOrUpdateMesh(MeshFolder + "/" + meshName + ".mesh", mesh);
    }

    private static Mesh CreateRailMesh(float side)
    {
        string meshName = side < 0f ? "LeftHumpMetalGuard" : "RightHumpMetalGuard";
        Mesh mesh = CreateTubeMesh(CreateRailPath(side), 0.075f, 14);
        mesh.name = meshName;
        return SaveOrUpdateMesh(MeshFolder + "/" + meshName + ".mesh", mesh);
    }

    private static List<Vector3> CreateRailPath(float side)
    {
        const int straightSamples = 8;
        const int bendSegments = 12;
        const int highSamples = 64;
        float startMountZ = -HalfLength + RailMountInset;
        float startHighZ = startMountZ + RailBendRadius;
        float endHighZ = HalfLength - RailMountInset - RailBendRadius;
        float endMountZ = HalfLength - RailMountInset;
        List<Vector3> points = new List<Vector3>();

        for (int i = 0; i <= straightSamples; i++)
        {
            float z = Mathf.Lerp(-HalfLength, startMountZ, i / (float)straightSamples);
            points.Add(new Vector3(side * RailOffset,
                RailSocketHeight + EvaluateHumpAtZ(z), z));
        }
        for (int i = 0; i <= bendSegments; i++)
        {
            float angle = i / (float)bendSegments * Mathf.PI * 0.5f;
            float z = startMountZ + RailBendRadius * (1f - Mathf.Cos(angle));
            float localHeight = RailBendStartHeight + RailBendRadius * Mathf.Sin(angle);
            points.Add(new Vector3(side * RailOffset, localHeight + EvaluateHumpAtZ(z), z));
        }
        for (int i = 1; i <= highSamples; i++)
        {
            float z = Mathf.Lerp(startHighZ, endHighZ, i / (float)highSamples);
            points.Add(new Vector3(side * RailOffset, RailHeight + EvaluateHumpAtZ(z), z));
        }
        for (int i = 1; i <= bendSegments; i++)
        {
            float angle = i / (float)bendSegments * Mathf.PI * 0.5f;
            float z = endHighZ + RailBendRadius * Mathf.Sin(angle);
            float localHeight = RailBendStartHeight + RailBendRadius * Mathf.Cos(angle);
            points.Add(new Vector3(side * RailOffset, localHeight + EvaluateHumpAtZ(z), z));
        }
        for (int i = 0; i <= straightSamples; i++)
        {
            float z = Mathf.Lerp(endMountZ, HalfLength, i / (float)straightSamples);
            points.Add(new Vector3(side * RailOffset,
                RailSocketHeight + EvaluateHumpAtZ(z), z));
        }
        return points;
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
                vertices.Add(path[i] +
                    (sideways * Mathf.Cos(angle) + upward * Mathf.Sin(angle)) * radius);
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

    private static void CreateRailMount(
        Transform root,
        string objectName,
        Mesh mountMesh,
        Material material,
        float side,
        float z)
    {
        Vector3 position = new Vector3(
            side * RailOffset,
            RimTop + EvaluateHumpAtZ(z),
            z);
        CreateMeshPart(root, objectName, mountMesh, material, true, position);
    }

    private static float EvaluateHumpAtZ(float z)
    {
        return EvaluateHump(Mathf.InverseLerp(-HalfLength, HalfLength, z));
    }

    private static float EvaluateHump(float t)
    {
        float sine = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
        return HumpHeight * sine * sine;
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

    private static void CreateConnection(Transform parent, string objectName, Vector3 position)
    {
        GameObject connection = new GameObject(objectName);
        connection.transform.SetParent(parent, false);
        connection.transform.localPosition = position;
        connection.transform.localRotation = Quaternion.identity;
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
