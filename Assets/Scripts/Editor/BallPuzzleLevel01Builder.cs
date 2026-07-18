using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class BallPuzzleLevel01Builder
{
    private const string ScenePath = "Assets/Scenes/Level01.unity";
    private const string StraightPrefabPath = "Assets/Prefabs/CircuitEditor/StraightPiece.prefab";
    private const string CurvePrefabPath = "Assets/Prefabs/CircuitEditor/Curve45RightPiece.prefab";
    private const string ArtFolder = "Assets/Art/Level01";
    private const string MaterialsFolder = ArtFolder + "/Materials";
    private const string MeshesFolder = ArtFolder + "/Meshes";
    private const string GridMeshPath = MeshesFolder + "/Level01Grid.asset";

    [MenuItem("Tools/Ball Puzzle/Build Level 01")]
    public static void BuildLevel01()
    {
        CircuitPiece straightPrefab = AssetDatabase.LoadAssetAtPath<CircuitPiece>(StraightPrefabPath);
        CircuitPiece curvePrefab = AssetDatabase.LoadAssetAtPath<CircuitPiece>(CurvePrefabPath);
        if (straightPrefab == null || curvePrefab == null)
        {
            throw new System.InvalidOperationException(
                "Level01 necesita los prefabs StraightPiece y Curve45RightPiece.");
        }

        EnsureFolder("Assets/Art");
        EnsureFolder(ArtFolder);
        EnsureFolder(MaterialsFolder);
        EnsureFolder(MeshesFolder);

        Material boardMaterial = GetOrCreateMaterial(
            MaterialsFolder + "/Board.mat",
            new Color(0.035f, 0.055f, 0.085f, 1f),
            0.05f,
            0.3f);
        Material frameMaterial = GetOrCreateMaterial(
            MaterialsFolder + "/Frame.mat",
            new Color(0.04f, 0.55f, 0.82f, 1f),
            0.35f,
            0.75f,
            new Color(0.02f, 0.28f, 0.55f));
        Material gridMaterial = GetOrCreateMaterial(
            MaterialsFolder + "/Grid.mat",
            new Color(0.10f, 0.32f, 0.48f, 1f),
            0f,
            0.4f,
            new Color(0.02f, 0.12f, 0.22f));
        Material markerMaterial = GetOrCreateMaterial(
            MaterialsFolder + "/StartMarker.mat",
            new Color(0.05f, 0.55f, 1f, 1f),
            0.15f,
            0.8f,
            new Color(0.02f, 0.40f, 1f));
        Material ballMaterial = GetOrCreateMaterial(
            MaterialsFolder + "/Ball.mat",
            new Color(0.035f, 0.045f, 0.065f, 1f),
            0.85f,
            0.9f);
        Material prizeMaterial = GetOrCreateMaterial(
            MaterialsFolder + "/Prize.mat",
            new Color(1f, 0.56f, 0.05f, 1f),
            0.65f,
            0.85f,
            new Color(1f, 0.22f, 0.01f));

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Camera camera = CreateCamera();
        CreateLighting();
        CreateGameSpace(boardMaterial, frameMaterial, gridMaterial);

        Transform startAnchor = CreateStartAnchor(markerMaterial);
        Transform spawnPoint = CreateSpawnPoint();
        CreateBallDropGuide(spawnPoint, markerMaterial, boardMaterial);
        Rigidbody ball = CreateBall(spawnPoint, ballMaterial);
        Transform prize = CreatePrize(prizeMaterial);
        CreateGoalGuide(prize, prizeMaterial, boardMaterial);

        GameObject controllerObject = new GameObject("Level 01 Controller");
        BallPuzzleLevelController controller = controllerObject.AddComponent<BallPuzzleLevelController>();
        controller.ConfigureForEditor(
            camera,
            straightPrefab,
            curvePrefab,
            ball,
            spawnPoint,
            prize,
            startAnchor);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            throw new System.InvalidOperationException("No se pudo guardar " + ScenePath);
        }

        AddSceneToBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeGameObject = controllerObject;
        Debug.Log("Level01 creada en " + ScenePath);
    }

    private static Camera CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.018f, 0.028f, 0.05f, 1f);
        camera.fieldOfView = 48f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 150f;
        cameraObject.transform.position = new Vector3(20f, 25f, -18f);
        cameraObject.transform.LookAt(new Vector3(2.5f, 0f, 5f));
        return camera;
    }

    private static void CreateLighting()
    {
        GameObject lightObject = new GameObject("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.35f;
        light.color = new Color(1f, 0.92f, 0.82f);
        light.shadows = LightShadows.Soft;
        lightObject.transform.rotation = Quaternion.Euler(52f, -28f, 0f);

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.25f, 0.34f, 0.50f);
        RenderSettings.ambientEquatorColor = new Color(0.09f, 0.14f, 0.22f);
        RenderSettings.ambientGroundColor = new Color(0.015f, 0.022f, 0.035f);
    }

    private static void CreateGameSpace(
        Material boardMaterial,
        Material frameMaterial,
        Material gridMaterial)
    {
        GameObject root = new GameObject("Game Space");

        CreatePrimitiveVisual(
            PrimitiveType.Cube,
            "Board",
            root.transform,
            new Vector3(0f, -1.1f, 4f),
            new Vector3(34f, 0.2f, 34f),
            boardMaterial);

        CreatePrimitiveVisual(
            PrimitiveType.Cube,
            "Frame North",
            root.transform,
            new Vector3(0f, -0.86f, 21f),
            new Vector3(34.5f, 0.28f, 0.22f),
            frameMaterial);
        CreatePrimitiveVisual(
            PrimitiveType.Cube,
            "Frame South",
            root.transform,
            new Vector3(0f, -0.86f, -13f),
            new Vector3(34.5f, 0.28f, 0.22f),
            frameMaterial);
        CreatePrimitiveVisual(
            PrimitiveType.Cube,
            "Frame East",
            root.transform,
            new Vector3(17f, -0.86f, 4f),
            new Vector3(0.22f, 0.28f, 34.5f),
            frameMaterial);
        CreatePrimitiveVisual(
            PrimitiveType.Cube,
            "Frame West",
            root.transform,
            new Vector3(-17f, -0.86f, 4f),
            new Vector3(0.22f, 0.28f, 34.5f),
            frameMaterial);

        GameObject gridObject = new GameObject("Placement Grid");
        gridObject.transform.SetParent(root.transform, false);
        gridObject.transform.localPosition = new Vector3(0f, -0.98f, 0f);
        MeshFilter filter = gridObject.AddComponent<MeshFilter>();
        filter.sharedMesh = GetOrCreateGridMesh();
        MeshRenderer renderer = gridObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = gridMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static Transform CreateStartAnchor(Material markerMaterial)
    {
        GameObject anchor = new GameObject("Start Anchor");
        anchor.transform.position = new Vector3(0f, 0f, -4f);
        anchor.transform.rotation = Quaternion.identity;

        GameObject marker = CreatePrimitiveVisual(
            PrimitiveType.Cylinder,
            "Start Marker",
            anchor.transform,
            new Vector3(0f, -0.03f, 0f),
            new Vector3(1.15f, 0.04f, 1.15f),
            markerMaterial);
        marker.transform.localRotation = Quaternion.identity;
        return anchor.transform;
    }

    private static Transform CreateSpawnPoint()
    {
        GameObject spawn = new GameObject("Ball Spawn Point");
        spawn.transform.position = new Vector3(0f, 1.05f, -3.45f);
        spawn.transform.rotation = Quaternion.identity;
        return spawn.transform;
    }

    private static Rigidbody CreateBall(Transform spawnPoint, Material material)
    {
        GameObject ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ballObject.name = "Ball";
        ballObject.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
        ballObject.transform.localScale = Vector3.one * 0.65f;
        ballObject.GetComponent<Renderer>().sharedMaterial = material;

        Rigidbody rigidbody = ballObject.AddComponent<Rigidbody>();
        rigidbody.mass = 1f;
        rigidbody.useGravity = true;
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rigidbody.isKinematic = true;
        return rigidbody;
    }

    private static void CreateBallDropGuide(
        Transform spawnPoint,
        Material markerMaterial,
        Material insetMaterial)
    {
        GameObject root = new GameObject("Ball Drop Guide");
        root.transform.position = new Vector3(
            spawnPoint.position.x,
            0f,
            spawnPoint.position.z);

        CreateRingMarker(
            "Ball Landing Ring",
            root.transform,
            new Vector3(0f, -0.03f, 0f),
            1.65f,
            0.9f,
            markerMaterial,
            insetMaterial);

        float guideHeight = spawnPoint.position.y;
        CreatePrimitiveVisual(
            PrimitiveType.Cylinder,
            "Ball Drop Line",
            root.transform,
            new Vector3(0f, guideHeight * 0.5f, 0f),
            new Vector3(0.08f, guideHeight * 0.5f, 0.08f),
            markerMaterial);

        CreateRingMarker(
            "Ball Spawn Ring",
            root.transform,
            new Vector3(0f, guideHeight, 0f),
            1.25f,
            0.78f,
            markerMaterial,
            insetMaterial);
    }

    private static Transform CreatePrize(Material material)
    {
        GameObject root = new GameObject("Prize");
        root.transform.position = new Vector3(7.12132f, 1.15f, 13.19239f);

        CreatePrimitiveVisual(
            PrimitiveType.Sphere,
            "Prize Core",
            root.transform,
            Vector3.zero,
            Vector3.one * 0.82f,
            material);

        for (int i = 0; i < 3; i++)
        {
            GameObject ray = CreatePrimitiveVisual(
                PrimitiveType.Cube,
                "Prize Ray " + (i + 1),
                root.transform,
                Vector3.zero,
                new Vector3(1.45f, 0.12f, 0.12f),
                material);
            ray.transform.localRotation = Quaternion.Euler(0f, i * 60f, 0f);
        }
        return root.transform;
    }

    private static void CreateGoalGuide(
        Transform prize,
        Material markerMaterial,
        Material insetMaterial)
    {
        GameObject root = new GameObject("Goal Guide");
        root.transform.position = new Vector3(prize.position.x, 0f, prize.position.z);

        CreateRingMarker(
            "Goal Ground Ring",
            root.transform,
            new Vector3(0f, -0.03f, 0f),
            2.35f,
            1.3f,
            markerMaterial,
            insetMaterial);

        float guideHeight = prize.position.y;
        CreatePrimitiveVisual(
            PrimitiveType.Cylinder,
            "Goal Height Line",
            root.transform,
            new Vector3(0f, guideHeight * 0.5f, 0f),
            new Vector3(0.1f, guideHeight * 0.5f, 0.1f),
            markerMaterial);
    }

    private static void CreateRingMarker(
        string name,
        Transform parent,
        Vector3 localPosition,
        float outerDiameter,
        float innerDiameter,
        Material outerMaterial,
        Material insetMaterial)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPosition;

        CreatePrimitiveVisual(
            PrimitiveType.Cylinder,
            "Outer Ring",
            root.transform,
            Vector3.zero,
            new Vector3(outerDiameter, 0.025f, outerDiameter),
            outerMaterial);
        CreatePrimitiveVisual(
            PrimitiveType.Cylinder,
            "Ring Inset",
            root.transform,
            new Vector3(0f, 0.03f, 0f),
            new Vector3(innerDiameter, 0.02f, innerDiameter),
            insetMaterial);
    }

    private static GameObject CreatePrimitiveVisual(
        PrimitiveType type,
        string name,
        Transform parent,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        GameObject visual = GameObject.CreatePrimitive(type);
        visual.name = name;
        visual.transform.SetParent(parent, false);
        visual.transform.localPosition = localPosition;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = localScale;
        visual.GetComponent<Renderer>().sharedMaterial = material;

        Collider collider = visual.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }
        return visual;
    }

    private static Mesh GetOrCreateGridMesh()
    {
        const int minimumX = -16;
        const int maximumX = 16;
        const int minimumZ = -12;
        const int maximumZ = 20;
        const int spacing = 2;

        List<Vector3> vertices = new List<Vector3>();
        for (int x = minimumX; x <= maximumX; x += spacing)
        {
            vertices.Add(new Vector3(x, 0f, minimumZ));
            vertices.Add(new Vector3(x, 0f, maximumZ));
        }
        for (int z = minimumZ; z <= maximumZ; z += spacing)
        {
            vertices.Add(new Vector3(minimumX, 0f, z));
            vertices.Add(new Vector3(maximumX, 0f, z));
        }

        Mesh generated = new Mesh { name = "Level01 Grid" };
        generated.SetVertices(vertices);
        generated.SetIndices(Enumerable.Range(0, vertices.Count).ToArray(), MeshTopology.Lines, 0);
        generated.RecalculateBounds();

        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(GridMeshPath);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(generated, GridMeshPath);
            return generated;
        }

        EditorUtility.CopySerialized(generated, existing);
        Object.DestroyImmediate(generated);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static Material GetOrCreateMaterial(
        string path,
        Color baseColor,
        float metallic,
        float smoothness,
        Color? emission = null)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            if (shader == null)
            {
                throw new System.InvalidOperationException("No se encontro un shader compatible.");
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", baseColor);
        material.SetColor("_Color", baseColor);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        if (emission.HasValue)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission.Value);
        }
        else
        {
            material.DisableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void AddSceneToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.All(scene => scene.path != ScenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
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
