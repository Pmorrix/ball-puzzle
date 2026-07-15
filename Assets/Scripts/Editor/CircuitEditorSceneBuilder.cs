using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CircuitEditorSceneBuilder
{
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string EditorScenePath = "Assets/Scenes/edit circuit.unity";
    private const string PrefabFolder = "Assets/Prefabs/CircuitEditor";
    private const string StartPrefabPath = PrefabFolder + "/StartPiece.prefab";
    private const string StraightPrefabPath = PrefabFolder + "/StraightPiece.prefab";
    private const string CurvePrefabPath = PrefabFolder + "/Curve45RightPiece.prefab";

    [MenuItem("Tools/Ball Puzzle/Build Circuit Editor Scene")]
    public static void Build()
    {
        EnsureFolder("Assets/Prefabs");
        EnsureFolder(PrefabFolder);

        Scene sourceScene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
        GameObject raceRoad = sourceScene.GetRootGameObjects().FirstOrDefault(root => root.name == "RaceRoad");
        if (raceRoad == null)
        {
            throw new System.InvalidOperationException("RaceRoad no existe en SampleScene.");
        }

        Transform startSource = raceRoad.transform.Find("Start_Straight_East");
        Transform straightSource = raceRoad.transform.Find("Straight_West");
        Transform curveSource = raceRoad.transform.Find("Curve_North_West");
        if (startSource == null || straightSource == null || curveSource == null)
        {
            throw new System.InvalidOperationException("No se encontraron las piezas base dentro de RaceRoad.");
        }

        CircuitPiece startPrefab = CreateStraightPrefab(
            startSource.gameObject,
            "Start Piece",
            CircuitPieceType.Start,
            "Salida",
            StartPrefabPath);
        CircuitPiece straightPrefab = CreateStraightPrefab(
            straightSource.gameObject,
            "Straight Piece",
            CircuitPieceType.Straight,
            "Recta",
            StraightPrefabPath);
        CircuitPiece curvePrefab = CreateRightCurvePrefab(curveSource.gameObject);

        CreateEditorScene(startPrefab, straightPrefab, curvePrefab);
        AddSceneToBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Circuit editor created at " + EditorScenePath);
    }

    private static CircuitPiece CreateStraightPrefab(
        GameObject source,
        string rootName,
        CircuitPieceType pieceType,
        string displayName,
        string prefabPath)
    {
        GameObject root = CreateWrapperWithVisual(source, rootName);
        Transform start = CreateConnector(root.transform, "Connector Start", new Vector3(0f, 0f, -4f), 180f);
        Transform end = CreateConnector(root.transform, "Connector End", new Vector3(0f, 0f, 4f), 0f);
        CircuitPiece piece = root.AddComponent<CircuitPiece>();
        piece.ConfigureForEditor(pieceType, displayName, new[] { start, end }, 0);
        return SavePrefab(root, prefabPath);
    }

    private static CircuitPiece CreateRightCurvePrefab(GameObject source)
    {
        GameObject root = CreateWrapperWithVisual(source, "Curve 45 Right Piece");
        Transform start = CreateConnector(root.transform, "Connector Start", Vector3.zero, 180f);
        Transform end = CreateConnector(
            root.transform,
            "Connector End",
            new Vector3(1.4644661f, 0f, 3.535534f),
            45f);
        CircuitPiece piece = root.AddComponent<CircuitPiece>();
        piece.ConfigureForEditor(CircuitPieceType.Curve45Right, "Curva 45 derecha", new[] { start, end }, 0);
        return SavePrefab(root, CurvePrefabPath);
    }

    private static GameObject CreateWrapperWithVisual(GameObject source, string rootName)
    {
        GameObject root = new GameObject(rootName);
        GameObject visual = Object.Instantiate(source);
        visual.name = "Visual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = source.transform.localScale;

        DestroyDirectChild(visual.transform, "Start Connection");
        DestroyDirectChild(visual.transform, "End Connection");
        return root;
    }

    private static void DestroyDirectChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            Object.DestroyImmediate(child.gameObject);
        }
    }

    private static Transform CreateConnector(Transform parent, string name, Vector3 position, float yaw)
    {
        GameObject connector = new GameObject(name);
        connector.transform.SetParent(parent, false);
        connector.transform.localPosition = position;
        connector.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return connector.transform;
    }

    private static CircuitPiece SavePrefab(GameObject root, string path)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        if (prefab == null)
        {
            throw new System.InvalidOperationException("No se pudo crear el prefab " + path);
        }
        return prefab.GetComponent<CircuitPiece>();
    }

    private static void CreateEditorScene(
        CircuitPiece startPrefab,
        CircuitPiece straightPrefab,
        CircuitPiece curvePrefab)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject cameraObject = new GameObject("Build Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.055f, 0.075f, 0.095f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 18f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100f;
        cameraObject.transform.position = new Vector3(17f, 24f, -20f);
        cameraObject.transform.LookAt(new Vector3(0f, 0f, 0f));

        GameObject lightObject = new GameObject("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.25f;
        light.shadows = LightShadows.Soft;
        lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.48f, 0.52f, 0.58f);
        RenderSettings.ambientEquatorColor = new Color(0.22f, 0.24f, 0.27f);
        RenderSettings.ambientGroundColor = new Color(0.08f, 0.09f, 0.10f);

        GameObject editorRoot = new GameObject("Circuit Editor");
        CircuitEditorController controller = editorRoot.AddComponent<CircuitEditorController>();
        controller.ConfigureForEditor(camera, startPrefab, straightPrefab, curvePrefab);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, EditorScenePath);
    }

    private static void AddSceneToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.All(scene => scene.path != EditorScenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(EditorScenePath, true));
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
