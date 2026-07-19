using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class BallPuzzleLevel01Builder
{
    private const string ScenePath = "Assets/Scenes/Level01.unity";
    private const string StraightPrefabPath = "Assets/Prefabs/CircuitEditor/StraightPiece.prefab";
    private const string CurvePrefabPath = "Assets/Prefabs/CircuitEditor/Curve45RightPiece.prefab";
    private const string ArtFolder = "Assets/Art/Level01";
    private const string MaterialsFolder = ArtFolder + "/Materials";
    private const string MeshesFolder = ArtFolder + "/Meshes";
    private const string GridMeshPath = MeshesFolder + "/Level01Grid.asset";

    private sealed class LevelUiReferences
    {
        public GameObject BuildControlsPanel;
        public GameObject TestingControlsPanel;
        public GameObject ResultPanel;
        public Button StraightButton;
        public Button CurveButton;
        public Button TestButton;
        public Button ResetButton;
        public Button StopButton;
        public Button RetryButton;
        public Button EditButton;
        public Button ResultResetButton;
        public Text StraightButtonLabel;
        public Text CurveButtonLabel;
        public Text TestingLabel;
        public Text StatusLabel;
        public Text ResultTitleLabel;
        public Text ResultMessageLabel;
    }

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

        LevelUiReferences ui = CreateLevelUi();
        ConfigureControllerUi(controller, ui);

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

    [MenuItem("Tools/Ball Puzzle/Install Level 01 Canvas UI")]
    public static void InstallLevel01CanvasUi()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        BallPuzzleLevelController controller = Object.FindFirstObjectByType<BallPuzzleLevelController>();
        if (controller == null)
        {
            throw new System.InvalidOperationException(
                "Level01 no contiene un BallPuzzleLevelController.");
        }

        GameObject existingUi = GameObject.Find("Level UI");
        if (existingUi != null)
        {
            Object.DestroyImmediate(existingUi);
        }

        EventSystem existingEventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (existingEventSystem != null)
        {
            Object.DestroyImmediate(existingEventSystem.gameObject);
        }

        LevelUiReferences ui = CreateLevelUi();
        ConfigureControllerUi(controller, ui);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            throw new System.InvalidOperationException("No se pudo guardar " + ScenePath);
        }

        Selection.activeGameObject = ui.BuildControlsPanel.transform.root.gameObject;
        Debug.Log("Canvas UI instalada en " + ScenePath);
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

    private static LevelUiReferences CreateLevelUi()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            throw new System.InvalidOperationException("No se pudo cargar la fuente UI integrada.");
        }

        GameObject canvasObject = new GameObject(
            "Level UI",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject eventSystemObject = new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(InputSystemUIInputModule));
        eventSystemObject.transform.SetAsLastSibling();

        Color panelColor = new Color(0.018f, 0.035f, 0.065f, 0.92f);
        Color buttonColor = new Color(0.07f, 0.20f, 0.32f, 0.98f);
        Color textColor = new Color(0.91f, 0.95f, 1f, 1f);

        GameObject topBar = CreatePanel(
            "Top Bar",
            canvasObject.transform,
            panelColor,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -12f),
            new Vector2(-24f, 76f));

        CreateText(
            "Title",
            topBar.transform,
            "BALL PUZZLE — NIVEL 01",
            font,
            22,
            FontStyle.Bold,
            TextAnchor.MiddleLeft,
            Color.white,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(16f, -7f),
            new Vector2(430f, 30f));
        CreateText(
            "Legend",
            topBar.transform,
            "AZUL: caída de la bola  |  NARANJA: objetivo. Conecta ambos puntos.",
            font,
            14,
            FontStyle.Normal,
            TextAnchor.MiddleLeft,
            textColor,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(16f, -40f),
            new Vector2(650f, 25f));

        GameObject buildPanel = CreateLayoutPanel(
            "Build Controls",
            topBar.transform,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-14f, -13f),
            new Vector2(690f, 50f));
        Text straightLabel;
        Button straightButton = CreateButton(
            "Straight Button", buildPanel.transform, "RECTA  ×2", font, buttonColor, 150f, out straightLabel);
        Text curveLabel;
        Button curveButton = CreateButton(
            "Curve Button", buildPanel.transform, "CURVA 45°  ×1", font, buttonColor, 170f, out curveLabel);
        Text unusedLabel;
        Button testButton = CreateButton(
            "Test Button", buildPanel.transform, "PROBAR", font, buttonColor, 135f, out unusedLabel);
        Button resetButton = CreateButton(
            "Reset Button", buildPanel.transform, "REINICIAR", font, buttonColor, 155f, out unusedLabel);

        GameObject testingPanel = CreateLayoutPanel(
            "Testing Controls",
            topBar.transform,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-14f, -13f),
            new Vector2(430f, 50f));
        Text testingLabel = CreateText(
            "Testing Label",
            testingPanel.transform,
            "PRUEBA  0.0 s",
            font,
            18,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            Color.white,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(210f, 46f));
        LayoutElement testingLayout = testingLabel.gameObject.AddComponent<LayoutElement>();
        testingLayout.preferredWidth = 210f;
        testingLayout.preferredHeight = 46f;
        Button stopButton = CreateButton(
            "Stop Button", testingPanel.transform, "DETENER Y EDITAR", font, buttonColor, 200f, out unusedLabel);

        GameObject statusBar = CreatePanel(
            "Status Bar",
            canvasObject.transform,
            panelColor,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 12f),
            new Vector2(-24f, 48f));
        Text statusLabel = CreateText(
            "Status",
            statusBar.transform,
            "Elige una pieza y conéctala al punto azul.",
            font,
            15,
            FontStyle.Normal,
            TextAnchor.MiddleLeft,
            Color.white,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(-32f, -10f));

        GameObject resultPanel = CreatePanel(
            "Result Panel",
            canvasObject.transform,
            new Color(0.018f, 0.035f, 0.065f, 0.97f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(520f, 250f));
        Text resultTitle = CreateText(
            "Result Title",
            resultPanel.transform,
            "PRUEBA FINALIZADA",
            font,
            24,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            Color.white,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -24f),
            new Vector2(460f, 42f));
        Text resultMessage = CreateText(
            "Result Message",
            resultPanel.transform,
            "Resultado de la prueba.",
            font,
            16,
            FontStyle.Normal,
            TextAnchor.MiddleCenter,
            textColor,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -82f),
            new Vector2(460f, 54f));
        GameObject resultButtons = CreateLayoutPanel(
            "Result Buttons",
            resultPanel.transform,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 24f),
            new Vector2(470f, 52f));
        Button retryButton = CreateButton(
            "Retry Button", resultButtons.transform, "REINTENTAR", font, buttonColor, 145f, out unusedLabel);
        Button editButton = CreateButton(
            "Edit Button", resultButtons.transform, "EDITAR", font, buttonColor, 145f, out unusedLabel);
        Button resultResetButton = CreateButton(
            "Result Reset Button", resultButtons.transform, "REINICIAR", font, buttonColor, 145f, out unusedLabel);

        testingPanel.SetActive(false);
        resultPanel.SetActive(false);

        return new LevelUiReferences
        {
            BuildControlsPanel = buildPanel,
            TestingControlsPanel = testingPanel,
            ResultPanel = resultPanel,
            StraightButton = straightButton,
            CurveButton = curveButton,
            TestButton = testButton,
            ResetButton = resetButton,
            StopButton = stopButton,
            RetryButton = retryButton,
            EditButton = editButton,
            ResultResetButton = resultResetButton,
            StraightButtonLabel = straightLabel,
            CurveButtonLabel = curveLabel,
            TestingLabel = testingLabel,
            StatusLabel = statusLabel,
            ResultTitleLabel = resultTitle,
            ResultMessageLabel = resultMessage
        };
    }

    private static void ConfigureControllerUi(
        BallPuzzleLevelController controller,
        LevelUiReferences ui)
    {
        controller.ConfigureUiForEditor(
            ui.BuildControlsPanel,
            ui.TestingControlsPanel,
            ui.ResultPanel,
            ui.StraightButton,
            ui.CurveButton,
            ui.TestButton,
            ui.ResetButton,
            ui.StopButton,
            ui.RetryButton,
            ui.EditButton,
            ui.ResultResetButton,
            ui.StraightButtonLabel,
            ui.CurveButtonLabel,
            ui.TestingLabel,
            ui.StatusLabel,
            ui.ResultTitleLabel,
            ui.ResultMessageLabel);
    }

    private static GameObject CreatePanel(
        string name,
        Transform parent,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);
        ConfigureRect(panel.GetComponent<RectTransform>(), anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
        Image image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return panel;
    }

    private static GameObject CreateLayoutPanel(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        panel.transform.SetParent(parent, false);
        ConfigureRect(panel.GetComponent<RectTransform>(), anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
        HorizontalLayoutGroup layout = panel.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return panel;
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        string label,
        Font font,
        Color normalColor,
        float width,
        out Text labelText)
    {
        GameObject buttonObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = normalColor;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor = new Color(0.10f, 0.38f, 0.58f, 1f);
        colors.pressedColor = new Color(0.04f, 0.13f, 0.22f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.10f, 0.12f, 0.15f, 0.75f);
        button.colors = colors;

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.preferredHeight = 46f;

        labelText = CreateText(
            "Label",
            buttonObject.transform,
            label,
            font,
            15,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            Color.white,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero);
        labelText.raycastTarget = false;
        return button;
    }

    private static Text CreateText(
        string name,
        Transform parent,
        string value,
        Font font,
        int fontSize,
        FontStyle fontStyle,
        TextAnchor alignment,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        ConfigureRect(textObject.GetComponent<RectTransform>(), anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
        Text text = textObject.GetComponent<Text>();
        text.text = value;
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static void ConfigureRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.localScale = Vector3.one;
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
