using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the reusable classic straight track prefab.
/// This script is editor-only and is not included in builds.
/// </summary>
public static class ClassicStraightTrackBuilder
{
    private const string ArtFolder = "Assets/Art/ClassicTrack";
    private const string MaterialFolder = ArtFolder + "/Materials";
    private const string MeshFolder = ArtFolder + "/Meshes";
    private const string MeshPath = ArtFolder + "/StraightTrackClassic.mesh";
    private const string WoodTexturePath = ArtFolder + "/WoodGrain.asset";
    private const string WoodMaterialPath = MaterialFolder + "/ClassicTrackWood.mat";
    private const string MetalMaterialPath = MaterialFolder + "/ClassicTrackMetal.mat";
    private const string PrefabPath = "Assets/Prefabs/StraightTrackClassic.prefab";

    [MenuItem("Tools/Ball Puzzle/Create Classic Straight Track")]
    public static void CreateClassicStraightTrack()
    {
        EnsureFolder("Assets/Art");
        EnsureFolder(ArtFolder);
        EnsureFolder("Assets/Prefabs");
        EnsureFolder(MaterialFolder);
        EnsureFolder(MeshFolder);

        Mesh deckMesh = GetOrCreateDeckMesh();
        Material woodMaterial = GetOrCreateWoodMaterial();
        Material metalMaterial = GetOrCreateMetalMaterial();

        GameObject prefabRoot = BuildPrefabContents(deckMesh, woodMaterial, metalMaterial);
        PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        Object.DestroyImmediate(prefabRoot);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static GameObject BuildPrefabContents(Mesh deckMesh, Material woodMaterial, Material metalMaterial)
    {
        GameObject root = new GameObject("Straight Track Classic");

        const float trackWidth = 2.1f;
        const float railHeight = 1.04f;
        const float railRadius = 0.11f;
        const float railLengthScale = 3.8f;
        float railX = trackWidth * 0.5f - railRadius;

        CreateWoodBlock(root.transform, "Wooden Base", new Vector3(0f, 0.28f, 0f), new Vector3(2.3f, 0.56f, 8f), woodMaterial);
        CreateLane(root.transform, deckMesh, woodMaterial);
        CreateWoodBlock(root.transform, "Left Wooden Frame", new Vector3(-1.12f, 0.36f, 0f), new Vector3(0.14f, 0.72f, 8f), woodMaterial);
        CreateWoodBlock(root.transform, "Right Wooden Frame", new Vector3(1.12f, 0.36f, 0f), new Vector3(0.14f, 0.72f, 8f), woodMaterial);
        CreateRail(root.transform, "Left Metal Guard", -railX, railHeight, railLengthScale, railRadius, metalMaterial);
        CreateRail(root.transform, "Right Metal Guard", railX, railHeight, railLengthScale, railRadius, metalMaterial);

        float[] supportPositions = { -2.65f, 0f, 2.65f };
        for (int index = 0; index < supportPositions.Length; index++)
        {
            float z = supportPositions[index];
            CreateRailSupport(root.transform, $"Left Metal Guard Support {index + 1}", -railX, z, metalMaterial);
            CreateRailSupport(root.transform, $"Right Metal Guard Support {index + 1}", railX, z, metalMaterial);
        }

        return root;
    }

    private static void CreateLane(Transform parent, Mesh mesh, Material material)
    {
        GameObject lane = new GameObject("Concave Wooden Lane");
        lane.transform.SetParent(parent, false);
        lane.transform.localPosition = new Vector3(0f, 0.55f, 0f);

        MeshFilter meshFilter = lane.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        MeshRenderer meshRenderer = lane.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;

        MeshCollider meshCollider = lane.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;
    }

    private static void CreateWoodBlock(Transform parent, string objectName, Vector3 position, Vector3 scale, Material material)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = objectName;
        block.transform.SetParent(parent, false);
        block.transform.localPosition = position;
        block.transform.localScale = scale;
        MeshFilter meshFilter = block.GetComponent<MeshFilter>();
        meshFilter.sharedMesh = GetOrCreateIndependentPrimitiveMesh(meshFilter.sharedMesh, objectName);
        block.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void CreateRail(Transform parent, string objectName, float x, float y, float lengthScale, float radius, Material material)
    {
        GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rail.name = objectName;
        rail.transform.SetParent(parent, false);
        rail.transform.localPosition = new Vector3(x, y, 0f);
        rail.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        rail.transform.localScale = new Vector3(radius * 2f, lengthScale, radius * 2f);
        MeshFilter meshFilter = rail.GetComponent<MeshFilter>();
        meshFilter.sharedMesh = GetOrCreateIndependentPrimitiveMesh(meshFilter.sharedMesh, objectName);
        rail.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void CreateRailSupport(Transform parent, string objectName, float x, float z, Material material)
    {
        GameObject support = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        support.name = objectName;
        support.transform.SetParent(parent, false);
        support.transform.localPosition = new Vector3(x, 0.82f, z);
        support.transform.localScale = new Vector3(0.25f, 0.19f, 0.25f);
        MeshFilter meshFilter = support.GetComponent<MeshFilter>();
        meshFilter.sharedMesh = GetOrCreateIndependentPrimitiveMesh(meshFilter.sharedMesh, objectName);
        support.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static Mesh GetOrCreateIndependentPrimitiveMesh(Mesh sourceMesh, string objectName)
    {
        string assetName = objectName.Replace(" ", string.Empty) + ".mesh";
        string assetPath = MeshFolder + "/" + assetName;
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        if (mesh != null)
        {
            return mesh;
        }

        mesh = Object.Instantiate(sourceMesh);
        mesh.name = objectName.Replace(" ", string.Empty);
        AssetDatabase.CreateAsset(mesh, assetPath);
        return mesh;
    }

    private static Mesh GetOrCreateDeckMesh()
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        bool isNew = mesh == null;

        const int segments = 16;
        const float width = 2.1f;
        const float length = 8f;
        const float thickness = 0.22f;
        const float grooveDepth = 0.18f;

        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        Vector2[] uvs = new Vector2[vertices.Length];

        for (int z = 0; z < 2; z++)
        {
            float zPosition = z == 0 ? -length * 0.5f : length * 0.5f;
            for (int x = 0; x <= segments; x++)
            {
                float t = x / (float)segments;
                float xPosition = Mathf.Lerp(-width * 0.5f, width * 0.5f, t);
                float curve = 1f - (xPosition / (width * 0.5f)) * (xPosition / (width * 0.5f));
                float topY = thickness - grooveDepth * Mathf.Clamp01(curve);

                int top = z * (segments + 1) + x;
                vertices[top] = new Vector3(xPosition, topY, zPosition);
                uvs[top] = new Vector2(t, z);
            }
        }

        int[] triangles = new int[segments * 6];
        int triangleIndex = 0;
        for (int x = 0; x < segments; x++)
        {
            int frontTop = x;
            int backTop = (segments + 1) + x;

            AddQuad(triangles, ref triangleIndex, frontTop, backTop, backTop + 1, frontTop + 1);
        }

        if (isNew)
        {
            mesh = new Mesh { name = "StraightTrackClassic" };
        }
        else
        {
            mesh.Clear();
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        if (isNew)
        {
            AssetDatabase.CreateAsset(mesh, MeshPath);
        }
        else
        {
            EditorUtility.SetDirty(mesh);
        }

        return mesh;
    }

    private static void AddQuad(int[] triangles, ref int index, int a, int b, int c, int d)
    {
        triangles[index++] = a;
        triangles[index++] = b;
        triangles[index++] = c;
        triangles[index++] = a;
        triangles[index++] = c;
        triangles[index++] = d;
    }

    private static Material GetOrCreateWoodMaterial()
    {
        Texture2D texture = GetOrCreateWoodTexture();
        Material material = AssetDatabase.LoadAssetAtPath<Material>(WoodMaterialPath);
        bool isNew = material == null;
        if (isNew)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                name = "ClassicTrackWood"
            };
        }

        material.SetColor("_BaseColor", new Color(0.9f, 0.62f, 0.31f));
        material.SetTexture("_BaseMap", texture);
        material.SetFloat("_Smoothness", 0.32f);
        if (isNew)
        {
            AssetDatabase.CreateAsset(material, WoodMaterialPath);
        }
        else
        {
            EditorUtility.SetDirty(material);
        }

        return material;
    }

    private static Material GetOrCreateMetalMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MetalMaterialPath);
        bool isNew = material == null;
        if (isNew)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                name = "ClassicTrackMetal"
            };
        }

        material.SetColor("_BaseColor", new Color(0.63f, 0.67f, 0.7f));
        material.SetFloat("_Metallic", 0.88f);
        material.SetFloat("_Smoothness", 0.68f);
        if (isNew)
        {
            AssetDatabase.CreateAsset(material, MetalMaterialPath);
        }
        else
        {
            EditorUtility.SetDirty(material);
        }

        return material;
    }

    private static Texture2D GetOrCreateWoodTexture()
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(WoodTexturePath);
        bool isNew = texture == null;

        const int size = 512;
        if (isNew)
        {
            texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "WoodGrain"
            };
        }

        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;

        Color dark = new Color(0.34f, 0.14f, 0.035f);
        Color light = new Color(0.95f, 0.62f, 0.24f);
        for (int y = 0; y < size; y++)
        {
            float v = y / (float)size;
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;
                float drift = Mathf.PerlinNoise(v * 1.5f, u * 5f) * 0.25f;
                float ring = Mathf.Sin((u * 12f + drift) * Mathf.PI * 2f) * 0.5f + 0.5f;
                float fineGrain = Mathf.PerlinNoise(u * 45f, v * 4f) * 0.18f;
                float plankLine = Mathf.Abs(Mathf.Repeat(u * 4f, 1f) - 0.5f) > 0.485f ? -0.18f : 0f;
                float grain = Mathf.Clamp01(ring * 0.45f + fineGrain + plankLine + 0.22f);
                texture.SetPixel(x, y, Color.Lerp(dark, light, grain));
            }
        }

        texture.Apply(true, false);
        if (isNew)
        {
            AssetDatabase.CreateAsset(texture, WoodTexturePath);
        }
        else
        {
            EditorUtility.SetDirty(texture);
        }

        return texture;
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
